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

public sealed record ConfirmedSlotDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

public sealed record AdjustConfirmedCapacityDto(
    Guid ConfirmedSlotId,
    int TotalHeadcount,
    int RemainingCapacity);

public sealed record SlotBoardDto(
    IReadOnlyList<OpenProposalDto> OpenProposals,
    IReadOnlyList<ConfirmedSlotDto> ConfirmedSlots);

/// <summary>Remaining and accepted places for one appointment type within a confirmed slot.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="TotalHeadcount">The headcount the manager accepted for this appointment type.</param>
/// <param name="RemainingCapacity">The places still free for this appointment type.</param>
public sealed record SlotOperationCapacityDto(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>One confirmed slot in the slot-only operations view; carries no candidate data.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot the row describes.</param>
/// <param name="Date">The date of the confirmed window.</param>
/// <param name="StartTime">The start of the confirmed window.</param>
/// <param name="EndTime">The end of the confirmed window.</param>
/// <param name="Capacities">Per-appointment-type capacity for this window.</param>
/// <param name="ActiveBookings">How many active bookings the window currently holds.</param>
public sealed record SlotOperationDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SlotOperationCapacityDto> Capacities,
    int ActiveBookings);

/// <summary>The slot-only operations view returned to administrators and coordinators.</summary>
/// <param name="Slots">Every active confirmed slot, newest data as the server returned it.</param>
public sealed record SlotOperationsDto(IReadOnlyList<SlotOperationDto> Slots);

public sealed class SlotsClient(HttpClient http)
{
    public async Task<ApiOutcome<SlotBoardDto>> GetBoardAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/slots/board", cancellationToken);
        return await ApiCall.ReadAsync<SlotBoardDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> ProposeAsync(
        DateOnly date,
        TimeOnly startTime,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/slots/proposals",
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
            $"/api/slots/proposals/{proposalId}/acceptance",
            new { Headcount = headcount },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawAcceptanceAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/proposals/{proposalId}/acceptance",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/proposals/{proposalId}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<AdjustConfirmedCapacityDto>> AdjustCapacityAsync(
        Guid confirmedSlotId,
        int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/slots/confirmed/{confirmedSlotId}/capacity",
            new { TotalHeadcount = totalHeadcount },
            cancellationToken);

        return await ApiCall.ReadAsync<AdjustConfirmedCapacityDto>(
            response,
            cancellationToken);
    }

    /// <summary>Loads the slot-only operations view; never touches candidate data.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every active confirmed slot, or the failure the API reported.</returns>
    public async Task<ApiOutcome<SlotOperationsDto>> GetSlotOperationsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/slots/operations", cancellationToken);
        return await ApiCall.ReadAsync<SlotOperationsDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> CancelConfirmedSlotAsync(
        Guid slotId,
        bool confirm,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/confirmed/{slotId}?confirm={(confirm ? "true" : "false")}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
