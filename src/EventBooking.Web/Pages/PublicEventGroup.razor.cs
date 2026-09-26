using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class PublicEventGroup
{
    [Inject] private IPublicEventGroupsClient Groups { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter]
    public Guid GroupId { get; set; }

    private PublicEventGroupDto? _group;
    private IReadOnlyList<PublicEventDto> _events = [];
    private string? _error;
    private bool _loading = true;

    private string CoordinatorContact =>
        Services.GetService<AttendeePageOptions>()?.CoordinatorContact ?? "the recruitment team";

    protected override Task OnParametersSetAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        _loading = true;
        _group = null;
        _events = [];
        _error = null;

        try
        {
            var outcome = await Groups.GetGroupAsync(GroupId, CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _group = outcome.Value;
                _events = [.. outcome.Value.Events
                    .OrderBy(x => x.Date)
                    .ThenBy(x => x.StartTime)];
            }
            else
            {
                _error = outcome.StatusCode == 404
                    ? "This group is no longer open. Please contact the recruitment team."
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
}
