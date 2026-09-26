using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class ConfirmSelfRegistration
{
    [Inject] private IPublicEventGroupsClient Groups { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter]
    public string Token { get; set; } = string.Empty;

    private SelfRegistrationSummaryDto? _summary;
    private string? _error;
    private bool _loading = true;
    private bool _busy;
    private bool _confirmed;
    private readonly PendingSubmission _confirmSubmission = new();

    private string CoordinatorContact =>
        Services.GetService<AttendeePageOptions>()?.CoordinatorContact ?? "the recruitment team";

    protected override Task OnParametersSetAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        _loading = true;
        _summary = null;
        _error = null;
        _confirmed = false;

        try
        {
            var outcome = await Groups.ViewConfirmationAsync(Token, CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _summary = outcome.Value;
            }
            else if (outcome.ErrorCode == "already-confirmed")
            {
                _confirmed = true;
            }
            else
            {
                _error = outcome.ErrorMessage;
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

    private async Task ConfirmAsync()
    {
        if (_busy || _summary is null)
            return;

        _busy = true;
        _error = null;

        try
        {
            var outcome = await Groups.ConfirmAsync(
                Token, _confirmSubmission.For(Token), CancellationToken.None);
            if (outcome.IsSuccess)
            {
                _confirmSubmission.Complete();
                _confirmed = true;
                _summary = null;
                return;
            }

            if (outcome.ErrorCode == "already-confirmed")
            {
                _confirmSubmission.Complete();
                _confirmed = true;
                _summary = null;
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
