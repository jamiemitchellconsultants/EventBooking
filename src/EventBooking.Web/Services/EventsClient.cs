using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record OpenProposalDto(
    Guid ProposalId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

public sealed record EventDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

public sealed record AdjustConfirmedCapacityDto(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity);

public sealed record EventBoardDto(
    IReadOnlyList<OpenProposalDto> OpenProposals,
    IReadOnlyList<EventDto> Events);

/// <summary>Remaining and accepted places for one appointment type within a eventItem.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="TotalHeadcount">The headcount the manager accepted for this appointment type.</param>
/// <param name="RemainingCapacity">The places still free for this appointment type.</param>
public sealed record EventOperationCapacityDto(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>One eventItem in the event-only operations view; carries no attendee data.</summary>
/// <param name="EventId">The event the row describes.</param>
/// <param name="Date">The date of the confirmed window.</param>
/// <param name="StartTime">The start of the confirmed window.</param>
/// <param name="EndTime">The end of the confirmed window.</param>
/// <param name="Capacities">Per-appointment-type capacity for this window.</param>
/// <param name="ActiveBookings">How many active bookings the window currently holds.</param>
public sealed record EventOperationDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<EventOperationCapacityDto> Capacities,
    int ActiveBookings);

/// <summary>The event-only operations view returned to administrators and coordinators.</summary>
/// <param name="Events">Every active eventItem, newest data as the server returned it.</param>
public sealed record EventOperationsDto(IReadOnlyList<EventOperationDto> Events);

public sealed class EventsClient(HttpClient http)
{
    public async Task<ApiOutcome<EventBoardDto>> GetBoardAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/events/board", cancellationToken);
        return await ApiCall.ReadAsync<EventBoardDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> ProposeAsync(
        DateOnly date,
        TimeOnly startTime,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/event-proposals",
            new { Date = date, StartTime = startTime },
            cancellationToken);

        return await ApiCall.ReadAsync<Guid>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> AcceptAsync(
        Guid proposalId,
        int headcount,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = headcount },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawAcceptanceAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/event-proposals/{proposalId}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<AdjustConfirmedCapacityDto>> AdjustCapacityAsync(
        Guid eventId,
        int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = totalHeadcount },
            cancellationToken);

        return await ApiCall.ReadAsync<AdjustConfirmedCapacityDto>(
            response,
            cancellationToken);
    }

    /// <summary>Loads the event-only operations view; never touches attendee data.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every active eventItem, or the failure the API reported.</returns>
    public async Task<ApiOutcome<EventOperationsDto>> GetEventOperationsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/events/operations", cancellationToken);
        return await ApiCall.ReadAsync<EventOperationsDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> CancelEventAsync(
        Guid eventId,
        bool confirm,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/events/{eventId}?confirm={(confirm ? "true" : "false")}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
