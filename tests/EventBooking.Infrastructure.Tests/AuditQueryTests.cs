using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class AuditQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ASlotsHistoryComesBackNewestFirst()
    {
        await fixture.ResetAsync();
        var slotId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-1", Now, null));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotCancelled,
                ActorType.Staff, "staff-2", Now.AddHours(1), "6 bookings voided"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed,
                ActorType.Staff, "staff-3", Now, null));
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForEntityAsync(
            AuditEntityTypes.ConfirmedSlot, slotId, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("SlotCancelled", rows[0].Action);
        Assert.Equal("6 bookings voided", rows[0].Details);
        Assert.Equal("SlotConfirmed", rows[1].Action);
        Assert.Equal("Staff", rows[1].ActorType);
    }

    [Fact]
    public async Task ACandidatesHistoryGathersTheirInviteAndBookingEntries()
    {
        await fixture.ResetAsync();

        var candidateId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();

        await using (var write = fixture.NewContext())
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                candidateId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            write.Invites.Add(Invite.CreateInitial(
                inviteId, candidate.Id, "hash", Now.AddDays(4),
                [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
                candidate.RequiredAppointmentTypeIds, 0));

            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, inviteId, AuditAction.InviteCreated,
                ActorType.System, null, Now, "retry 0"));
            write.AuditLogs.Add(AuditLog.Record(
                Guid.NewGuid(), AuditEntityTypes.Invite, Guid.NewGuid(), AuditAction.InviteCreated,
                ActorType.System, null, Now, "someone else"));

            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForCandidateAsync(candidateId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("InviteCreated", row.Action);
        Assert.Equal("retry 0", row.Details);
        Assert.Equal("System", row.ActorType);
        Assert.Null(row.ActorId);
    }

    [Fact]
    public async Task ACandidateWithNoHistoryGetsAnEmptyList()
    {
        await fixture.ResetAsync();

        await using var read = fixture.NewContext();
        var rows = await new AuditQueries(read).ForCandidateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task ForCandidateIncludesBookingAppointmentEvents()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var appointmentId = await SeedCandidateBookingAsync(candidateId);
        await AddAuditAsync(AuditEntityTypes.BookingAppointment, appointmentId, AuditAction.AppointmentCheckedIn);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForCandidateAsync(candidateId, CancellationToken.None);

        Assert.Contains(rows, r => r.EntityId == appointmentId);
    }

    [Fact]
    public async Task ForCandidateIncludesEntriesRecordedAgainstTheCandidateItself()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        await SeedCandidateBookingAsync(candidateId);
        await AddAuditAsync(AuditEntityTypes.Candidate, candidateId, AuditAction.EmployeeGroupAssigned, Now.AddHours(-1));
        await AddAuditAsync(AuditEntityTypes.Candidate, Guid.NewGuid(), AuditAction.EmployeeGroupChanged);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, candidateId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var rows = await new AuditQueries(context).ForCandidateAsync(candidateId, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(AuditEntityTypes.Candidate, row.EntityType);
        Assert.Equal(nameof(AuditAction.EmployeeGroupAssigned), row.Action);
    }

    [Fact]
    public async Task SearchOrdersNewestFirstWithStableCursor()
    {
        await fixture.ResetAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, first, AuditAction.SlotConfirmed, Now.AddHours(-2));
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, second, AuditAction.SlotCancelled, Now.AddHours(-1));

        await using var context = fixture.NewContext();
        var queries = new AuditQueries(context);
        var page1 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, null, 1),
            CancellationToken.None);
        Assert.Single(page1.Rows);
        Assert.Equal(second, page1.Rows[0].EntityId);
        Assert.NotNull(page1.NextCursor);

        // A row newer than the cursor must not shift the following page.
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed, Now);

        var page2 = await queries.SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, page1.NextCursor, 10),
            CancellationToken.None);
        Assert.Contains(page2.Rows, r => r.EntityId == first);
        Assert.DoesNotContain(page2.Rows, r => r.EntityId == second);
    }

    [Fact]
    public async Task SearchRestrictedToOperationalBucketNeverReturnsCandidateRows()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.Booking, Guid.NewGuid(), AuditAction.BookingCreated);
        var slotId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        IReadOnlyList<string> operational =
            [AuditEntityTypes.SlotProposal, AuditEntityTypes.ConfirmedSlot, AuditEntityTypes.StaffAccessProfile];
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, operational, null, null, 50),
            CancellationToken.None);

        Assert.All(page.Rows, r => Assert.Contains(r.EntityType, operational));
        Assert.Contains(page.Rows, r => r.EntityId == slotId);
    }

    [Fact]
    public async Task SearchWithNoAllowedEntityTypesReturnsAnEmptyPage()
    {
        await fixture.ResetAsync();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed);

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
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, entityId, AuditAction.SlotConfirmed, actor: actor);

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
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, wanted, AuditAction.SlotCancelled, Now);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotConfirmed, Now);
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, Guid.NewGuid(), AuditAction.SlotCancelled, Now.AddDays(-30));

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(
                Now.AddHours(-1), Now.AddHours(1), "Staff", nameof(AuditAction.SlotCancelled),
                null, AuditEntityTypes.All, null, null, 50),
            CancellationToken.None);

        var row = Assert.Single(page.Rows);
        Assert.Equal(wanted, row.EntityId);
    }

    [Fact]
    public async Task SearchTreatsAMalformedCursorAsAbsent()
    {
        await fixture.ResetAsync();
        var slotId = Guid.NewGuid();
        await AddAuditAsync(AuditEntityTypes.ConfirmedSlot, slotId, AuditAction.SlotConfirmed);

        await using var context = fixture.NewContext();
        var page = await new AuditQueries(context).SearchAsync(
            new AuditSearchFilter(null, null, null, null, null, AuditEntityTypes.All, null, "not-a-cursor", 50),
            CancellationToken.None);

        Assert.Contains(page.Rows, r => r.EntityId == slotId);
    }

    /// <summary>Seeds a candidate with an invite, booking, and booking appointment; returns the appointment id.</summary>
    private async Task<Guid> SeedCandidateBookingAsync(Guid candidateId)
    {
        await using var write = fixture.NewContext();
        var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(candidateId, "Amara Novak", "a.novak@mail.com", pilots);
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "hash", Now.AddDays(4),
            [slotId, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(Guid.NewGuid(), invite, slotId, "manage-token-hash", Now);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);

        write.Candidates.Add(candidate);
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
