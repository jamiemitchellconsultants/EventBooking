# 00d — Retire direct event import, edits 3 (Task 3b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Application/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Application/DependencyInjection.cs","beforeSha":"956c5c0f41c77a77e31b33cbe5c91c2dd16625a97b9626569e320358048d2389","afterSha":"a2ac56baac0e4c164adfbfb8438f425d074d6725f83a63a17efe909774751f3e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    /// <param name="staffIdPolicy">The deployment staff-number validation policy.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        AttendeePortalOptions portal,
        StaffIdPolicy? staffIdPolicy = null)
    {
        services.AddSingleton(portal);
        services.AddSingleton(staffIdPolicy ?? new StaffIdPolicy());

        // Shared services.
        services.AddScoped<EligibleEventFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<InviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Event negotiation.
        services.AddScoped<ProposeEventHandler>();
        services.AddScoped<AcceptProposalHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<GetManagerEventBoardHandler>();
        services.AddScoped<CancelEventHandler>();
        services.AddScoped<AdjustEventCapacityHandler>();

        // Attendees.
        services.AddScoped<ImportAttendeesHandler>();
        services.AddScoped<SaveAttendeeHandler>();
        services.AddScoped<DeleteAttendeeHandler>();
        services.AddScoped<ListAttendeesHandler>();
        services.AddScoped<ListAttendeeGroupsHandler>();
        services.AddScoped<AttendeeReadinessCalculator>();
        services.AddScoped<GetAttendeeReadinessHandler>();
        services.AddScoped<GetAttendeeBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetEventOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();

        // Invites and bookings.
        services.AddScoped<TriggerInviteHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ExpireInvitesHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CancelAttendeeBookingHandler>();

        return services;
    }
}
`````

## before — src/EventBooking.Application/Events/EventImportParser.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Application/Events/EventImportParser.cs","beforeSha":"16dabdd84801aa973eea0cf51817b9ce52338b0b71c9e6c52dc5b5c4cb0e3b53","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines event import row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Window">The window.</param>
/// <param name="HeadcountsByAppointmentType">The headcounts by appointment type.</param>
public sealed record EventImportRow(
    int LineNumber, EventWindow Window, IReadOnlyDictionary<Guid, int> HeadcountsByAppointmentType);

/// <summary>Defines event import error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record EventImportError(int LineNumber, string Message);

/// <summary>Defines event import parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record EventImportParseResult(
    IReadOnlyList<EventImportRow> Rows,
    IReadOnlyList<EventImportError> Errors);

/// <summary>
/// Structural validation only, mirroring AttendeeCsvParser (Task 29): header, field count,
/// parseable date/time, a positive integer per fixed AppointmentType column, duplicate windows
/// within the file, and a row-count ceiling. Nothing here reads the database.
/// </summary>
public static class EventImportParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "date,startTime,DAT,MED,UNI";
    /// <summary>Defines max rows for the current use case.</summary>
    public const int MaxRows = 200;

    private static readonly (string Code, Guid Id)[] TypeColumns =
    [
        ("DAT", AppointmentTypeIds.DrugAndAlcoholTesting),
        ("MED", AppointmentTypeIds.MedicalCheckUp),
        ("UNI", AppointmentTypeIds.UniformFitting),
    ];

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    /// <param name="today">The today.</param>
    public static EventImportParseResult Parse(string? content, DateOnly? today = null)
    {
        var rows = new List<EventImportRow>();
        var errors = new List<EventImportError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new EventImportError(0, "The file is empty."));
            return new EventImportParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new EventImportError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new EventImportParseResult(rows, errors);
        }

        var dataLineNumbers = new List<int>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                dataLineNumbers.Add(index);
            }
        }

        if (dataLineNumbers.Count > MaxRows)
        {
            errors.Add(new EventImportError(0, $"A file may contain at most 200 rows."));
            return new EventImportParseResult(rows, errors);
        }

        var seenWindows = new Dictionary<(DateOnly Date, TimeOnly StartTime), int>();

        foreach (var index in dataLineNumbers)
        {
            var lineNumber = index + 1;
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
            {
                errors.Add(new EventImportError(
                    lineNumber, "Expected 5 comma-separated fields: date,startTime,DAT,MED,UNI."));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    fields[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(new EventImportError(lineNumber, $"{fields[0].Trim()} is not a valid date (expected yyyy-MM-dd)."));
                continue;
            }

            if (!TimeOnly.TryParseExact(
                    fields[1].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            {
                errors.Add(new EventImportError(lineNumber, $"{fields[1].Trim()} is not a valid startTime (expected HH:mm)."));
                continue;
            }

            if (!TryReadHeadcounts(fields, lineNumber, errors, out var headcounts))
            {
                continue;
            }

            var window = (date, startTime);

            EventWindow eventWindow;
            try
            {
                eventWindow = new EventWindow(date, startTime);
            }
            catch (DomainException ex)
            {
                errors.Add(new EventImportError(lineNumber, ex.Message));
                continue;
            }

            // Imported events follow the same future-date rule as event proposals.
            if (today.HasValue && !eventWindow.StartsAfter(today.Value))
            {
                errors.Add(new EventImportError(
                    lineNumber, "The event date must be in the future."));
                continue;
            }

            if (seenWindows.TryGetValue(window, out var firstLine))
            {
                errors.Add(new EventImportError(
                    lineNumber, $"Duplicate event window — already used on line {firstLine}."));
                continue;
            }

            seenWindows.Add(window, lineNumber);
            rows.Add(new EventImportRow(lineNumber, eventWindow, headcounts));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new EventImportError(1, "The file contains no event rows."));
        }

        return new EventImportParseResult(rows, errors);
    }

    private static bool TryReadHeadcounts(
        string[] fields,
        int lineNumber,
        List<EventImportError> errors,
        out IReadOnlyDictionary<Guid, int> headcounts)
    {
        var result = new Dictionary<Guid, int>();
        headcounts = result;

        for (var column = 0; column < TypeColumns.Length; column++)
        {
            var (code, id) = TypeColumns[column];
            var raw = fields[column + 2].Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var headcount)
                || headcount <= 0)
            {
                errors.Add(new EventImportError(
                    lineNumber, $"{code} headcount must be a positive whole number."));
                return false;
            }

            result[id] = headcount;
        }

        return true;
    }
}
`````

## before — src/EventBooking.Application/Events/ImportEventsHandler.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Application/Events/ImportEventsHandler.cs","beforeSha":"6d7fa7ef147a1c6c253e8e2db69532db4c83063489df0a5329759e080a4a54fd","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <param name="StaffUserId">The staff identity performing the import.</param>
/// <param name="CsvContent">The raw CSV file content.</param>
/// <param name="AllowPastDates">Whether historical event dates are accepted. Only the demo
/// seeder sets this: its agreed events are deliberately historical, while the user-facing
/// import requires future dates like event proposals do.</param>
public sealed record ImportEventsCommand(Guid StaffUserId, string? CsvContent, bool AllowPastDates = false);

/// <summary>Defines event import outcome for the current use case.</summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record EventImportOutcome(
    bool Accepted, int ImportedCount, IReadOnlyList<EventImportError> Errors);

/// <summary>Defines import events handler for the current use case.</summary>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ImportEventsHandler(
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<EventImportOutcome>> HandleAsync(
        ImportEventsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ImportEvents,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<EventImportOutcome>.Failure(authorized.Error);
        }

        var parsed = EventImportParser.Parse(
            command.CsvContent,
            command.AllowPastDates ? null : clock.TodayAtTransitionalLocation);
        if (parsed.Errors.Count > 0)
        {
            return Result<EventImportOutcome>.Success(
                new EventImportOutcome(false, 0, parsed.Errors));
        }

        foreach (var row in parsed.Rows)
        {
            var eventItem = Event.CreateImported(Guid.NewGuid(), row.Window, row.HeadcountsByAppointmentType);
            events.Add(eventItem);

            audit.Record(
                AuditEntityTypes.Event,
                eventItem.Id,
                AuditAction.EventImported,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"Imported from CSV line {row.LineNumber}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EventImportOutcome>.Success(
            new EventImportOutcome(true, parsed.Rows.Count, []));
    }
}
`````

## before — src/EventBooking.Domain/Audit/AuditAction.cs — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Domain/Audit/AuditAction.cs","beforeSha":"01a641eb364fc18ca072722f09966f130d08519e33654c74ec1116948d4c6499","afterSha":"ebf666be5f6cc17710fffb5a258b16081cfcbfc202b8531f35d0f34e094f304d","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit action for the current use case.</summary>
public enum AuditAction
{
    /// <summary>Defines proposal created for the current use case.</summary>
    ProposalCreated = 1,
    /// <summary>Defines proposal withdrawn for the current use case.</summary>
    ProposalWithdrawn = 2,
    /// <summary>Defines acceptance recorded for the current use case.</summary>
    AcceptanceRecorded = 3,
    /// <summary>Defines acceptance withdrawn for the current use case.</summary>
    AcceptanceWithdrawn = 4,
    /// <summary>Defines event confirmed for the current use case.</summary>
    EventConfirmed = 5,
    /// <summary>Defines event cancelled for the current use case.</summary>
    EventCancelled = 6,
    /// <summary>Defines capacity decremented for the current use case.</summary>
    CapacityDecremented = 7,
    /// <summary>Defines capacity incremented for the current use case.</summary>
    CapacityIncremented = 8,
    /// <summary>Defines invite created for the current use case.</summary>
    InviteCreated = 9,
    /// <summary>Defines invite sent for the current use case.</summary>
    InviteSent = 10,
    /// <summary>Defines invite expired for the current use case.</summary>
    InviteExpired = 11,
    /// <summary>Defines invite option replaced for the current use case.</summary>
    InviteOptionReplaced = 12,
    /// <summary>Defines booking created for the current use case.</summary>
    BookingCreated = 13,
    /// <summary>Defines booking cancelled for the current use case.</summary>
    BookingCancelled = 14,
    /// <summary>Defines capacity adjusted for the current use case.</summary>
    CapacityAdjusted = 15,
    /// <summary>Defines event imported for the current use case.</summary>
    EventImported = 16,
    /// <summary>Defines staff access changed for the current use case.</summary>
    StaffAccessChanged = 17,
    /// <summary>Defines staff access removed for the current use case.</summary>
    StaffAccessRemoved = 18,
    /// <summary>Records an Expected appointment moving to CheckedIn.</summary>
    AppointmentCheckedIn = 19,
    /// <summary>Records a CheckedIn appointment moving to Completed.</summary>
    AppointmentCompleted = 20,
    /// <summary>Records an Expected appointment moving to NoShow.</summary>
    AppointmentMarkedNoShow = 21,
    /// <summary>Records one approved reverse appointment transition.</summary>
    AppointmentStatusCorrected = 22,
    /// <summary>Records the initial Attendee Group assignment of a Attendee.</summary>
    AttendeeGroupAssigned = 23,
    /// <summary>Records a Attendee Attendee Group change and its derived requirements.</summary>
    AttendeeGroupReassigned = 24,
    /// <summary>Records a Coordinator issuing a recovery Invite for missed appointments.</summary>
    RecoveryInviteCreated = 25,
    /// <summary>Records a Coordinator cancelling a pending recovery Invite.</summary>
    RecoveryInviteCancelled = 26,
    /// <summary>Records a recovery Booking linked to its original journey root.</summary>
    RecoveryBookingCreated = 27,
    /// <summary>Records a recovery Booking concluded after terminal appointment outcomes.</summary>
    RecoveryBookingConcluded = 28,
    /// <summary>Records an identity-provider-driven role change applied by the claims sync.</summary>
    StaffRolesSynced = 29,
    /// <summary>Records a Coordinator deleting a Attendee and cascading onto their active bookings.</summary>
    AttendeeDeleted = 30,
}
`````

## after — src/EventBooking.Domain/Audit/AuditAction.cs — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Domain/Audit/AuditAction.cs","beforeSha":"01a641eb364fc18ca072722f09966f130d08519e33654c74ec1116948d4c6499","afterSha":"ebf666be5f6cc17710fffb5a258b16081cfcbfc202b8531f35d0f34e094f304d","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Audit;

/// <summary>Defines audit action for the current use case.</summary>
public enum AuditAction
{
    /// <summary>Defines proposal created for the current use case.</summary>
    ProposalCreated = 1,
    /// <summary>Defines proposal withdrawn for the current use case.</summary>
    ProposalWithdrawn = 2,
    /// <summary>Defines acceptance recorded for the current use case.</summary>
    AcceptanceRecorded = 3,
    /// <summary>Defines acceptance withdrawn for the current use case.</summary>
    AcceptanceWithdrawn = 4,
    /// <summary>Defines event confirmed for the current use case.</summary>
    EventConfirmed = 5,
    /// <summary>Defines event cancelled for the current use case.</summary>
    EventCancelled = 6,
    /// <summary>Defines capacity decremented for the current use case.</summary>
    CapacityDecremented = 7,
    /// <summary>Defines capacity incremented for the current use case.</summary>
    CapacityIncremented = 8,
    /// <summary>Defines invite created for the current use case.</summary>
    InviteCreated = 9,
    /// <summary>Defines invite sent for the current use case.</summary>
    InviteSent = 10,
    /// <summary>Defines invite expired for the current use case.</summary>
    InviteExpired = 11,
    /// <summary>Defines invite option replaced for the current use case.</summary>
    InviteOptionReplaced = 12,
    /// <summary>Defines booking created for the current use case.</summary>
    BookingCreated = 13,
    /// <summary>Defines booking cancelled for the current use case.</summary>
    BookingCancelled = 14,
    /// <summary>Defines capacity adjusted for the current use case.</summary>
    CapacityAdjusted = 15,
    /// <summary>Defines staff access changed for the current use case.</summary>
    StaffAccessChanged = 17,
    /// <summary>Records an Expected appointment moving to CheckedIn.</summary>
    AppointmentCheckedIn = 19,
    /// <summary>Records a CheckedIn appointment moving to Completed.</summary>
    AppointmentCompleted = 20,
    /// <summary>Records an Expected appointment moving to NoShow.</summary>
    AppointmentMarkedNoShow = 21,
    /// <summary>Records one approved reverse appointment transition.</summary>
    AppointmentStatusCorrected = 22,
    /// <summary>Records the initial Attendee Group assignment of a Attendee.</summary>
    AttendeeGroupAssigned = 23,
    /// <summary>Records a Attendee Attendee Group change and its derived requirements.</summary>
    AttendeeGroupReassigned = 24,
    /// <summary>Records a Coordinator issuing a recovery Invite for missed appointments.</summary>
    RecoveryInviteCreated = 25,
    /// <summary>Records a Coordinator cancelling a pending recovery Invite.</summary>
    RecoveryInviteCancelled = 26,
    /// <summary>Records a recovery Booking linked to its original journey root.</summary>
    RecoveryBookingCreated = 27,
    /// <summary>Records a recovery Booking concluded after terminal appointment outcomes.</summary>
    RecoveryBookingConcluded = 28,
    /// <summary>Records an identity-provider-driven role change applied by the claims sync.</summary>
    StaffRolesSynced = 29,
    /// <summary>Records a Coordinator deleting a Attendee and cascading onto their active bookings.</summary>
    AttendeeDeleted = 30,
}
`````

## before — src/EventBooking.Domain/Events/Event.cs — 1/1

<!-- retirement-file: {"id":12,"file":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"50534079eb95597aaef7496e64b85f87f25b3927d7655c6ebfd542d338ebd094","afterSha":"7176938fa57aa67da2992f8f04353eb77bc09dd70592e56fde0881e6ef098316","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid? ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>
    /// Creates an active event with no backing proposal from a strictly complete set of
    /// positive headcounts for the three fixed appointment types.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="window">The window.</param>
    /// <param name="headcountsByAppointmentType">The headcounts by appointment type.</param>
    public static Event CreateImported(
        Guid id, EventWindow window, IReadOnlyDictionary<Guid, int> headcountsByAppointmentType)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(window is null, "window must be supplied.");
        Guard.Against(headcountsByAppointmentType is null, "headcountsByAppointmentType must be supplied.");
        Guard.Against(
            headcountsByAppointmentType!.Count != AppointmentTypeIds.All.Count
            || headcountsByAppointmentType.Keys.Any(id => !AppointmentTypeIds.All.Contains(id)),
            "Headcounts must be supplied for exactly the three fixed appointment types.");

        var eventItem = new Event
        {
            Id = id,
            ProposalId = null,
            Window = window!,
            Status = EventStatus.Active,
        };

        foreach (var appointmentTypeId in AppointmentTypeIds.All)
        {
            Guard.Against(
                !headcountsByAppointmentType.TryGetValue(appointmentTypeId, out var headcount),
                $"A headcount is required for {AppointmentTypeIds.NameOf(appointmentTypeId)}.");

            eventItem._capacities.Add(EventCapacity.Initialise(id, appointmentTypeId, headcount));
        }

        return eventItem;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Status = EventStatus.Cancelled;
    }
}
`````

## after — src/EventBooking.Domain/Events/Event.cs — 1/1

<!-- retirement-file: {"id":12,"file":"src/EventBooking.Domain/Events/Event.cs","beforeSha":"50534079eb95597aaef7496e64b85f87f25b3927d7655c6ebfd542d338ebd094","afterSha":"7176938fa57aa67da2992f8f04353eb77bc09dd70592e56fde0881e6ef098316","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>Defines event for the current use case.</summary>
public sealed class Event
{
    private readonly List<EventCapacity> _capacities = [];

    private Event()
    {
        // Required by the persistence layer's constructor binding.
        Window = null!;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines proposal id for the current use case.</summary>
    public Guid ProposalId { get; private set; }

    /// <summary>Defines window for the current use case.</summary>
    public EventWindow Window { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public EventStatus Status { get; private set; } = EventStatus.Active;

    /// <summary>Defines capacities for the current use case.</summary>
    public IReadOnlyList<EventCapacity> Capacities => _capacities;

    /// <summary>
    /// The only way a eventItem is created. Marks the proposal confirmed in the same call, so
    /// a proposal can never back a second eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="proposal">The proposal.</param>
    public static Event CreateFrom(Guid id, EventProposal proposal)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(proposal is null, "proposal must be supplied.");

        proposal!.MarkConfirmed();

        var eventItem = new Event
        {
            Id = id,
            ProposalId = proposal.Id,
            Window = proposal.Window,
            Status = EventStatus.Active,
        };

        foreach (var acceptance in proposal.Acceptances.OrderBy(a => a.AppointmentTypeId))
        {
            eventItem._capacities.Add(
                EventCapacity.Initialise(id, acceptance.AppointmentTypeId, acceptance.Headcount));
        }

        return eventItem;
    }

    /// <summary>Defines capacity for for the current use case.</summary>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public EventCapacity CapacityFor(Guid appointmentTypeId)
    {
        AppointmentTypeIds.EnsureKnown(appointmentTypeId);

        var capacity = _capacities.SingleOrDefault(c => c.AppointmentTypeId == appointmentTypeId);
        Guard.Against(capacity is null, $"This event has no capacity counter for {appointmentTypeId}.");

        return capacity!;
    }

    /// <summary>Defines has spare capacity for all for the current use case.</summary>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public bool HasSpareCapacityForAll(IEnumerable<Guid> appointmentTypeIds) =>
        Status == EventStatus.Active
        && appointmentTypeIds.All(id => CapacityFor(id).HasSpare);

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == EventStatus.Cancelled, "This event has already been cancelled.");
        Status = EventStatus.Cancelled;
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"5d1a441c5c038d76d7b3d79d1a1c35cf437f8fa4de11922f2d3dff3787cf0cc2","afterSha":"776dd446794cd0f3882139eac9961f46b62111f8a4e95cf3a0c837ad8e43972e","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"5d1a441c5c038d76d7b3d79d1a1c35cf437f8fa4de11922f2d3dff3787cf0cc2","afterSha":"776dd446794cd0f3882139eac9961f46b62111f8a4e95cf3a0c837ad8e43972e","side":"after","part":1,"parts":1} -->

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
