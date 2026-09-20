# 00b — Vocabulary edits 21 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Domain/Slots/ProposalAcceptance.cs — 1/1

<!-- vocabulary-file: {"id":115,"oldPath":"src/EventBooking.Domain/Slots/ProposalAcceptance.cs","newPath":"src/EventBooking.Domain/Events/ProposalAcceptance.cs","beforeSha":"56f058bd91c19cfa45b5fa92e9495a684b7a1bfc884e96f660aff045b8add3ea","afterSha":"10a2260ffd07f4920d88514f6f327856eb88151ff3e3c4bfec9b040b663665cf","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>One manager's acceptance of a proposal, carrying their own headcount.</summary>
public sealed class ProposalAcceptance
{
    private ProposalAcceptance()
    {
    }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines manager user id for the current use case.</summary>
    public Guid ManagerUserId { get; private set; }

    /// <summary>Defines headcount for the current use case.</summary>
    public int Headcount { get; private set; }

    internal static ProposalAcceptance Record(
        Guid proposalId,
        Guid appointmentTypeId,
        Guid managerUserId,
        int headcount)
    {
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        return new ProposalAcceptance
        {
            ProposalId = proposalId,
            AppointmentTypeId = appointmentTypeId,
            ManagerUserId = managerUserId,
            Headcount = Guard.Positive(headcount, "headcount"),
        };
    }

    internal bool ChangeHeadcount(int headcount)
    {
        var next = Guard.Positive(headcount, "headcount");
        if (next == Headcount)
        {
            return false;
        }

        Headcount = next;
        return true;
    }
}
`````

## after — src/EventBooking.Domain/Events/ProposalAcceptance.cs — 1/1

<!-- vocabulary-file: {"id":115,"oldPath":"src/EventBooking.Domain/Slots/ProposalAcceptance.cs","newPath":"src/EventBooking.Domain/Events/ProposalAcceptance.cs","beforeSha":"56f058bd91c19cfa45b5fa92e9495a684b7a1bfc884e96f660aff045b8add3ea","afterSha":"10a2260ffd07f4920d88514f6f327856eb88151ff3e3c4bfec9b040b663665cf","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>One manager's acceptance of a proposal, carrying their own headcount.</summary>
public sealed class ProposalAcceptance
{
    private ProposalAcceptance()
    {
    }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines manager user id for the current use case.</summary>
    public Guid ManagerUserId { get; private set; }

    /// <summary>Defines headcount for the current use case.</summary>
    public int Headcount { get; private set; }

    internal static ProposalAcceptance Record(
        Guid proposalId,
        Guid appointmentTypeId,
        Guid managerUserId,
        int headcount)
    {
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        return new ProposalAcceptance
        {
            ProposalId = proposalId,
            AppointmentTypeId = appointmentTypeId,
            ManagerUserId = managerUserId,
            Headcount = Guard.Positive(headcount, "headcount"),
        };
    }

    internal bool ChangeHeadcount(int headcount)
    {
        var next = Guard.Positive(headcount, "headcount");
        if (next == Headcount)
        {
            return false;
        }

        Headcount = next;
        return true;
    }
}
`````

## before — src/EventBooking.Domain/Slots/SlotCapacity.cs — 1/1

<!-- vocabulary-file: {"id":116,"oldPath":"src/EventBooking.Domain/Slots/SlotCapacity.cs","newPath":"src/EventBooking.Domain/Events/EventCapacity.cs","beforeSha":"e917831f5ce494e22577cab38b0b3e1bd5b01098909f35a512893b3a7f166d32","afterSha":"b7dcfc6afac4991f17c5cc6549dd6d68d4f4289cb1d1b1b6486cd1e24aa54162","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>
/// Remaining bookable headcount for one appointment type on one confirmed slot. This row is the
/// single source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class SlotCapacity
{
    private SlotCapacity()
    {
    }

    /// <summary>Defines confirmed slot id for the current use case.</summary>
    public Guid ConfirmedSlotId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines total headcount for the current use case.</summary>
    public int TotalHeadcount { get; private set; }

    /// <summary>Defines remaining capacity for the current use case.</summary>
    public int RemainingCapacity { get; private set; }

    /// <summary>Defines has spare for the current use case.</summary>
    public bool HasSpare => RemainingCapacity > 0;

    /// <summary>Defines occupied capacity for the current use case.</summary>
    public int OccupiedCapacity => TotalHeadcount - RemainingCapacity;

    internal static SlotCapacity Initialise(Guid confirmedSlotId, Guid appointmentTypeId, int totalHeadcount)
    {
        AppointmentTypeIdsGuard(appointmentTypeId);

        var total = Guard.Positive(totalHeadcount, "totalHeadcount");

        return new SlotCapacity
        {
            ConfirmedSlotId = confirmedSlotId,
            AppointmentTypeId = appointmentTypeId,
            TotalHeadcount = total,
            RemainingCapacity = total,
        };
    }

    /// <summary>Defines decrement for the current use case.</summary>
    public void Decrement()
    {
        Guard.Against(
            RemainingCapacity <= 0,
            "No remaining capacity for this appointment type on this slot.");

        RemainingCapacity -= 1;
    }

    /// <summary>Defines increment for the current use case.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>Defines adjust total headcount for the current use case.</summary>
    /// <param name="totalHeadcount">The total headcount.</param>
    public bool AdjustTotalHeadcount(int totalHeadcount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        if (next == TotalHeadcount)
        {
            return false;
        }

        Guard.Against(
            next < OccupiedCapacity,
            "totalHeadcount cannot be lower than occupied capacity.");

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;
        return true;
    }

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
`````

## after — src/EventBooking.Domain/Events/EventCapacity.cs — 1/1

<!-- vocabulary-file: {"id":116,"oldPath":"src/EventBooking.Domain/Slots/SlotCapacity.cs","newPath":"src/EventBooking.Domain/Events/EventCapacity.cs","beforeSha":"e917831f5ce494e22577cab38b0b3e1bd5b01098909f35a512893b3a7f166d32","afterSha":"b7dcfc6afac4991f17c5cc6549dd6d68d4f4289cb1d1b1b6486cd1e24aa54162","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// Remaining bookable headcount for one appointment type on one eventItem. This row is the
/// single source of truth for booking eligibility, and the row the confirm transaction locks.
/// </summary>
public sealed class EventCapacity
{
    private EventCapacity()
    {
    }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid AppointmentTypeId { get; private set; }

    /// <summary>Defines total headcount for the current use case.</summary>
    public int TotalHeadcount { get; private set; }

    /// <summary>Defines remaining capacity for the current use case.</summary>
    public int RemainingCapacity { get; private set; }

    /// <summary>Defines has spare for the current use case.</summary>
    public bool HasSpare => RemainingCapacity > 0;

    /// <summary>Defines occupied capacity for the current use case.</summary>
    public int OccupiedCapacity => TotalHeadcount - RemainingCapacity;

    internal static EventCapacity Initialise(Guid eventId, Guid appointmentTypeId, int totalHeadcount)
    {
        AppointmentTypeIdsGuard(appointmentTypeId);

        var total = Guard.Positive(totalHeadcount, "totalHeadcount");

        return new EventCapacity
        {
            EventId = eventId,
            AppointmentTypeId = appointmentTypeId,
            TotalHeadcount = total,
            RemainingCapacity = total,
        };
    }

    /// <summary>Defines decrement for the current use case.</summary>
    public void Decrement()
    {
        Guard.Against(
            RemainingCapacity <= 0,
            "No remaining capacity for this appointment type on this eventItem.");

        RemainingCapacity -= 1;
    }

    /// <summary>Defines increment for the current use case.</summary>
    public void Increment()
    {
        Guard.Against(
            RemainingCapacity >= TotalHeadcount,
            "Remaining capacity cannot exceed the headcount the manager accepted.");

        RemainingCapacity += 1;
    }

    /// <summary>Defines adjust total headcount for the current use case.</summary>
    /// <param name="totalHeadcount">The total headcount.</param>
    public bool AdjustTotalHeadcount(int totalHeadcount)
    {
        var next = Guard.Positive(totalHeadcount, "totalHeadcount");
        if (next == TotalHeadcount)
        {
            return false;
        }

        Guard.Against(
            next < OccupiedCapacity,
            "totalHeadcount cannot be lower than occupied capacity.");

        var delta = next - TotalHeadcount;
        TotalHeadcount = next;
        RemainingCapacity += delta;
        return true;
    }

    private static void AppointmentTypeIdsGuard(Guid appointmentTypeId) =>
        AppointmentTypes.AppointmentTypeIds.EnsureKnown(appointmentTypeId);
}
`````

## before — src/EventBooking.Domain/Slots/SlotProposal.cs — 1/1

<!-- vocabulary-file: {"id":117,"oldPath":"src/EventBooking.Domain/Slots/SlotProposal.cs","newPath":"src/EventBooking.Domain/Events/EventProposal.cs","beforeSha":"71f545967934064c041595534dbc62593155c120a5e453886f733ac3447043c2","afterSha":"42f672ffcf6626ea4644697aa738b01434f818e701891cf8d0ad1f1e591b141e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Domain.Slots;

/// <summary>Defines slot proposal for the current use case.</summary>
public sealed class SlotProposal
{
    private readonly List<ProposalAcceptance> _acceptances = [];

    private SlotProposal()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public SlotWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public SlotProposalStatus Status { get; private set; } = SlotProposalStatus.Open;

    /// <summary>Defines created by manager user id for the current use case.</summary>
    public Guid CreatedByManagerUserId { get; private set; }

    /// <summary>Defines acceptances for the current use case.</summary>
    public IReadOnlyList<ProposalAcceptance> Acceptances => _acceptances;

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="createdByManagerUserId">The created by manager user id.</param>
    public static SlotProposal Create(Guid id, SlotWindow window, Guid createdByManagerUserId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(createdByManagerUserId == Guid.Empty, "createdByManagerUserId must not be empty.");

        return new SlotProposal
        {
            Id = id,
            Window = window!,
            Status = SlotProposalStatus.Open,
            CreatedByManagerUserId = createdByManagerUserId,
        };
    }

    /// <summary>Withdraws an open proposal. Any Manager in scope or Admin may act, not just the creator.</summary>
    /// <param name="managerUserId">The manager user id.</param>
    public void Withdraw(Guid managerUserId)
    {
        Guard.Against(Status != SlotProposalStatus.Open, "Only an open proposal can be withdrawn.");
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        Status = SlotProposalStatus.Withdrawn;
    }

    /// <summary>Defines accept for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    /// <param name="headcount">The headcount.</param>
    public bool Accept(Guid appointmentTypeId, Guid managerUserId, int headcount)
    {
        Guard.Against(Status != SlotProposalStatus.Open, "Only an open proposal can be accepted.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var existing = _acceptances.SingleOrDefault(
            acceptance => acceptance.AppointmentTypeId == appointmentTypeId);

        if (existing is null)
        {
            _acceptances.Add(
                ProposalAcceptance.Record(Id, appointmentTypeId, managerUserId, headcount));
            return true;
        }

        return existing.ChangeHeadcount(headcount);
    }

    /// <summary>Defines withdraw acceptance for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    public void WithdrawAcceptance(Guid appointmentTypeId, Guid managerUserId)
    {
        Guard.Against(
            Status != SlotProposalStatus.Open,
            "An acceptance can only be withdrawn while the proposal is still open.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var acceptance = _acceptances.SingleOrDefault(a => a.AppointmentTypeId == appointmentTypeId);
        Guard.Against(acceptance is null, "This appointment type has not accepted the proposal.");

        _acceptances.Remove(acceptance!);
    }

    /// <summary>Defines is accepted by for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public bool IsAcceptedBy(Guid appointmentTypeId) =>
        _acceptances.Any(a => a.AppointmentTypeId == appointmentTypeId);

    /// <summary>Defines is fully accepted for the current use case.</summary>
    public bool IsFullyAccepted =>
        Status == SlotProposalStatus.Open
        && AppointmentTypeIds.All.All(IsAcceptedBy);

    internal void MarkConfirmed()
    {
        Guard.Against(!IsFullyAccepted, "A proposal can only be confirmed once all 3 managers have accepted it.");
        Status = SlotProposalStatus.Confirmed;
    }
}
`````

## after — src/EventBooking.Domain/Events/EventProposal.cs — 1/1

<!-- vocabulary-file: {"id":117,"oldPath":"src/EventBooking.Domain/Slots/SlotProposal.cs","newPath":"src/EventBooking.Domain/Events/EventProposal.cs","beforeSha":"71f545967934064c041595534dbc62593155c120a5e453886f733ac3447043c2","afterSha":"42f672ffcf6626ea4644697aa738b01434f818e701891cf8d0ad1f1e591b141e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Domain.Events;

/// <summary>Defines event proposal for the current use case.</summary>
public sealed class EventProposal
{
    private readonly List<ProposalAcceptance> _acceptances = [];

    private EventProposal()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventProposalStatus Status { get; private set; } = EventProposalStatus.Open;

    /// <summary>Defines created by manager user id for the current use case.</summary>
    public Guid CreatedByManagerUserId { get; private set; }

    /// <summary>Defines acceptances for the current use case.</summary>
    public IReadOnlyList<ProposalAcceptance> Acceptances => _acceptances;

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="createdByManagerUserId">The created by manager user id.</param>
    public static EventProposal Create(Guid id, EventWindow window, Guid createdByManagerUserId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(createdByManagerUserId == Guid.Empty, "createdByManagerUserId must not be empty.");

        return new EventProposal
        {
            Id = id,
            Window = window!,
            Status = EventProposalStatus.Open,
            CreatedByManagerUserId = createdByManagerUserId,
        };
    }

    /// <summary>Withdraws an open proposal. Any Manager in scope or Admin may act, not just the creator.</summary>
    /// <param name="managerUserId">The manager user id.</param>
    public void Withdraw(Guid managerUserId)
    {
        Guard.Against(Status != EventProposalStatus.Open, "Only an open proposal can be withdrawn.");
        Guard.Against(managerUserId == Guid.Empty, "managerUserId must not be empty.");

        Status = EventProposalStatus.Withdrawn;
    }

    /// <summary>Defines accept for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    /// <param name="headcount">The headcount.</param>
    public bool Accept(Guid appointmentTypeId, Guid managerUserId, int headcount)
    {
        Guard.Against(Status != EventProposalStatus.Open, "Only an open proposal can be accepted.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var existing = _acceptances.SingleOrDefault(
            acceptance => acceptance.AppointmentTypeId == appointmentTypeId);

        if (existing is null)
        {
            _acceptances.Add(
                ProposalAcceptance.Record(Id, appointmentTypeId, managerUserId, headcount));
            return true;
        }

        return existing.ChangeHeadcount(headcount);
    }

    /// <summary>Defines withdraw acceptance for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    /// <param name="managerUserId">The manager user id.</param>
    public void WithdrawAcceptance(Guid appointmentTypeId, Guid managerUserId)
    {
        Guard.Against(
            Status != EventProposalStatus.Open,
            "An acceptance can only be withdrawn while the proposal is still open.");
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var acceptance = _acceptances.SingleOrDefault(a => a.AppointmentTypeId == appointmentTypeId);
        Guard.Against(acceptance is null, "This appointment type has not accepted the proposal.");

        _acceptances.Remove(acceptance!);
    }

    /// <summary>Defines is accepted by for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public bool IsAcceptedBy(Guid appointmentTypeId) =>
        _acceptances.Any(a => a.AppointmentTypeId == appointmentTypeId);

    /// <summary>Defines is fully accepted for the current use case.</summary>
    public bool IsFullyAccepted =>
        Status == EventProposalStatus.Open
        && AppointmentTypeIds.All.All(IsAcceptedBy);

    internal void MarkConfirmed()
    {
        Guard.Against(!IsFullyAccepted, "A proposal can only be confirmed once all 3 managers have accepted it.");
        Status = EventProposalStatus.Confirmed;
    }
}
`````

## before — src/EventBooking.Domain/Slots/SlotProposalStatus.cs — 1/1

<!-- vocabulary-file: {"id":118,"oldPath":"src/EventBooking.Domain/Slots/SlotProposalStatus.cs","newPath":"src/EventBooking.Domain/Events/EventProposalStatus.cs","beforeSha":"b432848d2379e93d3da7d95f3e108b2ddca5c70c05e873465bf09074eddeb8fb","afterSha":"9c8c5ccdfa2bbed58b3376136bf82e1d553c1a57c9f3946cc8dbe9ec1fac71d4","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Slots;

/// <summary>Defines slot proposal status for the current use case.</summary>
public enum SlotProposalStatus
{
    /// <summary>Defines open for the current use case.</summary>
    Open = 1,
    /// <summary>Defines withdrawn for the current use case.</summary>
    Withdrawn = 2,
    /// <summary>Defines confirmed for the current use case.</summary>
    Confirmed = 3,
}
`````

## after — src/EventBooking.Domain/Events/EventProposalStatus.cs — 1/1

<!-- vocabulary-file: {"id":118,"oldPath":"src/EventBooking.Domain/Slots/SlotProposalStatus.cs","newPath":"src/EventBooking.Domain/Events/EventProposalStatus.cs","beforeSha":"b432848d2379e93d3da7d95f3e108b2ddca5c70c05e873465bf09074eddeb8fb","afterSha":"9c8c5ccdfa2bbed58b3376136bf82e1d553c1a57c9f3946cc8dbe9ec1fac71d4","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Events;

/// <summary>Defines event proposal status for the current use case.</summary>
public enum EventProposalStatus
{
    /// <summary>Defines open for the current use case.</summary>
    Open = 1,
    /// <summary>Defines withdrawn for the current use case.</summary>
    Withdrawn = 2,
    /// <summary>Defines confirmed for the current use case.</summary>
    Confirmed = 3,
}
`````

## before — src/EventBooking.Domain/Slots/SlotWindow.cs — 1/1

<!-- vocabulary-file: {"id":119,"oldPath":"src/EventBooking.Domain/Slots/SlotWindow.cs","newPath":"src/EventBooking.Domain/Events/EventWindow.cs","beforeSha":"915ec92574247cfff33ff6c0654fdaf8a35480937bcf3f32e564c7e14873db85","afterSha":"0070a97b34ee2c8050a3c39f6f4e31c638b2520f552f8e4617a1d04d57008912","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Slots;

/// <summary>
/// The 4-hour candidate-facing window. Duration is fixed by the domain, so only the date and the
/// start time are ever stored; the end time is always derived.
/// </summary>
public sealed record SlotWindow : IComparable<SlotWindow>
{
    /// <summary>Defines duration for the current use case.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromHours(4);

    /// <summary>Defines slot window for the current use case.</summary>
    /// <param name="date">The date.</param>
    /// <param name="startTime">The start time.</param>
    public SlotWindow(DateOnly date, TimeOnly startTime)
    {
        Guard.Against(
            startTime.ToTimeSpan() + Duration > TimeSpan.FromHours(24),
            "startTime must leave room for the full 4-hour window on the same day.");

        Date = date;
        StartTime = startTime;
    }

    /// <summary>Defines date for the current use case.</summary>
    public DateOnly Date { get; }

    /// <summary>Defines start time for the current use case.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Defines end time for the current use case.</summary>
    public TimeOnly EndTime => StartTime.Add(Duration);

    /// <summary>Defines starts after for the current use case.</summary>
    /// <param name="today">The today.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>Defines compare to for the current use case.</summary>
    /// <param name="other">The other.</param>
    public int CompareTo(SlotWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Defines to string for the current use case.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
`````

## after — src/EventBooking.Domain/Events/EventWindow.cs — 1/1

<!-- vocabulary-file: {"id":119,"oldPath":"src/EventBooking.Domain/Slots/SlotWindow.cs","newPath":"src/EventBooking.Domain/Events/EventWindow.cs","beforeSha":"915ec92574247cfff33ff6c0654fdaf8a35480937bcf3f32e564c7e14873db85","afterSha":"0070a97b34ee2c8050a3c39f6f4e31c638b2520f552f8e4617a1d04d57008912","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// The 4-hour attendee-facing window. Duration is fixed by the domain, so only the date and the
/// start time are ever stored; the end time is always derived.
/// </summary>
public sealed record EventWindow : IComparable<EventWindow>
{
    /// <summary>Defines duration for the current use case.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromHours(4);

    /// <summary>Defines event window for the current use case.</summary>
    /// <param name="date">The date.</param>
    /// <param name="startTime">The start time.</param>
    public EventWindow(DateOnly date, TimeOnly startTime)
    {
        Guard.Against(
            startTime.ToTimeSpan() + Duration > TimeSpan.FromHours(24),
            "startTime must leave room for the full 4-hour window on the same day.");

        Date = date;
        StartTime = startTime;
    }

    /// <summary>Defines date for the current use case.</summary>
    public DateOnly Date { get; }

    /// <summary>Defines start time for the current use case.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Defines end time for the current use case.</summary>
    public TimeOnly EndTime => StartTime.Add(Duration);

    /// <summary>Defines starts after for the current use case.</summary>
    /// <param name="today">The today.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>Defines compare to for the current use case.</summary>
    /// <param name="other">The other.</param>
    public int CompareTo(EventWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Defines to string for the current use case.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
`````

## before — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- vocabulary-file: {"id":120,"oldPath":"src/EventBooking.Infrastructure/DependencyInjection.cs","newPath":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"56e223322fb4ff37a3ead1219c9f1abeccce7e160732fc1655fd32bd4cb0c9ec","afterSha":"e8e1c7f56dd7943b0d7a41eb4e2c5c204e7e7a49d7ab026e54f599e6c0d9f4e4","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventBookingPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered as a factory, with a scoped context created from it. Task 51's email sender
        // needs a context of its own that is not tied to the request's unit of work, and this is
        // the pattern that gives it one without a second registration of the context type.
        services.AddSingleton<StatusStampingInterceptor>();
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<StatusStampingInterceptor>()));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<ISlotProposalRepository, SlotProposalRepository>();
        services.AddScoped<IConfirmedSlotRepository, ConfirmedSlotRepository>();
        services.AddScoped<ISlotCapacityRepository, SlotCapacityRepository>();
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IEmployeeGroupRepository, EmployeeGroupRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingAppointmentRepository, BookingAppointmentRepository>();
        services.AddScoped<IStaffAccessProfileRepository, StaffAccessProfileRepository>();
        services.AddScoped<IStaffIdentityRepository, StaffIdentityRepository>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<ICandidateReadinessQueries, CandidateReadinessQueries>();
        services.AddScoped<ICandidateBookingQueries, CandidateBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        HeadOfficeOptions headOffice,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(headOffice);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## after — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- vocabulary-file: {"id":120,"oldPath":"src/EventBooking.Infrastructure/DependencyInjection.cs","newPath":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"56e223322fb4ff37a3ead1219c9f1abeccce7e160732fc1655fd32bd4cb0c9ec","afterSha":"e8e1c7f56dd7943b0d7a41eb4e2c5c204e7e7a49d7ab026e54f599e6c0d9f4e4","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventBookingPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered as a factory, with a scoped context created from it. Task 51's email sender
        // needs a context of its own that is not tied to the request's unit of work, and this is
        // the pattern that gives it one without a second registration of the context type.
        services.AddSingleton<StatusStampingInterceptor>();
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<StatusStampingInterceptor>()));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<IEventProposalRepository, EventProposalRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventCapacityRepository, EventCapacityRepository>();
        services.AddScoped<IAttendeeRepository, AttendeeRepository>();
        services.AddScoped<IAttendeeGroupRepository, AttendeeGroupRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingAppointmentRepository, BookingAppointmentRepository>();
        services.AddScoped<IStaffAccessProfileRepository, StaffAccessProfileRepository>();
        services.AddScoped<IStaffIdentityRepository, StaffIdentityRepository>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<IAttendeeReadinessQueries, AttendeeReadinessQueries>();
        services.AddScoped<IAttendeeBookingQueries, AttendeeBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        TransitionalLocationOptions transitionalLocation,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(transitionalLocation);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## before — src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs — 1/1

<!-- vocabulary-file: {"id":121,"oldPath":"src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs","newPath":"src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs","beforeSha":"6eab1f5ceb7399d58887cebfabdba939c9efe3c388c1e44398b5bbc54b1d0c4f","afterSha":"2c35eb0d90a0ec41f286793253fdddd61d95618570f805c323ce9c96c41604d0","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// Sends through the transport and records one email log row either way. Returns false rather than
/// throwing: a bounced confirmation must never roll back a good booking.
/// </summary>
public sealed class LoggingEmailSender(
    IEmailTransport transport,
    IDbContextFactory<EventBookingDbContext> contextFactory,
    IClock clock,
    ILogger<LoggingEmailSender> logger) : IEmailSender
{
    /// <summary>
    /// Sends through the configured transport and returns a provider outcome. Coordinated durable
    /// deliveries already own their <see cref="EmailLog"/> row and therefore are not logged twice.
    /// </summary>
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var status = EmailStatus.Sent;

        try
        {
            await transport.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status = EmailStatus.Failed;
            logger.LogError(
                ex,
                "Sending the {Template} email to candidate {CandidateId} failed.",
                message.Template,
                message.CandidateId);
        }

        if (message.DeliveryId is null)
        {
            await RecordAsync(message, status, cancellationToken);
        }

        return status == EmailStatus.Sent;
    }

    private async Task RecordAsync(
        EmailMessage message,
        EmailStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            // A context of its own: the confirmation email is sent after its booking transaction
            // has already committed, so there is no unit of work left to save this row on.
            await using var context = contextFactory.CreateDbContext();

            context.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), message.CandidateId, message.Template, clock.UtcNow, status));

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Losing the audit row is bad, but not as bad as failing the caller's operation for it.
            logger.LogWarning(ex, "Writing the email log row failed.");
        }
    }
}
`````

## after — src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs — 1/1

<!-- vocabulary-file: {"id":121,"oldPath":"src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs","newPath":"src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs","beforeSha":"6eab1f5ceb7399d58887cebfabdba939c9efe3c388c1e44398b5bbc54b1d0c4f","afterSha":"2c35eb0d90a0ec41f286793253fdddd61d95618570f805c323ce9c96c41604d0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventBooking.Infrastructure.Email;

/// <summary>
/// Sends through the transport and records one email log row either way. Returns false rather than
/// throwing: a bounced confirmation must never roll back a good booking.
/// </summary>
public sealed class LoggingEmailSender(
    IEmailTransport transport,
    IDbContextFactory<EventBookingDbContext> contextFactory,
    IClock clock,
    ILogger<LoggingEmailSender> logger) : IEmailSender
{
    /// <summary>
    /// Sends through the configured transport and returns a provider outcome. Coordinated durable
    /// deliveries already own their <see cref="EmailLog"/> row and therefore are not logged twice.
    /// </summary>
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var status = EmailStatus.Sent;

        try
        {
            await transport.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            status = EmailStatus.Failed;
            logger.LogError(
                ex,
                "Sending the {Template} email to attendee {AttendeeId} failed.",
                message.Template,
                message.AttendeeId);
        }

        if (message.DeliveryId is null)
        {
            await RecordAsync(message, status, cancellationToken);
        }

        return status == EmailStatus.Sent;
    }

    private async Task RecordAsync(
        EmailMessage message,
        EmailStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            // A context of its own: the confirmation email is sent after its booking transaction
            // has already committed, so there is no unit of work left to save this row on.
            await using var context = contextFactory.CreateDbContext();

            context.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), message.AttendeeId, message.Template, clock.UtcNow, status));

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Losing the audit row is bad, but not as bad as failing the caller's operation for it.
            logger.LogWarning(ex, "Writing the email log row failed.");
        }
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":122,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs","beforeSha":"59cb0f90f0a4ca23a0fd09b0b51b7a10f7fe36fdfa014defbe88f96b76e5d5c1","afterSha":"a710f65da5c99a3fb6672f85b888d7714768c2a8a7aa5b453727eff06c354c69","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps booking persistence and its one-active-booking-per-candidate backstop.</summary>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    /// <summary>Configures booking columns and the filtered active-booking index.</summary>
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable(
            "booking",
            table => table.HasCheckConstraint(
                "ck_booking_no_self_recovery",
                "recovery_of_booking_id IS NULL OR recovery_of_booking_id <> id"));
        builder.HasKey(b => b.Id);
        builder.Ignore(b => b.IsOriginal);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.CandidateId).HasColumnName("candidate_id");
        builder.Property(b => b.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(b => b.InviteId).HasColumnName("invite_id");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(b => b.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");
        builder
            .Property(b => b.ManageTokenHash)
            .HasColumnName("manage_token_hash")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(b => b.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ManageTokenHash).IsUnique();
        builder.HasIndex(b => new { b.ConfirmedSlotId, b.Status });
        builder.HasIndex(b => new { b.CandidateId, b.Status });
        builder.HasIndex(b => b.RecoveryOfBookingId);
        builder.HasIndex(b => b.CandidateId)
            .HasDatabaseName("ux_booking_active_original_candidate")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NULL")
            .IsUnique();
        builder.HasIndex(b => b.RecoveryOfBookingId)
            .HasDatabaseName("ux_booking_active_recovery")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NOT NULL")
            .IsUnique();
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":122,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs","beforeSha":"59cb0f90f0a4ca23a0fd09b0b51b7a10f7fe36fdfa014defbe88f96b76e5d5c1","afterSha":"a710f65da5c99a3fb6672f85b888d7714768c2a8a7aa5b453727eff06c354c69","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps booking persistence and its one-active-booking-per-attendee backstop.</summary>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    /// <summary>Configures booking columns and the filtered active-booking index.</summary>
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable(
            "booking",
            table => table.HasCheckConstraint(
                "ck_booking_no_self_recovery",
                "recovery_of_booking_id IS NULL OR recovery_of_booking_id <> id"));
        builder.HasKey(b => b.Id);
        builder.Ignore(b => b.IsOriginal);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.AttendeeId).HasColumnName("attendee_id");
        builder.Property(b => b.EventId).HasColumnName("event_id");
        builder.Property(b => b.InviteId).HasColumnName("invite_id");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(b => b.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");
        builder
            .Property(b => b.ManageTokenHash)
            .HasColumnName("manage_token_hash")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(b => b.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ManageTokenHash).IsUnique();
        builder.HasIndex(b => new { b.EventId, b.Status });
        builder.HasIndex(b => new { b.AttendeeId, b.Status });
        builder.HasIndex(b => b.RecoveryOfBookingId);
        builder.HasIndex(b => b.AttendeeId)
            .HasDatabaseName("ux_booking_active_original_attendee")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NULL")
            .IsUnique();
        builder.HasIndex(b => b.RecoveryOfBookingId)
            .HasDatabaseName("ux_booking_active_recovery")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NOT NULL")
            .IsUnique();
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":123,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs","beforeSha":"93b49d2c01202cc457447fa44ea194cc02a2e65895385ad30c252935bd18c1ad","afterSha":"cfa95b1200e6684e654cd26daa4be9ad368aef4ee0d565819d3c78d87bdf23c8","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("candidate");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(c => c.EmployeeGroupId).HasColumnName("employee_group_id").IsRequired();
        builder.HasOne<EmployeeGroup>()
            .WithMany()
            .HasForeignKey(c => c.EmployeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.EmployeeGroupId);
        builder.Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty)
            .HasColumnName("status_changed_at");

        builder.Ignore(c => c.RequiredAppointmentTypeIds);

        builder
            .HasMany(c => c.Requirements)
            .WithOne()
            .HasForeignKey(r => r.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Status);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":123,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs","beforeSha":"93b49d2c01202cc457447fa44ea194cc02a2e65895385ad30c252935bd18c1ad","afterSha":"cfa95b1200e6684e654cd26daa4be9ad368aef4ee0d565819d3c78d87bdf23c8","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AttendeeConfiguration : IEntityTypeConfiguration<Attendee>
{
    public void Configure(EntityTypeBuilder<Attendee> builder)
    {
        builder.ToTable("attendee");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(c => c.AttendeeGroupId).HasColumnName("attendee_group_id").IsRequired();
        builder.HasOne<AttendeeGroup>()
            .WithMany()
            .HasForeignKey(c => c.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.AttendeeGroupId);
        builder.Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty)
            .HasColumnName("status_changed_at");

        builder.Ignore(c => c.RequiredAppointmentTypeIds);

        builder
            .HasMany(c => c.Requirements)
            .WithOne()
            .HasForeignKey(r => r.AttendeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Status);
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":124,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeRequirementConfiguration.cs","beforeSha":"1a3b8ac5bc422437960029faa6b1ab679fb1faab03303c52c8ba85d40a5ebfc7","afterSha":"dda093b866b3480487c1d644050b2d6f2893830608c78df3f9fb97f6a6102f29","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class CandidateRequirementConfiguration : IEntityTypeConfiguration<CandidateRequirement>
{
    public void Configure(EntityTypeBuilder<CandidateRequirement> builder)
    {
        builder.ToTable("candidate_requirement");
        builder.HasKey(r => new { r.CandidateId, r.AppointmentTypeId });

        builder.Property(r => r.CandidateId).HasColumnName("candidate_id");
        builder.Property(r => r.AppointmentTypeId).HasColumnName("appointment_type_id");
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeRequirementConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":124,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeRequirementConfiguration.cs","beforeSha":"1a3b8ac5bc422437960029faa6b1ab679fb1faab03303c52c8ba85d40a5ebfc7","afterSha":"dda093b866b3480487c1d644050b2d6f2893830608c78df3f9fb97f6a6102f29","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AttendeeRequirementConfiguration : IEntityTypeConfiguration<AttendeeRequirement>
{
    public void Configure(EntityTypeBuilder<AttendeeRequirement> builder)
    {
        builder.ToTable("attendee_requirement");
        builder.HasKey(r => new { r.AttendeeId, r.AppointmentTypeId });

        builder.Property(r => r.AttendeeId).HasColumnName("attendee_id");
        builder.Property(r => r.AppointmentTypeId).HasColumnName("appointment_type_id");
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":125,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"35deded7339cc21afaa0eeed73e9d7eb080f1abb8e7c9139a53baac8dd22f953","afterSha":"5d1a441c5c038d76d7b3d79d1a1c35cf437f8fa4de11922f2d3dff3787cf0cc2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class ConfirmedSlotConfiguration : IEntityTypeConfiguration<ConfirmedSlot>
{
    public void Configure(EntityTypeBuilder<ConfirmedSlot> builder)
    {
        builder.ToTable("confirmed_slot");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired(false);
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.ConfirmedSlotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":125,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"35deded7339cc21afaa0eeed73e9d7eb080f1abb8e7c9139a53baac8dd22f953","afterSha":"5d1a441c5c038d76d7b3d79d1a1c35cf437f8fa4de11922f2d3dff3787cf0cc2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired(false);
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":126,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs","beforeSha":"8ca10e21ff56a5a74fbb07892c335b0055f525f0feb13f4ee6b6926839423c10","afterSha":"cd6a3efedda6ac316895b3d0f972b7837302cacbc33ef44f58d38cd656a84add","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_log");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CandidateId).HasColumnName("candidate_id");
        builder.Property(e => e.TemplateName).HasColumnName("template_name").HasConversion<int>();
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(e => e.InviteId).HasColumnName("invite_id");
        builder.Property(e => e.BookingId).HasColumnName("booking_id");
        builder.Property(e => e.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(e => e.ClaimedAt).HasColumnName("claimed_at");

        builder.HasIndex(e => e.CandidateId);
        builder.HasIndex(e => new { e.CandidateId, e.SentAt });
    }
}
`````
