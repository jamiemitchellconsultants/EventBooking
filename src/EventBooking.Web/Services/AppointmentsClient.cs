using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCountsDto
{
    /// <summary>Gets attendees booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }

    /// <summary>Gets attendees checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }

    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }

    /// <summary>Gets attendees recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }
}

/// <summary>Describes one selectable active event without attendee rows.</summary>
public sealed record AppointmentEventSummaryDto
{
    /// <summary>Gets the event identifier.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCountsDto Counts { get; init; }
}

/// <summary>Returns the trusted appointment-type name and its selectable active events.</summary>
public sealed record AppointmentWorkspaceEventListDto
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets current and upcoming active events containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentEventSummaryDto> Events { get; init; }
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRowDto
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets the attendee name used for primary human identification.</summary>
    public required string AttendeeName { get; init; }

    /// <summary>Gets the attendee email used for secondary human identification.</summary>
    public required string AttendeeEmail { get; init; }

    /// <summary>Gets this appointment's independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }
}

/// <summary>Returns one scoped active event and only its minimum-data operational rows.</summary>
public sealed record AppointmentEventDetailDto
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets the selected event identifier.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped active-booking appointment rows ordered for staff identification.</summary>
    public required IReadOnlyList<BookingAppointmentRowDto> Appointments { get; init; }
}

/// <summary>Returns only the changed appointment row state needed by the workspace.</summary>
public sealed record BookingAppointmentUpdateDto
{
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets this appointment's current independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the current positive concurrency version.</summary>
    public required long Version { get; init; }
}

/// <summary>The downloaded roster: raw CSV content plus the server-suggested filename.</summary>
public sealed record RosterCsvDownload
{
    /// <summary>Gets the raw CSV response body, header row first.</summary>
    public required string Content { get; init; }

    /// <summary>Gets the filename taken from the response's content-disposition header.</summary>
    public required string FileName { get; init; }
}

/// <summary>Calls the minimum-data appointment-workspace HTTP surface.</summary>
public sealed class AppointmentsClient(HttpClient http)
{
    /// <summary>Identifies a stale booking-appointment version that requires a detail refresh.</summary>
    public const string VersionConflictErrorCode = "appointment_version_conflict";

    /// <summary>Gets the scoped current and upcoming event list.</summary>
    public async Task<ApiOutcome<AppointmentWorkspaceEventListDto>> ListEventsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            "/api/appointment-workspace/events", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentWorkspaceEventListDto>(response, cancellationToken);
    }

    /// <summary>Gets one selected event's scoped operational rows.</summary>
    public async Task<ApiOutcome<AppointmentEventDetailDto>> GetEventAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/events/{eventId}", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentEventDetailDto>(response, cancellationToken);
    }

    /// <summary>Downloads the scoped roster CSV for one selected eventItem.</summary>
    /// <param name="eventId">The selected event identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Raw CSV content plus the server-suggested filename, or the parsed failure.</returns>
    public async Task<ApiOutcome<RosterCsvDownload>> GetRosterAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/events/{eventId}/roster", cancellationToken);
        var outcome = await ApiCall.ReadTextWithHeaderAsync(
            response, "Content-Disposition", cancellationToken);

        return outcome is { IsSuccess: true, Value: not null }
            ? ApiOutcome<RosterCsvDownload>.Success(
                new RosterCsvDownload
                {
                    Content = outcome.Value.Text,
                    FileName = FileNameOf(outcome.Value.Header),
                },
                outcome.StatusCode)
            : ApiOutcome<RosterCsvDownload>.Failure(
                outcome.ErrorMessage ?? "Something went wrong. Please try again.",
                outcome.StatusCode,
                outcome.ErrorCode);
    }

    /// <summary>Reads the filename from a content-disposition value, starred form preferred.</summary>
    private static string FileNameOf(string disposition)
    {
        const string Fallback = "roster.csv";
        if (string.IsNullOrWhiteSpace(disposition))
        {
            return Fallback;
        }

        if (!ContentDispositionHeaderValue.TryParse(disposition, out var parsed))
        {
            return Fallback;
        }

        // The starred form is preferred: it carries the percent-encoded UTF-8 name.
        var name = parsed.FileNameStar ?? parsed.FileName;
        return string.IsNullOrWhiteSpace(name) ? Fallback : name.Trim('"');
    }

    /// <summary>Requests one status using the row's last observed version.</summary>
    public async Task<ApiOutcome<BookingAppointmentUpdateDto>> UpdateStatusAsync(
        Guid bookingAppointmentId,
        string status,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{bookingAppointmentId}/status",
            new { Status = status, ExpectedVersion = expectedVersion }, cancellationToken);
        return await ApiCall.ReadAsync<BookingAppointmentUpdateDto>(response, cancellationToken);
    }
}
