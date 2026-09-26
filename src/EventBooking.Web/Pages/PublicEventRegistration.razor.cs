using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class PublicEventRegistration
{
    [Inject] private IPublicEventGroupsClient Groups { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter]
    public Guid GroupId { get; set; }

    [Parameter]
    public Guid EventId { get; set; }

    private PublicEventGroupDto? _group;
    private PublicEventDto? _event;
    private string? _error;
    private string _name = string.Empty;
    private string _email = string.Empty;
    private string _attendeeGroupId = string.Empty;
    private bool _loading = true;
    private bool _busy;
    private bool _sent;

    private string CoordinatorContact =>
        Services.GetService<AttendeePageOptions>()?.CoordinatorContact ?? "the recruitment team";

    protected override Task OnParametersSetAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        _loading = true;
        _group = null;
        _event = null;
        _error = null;
        _sent = false;

        try
        {
            var outcome = await Groups.GetGroupAsync(GroupId, CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _group = outcome.Value;
                _event = outcome.Value.Events.SingleOrDefault(x => x.Id == EventId);
                if (_event is null)
                    _error = "This time is no longer open.";
            }
            else
            {
                _error = outcome.StatusCode == 404
                    ? "This group is no longer open."
                    : outcome.ErrorMessage;
            }
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

    private async Task SendAsync()
    {
        if (_busy || _group is null || _event is null)
            return;

        _error = null;
        if (string.IsNullOrWhiteSpace(_name)
            || string.IsNullOrWhiteSpace(_email)
            || !_email.Contains('@')
            || !Guid.TryParse(_attendeeGroupId, out var attendeeGroupId))
        {
            _error = "Enter your name, a valid email address and your group.";
            return;
        }

        _busy = true;
        try
        {
            var outcome = await Groups.RequestAsync(
                GroupId, EventId, _name.Trim(), _email.Trim(), attendeeGroupId,
                CancellationToken.None);
            if (outcome.IsSuccess)
            {
                _sent = true;
                return;
            }

            _error = outcome.ErrorMessage;
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
}
