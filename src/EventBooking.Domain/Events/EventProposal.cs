using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Events;

/// <summary>
/// A Manager's offer of an event: one window at one location, offering a fixed list of appointment
/// types. It becomes an event when every listed type's Manager has accepted with their own
/// headcount (design 01 — Negotiation).
/// </summary>
public sealed class EventProposal
{
    /// <summary>The most appointment types one proposal may list (design 08).</summary>
    public const int MaximumListedTypes = 20;

    /// <summary>The largest headcount a Manager may accept with (design 08).</summary>
    public const int MaximumHeadcount = 1000;

    private readonly List<ProposalAcceptance> _acceptances = [];
    private readonly List<EventProposalAppointmentType> _listedTypes = [];

    private EventProposal()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>The proposal identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The location that would host the event.</summary>
    public Guid LocationId { get; private set; }

    /// <summary>The proposed window, read in the location's zone.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Where the proposal stands.</summary>
    public EventProposalStatus Status { get; private set; } = EventProposalStatus.Open;

    /// <summary>The person who proposed it, kept for audit attribution only.</summary>
    public Guid CreatedByManagerUserId { get; private set; }

    /// <summary>
    /// The proposing Manager's own appointment type. Withdrawing the proposal, and withdrawing the
    /// proposer's acceptance, are judged against this type, so a successor Manager inherits both.
    /// </summary>
    public Guid ProposerAppointmentTypeId { get; private set; }

    /// <summary>The listed types, fixed at creation.</summary>
    public IReadOnlyList<EventProposalAppointmentType> ListedTypes => _listedTypes;

    /// <summary>The listed appointment type identifiers.</summary>
    public IReadOnlyList<Guid> ListedAppointmentTypeIds =>
        [.. _listedTypes.Select(listed => listed.AppointmentTypeId)];

    /// <summary>The acceptances recorded so far, one per accepted type.</summary>
    public IReadOnlyList<ProposalAcceptance> Acceptances => _acceptances;

    /// <summary>Whether every listed type has accepted.</summary>
    public bool IsFullyAccepted =>
        Status == EventProposalStatus.Open
        && _listedTypes.Count > 0
        && ListedAppointmentTypeIds.All(IsAcceptedBy);

    /// <summary>
    /// Makes a proposal, refusing it with every failure it has rather than the first (FR-2.1,
    /// FR-2.2). The proposer's own acceptance is recorded here, because a proposal nobody has
    /// committed to is noise.
    /// </summary>
    /// <param name="id">The new proposal identifier.</param>
    /// <param name="locationId">The location that would host the event.</param>
    /// <param name="locationIsActive">Whether that location is active.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="window">The proposed window, in local time at the location.</param>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="listedTypes">The appointment types the event would offer.</param>
    /// <param name="proposerAppointmentTypeId">The proposing Manager's own type.</param>
    /// <param name="createdByManagerUserId">The proposing Manager, for audit attribution.</param>
    /// <param name="headcount">The proposer's own headcount.</param>
    public static EventProposal Propose(
        Guid id,
        Guid locationId,
        bool locationIsActive,
        string timeZoneId,
        EventWindow window,
        IEventWindowZones zones,
        DateTimeOffset now,
        IReadOnlyList<ProposableAppointmentType> listedTypes,
        Guid proposerAppointmentTypeId,
        Guid createdByManagerUserId,
        int headcount)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(listedTypes);
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(locationId == Guid.Empty, "locationId must not be empty.");
        Guard.Against(createdByManagerUserId == Guid.Empty, "createdByManagerUserId must not be empty.");

        var failures = Validate(
            locationIsActive, timeZoneId, window, zones, now, listedTypes, proposerAppointmentTypeId, headcount);
        if (failures.Count > 0)
        {
            throw new ProposalValidationException(failures);
        }

        var proposal = new EventProposal
        {
            Id = id,
            LocationId = locationId,
            Window = window,
            Status = EventProposalStatus.Open,
            CreatedByManagerUserId = createdByManagerUserId,
            ProposerAppointmentTypeId = proposerAppointmentTypeId,
        };

        foreach (var listed in listedTypes.OrderBy(type => type.Id))
        {
            proposal._listedTypes.Add(EventProposalAppointmentType.For(id, listed.Id));
        }

        proposal._acceptances.Add(
            ProposalAcceptance.Record(id, proposerAppointmentTypeId, createdByManagerUserId, headcount));

        return proposal;
    }

    /// <summary>
    /// Records or revises one listed type's acceptance (FR-2.4). Returns whether the headcount
    /// actually changed, so callers audit a real change and not a repeated save.
    /// </summary>
    /// <param name="appointmentTypeId">The accepting type, which must be listed.</param>
    /// <param name="managerUserId">The accepting Manager, for audit attribution.</param>
    /// <param name="headcount">How many attendees that team can take.</param>
    public bool Accept(Guid appointmentTypeId, Guid managerUserId, int headcount)
    {
        EnsureOpen();
        EnsureListed(appointmentTypeId);
        Guard.Against(
            headcount is < 1 or > MaximumHeadcount,
            $"headcount must be between 1 and {MaximumHeadcount}.");

        var existing = _acceptances.SingleOrDefault(
            acceptance => acceptance.AppointmentTypeId == appointmentTypeId);

        if (existing is null)
        {
            _acceptances.Add(ProposalAcceptance.Record(Id, appointmentTypeId, managerUserId, headcount));
            return true;
        }

        return existing.ChangeHeadcount(headcount);
    }

    /// <summary>
    /// Withdraws one listed type's acceptance. The proposer's own acceptance cannot be withdrawn:
    /// withdrawing the proposal is the way to take the whole offer back (FR-2.9).
    /// </summary>
    /// <param name="appointmentTypeId">The type withdrawing its acceptance.</param>
    public void WithdrawAcceptance(Guid appointmentTypeId)
    {
        EnsureOpen();
        EnsureListed(appointmentTypeId);
        Guard.Against(
            appointmentTypeId == ProposerAppointmentTypeId,
            "The proposing type's acceptance cannot be withdrawn; withdraw the proposal instead.");

        var acceptance = _acceptances.SingleOrDefault(a => a.AppointmentTypeId == appointmentTypeId);
        Guard.Against(acceptance is null, "This appointment type has not accepted the proposal.");

        _acceptances.Remove(acceptance!);
    }

    /// <summary>
    /// Withdraws the whole proposal. Only the proposing type may do this, whoever currently holds
    /// that type (FR-2.9, FR-2.10).
    /// </summary>
    /// <param name="actingAppointmentTypeId">The type the caller currently holds.</param>
    public void Withdraw(Guid actingAppointmentTypeId)
    {
        EnsureOpen();
        Guard.Against(
            actingAppointmentTypeId != ProposerAppointmentTypeId,
            "Only the proposing appointment type may withdraw the proposal.");

        Status = EventProposalStatus.Withdrawn;
    }

    /// <summary>
    /// Withdraws an open proposal whose window has started, as the sweep does (FR-2.12). Returns
    /// whether anything changed, so the sweep audits only real withdrawals.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool TryWithdrawStarted(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        if (Status != EventProposalStatus.Open || !Window.HasStarted(zones, timeZoneId, now))
        {
            return false;
        }

        Status = EventProposalStatus.Withdrawn;
        return true;
    }

    /// <summary>Whether the given type has accepted.</summary>
    /// <param name="appointmentTypeId">The appointment type.</param>
    public bool IsAcceptedBy(Guid appointmentTypeId) =>
        _acceptances.Any(a => a.AppointmentTypeId == appointmentTypeId);

    internal void MarkConfirmed()
    {
        Guard.Against(
            !IsFullyAccepted,
            "A proposal is confirmed only once every listed appointment type has accepted it.");
        Status = EventProposalStatus.Confirmed;
    }

    private static List<string> Validate(
        bool locationIsActive,
        string timeZoneId,
        EventWindow window,
        IEventWindowZones zones,
        DateTimeOffset now,
        IReadOnlyList<ProposableAppointmentType> listedTypes,
        Guid proposerAppointmentTypeId,
        int headcount)
    {
        var failures = new List<string>();

        if (!locationIsActive)
        {
            failures.Add("location-inactive");
        }

        var zoneProblem = window.ProblemIn(zones, timeZoneId);
        if (zoneProblem == EventWindowZoneProblem.UnknownZone)
        {
            failures.Add("unknown-zone");
        }
        else if (zoneProblem != EventWindowZoneProblem.None)
        {
            failures.Add("window-has-no-unique-instant");
        }
        else if (window.StartInstant(zones, timeZoneId) <= now)
        {
            failures.Add("window-not-in-future");
        }

        if (listedTypes.Count == 0)
        {
            failures.Add("types-empty");
        }

        if (listedTypes.Count > MaximumListedTypes)
        {
            failures.Add("types-too-many");
        }

        if (listedTypes.Select(type => type.Id).Distinct().Count() != listedTypes.Count)
        {
            failures.Add("types-duplicated");
        }

        AddNamed(failures, "type-inactive", listedTypes.Where(type => !type.IsActive));
        AddNamed(failures, "type-without-manager", listedTypes.Where(type => type.IsActive && !type.HasCurrentManager));

        if (listedTypes.All(type => type.Id != proposerAppointmentTypeId))
        {
            failures.Add("proposer-type-not-listed");
        }

        if (headcount is < 1 or > MaximumHeadcount)
        {
            failures.Add("headcount-out-of-range");
        }

        return failures;
    }

    private static void AddNamed(
        List<string> failures, string code, IEnumerable<ProposableAppointmentType> offending)
    {
        var codes = offending.Select(type => type.Code).Distinct().Order().ToList();
        if (codes.Count > 0)
        {
            failures.Add($"{code}: {string.Join(", ", codes)}");
        }
    }

    private void EnsureOpen()
    {
        if (Status != EventProposalStatus.Open)
        {
            throw new ProposalNotOpenException(Status);
        }
    }

    private void EnsureListed(Guid appointmentTypeId) =>
        Guard.Against(
            !ListedAppointmentTypeIds.Contains(appointmentTypeId),
            "This appointment type is not listed on the proposal.");
}
