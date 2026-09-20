# 00b — Vocabulary edits 62 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Web/Pages/Slots.razor — 1/1

<!-- vocabulary-file: {"id":201,"oldPath":"src/EventBooking.Web/Pages/Slots.razor","newPath":"src/EventBooking.Web/Pages/EventNegotiation.razor","beforeSha":"066eae7b8f0b02f46e43b150af3b24d891047e5bbd4f52dd5d87111bfe2fd31e","afterSha":"976282f095b37c225f071070fd8ffa45a67383320277ae1df62f20793da86f62","side":"before","part":1,"parts":1} -->

`````razor
@page "/slots"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject SlotsClient SlotsApi

<PageTitle>Slot proposals</PageTitle>

<div class="page slot-board" aria-busy="@(_loading || _busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Slot negotiation</span>
            <h1>Slot proposals</h1>
            <p>Propose shared windows, accept with your headcount, and adjust capacity on confirmed slots.</p>
        </div>
    </div>

    @if (_error is not null)
    {
        <p class="banner error" role="alert">@_error</p>
    }

    @if (_board is null)
    {
        <div class="card loading-block" aria-label="Loading slot board">
            <span class="loading-line loading-line-medium"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-short"></span>
        </div>
    }
    else
    {
        <section class="card" aria-labelledby="propose-heading">
            <div class="card-heading">
                <h2 id="propose-heading">
                    Propose a new slot
                    <span class="tip" tabindex="0" role="note"
                          aria-label="A proposal only becomes a confirmed slot once every appointment type has accepted it."
                          data-tip="A proposal only becomes a confirmed slot once every appointment type has accepted it."></span>
                </h2>
            </div>
            <div class="card-body proposal-form">
                <div class="field">
                    <label for="new-slot-date">Date</label>
                    <InputDate id="new-slot-date" @bind-Value="_newDate" />
                    <span class="hint">Must be a future date.</span>
                </div>
                <div class="field">
                    <label for="new-slot-start">Start time</label>
                    <input id="new-slot-start" type="time" step="3600" @bind="_newStartTime" />
                    <span class="hint">Windows are fixed at four hours.</span>
                </div>
                <button class="button button-primary" @onclick="ProposeAsync" disabled="@_busy"
                        title="Sends the window to the other appointment types for acceptance.">
                    Submit proposal
                </button>
            </div>
        </section>

        <section class="card" aria-labelledby="open-heading">
            <div class="card-heading">
                <h2 id="open-heading">
                    Open proposals
                    <span class="tip" tabindex="0" role="note"
                          aria-label="Windows waiting on acceptances. Accept with the number of candidates you can handle; you can revise it until the slot confirms."
                          data-tip="Windows waiting on acceptances. Accept with the number of candidates you can handle; you can revise it until the slot confirms."></span>
                </h2>
            </div>
            @if (_board.OpenProposals.Count == 0)
            {
                <div class="empty-state">
                    <strong>No open proposals.</strong>
                    <p>Propose a slot above to start a negotiation.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Appointment types that have already accepted this window.">Accepted by</th>
                                <th scope="col" title="How many candidates your appointment type can see in this window.">My headcount</th>
                                <th scope="col" class="actions-column"><span class="muted">Actions</span></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var proposal in _board.OpenProposals)
                            {
                                <tr>
                                    <td data-label="Date">@proposal.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">
                                        @proposal.StartTime.ToString("HH\\:mm")-@proposal.EndTime.ToString("HH\\:mm")
                                    </td>
                                    <td data-label="Accepted by">
                                        <span class="chip-row">
                                            @foreach (var name in proposal.AcceptedByAppointmentTypeNames)
                                            {
                                                <span class="chip">@name</span>
                                            }
                                        </span>
                                    </td>
                                    <td data-label="My headcount">
                                        <input class="number-input"
                                               type="number"
                                               min="1"
                                               @bind="_headcounts[proposal.ProposalId]"
                                               title="Candidates your appointment type can take in this window."
                                               aria-label="@($"Headcount for {proposal.Date:yyyy-MM-dd}")" />
                                    </td>
                                    <td data-label="Actions">
                                        <span class="row-actions">
                                            <button class="button button-primary" @onclick="() => AcceptAsync(proposal.ProposalId)"
                                                    disabled="@_busy"
                                                    title="Records your acceptance with the headcount shown.">
                                                @(proposal.AcceptedByMe ? "Update acceptance" : "Accept")
                                            </button>
                                            @if (proposal.AcceptedByMe)
                                            {
                                                <button class="button button-quiet" @onclick="() => WithdrawAcceptanceAsync(proposal.ProposalId)"
                                                        disabled="@_busy"
                                                        title="Removes your acceptance. The proposal stays open for the others.">Withdraw acceptance</button>
                                            }
                                            @if (proposal.CreatedByMe)
                                            {
                                                <button class="button button-quiet" @onclick="() => WithdrawProposalAsync(proposal.ProposalId)"
                                                        disabled="@_busy"
                                                        title="Takes the whole proposal off the board for everyone.">Withdraw proposal</button>
                                            }
                                        </span>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </section>

        <section class="card" aria-labelledby="confirmed-heading">
            <div class="card-heading">
                <h2 id="confirmed-heading">
                    Confirmed slots (this type)
                    <span class="tip tip-end" tabindex="0" role="note"
                          aria-label="Confirmed windows candidates can book into. Lowering your headcount below the candidates already booked is refused."
                          data-tip="Confirmed windows candidates can book into. Lowering your headcount below the candidates already booked is refused."></span>
                </h2>
            </div>
            @if (_board.ConfirmedSlots.Count == 0)
            {
                <div class="empty-state">
                    <strong>No confirmed slots.</strong>
                    <p>Slots appear here once all three managers have accepted them.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Total candidates your appointment type will see in this window.">My total</th>
                                <th scope="col" title="Places left after the candidates already booked in.">Remaining</th>
                                <th scope="col" class="actions-column"><span class="muted">Actions</span></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var slot in _board.ConfirmedSlots)
                            {
                                <tr class="@(_cancelAwaitingConfirmation == slot.ConfirmedSlotId ? "awaiting-confirmation" : null)">
                                    <td data-label="Date">@slot.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">@slot.StartTime.ToString("HH\\:mm")-@slot.EndTime.ToString("HH\\:mm")</td>
                                    <td data-label="My total">
                                        <input class="number-input"
                                               type="number"
                                               min="1"
                                               @bind="_confirmedTotals[slot.ConfirmedSlotId]"
                                               title="Cannot go below the number of candidates already booked."
                                               aria-label="@($"Total headcount for {slot.Date:yyyy-MM-dd}")" />
                                    </td>
                                    <td data-label="Remaining">@slot.MyRemainingCapacity</td>
                                    <td data-label="Actions">
                                        <span class="row-actions">
                                            <button class="button" @onclick="() => AdjustCapacityAsync(slot.ConfirmedSlotId)"
                                                    disabled="@_busy"
                                                    title="Saves the total shown against this slot.">Adjust headcount</button>
                                            <button class="button button-danger" @onclick="() => CancelSlotAsync(slot.ConfirmedSlotId)"
                                                    disabled="@_busy"
                                                    title="Cancels the window for every appointment type. Booked candidates have to be re-invited.">
                                                @(_cancelAwaitingConfirmation == slot.ConfirmedSlotId ? "Confirm cancel" : "Cancel slot")
                                            </button>
                                        </span>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </section>
    }
</div>

@code {
    private SlotBoardDto? _board;
    private string? _error;
    private bool _busy;
    private bool _loading;

    private DateOnly _newDate = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
    private TimeOnly? _newStartTime = new(9, 0);

    private readonly Dictionary<Guid, int> _headcounts = [];
    private readonly Dictionary<Guid, int> _confirmedTotals = [];

    // Set when a cancel was refused pending confirmation; the next click on the same slot confirms.
    private Guid? _cancelAwaitingConfirmation;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        if (_loading || _busy)
        {
            return;
        }

        _loading = true;
        try
        {
            await ReloadBoardAsync();
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ProposeAsync()
    {
        if (_newStartTime is not { } startTime)
        {
            _error = "Enter a start time as HH:mm.";
            return;
        }

        await RunAsync(async () =>
        {
            var outcome = await SlotsApi.ProposeAsync(
                _newDate, startTime, CancellationToken.None);
            return AsBool(outcome);
        });
    }

    private Task AcceptAsync(Guid proposalId) =>
        RunAsync(() => SlotsApi.AcceptAsync(
            proposalId,
            _headcounts[proposalId],
            CancellationToken.None));

    private Task WithdrawAcceptanceAsync(Guid proposalId) =>
        RunAsync(() => SlotsApi.WithdrawAcceptanceAsync(proposalId, CancellationToken.None));

    private Task WithdrawProposalAsync(Guid proposalId) =>
        RunAsync(() => SlotsApi.WithdrawProposalAsync(proposalId, CancellationToken.None));

    private async Task AdjustCapacityAsync(Guid slotId)
    {
        if (_busy || _loading)
        {
            return;
        }

        _busy = true;
        try
        {
            var outcome = await SlotsApi.AdjustCapacityAsync(
                slotId,
                _confirmedTotals[slotId],
                CancellationToken.None);
            _error = outcome.ErrorMessage;
            if (outcome.IsSuccess)
            {
                await ReloadBoardAsync();
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task CancelSlotAsync(Guid slotId)
    {
        if (_busy || _loading)
        {
            return;
        }

        var confirm = _cancelAwaitingConfirmation == slotId;

        _busy = true;
        try
        {
            var outcome = await SlotsApi.CancelConfirmedSlotAsync(
                slotId, confirm, CancellationToken.None);

            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _cancelAwaitingConfirmation = slotId;
                _error = outcome.ErrorMessage + " Press Cancel slot again to confirm.";
                return;
            }

            _cancelAwaitingConfirmation = null;
            _error = outcome.ErrorMessage;
            await ReloadBoardAsync();
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunAsync(Func<Task<ApiOutcome<bool>>> action)
    {
        if (_busy || _loading)
        {
            return;
        }

        _busy = true;
        try
        {
            var outcome = await action();
            _error = outcome.ErrorMessage;
            await ReloadBoardAsync();
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ReloadBoardAsync()
    {
        var outcome = await SlotsApi.GetBoardAsync(CancellationToken.None);
        if (!outcome.IsSuccess || outcome.Value is null)
        {
            _error = outcome.ErrorMessage;
            return;
        }

        _board = outcome.Value;
        _error = null;
        _headcounts.Clear();
        _confirmedTotals.Clear();

        foreach (var proposal in _board.OpenProposals)
        {
            _headcounts[proposal.ProposalId] = proposal.MyAcceptedHeadcount ?? 1;
        }

        foreach (var slot in _board.ConfirmedSlots)
        {
            _confirmedTotals[slot.ConfirmedSlotId] = slot.MyHeadcount;
        }
    }

    private static ApiOutcome<bool> AsBool(ApiOutcome<Guid> outcome) =>
        outcome.IsSuccess
            ? ApiOutcome<bool>.Success(true, outcome.StatusCode)
            : ApiOutcome<bool>.Failure(outcome.ErrorMessage!, outcome.StatusCode);
}
`````

## after — src/EventBooking.Web/Pages/EventNegotiation.razor — 1/1

<!-- vocabulary-file: {"id":201,"oldPath":"src/EventBooking.Web/Pages/Slots.razor","newPath":"src/EventBooking.Web/Pages/EventNegotiation.razor","beforeSha":"066eae7b8f0b02f46e43b150af3b24d891047e5bbd4f52dd5d87111bfe2fd31e","afterSha":"976282f095b37c225f071070fd8ffa45a67383320277ae1df62f20793da86f62","side":"after","part":1,"parts":1} -->

`````razor
@page "/events/negotiate"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject EventsClient EventsApi

<PageTitle>Event proposals</PageTitle>

<div class="page event-board" aria-busy="@(_loading || _busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Event negotiation</span>
            <h1>Event proposals</h1>
            <p>Propose shared windows, accept with your headcount, and adjust capacity on events.</p>
        </div>
    </div>

    @if (_error is not null)
    {
        <p class="banner error" role="alert">@_error</p>
    }

    @if (_board is null)
    {
        <div class="card loading-block" aria-label="Loading event board">
            <span class="loading-line loading-line-medium"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-short"></span>
        </div>
    }
    else
    {
        <section class="card" aria-labelledby="propose-heading">
            <div class="card-heading">
                <h2 id="propose-heading">
                    Propose a new event
                    <span class="tip" tabindex="0" role="note"
                          aria-label="A proposal only becomes a event once every appointment type has accepted it."
                          data-tip="A proposal only becomes a event once every appointment type has accepted it."></span>
                </h2>
            </div>
            <div class="card-body proposal-form">
                <div class="field">
                    <label for="new-event-date">Date</label>
                    <InputDate id="new-event-date" @bind-Value="_newDate" />
                    <span class="hint">Must be a future date.</span>
                </div>
                <div class="field">
                    <label for="new-event-start">Start time</label>
                    <input id="new-event-start" type="time" step="3600" @bind="_newStartTime" />
                    <span class="hint">Windows are fixed at four hours.</span>
                </div>
                <button class="button button-primary" @onclick="ProposeAsync" disabled="@_busy"
                        title="Sends the window to the other appointment types for acceptance.">
                    Submit proposal
                </button>
            </div>
        </section>

        <section class="card" aria-labelledby="open-heading">
            <div class="card-heading">
                <h2 id="open-heading">
                    Open proposals
                    <span class="tip" tabindex="0" role="note"
                          aria-label="Windows waiting on acceptances. Accept with the number of attendees you can handle; you can revise it until the event confirms."
                          data-tip="Windows waiting on acceptances. Accept with the number of attendees you can handle; you can revise it until the event confirms."></span>
                </h2>
            </div>
            @if (_board.OpenProposals.Count == 0)
            {
                <div class="empty-state">
                    <strong>No open proposals.</strong>
                    <p>Propose a event above to start a negotiation.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Appointment types that have already accepted this window.">Accepted by</th>
                                <th scope="col" title="How many attendees your appointment type can see in this window.">My headcount</th>
                                <th scope="col" class="actions-column"><span class="muted">Actions</span></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var proposal in _board.OpenProposals)
                            {
                                <tr>
                                    <td data-label="Date">@proposal.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">
                                        @proposal.StartTime.ToString("HH\\:mm")-@proposal.EndTime.ToString("HH\\:mm")
                                    </td>
                                    <td data-label="Accepted by">
                                        <span class="chip-row">
                                            @foreach (var name in proposal.AcceptedByAppointmentTypeNames)
                                            {
                                                <span class="chip">@name</span>
                                            }
                                        </span>
                                    </td>
                                    <td data-label="My headcount">
                                        <input class="number-input"
                                               type="number"
                                               min="1"
                                               @bind="_headcounts[proposal.ProposalId]"
                                               title="Attendees your appointment type can take in this window."
                                               aria-label="@($"Headcount for {proposal.Date:yyyy-MM-dd}")" />
                                    </td>
                                    <td data-label="Actions">
                                        <span class="row-actions">
                                            <button class="button button-primary" @onclick="() => AcceptAsync(proposal.ProposalId)"
                                                    disabled="@_busy"
                                                    title="Records your acceptance with the headcount shown.">
                                                @(proposal.AcceptedByMe ? "Update acceptance" : "Accept")
                                            </button>
                                            @if (proposal.AcceptedByMe)
                                            {
                                                <button class="button button-quiet" @onclick="() => WithdrawAcceptanceAsync(proposal.ProposalId)"
                                                        disabled="@_busy"
                                                        title="Removes your acceptance. The proposal stays open for the others.">Withdraw acceptance</button>
                                            }
                                            @if (proposal.CreatedByMe)
                                            {
                                                <button class="button button-quiet" @onclick="() => WithdrawProposalAsync(proposal.ProposalId)"
                                                        disabled="@_busy"
                                                        title="Takes the whole proposal off the board for everyone.">Withdraw proposal</button>
                                            }
                                        </span>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </section>

        <section class="card" aria-labelledby="confirmed-heading">
            <div class="card-heading">
                <h2 id="confirmed-heading">
                    Events (this type)
                    <span class="tip tip-end" tabindex="0" role="note"
                          aria-label="Confirmed windows attendees can book into. Lowering your headcount below the attendees already booked is refused."
                          data-tip="Confirmed windows attendees can book into. Lowering your headcount below the attendees already booked is refused."></span>
                </h2>
            </div>
            @if (_board.Events.Count == 0)
            {
                <div class="empty-state">
                    <strong>No events.</strong>
                    <p>Events appear here once all three managers have accepted them.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Total attendees your appointment type will see in this window.">My total</th>
                                <th scope="col" title="Places left after the attendees already booked in.">Remaining</th>
                                <th scope="col" class="actions-column"><span class="muted">Actions</span></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var eventItem in _board.Events)
                            {
                                <tr class="@(_cancelAwaitingConfirmation == eventItem.EventId ? "awaiting-confirmation" : null)">
                                    <td data-label="Date">@eventItem.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">@eventItem.StartTime.ToString("HH\\:mm")-@eventItem.EndTime.ToString("HH\\:mm")</td>
                                    <td data-label="My total">
                                        <input class="number-input"
                                               type="number"
                                               min="1"
                                               @bind="_confirmedTotals[eventItem.EventId]"
                                               title="Cannot go below the number of attendees already booked."
                                               aria-label="@($"Total headcount for {eventItem.Date:yyyy-MM-dd}")" />
                                    </td>
                                    <td data-label="Remaining">@eventItem.MyRemainingCapacity</td>
                                    <td data-label="Actions">
                                        <span class="row-actions">
                                            <button class="button" @onclick="() => AdjustCapacityAsync(eventItem.EventId)"
                                                    disabled="@_busy"
                                                    title="Saves the total shown against this eventItem.">Adjust headcount</button>
                                            <button class="button button-danger" @onclick="() => CancelEventAsync(eventItem.EventId)"
                                                    disabled="@_busy"
                                                    title="Cancels the window for every appointment type. Booked attendees have to be re-invited.">
                                                @(_cancelAwaitingConfirmation == eventItem.EventId ? "Confirm cancel" : "Cancel event")
                                            </button>
                                        </span>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </section>
    }
</div>

@code {
    private EventBoardDto? _board;
    private string? _error;
    private bool _busy;
    private bool _loading;

    private DateOnly _newDate = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
    private TimeOnly? _newStartTime = new(9, 0);

    private readonly Dictionary<Guid, int> _headcounts = [];
    private readonly Dictionary<Guid, int> _confirmedTotals = [];

    // Set when a cancel was refused pending confirmation; the next click on the same event confirms.
    private Guid? _cancelAwaitingConfirmation;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        if (_loading || _busy)
        {
            return;
        }

        _loading = true;
        try
        {
            await ReloadBoardAsync();
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ProposeAsync()
    {
        if (_newStartTime is not { } startTime)
        {
            _error = "Enter a start time as HH:mm.";
            return;
        }

        await RunAsync(async () =>
        {
            var outcome = await EventsApi.ProposeAsync(
                _newDate, startTime, CancellationToken.None);
            return AsBool(outcome);
        });
    }

    private Task AcceptAsync(Guid proposalId) =>
        RunAsync(() => EventsApi.AcceptAsync(
            proposalId,
            _headcounts[proposalId],
            CancellationToken.None));

    private Task WithdrawAcceptanceAsync(Guid proposalId) =>
        RunAsync(() => EventsApi.WithdrawAcceptanceAsync(proposalId, CancellationToken.None));

    private Task WithdrawProposalAsync(Guid proposalId) =>
        RunAsync(() => EventsApi.WithdrawProposalAsync(proposalId, CancellationToken.None));

    private async Task AdjustCapacityAsync(Guid eventId)
    {
        if (_busy || _loading)
        {
            return;
        }

        _busy = true;
        try
        {
            var outcome = await EventsApi.AdjustCapacityAsync(
                eventId,
                _confirmedTotals[eventId],
                CancellationToken.None);
            _error = outcome.ErrorMessage;
            if (outcome.IsSuccess)
            {
                await ReloadBoardAsync();
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task CancelEventAsync(Guid eventId)
    {
        if (_busy || _loading)
        {
            return;
        }

        var confirm = _cancelAwaitingConfirmation == eventId;

        _busy = true;
        try
        {
            var outcome = await EventsApi.CancelEventAsync(
                eventId, confirm, CancellationToken.None);

            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _cancelAwaitingConfirmation = eventId;
                _error = outcome.ErrorMessage + " Press Cancel event again to confirm.";
                return;
            }

            _cancelAwaitingConfirmation = null;
            _error = outcome.ErrorMessage;
            await ReloadBoardAsync();
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunAsync(Func<Task<ApiOutcome<bool>>> action)
    {
        if (_busy || _loading)
        {
            return;
        }

        _busy = true;
        try
        {
            var outcome = await action();
            _error = outcome.ErrorMessage;
            await ReloadBoardAsync();
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ReloadBoardAsync()
    {
        var outcome = await EventsApi.GetBoardAsync(CancellationToken.None);
        if (!outcome.IsSuccess || outcome.Value is null)
        {
            _error = outcome.ErrorMessage;
            return;
        }

        _board = outcome.Value;
        _error = null;
        _headcounts.Clear();
        _confirmedTotals.Clear();

        foreach (var proposal in _board.OpenProposals)
        {
            _headcounts[proposal.ProposalId] = proposal.MyAcceptedHeadcount ?? 1;
        }

        foreach (var eventItem in _board.Events)
        {
            _confirmedTotals[eventItem.EventId] = eventItem.MyHeadcount;
        }
    }

    private static ApiOutcome<bool> AsBool(ApiOutcome<Guid> outcome) =>
        outcome.IsSuccess
            ? ApiOutcome<bool>.Success(true, outcome.StatusCode)
            : ApiOutcome<bool>.Failure(outcome.ErrorMessage!, outcome.StatusCode);
}
`````

## before — src/EventBooking.Web/Pages/Slots.razor.css — 1/1

<!-- vocabulary-file: {"id":202,"oldPath":"src/EventBooking.Web/Pages/Slots.razor.css","newPath":"src/EventBooking.Web/Pages/EventNegotiation.razor.css","beforeSha":"74d18a9347e037df14ce29b7c06670b728d12a1194f6c8ed025dace4afb1452a","afterSha":"74d18a9347e037df14ce29b7c06670b728d12a1194f6c8ed025dace4afb1452a","side":"before","part":1,"parts":1} -->

`````text
.proposal-form {
    align-items: flex-start;
    display: flex;
    flex-wrap: wrap;
    gap: 18px;
}

.proposal-form .button {
    margin-top: 19px;
}

@media (max-width: 760px) {
    .proposal-form {
        align-items: stretch;
        flex-direction: column;
        gap: 14px;
    }

    .proposal-form .button {
        margin-top: 0;
        width: 100%;
    }
}
`````

## after — src/EventBooking.Web/Pages/EventNegotiation.razor.css — 1/1

<!-- vocabulary-file: {"id":202,"oldPath":"src/EventBooking.Web/Pages/Slots.razor.css","newPath":"src/EventBooking.Web/Pages/EventNegotiation.razor.css","beforeSha":"74d18a9347e037df14ce29b7c06670b728d12a1194f6c8ed025dace4afb1452a","afterSha":"74d18a9347e037df14ce29b7c06670b728d12a1194f6c8ed025dace4afb1452a","side":"after","part":1,"parts":1} -->

`````text
.proposal-form {
    align-items: flex-start;
    display: flex;
    flex-wrap: wrap;
    gap: 18px;
}

.proposal-form .button {
    margin-top: 19px;
}

@media (max-width: 760px) {
    .proposal-form {
        align-items: stretch;
        flex-direction: column;
        gap: 14px;
    }

    .proposal-form .button {
        margin-top: 0;
        width: 100%;
    }
}
`````

## before — src/EventBooking.Web/Program.cs — 1/1

<!-- vocabulary-file: {"id":203,"oldPath":"src/EventBooking.Web/Program.cs","newPath":"src/EventBooking.Web/Program.cs","beforeSha":"ad6aaa5fa8d92230196d35a61d3dd8b4c929c44775f3bfc9264f432c2435ef1e","afterSha":"bfe363f47b4468b4f2fcd06fb4365eb0aa79800f33ba5a94d9352930c85b459c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");
var headOfficeTimeZoneId = builder.Configuration["HeadOfficeTimeZoneId"]
    ?? throw new InvalidOperationException("HeadOfficeTimeZoneId is not configured.");

string[] tokenScopes;
var authority = builder.Configuration["Auth:Local:Authority"]
    ?? throw new InvalidOperationException("Auth:Local:Authority is not configured.");
var clientId = builder.Configuration["Auth:Local:ClientId"] ?? "eventbooking-web";

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = authority;
    options.ProviderOptions.ClientId = clientId;
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.PostLogoutRedirectUri =
        builder.HostEnvironment.BaseAddress.TrimEnd('/') + "/authentication/logout-callback";
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
});

tokenScopes = ["openid", "profile"];

// The authorization message handler attaches the staff access token to every call to the API,
// and only to the API. It is a delegating handler with no transport of its own, so it must
// wrap the browser fetch handler explicitly — without an inner handler every call throws
// net_http_handler_not_assigned before leaving the page.
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    handler.ConfigureHandler([apiBaseUrl], tokenScopes);

    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<EventBooking.Web.Services.SlotsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.CandidatesClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AdminClient>();
builder.Services.AddScoped<EventBooking.Web.Services.StaffAccessClient>();
builder.Services.AddScoped<EventBooking.Web.Services.ConfirmedSlotsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.DashboardsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AuditClient>();
builder.Services.AddScoped<EventBooking.Web.Services.MeClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AppointmentsClient>();
builder.Services.AddSingleton(new EventBooking.Web.Services.HeadOfficeTimePresentation(headOfficeTimeZoneId));
builder.Services.AddSingleton(new EventBooking.Web.Services.HeadOfficePageClock(headOfficeTimeZoneId));

// Candidates authorise with the single-use token in their URL. This plain named client must never
// use AuthorizationMessageHandler, which would attach a staff access token and start sign-in.
builder.Services.AddHttpClient(EventBooking.Web.Services.BookingClient.ClientName, client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddScoped(sp => new EventBooking.Web.Services.BookingClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(EventBooking.Web.Services.BookingClient.ClientName)));
builder.Services.AddSingleton(new EventBooking.Web.Services.CandidatePageOptions(
    builder.Configuration["CoordinatorContact"] ?? "the recruitment team"));

await builder.Build().RunAsync();
`````

## after — src/EventBooking.Web/Program.cs — 1/1

<!-- vocabulary-file: {"id":203,"oldPath":"src/EventBooking.Web/Program.cs","newPath":"src/EventBooking.Web/Program.cs","beforeSha":"ad6aaa5fa8d92230196d35a61d3dd8b4c929c44775f3bfc9264f432c2435ef1e","afterSha":"bfe363f47b4468b4f2fcd06fb4365eb0aa79800f33ba5a94d9352930c85b459c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");
var transitionalLocationTimeZoneId = builder.Configuration["TransitionalLocationTimeZoneId"]
    ?? throw new InvalidOperationException("TransitionalLocationTimeZoneId is not configured.");

string[] tokenScopes;
var authority = builder.Configuration["Auth:Local:Authority"]
    ?? throw new InvalidOperationException("Auth:Local:Authority is not configured.");
var clientId = builder.Configuration["Auth:Local:ClientId"] ?? "eventbooking-web";

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = authority;
    options.ProviderOptions.ClientId = clientId;
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.PostLogoutRedirectUri =
        builder.HostEnvironment.BaseAddress.TrimEnd('/') + "/authentication/logout-callback";
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
});

tokenScopes = ["openid", "profile"];

// The authorization message handler attaches the staff access token to every call to the API,
// and only to the API. It is a delegating handler with no transport of its own, so it must
// wrap the browser fetch handler explicitly — without an inner handler every call throws
// net_http_handler_not_assigned before leaving the page.
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    handler.ConfigureHandler([apiBaseUrl], tokenScopes);

    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<EventBooking.Web.Services.EventsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AttendeesClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AdminClient>();
builder.Services.AddScoped<EventBooking.Web.Services.StaffAccessClient>();
builder.Services.AddScoped<EventBooking.Web.Services.EventOperationsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.DashboardsClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AuditClient>();
builder.Services.AddScoped<EventBooking.Web.Services.MeClient>();
builder.Services.AddScoped<EventBooking.Web.Services.AppointmentsClient>();
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationTimePresentation(transitionalLocationTimeZoneId));
builder.Services.AddSingleton(new EventBooking.Web.Services.TransitionalLocationPageClock(transitionalLocationTimeZoneId));

// Attendees authorise with the single-use token in their URL. This plain named client must never
// use AuthorizationMessageHandler, which would attach a staff access token and start sign-in.
builder.Services.AddHttpClient(EventBooking.Web.Services.BookingClient.ClientName, client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddScoped(sp => new EventBooking.Web.Services.BookingClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(EventBooking.Web.Services.BookingClient.ClientName)));
builder.Services.AddSingleton(new EventBooking.Web.Services.AttendeePageOptions(
    builder.Configuration["CoordinatorContact"] ?? "the recruitment team"));

await builder.Build().RunAsync();
`````

## before — src/EventBooking.Web/Services/AppointmentsClient.cs — 1/1

<!-- vocabulary-file: {"id":204,"oldPath":"src/EventBooking.Web/Services/AppointmentsClient.cs","newPath":"src/EventBooking.Web/Services/AppointmentsClient.cs","beforeSha":"cbdecefd2bf0b0e4f5ec7435f94349bec77b1c4e06dc2425a87790f278bd2694","afterSha":"b5fceaefbe29787d10f7bbfc1a5db7f06f5b79413a1a93af7f6e8bfb42634423","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCountsDto
{
    /// <summary>Gets candidates booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }

    /// <summary>Gets candidates checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }

    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }

    /// <summary>Gets candidates recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }
}

/// <summary>Describes one selectable active slot without candidate rows.</summary>
public sealed record AppointmentSlotSummaryDto
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
    public required AppointmentStatusCountsDto Counts { get; init; }
}

/// <summary>Returns the trusted appointment-type name and its selectable active slots.</summary>
public sealed record AppointmentWorkspaceSlotListDto
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }

    /// <summary>Gets current and upcoming active slots containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentSlotSummaryDto> Slots { get; init; }
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRowDto
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
}

/// <summary>Returns one scoped active slot and only its minimum-data operational rows.</summary>
public sealed record AppointmentSlotDetailDto
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
    public required IReadOnlyList<BookingAppointmentRowDto> Appointments { get; init; }
}

/// <summary>Returns only the changed appointment row state needed by the workspace.</summary>
public sealed record BookingAppointmentUpdateDto
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

    /// <summary>Gets the scoped current and upcoming slot list.</summary>
    public async Task<ApiOutcome<AppointmentWorkspaceSlotListDto>> ListSlotsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            "/api/appointment-workspace/slots", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentWorkspaceSlotListDto>(response, cancellationToken);
    }

    /// <summary>Gets one selected slot's scoped operational rows.</summary>
    public async Task<ApiOutcome<AppointmentSlotDetailDto>> GetSlotAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/slots/{confirmedSlotId}", cancellationToken);
        return await ApiCall.ReadAsync<AppointmentSlotDetailDto>(response, cancellationToken);
    }

    /// <summary>Downloads the scoped roster CSV for one selected slot.</summary>
    /// <param name="confirmedSlotId">The selected slot identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Raw CSV content plus the server-suggested filename, or the parsed failure.</returns>
    public async Task<ApiOutcome<RosterCsvDownload>> GetRosterAsync(
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/appointment-workspace/slots/{confirmedSlotId}/roster", cancellationToken);
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
`````
