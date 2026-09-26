using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class ManageBooking
{
    [Inject] private IBookingClient Booking { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter]
    public string Token { get; set; } = string.Empty;

    private ManagedBookingDto? _booking;
    private string? _error;
    private bool _expired;
    private bool _loading = true;
    private bool _busy;
    private readonly PendingSubmission _cancelSubmission = new();
    private bool _cancelled;
    private string? _cancelOutcome;
    private int _requestVersion;

    private string CoordinatorContact =>
        Services.GetService<AttendeePageOptions>()?.CoordinatorContact ?? "the recruitment team";

    private string SupportContact =>
        Services.GetService<ProductOptions>()?.CoordinatorContact ?? CoordinatorContact;

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
        _cancelOutcome = null;

        try
        {
            var outcome = await Booking.ViewManagedAsync(Token, CancellationToken.None);
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
        _busy = true;
        _error = null;

        try
        {
            var outcome = await Booking.CancelAsync(
                Token, rebook, _cancelSubmission.For(rebook), CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            if (outcome.IsSuccess)
            {
                _cancelSubmission.Complete();
                _cancelOutcome = outcome.Value?.Outcome;
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
