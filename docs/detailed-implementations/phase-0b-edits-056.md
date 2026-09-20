# 00b — Vocabulary edits 56 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Web/Pages/Appointments.razor — 1/1

<!-- vocabulary-file: {"id":188,"oldPath":"src/EventBooking.Web/Pages/Appointments.razor","newPath":"src/EventBooking.Web/Pages/Appointments.razor","beforeSha":"d6c275de7493dd7cdeec0909ac9d8f18bf2d5cfe03377b3c08b9f39c7726fa06","afterSha":"0c026ae0d37038faae88a63f6c43900d79f88ed242efe639ab13566566002b9d","side":"before","part":1,"parts":1} -->

`````razor
@page "/appointments"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AppointmentsClient AppointmentsApi
@inject Microsoft.JSInterop.IJSRuntime JS
@inject HeadOfficePageClock PageClock

<PageTitle>Appointments</PageTitle>

<div class="page appointments-page">
    <div class="page-header">
        <div>
            <span class="eyebrow">Appointment workspace</span>
            <h1>@(_slotList?.AppointmentTypeName is not null ? $"{_slotList.AppointmentTypeName} appointments" : "Appointments")</h1>
            <p>Check candidates in and record appointment outcomes for your appointment type.</p>
        </div>
        @if (_slotList is not null && _slotList.Slots.Count > 0)
        {
            <div class="field">
                <label for="slot-selector">
                    Slot
                    <span class="tip tip-end" tabindex="0" role="note"
                          aria-label="Recently past, current, and upcoming confirmed windows for your appointment type are listed."
                          data-tip="Recently past, current, and upcoming confirmed windows for your appointment type are listed."></span>
                </label>
                <select id="slot-selector" name="confirmed-slot" value="@_selectedSlotId" @onchange="OnSlotChanged">
                    @if (RecentPastSlots(_slotList).Count > 0)
                    {
                        <optgroup label="Recent past">
                            @foreach (var slot in RecentPastSlots(_slotList))
                            {
                                <option value="@slot.ConfirmedSlotId">
                                    @slot.Date.ToString("yyyy-MM-dd") @slot.StartTime.ToString("HH\\:mm")-@slot.EndTime.ToString("HH\\:mm")
                                </option>
                            }
                        </optgroup>
                    }
                    @if (CurrentAndUpcomingSlots(_slotList).Count > 0)
                    {
                        <optgroup label="Current and upcoming">
                            @foreach (var slot in CurrentAndUpcomingSlots(_slotList))
                            {
                                <option value="@slot.ConfirmedSlotId">
                                    @slot.Date.ToString("yyyy-MM-dd") @slot.StartTime.ToString("HH\\:mm")-@slot.EndTime.ToString("HH\\:mm")
                                </option>
                            }
                        </optgroup>
                    }
                </select>
            </div>
        }
    </div>

    @if (_error is not null)
    {
        <p class="banner error" role="alert">@_error</p>
    }

    @if (_announcement is not null)
    {
        <p class="banner notice" aria-live="polite">@_announcement</p>
    }

    @if (_loadingSlots && _slotList is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-medium"></span>
            <span class="loading-line"></span>
            <span class="visually-hidden">Loading appointment slots…</span>
        </div>
    }
    else if (_slotList is not null && _slotList.Slots.Count == 0)
    {
        <div class="card">
            <div class="empty-state">
                <strong>No current or upcoming appointment slots.</strong>
                <p>Slots appear here once every appointment type has accepted a proposed window.</p>
            </div>
        </div>
    }
    else if (_loadingDetail && _detail is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="visually-hidden">Loading appointments…</span>
        </div>
    }
    else if (_detail is not null)
    {
        <section class="card" aria-label="Selected slot">
            <div class="card-heading">
                <h2 class="slot-window">
                    @(_detail.Date.ToString("dddd, dd MMM yyyy"))
                    · @(_detail.StartTime.ToString("HH\\:mm"))-@(_detail.EndTime.ToString("HH\\:mm"))
                </h2>
                <ul class="count-chips">
                    <li class="chip">Expected @_detailCounts?.Expected</li>
                    <li class="chip status-checkedin">Checked in @_detailCounts?.CheckedIn</li>
                    <li class="chip status-completed">Completed @_detailCounts?.Completed</li>
                    <li class="chip status-noshow">No-show @_detailCounts?.NoShow</li>
                </ul>
                <button class="button" data-testid="download-roster" type="button"
                        @onclick="DownloadRosterAsync" disabled="@_downloadingRoster"
                        title="Downloads this slot's roster as a CSV file to work from offline.">
                    @(_downloadingRoster ? "Preparing…" : "Download roster")
                </button>
            </div>

            @if (_detail.Appointments.Count == 0)
            {
                <div class="empty-state">
                    <strong>Nobody to see in this window.</strong>
                    <p>No candidates require this appointment in the selected slot.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Candidate</th>
                                <th scope="col" title="Expected, checked in, completed, or no-show.">Status</th>
                                <th scope="col" title="When check-in was recorded, at head office local time.">Check-in recorded</th>
                                <th scope="col" title="When completion or no-show was recorded, at head office local time.">Outcome recorded</th>
                                <th scope="col" class="actions-column"><span class="muted">Actions</span></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var row in _detail.Appointments)
                            {
                                var busy = _busyRows.Contains(row.BookingAppointmentId);
                                <tr>
                                    <td data-label="Candidate">
                                        <span class="candidate-name">@row.CandidateName</span>
                                        <span class="candidate-email">@row.CandidateEmail</span>
                                    </td>
                                    <td data-label="Status"><span class="status-@row.Status.ToLowerInvariant()">@row.Status</span></td>
                                    <td data-label="Check-in recorded">@(row.CheckedInAt?.ToString("yyyy-MM-dd HH:mm zzz") ?? "—")</td>
                                    <td data-label="Outcome recorded">@(row.OutcomeAt?.ToString("yyyy-MM-dd HH:mm zzz") ?? "—")</td>
                                    <td data-label="Actions">
                                        <span class="row-actions">
                                            @foreach (var action in ActionsFor(row))
                                            {
                                                <button class="button" data-action="@action.Action"
                                                        title="@action.Tip"
                                                        @onclick="() => OnActionAsync(row, action.TargetStatus, action.Label, action.RequiresConfirmation)"
                                                        disabled="@(busy || !action.Enabled)">
                                                    @action.Label
                                                </button>
                                            }
                                        </span>
                                        @if (row.Status == "Expected")
                                        {
                                            @if (!IsToday(_detail))
                                            {
                                                <p class="row-hint">Check-in opens on the confirmed-slot date.</p>
                                            }
                                            else if (!WindowHasEnded(_detail))
                                            {
                                                <p class="row-hint">No-show is available after the slot window ends.</p>
                                            }
                                        }
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </section>
    }

    @if (_pending is not null)
    {
        <div class="dialog-overlay" @onclick="CancelPending">
            <div role="alertdialog" aria-labelledby="confirm-heading" aria-describedby="confirm-description" @onclick:stopPropagation="true">
                <h2 id="confirm-heading">@_pending.Label</h2>
                <p id="confirm-description">@_pending.Label for @_pending.CandidateName. This change is recorded immediately.</p>
                <span class="row-actions">
                    <button class="button button-primary" data-confirm="yes" @onclick="ConfirmPendingAsync"
                            title="Records the change and writes it to the appointment history.">Confirm</button>
                    <button class="button button-quiet" data-confirm="no" @onclick="CancelPending">Cancel</button>
                </span>
            </div>
        </div>
    }
</div>

@code {
    private AppointmentWorkspaceSlotListDto? _slotList;
    private AppointmentSlotDetailDto? _detail;
    private Guid? _selectedSlotId;
    private readonly HashSet<Guid> _busyRows = [];
    private PendingAction? _pending;
    private int _detailRequestVersion;
    private bool _loadingSlots;
    private bool _loadingDetail;
    private bool _downloadingRoster;
    private string? _error;
    private string? _announcement;

    private sealed record PendingAction(
        Guid BookingAppointmentId, string CandidateName, string TargetStatus, string Label);

    private sealed record RowAction(
        string Action, string Label, string TargetStatus, bool Enabled, bool RequiresConfirmation, string Tip);

    private AppointmentStatusCountsDto? _detailCounts =>
        _slotList?.Slots.FirstOrDefault(slot => slot.ConfirmedSlotId == _selectedSlotId)?.Counts;

    protected override async Task OnInitializedAsync()
    {
        _loadingSlots = true;
        try
        {
            var outcome = await AppointmentsApi.ListSlotsAsync(CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _error = outcome.ErrorMessage;
                return;
            }

            _slotList = outcome.Value;
            var first = CurrentAndUpcomingSlots(_slotList).FirstOrDefault()
                ?? RecentPastSlots(_slotList).LastOrDefault();
            if (first is not null)
            {
                await LoadDetailAsync(first.ConfirmedSlotId);
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _loadingSlots = false;
        }
    }

    // Fetched through the authenticated client and handed to the interop helper: a plain anchor
    // to the API route would not carry the caller's bearer token.
    private async Task DownloadRosterAsync()
    {
        if (_selectedSlotId is null || _downloadingRoster)
        {
            return;
        }

        _downloadingRoster = true;
        _error = null;
        try
        {
            var outcome = await AppointmentsApi.GetRosterAsync(
                _selectedSlotId.Value, CancellationToken.None);
            if (outcome is not { IsSuccess: true, Value: not null })
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            await JS.InvokeVoidAsync(
                "saveTextFile", outcome.Value.FileName, outcome.Value.Content);
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _downloadingRoster = false;
            StateHasChanged();
        }
    }

    private async Task OnSlotChanged(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var slotId) && slotId != _selectedSlotId)
        {
            await LoadDetailAsync(slotId);
        }
    }

    private async Task<bool> LoadDetailAsync(Guid slotId)
    {
        var requestVersion = ++_detailRequestVersion;
        _loadingDetail = true;
        _selectedSlotId = slotId;
        _detail = null;
        _pending = null;
        _error = null;
        _announcement = null;
        try
        {
            var outcome = await AppointmentsApi.GetSlotAsync(slotId, CancellationToken.None);
            if (requestVersion != _detailRequestVersion)
            {
                return false;
            }

            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _error = outcome.ErrorMessage;
                return false;
            }

            _detail = outcome.Value;
            _error = null;
            return true;
        }
        catch (Exception)
        {
            if (requestVersion == _detailRequestVersion)
            {
                _error = "Something went wrong. Please try again.";
            }

            return false;
        }
        finally
        {
            if (requestVersion == _detailRequestVersion)
            {
                _loadingDetail = false;
            }
        }
    }

    private bool IsToday(AppointmentSlotDetailDto detail) =>
        detail.Date == DateOnly.FromDateTime(PageClock.NowAtHeadOffice.DateTime);

    private DateOnly TodayAtHeadOffice() =>
        DateOnly.FromDateTime(PageClock.NowAtHeadOffice.DateTime);

    private IReadOnlyList<AppointmentSlotSummaryDto> RecentPastSlots(
        AppointmentWorkspaceSlotListDto slotList) =>
        slotList.Slots.Where(slot => slot.Date < TodayAtHeadOffice()).ToList();

    private IReadOnlyList<AppointmentSlotSummaryDto> CurrentAndUpcomingSlots(
        AppointmentWorkspaceSlotListDto slotList) =>
        slotList.Slots.Where(slot => slot.Date >= TodayAtHeadOffice()).ToList();

    private bool WindowHasEnded(AppointmentSlotDetailDto detail)
    {
        var now = PageClock.NowAtHeadOffice;
        var end = new DateTimeOffset(detail.Date.ToDateTime(detail.EndTime), now.Offset);
        return now >= end;
    }

    private IReadOnlyList<RowAction> ActionsFor(BookingAppointmentRowDto row)
    {
        return row.Status switch
        {
            "Expected" when _detail is not null => new RowAction[]
            {
                new("check-in", "Check in", "CheckedIn", IsToday(_detail), false,
                    "Marks the candidate as arrived. Only available on the slot date."),
                new("no-show", "No-show", "NoShow", WindowHasEnded(_detail), true,
                    "Records that the candidate never arrived. Only available once the window has ended."),
            },
            "CheckedIn" => new RowAction[]
            {
                new("complete", "Complete", "Completed", true, false,
                    "Records the appointment as delivered."),
                new("correct-expected", "Correct to expected", "Expected", true, true,
                    "Undoes the check-in if it was recorded against the wrong candidate."),
            },
            "Completed" => new RowAction[]
            {
                new("correct-checked-in", "Correct to checked in", "CheckedIn", true, true,
                    "Undoes the completion and puts the candidate back to checked in."),
            },
            "NoShow" => new RowAction[]
            {
                new("correct-expected", "Correct to expected", "Expected", true, true,
                    "Clears the no-show so the candidate is expected again."),
            },
            _ => [],
        };
    }

    private Task OnActionAsync(
        BookingAppointmentRowDto row, string targetStatus, string label, bool requiresConfirmation)
    {
        if (requiresConfirmation)
        {
            _pending = new PendingAction(row.BookingAppointmentId, row.CandidateName, targetStatus, label);
            return Task.CompletedTask;
        }

        return UpdateAsync(row.BookingAppointmentId, row.CandidateName, targetStatus);
    }

    private void CancelPending() => _pending = null;

    private async Task ConfirmPendingAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var pending = _pending;
        _pending = null;
        await UpdateAsync(pending.BookingAppointmentId, pending.CandidateName, pending.TargetStatus);
    }

    private async Task UpdateAsync(Guid bookingAppointmentId, string candidateName, string targetStatus)
    {
        var sourceDetail = _detail;
        if (sourceDetail is null || !_busyRows.Add(bookingAppointmentId))
        {
            return;
        }

        var sourceSlotId = sourceDetail.ConfirmedSlotId;
        var sourceRequestVersion = _detailRequestVersion;
        var row = sourceDetail.Appointments.FirstOrDefault(
            item => item.BookingAppointmentId == bookingAppointmentId);
        if (row is null)
        {
            _busyRows.Remove(bookingAppointmentId);
            return;
        }

        try
        {
            var outcome = await AppointmentsApi.UpdateStatusAsync(
                bookingAppointmentId, targetStatus, row.Version, CancellationToken.None);
            var appliesToCurrentDetail = IsCurrentDetail(sourceSlotId, sourceRequestVersion);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                ApplyUpdate(sourceSlotId, row, outcome.Value, appliesToCurrentDetail);
                if (appliesToCurrentDetail)
                {
                    _error = null;
                    _announcement = AnnouncementFor(targetStatus, candidateName);
                }

                return;
            }

            if (!appliesToCurrentDetail)
            {
                return;
            }

            if (outcome.ErrorCode == AppointmentsClient.VersionConflictErrorCode)
            {
                if (await LoadDetailAsync(sourceSlotId))
                {
                    _error = "Another staff member changed this appointment. The row has been refreshed; review it before trying again.";
                }

                return;
            }

            _error = outcome.ErrorMessage;
        }
        catch (Exception)
        {
            if (IsCurrentDetail(sourceSlotId, sourceRequestVersion))
            {
                _error = "Something went wrong. Please try again.";
            }
        }
        finally
        {
            _busyRows.Remove(bookingAppointmentId);
        }
    }

    private bool IsCurrentDetail(Guid slotId, int requestVersion) =>
        requestVersion == _detailRequestVersion
        && _selectedSlotId == slotId
        && _detail?.ConfirmedSlotId == slotId;

    private void ApplyUpdate(
        Guid sourceSlotId,
        BookingAppointmentRowDto row,
        BookingAppointmentUpdateDto update,
        bool applyToCurrentDetail)
    {
        if (_slotList is null)
        {
            return;
        }

        var oldStatus = row.Status;
        if (applyToCurrentDetail && _detail is not null)
        {
            _detail = _detail with
            {
                Appointments = _detail.Appointments
                    .Select(item => item.BookingAppointmentId == row.BookingAppointmentId
                        ? item with
                        {
                            Status = update.Status,
                            CheckedInAt = update.CheckedInAt,
                            OutcomeAt = update.OutcomeAt,
                            Version = update.Version,
                        }
                        : item)
                    .ToList(),
            };
        }

        _slotList = _slotList with
        {
            Slots = _slotList.Slots
                .Select(slot => slot.ConfirmedSlotId == sourceSlotId
                    ? slot with { Counts = MoveCount(slot.Counts, oldStatus, update.Status) }
                    : slot)
                .ToList(),
        };
    }

    private static AppointmentStatusCountsDto MoveCount(
        AppointmentStatusCountsDto counts, string oldStatus, string newStatus)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Expected"] = counts.Expected,
            ["CheckedIn"] = counts.CheckedIn,
            ["Completed"] = counts.Completed,
            ["NoShow"] = counts.NoShow,
        };
        if (buckets.ContainsKey(oldStatus))
        {
            buckets[oldStatus] = Math.Max(0, buckets[oldStatus] - 1);
        }

        if (buckets.ContainsKey(newStatus))
        {
            buckets[newStatus] += 1;
        }

        return new AppointmentStatusCountsDto
        {
            Expected = buckets["Expected"],
            CheckedIn = buckets["CheckedIn"],
            Completed = buckets["Completed"],
            NoShow = buckets["NoShow"],
        };
    }

    private static string AnnouncementFor(string targetStatus, string candidateName) =>
        targetStatus switch
        {
            "CheckedIn" => $"Check-in recorded for {candidateName}.",
            "Completed" => $"Completion recorded for {candidateName}.",
            "NoShow" => $"No-show recorded for {candidateName}.",
            _ => $"Correction recorded for {candidateName}.",
        };
}
`````

## after — src/EventBooking.Web/Pages/Appointments.razor — 1/1

<!-- vocabulary-file: {"id":188,"oldPath":"src/EventBooking.Web/Pages/Appointments.razor","newPath":"src/EventBooking.Web/Pages/Appointments.razor","beforeSha":"d6c275de7493dd7cdeec0909ac9d8f18bf2d5cfe03377b3c08b9f39c7726fa06","afterSha":"0c026ae0d37038faae88a63f6c43900d79f88ed242efe639ab13566566002b9d","side":"after","part":1,"parts":1} -->

`````razor
@page "/appointments"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AppointmentsClient AppointmentsApi
@inject Microsoft.JSInterop.IJSRuntime JS
@inject TransitionalLocationPageClock PageClock

<PageTitle>Appointments</PageTitle>

<div class="page appointments-page">
    <div class="page-header">
        <div>
            <span class="eyebrow">Appointment workspace</span>
            <h1>@(_eventList?.AppointmentTypeName is not null ? $"{_eventList.AppointmentTypeName} appointments" : "Appointments")</h1>
            <p>Check attendees in and record appointment outcomes for your appointment type.</p>
        </div>
        @if (_eventList is not null && _eventList.Events.Count > 0)
        {
            <div class="field">
                <label for="event-selector">
                    Event
                    <span class="tip tip-end" tabindex="0" role="note"
                          aria-label="Recently past, current, and upcoming confirmed windows for your appointment type are listed."
                          data-tip="Recently past, current, and upcoming confirmed windows for your appointment type are listed."></span>
                </label>
                <select id="event-selector" name="event" value="@_selectedEventId" @onchange="OnEventChanged">
                    @if (RecentPastEvents(_eventList).Count > 0)
                    {
                        <optgroup label="Recent past">
                            @foreach (var eventItem in RecentPastEvents(_eventList))
                            {
                                <option value="@eventItem.EventId">
                                    @eventItem.Date.ToString("yyyy-MM-dd") @eventItem.StartTime.ToString("HH\\:mm")-@eventItem.EndTime.ToString("HH\\:mm")
                                </option>
                            }
                        </optgroup>
                    }
                    @if (CurrentAndUpcomingEvents(_eventList).Count > 0)
                    {
                        <optgroup label="Current and upcoming">
                            @foreach (var eventItem in CurrentAndUpcomingEvents(_eventList))
                            {
                                <option value="@eventItem.EventId">
                                    @eventItem.Date.ToString("yyyy-MM-dd") @eventItem.StartTime.ToString("HH\\:mm")-@eventItem.EndTime.ToString("HH\\:mm")
                                </option>
                            }
                        </optgroup>
                    }
                </select>
            </div>
        }
    </div>

    @if (_error is not null)
    {
        <p class="banner error" role="alert">@_error</p>
    }

    @if (_announcement is not null)
    {
        <p class="banner notice" aria-live="polite">@_announcement</p>
    }

    @if (_loadingEvents && _eventList is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-medium"></span>
            <span class="loading-line"></span>
            <span class="visually-hidden">Loading appointment events…</span>
        </div>
    }
    else if (_eventList is not null && _eventList.Events.Count == 0)
    {
        <div class="card">
            <div class="empty-state">
                <strong>No current or upcoming appointment events.</strong>
                <p>Events appear here once every appointment type has accepted a proposed window.</p>
            </div>
        </div>
    }
    else if (_loadingDetail && _detail is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="visually-hidden">Loading appointments…</span>
        </div>
    }
    else if (_detail is not null)
    {
        <section class="card" aria-label="Selected event">
            <div class="card-heading">
                <h2 class="event-window">
                    @(_detail.Date.ToString("dddd, dd MMM yyyy"))
                    · @(_detail.StartTime.ToString("HH\\:mm"))-@(_detail.EndTime.ToString("HH\\:mm"))
                </h2>
                <ul class="count-chips">
                    <li class="chip">Expected @_detailCounts?.Expected</li>
                    <li class="chip status-checkedin">Checked in @_detailCounts?.CheckedIn</li>
                    <li class="chip status-completed">Completed @_detailCounts?.Completed</li>
                    <li class="chip status-noshow">No-show @_detailCounts?.NoShow</li>
                </ul>
                <button class="button" data-testid="download-roster" type="button"
                        @onclick="DownloadRosterAsync" disabled="@_downloadingRoster"
                        title="Downloads this event's roster as a CSV file to work from offline.">
                    @(_downloadingRoster ? "Preparing…" : "Download roster")
                </button>
            </div>

            @if (_detail.Appointments.Count == 0)
            {
                <div class="empty-state">
                    <strong>Nobody to see in this window.</strong>
                    <p>No attendees require this appointment in the selected eventItem.</p>
                </div>
            }
            else
            {
                <div class="table-wrap">
                    <table>
                        <thead>
                            <tr>
                                <th scope="col">Attendee</th>
                                <th scope="col" title="Expected, checked in, completed, or no-show.">Status</th>
                                <th scope="col" title="When check-in was recorded, at transitional location local time.">Check-in recorded</th>
                                <th scope="col" title="When completion or no-show was recorded, at transitional location local time.">Outcome recorded</th>
                                <th scope="col" class="actions-column"><span class="muted">Actions</span></th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var row in _detail.Appointments)
                            {
                                var busy = _busyRows.Contains(row.BookingAppointmentId);
                                <tr>
                                    <td data-label="Attendee">
                                        <span class="attendee-name">@row.AttendeeName</span>
                                        <span class="attendee-email">@row.AttendeeEmail</span>
                                    </td>
                                    <td data-label="Status"><span class="status-@row.Status.ToLowerInvariant()">@row.Status</span></td>
                                    <td data-label="Check-in recorded">@(row.CheckedInAt?.ToString("yyyy-MM-dd HH:mm zzz") ?? "—")</td>
                                    <td data-label="Outcome recorded">@(row.OutcomeAt?.ToString("yyyy-MM-dd HH:mm zzz") ?? "—")</td>
                                    <td data-label="Actions">
                                        <span class="row-actions">
                                            @foreach (var action in ActionsFor(row))
                                            {
                                                <button class="button" data-action="@action.Action"
                                                        title="@action.Tip"
                                                        @onclick="() => OnActionAsync(row, action.TargetStatus, action.Label, action.RequiresConfirmation)"
                                                        disabled="@(busy || !action.Enabled)">
                                                    @action.Label
                                                </button>
                                            }
                                        </span>
                                        @if (row.Status == "Expected")
                                        {
                                            @if (!IsToday(_detail))
                                            {
                                                <p class="row-hint">Check-in opens on the event date.</p>
                                            }
                                            else if (!WindowHasEnded(_detail))
                                            {
                                                <p class="row-hint">No-show is available after the event window ends.</p>
                                            }
                                        }
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </section>
    }

    @if (_pending is not null)
    {
        <div class="dialog-overlay" @onclick="CancelPending">
            <div role="alertdialog" aria-labelledby="confirm-heading" aria-describedby="confirm-description" @onclick:stopPropagation="true">
                <h2 id="confirm-heading">@_pending.Label</h2>
                <p id="confirm-description">@_pending.Label for @_pending.AttendeeName. This change is recorded immediately.</p>
                <span class="row-actions">
                    <button class="button button-primary" data-confirm="yes" @onclick="ConfirmPendingAsync"
                            title="Records the change and writes it to the appointment history.">Confirm</button>
                    <button class="button button-quiet" data-confirm="no" @onclick="CancelPending">Cancel</button>
                </span>
            </div>
        </div>
    }
</div>

@code {
    private AppointmentWorkspaceEventListDto? _eventList;
    private AppointmentEventDetailDto? _detail;
    private Guid? _selectedEventId;
    private readonly HashSet<Guid> _busyRows = [];
    private PendingAction? _pending;
    private int _detailRequestVersion;
    private bool _loadingEvents;
    private bool _loadingDetail;
    private bool _downloadingRoster;
    private string? _error;
    private string? _announcement;

    private sealed record PendingAction(
        Guid BookingAppointmentId, string AttendeeName, string TargetStatus, string Label);

    private sealed record RowAction(
        string Action, string Label, string TargetStatus, bool Enabled, bool RequiresConfirmation, string Tip);

    private AppointmentStatusCountsDto? _detailCounts =>
        _eventList?.Events.FirstOrDefault(eventItem => eventItem.EventId == _selectedEventId)?.Counts;

    protected override async Task OnInitializedAsync()
    {
        _loadingEvents = true;
        try
        {
            var outcome = await AppointmentsApi.ListEventsAsync(CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _error = outcome.ErrorMessage;
                return;
            }

            _eventList = outcome.Value;
            var first = CurrentAndUpcomingEvents(_eventList).FirstOrDefault()
                ?? RecentPastEvents(_eventList).LastOrDefault();
            if (first is not null)
            {
                await LoadDetailAsync(first.EventId);
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _loadingEvents = false;
        }
    }

    // Fetched through the authenticated client and handed to the interop helper: a plain anchor
    // to the API route would not carry the caller's bearer token.
    private async Task DownloadRosterAsync()
    {
        if (_selectedEventId is null || _downloadingRoster)
        {
            return;
        }

        _downloadingRoster = true;
        _error = null;
        try
        {
            var outcome = await AppointmentsApi.GetRosterAsync(
                _selectedEventId.Value, CancellationToken.None);
            if (outcome is not { IsSuccess: true, Value: not null })
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
                return;
            }

            await JS.InvokeVoidAsync(
                "saveTextFile", outcome.Value.FileName, outcome.Value.Content);
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        finally
        {
            _downloadingRoster = false;
            StateHasChanged();
        }
    }

    private async Task OnEventChanged(ChangeEventArgs args)
    {
        if (Guid.TryParse(args.Value?.ToString(), out var eventId) && eventId != _selectedEventId)
        {
            await LoadDetailAsync(eventId);
        }
    }

    private async Task<bool> LoadDetailAsync(Guid eventId)
    {
        var requestVersion = ++_detailRequestVersion;
        _loadingDetail = true;
        _selectedEventId = eventId;
        _detail = null;
        _pending = null;
        _error = null;
        _announcement = null;
        try
        {
            var outcome = await AppointmentsApi.GetEventAsync(eventId, CancellationToken.None);
            if (requestVersion != _detailRequestVersion)
            {
                return false;
            }

            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _error = outcome.ErrorMessage;
                return false;
            }

            _detail = outcome.Value;
            _error = null;
            return true;
        }
        catch (Exception)
        {
            if (requestVersion == _detailRequestVersion)
            {
                _error = "Something went wrong. Please try again.";
            }

            return false;
        }
        finally
        {
            if (requestVersion == _detailRequestVersion)
            {
                _loadingDetail = false;
            }
        }
    }

    private bool IsToday(AppointmentEventDetailDto detail) =>
        detail.Date == DateOnly.FromDateTime(PageClock.NowAtTransitionalLocation.DateTime);

    private DateOnly TodayAtTransitionalLocation() =>
        DateOnly.FromDateTime(PageClock.NowAtTransitionalLocation.DateTime);

    private IReadOnlyList<AppointmentEventSummaryDto> RecentPastEvents(
        AppointmentWorkspaceEventListDto eventList) =>
        eventList.Events.Where(eventItem => eventItem.Date < TodayAtTransitionalLocation()).ToList();

    private IReadOnlyList<AppointmentEventSummaryDto> CurrentAndUpcomingEvents(
        AppointmentWorkspaceEventListDto eventList) =>
        eventList.Events.Where(eventItem => eventItem.Date >= TodayAtTransitionalLocation()).ToList();

    private bool WindowHasEnded(AppointmentEventDetailDto detail)
    {
        var now = PageClock.NowAtTransitionalLocation;
        var end = new DateTimeOffset(detail.Date.ToDateTime(detail.EndTime), now.Offset);
        return now >= end;
    }

    private IReadOnlyList<RowAction> ActionsFor(BookingAppointmentRowDto row)
    {
        return row.Status switch
        {
            "Expected" when _detail is not null => new RowAction[]
            {
                new("check-in", "Check in", "CheckedIn", IsToday(_detail), false,
                    "Marks the attendee as arrived. Only available on the event date."),
                new("no-show", "No-show", "NoShow", WindowHasEnded(_detail), true,
                    "Records that the attendee never arrived. Only available once the window has ended."),
            },
            "CheckedIn" => new RowAction[]
            {
                new("complete", "Complete", "Completed", true, false,
                    "Records the appointment as delivered."),
                new("correct-expected", "Correct to expected", "Expected", true, true,
                    "Undoes the check-in if it was recorded against the wrong attendee."),
            },
            "Completed" => new RowAction[]
            {
                new("correct-checked-in", "Correct to checked in", "CheckedIn", true, true,
                    "Undoes the completion and puts the attendee back to checked in."),
            },
            "NoShow" => new RowAction[]
            {
                new("correct-expected", "Correct to expected", "Expected", true, true,
                    "Clears the no-show so the attendee is expected again."),
            },
            _ => [],
        };
    }

    private Task OnActionAsync(
        BookingAppointmentRowDto row, string targetStatus, string label, bool requiresConfirmation)
    {
        if (requiresConfirmation)
        {
            _pending = new PendingAction(row.BookingAppointmentId, row.AttendeeName, targetStatus, label);
            return Task.CompletedTask;
        }

        return UpdateAsync(row.BookingAppointmentId, row.AttendeeName, targetStatus);
    }

    private void CancelPending() => _pending = null;

    private async Task ConfirmPendingAsync()
    {
        if (_pending is null)
        {
            return;
        }

        var pending = _pending;
        _pending = null;
        await UpdateAsync(pending.BookingAppointmentId, pending.AttendeeName, pending.TargetStatus);
    }

    private async Task UpdateAsync(Guid bookingAppointmentId, string attendeeName, string targetStatus)
    {
        var sourceDetail = _detail;
        if (sourceDetail is null || !_busyRows.Add(bookingAppointmentId))
        {
            return;
        }

        var sourceEventId = sourceDetail.EventId;
        var sourceRequestVersion = _detailRequestVersion;
        var row = sourceDetail.Appointments.FirstOrDefault(
            item => item.BookingAppointmentId == bookingAppointmentId);
        if (row is null)
        {
            _busyRows.Remove(bookingAppointmentId);
            return;
        }

        try
        {
            var outcome = await AppointmentsApi.UpdateStatusAsync(
                bookingAppointmentId, targetStatus, row.Version, CancellationToken.None);
            var appliesToCurrentDetail = IsCurrentDetail(sourceEventId, sourceRequestVersion);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                ApplyUpdate(sourceEventId, row, outcome.Value, appliesToCurrentDetail);
                if (appliesToCurrentDetail)
                {
                    _error = null;
                    _announcement = AnnouncementFor(targetStatus, attendeeName);
                }

                return;
            }

            if (!appliesToCurrentDetail)
            {
                return;
            }

            if (outcome.ErrorCode == AppointmentsClient.VersionConflictErrorCode)
            {
                if (await LoadDetailAsync(sourceEventId))
                {
                    _error = "Another staff member changed this appointment. The row has been refreshed; review it before trying again.";
                }

                return;
            }

            _error = outcome.ErrorMessage;
        }
        catch (Exception)
        {
            if (IsCurrentDetail(sourceEventId, sourceRequestVersion))
            {
                _error = "Something went wrong. Please try again.";
            }
        }
        finally
        {
            _busyRows.Remove(bookingAppointmentId);
        }
    }

    private bool IsCurrentDetail(Guid eventId, int requestVersion) =>
        requestVersion == _detailRequestVersion
        && _selectedEventId == eventId
        && _detail?.EventId == eventId;

    private void ApplyUpdate(
        Guid sourceEventId,
        BookingAppointmentRowDto row,
        BookingAppointmentUpdateDto update,
        bool applyToCurrentDetail)
    {
        if (_eventList is null)
        {
            return;
        }

        var oldStatus = row.Status;
        if (applyToCurrentDetail && _detail is not null)
        {
            _detail = _detail with
            {
                Appointments = _detail.Appointments
                    .Select(item => item.BookingAppointmentId == row.BookingAppointmentId
                        ? item with
                        {
                            Status = update.Status,
                            CheckedInAt = update.CheckedInAt,
                            OutcomeAt = update.OutcomeAt,
                            Version = update.Version,
                        }
                        : item)
                    .ToList(),
            };
        }

        _eventList = _eventList with
        {
            Events = _eventList.Events
                .Select(eventItem => eventItem.EventId == sourceEventId
                    ? eventItem with { Counts = MoveCount(eventItem.Counts, oldStatus, update.Status) }
                    : eventItem)
                .ToList(),
        };
    }

    private static AppointmentStatusCountsDto MoveCount(
        AppointmentStatusCountsDto counts, string oldStatus, string newStatus)
    {
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Expected"] = counts.Expected,
            ["CheckedIn"] = counts.CheckedIn,
            ["Completed"] = counts.Completed,
            ["NoShow"] = counts.NoShow,
        };
        if (buckets.ContainsKey(oldStatus))
        {
            buckets[oldStatus] = Math.Max(0, buckets[oldStatus] - 1);
        }

        if (buckets.ContainsKey(newStatus))
        {
            buckets[newStatus] += 1;
        }

        return new AppointmentStatusCountsDto
        {
            Expected = buckets["Expected"],
            CheckedIn = buckets["CheckedIn"],
            Completed = buckets["Completed"],
            NoShow = buckets["NoShow"],
        };
    }

    private static string AnnouncementFor(string targetStatus, string attendeeName) =>
        targetStatus switch
        {
            "CheckedIn" => $"Check-in recorded for {attendeeName}.",
            "Completed" => $"Completion recorded for {attendeeName}.",
            "NoShow" => $"No-show recorded for {attendeeName}.",
            _ => $"Correction recorded for {attendeeName}.",
        };
}
`````

## before — src/EventBooking.Web/Pages/Appointments.razor.css — 1/1

<!-- vocabulary-file: {"id":189,"oldPath":"src/EventBooking.Web/Pages/Appointments.razor.css","newPath":"src/EventBooking.Web/Pages/Appointments.razor.css","beforeSha":"05efdf9bb471e02b5fdaf57c63e9cb6f4d40f51b1dd3c1c911db2fdfc4c1b6c4","afterSha":"e70c481dcd7bab114df527d42edac672f8ad8a37b6d97f475f1846743bae89a2","side":"before","part":1,"parts":1} -->

`````text
.slot-window {
    color: var(--ink-strong);
    font-size: 0.9375rem;
    font-weight: 600;
    letter-spacing: 0;
    margin: 0;
    text-transform: none;
}

.row-hint {
    color: var(--sub);
    font-size: 0.75rem;
    font-style: italic;
    margin: 6px 0 0;
}

.candidate-name {
    display: block;
    font-weight: 600;
}

.candidate-email {
    color: var(--sub);
    display: block;
    font-size: 0.75rem;
}

.table-wrap {
    max-height: calc(100vh - 360px);
    min-height: 200px;
    overflow-y: auto;
}

.table-wrap thead th {
    position: sticky;
    top: 0;
    z-index: 1;
}

.dialog-overlay {
    align-items: center;
    background: rgba(0, 0, 0, 0.45);
    display: flex;
    inset: 0;
    justify-content: center;
    padding: 20px;
    position: fixed;
    z-index: 100;
}

[role="alertdialog"] {
    background: var(--surface);
    border: 1px solid var(--accent);
    border-left: 5px solid var(--accent);
    border-radius: var(--radius);
    box-shadow: var(--shadow);
    display: flex;
    flex-direction: column;
    gap: 10px;
    max-width: 480px;
    padding: 18px 20px;
    width: 100%;
}

[role="alertdialog"] h2 {
    font-size: 1rem;
    margin: 0;
}

[role="alertdialog"] p {
    color: var(--sub);
    font-size: 0.8125rem;
    margin: 0;
    max-width: 60ch;
}

@media (max-width: 760px) {
    .candidate-email,
    .candidate-name {
        text-align: right;
    }
}
`````

## after — src/EventBooking.Web/Pages/Appointments.razor.css — 1/1

<!-- vocabulary-file: {"id":189,"oldPath":"src/EventBooking.Web/Pages/Appointments.razor.css","newPath":"src/EventBooking.Web/Pages/Appointments.razor.css","beforeSha":"05efdf9bb471e02b5fdaf57c63e9cb6f4d40f51b1dd3c1c911db2fdfc4c1b6c4","afterSha":"e70c481dcd7bab114df527d42edac672f8ad8a37b6d97f475f1846743bae89a2","side":"after","part":1,"parts":1} -->

`````text
.event-window {
    color: var(--ink-strong);
    font-size: 0.9375rem;
    font-weight: 600;
    letter-spacing: 0;
    margin: 0;
    text-transform: none;
}

.row-hint {
    color: var(--sub);
    font-size: 0.75rem;
    font-style: italic;
    margin: 6px 0 0;
}

.attendee-name {
    display: block;
    font-weight: 600;
}

.attendee-email {
    color: var(--sub);
    display: block;
    font-size: 0.75rem;
}

.table-wrap {
    max-height: calc(100vh - 360px);
    min-height: 200px;
    overflow-y: auto;
}

.table-wrap thead th {
    position: sticky;
    top: 0;
    z-index: 1;
}

.dialog-overlay {
    align-items: center;
    background: rgba(0, 0, 0, 0.45);
    display: flex;
    inset: 0;
    justify-content: center;
    padding: 20px;
    position: fixed;
    z-index: 100;
}

[role="alertdialog"] {
    background: var(--surface);
    border: 1px solid var(--accent);
    border-left: 5px solid var(--accent);
    border-radius: var(--radius);
    box-shadow: var(--shadow);
    display: flex;
    flex-direction: column;
    gap: 10px;
    max-width: 480px;
    padding: 18px 20px;
    width: 100%;
}

[role="alertdialog"] h2 {
    font-size: 1rem;
    margin: 0;
}

[role="alertdialog"] p {
    color: var(--sub);
    font-size: 0.8125rem;
    margin: 0;
    max-width: 60ch;
}

@media (max-width: 760px) {
    .attendee-email,
    .attendee-name {
        text-align: right;
    }
}
`````
