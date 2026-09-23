using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies locked Employee Group changes follow every Candidate lifecycle rule.</summary>
public sealed class EmployeeGroupLifecycleTests
{
    private static readonly EmployeeGroup CabinCrew = EmployeeGroup.Define(
        EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup GroundTransport = EmployeeGroup.Define(
        EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
        "Ground Transport Services", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting]);
    private static readonly EmployeeGroup Pilots = EmployeeGroup.Define(
        EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);

    /// <summary>A set-equivalent active-Booking update preserves all lifecycle state.</summary>
    [Fact]
    public async Task EquivalentGroupPreservesActiveBookingAndUsedInviteHistory()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Amara N.",
                "amara.n@example.com", GroundTransport.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GroundTransport.Id, fixture.Candidate.EmployeeGroupId);
        Assert.Equal(CandidateStatus.Booked, fixture.Candidate.Status);
        Assert.Equal(InviteStatus.Used, fixture.Invite!.Status);
        Assert.Equal(
            ["candidate-locked", "initial-invite-locked", "original-booking-locked"],
            fixture.Operations.Events);
    }

    /// <summary>A set-changing active-Booking update fails before any Candidate mutation.</summary>
    [Fact]
    public async Task ChangedGroupConflictsWithActiveOriginalBooking()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.Booked, activeBooking: true);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Changed", "changed@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("candidate_group_active_booking_conflict", result.Error.Code);
        Assert.Equal(CabinCrew.Id, fixture.Candidate.EmployeeGroupId);
        Assert.Equal("Amara", fixture.Candidate.Name);
    }

    /// <summary>A set-changing pending Invite is superseded without automatic replacement.</summary>
    [Fact]
    public async Task ChangedGroupSupersedesPendingInviteAndResetsStatus()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.Invited, activeBooking: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Amara", "amara@example.com", Pilots.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, fixture.Invite!.Status);
        Assert.Equal(CandidateStatus.NotYetInvited, fixture.Candidate.Status);
        Assert.Equal(Pilots.RequiredAppointmentTypeIds, fixture.Candidate.RequiredAppointmentTypeIds);
        Assert.Equal(1, fixture.Audit.Entries.Count(entry =>
            entry.Action == EventBooking.Domain.Audit.AuditAction.EmployeeGroupChanged));
    }

    /// <summary>A repeated identical request performs no save and writes no audit row.</summary>
    [Fact]
    public async Task IdenticalUpdateIsANoOp()
    {
        var fixture = GivenCandidate(CabinCrew, CandidateStatus.NotYetInvited, activeBooking: false,
            includeInvite: false);

        var result = await fixture.Handler.UpdateAsync(
            new UpdateCandidateCommand(
                fixture.Coordinator, fixture.Candidate.Id, "Amara", "amara@example.com", CabinCrew.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Empty(fixture.Audit.Entries);
    }

    private static Fixture GivenCandidate(
        EmployeeGroup group,
        CandidateStatus status,
        bool activeBooking,
        bool includeInvite = true)
    {
        var coordinator = Guid.NewGuid();
        var operations = new TransactionOperationLog();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(coordinator, [Role.Coordinator], null));
        var groups = new InMemoryEmployeeGroupRepository();
        groups.Items.AddRange([CabinCrew, GroundTransport, Pilots]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        if (status == CandidateStatus.Invited || status == CandidateStatus.Booked)
        {
            candidate.MarkInvited();
        }
        if (status == CandidateStatus.Booked)
        {
            candidate.MarkBooked();
        }
        var candidates = new InMemoryCandidateRepository(operations);
        candidates.Add(candidate);
        var invites = new InMemoryInviteRepository(operations);
        Invite? invite = null;
        if (includeInvite)
        {
            var slotId = Guid.NewGuid();
            invite = Invite.CreateInitial(
                Guid.NewGuid(), candidate.Id, "token", DateTimeOffset.UtcNow.AddDays(1),
                [slotId, Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);
            invites.Add(invite);
        }
        var bookings = new InMemoryBookingRepository(operations);
        if (activeBooking)
        {
            bookings.Add(Booking.Create(
                Guid.NewGuid(), invite!, invite!.OfferedSlotIds[0], "manage", DateTimeOffset.UtcNow));
            invite!.MarkUsed();
        }
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SaveCandidateHandler(
            candidates, groups, invites, bookings, new StaffAccessAuthorizer(profiles), audit, unitOfWork);
        return new Fixture(handler, candidate, invite, coordinator, operations, audit, unitOfWork);
    }

    private sealed record Fixture(
        SaveCandidateHandler Handler,
        Candidate Candidate,
        Invite? Invite,
        Guid Coordinator,
        TransactionOperationLog Operations,
        RecordingAuditLogger Audit,
        FakeUnitOfWork UnitOfWork);
}
