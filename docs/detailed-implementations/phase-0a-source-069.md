# 00a — Port source 69 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs","encoding":"utf8","sha256":"897db9bf0c6836ad49062f35cd1477d05e284b882761c53f28c3a41e559cb42b","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.Candidates;

public class CandidateStatusTests
{
    /// <summary>Builds a DAT-only group; lifecycle tests need a mapping, not an identity.</summary>
    private static EmployeeGroup DatOnly() =>
        EmployeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Candidate NewCandidate() =>
        Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", DatOnly());

    private static Candidate InvitedCandidate()
    {
        var candidate = NewCandidate();
        candidate.MarkInvited();
        return candidate;
    }

    [Fact]
    public void ANotYetInvitedCandidateCanBeInvited()
    {
        var candidate = NewCandidate();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void ACandidateWithNoEligibleSlotsBecomesAwaitingAvailability()
    {
        var candidate = NewCandidate();

        candidate.MarkAwaitingAvailability();

        Assert.Equal(CandidateStatus.AwaitingAvailability, candidate.Status);
    }

    [Fact]
    public void AnAwaitingCandidateCanBeInvitedOnceSlotsAppear()
    {
        var candidate = NewCandidate();
        candidate.MarkAwaitingAvailability();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void ReInvitingAnAlreadyInvitedCandidateIsAllowed()
    {
        var candidate = InvitedCandidate();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void OnlyAnInvitedCandidateCanBecomeBooked()
    {
        var candidate = InvitedCandidate();
        candidate.MarkBooked();
        Assert.Equal(CandidateStatus.Booked, candidate.Status);

        var notInvited = NewCandidate();
        var ex = Assert.Throws<DomainException>(() => notInvited.MarkBooked());
        Assert.Equal("A candidate cannot move from NotYetInvited to Booked.", ex.Message);
    }

    [Fact]
    public void OnlyAnInvitedCandidateCanRunOutOfRetries()
    {
        var candidate = InvitedCandidate();
        candidate.MarkNoResponse();
        Assert.Equal(CandidateStatus.NoResponseNeedsFollowUp, candidate.Status);

        var booked = InvitedCandidate();
        booked.MarkBooked();
        Assert.Throws<DomainException>(() => booked.MarkNoResponse());
    }

    [Fact]
    public void AFollowUpCandidateCanBeManuallyReInvited()
    {
        var candidate = InvitedCandidate();
        candidate.MarkNoResponse();

        candidate.MarkInvited();

        Assert.Equal(CandidateStatus.Invited, candidate.Status);
    }

    [Fact]
    public void CancellingABookingReturnsTheCandidateToNotYetInvited()
    {
        var candidate = InvitedCandidate();
        candidate.MarkBooked();

        candidate.ResetToNotYetInvited();

        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);
    }

    [Fact]
    public void ANotYetInvitedCandidateCannotBeResetAgain()
    {
        var candidate = NewCandidate();

        var ex = Assert.Throws<DomainException>(() => candidate.ResetToNotYetInvited());
        Assert.Equal("A candidate cannot move from NotYetInvited to NotYetInvited.", ex.Message);
    }

}
`````

## tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs","encoding":"utf8","sha256":"760d89bb92d13ea0b5a3ad0d606364f597cc68f9dc62741b0136e0d41070dcbb","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.Candidates;

public class CandidateTests
{
    private static readonly Guid[] TwoTypes =
        [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting];

    /// <summary>Builds the Pilots group used across these fixtures for DAT+UNI.</summary>
    private static EmployeeGroup Pilots() =>
        EmployeeGroup.Define(EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true, TwoTypes);

    private static Candidate NewCandidate() =>
        Candidate.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", Pilots());

    [Fact]
    public void ANewCandidateStartsNotYetInvited()
    {
        var candidate = NewCandidate();

        Assert.Equal(CandidateStatus.NotYetInvited, candidate.Status);
        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("a.novak@mail.com", candidate.Email);
    }

    [Fact]
    public void NameAndEmailAreTrimmedAndTheEmailIsLowerCased()
    {
        var candidate = Candidate.Create(
            Guid.NewGuid(), "  Amara Novak  ", "  A.Novak@Mail.COM ", Pilots());

        Assert.Equal("Amara Novak", candidate.Name);
        Assert.Equal("a.novak@mail.com", candidate.Email);
    }

    [Fact]
    public void RequirementsAreRecordedAgainstTheCandidate()
    {
        var candidate = NewCandidate();

        Assert.Equal(2, candidate.Requirements.Count);
        Assert.All(candidate.Requirements, r => Assert.Equal(candidate.Id, r.CandidateId));
        Assert.Equal(
            TwoTypes.OrderBy(id => id),
            candidate.RequiredAppointmentTypeIds.OrderBy(id => id));
    }

    [Fact]
    public void AMissingNameIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Candidate.Create(Guid.NewGuid(), "  ", "a.novak@mail.com", Pilots()));
        Assert.Equal("name must not be blank.", ex.Message);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("no@domain")]
    [InlineData("two@@at.com")]
    [InlineData("spaces in@mail.com")]
    [InlineData("")]
    [InlineData(null)]
    public void AnInvalidEmailIsRejected(string? email)
    {
        var ex = Assert.Throws<DomainException>(
            () => Candidate.Create(Guid.NewGuid(), "Amara Novak", email, Pilots()));
        Assert.Equal("email is not a valid email address.", ex.Message);
    }

    [Fact]
    public void AGroupWithNoMappedTypesIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(Guid.NewGuid(), "EMPTY", "Empty", true, []));
        Assert.Equal("An employee group must map at least one appointment type.", ex.Message);
    }

    [Fact]
    public void DuplicateMappedTypesAreRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(
                Guid.NewGuid(),
                "DUP",
                "Dup",
                true,
                [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Equal("An employee group cannot map the same appointment type twice.", ex.Message);
    }

    [Fact]
    public void AnUnknownMappedTypeIsRejected()
    {
        Assert.Throws<DomainException>(
            () => EmployeeGroup.Define(Guid.NewGuid(), "UNKNOWN", "Unknown", true, [Guid.NewGuid()]));
    }

    [Fact]
    public void AllThreeAppointmentTypesAreAllowed()
    {
        var cabinCrew = EmployeeGroup.Define(
            EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true, AppointmentTypeIds.All);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", cabinCrew);

        Assert.Equal(3, candidate.Requirements.Count);
    }

    [Fact]
    public void UpdatingDetailsRevalidates()
    {
        var candidate = NewCandidate();

        candidate.UpdateDetails("Amara N. Novak", "amara@mail.com");
        Assert.Equal("Amara N. Novak", candidate.Name);
        Assert.Equal("amara@mail.com", candidate.Email);

        Assert.Throws<DomainException>(() => candidate.UpdateDetails("Amara", "broken"));
        Assert.Equal("amara@mail.com", candidate.Email);
    }

    [Fact]
    public void AssigningADifferentGroupReplacesThePreviousSet()
    {
        var candidate = NewCandidate();
        var groundOps = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]);

        Assert.True(candidate.AssignEmployeeGroup(groundOps));
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], candidate.RequiredAppointmentTypeIds);
    }

    [Fact]
    public void AssigningAnEquivalentGroupPreservesThePreviousSet()
    {
        var candidate = NewCandidate();
        var equivalent = EmployeeGroup.Define(
            Guid.NewGuid(), "PILOTS_EQUIVALENT", "Pilots equivalent", true, TwoTypes);

        Assert.False(candidate.AssignEmployeeGroup(equivalent));
        Assert.Equal(2, candidate.Requirements.Count);
    }
}
`````

## tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs","encoding":"utf8","sha256":"8362d30540a3e2213f0d2efb1ba8a0dcef815e36e5e61fa91e0e51e48ecb1cee","parts":1,"part":1} -->

`````csharp
using System.Reflection;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Candidates;

/// <summary>Prevents public factories from reintroducing unsourced Candidate requirements.</summary>
public sealed class RequirementOverrideSurfaceTests
{
    /// <summary>Candidate has no public factory accepting raw requirement identifiers.</summary>
    [Fact]
    public void CandidateHasNoRawRequirementFactory()
    {
        var raw = typeof(Candidate).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Create")
            .SelectMany(method => method.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IEnumerable<Guid>));

        Assert.False(raw);
    }

    /// <summary>Invite has only named initial and recovery factories.</summary>
    [Fact]
    public void InviteHasNoUnsnapshottedCreateFactory()
    {
        Assert.DoesNotContain(
            typeof(Invite).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == "Create");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateInitial");
        Assert.Contains(typeof(Invite).GetMethods(), method => method.Name == "CreateRecovery");
    }
}
`````

## tests/EventBooking.Domain.Tests/Common/GuardTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Common/GuardTests.cs","encoding":"utf8","sha256":"9656edd2f1b719136e0633fb5535da330e189cd232f0d23537c3eff628ac6e18","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Tests.Common;

public class GuardTests
{
    [Fact]
    public void NotBlankTrimsAndReturnsTheValue()
    {
        Assert.Equal("Amara Novak", Guard.NotBlank("  Amara Novak  ", "name"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotBlankRejectsMissingValues(string? value)
    {
        var ex = Assert.Throws<DomainException>(() => Guard.NotBlank(value, "name"));
        Assert.Equal("name must not be blank.", ex.Message);
    }

    [Fact]
    public void PositiveAcceptsOneAndRejectsZero()
    {
        Assert.Equal(1, Guard.Positive(1, "headcount"));
        var ex = Assert.Throws<DomainException>(() => Guard.Positive(0, "headcount"));
        Assert.Equal("headcount must be greater than zero.", ex.Message);
    }

    [Fact]
    public void NotNegativeAcceptsZeroAndRejectsMinusOne()
    {
        Assert.Equal(0, Guard.NotNegative(0, "maxAutoRetryCount"));
        Assert.Throws<DomainException>(() => Guard.NotNegative(-1, "maxAutoRetryCount"));
    }

    [Fact]
    public void AgainstThrowsOnlyWhenTheConditionHolds()
    {
        Guard.Against(false, "never thrown");
        var ex = Assert.Throws<DomainException>(() => Guard.Against(true, "boom"));
        Assert.Equal("boom", ex.Message);
    }
}
`````

## tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs","encoding":"utf8","sha256":"b0fce3d8848bcd2986c9984c99762ca1f4214d3bbccf37028b453d49473f0c4c","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Domain.Tests.EmployeeGroups;

/// <summary>Verifies Employee Group invariants and Candidate requirement derivation.</summary>
public sealed class EmployeeGroupCandidateTests
{
    /// <summary>Every approved mapping produces exactly the Issue 91 requirement set.</summary>
    [Theory]
    [MemberData(nameof(ApprovedMappings))]
    public void ApprovedMappingsAreDerived(
        Guid groupId,
        string code,
        string name,
        Guid[] expected)
    {
        var group = EmployeeGroup.Define(groupId, code, name, true, expected);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", group);

        Assert.Equal(groupId, candidate.EmployeeGroupId);
        Assert.Equal(expected.Order(), candidate.RequiredAppointmentTypeIds.Order());
    }

    /// <summary>Changing between equal mappings changes only the assigned group.</summary>
    [Fact]
    public void SetEquivalentAssignmentPreservesTheMaterializedSet()
    {
        var engineering = EmployeeGroup.Define(
            EmployeeGroupIds.Engineering,
            "ENGINEERING",
            "Engineering",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var groundOperations = EmployeeGroup.Define(
            EmployeeGroupIds.GroundOperationsAgent,
            "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent",
            true,
            [AppointmentTypeIds.MedicalCheckUp]);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", engineering);

        var changed = candidate.AssignEmployeeGroup(groundOperations);

        Assert.False(changed);
        Assert.Equal(EmployeeGroupIds.GroundOperationsAgent, candidate.EmployeeGroupId);
        Assert.Equal([AppointmentTypeIds.MedicalCheckUp], candidate.RequiredAppointmentTypeIds);
    }

    /// <summary>Inactive, empty, duplicate, and unknown mappings cannot become assignment authority.</summary>
    [Fact]
    public void InvalidReferenceDataIsRejected()
    {
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", false,
            [AppointmentTypeIds.DrugAndAlcoholTesting]));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true, []));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp]));
        Assert.Throws<DomainException>(() => EmployeeGroup.Define(
            Guid.NewGuid(), "not-canonical", "Cabin Crew", true, [Guid.NewGuid()]));
    }

    /// <summary>Provides the exact five approved group mappings.</summary>
    public static TheoryData<Guid, string, string, Guid[]> ApprovedMappings => new()
    {
        { EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
        { EmployeeGroupIds.Pilots, "PILOTS", "Pilots",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting] },
        { EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent",
            [AppointmentTypeIds.MedicalCheckUp] },
        { EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering",
            [AppointmentTypeIds.MedicalCheckUp] },
        { EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services",
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting] },
    };
}
`````

## tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj","encoding":"utf8","sha256":"047860c4691e9482fa016626f3f05aa1c1bbbb6d7f3461ab53a0342f98e3084a","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\EventBooking.Domain\EventBooking.Domain.csproj" />
  </ItemGroup>

</Project>
`````

## tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs","encoding":"utf8","sha256":"f97f43739cfeac43ad7db68c305f4481cf76d025c12ae99fc28f7e6257eab564","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

/// <summary>Verifies initial and recovery Invites own immutable requirement snapshots.</summary>
public sealed class InviteRequirementSnapshotTests
{
    private static readonly Guid[] Options = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    /// <summary>An initial Invite snapshots distinct known requirements in stable order.</summary>
    [Fact]
    public void InitialInviteSnapshotsRequirements()
    {
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(1), Options,
            [AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting], 0);

        Assert.Null(invite.RecoveryOfBookingId);
        Assert.Equal(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            invite.RequiredAppointmentTypeIds);
    }

    /// <summary>A recovery Invite retains its root Booking and can be explicitly cancelled.</summary>
    [Fact]
    public void RecoveryInviteLinksTheRootAndCancels()
    {
        var root = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), Guid.NewGuid(), root, "hash", DateTimeOffset.UtcNow.AddDays(1), Options,
            [AppointmentTypeIds.MedicalCheckUp]);

        invite.CancelRecovery();

        Assert.Equal(root, invite.RecoveryOfBookingId);
        Assert.Equal(InviteStatus.Cancelled, invite.Status);
    }

    /// <summary>Empty, duplicate, unknown, and oversized snapshots are rejected.</summary>
    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void InvalidSnapshotsCannotBeCreated(Guid[] snapshot)
    {
        Assert.Throws<DomainException>(() => Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(1), Options,
            snapshot, 0));
    }

    /// <summary>Provides every invalid snapshot shape.</summary>
    public static TheoryData<Guid[]> InvalidSnapshots => new()
    {
        { [] },
        { [AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.MedicalCheckUp] },
        { [Guid.NewGuid()] },
        { [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
            AppointmentTypeIds.UniformFitting, AppointmentTypeIds.DrugAndAlcoholTesting] },
    };
}
`````

## tests/EventBooking.Domain.Tests/Invites/InviteTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Invites/InviteTests.cs","encoding":"utf8","sha256":"74b496c6098f24acef6b7aef47cba988fdddbccd10aec1487c37efffbe281354","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Tests.Invites;

public class InviteTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid SlotA = Guid.Parse("50000001-0000-0000-0000-000000000001");
    private static readonly Guid SlotB = Guid.Parse("50000002-0000-0000-0000-000000000002");
    private static readonly Guid SlotC = Guid.Parse("50000003-0000-0000-0000-000000000003");
    private static readonly Guid SlotD = Guid.Parse("50000004-0000-0000-0000-000000000004");

    private static Invite NewInvite(int retryCount = 0) =>
        Invite.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), "hash-of-the-token", Now.AddDays(4),
            [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], retryCount);

    [Fact]
    public void ANewInviteIsPendingWithThreeOptions()
    {
        var invite = NewInvite();

        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Equal(3, invite.Options.Count);
        Assert.Equal(Invite.RequiredOptionCount, invite.Options.Count);
        Assert.Equal([SlotA, SlotB, SlotC], invite.OfferedSlotIds);
        Assert.Equal(0, invite.RetryCount);
        Assert.Equal("hash-of-the-token", invite.TokenHash);
    }

    [Fact]
    public void EveryOptionBelongsToTheInvite()
    {
        var invite = NewInvite();

        Assert.All(invite.Options, o => Assert.Equal(invite.Id, o.InviteId));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void AnInviteMustOfferExactlyThreeOptions(int optionCount)
    {
        var slots = new[] { SlotA, SlotB, SlotC, SlotD }.Take(optionCount);

        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                slots, [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite must offer exactly 3 slot options.", ex.Message);
    }

    [Fact]
    public void TheSameSlotCannotBeOfferedTwice()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [SlotA, SlotA, SlotB], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("An invite cannot offer the same slot twice.", ex.Message);
    }

    [Fact]
    public void AnInviteWithoutATokenHashIsRejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "  ", Now.AddDays(4),
                [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], 0));
        Assert.Equal("tokenHash must not be blank.", ex.Message);
    }

    [Fact]
    public void ANegativeRetryCountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => Invite.CreateInitial(
                Guid.NewGuid(), Guid.NewGuid(), "hash", Now.AddDays(4),
                [SlotA, SlotB, SlotC], [AppointmentTypeIds.DrugAndAlcoholTesting], -1));
    }

    [Fact]
    public void APendingInviteIsUsableUntilItExpires()
    {
        var invite = NewInvite();

        Assert.True(invite.IsUsableAt(Now));
        Assert.True(invite.IsUsableAt(Now.AddDays(4).AddSeconds(-1)));
        Assert.False(invite.IsUsableAt(Now.AddDays(4)));
        Assert.False(invite.IsUsableAt(Now.AddDays(5)));
    }

    [Fact]
    public void AUsedInviteIsNeverUsableAgain()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Equal(InviteStatus.Used, invite.Status);
        Assert.False(invite.IsUsableAt(Now));
    }

    [Fact]
    public void OnlyAPendingInviteCanBeUsedExpiredOrSuperseded()
    {
        var used = NewInvite();
        used.MarkUsed();
        Assert.Throws<DomainException>(() => used.MarkExpired());
        Assert.Throws<DomainException>(() => used.MarkSuperseded());
        Assert.Throws<DomainException>(() => used.MarkUsed());

        var expired = NewInvite();
        expired.MarkExpired();
        Assert.Equal(InviteStatus.Expired, expired.Status);
        Assert.Throws<DomainException>(() => expired.MarkUsed());

        var superseded = NewInvite();
        superseded.MarkSuperseded();
        Assert.Equal(InviteStatus.Superseded, superseded.Status);
    }

    [Fact]
    public void AnOptionThatFilledUpIsDroppedAndAReplacementRestoresThree()
    {
        var invite = NewInvite();

        invite.RemoveOption(SlotB);
        Assert.Equal(2, invite.Options.Count);
        Assert.False(invite.Offers(SlotB));

        invite.AddOption(SlotD);
        Assert.Equal(3, invite.Options.Count);
        Assert.True(invite.Offers(SlotD));
    }

    [Fact]
    public void AFourthOptionIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(SlotD));
        Assert.Equal("An invite cannot offer more than 3 slot options.", ex.Message);
    }

    [Fact]
    public void AddingAnOptionAlreadyOfferedIsRejected()
    {
        var invite = NewInvite();
        invite.RemoveOption(SlotB);

        var ex = Assert.Throws<DomainException>(() => invite.AddOption(SlotA));
        Assert.Equal("An invite cannot offer the same slot twice.", ex.Message);
    }

    [Fact]
    public void RemovingAnOptionThatWasNotOfferedIsRejected()
    {
        var invite = NewInvite();

        var ex = Assert.Throws<DomainException>(() => invite.RemoveOption(SlotD));
        Assert.Equal("This invite does not offer that slot.", ex.Message);
    }

    [Fact]
    public void OptionsCanOnlyChangeWhileTheInviteIsPending()
    {
        var invite = NewInvite();
        invite.MarkUsed();

        Assert.Throws<DomainException>(() => invite.RemoveOption(SlotA));
        Assert.Throws<DomainException>(() => invite.AddOption(SlotD));
    }

    [Fact]
    public void TheRetryCountIsCarriedForwardByTheCaller()
    {
        var invite = NewInvite(retryCount: 2);

        Assert.Equal(2, invite.RetryCount);
    }
}
`````

## tests/EventBooking.Domain.Tests/OntologyEnumTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/OntologyEnumTests.cs","encoding":"utf8","sha256":"5f794cd9644e8506e24039f0e30f88740bb4f0326906dda5b0b56a3c6106de88","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests;

/// <summary>
/// Locks every enum to the member list docs/ontology.md declares. Changing an enum without
/// changing the ontology fails here, which is the point.
/// </summary>
public class OntologyEnumTests
{
    /// <summary>Checks the role vocabulary against the ontology.</summary>
    [Fact]
    public void RoleMatchesTheOntology()
    {
        Assert.Equal(
            new[] { "Manager", "Coordinator", "Admin", "AppointmentStaff" },
            Enum.GetNames<Role>());
    }

    /// <summary>Checks the candidate lifecycle vocabulary against the ontology.</summary>
    [Fact]
    public void CandidateStatusMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "NotYetInvited", "AwaitingAvailability", "Invited", "Booked",
                "NoResponseNeedsFollowUp",
            },
            Enum.GetNames<CandidateStatus>());
    }

    /// <summary>Checks the slot-proposal vocabulary against the ontology.</summary>
    [Fact]
    public void SlotProposalStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Open", "Withdrawn", "Confirmed" }, Enum.GetNames<SlotProposalStatus>());
    }

    /// <summary>Checks the confirmed-slot vocabulary against the ontology.</summary>
    [Fact]
    public void ConfirmedSlotStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Active", "Cancelled" }, Enum.GetNames<ConfirmedSlotStatus>());
    }

    /// <summary>Checks the invite vocabulary against the ontology.</summary>
    [Fact]
    public void InviteStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Pending", "Used", "Expired", "Superseded", "Cancelled" }, Enum.GetNames<InviteStatus>());
    }

    /// <summary>Checks the booking vocabulary against the ontology.</summary>
    [Fact]
    public void BookingStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Active", "Cancelled", "Concluded" }, Enum.GetNames<BookingStatus>());
    }

    /// <summary>Checks the audit actor vocabulary against the ontology.</summary>
    [Fact]
    public void ActorTypeMatchesTheOntology()
    {
        Assert.Equal(new[] { "Staff", "CandidateToken", "System" }, Enum.GetNames<ActorType>());
    }

    /// <summary>Checks the audit action vocabulary against the ontology.</summary>
    [Fact]
    public void AuditActionMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
                "SlotConfirmed", "SlotCancelled", "CapacityDecremented", "CapacityIncremented",
                "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
                "BookingCreated", "BookingCancelled", "CapacityAdjusted", "SlotImported",
                "StaffAccessChanged", "StaffAccessRemoved",
                "AppointmentCheckedIn", "AppointmentCompleted",
                "AppointmentMarkedNoShow", "AppointmentStatusCorrected",
                "EmployeeGroupAssigned", "EmployeeGroupChanged",
                "RecoveryInviteCreated", "RecoveryInviteCancelled",
                "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
                "CandidateDeleted",
            },
            Enum.GetNames<AuditAction>());
    }

    /// <summary>Verifies booking-appointment statuses remain synchronized with the ontology.</summary>
    [Fact]
    public void BookingAppointmentStatusMatchesTheOntology()
    {
        Assert.Equal(
            new[] { "Expected", "CheckedIn", "Completed", "NoShow" },
            Enum.GetNames<BookingAppointmentStatus>());
    }

    /// <summary>Checks the email-template vocabulary against the ontology.</summary>
    [Fact]
    public void EmailTemplateMatchesTheOntology()
    {
        Assert.Equal(
            new[]
            {
                "CandidateInvite", "BookingConfirmation", "SlotCancelledRebookingNeeded",
                "CandidateReinvite",
            },
            Enum.GetNames<EmailTemplate>());
    }

    /// <summary>Checks the durable email delivery vocabulary against the ontology.</summary>
    [Fact]
    public void EmailStatusMatchesTheOntology()
    {
        Assert.Equal(new[] { "Sent", "Failed", "Pending", "Resolved" }, Enum.GetNames<EmailStatus>());
    }

    /// <summary>Ensures every persisted enum has a deliberate non-zero value.</summary>
    [Fact]
    public void NoEnumMemberUsesTheDefaultZeroValue()
    {
        // A zero member would be indistinguishable from an unset integer column.
        Assert.DoesNotContain(0, Enum.GetValues<Role>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<CandidateStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<SlotProposalStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<ConfirmedSlotStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<InviteStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<BookingStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<BookingAppointmentStatus>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<ActorType>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<AuditAction>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EmailTemplate>().Cast<int>());
        Assert.DoesNotContain(0, Enum.GetValues<EmailStatus>().Cast<int>());
    }
}
`````

## tests/EventBooking.Domain.Tests/ScaffoldSmokeTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/ScaffoldSmokeTests.cs","encoding":"utf8","sha256":"c5c3f01a2a2b130dbf18d3833d18fda66bc6f7fb46d8e307c18ab6c78ae66e94","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public void TestHarnessRuns()
    {
        Assert.True(true);
    }
}
`````

## tests/EventBooking.Domain.Tests/Settings/SystemSettingsTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Settings/SystemSettingsTests.cs","encoding":"utf8","sha256":"0b3cbab0af6c0a3ff4a68eeb4c4ca0a12edf5dda80507152ab06b307e024251a","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Settings;

namespace EventBooking.Domain.Tests.Settings;

public class SystemSettingsTests
{
    [Fact]
    public void TheDefaultsMatchTheAdminScreenWireframe()
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Equal(1, settings.Id);
        Assert.Equal(4, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }

    [Fact]
    public void UpdateStoresBothValues()
    {
        var settings = SystemSettings.CreateDefault();

        settings.Update(7, 0);

        Assert.Equal(7, settings.InviteExpiryDays);
        Assert.Equal(0, settings.MaxAutoRetryCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExpiryMustBeAPositiveNumberOfDays(int days)
    {
        var settings = SystemSettings.CreateDefault();

        var ex = Assert.Throws<DomainException>(() => settings.Update(days, 2));
        Assert.Equal("inviteExpiryDays must be greater than zero.", ex.Message);
    }

    [Fact]
    public void RetryCountMayBeZeroButNotNegative()
    {
        var settings = SystemSettings.CreateDefault();

        settings.Update(4, 0);
        Assert.Equal(0, settings.MaxAutoRetryCount);

        var ex = Assert.Throws<DomainException>(() => settings.Update(4, -1));
        Assert.Equal("maxAutoRetryCount must not be negative.", ex.Message);
    }

    [Fact]
    public void ARejectedUpdateLeavesTheSettingsUnchanged()
    {
        var settings = SystemSettings.CreateDefault();

        Assert.Throws<DomainException>(() => settings.Update(0, 99));

        Assert.Equal(4, settings.InviteExpiryDays);
        Assert.Equal(2, settings.MaxAutoRetryCount);
    }
}
`````

## tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs","encoding":"utf8","sha256":"6609d93db88d174a9231c2ba2132b8ef05998b421ffd2bc268a66df22678c7bb","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotCancellationTests
{
    private static ConfirmedSlot ActiveSlot()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }

    [Fact]
    public void CancellingMarksTheSlotCancelled()
    {
        var slot = ActiveSlot();

        slot.Cancel();

        Assert.Equal(ConfirmedSlotStatus.Cancelled, slot.Status);
    }

    [Fact]
    public void ACancelledSlotOffersNoSpareCapacityEvenWhenItsCountersAreFull()
    {
        var slot = ActiveSlot();
        Assert.True(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));

        slot.Cancel();

        Assert.False(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public void CancellingTwiceIsRejected()
    {
        var slot = ActiveSlot();
        slot.Cancel();

        var ex = Assert.Throws<DomainException>(() => slot.Cancel());
        Assert.Equal("This slot has already been cancelled.", ex.Message);
    }
}
`````

## tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs","encoding":"utf8","sha256":"9f58ec736f48b877d04aeea8166a71c1bfd5d1875388b8dbf31836175deea748","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotImportTests
{
    private static readonly SlotWindow Window = new(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

    private static Dictionary<Guid, int> FullHeadcounts(int dat = 10, int med = 6, int uni = 8) => new()
    {
        [AppointmentTypeIds.DrugAndAlcoholTesting] = dat,
        [AppointmentTypeIds.MedicalCheckUp] = med,
        [AppointmentTypeIds.UniformFitting] = uni,
    };

    [Fact]
    public void AnImportedSlotHasNoProposalAndIsActive()
    {
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());

        Assert.Null(slot.ProposalId);
        Assert.Equal(Window, slot.Window);
        Assert.Equal(ConfirmedSlotStatus.Active, slot.Status);
    }

    [Fact]
    public void EachAppointmentTypeGetsItsOwnHeadcountAsBothTotalAndRemaining()
    {
        var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(10, 6, 8));

        Assert.Equal(3, slot.Capacities.Count);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void AMissingAppointmentTypeIsRejected()
    {
        var incomplete = new Dictionary<Guid, int>
        {
            [AppointmentTypeIds.DrugAndAlcoholTesting] = 10,
            [AppointmentTypeIds.MedicalCheckUp] = 6,
        };

        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, incomplete));
    }

    [Fact]
    public void AnExtraAppointmentTypeIsRejected()
    {
        var headcounts = FullHeadcounts();
        headcounts.Add(Guid.NewGuid(), 4);

        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, headcounts));
    }

    [Fact]
    public void ANonPositiveHeadcountIsRejected()
    {
        Assert.Throws<DomainException>(
            () => ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts(dat: 0)));
    }

    [Fact]
    public void TwoImportedSlotsAreIndependentEntities()
    {
        var first = ConfirmedSlot.CreateImported(Guid.NewGuid(), Window, FullHeadcounts());
        var second = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, 11), new TimeOnly(13, 0)), FullHeadcounts());

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(first.ProposalId);
        Assert.Null(second.ProposalId);
    }
}
`````

## tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs","encoding":"utf8","sha256":"bae5346589b445248440be01473130a0286031a46dd72da6f373f03cb8e6a63b","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ConfirmedSlotTests
{
    private static SlotProposal FullyAcceptedProposal(
        int drugAndAlcohol = 10, int medical = 6, int uniform = 8)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), drugAndAlcohol);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), medical);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), uniform);
        return proposal;
    }

    [Fact]
    public void ConfirmingCarriesTheWindowAndMarksTheProposalConfirmed()
    {
        var proposal = FullyAcceptedProposal();

        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Equal(proposal.Id, slot.ProposalId);
        Assert.Equal(proposal.Window, slot.Window);
        Assert.Equal(ConfirmedSlotStatus.Active, slot.Status);
        Assert.Equal(SlotProposalStatus.Confirmed, proposal.Status);
    }

    [Fact]
    public void ConfirmingCreatesOneCapacityCounterPerAppointmentType()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Equal(3, slot.Capacities.Count);
        Assert.Equal(
            AppointmentTypeIds.All.OrderBy(id => id),
            slot.Capacities.Select(c => c.AppointmentTypeId).OrderBy(id => id));
    }

    [Fact]
    public void EachCounterStartsAtTheHeadcountItsManagerAccepted()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal(10, 6, 8));

        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).TotalHeadcount);
        Assert.Equal(10, slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(6, slot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(8, slot.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
    }

    [Fact]
    public void EveryCounterBelongsToTheSlotThatOwnsIt()
    {
        var id = Guid.NewGuid();

        var slot = ConfirmedSlot.CreateFrom(id, FullyAcceptedProposal());

        Assert.All(slot.Capacities, c => Assert.Equal(id, c.ConfirmedSlotId));
    }

    [Fact]
    public void APartlyAcceptedProposalCannotBeConfirmed()
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);

        var ex = Assert.Throws<DomainException>(() => ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        Assert.Equal("A proposal can only be confirmed once all 3 managers have accepted it.", ex.Message);
    }

    [Fact]
    public void AProposalCannotBeConfirmedTwice()
    {
        var proposal = FullyAcceptedProposal();
        ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        Assert.Throws<DomainException>(() => ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
    }

    [Fact]
    public void CapacityForAnUnknownAppointmentTypeIsRejected()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.Throws<DomainException>(() => slot.CapacityFor(Guid.NewGuid()));
    }

    [Fact]
    public void SpareCapacityIsCheckedAcrossEveryRequiredType()
    {
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), FullyAcceptedProposal());

        Assert.True(slot.HasSpareCapacityForAll(
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        Assert.True(slot.HasSpareCapacityForAll(AppointmentTypeIds.All));
    }
}
`````

## tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs","encoding":"utf8","sha256":"60f7ee50ac59ad93f8a6e21d5d1535f72526294ab59afdd760cfb998ed224ffd","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Domain.Tests.Slots;

public class ProposalAcceptanceHeadcountRevisionTests
{
    private static readonly Guid DrugAndAlcoholManager =
        Guid.Parse("c0000001-0000-0000-0000-000000000001");
    private static readonly Guid FormerDrugAndAlcoholManager =
        Guid.Parse("c0000011-0000-0000-0000-000000000011");
    private static readonly Guid MedicalManager =
        Guid.Parse("c0000002-0000-0000-0000-000000000002");
    private static readonly Guid UniformManager =
        Guid.Parse("c0000003-0000-0000-0000-000000000003");

    private static SlotProposal NewProposal() =>
        SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0)),
            DrugAndAlcoholManager);

    [Fact]
    public void TheSameManagerRevisesTheExistingAcceptanceInPlace()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        var original = Assert.Single(proposal.Acceptances);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Same(original, Assert.Single(proposal.Acceptances));
        Assert.Equal(12, original.Headcount);
    }

    [Fact]
    public void ResubmittingTheCurrentHeadcountReportsNoChange()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        Assert.False(changed);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AReplacementManagerRevisesTheFormerManagersAcceptance()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, FormerDrugAndAlcoholManager, 10);

        var changed = proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12);

        Assert.True(changed);
        Assert.Equal(12, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void AnInvalidRevisionLeavesTheCurrentHeadcountUntouched(int headcount)
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, headcount));

        Assert.Equal("headcount must be greater than zero.", ex.Message);
        Assert.Equal(10, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AWithdrawnProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Withdraw(DrugAndAlcoholManager);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.MedicalCheckUp, MedicalManager, 8));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(6, Assert.Single(proposal.Acceptances).Headcount);
    }

    [Fact]
    public void AConfirmedProposalCannotHaveAnAcceptanceRevised()
    {
        var proposal = NewProposal();
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, MedicalManager, 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, UniformManager, 8);
        ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        var ex = Assert.Throws<DomainException>(() => proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManager, 12));

        Assert.Equal("Only an open proposal can be accepted.", ex.Message);
        Assert.Equal(
            10,
            proposal.Acceptances.Single(acceptance =>
                acceptance.AppointmentTypeId
                == AppointmentTypeIds.DrugAndAlcoholTesting).Headcount);
    }
}
`````
