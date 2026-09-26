using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class Appointments
{
    [Inject] private IAppointmentsClient Api { get; set; } = default!;
    [Inject] private Microsoft.JSInterop.IJSRuntime JS { get; set; } = default!;

    private WorkspaceContextDto? _context;
    private readonly List<WorkspaceEventDto> _events = [];
    private readonly List<WorkspaceRosterRowDto> _rows = [];
    private Guid? _selectedEventId;
    private bool _loading = true; private bool _busy; private bool _rosterLoading; private bool _downloading;
    private string? _error; private string? _announcement;
    private static string EventLabel(WorkspaceEventDto evt) =>
        $"{evt.Time.StartLocal:ddd d MMM yyyy, HH:mm}–{evt.Time.EndLocal:HH:mm} {evt.Time.ZoneAbbreviation}";
    private static string StatusDisplay(string status) => status switch
    {
        "CheckedIn" => "Checked in",
        "NoShow" => "No-show",
        _ => status,
    };
    protected override Task OnInitializedAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        _loading = true; _error = null;
        try
        {
            var context = await Api.GetContextAsync(CancellationToken.None);
            if (!context.IsSuccess || context.Value is null) { _error = context.ErrorMessage; _loading = false; return; }
            _context = context.Value;
            var events = await Api.ListEventsAsync(null, CancellationToken.None);
            if (!events.IsSuccess || events.Value is null) { _error = events.ErrorMessage; _loading = false; return; }
            _events.Clear(); _events.AddRange(events.Value.Items);
            _selectedEventId = _events.FirstOrDefault()?.EventId;
            _loading = false;
            await LoadRosterAsync();
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; _loading = false; }
    }
    private async Task OnEventChanged(ChangeEventArgs args)
    {
        _selectedEventId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : null;
        await LoadRosterAsync();
    }
    private async Task LoadRosterAsync()
    {
        if (_selectedEventId is null) { _rows.Clear(); return; }
        _rosterLoading = true; _error = null;
        try
        {
            var roster = await Api.GetRosterAsync(_selectedEventId.Value, CancellationToken.None);
            if (!roster.IsSuccess || roster.Value is null) { _error = roster.ErrorMessage; _rosterLoading = false; return; }
            _rows.Clear(); _rows.AddRange(roster.Value.Items);
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _rosterLoading = false;
    }
    private async Task DownloadRosterAsync()
    {
        if (_selectedEventId is null || _downloading) return;
        _downloading = true; _error = null;
        try
        {
            var file = await Api.DownloadRosterCsvAsync(_selectedEventId.Value, CancellationToken.None);
            if (file.IsSuccess && file.Value is not null)
                await JS.InvokeVoidAsync("saveTextFile", file.Value.FileName, file.Value.Content);
            else
                _error = file.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _downloading = false;
    }
    private async Task SetStatusAsync(WorkspaceRosterRowDto row, string targetStatus)
    {
        _busy = true; _error = null; _announcement = null;
        try
        {
            var result = await Api.SetStatusAsync(row.AppointmentId, targetStatus, row.Version, CancellationToken.None);
            if (result.IsSuccess) { _busy = false; await LoadRosterAsync(); return; }
            if (result.ErrorCode == "version-conflict")
            {
                await LoadRosterAsync();
                _announcement = $"{row.Name} changed elsewhere; the row has been refreshed.";
                _busy = false;
                return;
            }
            _error = result.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
}
