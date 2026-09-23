using System.Text;
using System.Text.Json.Serialization;
using EventBooking.Api.Auth;
using EventBooking.Api.Contracts;
using EventBooking.Api.OpenApi;
using EventBooking.Application.Appointments;
using EventBooking.Application.Common;
using EventBooking.Domain.Bookings;

namespace EventBooking.Api.Endpoints;

/// <summary>Accepts one requested appointment status and the last observed version.</summary>
public sealed record UpdateBookingAppointmentStatusRequest
{
    /// <summary>Gets the requested `BookingAppointmentStatus` name.</summary>
    public string? Status { get; init; }

    /// <summary>Gets the positive version last observed by the caller.</summary>
    public long? ExpectedVersion { get; init; }
}

/// <summary>Maps the three authenticated appointment-workspace routes.</summary>
public static class AppointmentWorkspaceEndpoints
{
    /// <summary>Adds the capability-protected appointment-workspace routes.</summary>
    public static IEndpointRouteBuilder MapAppointmentWorkspaceEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/appointment-workspace")
            .RequireAuthorization(AuthenticationExtensions.StaffPolicy);

        group.MapGet("/slots", async (
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.ListSlotsAsync(
                caller.RequireStaffUserId(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(AppointmentWorkspaceSlotListResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("listAppointmentSlots")
            .Produces(200)
            .ProducesProblem(403);

        group.MapGet("/slots/{confirmedSlotId:guid}", async (
            Guid confirmedSlotId,
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.GetSlotAsync(
                caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(AppointmentSlotDetailResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("getAppointmentSlot")
            .Produces(200)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapGet("/slots/{confirmedSlotId:guid}/roster", async (
            Guid confirmedSlotId,
            ICallerAccessor caller,
            GetAppointmentWorkspaceHandler handler,
            AppointmentRosterCsvFormatter formatter,
            CancellationToken cancellationToken) =>
        {
            // The same read the JSON detail route performs, so authorization, scoping, and the
            // not-found-for-out-of-scope behaviour are identical by construction.
            var result = await handler.GetSlotAsync(
                caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
            if (result.IsFailure)
            {
                return result.ToResponse();
            }

            var roster = formatter.Format(result.Value);
            return Results.File(
                Encoding.UTF8.GetBytes(roster.CsvText),
                contentType: "text/csv; charset=utf-8",
                fileDownloadName: roster.FileName);
        })
            .WithAgentMetadata("exportAppointmentRoster")
            .Produces<string>(200, "text/csv")
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapPut("/appointments/{bookingAppointmentId:guid}/status", async (
            Guid bookingAppointmentId,
            UpdateBookingAppointmentStatusRequest? request,
            ICallerAccessor caller,
            UpdateBookingAppointmentStatusHandler handler,
            CancellationToken cancellationToken) =>
        {
            var expectedVersion = request?.ExpectedVersion ?? 0;
            if (request?.Status is null
                || !Enum.TryParse<BookingAppointmentStatus>(
                    request.Status, ignoreCase: false, out var status)
                || !Enum.IsDefined(status)
                || expectedVersion <= 0)
            {
                return Result<BookingAppointmentUpdateView>.Failure(
                    Error.Validation("A recognised status and positive expectedVersion are required."))
                    .ToResponse();
            }

            var result = await handler.HandleAsync(
                new UpdateBookingAppointmentStatusCommand
                {
                    StaffUserId = caller.RequireStaffUserId(),
                    BookingAppointmentId = bookingAppointmentId,
                    Status = status,
                    ExpectedVersion = expectedVersion,
                },
                cancellationToken);
            return result.IsSuccess
                ? Results.Ok(BookingAppointmentUpdateResponse.From(result.Value))
                : result.ToResponse();
        })
            .WithAgentMetadata("updateAppointmentStatus")
            .Produces(200)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409);

        return app;
    }
}

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCountsResponse
{
    /// <summary>Gets candidates booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }

    /// <summary>Gets candidates checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }

    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }

    /// <summary>Gets candidates recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }

    internal static AppointmentStatusCountsResponse From(AppointmentStatusCounts counts) => new()
    {
        Expected = counts.Expected,
        CheckedIn = counts.CheckedIn,
        Completed = counts.Completed,
        NoShow = counts.NoShow,
    };
}

/// <summary>Describes one selectable active slot without candidate rows.</summary>
public sealed record AppointmentSlotSummaryResponse
{
    /// <summary>Gets the confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }

    /// <summary>Gets the slot's head-office calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCountsResponse Counts { get; init; }

    /// <summary>Gets the detail and roster affordances for the slot.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentSlotSummaryResponse From(AppointmentSlotSummary summary) => new()
    {
        ConfirmedSlotId = summary.ConfirmedSlotId,
        Date = summary.Date,
        StartTime = summary.StartTime,
        EndTime = summary.EndTime,
        Counts = AppointmentStatusCountsResponse.From(summary.Counts),
        Links = StaffResourceLinks.ForAppointmentSlot(summary.ConfirmedSlotId),
    };
}

/// <summary>Returns the trusted appointment-type name and its selectable active slots.</summary>
public sealed record AppointmentWorkspaceSlotListResponse
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets current and upcoming active slots containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentSlotSummaryResponse> Slots { get; init; }

    /// <summary>Gets the collection self affordance.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentWorkspaceSlotListResponse From(AppointmentWorkspaceSlotList list) => new()
    {
        AppointmentTypeName = list.AppointmentTypeName,
        Slots = list.Slots.Select(AppointmentSlotSummaryResponse.From).ToList(),
        Links = new Dictionary<string, ApiLink>
        {
            ["self"] = new("/api/appointment-workspace/slots", "GET", "listAppointmentSlots"),
        },
    };
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRowResponse
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets the candidate name used for primary human identification.</summary>
    public required string CandidateName { get; init; }

    /// <summary>Gets the candidate email used for secondary human identification.</summary>
    public required string CandidateEmail { get; init; }

    /// <summary>Gets this appointment's independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }

    /// <summary>Gets the status-update affordance for the appointment.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static BookingAppointmentRowResponse From(BookingAppointmentRow row) => new()
    {
        BookingAppointmentId = row.BookingAppointmentId,
        CandidateName = row.CandidateName,
        CandidateEmail = row.CandidateEmail,
        Status = row.Status.ToString(),
        CheckedInAt = row.CheckedInAt,
        OutcomeAt = row.OutcomeAt,
        Version = row.Version,
        Links = StaffResourceLinks.ForBookingAppointment(row.BookingAppointmentId),
    };
}

/// <summary>Returns one scoped active slot and only its minimum-data operational rows.</summary>
public sealed record AppointmentSlotDetailResponse
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets the selected confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }

    /// <summary>Gets the slot's head-office calendar date.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }

    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }

    /// <summary>Gets scoped active-booking appointment rows ordered for staff identification.</summary>
    public required IReadOnlyList<BookingAppointmentRowResponse> Appointments { get; init; }

    /// <summary>Gets the detail self and roster affordances.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static AppointmentSlotDetailResponse From(AppointmentSlotDetail detail) => new()
    {
        AppointmentTypeName = detail.AppointmentTypeName,
        ConfirmedSlotId = detail.ConfirmedSlotId,
        Date = detail.Date,
        StartTime = detail.StartTime,
        EndTime = detail.EndTime,
        Appointments = detail.Appointments.Select(BookingAppointmentRowResponse.From).ToList(),
        Links = new Dictionary<string, ApiLink>
        {
            ["self"] = new($"/api/appointment-workspace/slots/{detail.ConfirmedSlotId}", "GET", "getAppointmentSlot"),
            ["roster"] = new($"/api/appointment-workspace/slots/{detail.ConfirmedSlotId}/roster", "GET", "exportAppointmentRoster"),
        },
    };
}

/// <summary>Returns only the changed appointment row state needed by the workspace.</summary>
public sealed record BookingAppointmentUpdateResponse
{
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }

    /// <summary>Gets this appointment's current independent operational status name.</summary>
    public required string Status { get; init; }

    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public DateTimeOffset? CheckedInAt { get; init; }

    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public DateTimeOffset? OutcomeAt { get; init; }

    /// <summary>Gets the current positive concurrency version.</summary>
    public required long Version { get; init; }

    /// <summary>Gets the status-update affordance for the appointment.</summary>
    [JsonPropertyName("_links")]
    public required IReadOnlyDictionary<string, ApiLink> Links { get; init; }

    internal static BookingAppointmentUpdateResponse From(BookingAppointmentUpdateView view) => new()
    {
        BookingAppointmentId = view.BookingAppointmentId,
        Status = view.Status.ToString(),
        CheckedInAt = view.CheckedInAt,
        OutcomeAt = view.OutcomeAt,
        Version = view.Version,
        Links = StaffResourceLinks.ForBookingAppointment(view.BookingAppointmentId),
    };
}
