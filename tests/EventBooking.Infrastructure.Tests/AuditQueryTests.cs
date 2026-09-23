using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class AuditQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AEventsHistoryComesBackNewestFirst()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed,
                ActorType.Staff, "staff-1", Now, null));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, eventId, AuditAction.EventCancelled,
                ActorType.Staff, "staff-2", Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed,
                ActorType.Staff, "staff-3", Now, null));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForEntityAsync(
            AuditEntityTypes.Event, eventId, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("EventCancelled", rows[0].Action);
        Assert.Equal("6 bookings voided", rows[0].Details);
        Assert.Equal("EventConfirmed", rows[1].Action);
        Assert.Equal("Staff", rows[1].ActorType);
    }

    [Fact]
    public async Task AAttendeesHistoryGathersTheirInviteAndBookingEntries()
    {
        await fixture.ResetAsync();

        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId,
                "Amara Novak",
                "a.novak@mail.com",
                pilots,
                ProposalFixture.Now);
            write.Attendees.Add(attendee);
            write.Invites.Add(Invite.CreateInitial(
                inviteId,
                attendee.Id,
                "hash",
                Now.AddDays(4),
                [ProposalFixture.LocationId],
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                attendee.RequiredAppointmentTypeIds,
                0));

            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteCreated,
                ActorType.System, null, Now, "someone else"));

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("retry 0", row.Details);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
    }

    [Fact]
    public async Task AAttendeeWithNoHistoryGetsAnEmptyList()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForAttendeeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ForAttendeeIncludesBookingAppointmentEvents()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var appointmentId = await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.BookingAppointment, appointmentId, AuditAction.AppointmentCheckedIn);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        Assert.Contains(rows, r => r.EntityId == appointmentId);
    }

    [Fact]
    public async Task ForAttendeeIncludesEntriesRecordedAgainstTheAttendeeItself()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        await SeedAttendeeBookingAsync(attendeeId);
        await AddAuditAsync(AuditEntityTypes.Attendee, attendeeId, AuditAction.AttendeeGroupAssigned, Now.AddHours(-1));
        await AddAuditAsync(AuditEntityTypes.Attendee, Guid.NewGuid(), AuditAction.AttendeeGroupReassigned);
        await AddAuditAsync(AuditEntityTypes.Event, attendeeId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForAttendeeAsync(attendeeId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(AuditEntityTypes.Attendee, row.EntityType);
        Assert.Equal(nameof(AuditAction.AttendeeGroupAssigned), row.Action);
    }

    [Fact]
    public async Task SearchOrdersNewestFirstWithStableCursor()
    {
        await fixture.ResetAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, first, AuditAction.EventConfirmed, Now.AddHours(-2));
        await AddAuditAsync(AuditEntityTypes.Event, second, AuditAction.EventCancelled, Now.AddHours(-1));

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var page1 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, null, 1),
            CancellationToken.None);
        Assert.Single(page1.Rows);
        Assert.Equal(second, page1.Rows[0].EntityId);
        Assert.NotNull(page1.NextCursor);

        // A row newer than the cursor must not shift the following page.
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);

        var page2 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, page1.NextCursor, 10),
            CancellationToken.None);
        Assert.Contains(page2.Rows, r => r.EntityId == first);
        Assert.DoesNotContain(page2.Rows, r => r.EntityId == second);
    }

    [Fact]
    public async Task SearchRestrictedToOperationalBucketNeverReturnsAttendeeRows()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated);
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        IReadOnlyList<string> operational =
            [AuditEntityTypes.EventProposal, AuditEntityTypes.Event, AuditEntityTypes.StaffAccessProfile];
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, operational, null, null, 50),
            CancellationToken.None);

        Assert.All(page.Rows, r => Assert.Contains(r.EntityType, operational));
        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    [Fact]
    public async Task SearchWithNoAllowedEntityTypesReturnsAnEmptyPage()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, [], null, null, 50),
            CancellationToken.None);

        Assert.Empty(page.Rows);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task IdentifierMatchesEntityIdAndActorId()
    {
        await fixture.ResetAsync();
        var entityId = Guid.NewGuid();
        const string actor = "staff-actor-7";
        await AddAuditAsync(AuditEntityTypes.Event, entityId, AuditAction.EventConfirmed, actor: actor);

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var byEntity = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, entityId.ToString(), AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byEntity.Rows, r => r.EntityId == entityId);

        var byActor = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, actor, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);
        Assert.Contains(byActor.Rows, r => r.EntityId == entityId);
    }

    [Fact]
    public async Task SearchFiltersByTimestampRangeActionAndActorType()
    {
        await fixture.ResetAsync();
        var wanted = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, wanted, AuditAction.EventCancelled, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventConfirmed, Now);
        await AddAuditAsync(AuditEntityTypes.Event, Guid.NewGuid(), AuditAction.EventCancelled, Now.AddDays(-30));

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(
                Now.AddHours(-1), Now.AddHours(1), "Staff", nameof(AuditAction.EventCancelled),
                null, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal(wanted, row.EntityId);
    }

    [Fact]
    public async Task SearchTreatsAMalformedCursorAsAbsent()
    {
        await fixture.ResetAsync();
        var eventId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.Event, eventId, AuditAction.EventConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, "not-a-cursor", 50),
            CancellationToken.None);

        Assert.Contains(page.Rows, r => r.EntityId == eventId);
    }

    /// <summary>Seeds a attendee with an invite, booking, and booking appointment; returns the appointment id.</summary>
    private async Task<Guid> SeedAttendeeBookingAsync(Guid attendeeId)
    {
        await using var write = fixture.NewContext();
        var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(attendeeId, "Amara Novak", "a.novak@mail.com", pilots, ProposalFixture.Now);
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "hash",
            Now.AddDays(4),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventId, "manage-token-hash", Now);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);

        write.Attendees.Add(attendee);
        write.Invites.Add(invite);
        write.Bookings.Add(booking);
        write.BookingAppointments.Add(appointment);
        await write.SaveChangesAsync();

        return appointment.Id;
    }

    /// <summary>Appends one audit-log row and returns the audited entity id.</summary>
    private async Task<Guid> AddAuditAsync(
        string entityType,
        Guid entityId,
        AuditAction action,
        DateTimeOffset? timestamp = null,
        string? actor = "staff-1")
    {
        await using var write = fixture.NewContext();
        write.AuditLogs.Add(AuditLog.Record(
            Guid.NewGuid(), entityType, entityId, action,
            actor is null ? ActorType.System : ActorType.Staff, actor, timestamp ?? Now, null));
        await write.SaveChangesAsync();
        return entityId;
    }
}
