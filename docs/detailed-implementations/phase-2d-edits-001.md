# 02d — The invite eligibility query, and the start instant it orders on, edits 1 (Task 11)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Application/Abstractions/IEventEligibilityQuery.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Abstractions/IEventEligibilityQuery.cs","beforeSha":null,"afterSha":"fa406fcc8f162bee6ba467f49a8a9503686e7562326ae7664f7a5cc5c7a379f5","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Abstractions;

/// <summary>
/// Which events a attendee may be offered. Relational division: an event qualifies only if it
/// covers every required appointment type with at least one place left, and an event that does not
/// list a required type at all is never a candidate however much room its other types have.
///
/// The rule is executed in the database, in one statement, because the alternative is loading
/// every active event and its capacity rows into memory to filter them there (design 04 — invite
/// selection). The query only proposes candidates: capacity is re-checked under lock at booking
/// time, so a stale option can never overbook.
/// </summary>
public interface IEventEligibilityQuery
{
    /// <summary>
    /// The eligible events, earliest first by start instant and then by identifier, at most
    /// <paramref name="count"/> of them.
    /// </summary>
    /// <param name="requiredAppointmentTypeIds">Every type the attendee needs; duplicates collapse.</param>
    /// <param name="locationIds">The locations the caller will offer; an event elsewhere is not a candidate.</param>
    /// <param name="excludeEventIds">Events the caller has already offered or ruled out.</param>
    /// <param name="count">The most identifiers to return.</param>
    /// <param name="asOf">The instant to judge "still to come" against.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        int count,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many events the same filters match, with no limit. The invite dialog shows the number
    /// before it shows the options, and counting in the database avoids fetching rows to discard.
    /// </summary>
    /// <param name="requiredAppointmentTypeIds">Every type the attendee needs; duplicates collapse.</param>
    /// <param name="locationIds">The locations the caller will offer.</param>
    /// <param name="excludeEventIds">Events the caller has already offered or ruled out.</param>
    /// <param name="asOf">The instant to judge "still to come" against.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<int> CountEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Abstractions/IEventRepository.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Abstractions/IEventRepository.cs","beforeSha":"4e85b0c9f66d351f57887e816d095bb8b718159bfc1d595ebd8274b19573ef3a","afterSha":"8cad2ad47674eb721834e4ed7e8e1ee973c2a0b268dedf39ef8cf258f58d6898","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent repository for the current use case.</summary>
public interface IEventRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the transactional write guard for a event and returns its current state.
    /// Confirmation and every cancellation path that can change bookings on the event must take
    /// this guard before reading booking or capacity state for that eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active events whose window falls on or after the given date, capacities loaded.</summary>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListActiveAsync(DateOnly onOrAfter, CancellationToken cancellationToken);

    /// <summary>Every eventItem, cancelled ones included — for the coordinator's events overview.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="eventItem">The eventItem.</param>
    void Add(Event eventItem);
}
`````

## after — src/EventBooking.Application/Abstractions/IEventRepository.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Abstractions/IEventRepository.cs","beforeSha":"4e85b0c9f66d351f57887e816d095bb8b718159bfc1d595ebd8274b19573ef3a","afterSha":"8cad2ad47674eb721834e4ed7e8e1ee973c2a0b268dedf39ef8cf258f58d6898","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent repository for the current use case.</summary>
public interface IEventRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the transactional write guard for a event and returns its current state.
    /// Confirmation and every cancellation path that can change bookings on the event must take
    /// this guard before reading booking or capacity state for that eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active events whose window falls on or after the given date, capacities loaded.</summary>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListActiveAsync(DateOnly onOrAfter, CancellationToken cancellationToken);

    /// <summary>Every eventItem, cancelled ones included — for the coordinator's events overview.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>The events with these identifiers, capacities loaded, in no particular order.</summary>
    /// <param name="ids">The event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a eventItem, computing its derived start instant from the window and the location's
    /// zone so the row is written complete in the transaction that inserts it (design 04 — invite
    /// selection). Asynchronous because the zone is a property of the location row.
    /// </summary>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AddAsync(Event eventItem, CancellationToken cancellationToken);
}
`````

## before — src/EventBooking.Application/Events/AcceptProposalHandler.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Application/Events/AcceptProposalHandler.cs","beforeSha":"004ef2c00f51dc7bd48137df1f99b60454912684284fd11ad8ce4f88b2e5e41e","afterSha":"3afc79d6f1f9473d909283e2e7ce768a3ce969c07de3b47df3214ddf2fec1228","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines accept proposal command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Headcount">The headcount.</param>
public sealed record AcceptProposalCommand(Guid ManagerUserId, Guid ProposalId, int Headcount);

/// <summary>The event identifier is null unless this accept was the third.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="EventId">The event id.</param>
public sealed record AcceptProposalOutcome(Guid ProposalId, Guid? EventId);

/// <summary>Records an acceptance under the proposal row lock and confirms at most one eventItem.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AcceptProposalHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Records or revises the appointment-type acceptance while preserving confirmation atomicity.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AcceptProposalOutcome>> HandleAsync(
        AcceptProposalCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AcceptProposalOutcome>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.NotFound("No such proposal."));
        }

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;

        var previousHeadcount = proposal.Acceptances
            .SingleOrDefault(acceptance => acceptance.AppointmentTypeId == appointmentTypeId)
            ?.Headcount;

        bool changed;
        try
        {
            changed = proposal.Accept(
                appointmentTypeId,
                command.ManagerUserId,
                command.Headcount);
        }
        catch (ProposalNotOpenException ex)
        {
            // FR-2.11: nothing about the request is malformed; the proposal moved on.
            return Result<AcceptProposalOutcome>.Failure(
                Error.Conflict($"The proposal is {ex.CurrentStatus} and can no longer be changed."));
        }
        catch (DomainException ex)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<AcceptProposalOutcome>.Success(
                new AcceptProposalOutcome(proposal.Id, null));
        }

        var appointmentTypeName = AppointmentTypeIdsName(appointmentTypeId);
        var auditDetails = previousHeadcount is null
            ? $"{appointmentTypeName} headcount {command.Headcount}"
            : $"{appointmentTypeName} headcount {previousHeadcount} -> {command.Headcount}";

        audit.Record(
            AuditEntityTypes.EventProposal,
            proposal.Id,
            AuditAction.AcceptanceRecorded,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            auditDetails);

        Guid? eventId = null;

        if (proposal.IsFullyAccepted)
        {
            var createdEventId = Guid.NewGuid();

            Event eventItem;
            try
            {
                eventItem = Event.CreateFrom(createdEventId, proposal);
            }
            catch (DomainException ex)
            {
                return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
            }

            events.Add(eventItem);
            eventId = createdEventId;

            audit.Record(
                AuditEntityTypes.Event,
                createdEventId,
                AuditAction.EventConfirmed,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                eventItem.Window.ToString());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AcceptProposalOutcome>.Success(
            new AcceptProposalOutcome(proposal.Id, eventId));
    }

    private static string AppointmentTypeIdsName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````

## after — src/EventBooking.Application/Events/AcceptProposalHandler.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Application/Events/AcceptProposalHandler.cs","beforeSha":"004ef2c00f51dc7bd48137df1f99b60454912684284fd11ad8ce4f88b2e5e41e","afterSha":"3afc79d6f1f9473d909283e2e7ce768a3ce969c07de3b47df3214ddf2fec1228","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines accept proposal command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Headcount">The headcount.</param>
public sealed record AcceptProposalCommand(Guid ManagerUserId, Guid ProposalId, int Headcount);

/// <summary>The event identifier is null unless this accept was the third.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="EventId">The event id.</param>
public sealed record AcceptProposalOutcome(Guid ProposalId, Guid? EventId);

/// <summary>Records an acceptance under the proposal row lock and confirms at most one eventItem.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class AcceptProposalHandler(
    IEventProposalRepository proposals,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Records or revises the appointment-type acceptance while preserving confirmation atomicity.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AcceptProposalOutcome>> HandleAsync(
        AcceptProposalCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<AcceptProposalOutcome>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.NotFound("No such proposal."));
        }

        var appointmentTypeId = authorized.Value.AppointmentTypeId!.Value;

        var previousHeadcount = proposal.Acceptances
            .SingleOrDefault(acceptance => acceptance.AppointmentTypeId == appointmentTypeId)
            ?.Headcount;

        bool changed;
        try
        {
            changed = proposal.Accept(
                appointmentTypeId,
                command.ManagerUserId,
                command.Headcount);
        }
        catch (ProposalNotOpenException ex)
        {
            // FR-2.11: nothing about the request is malformed; the proposal moved on.
            return Result<AcceptProposalOutcome>.Failure(
                Error.Conflict($"The proposal is {ex.CurrentStatus} and can no longer be changed."));
        }
        catch (DomainException ex)
        {
            return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
        }

        if (!changed)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<AcceptProposalOutcome>.Success(
                new AcceptProposalOutcome(proposal.Id, null));
        }

        var appointmentTypeName = AppointmentTypeIdsName(appointmentTypeId);
        var auditDetails = previousHeadcount is null
            ? $"{appointmentTypeName} headcount {command.Headcount}"
            : $"{appointmentTypeName} headcount {previousHeadcount} -> {command.Headcount}";

        audit.Record(
            AuditEntityTypes.EventProposal,
            proposal.Id,
            AuditAction.AcceptanceRecorded,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            auditDetails);

        Guid? eventId = null;

        if (proposal.IsFullyAccepted)
        {
            var createdEventId = Guid.NewGuid();

            Event eventItem;
            try
            {
                eventItem = Event.CreateFrom(createdEventId, proposal);
            }
            catch (DomainException ex)
            {
                return Result<AcceptProposalOutcome>.Failure(Error.Validation(ex.Message));
            }

            await events.AddAsync(eventItem, cancellationToken);
            eventId = createdEventId;

            audit.Record(
                AuditEntityTypes.Event,
                createdEventId,
                AuditAction.EventConfirmed,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                eventItem.Window.ToString());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<AcceptProposalOutcome>.Success(
            new AcceptProposalOutcome(proposal.Id, eventId));
    }

    private static string AppointmentTypeIdsName(Guid appointmentTypeId) =>
        Domain.AppointmentTypes.AppointmentTypeIds.NameOf(appointmentTypeId);
}
`````

## before — src/EventBooking.Application/Invites/EligibleEventFinder.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Invites/EligibleEventFinder.cs","beforeSha":"02b647682c72ec1b80e809ee4b3c225c552657d7e7e32b4ce8ebba54dec204ba","afterSha":"99fce022f008ff3ad1083447244b5a1195c648e3b0792a17ac3ebf06cbff4e62","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which events may a attendee be offered" rule lives. Task 36 uses it to build
/// an invite; Task 40 uses it to find a single replacement when an option fills up.
/// </summary>
/// <param name="events">The events.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleEventFinder(IEventRepository events, IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Event>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtTransitionalLocation;

        var attendees = await events.ListActiveAsync(today, cancellationToken);

        return attendees
            .Where(s => !excludeEventIds.Contains(s.Id))
            .Where(s => s.Window.StartsAfter(today))
            .Where(s => s.HasSpareCapacityForAll(requiredAppointmentTypeIds))
            .OrderBy(s => s.Window)
            .Take(take)
            .ToList();
    }
}
`````

## after — src/EventBooking.Application/Invites/EligibleEventFinder.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Invites/EligibleEventFinder.cs","beforeSha":"02b647682c72ec1b80e809ee4b3c225c552657d7e7e32b4ce8ebba54dec204ba","afterSha":"99fce022f008ff3ad1083447244b5a1195c648e3b0792a17ac3ebf06cbff4e62","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Events;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which events may a attendee be offered" rule is reached from. The rule
/// itself lives in the database from Task 11 onward: this asks the eligibility port for ordered
/// identifiers and hydrates them, so an invite and a single replacement option are chosen by the
/// same statement.
/// </summary>
/// <param name="eligibility">The eligibility query.</param>
/// <param name="events">The events.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleEventFinder(
    IEventEligibilityQuery eligibility,
    IEventRepository events,
    IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<Event>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken)
    {
        var ids = await eligibility.FindEligibleEventsAsync(
            requiredAppointmentTypeIds,
            Locations,
            excludeEventIds,
            take,
            clock.UtcNow,
            cancellationToken);

        if (ids.Count == 0)
        {
            return [];
        }

        var loaded = (await events.ListByIdsAsync(ids, cancellationToken))
            .ToDictionary(eventItem => eventItem.Id);

        // The query decided the order; hydrating must not quietly re-impose another one.
        return [.. ids.Where(loaded.ContainsKey).Select(id => loaded[id])];
    }

    /// <summary>How many events the attendee could be offered, ignoring any option limit.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="excludeEventIds">The exclude event ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<int> CountAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        CancellationToken cancellationToken) =>
        eligibility.CountEligibleEventsAsync(
            requiredAppointmentTypeIds,
            Locations,
            excludeEventIds,
            clock.UtcNow,
            cancellationToken);

    // Invites are restricted to the transitional location until Task 14, whose InviteAttendee
    // command carries the Coordinator's own selection of locations.
    private static IReadOnlyCollection<Guid> Locations => [TransitionalLocation.Id];
}
`````

## before — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"6d4e02d900543d233dd7bd2d7d51d28a1d0acf650c1d7ee87f4413288f8cbfcd","afterSha":"c63524783b23281e9cc5608a1b80b96beb59561927b2c7e61a720b0dc1ebe516","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Locking;
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

        // Scoped: one tracker per DbContext, because the order is a property of one connection's
        // transaction, not of the process.
        services.AddScoped<TransactionLocks>();
        services.AddScoped<RowLocks>();
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

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"6d4e02d900543d233dd7bd2d7d51d28a1d0acf650c1d7ee87f4413288f8cbfcd","afterSha":"c63524783b23281e9cc5608a1b80b96beb59561927b2c7e61a720b0dc1ebe516","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Locking;
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

        // Scoped: one tracker per DbContext, because the order is a property of one connection's
        // transaction, not of the process.
        services.AddScoped<TransactionLocks>();
        services.AddScoped<RowLocks>();
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
        services.AddScoped<IEventEligibilityQuery, EventEligibilityQuery>();

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

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"9b5ff97642ca9c5d32d53e48f394b7d43eb5227478fd7ed021206f0b39f7fb4c","afterSha":"b1fc66f638bfa9dce83d78404b5bd9155e0c2593704dc3d8054389a2bef60922","side":"before","part":1,"parts":1} -->

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

        // A derived persistence column, not domain data (design 04). It exists to index and order
        // the eligibility query, and the domain never reads it as the source of truth; PostgreSQL
        // cannot evaluate IANA rules in a generated column, so the application computes it.
        //
        // Nullable until Task 11, which is where the repository writes it in the same transaction
        // as the insert and makes the column required. A non-nullable column here would take EF's
        // default of 0001-01-01 for every row nothing has computed yet, and the eligibility query
        // filters on start_utc: a wrong instant would quietly hide the event rather than fail.
        builder.Property<DateTimeOffset?>("StartUtc").HasColumnName("start_utc");

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
        builder
            .HasIndex(nameof(Event.Status), nameof(Event.LocationId), "StartUtc")
            .HasDatabaseName("ix_event_eligibility");
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"9b5ff97642ca9c5d32d53e48f394b7d43eb5227478fd7ed021206f0b39f7fb4c","afterSha":"b1fc66f638bfa9dce83d78404b5bd9155e0c2593704dc3d8054389a2bef60922","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    /// <summary>The index design 04 names for the eligibility query, by its database name.</summary>
    public const string EligibilityIndexName = "ix_event_eligibility";

    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.LocationId).HasColumnName("location_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        // A derived persistence column, not domain data (design 04). It exists to index and order
        // the eligibility query, and the domain never reads it as the source of truth; PostgreSQL
        // cannot evaluate IANA rules in a generated column, so the application computes it.
        //
        // The CLR type stays nullable although the column is not: an event that has not been
        // stamped yet has to be distinguishable from one stamped with a default, which is exactly
        // what the save-time backstop looks for. A missing value fails the insert instead of
        // storing 0001-01-01, which the eligibility query would read as an event in the past.
        builder.Property<DateTimeOffset?>(EventStartInstants.PropertyName)
            .HasColumnName("start_utc")
            .IsRequired();

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
        builder
            .HasIndex(nameof(Event.Status), nameof(Event.LocationId), EventStartInstants.PropertyName)
            .HasDatabaseName(EligibilityIndexName);
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","beforeSha":"3a842a313b009d10021ed506ddd977408355bfdd171539c43588cbb0d9a341c0","afterSha":"310b19c45ccfb019f2fea90e33574157827fd10aa8488125dfcef11afda1d6fb","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

public sealed class EventBookingDbContext(DbContextOptions<EventBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    public DbSet<EventProposal> EventProposals => Set<EventProposal>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventCapacity> EventCapacities => Set<EventCapacity>();

    /// <summary>Gets Attendee Group reference rows and their required Appointment Type mappings.</summary>
    public DbSet<Location> Locations => Set<Location>();

    public DbSet<AttendeeGroup> AttendeeGroups => Set<AttendeeGroup>();

    public DbSet<Attendee> Attendees => Set<Attendee>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Gets independently progressing required appointments for persisted bookings.</summary>
    public DbSet<BookingAppointment> BookingAppointments => Set<BookingAppointment>();

    public DbSet<StaffAccessProfile> StaffAccessProfiles => Set<StaffAccessProfile>();

    /// <summary>Gets identity-provider pairs learned from authenticated staff tokens.</summary>
    public DbSet<StaffIdentity> StaffIdentities => Set<StaffIdentity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventBookingDbContext).Assembly);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","beforeSha":"3a842a313b009d10021ed506ddd977408355bfdd171539c43588cbb0d9a341c0","afterSha":"310b19c45ccfb019f2fea90e33574157827fd10aa8488125dfcef11afda1d6fb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

public sealed class EventBookingDbContext(DbContextOptions<EventBookingDbContext> options)
    : DbContext(options)
{
    // The IANA rules are versioned data, not configuration, and the resolver is a pure function
    // over them, so the context holds one rather than taking it as a dependency every caller that
    // builds a context by hand would then have to supply.
    private static readonly IEventWindowZones Zones = new NodaTimeEventWindowZones();

    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    public DbSet<EventProposal> EventProposals => Set<EventProposal>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventCapacity> EventCapacities => Set<EventCapacity>();

    /// <summary>Gets Attendee Group reference rows and their required Appointment Type mappings.</summary>
    public DbSet<Location> Locations => Set<Location>();

    public DbSet<AttendeeGroup> AttendeeGroups => Set<AttendeeGroup>();

    public DbSet<Attendee> Attendees => Set<Attendee>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Gets independently progressing required appointments for persisted bookings.</summary>
    public DbSet<BookingAppointment> BookingAppointments => Set<BookingAppointment>();

    public DbSet<StaffAccessProfile> StaffAccessProfiles => Set<StaffAccessProfile>();

    /// <summary>Gets identity-provider pairs learned from authenticated staff tokens.</summary>
    public DbSet<StaffIdentity> StaffIdentities => Set<StaffIdentity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    /// <summary>
    /// Fills in the derived start instant for any event being inserted that has not had one
    /// computed, so the column the eligibility query filters and orders on cannot be left empty by
    /// a writer that has not heard of the rule.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Whether to accept the tracked changes on success.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        await EventStartInstants.StampPendingAsync(this, Zones, cancellationToken);

        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventBookingDbContext).Assembly);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/EventStartInstants.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/EventStartInstants.cs","beforeSha":null,"afterSha":"fc772d6d42c198888d1948587b5d631cdab76030bb1735181f65b503cd8de4f4","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Writes <c>start_utc</c>, the derived persistence column the eligibility query orders and
/// indexes on. It is not domain data: the window and the location's zone are the source of truth,
/// and this is the one function that turns them into an instant (design 04 — invite selection).
///
/// PostgreSQL cannot evaluate IANA rules in a generated column deterministically, so the value has
/// to be written by whoever inserts the row. The repository writes it as it adds the event, and
/// the context writes it for any other writer at save time, so the column cannot be left empty by
/// a path that has not heard of the rule.
/// </summary>
public static class EventStartInstants
{
    /// <summary>The shadow property that carries the column.</summary>
    public const string PropertyName = "StartUtc";

    /// <summary>Computes and stamps one tracked event's start instant.</summary>
    /// <param name="context">The context tracking the event.</param>
    /// <param name="eventItem">The event.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="zones">The zone abstraction.</param>
    public static void Stamp(
        DbContext context,
        Event eventItem,
        string timeZoneId,
        IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventItem);

        context.Entry(eventItem).Property<DateTimeOffset?>(PropertyName).CurrentValue =
            InstantOf(eventItem, timeZoneId, zones);
    }

    /// <summary>
    /// Fills in the start instant for every event being inserted that has not had one computed.
    /// The seeder and the suites add events straight through the context; this is what keeps the
    /// column's promise for them without each of them restating the rule.
    /// </summary>
    /// <param name="context">The context about to save.</param>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task StampPendingAsync(
        EventBookingDbContext context,
        IEventWindowZones zones,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var pending = context.ChangeTracker.Entries<Event>()
            .Where(entry => entry.State == EntityState.Added)
            .Where(entry => entry.Property<DateTimeOffset?>(PropertyName).CurrentValue is null)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var locationIds = pending.Select(entry => entry.Entity.LocationId).Distinct().ToArray();
        var zonesByLocation = await context.Locations
            .Where(location => locationIds.Contains(location.Id))
            .Select(location => new { location.Id, location.TimeZoneId })
            .ToDictionaryAsync(row => row.Id, row => row.TimeZoneId, cancellationToken);

        foreach (var entry in pending)
        {
            var locationId = entry.Entity.LocationId;
            zonesByLocation.TryGetValue(locationId, out var timeZoneId);

            entry.Property<DateTimeOffset?>(PropertyName).CurrentValue =
                InstantOf(entry.Entity, Required(timeZoneId, locationId), zones);
        }
    }

    /// <summary>
    /// The window's start, as an instant at UTC. The resolver answers with the location's own
    /// offset, and the column stores an instant with no offset of its own, so the value is
    /// normalised here rather than at each caller — PostgreSQL refuses any other offset outright.
    /// </summary>
    /// <param name="eventItem">The event.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="zones">The zone abstraction.</param>
    private static DateTimeOffset InstantOf(
        Event eventItem, string timeZoneId, IEventWindowZones zones) =>
        eventItem.Window.StartInstant(zones, timeZoneId).ToUniversalTime();

    /// <summary>The zone an event's location must have, refusing a location that is not persisted.</summary>
    /// <param name="timeZoneId">The zone read for that location, or null if there was no row.</param>
    /// <param name="locationId">The location the event is at.</param>
    public static string Required(string? timeZoneId, Guid locationId) =>
        timeZoneId
        ?? throw new InvalidOperationException(
            $"Location {locationId} has no persisted row, so the event's start instant cannot be "
            + "computed. Seed the location before the event.");
}
`````
