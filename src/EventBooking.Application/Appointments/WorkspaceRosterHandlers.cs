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

/// <summary>One roster row: delivery-list fields plus the stable command identifier.</summary>
/// <param name="AppointmentId">The booking-appointment command identifier.</param>
/// <param name="Name">The attendee display name.</param>
/// <param name="Email">The attendee email.</param>
/// <param name="ScopeTypeCode">The caller's scope type code.</param>
/// <param name="AppointmentStatus">The appointment status.</param>
/// <param name="CheckedInAt">The check-in instant, when checked in.</param>
/// <param name="Version">The appointment version for stale-page detection.</param>
public sealed record WorkspaceRosterRow(
    Guid AppointmentId, string Name, string Email, string ScopeTypeCode,
    string AppointmentStatus, DateTimeOffset? CheckedInAt, long Version);

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

            // Names and emails travel because the roster is the delivery list. The
            // appointment identifier travels too: the workspace client addresses its
            // status command with it, and a random identifier joins nothing without
            // the capability-gated API itself. Attendee, booking and requirement
            // identifiers still never leave this handler.
            roster.Add(new WorkspaceRosterRow(
                appointment.Id, attendee.Name, attendee.Email, scopeCode,
                appointment.Status.ToString(), appointment.CheckedInAt,
                appointment.Version));
        }

        return Result<IReadOnlyList<WorkspaceRosterRow>>.Success(
            [.. roster.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Email, StringComparer.OrdinalIgnoreCase)]);
    }
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
                rows.Value, ["Name", "Email", "Status", "CheckedInAt"]));
    }
}
