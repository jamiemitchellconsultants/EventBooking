# 00b — Vocabulary edits 60 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Web/Pages/ConfirmedSlots.razor — 1/1

<!-- vocabulary-file: {"id":194,"oldPath":"src/EventBooking.Web/Pages/ConfirmedSlots.razor","newPath":"src/EventBooking.Web/Pages/EventOperations.razor","beforeSha":"558b10ad91b74e9ff664cb4f926acb764f4f707bc8e3bc0d194d5f4dff41ddc8","afterSha":"f7043ae4369a1e0e8124d04d93c96fe033a3afe32efcba9e1477eb5409078487","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Web/Pages/EventOperations.razor — 1/1

<!-- vocabulary-file: {"id":194,"oldPath":"src/EventBooking.Web/Pages/ConfirmedSlots.razor","newPath":"src/EventBooking.Web/Pages/EventOperations.razor","beforeSha":"558b10ad91b74e9ff664cb4f926acb764f4f707bc8e3bc0d194d5f4dff41ddc8","afterSha":"f7043ae4369a1e0e8124d04d93c96fe033a3afe32efcba9e1477eb5409078487","side":"after","part":1,"parts":1} -->

`````razor
@page "/events/operations"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject EventOperationsClient EventImportsApi
@inject EventsClient EventsApi

<PageTitle>Events</PageTitle>

<section class="page events-page" aria-labelledby="events-heading" aria-busy="@(_busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Bulk import</span>
            <h1 id="events-heading">Events</h1>
            <p>Bring in windows that were already agreed away from the negotiation board.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-heading">
            <h2>
                Import already agreed events
                <span class="tip" tabindex="0" role="note"
                      aria-label="Imported events skip the negotiation board: each row lands as a confirmed window with the headcount given per appointment type."
                      data-tip="Imported events skip the negotiation board: each row lands as a confirmed window with the headcount given per appointment type."></span>
            </h2>
        </div>
        <div class="card-body import-card">
            <p class="hint">
                Upload a CSV to add events agreed outside the system. The required header is
                <code>date,startTime,DAT,MED,UNI</code>. The whole file is accepted or rejected.
            </p>
            <InputFile OnChange="OnEventFileChosenAsync" accept=".csv" disabled="@_busy"
                       aria-label="Import events CSV" />

            @if (_busy)
            {
                <p class="hint" role="status">Importing events…</p>
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
                Cancel a event
                <span class="tip" tabindex="0" role="note"
                      aria-label="Cancelling a window releases every place it holds. Any attendee booked into it is notified and re-invited."
                      data-tip="Cancelling a window releases every place it holds. Any attendee booked into it is notified and re-invited."></span>
            </h2>
        </div>
        <div class="card-body">
            @if (_eventsLoading)
            {
                <p class="hint" role="status">Loading events…</p>
            }
            else if (_events is null || _events.Count == 0)
            {
                <p class="hint" role="status">No events yet.</p>
            }
            else
            {
                <div class="table-wrap">
                    <table id="event-operations">
                        <thead>
                            <tr>
                                <th scope="col">Date</th>
                                <th scope="col">Window</th>
                                <th scope="col" title="Places left over the total headcount each appointment type accepted.">Capacity by type</th>
                                <th scope="col" title="Attendees currently booked into this window.">Active bookings</th>
                                <th scope="col" class="actions-column">Cancel</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var eventItem in _events)
                            {
                                <tr @key="eventItem.EventId">
                                    <td data-label="Date">@eventItem.Date.ToString("yyyy-MM-dd")</td>
                                    <td data-label="Window">@eventItem.StartTime.ToString("HH\\:mm")–@eventItem.EndTime.ToString("HH\\:mm")</td>
                                    <td data-label="Capacity by type">
                                        <div class="chip-row">
                                            @foreach (var capacity in eventItem.Capacities)
                                            {
                                                <span class="chip">@capacity.Code @capacity.RemainingCapacity/@capacity.TotalHeadcount</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Active bookings">@eventItem.ActiveBookings</td>
                                    <td data-label="Cancel">
                                        <button class="button button-danger button-small"
                                                @onclick="() => CancelEventAsync(eventItem.EventId)" disabled="@_busy">
                                            @(_cancelAwaitingConfirmation == eventItem.EventId ? "Confirm cancel" : "Cancel event")
                                        </button>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }

            @if (_eventsError is not null)
            {
                <p class="banner error" role="alert">@_eventsError</p>
            }
        </div>
    </div>
</section>

@code {
    private bool _busy;
    private string? _error;
    private string? _importMessage;
    private List<EventImportErrorDto> _importErrors = [];

    private List<EventOperationDto>? _events;
    private bool _eventsLoading = true;
    private string? _eventsError;
    private Guid? _cancelAwaitingConfirmation;

    protected override Task OnInitializedAsync() => ReloadEventsAsync();

    internal Task ReloadEventsForTestingAsync() => ReloadEventsAsync();

    internal Task CancelEventForTestingAsync(Guid eventId) => CancelEventAsync(eventId);

    private async Task ReloadEventsAsync()
    {
        _eventsLoading = true;
        try
        {
            var outcome = await EventsApi.GetEventOperationsAsync(CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _eventsError = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            _events = outcome.Value.Events.ToList();
            _eventsError = null;
        }
        catch (Exception)
        {
            _eventsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _eventsLoading = false;
            StateHasChanged();
        }
    }

    // Two-stage: the first click asks without authorizing the cascade, so a event holding bookings
    // comes back 409 and the button becomes the confirmation.
    private async Task CancelEventAsync(Guid eventId)
    {
        if (_busy)
        {
            return;
        }

        var confirm = _cancelAwaitingConfirmation == eventId;
        _busy = true;
        try
        {
            var outcome = await EventsApi.CancelEventAsync(eventId, confirm, CancellationToken.None);
            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _cancelAwaitingConfirmation = eventId;
                _eventsError = $"{outcome.ErrorMessage} Press Confirm cancel to proceed.";
                return;
            }

            _cancelAwaitingConfirmation = null;
            _eventsError = outcome.ErrorMessage;
            if (outcome.IsSuccess)
            {
                await ReloadEventsAsync();
            }
        }
        catch (Exception)
        {
            _eventsError = "Something went wrong. Please try again.";
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private async Task OnEventFileChosenAsync(InputFileChangeEventArgs args)
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
        OnEventFileChosenAsync(args);

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
            var outcome = await EventImportsApi.ImportAsync(csv, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            if (outcome.Value!.Accepted)
            {
                _importMessage = $"{outcome.Value.ImportedCount} events imported.";
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

## before — src/EventBooking.Web/Pages/ConfirmedSlots.razor.css — 1/1

<!-- vocabulary-file: {"id":195,"oldPath":"src/EventBooking.Web/Pages/ConfirmedSlots.razor.css","newPath":"src/EventBooking.Web/Pages/EventOperations.razor.css","beforeSha":"bb0574d1ef0dc4b7419a7524e2b20e6943c3dc5ab52e272f55a6075761260d4f","afterSha":"5646ad2b4bed08f37230d507c80e09d5e1f13fb30fa0ec327999feb105906f2b","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Web/Pages/EventOperations.razor.css — 1/1

<!-- vocabulary-file: {"id":195,"oldPath":"src/EventBooking.Web/Pages/ConfirmedSlots.razor.css","newPath":"src/EventBooking.Web/Pages/EventOperations.razor.css","beforeSha":"bb0574d1ef0dc4b7419a7524e2b20e6943c3dc5ab52e272f55a6075761260d4f","afterSha":"5646ad2b4bed08f37230d507c80e09d5e1f13fb30fa0ec327999feb105906f2b","side":"after","part":1,"parts":1} -->

`````text
.events-page {
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

## before — src/EventBooking.Web/Pages/Dashboards.razor — 1/1

<!-- vocabulary-file: {"id":196,"oldPath":"src/EventBooking.Web/Pages/Dashboards.razor","newPath":"src/EventBooking.Web/Pages/Dashboards.razor","beforeSha":"72d1fc0cc40a78f9098598a50bd2000dc8a28d178a904c2cb1c2177c25b2bc92","afterSha":"3b3c21ba7fbd362258caf396019ab8e51c0c0278c249f1994754f80002a9cf5a","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Web/Pages/Dashboards.razor — 1/1

<!-- vocabulary-file: {"id":196,"oldPath":"src/EventBooking.Web/Pages/Dashboards.razor","newPath":"src/EventBooking.Web/Pages/Dashboards.razor","beforeSha":"72d1fc0cc40a78f9098598a50bd2000dc8a28d178a904c2cb1c2177c25b2bc92","afterSha":"3b3c21ba7fbd362258caf396019ab8e51c0c0278c249f1994754f80002a9cf5a","side":"after","part":1,"parts":1} -->

`````razor
@page "/dashboards"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject DashboardsClient DashboardsApi
@inject AttendeesClient AttendeesApi

<PageTitle>Dashboards</PageTitle>

<section class="page dashboards-page" aria-labelledby="dashboards-heading" aria-busy="@(_isLoading || _busy)">
    <div class="page-header">
        <div>
            <span class="eyebrow">Coordination</span>
            <h1 id="dashboards-heading">Dashboards</h1>
            <p>Read-only views of current availability, follow-ups, and events.</p>
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
                <button @ref="_eventsTabRef" id="events-tab" class="@TabClass(Tab.Events)" role="tab"
                        aria-selected="@AriaSelected(Tab.Events)" aria-controls="dashboard-panel"
                        tabindex="@TabIndexFor(Tab.Events)"
                        @onclick="() => SelectTab(Tab.Events)" disabled="@_busy">
                    Events <span class="tab-count">(@_data.Events.Count)</span>
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
                        <p>Every attendee either has an invitation or is ready for follow-up.</p>
                    </div>
                }
                else
                {
                    <div class="table-wrap">
                        <table>
                            <thead>
                                <tr><th scope="col">Attendee</th><th scope="col" title="The appointment types this attendee's attendee group requires.">Required types</th><th scope="col" title="When the attendee first became available to invite.">Waiting since</th><th scope="col" title="Every recorded change for this attendee, with who made it.">History</th></tr>
                            </thead>
                            <tbody>
                                @foreach (var row in _data.AwaitingAvailability)
                                {
                                    <tr @key="row.AttendeeId">
                                        <td data-label="Attendee"><strong>@row.Name</strong><a href="mailto:@row.Email">@row.Email</a></td>
                                        <td data-label="Required types">
                                            <div class="chip-row">
                                                @foreach (var requiredCode in row.RequiredCodes)
                                                {
                                                    <span class="chip">@requiredCode</span>
                                                }
                                            </div>
                                        </td>
                                        <td data-label="Waiting since">@row.WaitingSince.ToString("dd MMM yyyy") · @row.DaysWaiting day@(row.DaysWaiting == 1 ? string.Empty : "s")</td>
                                        <td data-label="History"><AuditHistory AttendeeId="@row.AttendeeId" /></td>
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
                        <p>Every invited attendee has responded or is still within the follow-up window.</p>
                    </div>
                }
                else
                {
                    <div class="table-wrap">
                        <table>
                            <thead>
                                <tr><th scope="col">Attendee</th><th scope="col" title="The appointment types this attendee's attendee group requires.">Required types</th><th scope="col" title="When the invitation stopped being chased.">Gave up on</th><th scope="col" title="Every recorded change for this attendee, with who made it.">History</th><th scope="col" class="actions-column"><span class="visually-hidden">Actions</span></th></tr>
                            </thead>
                            <tbody>
                                @foreach (var row in _data.NoResponse)
                                {
                                    <tr @key="row.AttendeeId">
                                        <td data-label="Attendee"><strong>@row.Name</strong><a href="mailto:@row.Email">@row.Email</a></td>
                                        <td data-label="Required types">
                                            <div class="chip-row">
                                                @foreach (var requiredCode in row.RequiredCodes)
                                                {
                                                    <span class="chip">@requiredCode</span>
                                                }
                                            </div>
                                        </td>
                                        <td data-label="Gave up on">@row.GaveUpOn.ToString("dd MMM yyyy")</td>
                                        <td data-label="History"><AuditHistory AttendeeId="@row.AttendeeId" /></td>
                                        <td data-label="Actions">
                                            <button class="button button-primary button-small" @onclick="() => ReinviteAsync(row.AttendeeId)" disabled="@_busy"
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
            else if (_data.Events.Count == 0)
            {
                <div class="empty-state">
                    <strong>No events</strong>
                    <p>Confirmed appointment windows will appear here once all appointment types have accepted them.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr><th scope="col">Date</th><th scope="col">Window</th><th scope="col" title="Places left over the total headcount each appointment type accepted.">Capacity by type</th><th scope="col" title="Attendees currently booked into this window.">Active bookings</th><th scope="col" title="Every recorded change to this eventItem, with who made it.">History</th></tr>
                        </thead>
                        <tbody>
                            @foreach (var eventItem in _data.Events)
                            {
                                <tr @key="eventItem.EventId">
                                    <td data-label="Date">@eventItem.Date.ToString("dd MMM yyyy")</td>
                                    <td data-label="Window">@FormatWindow(eventItem.StartTime, eventItem.EndTime)</td>
                                    <td data-label="Capacity by type">
                                        <div class="chip-row">
                                            @foreach (var capacity in eventItem.Capacities)
                                            {
                                                <span class="chip">@capacity.Code @capacity.RemainingCapacity/@capacity.TotalHeadcount</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Active bookings">@eventItem.ActiveBookings</td>
                                    <td data-label="History"><AuditHistory EventId="@eventItem.EventId" /></td>
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

    private enum Tab { Awaiting, NoResponse, Events }

    private static readonly Tab[] TabOrder = [Tab.Awaiting, Tab.NoResponse, Tab.Events];

    private DashboardsDto? _data;
    private Tab _tab = Tab.Awaiting;
    private string? _error;
    private bool _busy;
    private bool _isLoading = true;

    private ElementReference _awaitingTabRef;
    private ElementReference _noResponseTabRef;
    private ElementReference _eventsTabRef;

    private string ActiveTabId => _tab switch
    {
        Tab.Awaiting => "awaiting-tab",
        Tab.NoResponse => "no-response-tab",
        _ => "events-tab",
    };

    protected override Task OnInitializedAsync() => ReloadAsync();

    private void SelectTab(Tab tab) => _tab = tab;

    private string AriaSelected(Tab tab) => _tab == tab ? "true" : "false";

    private ElementReference TabRefFor(Tab tab) => tab switch
    {
        Tab.Awaiting => _awaitingTabRef,
        Tab.NoResponse => _noResponseTabRef,
        _ => _eventsTabRef,
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

    private async Task ReinviteAsync(Guid attendeeId)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;

        try
        {
            var outcome = await AttendeesApi.TriggerInviteAsync(attendeeId, CancellationToken.None);
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

## before — src/EventBooking.Web/Pages/Help.razor — 1/1

<!-- vocabulary-file: {"id":197,"oldPath":"src/EventBooking.Web/Pages/Help.razor","newPath":"src/EventBooking.Web/Pages/Help.razor","beforeSha":"03a75e6f69aa1300ab3f26ee49479e3846acf417d66e1b0bd2f4a30c55384a1b","afterSha":"4ca4ea9f6274af9f60033be88f4ee603675848093649bde673161a22d7efe6a9","side":"before","part":1,"parts":1} -->

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
