using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class Dashboards
{
    [Inject] private IDashboardsClient DashboardsApi { get; set; } = default!;
    [Inject] private IAttendeesClient AttendeesApi { get; set; } = default!;

    private const string UnexpectedError = "Something went wrong. Please try again.";

    private enum Tab { Awaiting, NoResponse, Events }

    private static readonly Tab[] TabOrder = [Tab.Awaiting, Tab.NoResponse, Tab.Events];

    private DashboardsDto? _data;
    private int FailedEmailCount => _data?.FailedEmails ?? 0;
    private int PendingEmailCount => _data?.PendingEmails ?? 0;
    private Tab _tab = Tab.Awaiting;
    private string? _error;
    private bool _busy;
    private readonly PendingSubmission _reinviteSubmission = new();
    private bool _isLoading = true;
    private Guid? _locationId;
    private readonly List<(Guid Id, string Name)> _knownLocations = [];

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

    private async Task OnLocationFilterChanged(ChangeEventArgs args)
    {
        _locationId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : null;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;

        try
        {
            var outcome = await DashboardsApi.GetAsync(_locationId, CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _data = outcome.Value;
                _error = null;
                _knownLocations.Clear();
                _knownLocations.AddRange(_data.Events.Rows
                    .Select(x => (x.LocationId, x.LocationName)).Distinct()
                    .OrderBy(x => x.LocationName, StringComparer.Ordinal));
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
            var outcome = await AttendeesApi.InviteAsync(
                attendeeId, [], _reinviteSubmission.For(attendeeId), CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _reinviteSubmission.Complete();

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
}
