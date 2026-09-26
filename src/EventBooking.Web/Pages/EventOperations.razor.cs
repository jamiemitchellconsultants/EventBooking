using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class EventOperations
{
    [Inject] private IEventsClient Api { get; set; } = default!;

    private readonly List<EventDto> _events = [];
    private readonly List<(Guid Id, string Name)> _knownLocations = [];
    private Guid? _locationId; private DateOnly? _from; private DateOnly? _to; private string? _cursor;
    private Guid? _arming; private bool _loading = true; private bool _busy;
    private string? _error; private string? _confirmation;
    protected override Task OnInitializedAsync() => LoadFirstPageAsync();
    private async Task LoadFirstPageAsync()
    {
        _loading = true; _error = null;
        try
        {
            var page = await Api.ListEventsAsync(_locationId, _from, _to, null, CancellationToken.None);
            if (!page.IsSuccess || page.Value is null) { _error = page.ErrorMessage; _loading = false; return; }
            _events.Clear(); _events.AddRange(page.Value.Items);
            _cursor = page.Value.NextCursor;
            _knownLocations.Clear();
            _knownLocations.AddRange(_events.Select(x => (x.LocationId, x.LocationName)).Distinct().OrderBy(x => x.LocationName, StringComparer.Ordinal));
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _loading = false;
    }
    private Task LoadFirstPageAfterFilterAsync() => LoadFirstPageAsync();
    private async Task OnLocationFilterChanged(ChangeEventArgs args)
    {
        _locationId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : null;
        await LoadFirstPageAsync();
    }
    private async Task LoadMoreAsync()
    {
        _busy = true;
        try
        {
            var page = await Api.ListEventsAsync(_locationId, _from, _to, _cursor, CancellationToken.None);
            if (page.IsSuccess && page.Value is not null) { _events.AddRange(page.Value.Items); _cursor = page.Value.NextCursor; }
            else _error = page.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
    private async Task ConfirmCancelAsync(EventDto evt)
    {
        _busy = true; _error = null; _confirmation = null;
        try
        {
            var result = await Api.CancelAsync(evt.Id, true, CancellationToken.None);
            if (result.IsSuccess && result.Value is not null)
            {
                var outcome = result.Value;
                _confirmation = $"Cancelled {outcome.CancelledCount} bookings; {outcome.ReinvitedCount} re-invited, {outcome.AwaitingAvailabilityCount} awaiting availability.";
                _arming = null;
                _busy = false;
                await LoadFirstPageAsync();
                return;
            }
            _error = result.ErrorMessage;
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
}
