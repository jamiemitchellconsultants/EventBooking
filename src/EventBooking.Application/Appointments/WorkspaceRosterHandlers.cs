using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Appointments;

/// <summary>One roster row: delivery-list fields only, never identifiers.</summary>
/// <param name="Name">The attendee display name.</param>
/// <param name="Email">The attendee email.</param>
/// <param name="ScopeTypeCode">The caller's scope type code.</param>
/// <param name="AppointmentStatus">The appointment status.</param>
/// <param name="CheckedInAt">The check-in instant, when checked in.</param>
/// <param name="Version">The appointment version for stale-page detection.</param>
public sealed record WorkspaceRosterRow(
    string Name, string Email, string ScopeTypeCode, string AppointmentStatus,
    DateTimeOffset? CheckedInAt, long Version);

/// <summary>Sets one appointment's status from the workspace.</summary>
/// <param name="StaffUserId">The appointment-staff identity acting.</param>
/// <param name="AppointmentId">The appointment to move.</param>
/// <param name="TargetStatus">The wanted status name.</param>
/// <param name="ExpectedVersion">The version the page was rendered from.</param>
public sealed record SetAppointmentStatusCommand(
    Guid StaffUserId, Guid AppointmentId, string TargetStatus, long ExpectedVersion);

/// <summary>Reads one event's workspace roster.</summary>
/// <param name="StaffUserId">The appointment-staff identity asking.</param>
/// <param name="EventId">The event whose roster is read.</param>
public sealed record GetWorkspaceRosterQuery(Guid StaffUserId, Guid EventId);

/// <summary>Reads one event's roster scoped to the caller's appointment type.</summary>
/// <param name="appointments">The appointments.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="access">The staff access authorizer.</param>
public sealed class GetWorkspaceRosterHandler(
    IBookingAppointmentRepository appointments,
    IBookingRepository bookings,
    IAttendeeRepository attendees,
    IEventRepository events,
    IAppointmentTypeRepository appointmentTypes,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<WorkspaceRosterRow>>> HandleAsync(
        GetWorkspaceRosterQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ConductAppointments, null, ct);
        if (authorized.IsFailure)
            return Result<IReadOnlyList<WorkspaceRosterRow>>.Failure(authorized.Error);
        if (authorized.Value.AppointmentTypeId is not { } scopeType)
            return Result<IReadOnlyList<WorkspaceRosterRow>>.Failure(
                Error.Forbidden("The workspace needs an assigned appointment type."));

        var eventItem = await events.GetAsync(query.EventId, ct);
        if (eventItem is null)
            return Result<IReadOnlyList<WorkspaceRosterRow>>.Failure(
                Error.NotFound("No such event."));

        // An event that does not list the caller's type is not theirs to see, and saying
        // so as forbidden rather than empty keeps a missing scope distinguishable from an
        // event with nobody booked.
        if (eventItem.Capacities.All(c => c.AppointmentTypeId != scopeType))
            return Result<IReadOnlyList<WorkspaceRosterRow>>.Failure(
                Error.Forbidden("This event does not offer your appointment type."));

        // The appointment port lists by booking, so the event's active bookings are
        // resolved first. Active only: a cancelled booking's attendee is not expected.
        var eventBookings = await bookings.ListActiveForEventAsync(query.EventId, ct);
        if (eventBookings.Count == 0)
            return Result<IReadOnlyList<WorkspaceRosterRow>>.Success([]);

        var rows = await appointments.ListForBookingsAsync(
            [.. eventBookings.Select(b => b.Id)], ct);
        var attendeeIdByBooking = eventBookings.ToDictionary(b => b.Id, b => b.AttendeeId);
        var scopeCode = (await appointmentTypes.GetAsync(scopeType, ct))?.Code ?? string.Empty;

        var roster = new List<WorkspaceRosterRow>();
        foreach (var appointment in rows.Where(a => a.AppointmentTypeId == scopeType))
        {
            if (!attendeeIdByBooking.TryGetValue(appointment.BookingId, out var attendeeId))
                continue;
            var attendee = await attendees.GetAsync(attendeeId, ct);
            if (attendee is null) continue;

            // Names and emails travel because the roster is the delivery list; no
            // identifiers do, so a leaked roster cannot be joined back to other records.
            roster.Add(new WorkspaceRosterRow(
                attendee.Name, attendee.Email, scopeCode,
                appointment.Status.ToString(), appointment.CheckedInAt,
                appointment.Version));
        }

        return Result<IReadOnlyList<WorkspaceRosterRow>>.Success(
            [.. roster.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Email, StringComparer.OrdinalIgnoreCase)]);
    }
}

/// <summary>
/// Moves one appointment to its next status, judging every time rule in the event
/// location's zone. A no-show correction is refused while a recovery invite or booking
/// for the same type is still open.
/// </summary>
/// <param name="appointments">The appointments.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="invites">The invites.</param>
/// <param name="events">The events.</param>
/// <param name="locations">The locations.</param>
/// <param name="access">The staff access authorizer.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction windows are read in.</param>
public sealed class SetAppointmentStatusHandler(
    IBookingAppointmentRepository appointments,
    IBookingRepository bookings,
    IInviteRepository invites,
    IEventRepository events,
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        SetAppointmentStatusCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId, StaffCapability.ConductAppointments, null, ct);
        if (authorized.IsFailure) return Result.Failure(authorized.Error);
        if (authorized.Value.AppointmentTypeId is not { } scopeType)
            return Result.Failure(
                Error.Forbidden("The workspace needs an assigned appointment type."));

        if (!Enum.TryParse<BookingAppointmentStatus>(
                command.TargetStatus, ignoreCase: true, out var target))
            return Result.Failure(
                Error.Validation($"{command.TargetStatus} is not an appointment status."));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        // The lock itself is scope-qualified: a row of another type reads as absent,
        // which also keeps this endpoint from becoming an existence oracle for rows
        // outside the caller's type.
        var appointment = await appointments.LockForUpdateAsync(
            command.AppointmentId, scopeType, ct);
        if (appointment is null)
            return Result.Failure(Error.NotFound("No such appointment."));

        // The version is checked before the window rules, so a stale page is told it is
        // stale rather than told its action is out of hours.
        if (appointment.Version != command.ExpectedVersion)
            return Result.Failure(Error.AppointmentVersionConflict(
                $"The appointment has moved on to version {appointment.Version}.",
                appointment.Version));

        var booking = await bookings.GetAsync(appointment.BookingId, ct);
        if (booking is null) return Result.Failure(Error.NotFound("No such booking."));
        var eventItem = await events.GetAsync(booking.EventId, ct);
        if (eventItem is null) return Result.Failure(Error.NotFound("No such event."));
        var location = await locations.GetAsync(eventItem.LocationId, ct);
        if (location is null) return Result.Failure(Error.NotFound("No such location."));

        var now = clock.UtcNow;
        var checkInAllowed =
            zones.LocalDateOf(now, location.TimeZoneId) == eventItem.Window.Date;
        var noShowAllowed = eventItem.Window.HasEnded(zones, location.TimeZoneId, now);

        // A correction back to Expected cannot stand while a recovery is already running
        // for the same type: the attendee would hold both a second chance and the first.
        // The pending invite is locked before the recovery booking so the two stay in
        // ladder order, and so a recovery issuing concurrently serializes here instead
        // of committing a stale invite beside the correction.
        if (appointment.Status == BookingAppointmentStatus.NoShow
            && target == BookingAppointmentStatus.Expected)
        {
            var openRecovery = await invites.LockPendingForAttendeeAsync(booking.AttendeeId, ct);
            if (openRecovery?.RecoveryOfBookingId is not null
                && openRecovery.RequiredAppointmentTypeIds.Contains(appointment.AppointmentTypeId))
                return Result.Failure(Error.RecoveryActive(
                    "A recovery invite is already open for this appointment type."));

            var rootId = booking.RecoveryOfBookingId ?? booking.Id;
            if (await bookings.LockActiveRecoveryAsync(rootId, ct) is not null)
                return Result.Failure(Error.RecoveryActive(
                    "A recovery booking is already open for this attendee."));
        }

        bool changed;
        try
        {
            changed = appointment.TransitionTo(
                target, command.StaffUserId, now, checkInAllowed, noShowAllowed);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        // Setting a status it already holds is not a change, and an audit trail that
        // records it would make a refreshed page look like an action.
        if (!changed)
        {
            await transaction.CommitAsync(ct);
            return Result.Success();
        }

        audit.Record(
            AuditEntityTypes.BookingAppointment,
            appointment.Id,
            ActionFor(appointment.Status, target),
            ActorType.Staff,
            command.StaffUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    private static AuditAction ActionFor(
        BookingAppointmentStatus from, BookingAppointmentStatus to) => to switch
    {
        BookingAppointmentStatus.CheckedIn when from == BookingAppointmentStatus.Expected =>
            AuditAction.AppointmentCheckedIn,
        BookingAppointmentStatus.NoShow when from != BookingAppointmentStatus.Completed =>
            AuditAction.AppointmentMarkedNoShow,
        BookingAppointmentStatus.Completed when from == BookingAppointmentStatus.CheckedIn =>
            AuditAction.AppointmentCompleted,
        // Anything else is a correction of a settled outcome, which is the move the
        // audit reader most needs to be able to find.
        _ => AuditAction.AppointmentStatusCorrected,
    };
}

/// <summary>Downloads one event's roster as neutralised CSV.</summary>
/// <param name="roster">The roster handler.</param>
public sealed class DownloadRosterHandler(GetWorkspaceRosterHandler roster)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<string>> HandleAsync(
        GetWorkspaceRosterQuery query, CancellationToken ct)
    {
        var rows = await roster.HandleAsync(query, ct);
        return rows.IsFailure
            ? Result<string>.Failure(rows.Error)
            : Result<string>.Success(RosterCsv.Render(
                rows.Value, ["Name", "Email", "Status", "CheckedInAt", "Version"]));
    }
}
