# 00a — Port source 35 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Web/Pages/ManageBooking.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/ManageBooking.razor.css","encoding":"utf8","sha256":"a1d627d75e8cb741ec8c889c6b4c51ed6e1f7b7a7f8918b2c1546f76c4c09b10","parts":1,"part":1} -->

`````text
.manage-booking-page {
    margin: 0 auto;
    max-width: 580px;
    padding: 36px 28px 60px;
}

.manage-booking-page__intro {
    display: grid;
    gap: 8px;
    margin-bottom: 20px;
}

.manage-booking-page h1 {
    color: var(--ink-strong);
    font-size: 1.5rem;
    line-height: 1.2;
    margin: 0;
}

.manage-booking-page__lead,
.manage-booking-page__note {
    color: var(--sub);
    font-size: 0.875rem;
    line-height: 1.55;
    margin: 0;
}

.manage-booking-page__card,
.manage-booking-page__status {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    box-shadow: var(--shadow-sm);
}

.manage-booking-page__card {
    border-left: 5px solid var(--ba-speedmarque-red);
    display: grid;
    gap: 6px;
    padding: 20px;
}

.manage-booking-page__label {
    color: var(--sub);
    font-size: 0.6875rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
}

.manage-booking-page__window {
    color: var(--ink-strong);
    font-size: 1.0625rem;
    font-weight: 600;
    line-height: 1.45;
}

.manage-booking-page__actions {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
    margin-top: 20px;
}

.manage-booking-page__button {
    background: var(--surface);
    border: 1.5px solid var(--line-strong);
    border-radius: var(--radius-sm);
    color: var(--ink);
    cursor: pointer;
    font: inherit;
    font-size: 0.875rem;
    font-weight: 600;
    min-height: 46px;
    padding: 12px 20px;
    transition: background-color 120ms ease, border-color 120ms ease;
}

.manage-booking-page__button:hover:not(:disabled) {
    background: var(--surface-sunken);
}

.manage-booking-page__button--danger {
    border-color: var(--error-line);
    color: var(--ba-red-ink);
}

.manage-booking-page__button--danger:hover:not(:disabled) {
    background: var(--error-bg);
}

.manage-booking-page__button--primary {
    background: var(--accent);
    border-color: var(--accent);
    box-shadow: var(--shadow-sm);
    color: #fff;
}

.manage-booking-page__button--primary:hover:not(:disabled) {
    background: var(--accent-hover);
    border-color: var(--accent-hover);
}

.manage-booking-page__button:disabled {
    cursor: not-allowed;
    opacity: 0.55;
}

.manage-booking-page__banner {
    background: var(--error-bg);
    border: 1px solid var(--error-line);
    border-left-width: 5px;
    border-radius: var(--radius-sm);
    color: var(--error-ink);
    font-size: 0.8125rem;
    line-height: 1.5;
    margin: 0 0 18px;
    padding: 11px 14px;
}

.manage-booking-page__status {
    display: grid;
    gap: 10px;
    padding: 22px 20px;
}

.manage-booking-page__status--cancelled {
    border-color: var(--success-line);
    border-left: 5px solid var(--success-line);
}

.manage-booking-page__status--expired {
    margin-top: 40px;
    padding: 34px 22px;
    text-align: center;
}

.manage-booking-page__footnote {
    margin-top: 20px;
}

@media (max-width: 640px) {
    .manage-booking-page {
        padding: 26px 18px 44px;
    }

    .manage-booking-page__actions {
        display: grid;
    }

    .manage-booking-page__button {
        inline-size: 100%;
    }
}
`````

## src/EventBooking.Web/Pages/NotFound.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/NotFound.razor","encoding":"utf8","sha256":"2349c4ae572d6c38cd0c301664447bb0c7fa9602f57b7d42eefbb99b77da1437","parts":1,"part":1} -->

`````razor
@page "/not-found"
@layout MainLayout

<PageTitle>Page not found</PageTitle>

<div class="page">
    <div class="card">
        <div class="empty-state">
            <strong>Not found</strong>
            <p>Sorry, the content you are looking for does not exist.</p>
            <a class="button" href="/">Back to your workspace</a>
        </div>
    </div>
</div>
`````

## src/EventBooking.Web/Pages/Settings.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Settings.razor","encoding":"utf8","sha256":"a1149952c348f5f3e33f3ddfd6e020fa3980a3549abbc3170ec904f775e7eda1","parts":1,"part":1} -->

`````razor
@page "/settings"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AdminClient Admin

<PageTitle>System settings</PageTitle>

<section class="page settings-page" aria-labelledby="settings-heading" aria-busy="@(_isLoading || _busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Administration</span>
            <h1 id="settings-heading">System settings</h1>
            <p>Fixed appointment types and invitation timing.</p>
        </div>
    </div>

    @if (_error is not null)
    {
        <div class="banner error" role="alert">
            <strong>@(_settings is null ? "Couldn’t load settings." : "Couldn’t refresh settings.")</strong>
            @_error
        </div>
    }

    @if (_saved)
    {
        <div class="banner saved" role="status">
            <strong>Saved.</strong> These changes apply to invites created from now on.
        </div>
    }

    @if (_isLoading && _settings is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
            <span class="visually-hidden">Loading system settings…</span>
        </div>
    }
    else if (_settings is null)
    {
        <div class="card">
            <div class="empty-state">
                <strong>Settings could not be loaded.</strong>
                <p>Try again to fetch the current appointment types and invite timing.</p>
                <button class="button" @onclick="ReloadPageAsync" disabled="@(_isLoading || _busy)">Try again</button>
            </div>
        </div>
    }
    else
    {
        <section class="card" aria-labelledby="types-heading">
            <div class="card-heading">
                <h2 id="types-heading">
                    Fixed appointment types
                    <span class="tip" tabindex="0" role="note"
                          aria-label="The three appointment types are fixed by the system. Only the manager assignment changes."
                          data-tip="The three appointment types are fixed by the system. Only the manager assignment changes."></span>
                </h2>
            </div>
            <div class="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Appointment type</th>
                            <th scope="col" title="The staff account that negotiates slots for this appointment type.">Manager identifier</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var type in _settings.AppointmentTypes)
                        {
                            <tr @key="type.Id">
                                <td data-label="Appointment type">
                                    <strong>@type.Name</strong>
                                    <span class="type-code">@type.Code</span>
                                </td>
                                <td data-label="Manager identifier">
                                    @ManagerLabel(type)
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </section>

        <section class="card" aria-labelledby="invite-timing-heading">
            <div class="card-heading">
                <h2 id="invite-timing-heading">
                    Invite timing
                    <span class="tip" tabindex="0" role="note"
                          aria-label="These two values shape how long a candidate has to respond and how hard the system chases them."
                          data-tip="These two values shape how long a candidate has to respond and how hard the system chases them."></span>
                </h2>
            </div>
            <div class="card-body settings-form-row">
                <div class="field">
                    <label for="invite-expiry-days">Invite expiry window (days)</label>
                    <input id="invite-expiry-days" class="number-input" type="number" min="1" @bind="_inviteExpiryDays" disabled="@_busy" />
                    <span class="hint">How long a booking link stays usable.</span>
                </div>
                <div class="field">
                    <label for="max-auto-retries">Max auto-retry count</label>
                    <input id="max-auto-retries" class="number-input" type="number" min="0" @bind="_maxAutoRetryCount" disabled="@_busy" />
                    <span class="hint">Automatic re-invites for an unanswered invitation before a human is asked to step in.</span>
                </div>
                <button class="button button-primary" @onclick="SaveAsync" disabled="@_busy"
                        title="Applies to invites created after you save.">Save changes</button>
            </div>
            <p class="callout settings-callout">
                <strong>Applies going forward:</strong> a change here governs invites created after you save — it does not reach back and change invites already sent.
            </p>
        </section>

    }
</section>

@code {
    // The staff number stays visible next to the name: it is the identifier an admin acts on.
    private static string ManagerLabel(AppointmentTypeDto type) => type switch
    {
        { ManagerDisplayName: not null, ManagerStaffId: not null } =>
            $"{type.ManagerDisplayName} ({type.ManagerStaffId})",
        { ManagerStaffId: not null } => type.ManagerStaffId,
        { ManagerUserId: null } => "Unassigned",
        _ => "Assigned (pending identity sync)",
    };

    private const string UnexpectedError = "Something went wrong. Please try again.";

    private SettingsDto? _settings;
    private string? _error;
    private bool _busy;
    private bool _isLoading;
    private bool _saved;
    private int _inviteExpiryDays;
    private int _maxAutoRetryCount;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadPageAsync()
    {
        await ReloadAsync();
    }

    private Task<bool> ReloadAsync()
    {
        if (_isLoading || _busy)
        {
            return Task.FromResult(false);
        }

        return ReloadCoreAsync();
    }

    private async Task<bool> ReloadCoreAsync()
    {
        _isLoading = true;
        try
        {
            var outcome = await Admin.GetAsync(CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _settings = outcome.Value;
                _inviteExpiryDays = _settings.InviteExpiryDays;
                _maxAutoRetryCount = _settings.MaxAutoRetryCount;
                _error = null;
                return true;
            }

            _error = outcome.ErrorMessage ?? UnexpectedError;
            return false;
        }
        catch (Exception)
        {
            _error = UnexpectedError;
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_busy || _isLoading)
        {
            return;
        }

        _saved = false;
        _error = null;
        _busy = true;
        try
        {
            var outcome = await Admin.UpdateAsync(
                _inviteExpiryDays,
                _maxAutoRetryCount,
                CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _saved = true;
            await ReloadCoreAsync();
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }
    }
}
`````

## src/EventBooking.Web/Pages/Settings.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Settings.razor.css","encoding":"utf8","sha256":"d36c16dbc9ceb3f63844e85095f72c777c7bec9e0c8e336cdc6836588ca837ed","parts":1,"part":1} -->

`````text
.type-code {
    background: var(--accent-soft);
    border-radius: 999px;
    color: var(--accent);
    display: inline-block;
    font-size: 0.6875rem;
    font-weight: 700;
    margin-left: 7px;
    padding: 3px 7px;
}

td:first-child strong {
    color: var(--ink-strong);
}

.settings-form-row {
    align-items: flex-start;
    display: flex;
    flex-wrap: wrap;
    gap: 16px 20px;
}

.settings-form-row .button {
    margin-top: 19px;
}

.settings-callout {
    margin: 0 20px 18px;
}

@media (max-width: 760px) {
    .settings-form-row {
        align-items: stretch;
        flex-direction: column;
    }

    .settings-form-row .button {
        margin-top: 0;
        width: 100%;
    }

    .settings-callout {
        margin: 0 14px 16px;
    }
}
`````

## src/EventBooking.Web/Pages/Slots.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Slots.razor","encoding":"utf8","sha256":"066eae7b8f0b02f46e43b150af3b24d891047e5bbd4f52dd5d87111bfe2fd31e","parts":1,"part":1} -->

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

## src/EventBooking.Web/Pages/Slots.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Slots.razor.css","encoding":"utf8","sha256":"74d18a9347e037df14ce29b7c06670b728d12a1194f6c8ed025dace4afb1452a","parts":1,"part":1} -->

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

## src/EventBooking.Web/Pages/StaffAccess.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/StaffAccess.razor","encoding":"utf8","sha256":"1979764b46fa65e8401026e3497ffd55fe5a9db472f29ac8ed18636541d39bf8","parts":1,"part":1} -->

`````razor
@page "/staff-access"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject StaffAccessClient StaffAccessApi
@inject AdminClient Admin

<PageTitle>Staff access</PageTitle>

<section class="page staff-access-page" aria-labelledby="staff-access-heading">
    <header class="page-header">
        <div>
            <span class="eyebrow">Administration</span>
            <h1 id="staff-access-heading">Staff access</h1>
            <p>Appointment-type scope for staff whose roles are assigned through the identity provider.</p>
        </div>
    </header>

    @if (_error is not null)
    {
        <div class="banner error" role="alert">@_error</div>
    }
    @if (_success is not null)
    {
        <div class="banner saved" role="status">@_success</div>
    }

    @if (_profiles is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="visually-hidden">Loading staff access…</span>
        </div>
    }
    else if (_profiles.Count == 0)
    {
        <div class="card">
            <div class="empty-state">
                <strong>No one has signed in with a EventBooking role yet.</strong>
                <p>Profiles appear here automatically the first time a staff member with an assigned role signs in.</p>
            </div>
        </div>
    }
    else
    {
        <div class="card">
            <div class="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th scope="col" title="The staff number this identity signed in with.">Staff number</th>
                            <th scope="col" title="Assigned through the identity provider. Read-only here.">Roles</th>
                            <th scope="col" title="Only Manager and Appointment staff use a single appointment-type scope.">Appointment type</th>
                            <th scope="col">Action</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var profile in _profiles)
                        {
                            <tr @key="profile.StaffUserId">
                                <td data-label="Staff number">@IdentityLabel(profile)</td>
                                <td data-label="Roles">
                                    <span class="chip-row">
                                        @foreach (var role in profile.Roles)
                                        {
                                            <span class="role-chip">@DisplayRole(role)</span>
                                        }
                                    </span>
                                </td>
                                <td data-label="Appointment type">
                                    @if (profile.AppointmentTypeName is not null)
                                    {
                                        @profile.AppointmentTypeName
                                    }
                                    else if (NeedsScope(profile))
                                    {
                                        <span class="awaiting-scope-badge">Awaiting appointment-type assignment</span>
                                    }
                                    else
                                    {
                                        <text>—</text>
                                    }
                                </td>
                                <td data-label="Action">
                                    @if (NeedsScope(profile))
                                    {
                                        <button class="button button-small edit-profile"
                                                @onclick="() => Edit(profile)"
                                                disabled="@_busy"
                                                title="Set or change this profile's appointment-type scope.">
                                            @(profile.AppointmentTypeId is null ? "Assign" : "Edit")
                                        </button>
                                    }
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </div>
    }

    @if (_editorOpen && _editingProfile is not null)
    {
        <section class="card editor-card" aria-labelledby="profile-editor-heading">
            <div class="card-heading">
                <h2 id="profile-editor-heading">
                    @(_editingProfile.AppointmentTypeId is null ? "Assign appointment type" : "Edit appointment type")
                </h2>
            </div>
            <div class="card-body editor-body">
                <div class="field">
                    <span class="field-label">Staff number</span>
                    <span>@IdentityLabel(_editingProfile)</span>
                </div>

                <div class="field">
                    <span class="field-label">Roles</span>
                    <span class="chip-row">
                        @foreach (var role in _editingProfile.Roles)
                        {
                            <span class="role-chip">@DisplayRole(role)</span>
                        }
                    </span>
                    <span class="hint">Assigned through the identity provider. Not editable here.</span>
                </div>

                <div class="field">
                    <label for="appointment-type">Appointment type</label>
                    <select id="appointment-type"
                            value="@_editAppointmentTypeId"
                            @onchange="OnAppointmentTypeChanged"
                            disabled="@_busy">
                        <option value="">Select appointment type</option>
                        @foreach (var type in _appointmentTypes)
                        {
                            <option value="@type.Id">@type.Name</option>
                        }
                    </select>
                </div>

                <div class="editor-actions">
                    <button class="button" @onclick="CloseEditor" disabled="@_busy">Cancel</button>
                    @if (_editingProfile.AppointmentTypeId is not null)
                    {
                        <button class="button button-danger clear-scope"
                                @onclick="ClearScopeAsync"
                                disabled="@_busy"
                                title="Clears this profile's appointment-type scope. The role stays assigned.">
                            Clear scope
                        </button>
                    }
                    <button class="button button-primary save-profile"
                            @onclick="SaveAsync"
                            disabled="@(_busy || _editAppointmentTypeId is null)">
                        Save appointment type
                    </button>
                </div>
            </div>
        </section>
    }
</section>

@code {
    // The staff number stays visible next to the name: it is the identifier an admin acts on.
    private static string IdentityLabel(StaffAccessProfileDto profile) => profile switch
    {
        { DisplayName: not null, StaffId: not null } => $"{profile.DisplayName} ({profile.StaffId})",
        { StaffId: not null } => profile.StaffId,
        _ => profile.StaffUserId.ToString(),
    };

    private const string UnexpectedError = "Something went wrong. Please try again.";

    private IReadOnlyList<StaffAccessProfileDto>? _profiles;
    private IReadOnlyList<AppointmentTypeDto> _appointmentTypes = [];
    private StaffAccessProfileDto? _editingProfile;
    private Guid? _editAppointmentTypeId;
    private bool _editorOpen;
    private bool _busy;
    private string? _error;
    private string? _success;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        var settings = await Admin.GetAsync(CancellationToken.None);
        if (settings.IsSuccess)
        {
            _appointmentTypes = settings.Value!.AppointmentTypes;
        }
        else
        {
            _error = settings.ErrorMessage;
        }
    }

    private static bool NeedsScope(StaffAccessProfileDto profile) =>
        profile.Roles.Contains("Manager") || profile.Roles.Contains("AppointmentStaff");

    private static string DisplayRole(string role) =>
        role == "AppointmentStaff" ? "Appointment staff" : role;

    private void Edit(StaffAccessProfileDto profile)
    {
        _editorOpen = true;
        _editingProfile = profile;
        _editAppointmentTypeId = profile.AppointmentTypeId;
        _error = null;
        _success = null;
    }

    private void OnAppointmentTypeChanged(ChangeEventArgs eventArgs)
    {
        _editAppointmentTypeId = Guid.TryParse(eventArgs.Value?.ToString(), out var id)
            ? id
            : null;
    }

    private void CloseEditor() => _editorOpen = false;

    private async Task ReloadAsync()
    {
        try
        {
            var outcome = await StaffAccessApi.ListAsync(CancellationToken.None);
            if (outcome.IsSuccess)
            {
                _profiles = outcome.Value!;
                return;
            }

            _error = outcome.ErrorMessage ?? UnexpectedError;
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
    }

    private async Task SaveAsync()
    {
        if (_editingProfile is null || _editAppointmentTypeId is null)
        {
            return;
        }

        _error = null;
        _success = null;
        _busy = true;
        try
        {
            var outcome = await StaffAccessApi.ReplaceScopeAsync(
                _editingProfile.StaffUserId,
                _editAppointmentTypeId,
                _editingProfile.Version,
                CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
                if (outcome.StatusCode == 409)
                {
                    await ReloadAsync();
                }
                return;
            }

            _success = outcome.Value!.FormerManagerStaffUserId is Guid former
                ? $"Appointment type saved. Manager scope was cleared for {former}."
                : "Appointment type saved.";
            await ReloadAsync();
            _editorOpen = false;
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ClearScopeAsync()
    {
        if (_editingProfile is null)
        {
            return;
        }

        _error = null;
        _success = null;
        _busy = true;
        try
        {
            var outcome = await StaffAccessApi.ClearScopeAsync(
                _editingProfile.StaffUserId, _editingProfile.Version, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
                if (outcome.StatusCode == 409)
                {
                    await ReloadAsync();
                }
                return;
            }

            _success = "Appointment-type scope cleared.";
            await ReloadAsync();
            _editorOpen = false;
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }
    }
}
`````

## src/EventBooking.Web/Pages/StaffAccess.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/StaffAccess.razor.css","encoding":"utf8","sha256":"1a4d412ff58fedabc08239a84169c0b3ee2febc2d5646bb7cca33db690aa8fba","parts":1,"part":1} -->

`````text
.staff-access-page {
    margin: 0 auto;
    max-width: 72rem;
    width: 100%;
}

.editor-card {
    border-left: 5px solid var(--accent);
}

.editor-body {
    display: grid;
    gap: 18px;
    max-width: 34rem;
}

.role-options {
    display: flex;
    flex-wrap: wrap;
    gap: 10px 20px;
    margin-top: 8px;
}

.role-option {
    align-items: center;
    display: flex;
    font-size: 0.8125rem;
    gap: 8px;
}

.editor-actions {
    display: flex;
    flex-wrap: wrap;
    gap: 10px;
}

.awaiting-scope-badge {
    border: 1px dashed var(--warning, #b45309);
    border-radius: 999px;
    color: var(--warning, #b45309);
    display: inline-block;
    font-size: 0.75rem;
    font-weight: 600;
    padding: 2px 10px;
    white-space: nowrap;
}

@media (max-width: 760px) {
    .editor-actions .button {
        flex: 1 1 auto;
    }
}
`````

## src/EventBooking.Web/Program.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Program.cs","encoding":"utf8","sha256":"ad6aaa5fa8d92230196d35a61d3dd8b4c929c44775f3bfc9264f432c2435ef1e","parts":1,"part":1} -->

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

## src/EventBooking.Web/Properties/AssemblyInfo.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Properties/AssemblyInfo.cs","encoding":"utf8","sha256":"74839a14b79406267f796b04580eb55214b3dc879d48008074f00cfe51303ff1","parts":1,"part":1} -->

`````csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EventBooking.Web.Tests")]
`````

## src/EventBooking.Web/Properties/launchSettings.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Properties/launchSettings.json","encoding":"utf8","sha256":"9a52d2e16f5ba50c9895cea230614c5ddcf0cf867541fd11e464cc2374c78964","parts":1,"part":1} -->

`````text
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
      "applicationUrl": "http://localhost:5002",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "inspectUri": "{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}",
      "applicationUrl": "https://localhost:5002;http://localhost:5082",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
`````
