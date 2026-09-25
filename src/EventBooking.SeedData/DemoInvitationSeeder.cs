// src/EventBooking.SeedData/DemoInvitationSeeder.cs (complete)
using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

public sealed class DemoInvitationSeeder(
    EventBookingDbContext database,
    IAttendeeRepository attendees,
    InviteAttendeeHandler inviteAttendee,
    RetryEmailHandler retryEmail,
    OutboxDispatcher dispatcher,
    IEventWindowZones zones,
    IClock clock)
{
    public TextWriter Progress { get; set; } = TextWriter.Null;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var data = DemoSeedSpec.Build();
        var locationSpecs = data.Locations.ToDictionary(x => x.Code);
        if (data.Events.Any(item => item.Date <=
            zones.LocalDateOf(clock.UtcNow, locationSpecs[item.LocationCode].TimeZoneId)))
            throw new SeedException("Demo invitation dates are stale. Run with --reanchor.");

        var locationCodes = new[] { "LONDON", "DUBLIN" };
        var locations = await database.Locations.AsNoTracking()
            .Where(x => locationCodes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, ct);
        if (locationCodes.Any(code => !locations.TryGetValue(code, out var item) || !item.IsActive))
            throw new SeedException("Demo invitations require active LONDON and DUBLIN locations.");
        var locationIds = locationCodes.Select(code => locations[code].Id).ToList();
        var recipients = data.Attendees.Where(x => x.SendInvitation).ToList();
        var sent = 0;

        foreach (var spec in recipients)
        {
            var group = data.AttendeeGroups.Single(x => x.Code == spec.GroupCode);
            if (!group.IsActive)
                throw new SeedException($"Demo invitation group {group.Code} is inactive.");
            var attendee = await attendees.GetByEmailAsync(spec.Email, ct)
                ?? throw new SeedException($"Seed Attendee is missing: {spec.Email}.");
            var persistedGroup = await database.AttendeeGroups.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == attendee.AttendeeGroupId, ct);
            if (persistedGroup is null || persistedGroup.Code != group.Code || !persistedGroup.IsActive)
                throw new SeedException($"Demo invitation requires active group {group.Code}.");
            if (attendee.Status == AttendeeStatus.Booked)
            {
                Report($"Preserved existing booking: {spec.Email}.");
                continue;
            }

            var hasHistory = await database.Invites.AnyAsync(x => x.AttendeeId == attendee.Id, ct);
            var pendingInvite = await database.Invites.AsNoTracking().SingleOrDefaultAsync(
                x => x.AttendeeId == attendee.Id && x.Status == InviteStatus.Pending
                    && x.RecoveryOfBookingId == null, ct);
            if (!hasHistory)
            {
                var issued = await inviteAttendee.HandleAsync(
                    new InviteAttendeeCommand(DemoSeedSpec.CoordinatorUserId(), attendee.Id, locationIds), ct);
                if (issued.IsFailure)
                    throw new SeedException($"Demo invitation for {spec.Email} failed: {issued.Error}.");
                var inviteId = issued.Value.InviteId
                    ?? throw new SeedException($"Demo invitation for {spec.Email} produced no Invite.");
                pendingInvite = await database.Invites.AsNoTracking()
                    .SingleAsync(x => x.Id == inviteId, ct);
            }
            else if (pendingInvite is null || !pendingInvite.IsUsableAt(clock.UtcNow))
            {
                Report($"Preserved invitation history: {spec.Email}.");
                continue;
            }

            var delivery = await database.EmailLogs.AsNoTracking()
                .Where(x => x.AttendeeId == attendee.Id && x.InviteId == pendingInvite.Id
                    && x.TemplateName == EmailTemplate.AttendeeInvite
                    && x.Status != EmailStatus.Resolved)
                .OrderByDescending(x => x.SentAt).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new SeedException($"Pending demo invitation has no delivery: {spec.Email}.");
            if (delivery.Status == EmailStatus.Sent)
            {
                Report($"Invitation already delivered: {spec.Email}.");
                continue;
            }

            Guid deliveryId;
            if (delivery.Status == EmailStatus.Failed)
            {
                var retried = await retryEmail.HandleAsync(new RetryEmailCommand(
                    DemoSeedSpec.CoordinatorUserId(), attendee.Id, delivery.Id), ct);
                if (retried.IsFailure)
                    throw new SeedException($"Demo delivery retry for {spec.Email} failed: {retried.Error}.");
                deliveryId = retried.Value.EmailLogId;
            }
            else if (delivery.Status == EmailStatus.Pending)
            {
                deliveryId = delivery.Id;
            }
            else
            {
                throw new SeedException($"Demo delivery for {spec.Email} is {delivery.Status}.");
            }

            await dispatcher.DispatchOneAsync(deliveryId, ct);
            var status = await database.EmailLogs.AsNoTracking().Where(x => x.Id == deliveryId)
                .Select(x => x.Status).SingleAsync(ct);
            if (status != EmailStatus.Sent) throw DeliveryFailed(spec.Email, status);
            sent++;
            Report($"Invitation delivered: {spec.Email}.");
        }

        return sent;
    }

    private static SeedException DeliveryFailed(string recipient, EmailStatus status) => new(
        $"Demo invitation delivery for {recipient} is {status}; fix SMTP or wait for backoff and rerun.");

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");
}
