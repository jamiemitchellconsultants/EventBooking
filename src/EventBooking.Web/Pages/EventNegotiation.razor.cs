using Microsoft.AspNetCore.Components;
using EventBooking.Web.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class EventNegotiation
{
    [Inject] private IEventsClient Api { get; set; } = default!;

    private NegotiationReferenceData? _reference;
    private readonly List<EventProposalDto> _proposals = [];
    private readonly List<EventDto> _events = [];
    private readonly Dictionary<Guid, string> _acceptHeadcount = new();
    private readonly Dictionary<(Guid EventId, Guid TypeId), string> _capacityInputs = new();
    private readonly Dictionary<(Guid EventId, Guid TypeId), string> _capacityErrors = new();
    private readonly Dictionary<string, string> _fieldErrors = new(StringComparer.OrdinalIgnoreCase);
    private string? _proposalsCursor; private string? _eventsCursor;
    private ProposalForm? _dialog; private IdempotencySubmission? _submission;
    private bool _loading = true; private bool _busy;
    private string? _error; private string? _notice; private string? _scopeTypeName;
    private IReadOnlyList<LocationOption> LocationOptions => _reference?.Locations.Select(x => new LocationOption(x.Id, x.Name, x.Address, x.ZoneAbbreviation, x.IsActive)).ToArray() ?? [];
    private IReadOnlyList<TypeOption> TypeOptions => _reference?.AppointmentTypes.Select(x => new TypeOption(x.Id, x.Code, x.Name, x.IsActive, x.HasManager)).ToArray() ?? [];
    private string SelectedZoneId => _reference?.Locations.FirstOrDefault(x => x.Id == _dialog?.LocationId)?.TimeZoneId ?? "";
    private string EndPreview
    {
        get
        {
            if (_dialog is null || _reference is null) return "";
            var zone = _reference.Locations.FirstOrDefault(x => x.Id == _dialog.LocationId);
            if (zone is null) return "";
            var start = _dialog.Start;
            if (start.ToTimeSpan() + TimeSpan.FromMinutes(_dialog.DurationMinutes) >= TimeSpan.FromHours(24))
                return "That window runs past midnight — shorten it.";
            var end = start.Add(TimeSpan.FromMinutes(_dialog.DurationMinutes));
            return $"ends {end:HH:mm} {zone.ZoneAbbreviation}";
        }
    }
    private RenderFragment FieldError(string field) => __builder =>
    {
        if (_fieldErrors.TryGetValue(field, out var message))
        {
            __builder.OpenElement(0, "p"); __builder.AddAttribute(1, "class", "hint");
            __builder.AddAttribute(2, "role", "alert"); __builder.AddContent(3, message);
            __builder.CloseElement();
        }
    };
    protected override Task OnInitializedAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        _loading = true; _error = null;
        try
        {
            var reference = await Api.GetReferenceDataAsync(CancellationToken.None);
            if (!reference.IsSuccess || reference.Value is null) { _error = reference.ErrorMessage; _loading = false; return; }
            _reference = reference.Value;
            _scopeTypeName = _reference.AppointmentTypes.FirstOrDefault(x => x.Id == _reference.CallerAppointmentTypeId)?.Name;
            await LoadProposalsFirstPageAsync();
            await RefreshEventsAsync();
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _loading = false;
    }
    private async Task LoadProposalsFirstPageAsync()
    {
        var page = await Api.ListProposalsAsync(null, CancellationToken.None);
        if (!page.IsSuccess || page.Value is null) { _error = page.ErrorMessage; return; }
        _proposals.Clear(); _proposals.AddRange(page.Value.Items);
        _proposalsCursor = page.Value.NextCursor;
    }
    // Refreshes the confirmed events without undoing the Manager's view of them: every page
    // already loaded is read again, and _capacityInputs holds only unsaved edits, which a
    // refresh after saving (or accepting) somewhere else must not discard.
    private async Task RefreshEventsAsync()
    {
        var loaded = Math.Max(_events.Count, 1);
        var events = new List<EventDto>();
        string? cursor = null;
        do
        {
            var page = await Api.ListEventsAsync(null, null, null, cursor, CancellationToken.None);
            if (!page.IsSuccess || page.Value is null) { _error = page.ErrorMessage; return; }
            events.AddRange(page.Value.Items);
            cursor = page.Value.NextCursor;
        }
        while (cursor is not null && events.Count < loaded);
        _events.Clear(); _events.AddRange(events);
        _eventsCursor = cursor;
    }
    private async Task LoadMoreProposalsAsync() { _busy = true; try { var page = await Api.ListProposalsAsync(_proposalsCursor, CancellationToken.None); if (page.IsSuccess && page.Value is not null) { _proposals.AddRange(page.Value.Items); _proposalsCursor = page.Value.NextCursor; } else _error = page.ErrorMessage; } catch (Exception) { _error = "Something went wrong. Please try again."; } _busy = false; }
    private async Task LoadMoreEventsAsync() { _busy = true; try { var page = await Api.ListEventsAsync(null, null, null, _eventsCursor, CancellationToken.None); if (page.IsSuccess && page.Value is not null) { _events.AddRange(page.Value.Items); _eventsCursor = page.Value.NextCursor; } else _error = page.ErrorMessage; } catch (Exception) { _error = "Something went wrong. Please try again."; } _busy = false; }
    private string AcceptHeadcountFor(Guid id) => _acceptHeadcount.TryGetValue(id, out var value) ? value : "";
    private void SetAcceptHeadcount(Guid id, string? value) { if (value is not null) _acceptHeadcount[id] = value; }
    private string CapacityInputFor(Guid eventId, EventCapacityDto capacity) => _capacityInputs.TryGetValue((eventId, capacity.AppointmentTypeId), out var value) ? value : capacity.TotalHeadcount.ToString();
    private void SetCapacityInput(Guid eventId, Guid typeId, string? value) { if (value is not null) _capacityInputs[(eventId, typeId)] = value; }
    private string? CapacityErrorFor(Guid eventId, Guid typeId) => _capacityErrors.TryGetValue((eventId, typeId), out var value) ? value : null;
    private void OpenDialog()
    {
        if (_reference is null) return;
        _submission = IdempotencySubmission.Start();
        _fieldErrors.Clear();
        _dialog = new ProposalForm
        {
            LocationId = _reference.Locations.FirstOrDefault()?.Id ?? Guid.Empty,
            Date = DateOnly.FromDateTime(DateTime.Today),
            Start = new TimeOnly(9, 30),
            DurationMinutes = 90,
            TypeIds = [_reference.CallerAppointmentTypeId],
            Headcount = 1,
        };
    }
    private void CloseDialog() { _dialog = null; _submission = null; _fieldErrors.Clear(); }
    private async Task SubmitProposalAsync()
    {
        if (_dialog is null || _reference is null) return;
        _busy = true; _error = null; _fieldErrors.Clear();
        try
        {
            var result = await Api.ProposeAsync(new ProposeEventRequest(
                _dialog.LocationId, _dialog.Date, _dialog.Start, _dialog.DurationMinutes,
                _dialog.TypeIds, _dialog.Headcount), _submission!, CancellationToken.None);
            if (result.IsSuccess) { _busy = false; CloseDialog(); await LoadProposalsFirstPageAsync(); await RefreshEventsAsync(); return; }
            _error = result.ErrorMessage;
            if (result.Problem is { } problem)
                foreach (var item in problem.Errors)
                    if (item.Field is not null) _fieldErrors.TryAdd(item.Field, item.Message ?? item.Code);
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
    private async Task AcceptAsync(EventProposalDto proposal)
    {
        if (!int.TryParse(AcceptHeadcountFor(proposal.Id), out var headcount) || headcount < 1)
        {
            _error = "Enter a headcount of at least 1.";
            return;
        }
        _busy = true; _error = null; _notice = null;
        try
        {
            var result = await Api.RecordAcceptanceAsync(proposal.Id, headcount, CancellationToken.None);
            if (result.IsSuccess) { _busy = false; await LoadProposalsFirstPageAsync(); await RefreshEventsAsync(); return; }
            if (result.ErrorCode == "proposal-not-open")
            {
                _notice = "That proposal is no longer open; the board has been refreshed.";
                _busy = false;
                await LoadProposalsFirstPageAsync();
                return;
            }
            _error = result.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
    private async Task WithdrawAcceptanceAsync(EventProposalDto proposal)
    {
        _busy = true; _error = null; _notice = null;
        try
        {
            var result = await Api.WithdrawAcceptanceAsync(proposal.Id, CancellationToken.None);
            if (result.IsSuccess) { _busy = false; await LoadProposalsFirstPageAsync(); return; }
            if (result.ErrorCode == "proposal-not-open")
            {
                _notice = "That proposal is no longer open; the board has been refreshed.";
                _busy = false;
                await LoadProposalsFirstPageAsync();
                return;
            }
            _error = result.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
    private async Task WithdrawProposalAsync(EventProposalDto proposal)
    {
        _busy = true; _error = null; _notice = null;
        try
        {
            var result = await Api.WithdrawProposalAsync(proposal.Id, CancellationToken.None);
            if (result.IsSuccess) { _busy = false; await LoadProposalsFirstPageAsync(); return; }
            _error = result.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
    private async Task SaveCapacityAsync(EventDto evt, EventCapacityDto capacity)
    {
        var key = (evt.Id, capacity.AppointmentTypeId);
        if (!int.TryParse(CapacityInputFor(evt.Id, capacity), out var total) || total < 0)
        {
            _capacityErrors[key] = "Enter a total of 0 or more.";
            return;
        }
        _busy = true; _error = null;
        try
        {
            var result = await Api.AdjustCapacityAsync(evt.Id, capacity.AppointmentTypeId, total, CancellationToken.None);
            _capacityErrors.Remove(key);
            if (result.IsSuccess) { _capacityInputs.Remove(key); _busy = false; await RefreshEventsAsync(); return; }
            if (result.ErrorCode == "capacity-below-bookings" && result.Problem is { } problem)
            {
                var server = problem.Current?.GetProperty("totalHeadcount").GetInt32();
                _capacityErrors[key] = $"The minimum is {problem.Minimum}; the server total is {server}.";
                _busy = false;
                return;
            }
            _error = result.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
    private sealed class ProposalForm
    {
        public Guid LocationId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Start { get; set; }
        public int DurationMinutes { get; set; }
        public IReadOnlyList<Guid> TypeIds { get; set; } = [];
        public int Headcount { get; set; }
    }
}
