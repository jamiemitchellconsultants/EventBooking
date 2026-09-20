# 00a — Port source 34 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Web/Pages/Candidates.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Candidates.razor.css","encoding":"utf8","sha256":"f8c0a9449bb237c2eda4c86967714374eb5c8090f27a8001d581dd172e82682a","parts":1,"part":1} -->

`````text
.candidates-toolbar {
    align-items: flex-end;
    display: flex;
    flex-wrap: wrap;
    gap: 10px;
}

.candidates-toolbar .field-label {
    display: flex;
    flex-direction: column;
    gap: 5px;
}

.search-field input {
    min-width: 240px;
}

/* The real file input is visually hidden, so its focus has to show on the label. */
.upload-button:focus-within {
    background: var(--accent-soft);
    border-color: var(--accent);
    color: var(--accent);
    outline: 3px solid var(--focus);
    outline-offset: 2px;
}

/* The first row is an inline "new candidate" form, not data. */
.candidate-add-row td {
    background: var(--surface-sunken);
}

.candidate-edit-row td {
    background: var(--accent-soft);
}

/* These rows mix single-line cells with the group select's extra chip-row beneath it;
   middle-aligning (the table default) would centre the shorter cells against that taller
   one and throw their inputs out of line with the select. */
.candidate-add-row td,
.candidate-edit-row td {
    vertical-align: top;
}

td input,
.group-select {
    width: 100%;
}

.group-preview {
    margin-top: 6px;
    min-height: 22px;
}

.invite-help {
    color: var(--sub);
    display: block;
    font-size: 0.6875rem;
    margin-top: 4px;
}

.readiness-badge {
    white-space: nowrap;
}

.readiness-detail {
    background: var(--surface-sunken);
    border: 1px solid var(--line);
    border-radius: var(--radius-sm);
    display: flex;
    flex-direction: column;
    font-size: 0.75rem;
    gap: 6px;
    margin-top: 6px;
    max-width: 240px;
    padding: 8px 10px;
}

.readiness-types {
    margin: 0 0 0 16px;
    padding: 0;
}

.readiness-loading {
    color: var(--sub);
    font-size: 0.75rem;
}

.readiness-error,
.recovery-error {
    color: var(--error-ink);
    display: block;
    font-size: 0.75rem;
}

.recovery-outcome {
    color: var(--success-ink);
    display: block;
    font-size: 0.75rem;
    font-weight: 600;
}

.import-errors {
    list-style: none;
    margin: 10px 0 0;
    padding: 0;
}

.import-errors li {
    border-top: 1px dashed currentcolor;
    padding: 6px 0 0;
}

@media (max-width: 760px) {
    .candidates-toolbar {
        align-items: stretch;
    }

    .search-field {
        flex: 1 1 100%;
    }

    .search-field input {
        min-width: 0;
    }

    .readiness-detail {
        max-width: none;
    }

    .candidate-add-row td,
    .candidate-edit-row td {
        background: none;
    }

    .candidate-add-row,
    .candidate-edit-row {
        background: var(--surface-sunken);
    }
}
`````

## src/EventBooking.Web/Pages/ConfirmedSlots.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/ConfirmedSlots.razor","encoding":"utf8","sha256":"558b10ad91b74e9ff664cb4f926acb764f4f707bc8e3bc0d194d5f4dff41ddc8","parts":1,"part":1} -->

`````razor
@page "/confirmed-slots"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject ConfirmedSlotsClient ConfirmedSlotsApi
@inject SlotsClient SlotsApi

<PageTitle>Confirmed slots</PageTitle>

<section class="page confirmed-slots-page" aria-labelledby="confirmed-slots-heading" aria-busy="@(_busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Bulk import</span>
            <h1 id="confirmed-slots-heading">Confirmed slots</h1>
            <p>Bring in windows that were already agreed away from the negotiation board.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-heading">
            <h2>
                Import already agreed slots
                <span class="tip" tabindex="0" role="note"
                      aria-label="Imported slots skip the negotiation board: each row lands as a confirmed window with the headcount given per appointment type."
                      data-tip="Imported slots skip the negotiation board: each row lands as a confirmed window with the headcount given per appointment type."></span>
            </h2>
        </div>
        <div class="card-body import-card">
            <p class="hint">
                Upload a CSV to add slots agreed outside the system. The required header is
                <code>date,startTime,DAT,MED,UNI</code>. The whole file is accepted or rejected.
            </p>
            <InputFile OnChange="OnSlotFileChosenAsync" accept=".csv" disabled="@_busy"
                       aria-label="Import confirmed slots CSV" />

            @if (_busy)
            {
                <p class="hint" role="status">Importing confirmed slots…</p>
            }
            @if (_error is not null)
            {
                <p class="banner error" role="alert">@_error</p>
            }
            @if (_importMessage is not null)
            {
                <p class="banner saved" role="status">@_importMessage</p>
            }
            @if (_importErrors.Count > 0)
            {
                <div class="banner error" role="alert">
                    <p><strong>Nothing was imported.</strong> Fix the file and try again.</p>
                    <ul class="import-errors">
                        @foreach (var error in _importErrors)
                        {
                            <li><strong>Line @error.LineNumber:</strong> @error.Message</li>
                        }
                    </ul>
                </div>
            }
        </div>
    </div>

    <div class="card">
        <div class="card-heading">
            <h2>
                Cancel a confirmed slot
                <span class="tip" tabindex="0" role="note"
                      aria-label="Cancelling a window releases every place it holds. Any candidate booked into it is notified and re-invited."
                      data-tip="Cancelling a window releases every place it holds. Any candidate booked into it is notified and re-invited."></span>
            </h2>
        </div>
        <div class="card-body">
            @if (_slotsLoading)
            {
                <p class="hint" role="status">Loading confirmed slots…</p>
            }
            else if (_slots is null || _slots.Count == 0)
            {
                <p class="hint" role="status">No confirmed slots yet.</p>
            }
            else
            {
                <div class="table-wrap">
                    <table id="confirmed-slot-operations">
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Places left over the total headcount each appointment type accepted.">Capacity by type</th>
                                <th scope="col" title="Candidates currently booked into this window.">Active bookings</th>
                                <th scope="col" class="actions-column">Cancel</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var slot in _slots)
                            {
                                <tr @key="slot.ConfirmedSlotId">
                                    <td data-label="Date">@slot.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">@slot.StartTime.ToString("HH\\:mm")–@slot.EndTime.ToString("HH\\:mm")</td>
                                    <td data-label="Capacity by type">
                                        <div class="chip-row">
                                            @foreach (var capacity in slot.Capacities)
                                            {
                                                <span class="chip">@capacity.Code @capacity.RemainingCapacity/@capacity.TotalHeadcount</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Active bookings">@slot.ActiveBookings</td>
                                    <td data-label="Cancel">
                                        <button class="button button-danger button-small"
                                                @onclick="() => CancelSlotAsync(slot.ConfirmedSlotId)" disabled="@_busy">
                                            @(_cancelAwaitingConfirmation == slot.ConfirmedSlotId ? "Confirm cancel" : "Cancel slot")
                                        </button>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }

            @if (_slotsError is not null)
            {
                <p class="banner error" role="alert">@_slotsError</p>
            }
        </div>
    </div>
</section>

@code {
    private bool _busy;
    private string? _error;
    private string? _importMessage;
    private List<SlotImportErrorDto> _importErrors = [];

    private List<SlotOperationDto>? _slots;
    private bool _slotsLoading = true;
    private string? _slotsError;
    private Guid? _cancelAwaitingConfirmation;

    protected override Task OnInitializedAsync() => ReloadSlotsAsync();

    internal Task ReloadSlotsForTestingAsync() => ReloadSlotsAsync();

    internal Task CancelSlotForTestingAsync(Guid slotId) => CancelSlotAsync(slotId);

    private async Task ReloadSlotsAsync()
    {
        _slotsLoading = true;
        try
        {
            var outcome = await SlotsApi.GetSlotOperationsAsync(CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _slotsError = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            _slots = outcome.Value.Slots.ToList();
            _slotsError = null;
        }
        catch (Exception)
        {
            _slotsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _slotsLoading = false;
            StateHasChanged();
        }
    }

    // Two-stage: the first click asks without authorizing the cascade, so a slot holding bookings
    // comes back 409 and the button becomes the confirmation.
    private async Task CancelSlotAsync(Guid slotId)
    {
        if (_busy)
        {
            return;
        }

        var confirm = _cancelAwaitingConfirmation == slotId;
        _busy = true;
        try
        {
            var outcome = await SlotsApi.CancelConfirmedSlotAsync(slotId, confirm, CancellationToken.None);
            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _cancelAwaitingConfirmation = slotId;
                _slotsError = $"{outcome.ErrorMessage} Press Confirm cancel to proceed.";
                return;
            }

            _cancelAwaitingConfirmation = null;
            _slotsError = outcome.ErrorMessage;
            if (outcome.IsSuccess)
            {
                await ReloadSlotsAsync();
            }
        }
        catch (Exception)
        {
            _slotsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private async Task OnSlotFileChosenAsync(InputFileChangeEventArgs args)
    {
        await ImportAsync(async () =>
        {
            using var stream = args.File.OpenReadStream(1024 * 1024);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        });
    }

    internal Task ImportCsvForTestingAsync(string csv) =>
        ImportAsync(() => Task.FromResult(csv));

    internal Task ImportFileForTestingAsync(InputFileChangeEventArgs args) =>
        OnSlotFileChosenAsync(args);

    private async Task ImportAsync(Func<Task<string>> readCsv)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _error = null;
        _importMessage = null;
        _importErrors = [];
        try
        {
            var csv = await readCsv();
            var outcome = await ConfirmedSlotsApi.ImportAsync(csv, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            if (outcome.Value!.Accepted)
            {
                _importMessage = $"{outcome.Value.ImportedCount} confirmed slots imported.";
            }
            else
            {
                _importErrors = outcome.Value.Errors.ToList();
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }
}
`````

## src/EventBooking.Web/Pages/ConfirmedSlots.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/ConfirmedSlots.razor.css","encoding":"utf8","sha256":"bb0574d1ef0dc4b7419a7524e2b20e6943c3dc5ab52e272f55a6075761260d4f","parts":1,"part":1} -->

`````text
.confirmed-slots-page {
    margin: 0 auto;
    max-width: 60rem;
    width: 100%;
}

.import-card {
    display: grid;
    gap: 14px;
}

.import-card p {
    margin: 0;
}

.import-errors {
    list-style: none;
    margin: 8px 0 0;
    padding: 0;
}

.import-errors li {
    border-top: 1px dashed currentcolor;
    padding: 6px 0 0;
}
`````

## src/EventBooking.Web/Pages/Dashboards.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Dashboards.razor","encoding":"utf8","sha256":"72d1fc0cc40a78f9098598a50bd2000dc8a28d178a904c2cb1c2177c25b2bc92","parts":1,"part":1} -->

`````razor
@page "/dashboards"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject DashboardsClient DashboardsApi
@inject CandidatesClient CandidatesApi

<PageTitle>Dashboards</PageTitle>

<section class="page dashboards-page" aria-labelledby="dashboards-heading" aria-busy="@(_isLoading || _busy)">
    <div class="page-header">
        <div>
            <span class="eyebrow">Coordination</span>
            <h1 id="dashboards-heading">Dashboards</h1>
            <p>Read-only views of current availability, follow-ups, and slots.</p>
        </div>
    </div>

    @if (_error is not null)
    {
        <div class="banner error dashboard-banner" role="alert">
            @_error
            @if (!_isLoading)
            {
                <button class="button button-quiet button-small" @onclick="ReloadAsync" disabled="@_busy">Try again</button>
            }
        </div>
    }

    @if (_isLoading && _data is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
            <span class="visually-hidden">Loading dashboards…</span>
        </div>
    }
    else if (_data is null)
    {
        <div class="card">
            <div class="empty-state">
                <strong>Couldn’t load dashboards</strong>
                <p>Try again to load the latest information.</p>
                <button class="button" @onclick="ReloadAsync">Try again</button>
            </div>
        </div>
    }
    else
    {
        <div class="dashboard-tabs-row">
            <div class="dashboard-tabs" role="tablist" aria-label="Dashboard views" @onkeydown="OnTabListKeyDownAsync">
                <button @ref="_awaitingTabRef" id="awaiting-tab" class="@TabClass(Tab.Awaiting)" role="tab"
                        aria-selected="@AriaSelected(Tab.Awaiting)" aria-controls="dashboard-panel"
                        tabindex="@TabIndexFor(Tab.Awaiting)"
                        @onclick="() => SelectTab(Tab.Awaiting)" disabled="@_busy">
                    Awaiting availability <span class="tab-count">(@_data.AwaitingAvailability.Count)</span>
                </button>
                <button @ref="_noResponseTabRef" id="no-response-tab" class="@TabClass(Tab.NoResponse)" role="tab"
                        aria-selected="@AriaSelected(Tab.NoResponse)" aria-controls="dashboard-panel"
                        tabindex="@TabIndexFor(Tab.NoResponse)"
                        @onclick="() => SelectTab(Tab.NoResponse)" disabled="@_busy">
                    No response <span class="tab-count">(@_data.NoResponse.Count)</span>
                </button>
                <button @ref="_slotsTabRef" id="slots-tab" class="@TabClass(Tab.Slots)" role="tab"
                        aria-selected="@AriaSelected(Tab.Slots)" aria-controls="dashboard-panel"
                        tabindex="@TabIndexFor(Tab.Slots)"
                        @onclick="() => SelectTab(Tab.Slots)" disabled="@_busy">
                    Slots <span class="tab-count">(@_data.Slots.Count)</span>
                </button>
                @{
                    var failedCount = _data.EmailStatuses.Count(e => e.Status == "Failed");
                    var pendingCount = _data.EmailStatuses.Count(e => e.Status == "Pending");
                }
                @if (failedCount > 0)
                {
                    <span class="error">@failedCount failed email@(failedCount == 1 ? "" : "s")</span>
                }
                @if (pendingCount > 0)
                {
                    <span class="warning">@pendingCount pending email@(pendingCount == 1 ? "" : "s")</span>
                }
            </div>
        </div>

        <div id="dashboard-panel" class="card dashboard-card" role="tabpanel" aria-labelledby="@ActiveTabId">
            @if (_tab == Tab.Awaiting)
            {
                @if (_data.AwaitingAvailability.Count == 0)
                {
                    <div class="empty-state">
                        <strong>Nobody is waiting</strong>
                        <p>Every candidate either has an invitation or is ready for follow-up.</p>
                    </div>
                }
                else
                {
                    <div class="table-wrap">
                        <table>
                            <thead>
                                <tr><th scope="col">Candidate</th><th scope="col" title="The appointment types this candidate's employee group requires.">Required types</th><th scope="col" title="When the candidate first became available to invite.">Waiting since</th><th scope="col" title="Every recorded change for this candidate, with who made it.">History</th></tr>
                            </thead>
                            <tbody>
                                @foreach (var row in _data.AwaitingAvailability)
                                {
                                    <tr @key="row.CandidateId">
                                        <td data-label="Candidate"><strong>@row.Name</strong><a href="mailto:@row.Email">@row.Email</a></td>
                                        <td data-label="Required types">
                                            <div class="chip-row">
                                                @foreach (var requiredCode in row.RequiredCodes)
                                                {
                                                    <span class="chip">@requiredCode</span>
                                                }
                                            </div>
                                        </td>
                                        <td data-label="Waiting since">@row.WaitingSince.ToString("dd MMM yyyy") · @row.DaysWaiting day@(row.DaysWaiting == 1 ? string.Empty : "s")</td>
                                        <td data-label="History"><AuditHistory CandidateId="@row.CandidateId" /></td>
                                    </tr>
                                }
                            </tbody>
                        </table>
                    </div>
                }
            }
            else if (_tab == Tab.NoResponse)
            {
                @if (_data.NoResponse.Count == 0)
                {
                    <div class="empty-state">
                        <strong>Nobody is stuck</strong>
                        <p>Every invited candidate has responded or is still within the follow-up window.</p>
                    </div>
                }
                else
                {
                    <div class="table-wrap">
                        <table>
                            <thead>
                                <tr><th scope="col">Candidate</th><th scope="col" title="The appointment types this candidate's employee group requires.">Required types</th><th scope="col" title="When the invitation stopped being chased.">Gave up on</th><th scope="col" title="Every recorded change for this candidate, with who made it.">History</th><th scope="col" class="actions-column"><span class="visually-hidden">Actions</span></th></tr>
                            </thead>
                            <tbody>
                                @foreach (var row in _data.NoResponse)
                                {
                                    <tr @key="row.CandidateId">
                                        <td data-label="Candidate"><strong>@row.Name</strong><a href="mailto:@row.Email">@row.Email</a></td>
                                        <td data-label="Required types">
                                            <div class="chip-row">
                                                @foreach (var requiredCode in row.RequiredCodes)
                                                {
                                                    <span class="chip">@requiredCode</span>
                                                }
                                            </div>
                                        </td>
                                        <td data-label="Gave up on">@row.GaveUpOn.ToString("dd MMM yyyy")</td>
                                        <td data-label="History"><AuditHistory CandidateId="@row.CandidateId" /></td>
                                        <td data-label="Actions">
                                            <button class="button button-primary button-small" @onclick="() => ReinviteAsync(row.CandidateId)" disabled="@_busy"
                                                    title="Sends a fresh single-use booking link and restarts the follow-up window.">
                                                @(_busy ? "Re-inviting…" : "Re-invite now")
                                            </button>
                                        </td>
                                    </tr>
                                }
                            </tbody>
                        </table>
                    </div>
                }
            }
            else if (_data.Slots.Count == 0)
            {
                <div class="empty-state">
                    <strong>No confirmed slots</strong>
                    <p>Confirmed appointment windows will appear here once all appointment types have accepted them.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr><th scope="col">Date</th><th scope="col">Window</th><th scope="col" title="Places left over the total headcount each appointment type accepted.">Capacity by type</th><th scope="col" title="Candidates currently booked into this window.">Active bookings</th><th scope="col" title="Every recorded change to this slot, with who made it.">History</th></tr>
                        </thead>
                        <tbody>
                            @foreach (var slot in _data.Slots)
                            {
                                <tr @key="slot.ConfirmedSlotId">
                                    <td data-label="Date">@slot.Date.ToString("dd MMM yyyy")</td>
                                    <td data-label="Window">@FormatWindow(slot.StartTime, slot.EndTime)</td>
                                    <td data-label="Capacity by type">
                                        <div class="chip-row">
                                            @foreach (var capacity in slot.Capacities)
                                            {
                                                <span class="chip">@capacity.Code @capacity.RemainingCapacity/@capacity.TotalHeadcount</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Active bookings">@slot.ActiveBookings</td>
                                    <td data-label="History"><AuditHistory SlotId="@slot.ConfirmedSlotId" /></td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </div>
    }
</section>

@code {
    private const string UnexpectedError = "Something went wrong. Please try again.";

    private enum Tab { Awaiting, NoResponse, Slots }

    private static readonly Tab[] TabOrder = [Tab.Awaiting, Tab.NoResponse, Tab.Slots];

    private DashboardsDto? _data;
    private Tab _tab = Tab.Awaiting;
    private string? _error;
    private bool _busy;
    private bool _isLoading = true;

    private ElementReference _awaitingTabRef;
    private ElementReference _noResponseTabRef;
    private ElementReference _slotsTabRef;

    private string ActiveTabId => _tab switch
    {
        Tab.Awaiting => "awaiting-tab",
        Tab.NoResponse => "no-response-tab",
        _ => "slots-tab",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    private void SelectTab(Tab tab) => _tab = tab;

    private string AriaSelected(Tab tab) => _tab == tab ? "true" : "false";

    private ElementReference TabRefFor(Tab tab) => tab switch
    {
        Tab.Awaiting => _awaitingTabRef,
        Tab.NoResponse => _noResponseTabRef,
        _ => _slotsTabRef,
    };

    private int TabIndexFor(Tab tab) => _tab == tab ? 0 : -1;

    /// <summary>
    /// The WAI-ARIA tabs pattern: left/right (with wraparound) and home/end move both the selection
    /// and keyboard focus together, so a screen reader user never lands on a tab that isn't active.
    /// </summary>
    private async Task OnTabListKeyDownAsync(KeyboardEventArgs e)
    {
        var currentIndex = Array.IndexOf(TabOrder, _tab);
        var newIndex = e.Key switch
        {
            "ArrowRight" => (currentIndex + 1) % TabOrder.Length,
            "ArrowLeft" => (currentIndex - 1 + TabOrder.Length) % TabOrder.Length,
            "Home" => 0,
            "End" => TabOrder.Length - 1,
            _ => currentIndex,
        };

        if (newIndex == currentIndex)
        {
            return;
        }

        _tab = TabOrder[newIndex];
        StateHasChanged();
        await TabRefFor(_tab).FocusAsync();
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;

        try
        {
            var outcome = await DashboardsApi.GetAsync(CancellationToken.None);
            if (outcome.IsSuccess)
            {
                _data = outcome.Value;
                _error = null;
            }
            else
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
            }
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task ReinviteAsync(Guid candidateId)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            var outcome = await CandidatesApi.TriggerInviteAsync(candidateId, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            await ReloadAsync();
            if (_error is not null)
            {
                // The invite itself succeeded; do not let the refresh's own failure read as if the
                // invite had failed.
                _error = $"Invite sent, but the dashboard could not refresh: {_error}";
            }
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

    private string TabClass(Tab tab) => _tab == tab ? "dashboard-tab selected" : "dashboard-tab";

    private static string FormatWindow(TimeOnly startTime, TimeOnly endTime) =>
        $"{startTime:HH\\:mm}–{endTime:HH\\:mm}";
}
`````

## src/EventBooking.Web/Pages/Dashboards.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Dashboards.razor.css","encoding":"utf8","sha256":"2cffe42669ad5cb33d789762c5f75252162fdb1ddd5dc0d935ce6d595fc4f9da","parts":1,"part":1} -->

`````text
.dashboard-tabs-row {
    align-items: center;
    display: flex;
    justify-content: space-between;
}

.dashboard-tabs {
    align-items: center;
    border-bottom: 1.5px solid var(--line);
    display: flex;
    gap: 4px;
    width: 100%;
}

.dashboard-tab {
    background: transparent;
    border: 0;
    border-bottom: 3px solid transparent;
    color: var(--sub);
    cursor: pointer;
    font: inherit;
    font-size: 0.8125rem;
    font-weight: 600;
    margin-bottom: -1.5px;
    padding: 10px 14px;
}

.dashboard-tab:hover:not(:disabled) {
    color: var(--ink);
}

.dashboard-tab.selected {
    border-bottom-color: var(--ba-speedmarque-red);
    color: var(--accent);
}

.dashboard-tab:disabled {
    cursor: wait;
    opacity: 0.65;
}

.tab-count {
    font-weight: 400;
}

.dashboard-tabs .error,
.dashboard-tabs .warning {
    font-size: 0.75rem;
    margin-left: auto;
    padding-left: 12px;
}

.dashboard-banner {
    align-items: center;
    display: flex;
    gap: 12px;
    justify-content: space-between;
}

.dashboard-card {
    padding: 4px 20px;
}

td strong,
td a {
    display: block;
}

td a {
    color: var(--sub);
    font-size: 0.75rem;
    margin-top: 2px;
}

.actions-column {
    min-width: 140px;
}

@media (max-width: 760px) {
    .dashboard-card {
        padding: 4px 12px;
    }

    .dashboard-tabs {
        overflow-x: auto;
    }

    .dashboard-tab {
        flex: 0 0 auto;
        padding-inline: 10px;
    }

    td:first-child {
        align-items: flex-end;
    }

    td:first-child strong,
    td:first-child a {
        text-align: right;
    }
}
`````

## src/EventBooking.Web/Pages/Help.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Help.razor","encoding":"utf8","sha256":"03a75e6f69aa1300ab3f26ee49479e3846acf417d66e1b0bd2f4a30c55384a1b","parts":1,"part":1} -->

`````razor
@page "/help"
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services

<PageTitle>Help</PageTitle>

<AuthorizeView>
    <Authorized>
        <section class="page help-page" aria-labelledby="help-heading" aria-busy="@(Loading ? "true" : "false")">
            <div class="page-header">
                <div>
                    <span class="eyebrow">User guide</span>
                    <h1 id="help-heading">Help</h1>
                    <p>Guidance for the roles on your profile.</p>
                </div>
            </div>

            @if (Loading)
            {
                <div class="card loading-block" role="status">
                    <span class="loading-line loading-line-short"></span>
                    <span class="loading-line"></span>
                    <span class="loading-line loading-line-medium"></span>
                    <span class="visually-hidden">Loading…</span>
                </div>
            }
            else if (Error is not null)
            {
                <p class="landing-hint">@Error</p>
            }
            else if (Guides.Count == 0)
            {
                <p class="landing-hint">
                    Your account has not been assigned a role yet. Contact an administrator to be given
                    access as Manager, Coordinator, Admin, or Appointment staff.
                </p>
            }
            else
            {
                @if (Guides.Count > 1)
                {
                    <nav class="card guide-toc" aria-label="Guides on this page">
                        <ul>
                            @foreach (var guide in Guides)
                            {
                                <li><a href="/help#@guide.AnchorId">@guide.Title</a></li>
                            }
                        </ul>
                    </nav>
                }

                @foreach (var guide in Guides)
                {
                    <section class="card guide-section" id="@guide.AnchorId" aria-label="@guide.Title">
                        @((MarkupString)guide.Html)
                    </section>
                }
            }
        </section>
    </Authorized>
    <NotAuthorized>
        <section class="page help-page" aria-labelledby="help-heading">
            <div class="page-header">
                <div>
                    <span class="eyebrow">User guide</span>
                    <h1 id="help-heading">Help</h1>
                    <p>Guidance for booking and managing your appointment.</p>
                </div>
            </div>
            <section class="card guide-section" id="@CandidateGuide.AnchorId" aria-label="@CandidateGuide.Title">
                @((MarkupString)CandidateGuide.Html)
            </section>
        </section>
    </NotAuthorized>
</AuthorizeView>

@code {
    // Cascaded by MainLayout, which fetches it once for both the nav bar and Home — reused here
    // rather than issued as a second concurrent call to the same token-protected endpoint.
    [CascadingParameter]
    private ApiOutcome<MeDto>? MeOutcome { get; set; }

    private static readonly UserGuide CandidateGuide = UserGuideCatalog.CandidateGuide();

    private bool Loading => MeOutcome is null;
    private string? Error => MeOutcome is { IsSuccess: false } ? MeOutcome.ErrorMessage : null;
    private IReadOnlyList<UserGuide> Guides =>
        MeOutcome?.Value is { } me ? UserGuideCatalog.GuidesFor(me.Roles) : [];
}
`````

## src/EventBooking.Web/Pages/Home.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Home.razor","encoding":"utf8","sha256":"04fb6736a76dda6d6d8e9440dfad1b5b859111c802ba94e1ad6255d3bef593f1","parts":1,"part":1} -->

`````razor
@page "/"
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services

<PageTitle>EventBooking</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="landing">
            <div class="landing-intro">
                <span class="eyebrow">British Airways staff workspace</span>
                <h1>Hi @context.User.Identity?.Name</h1>
                <p>Everything below is scoped to the roles you hold. Pick a workspace to carry on.</p>
            </div>

            @if (Loading)
            {
                <div class="card loading-block" role="status">
                    <span class="loading-line loading-line-short"></span>
                    <span class="loading-line"></span>
                    <span class="loading-line loading-line-medium"></span>
                    <span class="visually-hidden">Loading…</span>
                </div>
            }
            else if (Error is not null)
            {
                <p class="landing-hint">@Error</p>
            }
            else if (Me is null || Me.Roles.Count == 0)
            {
                <p class="landing-hint">
                    Your account has not been assigned a role yet. Contact an administrator to be given
                    access as Manager, Coordinator, Admin, or Appointment staff.
                </p>
            }
            else
            {
                <div class="access-summary" aria-label="Your access">
                    <p>
                        Roles: @string.Join(", ", Me.Roles)
                        @if (Me.AppointmentTypeName is not null)
                        {
                            <span> · Appointment type: @Me.AppointmentTypeName</span>
                        }
                        <span class="tip" tabindex="0" role="note"
                              aria-label="Your roles decide which workspaces appear below. An appointment type scopes what you see to that one type of appointment."
                              data-tip="Your roles decide which workspaces appear below. An appointment type scopes what you see to that one type of appointment."></span>
                    </p>
                </div>

                @if (StaffNavigation.LinksFor(Me).Count == 0)
                {
                    <p class="landing-hint">
                        Your appointment workspace will appear here when appointment delivery is enabled.
                    </p>
                }
                else
                {
                    <nav class="landing-links" aria-label="Workspace sections">
                        @foreach (var link in StaffNavigation.LinksFor(Me))
                        {
                            <a class="link-card" href="@link.Href">
                                <span>@link.Label<span class="landing-sub">@link.Description</span></span>
                                <span class="arrow" aria-hidden="true">→</span>
                            </a>
                        }
                        <a class="link-card" href="/help">
                            <span>Help<span class="landing-sub">Read the guide for your role</span></span>
                            <span class="arrow" aria-hidden="true">→</span>
                        </a>
                    </nav>
                }
            }
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="landing landing-signed-out">
            <div class="landing-intro">
                <span class="eyebrow">British Airways staff workspace</span>
                <h1>EventBooking</h1>
                <p>Coordinate candidate appointments across drug &amp; alcohol testing, medical check-ups and uniform fittings — without overbooking anyone.</p>
                <a class="sign-in-button" href="authentication/login">Sign in</a>
                <p class="hint">Candidates do not sign in — they use the personal booking link emailed to them.</p>
                <p class="hint"><a href="/help">Read the candidate guide</a></p>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    // Fetched once by MainLayout and cascaded here, rather than fetched again by this page —
    // two concurrent calls to the same token-protected endpoint on first load raced the WASM
    // auth token acquisition and intermittently failed one of them.
    [CascadingParameter]
    private ApiOutcome<MeDto>? MeOutcome { get; set; }

    private bool Loading => MeOutcome is null;
    private string? Error => MeOutcome is { IsSuccess: false } ? MeOutcome.ErrorMessage : null;
    private MeDto? Me => MeOutcome?.Value;
}
`````

## src/EventBooking.Web/Pages/ManageBooking.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/ManageBooking.razor","encoding":"utf8","sha256":"7fc727d9d0e6ce986913e6194ded74029f80ffd182c9b577d37ce430e71d2c7c","parts":1,"part":1} -->

`````razor
@page "/manage/{Token}"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@using EventBooking.Web.Services
@layout EventBooking.Web.Layout.CandidateLayout
@inject BookingClient Booking
@inject CandidatePageOptions PageOptions

<PageTitle>Manage your booking</PageTitle>


<section class="manage-booking-page" aria-busy="@(_loading || _busy)">
    @if (_loading)
    {
        <div class="manage-booking-page__status" aria-label="Loading booking details">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
        </div>
    }
    else if (_cancelled)
    {
        <div class="manage-booking-page__intro">
            <h1>Booking cancelled</h1>
        </div>
        <div class="manage-booking-page__status manage-booking-page__status--cancelled" role="status">
            @if (_reinvited && _deliveryStatus == "Sent")
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled and a new invitation with fresh times has been sent.</p>
            }
            else if (_rebookRequested)
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled. @(_inviteCreated ? "We could not confirm delivery of a replacement invitation; " : "There are no times available right now; ")the recruitment team will be in touch with the next available times.</p>
            }
            else
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled. If that was a mistake, contact the recruitment team at @PageOptions.CoordinatorContact.</p>
            }
        </div>
    }
    else if (_expired)
    {
        <div class="manage-booking-page__status manage-booking-page__status--expired">
            <h1>This link has expired</h1>
            <p class="manage-booking-page__note">This booking link is no longer valid. Please contact the recruitment team at @PageOptions.CoordinatorContact for help.</p>
        </div>
    }
    else if (_booking is null)
    {
        <div class="manage-booking-page__intro">
            <h1>We couldn’t load your booking</h1>
        </div>
        <div class="manage-booking-page__status">
            <p class="manage-booking-page__banner" role="alert">@(_error ?? "Something went wrong. Please try again.")</p>
            <button class="manage-booking-page__button" @onclick="ReloadAsync">Try again</button>
        </div>
    }
    else
    {
        <div class="manage-booking-page__intro">
            <h1>Manage your booking</h1>
            <p class="manage-booking-page__lead">Review your appointment or cancel it below.</p>
        </div>

        @if (_error is not null)
        {
            <p class="manage-booking-page__banner" role="alert"><strong>We couldn’t cancel that booking.</strong> @_error</p>
        }

        <div class="manage-booking-page__card">
            <span class="manage-booking-page__label">Your appointment</span>
            <span class="manage-booking-page__window">@_booking.Display</span>
        </div>

        <div class="manage-booking-page__actions">
            <button class="manage-booking-page__button manage-booking-page__button--danger" @onclick="() => CancelAsync(false)" disabled="@_busy"
                    title="Releases your place. No replacement times are offered — the recruitment team will follow up.">@(_busy ? "Cancelling…" : "Cancel booking")</button>
            <button class="manage-booking-page__button manage-booking-page__button--primary" @onclick="() => CancelAsync(true)" disabled="@_busy"
                    title="Releases your place and emails you a fresh link with the times still open.">@(_busy ? "Cancelling…" : "Cancel and choose a new time")</button>
        </div>
        <p class="manage-booking-page__note manage-booking-page__footnote">This link is single-use and only works for you — no sign-in required. Please do not forward it.</p>
    }
</section>

@code {
    [Parameter]
    public string Token { get; set; } = string.Empty;

    private BookingDto? _booking;
    private string? _error;
    private bool _expired;
    private bool _loading = true;
    private bool _busy;
    private bool _cancelled;
    private bool _rebookRequested;
    private bool _reinvited;
    private bool _inviteCreated;
    private string? _deliveryStatus;
    private int _requestVersion;

    protected override Task OnParametersSetAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        var version = ++_requestVersion;
        _loading = true;
        _busy = false;
        _booking = null;
        _error = null;
        _expired = false;
        _cancelled = false;
        _rebookRequested = false;
        _reinvited = false;
        _inviteCreated = false;
        _deliveryStatus = null;

        try
        {
            var outcome = await Booking.GetBookingAsync(Token, CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            _booking = outcome.Value;
            _expired = outcome.StatusCode == 404;
            _error = _expired || outcome.IsSuccess ? null : outcome.ErrorMessage;
        }
        catch (Exception)
        {
            if (version == _requestVersion)
            {
                _error = "Something went wrong. Please try again.";
            }
        }
        finally
        {
            if (version == _requestVersion)
            {
                _loading = false;
            }
        }
    }

    private async Task CancelAsync(bool rebook)
    {
        if (_busy)
        {
            return;
        }

        var version = _requestVersion;
        _rebookRequested = rebook;
        _busy = true;
        _error = null;

        try
        {
            var outcome = await Booking.CancelAsync(Token, rebook, CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            if (outcome.IsSuccess)
            {
                _inviteCreated = rebook && outcome.Value?.InviteCreated == true;
                _reinvited = _inviteCreated;
                _deliveryStatus = outcome.Value?.DeliveryStatus;
                _cancelled = true;
                _booking = null;
                return;
            }

            _error = outcome.ErrorMessage;
        }
        catch (Exception)
        {
            if (version == _requestVersion)
            {
                _error = "Something went wrong. Please try again.";
            }
        }
        finally
        {
            if (version == _requestVersion)
            {
                _busy = false;
            }
        }
    }
}
`````
