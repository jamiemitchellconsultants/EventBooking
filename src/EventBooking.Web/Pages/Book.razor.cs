using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class Book
{
    [Inject] private IBookingClient Booking { get; set; } = default!;
    [Inject] private IServiceProvider Services { get; set; } = default!;

    [Parameter]
    public string Token { get; set; } = string.Empty;

    private InviteDto? _invite;
    private ConfirmBookingOutcomeDto? _confirmed;
    private InviteOptionDto? _confirmedOption;
    private Guid? _chosen;
    private string? _error;
    private bool _expired;
    private bool _loading = true;
    private bool _busy;
    private readonly PendingSubmission _confirmSubmission = new();
    private int _requestVersion;

    private string CoordinatorContact =>
        Services.GetService<AttendeePageOptions>()?.CoordinatorContact ?? "the recruitment team";

    private string ManageUrl => _confirmed is null
        ? "/"
        : $"/manage/{Uri.EscapeDataString(_confirmed.ManageToken)}";

    private static string BookHeading(InviteDto invite) =>
        invite.IsRecovery switch
        {
            false => "Choose a time",
            true when invite.AppointmentTypeNames.Count == 1 =>
                "Choose a new time for your missed appointment",
            _ => "Choose a new time for your missed appointments",
        };

    protected override Task OnParametersSetAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        var version = ++_requestVersion;
        _loading = true;
        _busy = false;
        _invite = null;
        _confirmed = null;
        _confirmedOption = null;
        _chosen = null;
        _error = null;
        _expired = false;

        try
        {
            var outcome = await Booking.ViewInviteAsync(Token, CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            _invite = outcome.Value;
            _expired = outcome.StatusCode == 404;
            _error = _expired ? null : outcome.IsSuccess ? null : outcome.ErrorMessage;
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

    private async Task ConfirmAsync()
    {
        if (_chosen is null || _busy)
        {
            return;
        }

        var version = _requestVersion;
        var chosen = _chosen.Value;
        _busy = true;
        _error = null;

        try
        {
            var outcome = await Booking.ConfirmAsync(
                Token, chosen, _confirmSubmission.For(chosen), CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            if (outcome.IsSuccess)
            {
                _confirmSubmission.Complete();
                _confirmed = outcome.Value;
                _confirmedOption = _invite?.Options.SingleOrDefault(option => option.EventId == chosen);
                _invite = null;
                return;
            }

            if (_invite is not null
                && string.Equals(outcome.ErrorCode, "capacity-exhausted", StringComparison.Ordinal))
            {
                // A lost race removes only the filled option: the rest are still live,
                // so the attendee picks again without re-reading the whole offer.
                _invite = _invite with
                {
                    Options = _invite.Options.Where(option => option.EventId != chosen).ToArray(),
                };
                _chosen = null;
                _error = "That time has just filled up. Please choose another.";
                return;
            }

            _error = outcome.ErrorMessage;
            if (outcome.StatusCode == 409)
            {
                var conflictMessage = _error;
                await ReloadAsync();
                if (version + 1 == _requestVersion && !_expired)
                {
                    _error = conflictMessage;
                }
            }
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
