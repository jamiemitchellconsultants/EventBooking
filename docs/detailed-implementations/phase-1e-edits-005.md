# 01e — Location-restricted invites and closed attendee transitions, edits 5 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Domain/Invites/Invite.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Domain/Invites/Invite.cs","beforeSha":"3df352dcc48b474087847be09920cc882eaba4c5dfbbddaee177c8cc9b1c9188","afterSha":"df7de7955fd7b13cca52d38b28fec3b8b146879c95190fc5a33a23cb7820b042","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Invites;

/// <summary>An offer of event options carrying an immutable requirement snapshot.</summary>
public sealed class Invite
{
    /// <summary>Gets the number of event options every invite offers.</summary>
    public const int RequiredOptionCount = 3;

    private readonly List<InviteOption> _options = [];
    private readonly List<InviteRequirement> _requirements = [];

    private Invite()
    {
        // Required by the persistence layer's constructor binding.
        TokenHash = string.Empty;
    }

    /// <summary>Gets the invite identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the invited attendee identifier.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Gets the original Booking recovered by this Invite, or null for an initial Invite.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>The hash of the single-use token. The token itself is never stored.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Gets when the invite stops being usable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the invite lifecycle state.</summary>
    public InviteStatus Status { get; private set; } = InviteStatus.Pending;

    /// <summary>Gets how many retries preceded this invite.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Gets the offered event options.</summary>
    public IReadOnlyList<InviteOption> Options => _options;

    /// <summary>Gets the offered event identifiers.</summary>
    public IReadOnlyList<Guid> OfferedEventIds => _options.Select(o => o.EventId).ToList();

    /// <summary>Gets the immutable requirement snapshot used by every downstream operation.</summary>
    public IReadOnlyList<InviteRequirement> Requirements => _requirements;

    /// <summary>Gets the snapshotted Appointment Type identifiers in stable order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).Order().ToList();

    /// <summary>Replaces the persisted hash after issuing a fresh in-memory invite token.</summary>
    /// <param name="tokenHash">The token hash.</param>
    public void RotateTokenHash(string? tokenHash)
    {
        EnsurePending("Only a pending invite token can be rotated.");
        TokenHash = Guard.NotBlank(tokenHash, "tokenHash");
    }

    /// <summary>Creates an initial invite snapshotting every current derived requirement.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="retryCount">The retry count.</param>
    public static Invite CreateInitial(
        Guid id,
        Guid attendeeId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount) =>
        Create(id, attendeeId, null, tokenHash, expiresAt, eventIds, appointmentTypeIds, retryCount);

    /// <summary>Creates a recovery invite snapshotting only recoverable no-show types.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="recoveryOfBookingId">The recovery of booking id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static Invite CreateRecovery(
        Guid id,
        Guid attendeeId,
        Guid recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(recoveryOfBookingId == Guid.Empty, "recoveryOfBookingId must not be empty.");
        return Create(
            id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt,
            eventIds, appointmentTypeIds, 0);
    }

    /// <summary>Determines whether the invite can still be used at the supplied instant.</summary>
    /// <param name="now">The now.</param>
    public bool IsUsableAt(DateTimeOffset now) =>
        Status == InviteStatus.Pending && now < ExpiresAt;

    /// <summary>Determines whether the invite offers the supplied eventItem.</summary>
    /// <param name="eventId">The event id.</param>
    public bool Offers(Guid eventId) =>
        _options.Any(o => o.EventId == eventId);

    /// <summary>Removes one offered event from a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void RemoveOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");

        var option = _options.SingleOrDefault(o => o.EventId == eventId);
        Guard.Against(option is null, "This invite does not offer that eventItem.");

        _options.Remove(option!);
    }

    /// <summary>Adds one offered event to a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void AddOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");
        Guard.Against(
            _options.Count >= RequiredOptionCount,
            $"An invite cannot offer more than {RequiredOptionCount} event options.");
        Guard.Against(Offers(eventId), "An invite cannot offer the same event twice.");

        _options.Add(InviteOption.For(Id, eventId));
    }

    /// <summary>Moves a pending invite to used.</summary>
    public void MarkUsed() => TransitionFromPendingTo(InviteStatus.Used);

    /// <summary>Moves a pending invite to expired.</summary>
    public void MarkExpired() => TransitionFromPendingTo(InviteStatus.Expired);

    /// <summary>Moves a pending invite to superseded.</summary>
    public void MarkSuperseded() => TransitionFromPendingTo(InviteStatus.Superseded);

    /// <summary>Cancels a pending recovery Invite without changing capacity.</summary>
    public void CancelRecovery()
    {
        Guard.Against(RecoveryOfBookingId is null, "Only a recovery invite can be cancelled.");
        EnsurePending("Only a pending recovery invite can be cancelled.");
        Status = InviteStatus.Cancelled;
    }

    private static Invite Create(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount)
    {
        var invite = CreateCore(id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt, eventIds, retryCount);

        var snapshot = appointmentTypeIds.ToList();
        Guard.Against(snapshot.Count == 0, "An invite must snapshot at least one appointment type.");
        Guard.Against(snapshot.Count > 3, "An invite cannot snapshot more than three appointment types.");
        Guard.Against(
            snapshot.Distinct().Count() != snapshot.Count,
            "An invite cannot snapshot the same appointment type twice.");

        foreach (var appointmentTypeId in snapshot)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        foreach (var appointmentTypeId in snapshot.Order())
        {
            invite._requirements.Add(InviteRequirement.For(id, appointmentTypeId));
        }

        return invite;
    }

    private static Invite CreateCore(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds,
        int retryCount)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        var offeredEventIds = eventIds.ToList();
        Guard.Against(
            offeredEventIds.Count != RequiredOptionCount,
            $"An invite must offer exactly {RequiredOptionCount} event options.");
        Guard.Against(
            offeredEventIds.Distinct().Count() != offeredEventIds.Count,
            "An invite cannot offer the same event twice.");

        var invite = new Invite
        {
            Id = id,
            AttendeeId = attendeeId,
            RecoveryOfBookingId = recoveryOfBookingId,
            TokenHash = Guard.NotBlank(tokenHash, "tokenHash"),
            ExpiresAt = expiresAt,
            Status = InviteStatus.Pending,
            RetryCount = Guard.NotNegative(retryCount, "retryCount"),
        };

        foreach (var eventId in eventIds)
        {
            invite._options.Add(InviteOption.For(id, eventId));
        }

        return invite;
    }

    private void TransitionFromPendingTo(InviteStatus target)
    {
        EnsurePending($"An invite that is already {Status} cannot become {target}.");
        Status = target;
    }

    private void EnsurePending(string message) =>
        Guard.Against(Status != InviteStatus.Pending, message);
}
`````

## after — src/EventBooking.Domain/Invites/Invite.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Domain/Invites/Invite.cs","beforeSha":"3df352dcc48b474087847be09920cc882eaba4c5dfbbddaee177c8cc9b1c9188","afterSha":"df7de7955fd7b13cca52d38b28fec3b8b146879c95190fc5a33a23cb7820b042","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Invites;

/// <summary>An offer of event options carrying an immutable requirement snapshot.</summary>
public sealed class Invite
{
    /// <summary>Gets the number of event options every invite offers.</summary>
    public const int RequiredOptionCount = 3;

    /// <summary>The fewest locations an invite may be restricted to (design 08).</summary>
    public const int MinimumLocationCount = 1;

    /// <summary>The most locations an invite may be restricted to (design 08).</summary>
    public const int MaximumLocationCount = 50;

    private readonly List<InviteLocation> _locations = [];
    private readonly List<InviteOption> _options = [];
    private readonly List<InviteRequirement> _requirements = [];

    private Invite()
    {
        // Required by the persistence layer's constructor binding.
        TokenHash = string.Empty;
    }

    /// <summary>Gets the invite identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the invited attendee identifier.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Gets the original Booking recovered by this Invite, or null for an initial Invite.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>The hash of the single-use token. The token itself is never stored.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Gets when the invite stops being usable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the invite lifecycle state.</summary>
    public InviteStatus Status { get; private set; } = InviteStatus.Pending;

    /// <summary>Gets how many retries preceded this invite.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Gets the locations every offer on this invite is drawn from.</summary>
    public IReadOnlyList<InviteLocation> Locations => _locations;

    /// <summary>Gets the selected location identifiers in stable order.</summary>
    public IReadOnlyList<Guid> LocationIds =>
        _locations.Select(l => l.LocationId).ToList();

    /// <summary>Gets the offered event options.</summary>
    public IReadOnlyList<InviteOption> Options => _options;

    /// <summary>Gets the offered event identifiers.</summary>
    public IReadOnlyList<Guid> OfferedEventIds => _options.Select(o => o.EventId).ToList();

    /// <summary>Gets the immutable requirement snapshot used by every downstream operation.</summary>
    public IReadOnlyList<InviteRequirement> Requirements => _requirements;

    /// <summary>Gets the snapshotted Appointment Type identifiers in stable order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).Order().ToList();

    /// <summary>Replaces the persisted hash after issuing a fresh in-memory invite token.</summary>
    /// <param name="tokenHash">The token hash.</param>
    public void RotateTokenHash(string? tokenHash)
    {
        EnsurePending("Only a pending invite token can be rotated.");
        TokenHash = Guard.NotBlank(tokenHash, "tokenHash");
    }

    /// <summary>Creates an initial invite snapshotting every current derived requirement.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="locationIds">The locations the Coordinator selected; 1 to 50, no duplicates.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="retryCount">The retry count.</param>
    public static Invite CreateInitial(
        Guid id,
        Guid attendeeId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount) =>
        Create(
            id, attendeeId, null, tokenHash, expiresAt,
            DistinctLocations(locationIds), eventIds, appointmentTypeIds, retryCount);

    /// <summary>
    /// Issues the next invite of the same journey, on the same locations. Expiry re-issue, top-up
    /// and event cancellation all go through here, so none of them can quietly widen the set the
    /// Coordinator chose.
    /// </summary>
    /// <param name="id">The new invite's id.</param>
    /// <param name="originating">The invite being replaced.</param>
    /// <param name="tokenHash">The new token hash.</param>
    /// <param name="expiresAt">When the new invite stops being usable.</param>
    /// <param name="eventIds">The freshly chosen event options.</param>
    public static Invite Reissue(
        Guid id,
        Invite originating,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds)
    {
        ArgumentNullException.ThrowIfNull(originating);

        return Create(
            id,
            originating.AttendeeId,
            originating.RecoveryOfBookingId,
            tokenHash,
            expiresAt,
            originating.LocationIds,
            eventIds,
            originating.RequiredAppointmentTypeIds,
            originating.RetryCount + 1);
    }

    /// <summary>Creates a recovery invite snapshotting only recoverable no-show types.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="recoveryOfBookingId">The recovery of booking id.</param>
    /// <param name="tokenHash">The token hash.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="originalLocationId">The location of the booking being recovered.</param>
    /// <param name="additionalLocationIds">Further locations the Coordinator opened up, or null.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static Invite CreateRecovery(
        Guid id,
        Guid attendeeId,
        Guid recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        Guid originalLocationId,
        IEnumerable<Guid>? additionalLocationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(recoveryOfBookingId == Guid.Empty, "recoveryOfBookingId must not be empty.");
        Guard.Against(originalLocationId == Guid.Empty, "originalLocationId must not be empty.");

        // The attendee already travelled to the original booking's location, so it is always
        // offered. Anything the Coordinator adds joins it; a repeat of it is not an error.
        var locations = new List<Guid> { originalLocationId };
        foreach (var locationId in additionalLocationIds ?? [])
        {
            if (!locations.Contains(locationId))
            {
                locations.Add(locationId);
            }
        }

        return Create(
            id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt,
            Bounded(locations), eventIds, appointmentTypeIds, 0);
    }

    /// <summary>Determines whether the invite can still be used at the supplied instant.</summary>
    /// <param name="now">The now.</param>
    public bool IsUsableAt(DateTimeOffset now) =>
        Status == InviteStatus.Pending && now < ExpiresAt;

    /// <summary>Determines whether the invite offers the supplied eventItem.</summary>
    /// <param name="eventId">The event id.</param>
    public bool Offers(Guid eventId) =>
        _options.Any(o => o.EventId == eventId);

    /// <summary>Removes one offered event from a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void RemoveOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");

        var option = _options.SingleOrDefault(o => o.EventId == eventId);
        Guard.Against(option is null, "This invite does not offer that eventItem.");

        _options.Remove(option!);
    }

    /// <summary>Adds one offered event to a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void AddOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");
        Guard.Against(
            _options.Count >= RequiredOptionCount,
            $"An invite cannot offer more than {RequiredOptionCount} event options.");
        Guard.Against(Offers(eventId), "An invite cannot offer the same event twice.");

        _options.Add(InviteOption.For(Id, eventId));
    }

    /// <summary>Moves a pending invite to used.</summary>
    public void MarkUsed() => TransitionFromPendingTo(InviteStatus.Used);

    /// <summary>Moves a pending invite to expired.</summary>
    public void MarkExpired() => TransitionFromPendingTo(InviteStatus.Expired);

    /// <summary>Moves a pending invite to superseded.</summary>
    public void MarkSuperseded() => TransitionFromPendingTo(InviteStatus.Superseded);

    /// <summary>Cancels a pending recovery Invite without changing capacity.</summary>
    public void CancelRecovery()
    {
        Guard.Against(RecoveryOfBookingId is null, "Only a recovery invite can be cancelled.");
        EnsurePending("Only a pending recovery invite can be cancelled.");
        Status = InviteStatus.Cancelled;
    }

    private static Invite Create(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IReadOnlyList<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount)
    {
        var invite = CreateCore(
            id, attendeeId, recoveryOfBookingId, tokenHash, expiresAt, locationIds, eventIds, retryCount);

        var snapshot = appointmentTypeIds.ToList();
        Guard.Against(snapshot.Count == 0, "An invite must snapshot at least one appointment type.");
        Guard.Against(
            snapshot.Distinct().Count() != snapshot.Count,
            "An invite cannot snapshot the same appointment type twice.");

        foreach (var appointmentTypeId in snapshot)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        foreach (var appointmentTypeId in snapshot.Order())
        {
            invite._requirements.Add(InviteRequirement.For(id, appointmentTypeId));
        }

        return invite;
    }

    private static Invite CreateCore(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        string? tokenHash,
        DateTimeOffset expiresAt,
        IReadOnlyList<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        int retryCount)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        var offeredEventIds = eventIds.ToList();
        Guard.Against(
            offeredEventIds.Count != RequiredOptionCount,
            $"An invite must offer exactly {RequiredOptionCount} event options.");
        Guard.Against(
            offeredEventIds.Distinct().Count() != offeredEventIds.Count,
            "An invite cannot offer the same event twice.");

        var invite = new Invite
        {
            Id = id,
            AttendeeId = attendeeId,
            RecoveryOfBookingId = recoveryOfBookingId,
            TokenHash = Guard.NotBlank(tokenHash, "tokenHash"),
            ExpiresAt = expiresAt,
            Status = InviteStatus.Pending,
            RetryCount = Guard.NotNegative(retryCount, "retryCount"),
        };

        foreach (var locationId in locationIds)
        {
            invite._locations.Add(InviteLocation.For(id, locationId));
        }

        foreach (var eventId in eventIds)
        {
            invite._options.Add(InviteOption.For(id, eventId));
        }

        return invite;
    }

    private static IReadOnlyList<Guid> DistinctLocations(IEnumerable<Guid> locationIds)
    {
        ArgumentNullException.ThrowIfNull(locationIds);

        var chosen = locationIds.ToList();
        Guard.Against(
            chosen.Distinct().Count() != chosen.Count,
            "An invite cannot be restricted to the same location twice.");

        return Bounded(chosen);
    }

    private static IReadOnlyList<Guid> Bounded(IReadOnlyList<Guid> locationIds)
    {
        Guard.Against(
            locationIds.Count < MinimumLocationCount,
            "An invite must be restricted to at least one location.");
        Guard.Against(
            locationIds.Count > MaximumLocationCount,
            $"An invite cannot be restricted to more than {MaximumLocationCount} locations.");
        Guard.Against(
            locationIds.Any(locationId => locationId == Guid.Empty),
            "A location id must not be empty.");

        return locationIds;
    }

    private void TransitionFromPendingTo(InviteStatus target)
    {
        EnsurePending($"An invite that is already {Status} cannot become {target}.");
        Status = target;
    }

    private void EnsurePending(string message) =>
        Guard.Against(Status != InviteStatus.Pending, message);
}
`````

## after — src/EventBooking.Domain/Invites/InviteLocation.cs — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Domain/Invites/InviteLocation.cs","beforeSha":null,"afterSha":"b26d561cb018d32e2e59377bb1a13b57fd3b58b86c168e818aa229b020a2482e","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Invites;

/// <summary>
/// One Location a Coordinator selected when issuing an Invite. Options, replacements and automatic
/// re-issues are drawn only from events at these locations.
/// </summary>
public sealed class InviteLocation
{
    private InviteLocation()
    {
    }

    /// <summary>Gets the owning Invite identifier.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Gets the selected Location identifier.</summary>
    public Guid LocationId { get; private set; }

    internal static InviteLocation For(Guid inviteId, Guid locationId) =>
        new() { InviteId = inviteId, LocationId = locationId };
}
`````

## before — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":12,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"892820087fc053b3ebaeb0b582958d3493bb07adb54366dc61e036b12decc092","afterSha":"48b575f386db9da4a8c9ec2e210a84627c12a27abf9e620a3ea7921ba20a8583","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Time;
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
        ClockOptions clock,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(clock);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEventWindowZones, NodaTimeEventWindowZones>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## after — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":12,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"892820087fc053b3ebaeb0b582958d3493bb07adb54366dc61e036b12decc092","afterSha":"48b575f386db9da4a8c9ec2e210a84627c12a27abf9e620a3ea7921ba20a8583","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Time;
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
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString));
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
        ClockOptions clock,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(clock);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEventWindowZones, NodaTimeEventWindowZones>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs","beforeSha":"cfa95b1200e6684e654cd26daa4be9ad368aef4ee0d565819d3c78d87bdf23c8","afterSha":"88b58e313b119aae524b28f747d1bd7dddc8483e2b2d10926bf7f7305eb0b02e","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs","beforeSha":"cfa95b1200e6684e654cd26daa4be9ad368aef4ee0d565819d3c78d87bdf23c8","afterSha":"88b58e313b119aae524b28f747d1bd7dddc8483e2b2d10926bf7f7305eb0b02e","side":"after","part":1,"parts":1} -->

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
        builder.Property(c => c.StatusChangedAt).HasColumnName("status_changed_at");

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

## before — src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs — 1/1

<!-- retirement-file: {"id":14,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs","beforeSha":"5953576f7ed4becc505fed3a42765d1d410fe4a26b1258c7ec3f3d30c94bede0","afterSha":"fa8b31aaf929481062b3a383f3d4e17ffd129f422a9113478d9132d999aa1d70","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps invite persistence and its one-pending-invite-per-attendee backstop.</summary>
public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    /// <summary>Configures invite columns, options, and the filtered pending-invite index.</summary>
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invite");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.AttendeeId).HasColumnName("attendee_id");
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(i => i.RetryCount).HasColumnName("retry_count");
        builder.Property(i => i.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");

        builder.Ignore(i => i.OfferedEventIds);
        builder.Ignore(i => i.RequiredAppointmentTypeIds);

        builder
            .HasMany(i => i.Options)
            .WithOne()
            .HasForeignKey(o => o.InviteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(i => i.Requirements)
            .WithOne()
            .HasForeignKey(r => r.InviteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(i => i.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(i => i.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.RecoveryOfBookingId);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.Status, i.ExpiresAt });
        builder.HasIndex(i => i.AttendeeId);
        builder.HasIndex(i => i.AttendeeId)
            .HasDatabaseName("ux_invite_pending_attendee")
            .HasFilter("status = 1")
            .IsUnique();
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs — 1/1

<!-- retirement-file: {"id":14,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs","beforeSha":"5953576f7ed4becc505fed3a42765d1d410fe4a26b1258c7ec3f3d30c94bede0","afterSha":"fa8b31aaf929481062b3a383f3d4e17ffd129f422a9113478d9132d999aa1d70","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps invite persistence and its one-pending-invite-per-attendee backstop.</summary>
public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    /// <summary>Configures invite columns, options, and the filtered pending-invite index.</summary>
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invite");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.AttendeeId).HasColumnName("attendee_id");
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(i => i.RetryCount).HasColumnName("retry_count");
        builder.Property(i => i.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");

        builder.Ignore(i => i.OfferedEventIds);
        builder.Ignore(i => i.RequiredAppointmentTypeIds);
        builder.Ignore(i => i.LocationIds);

        builder
            .HasMany(i => i.Locations)
            .WithOne()
            .HasForeignKey(l => l.InviteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Locations).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(i => i.Options)
            .WithOne()
            .HasForeignKey(o => o.InviteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(i => i.Requirements)
            .WithOne()
            .HasForeignKey(r => r.InviteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(i => i.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(i => i.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.RecoveryOfBookingId);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.Status, i.ExpiresAt });
        builder.HasIndex(i => i.AttendeeId);
        builder.HasIndex(i => i.AttendeeId)
            .HasDatabaseName("ux_invite_pending_attendee")
            .HasFilter("status = 1")
            .IsUnique();
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/InviteLocationConfiguration.cs — 1/1

<!-- retirement-file: {"id":15,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteLocationConfiguration.cs","beforeSha":null,"afterSha":"1e96e23ce4a42c9f0213db6919b1568c0f0418974891fa24c333a2c4c8147e5f","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Location an Invite's options may be drawn from.</summary>
public sealed class InviteLocationConfiguration : IEntityTypeConfiguration<InviteLocation>
{
    /// <summary>
    /// Configures the composite key. There is deliberately no foreign key to the location table:
    /// the Location aggregate Task 5 introduced is not persisted until Task 9's fresh schema, and
    /// mapping it from here would have this migration create a second, parallel table for it.
    /// </summary>
    public void Configure(EntityTypeBuilder<InviteLocation> builder)
    {
        builder.ToTable("invite_location");
        builder.HasKey(location => new { location.InviteId, location.LocationId });

        builder.Property(location => location.InviteId).HasColumnName("invite_id");
        builder.Property(location => location.LocationId).HasColumnName("location_id");

        builder.HasIndex(location => location.LocationId);
    }
}
`````
