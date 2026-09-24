using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventBooking.Infrastructure.Email;

/// <summary>Claims pending outbox rows and sends them, with backoff and reclaim.</summary>
/// <param name="scopes">The scopes.</param>
/// <param name="transport">The transport.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="links">The portal links.</param>
/// <param name="logger">The logger.</param>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    IEmailTransport transport,
    ITokenService tokens,
    AttendeePortalOptions links,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    /// <summary>Runs one claim-and-send pass.</summary>
    /// <param name="ct">The cancellation token.</param>
    public async Task<int> DispatchOnceAsync(CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        List<(Guid Id, int ClaimCount)> claimed;
        using (var claimScope = scopes.CreateScope())
        {
            var claimContext = claimScope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var claimClock = claimScope.ServiceProvider.GetRequiredService<IClock>();
            claimed = await ClaimAsync(claimContext, correlationId, claimClock.UtcNow, ct);
        }

        var sent = 0;
        foreach (var (id, claimCount) in claimed)
        {
            // A scope per row: one failed save leaves its entities in the change tracker, and
            // a shared context would replay that failure into every later row of the batch.
            using var scope = scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
            try
            {
                // Renew the lease before the send: a batch of slow sends can outlast it, and
                // an expired lease lets a second dispatcher claim and re-send this row. If the
                // row was already reclaimed, its count moved on and it is no longer ours to
                // send. The count is the ownership token because the correlation is
                // write-once: a staged request identifier survives every claim.
                var renewed = await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE email_log SET claimed_at = {now} WHERE id = {id} AND claim_count = {claimCount} AND status = 3",
                    ct);
                if (renewed == 0) continue;

                var row = await context.EmailLogs.SingleAsync(e => e.Id == id, ct);
                try
                {
                    await SendRowAsync(scope, row, now, ct);
                    sent++;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "Outbox send failed for delivery {DeliveryId}.", id);
                    await MarkTransientAsync(scope, row, now, ct);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // The failure bookkeeping itself failed: the row stays claimed and is
                // reclaimed after the lease. Later rows still run in their own scopes.
                logger.LogError(ex, "Outbox bookkeeping failed for delivery {DeliveryId}.", id);
            }
        }

        return sent;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch pass failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private static async Task<List<(Guid Id, int ClaimCount)>> ClaimAsync(
        EventBookingDbContext context, string correlationId, DateTimeOffset now, CancellationToken ct)
    {
        await context.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)context.Database.GetDbConnection();
            await using var command = new NpgsqlCommand(ClaimQuery.Sql, connection);
            command.Transaction =
                context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
            command.Parameters.Add(new NpgsqlParameter("@now", now));
            command.Parameters.Add(new NpgsqlParameter("@correlationId", correlationId));

            var claimed = new List<(Guid Id, int ClaimCount)>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                claimed.Add((reader.GetGuid(0), reader.GetInt32(1)));
            }

            return claimed.OrderBy(x => x.Id).ToList();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private async Task SendRowAsync(IServiceScope scope, EmailLog row, DateTimeOffset now, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        var message = await RenderAsync(scope, row, ct);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        EmailSendOutcome outcome;
        try
        {
            outcome = await transport.SendAsync(
                message.ToAddress, message.Subject, message.TextBody, message.HtmlBody, linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            outcome = EmailSendOutcome.TransientFailure;
        }

        if (outcome == EmailSendOutcome.Sent)
        {
            row.MarkSent(now);
            if (row.TemplateName is EmailTemplate.AttendeeInvite or EmailTemplate.AttendeeReinvite)
                audit.Record(AuditEntityTypes.Invite, row.InviteId!.Value, AuditAction.InviteSent,
                    ActorType.System, null, $"invite {row.InviteId}");
        }
        else if (outcome == EmailSendOutcome.TransientFailure)
        {
            if (row.ClaimCount >= ClaimQuery.MaxClaims)
                row.MarkFailed(now);
            else
                row.SetNotBefore(now.AddMinutes(Math.Min(60, 1 << row.ClaimCount)));
        }
        else
        {
            row.MarkFailed(now);
        }

        await context.SaveChangesAsync(ct);
    }

    private async Task MarkTransientAsync(
        IServiceScope scope, EmailLog row, DateTimeOffset now, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        if (row.ClaimCount >= ClaimQuery.MaxClaims)
            row.MarkFailed(now);
        else
            row.SetNotBefore(now.AddMinutes(Math.Min(60, 1 << row.ClaimCount)));
        await context.SaveChangesAsync(ct);
    }

    private async Task<EmailMessage> RenderAsync(
        IServiceScope scope, EmailLog row, CancellationToken ct)
    {
        var attendees = scope.ServiceProvider.GetRequiredService<IAttendeeRepository>();
        var attendee = await attendees.GetAsync(row.AttendeeId, ct)
            ?? throw new InvalidOperationException($"Attendee {row.AttendeeId} is gone.");

        var context = row.TemplateName switch
        {
            EmailTemplate.AttendeeInvite or EmailTemplate.AttendeeReinvite =>
                await InviteContextAsync(scope, row, ct),
            EmailTemplate.BookingConfirmation =>
                await BookingContextAsync(scope, row, attendee.Id, ct),
            EmailTemplate.EventCancelledRebookingNeeded =>
                await CancellationContextAsync(scope, row, attendee.Id, ct),
            _ => throw new InvalidOperationException($"Template {row.TemplateName} cannot be rendered."),
        };

        return EmailComposer.Compose(row.TemplateName.ToString(), context) with
        {
            AttendeeId = attendee.Id,
            ToAddress = attendee.Email,
            ToName = attendee.Name,
            DeliveryId = row.Id,
        };
    }

    private async Task<EmailContext> InviteContextAsync(
        IServiceScope scope, EmailLog row, CancellationToken ct)
    {
        var invites = scope.ServiceProvider.GetRequiredService<IInviteRepository>();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var invite = row.InviteId is { } inviteId
            ? await invites.GetAsync(inviteId, ct)
            : null;
        if (invite is null || !invite.IsUsableAt(clock.UtcNow))
            throw new InvalidOperationException($"Invite {row.InviteId} is no longer usable.");

        var options = new List<Event>();
        foreach (var eventId in invite.OfferedEventIds)
        {
            var option = await events.GetAsync(eventId, ct);
            if (option is not null)
            {
                options.Add(option);
            }
        }

        if (options.Count == 0)
            throw new InvalidOperationException($"Invite {invite.Id} has no live options.");

        // One window line for a multi-option invite: the earliest option sets the scene and
        // the book link carries every option.
        var first = options.OrderBy(option => option.Window).First();
        var (locationName, locationAddress, windowText) =
            await WindowTextAsync(scope, first, ct);
        var issued = tokens.Issue(TokenPurpose.Book, invite.Id, invite.TokenVersion);
        return new EmailContext(
            invite.RequiredAppointmentTypeIds.Select(AppointmentTypeIds.CodeOf).ToList(),
            locationName,
            locationAddress,
            windowText,
            $"{links.BaseUrl}/book/{issued}",
            links.CoordinatorContact,
            false,
            invite.RecoveryOfBookingId.HasValue,
            invite.RequiredAppointmentTypeIds.Count);
    }

    private async Task<EmailContext> BookingContextAsync(
        IServiceScope scope, EmailLog row, Guid attendeeId, CancellationToken ct)
    {
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var appointments =
            scope.ServiceProvider.GetRequiredService<IBookingAppointmentRepository>();

        var booking = row.BookingId is { } bookingId
            ? await bookings.GetAsync(bookingId, ct)
            : null;
        if (booking is null || booking.Status != BookingStatus.Active || booking.AttendeeId != attendeeId)
            throw new InvalidOperationException($"Booking {row.BookingId} is no longer available.");

        var eventItem = await events.GetAsync(booking.EventId, ct)
            ?? throw new InvalidOperationException($"Event {booking.EventId} is gone.");
        var snapshot = (await appointments.ListForBookingAsync(booking.Id, ct))
            .Select(appointment => appointment.AppointmentTypeId)
            .ToList();
        if (snapshot.Count == 0)
            throw new InvalidOperationException($"Booking {booking.Id} has no appointments to name.");

        var (locationName, locationAddress, windowText) =
            await WindowTextAsync(scope, eventItem, ct);
        var issued = tokens.Issue(TokenPurpose.Manage, booking.Id, booking.ManageTokenVersion);
        return new EmailContext(
            snapshot.Select(AppointmentTypeIds.CodeOf).ToList(),
            locationName,
            locationAddress,
            windowText,
            $"{links.BaseUrl}/manage/{issued}",
            links.CoordinatorContact,
            false,
            false,
            snapshot.Count);
    }

    private async Task<EmailContext> CancellationContextAsync(
        IServiceScope scope, EmailLog row, Guid attendeeId, CancellationToken ct)
    {
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var events = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var appointments =
            scope.ServiceProvider.GetRequiredService<IBookingAppointmentRepository>();
        var invites = scope.ServiceProvider.GetRequiredService<IInviteRepository>();

        // The booking is cancelled, not active: existence is the check, matching the
        // dashboard's staged-booking rule.
        var booking = row.BookingId is { } bookingId
            ? await bookings.GetAsync(bookingId, ct)
            : null;
        if (booking is null || booking.AttendeeId != attendeeId)
            throw new InvalidOperationException($"Booking {row.BookingId} is no longer available.");

        var eventItem = row.EventId is { } eventId
            ? await events.GetAsync(eventId, ct)
            : null;
        if (eventItem is null)
            throw new InvalidOperationException($"Event {row.EventId} is gone.");

        var snapshot = (await appointments.ListForBookingAsync(booking.Id, ct))
            .Select(appointment => appointment.AppointmentTypeId)
            .ToList();
        if (snapshot.Count == 0)
            throw new InvalidOperationException($"Booking {booking.Id} has no appointments to name.");

        // The row does not record whether the cancellation issued a replacement, so the
        // ending is derived: a pending recovery invite for this booking means one was
        // created. Issued and staged in one transaction, it is still pending in every
        // realistic send window.
        var pending = await invites.GetPendingForAttendeeAsync(attendeeId, ct);
        var replacementCreated =
            pending?.RecoveryOfBookingId is { } recoveryOf && recoveryOf == booking.Id;

        var (locationName, locationAddress, windowText) =
            await WindowTextAsync(scope, eventItem, ct);
        return new EmailContext(
            snapshot.Select(AppointmentTypeIds.CodeOf).ToList(),
            locationName,
            locationAddress,
            windowText,
            string.Empty,
            links.CoordinatorContact,
            replacementCreated,
            false,
            snapshot.Count);
    }

    private async Task<(string Name, string Address, string WindowText)> WindowTextAsync(
        IServiceScope scope, Event eventItem, CancellationToken ct)
    {
        var locations = scope.ServiceProvider.GetRequiredService<ILocationRepository>();
        var zones = scope.ServiceProvider.GetRequiredService<IEventWindowZones>();

        var location = await locations.GetAsync(eventItem.LocationId, ct)
            ?? throw new InvalidOperationException($"Location {eventItem.LocationId} is gone.");
        var window = eventItem.Window;
        var abbreviation = zones.AbbreviationOf(
            window.StartInstant(zones, location.TimeZoneId), location.TimeZoneId);
        return (location.Name, location.Address, EmailComposer.FormatWindow(
            window.Date, window.StartTime, window.EndTime, location.Name, abbreviation));
    }
}
