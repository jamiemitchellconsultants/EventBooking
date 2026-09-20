# 01c — Negotiation across any number of types, edits 2 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Domain/Events/EventProposal.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Domain/Events/EventProposal.cs","beforeSha":"42f672ffcf6626ea4644697aa738b01434f818e701891cf8d0ad1f1e591b141e","afterSha":"1bdb31907a4b28d4932f594c175ccc29ef6f3cd5818bb9572525b28fd6705815","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## after — src/EventBooking.Domain/Events/EventProposalAppointmentType.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Domain/Events/EventProposalAppointmentType.cs","beforeSha":null,"afterSha":"f45837e396ca6c1066a3465b57b6c4c5d41e91d5480672791d597a9eddadca33","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Events;

/// <summary>
/// One appointment type listed on a proposal. The list is fixed at creation: changing it after
/// other Managers have accepted would make their headcounts refer to a different event.
/// </summary>
public sealed class EventProposalAppointmentType
{
    private EventProposalAppointmentType()
    {
    }

    /// <summary>The proposal that lists the type.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>The listed appointment type.</summary>
    public Guid AppointmentTypeId { get; private set; }

    internal static EventProposalAppointmentType For(Guid proposalId, Guid appointmentTypeId) =>
        new() { ProposalId = proposalId, AppointmentTypeId = appointmentTypeId };
}
`````

## after — src/EventBooking.Domain/Events/ProposalNegotiation.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Domain/Events/ProposalNegotiation.cs","beforeSha":null,"afterSha":"c678b466befa6309f59539419735d7d52148b623f0d93aef553d58fb7a32f060","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// One appointment type as a proposer offers it, with the two facts the proposal has to check:
/// whether it is still active, and whether a Manager currently holds it. The code travels so a
/// refusal can name the type the way a screen does.
/// </summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical code, for refusal messages.</param>
/// <param name="IsActive">Whether the type may be listed on a new proposal.</param>
/// <param name="HasCurrentManager">Whether a Manager currently holds the type.</param>
public readonly record struct ProposableAppointmentType(
    Guid Id, string Code, bool IsActive, bool HasCurrentManager);

/// <summary>
/// Every reason a proposal was refused, not just the first. A Manager filling in a form should see
/// all of them at once (FR-2.2).
/// </summary>
public sealed class ProposalValidationException : DomainException
{
    /// <summary>Creates a refusal listing every failure.</summary>
    /// <param name="failures">The failure codes, each optionally naming the offending type codes.</param>
    public ProposalValidationException(IReadOnlyList<string> failures)
        : base("The proposal was refused: " + string.Join(", ", failures)) => Failures = failures;

    /// <summary>The failure codes, in a stable order.</summary>
    public IReadOnlyList<string> Failures { get; }
}

/// <summary>
/// An acceptance, revision or withdrawal reached a proposal that is no longer open. The current
/// status travels so the caller can report it instead of guessing (FR-2.11).
/// </summary>
public sealed class ProposalNotOpenException : DomainException
{
    /// <summary>Creates a refusal carrying the proposal's current status.</summary>
    /// <param name="currentStatus">The status the proposal actually has.</param>
    public ProposalNotOpenException(EventProposalStatus currentStatus)
        : base($"The proposal is {currentStatus} and can no longer be changed.") =>
        CurrentStatus = currentStatus;

    /// <summary>The status the proposal actually has.</summary>
    public EventProposalStatus CurrentStatus { get; }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"4da235933993c514b2d4258a2d594207d8acec084a573dc36d18ddba412606a2","afterSha":"71ae27e342883b3d08c50b2bccabba544c19dacc2164160f6c1e9c8363d3e611","side":"before","part":1,"parts":1} -->

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
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"4da235933993c514b2d4258a2d594207d8acec084a573dc36d18ddba412606a2","afterSha":"71ae27e342883b3d08c50b2bccabba544c19dacc2164160f6c1e9c8363d3e611","side":"after","part":1,"parts":1} -->

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
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.LocationId).HasColumnName("location_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalAppointmentTypeConfiguration.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalAppointmentTypeConfiguration.cs","beforeSha":null,"afterSha":"735f0605b5e3a50ae5602b0944d0eefad324297c066a5f196c4bb0d075f7ac39","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the fixed list of appointment types one proposal offers.</summary>
public sealed class EventProposalAppointmentTypeConfiguration
    : IEntityTypeConfiguration<EventProposalAppointmentType>
{
    /// <summary>Configures the listed-type rows and their composite key.</summary>
    public void Configure(EntityTypeBuilder<EventProposalAppointmentType> builder)
    {
        builder.ToTable("event_proposal_appointment_type");
        builder.HasKey(listed => new { listed.ProposalId, listed.AppointmentTypeId });

        builder.Property(listed => listed.ProposalId).HasColumnName("proposal_id");
        builder.Property(listed => listed.AppointmentTypeId).HasColumnName("appointment_type_id");
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs","beforeSha":"d538bc93030fc20009c56bebc73302b5851a077a807c170d39bb843483b8b0d5","afterSha":"ffbd90286b37ddb561d9636a11fe55f73d5f6afb680f5ced8b633479852f529e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps proposal persistence and its one-open-window uniqueness backstop.</summary>
public sealed class EventProposalConfiguration : IEntityTypeConfiguration<EventProposal>
{
    /// <summary>Configures proposal columns, acceptances, and the filtered window index.</summary>
    public void Configure(EntityTypeBuilder<EventProposal> builder)
    {
        builder.ToTable("event_proposal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(p => p.CreatedByManagerUserId).HasColumnName("created_by_manager_user_id");

        // The 4-hour window lives in this table's own date and start_time columns.
        builder.OwnsOne(p => p.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
            window.HasIndex(item => new { item.Date, item.StartTime })
                .HasDatabaseName("ux_event_proposal_open_window")
                .HasFilter("status = 1")
                .IsUnique();
        });
        builder.Navigation(p => p.Window).IsRequired();

        builder
            .HasMany(p => p.Acceptances)
            .WithOne()
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Acceptances).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.Status);

    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs","beforeSha":"d538bc93030fc20009c56bebc73302b5851a077a807c170d39bb843483b8b0d5","afterSha":"ffbd90286b37ddb561d9636a11fe55f73d5f6afb680f5ced8b633479852f529e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps proposal persistence and its one-open-window uniqueness backstop.</summary>
public sealed class EventProposalConfiguration : IEntityTypeConfiguration<EventProposal>
{
    /// <summary>Configures proposal columns, acceptances, and the filtered window index.</summary>
    public void Configure(EntityTypeBuilder<EventProposal> builder)
    {
        builder.ToTable("event_proposal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(p => p.CreatedByManagerUserId).HasColumnName("created_by_manager_user_id");
        builder.Property(p => p.LocationId).HasColumnName("location_id");
        // The proposing type, not the proposing person, decides who may withdraw the proposal.
        builder.Property(p => p.ProposerAppointmentTypeId).HasColumnName("proposer_appointment_type_id");

        // The 4-hour window lives in this table's own date and start_time columns.
        builder.OwnsOne(p => p.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
            window.HasIndex(item => new { item.Date, item.StartTime })
                .HasDatabaseName("ux_event_proposal_open_window")
                .HasFilter("status = 1")
                .IsUnique();
        });
        builder.Navigation(p => p.Window).IsRequired();

        builder
            .HasMany(p => p.Acceptances)
            .WithOne()
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Acceptances).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(p => p.ListedTypes)
            .WithOne()
            .HasForeignKey(listed => listed.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.ListedTypes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.Status);

    }
}
`````
