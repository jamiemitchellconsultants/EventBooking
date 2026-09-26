using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using EventBooking.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EventBooking.Infrastructure.Tests.Email;

/// <summary>A provider-accepted send captured at the transport boundary.</summary>
/// <param name="Recipient">The recipient address.</param>
/// <param name="Subject">The subject.</param>
/// <param name="TextBody">The text body.</param>
/// <param name="HtmlBody">The HTML body.</param>
public sealed record DispatchedMail(string Recipient, string Subject, string TextBody, string HtmlBody);

/// <summary>Stands in for the mail provider with a sticky settable outcome.</summary>
public sealed class HarnessTransport : IEmailTransport
{
    /// <summary>Sends the provider accepted.</summary>
    public List<DispatchedMail> Sent { get; } = [];

    /// <summary>The outcome every send reports until reassigned.</summary>
    public EmailSendOutcome Next { get; set; } = EmailSendOutcome.Sent;

    /// <summary>When set, the next send cancels this source and observes the cancellation,
    /// as a host shutting down mid-send would.</summary>
    public CancellationTokenSource? CancelDuringSend { get; set; }

    /// <inheritdoc />
    public Task<EmailSendOutcome> SendAsync(
        string recipient, string subject, string textBody, string htmlBody,
        CancellationToken cancellationToken)
    {
        if (CancelDuringSend is { } shutdown)
        {
            shutdown.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (Next == EmailSendOutcome.Sent)
        {
            Sent.Add(new DispatchedMail(recipient, subject, textBody, htmlBody));
        }

        return Task.FromResult(Next);
    }
}

/// <summary>Captures error-level log messages from every logger it creates.</summary>
public sealed class ErrorLogCapture : ILoggerProvider
{
    private readonly List<string> _messages = [];

    /// <summary>The captured messages, in the order they were logged.</summary>
    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_messages)
            {
                return [.. _messages];
            }
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Capturing(_messages);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class Capturing(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            lock (messages)
            {
                messages.Add(formatter(state, exception));
            }
        }
    }
}

/// <summary>Manually advanced clock shared by the harness and its dispatchers.</summary>
public sealed class HarnessClock(DateTimeOffset now) : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; } = now;

    /// <inheritdoc />
    public DateTimeOffset NowAtTransitionalLocation => UtcNow;

    /// <inheritdoc />
    public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(UtcNow.DateTime);

    /// <inheritdoc />
    public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
        DateOnly.FromDateTime(instant.DateTime);

    /// <inheritdoc />
    public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;

    /// <summary>Moves the clock forward.</summary>
    /// <param name="elapsed">How far to advance.</param>
    public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
}

/// <summary>Stages outbox rows through the domain and drives dispatcher passes.</summary>
public abstract class PostgresEmailHarness(PostgresFixture fixture) : IDisposable
{
    /// <summary>The fixed instant every staged row is created at.</summary>
    protected static readonly DateTimeOffset Now =
        new(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);

    /// <summary>First dispatcher transport.</summary>
    protected readonly HarnessTransport transportA = new();

    /// <summary>Second dispatcher transport.</summary>
    protected readonly HarnessTransport transportB = new();

    /// <summary>The clock the harness and its dispatchers share.</summary>
    protected readonly HarnessClock Clock = new(Now);

    /// <summary>Every error the harness dispatchers logged.</summary>
    protected readonly ErrorLogCapture LoggedErrors = new();

    private readonly List<ServiceProvider> _providers = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
    }

    /// <summary>Builds a production-shaped dispatcher over the fixture database.</summary>
    /// <param name="transport">The transport the dispatcher sends through.</param>
    protected OutboxDispatcher Dispatcher(HarnessTransport transport)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(LoggedErrors));
        services.AddEventBookingInfrastructure(
            fixture.ConnectionString,
            new TokenOptions("a-test-signing-key-that-is-long-enough-here"));
        services.RemoveAll<IClock>();
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IEmailTransport>(transport);
        services.AddSingleton(new AttendeePortalOptions(
            "https://portal.example.invalid", "coordinator@example.invalid"));
        services.AddSingleton<OutboxDispatcher>();
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<OutboxDispatcher>();
    }

    /// <summary>Stages one pending invite row for Amy.</summary>
    /// <param name="template">The template.</param>
    protected async Task<Guid> StagePendingAsync(EmailTemplate template)
    {
        await fixture.ResetAsync();
        return await StageAdditionalAsync(template, "Amy", "amy@example.invalid");
    }

    /// <summary>Stages a second pending row without resetting, for targeted-dispatch tests.</summary>
    /// <param name="template">The template.</param>
    /// <param name="name">The attendee name.</param>
    /// <param name="email">The attendee email.</param>
    protected async Task<Guid> StageAdditionalAsync(EmailTemplate template, string name, string email)
    {
        await using var seed = fixture.NewContext();
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"DAT_ONLY_{Guid.NewGuid():N}".ToUpperInvariant(), "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), name, email, group, ProposalFixture.Now);
        var eventIds = new List<Guid>();
        foreach (var day in new[] { 30, 31, 32 })
        {
            var eventItem = EventFixture.Create(
                Guid.NewGuid(),
                new EventWindow(DateOnly.FromDateTime(Now.DateTime).AddDays(day), new TimeOnly(9, 0), 240),
                AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
            seed.Events.Add(eventItem);
            eventIds.Add(eventItem.Id);
        }

        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            Now.AddDays(7),
            [ProposalFixture.LocationId],
            eventIds,
            attendee.RequiredAppointmentTypeIds,
            0);
        var row = EmailLog.RecordPending(
            Guid.NewGuid(), attendee.Id, template, Now, inviteId: invite.Id);
        seed.Attendees.Add(attendee);
        seed.AttendeeGroups.Add(group);
        seed.Invites.Add(invite);
        seed.EmailLogs.Add(row);
        await seed.SaveChangesAsync();
        return row.Id;
    }

    /// <summary>Stages one pending confirmation row for a two-type booking.</summary>
    protected async Task<Guid> StageConfirmationAsync()
    {
        await fixture.ResetAsync();
        await using var seed = fixture.NewContext();
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"TWO_TYPE_{Guid.NewGuid():N}".ToUpperInvariant(), "Two types", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amy", "amy@example.invalid", group, ProposalFixture.Now);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(Now.DateTime).AddDays(30), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            Now.AddDays(7),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, Now);
        invite.MarkUsed();
        var first = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
        var second = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var row = EmailLog.RecordPending(
            Guid.NewGuid(), attendee.Id, EmailTemplate.BookingConfirmation, Now,
            bookingId: booking.Id);
        seed.Attendees.Add(attendee);
        seed.AttendeeGroups.Add(group);
        seed.Events.Add(eventItem);
        seed.Invites.Add(invite);
        seed.Bookings.Add(booking);
        seed.BookingAppointments.Add(first);
        seed.BookingAppointments.Add(second);
        seed.EmailLogs.Add(row);
        await seed.SaveChangesAsync();
        return row.Id;
    }

    /// <summary>Stages one pending cancellation row, with or without a replacement invite.</summary>
    /// <param name="withReplacement">Whether a recovery invite covers the booking.</param>
    protected async Task<Guid> StageCancellationAsync(bool withReplacement)
    {
        await fixture.ResetAsync();
        await using var seed = fixture.NewContext();
        var group = AttendeeGroup.Define(
            Guid.NewGuid(), $"DAT_ONLY_{Guid.NewGuid():N}".ToUpperInvariant(), "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amy", "amy@example.invalid", group, ProposalFixture.Now);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(Now.DateTime).AddDays(30), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        eventItem.Cancel(new NodaTimeEventWindowZones(), "Europe/London", Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            Now.AddDays(7),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, Now);
        invite.MarkUsed();
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var row = EmailLog.RecordPending(
            Guid.NewGuid(), attendee.Id, EmailTemplate.EventCancelledRebookingNeeded, Now,
            bookingId: booking.Id, eventId: eventItem.Id);
        seed.Attendees.Add(attendee);
        seed.AttendeeGroups.Add(group);
        seed.Events.Add(eventItem);
        seed.Invites.Add(invite);
        seed.Bookings.Add(booking);
        seed.BookingAppointments.Add(appointment);
        seed.EmailLogs.Add(row);
        if (withReplacement)
        {
            seed.Invites.Add(Invite.CreateRecovery(
                Guid.NewGuid(), attendee.Id, booking.Id, Now.AddDays(7),
                ProposalFixture.LocationId, null, [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds));
        }

        await seed.SaveChangesAsync();
        return row.Id;
    }

    /// <summary>Claims the row as a dispatcher that crashed minutes ago did.</summary>
    /// <param name="rowId">The row.</param>
    /// <param name="claimedMinutesAgo">How stale the claim is.</param>
    protected async Task CrashClaimAsync(Guid rowId, int claimedMinutesAgo)
    {
        await using var context = fixture.NewContext();
        var row = await context.EmailLogs.SingleAsync(e => e.Id == rowId);
        Assert.True(row.TryClaim(
            Clock.UtcNow.AddMinutes(-claimedMinutesAgo), TimeSpan.FromMinutes(5)));
        await context.SaveChangesAsync();
    }

    /// <summary>Reports the row failed, as a late provider bounce would.</summary>
    /// <param name="rowId">The row.</param>
    protected async Task FailAsync(Guid rowId)
    {
        await using var context = fixture.NewContext();
        var row = await context.EmailLogs.SingleAsync(e => e.Id == rowId);
        row.MarkFailed(Clock.UtcNow);
        await context.SaveChangesAsync();
    }

    /// <summary>Moves the shared clock forward.</summary>
    /// <param name="elapsed">How far to advance.</param>
    protected void Advance(TimeSpan elapsed) => Clock.Advance(elapsed);

    /// <summary>Drives the retry handler against the row.</summary>
    /// <param name="rowId">The failed row.</param>
    protected async Task RetryAsync(Guid rowId)
    {
        await using var context = fixture.NewContext();
        var coordinator = Guid.NewGuid();
        context.StaffAccessProfiles.Add(StaffAccessProfile.Create(
            coordinator, [Role.Coordinator], null));
        await context.SaveChangesAsync();
        var row = await context.EmailLogs.AsNoTracking().SingleAsync(e => e.Id == rowId);
        var handler = new RetryEmailHandler(
            new EmailDeliveryRepository(context),
            new StaffAccessAuthorizer(new StaffAccessProfileRepository(context)),
            new UnitOfWork(context),
            Clock);

        var result = await handler.HandleAsync(
            new RetryEmailCommand(coordinator, row.AttendeeId!.Value, rowId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    /// <summary>Reads the row status on a fresh context.</summary>
    /// <param name="rowId">The row.</param>
    protected async Task<EmailStatus> StatusOfAsync(Guid rowId)
    {
        await using var context = fixture.NewContext();
        return await context.EmailLogs.Where(e => e.Id == rowId)
            .Select(e => e.Status).SingleAsync();
    }

    /// <summary>Stamps a staged request correlation onto a pending row.</summary>
    /// <param name="rowId">The row.</param>
    /// <param name="correlationId">The staged identifier.</param>
    protected async Task StampCorrelationAsync(Guid rowId, string correlationId)
    {
        await using var context = fixture.NewContext();
        var row = await context.EmailLogs.SingleAsync(e => e.Id == rowId);
        row.StampCorrelation(correlationId);
        await context.SaveChangesAsync();
    }

    /// <summary>Reads the row's backoff on a fresh context.</summary>
    /// <param name="rowId">The row.</param>
    protected async Task<DateTimeOffset?> NotBeforeOfAsync(Guid rowId)
    {
        await using var context = fixture.NewContext();
        return await context.EmailLogs.Where(e => e.Id == rowId)
            .Select(e => e.NotBefore).SingleAsync();
    }

    /// <summary>Concatenates every column's text for the personal-data scan.</summary>
    /// <param name="rowId">The row.</param>
    protected async Task<string> AllColumnsAsync(Guid rowId)
    {
        await using var context = fixture.NewContext();
        var row = await context.EmailLogs.AsNoTracking().SingleAsync(e => e.Id == rowId);
        return string.Join("|",
            row.Id, row.AttendeeId, row.TemplateName, row.SentAt.ToString("O"), row.Status,
            row.InviteId, row.BookingId, row.EventId,
            row.ClaimedAt?.ToString("O"), row.ClaimCount,
            row.NotBefore?.ToString("O"), row.CorrelationId);
    }
}
