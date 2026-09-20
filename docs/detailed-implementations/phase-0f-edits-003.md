# 00f — Retire the single-site configuration, edits 3 (Task 3d)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Web/Pages/Book.razor — 1/1

<!-- retirement-file: {"id":14,"file":"src/EventBooking.Web/Pages/Book.razor","beforeSha":"d1ab9a5ecd0aa08a66faee4dce3de57fef93caaff44306d19e244e11aee30451","afterSha":"704df056aea09548a1a6620fa1b532cf304d8be32f7b938d0eab70a4d3bfd27e","side":"before","part":1,"parts":1} -->

`````text
@page "/book/{Token}"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@using EventBooking.Web.Services
@layout EventBooking.Web.Layout.AttendeeLayout
@inject BookingClient Booking
@inject AttendeePageOptions PageOptions

<PageTitle>Choose a time</PageTitle>


<section class="booking-page" aria-busy="@_loading">
    @if (_loading)
    {
        <div class="booking-page__status" aria-label="Loading booking link">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
        </div>
    }
    else if (_confirmed is not null)
    {
        <div class="booking-page__intro">
            <h1>Booking confirmed</h1>
            <p class="booking-page__lead">You're booked in for:</p>
        </div>
        <div class="booking-page__status">
            @if (_confirmed.DeliveryStatus == "Sent")
            {
                <p class="booking-page__banner booking-page__banner--success" role="status"><strong>Booked.</strong> Your confirmation email has been sent.</p>
            }
            else
            {
                <p class="booking-page__banner booking-page__banner--success" role="status"><strong>Booked.</strong> Your booking is confirmed. We could not confirm email delivery; use your management link below.</p>
            }
            <p class="booking-page__confirmed-window">@_confirmed.Date.ToString("dddd, dd MMM yyyy") · @_confirmed.StartTime.ToString("HH\\:mm") – @_confirmed.EndTime.ToString("HH\\:mm")</p>
            @if (!string.IsNullOrWhiteSpace(_confirmed.TransitionalLocationAddress))
            {
                <p class="booking-page__note">Head office: @_confirmed.TransitionalLocationAddress</p>
            }
            <p class="booking-page__note">Need to change or cancel? <a href="@ManageUrl">Use your booking management link.</a></p>
        </div>
    }
    else if (_expired)
    {
        <div class="booking-page__status booking-page__expired">
            <p class="booking-page__note"><strong>This link has expired.</strong><br />Contact your coordinator at @PageOptions.CoordinatorContact and they’ll send you a fresh one.</p>
        </div>
    }
    else if (_invite is null)
    {
        <div class="booking-page__intro">
            <h1>We couldn’t load your booking link</h1>
        </div>
        <div class="booking-page__status">
            <p class="booking-page__banner" role="alert">@(_error ?? "Something went wrong. Please try again.")</p>
            <button class="booking-page__button" @onclick="ReloadAsync">Try again</button>
        </div>
    }
    else
    {
        <div class="booking-page__intro">
            <h1>@BookHeading(_invite)</h1>
            <p class="booking-page__lead">Hi <strong>@_invite.AttendeeName</strong> — pick a time for your <strong>@string.Join(" and ", _invite.AppointmentTypeNames)</strong> appointment.</p>
        </div>

        @if (_error is not null)
        {
            <p class="booking-page__banner" role="alert"><strong>Something went wrong confirming that time.</strong> @_error</p>
        }

        @if (_invite.Options.Count == 0)
        {
            <div class="booking-page__status">
                <p class="booking-page__note"><strong>Nothing fits right now.</strong> There’s no time currently open for your appointment type. Please contact @PageOptions.CoordinatorContact and the team will be in touch to arrange one.</p>
            </div>
        }
        else
        {
            <p class="booking-page__note booking-page__choose-hint">
                Each option is a four-hour window. Once you confirm, you can still cancel or move
                it using the link in your confirmation email.
            </p>

            <div class="booking-page__card">
                <fieldset class="booking-page__options">
                    <legend class="visually-hidden">Available appointment times</legend>
                    @foreach (var option in _invite.Options)
                    {
                        <label class="booking-page__option">
                            <input type="radio"
                                   name="event"
                                   checked="@(_chosen == option.EventId)"
                                   @onchange="() => _chosen = option.EventId" />
                            <span class="booking-page__option-copy">
                                <span class="booking-page__option-date">@option.Date.ToString("dddd, dd MMM yyyy")</span>
                                <span class="booking-page__option-time">@option.StartTime.ToString("HH\\:mm") – @option.EndTime.ToString("HH\\:mm")</span>
                            </span>
                        </label>
                    }
                </fieldset>
            </div>

            <button class="booking-page__button" @onclick="ConfirmAsync" disabled="@(_busy || _chosen is null)">
                @(_busy ? "Confirming…" : "Confirm this time")
            </button>
        }

        <p class="booking-page__note booking-page__footnote">This link is single-use and only works for you — no sign-in required. Please do not forward it.</p>
    }
</section>

@code {
    [Parameter]
    public string Token { get; set; } = string.Empty;

    private InviteDto? _invite;
    private ConfirmedBookingDto? _confirmed;
    private Guid? _chosen;
    private string? _error;
    private bool _expired;
    private bool _loading = true;
    private bool _busy;
    private int _requestVersion;

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
        _chosen = null;
        _error = null;
        _expired = false;

        try
        {
            var outcome = await Booking.GetInviteAsync(Token, CancellationToken.None);
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
            var outcome = await Booking.ConfirmAsync(Token, chosen, CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            if (outcome.IsSuccess)
            {
                _confirmed = outcome.Value;
                _invite = null;
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
`````

## after — src/EventBooking.Web/Pages/Book.razor — 1/1

<!-- retirement-file: {"id":14,"file":"src/EventBooking.Web/Pages/Book.razor","beforeSha":"d1ab9a5ecd0aa08a66faee4dce3de57fef93caaff44306d19e244e11aee30451","afterSha":"704df056aea09548a1a6620fa1b532cf304d8be32f7b938d0eab70a4d3bfd27e","side":"after","part":1,"parts":1} -->

`````text
@page "/book/{Token}"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@using EventBooking.Web.Services
@layout EventBooking.Web.Layout.AttendeeLayout
@inject BookingClient Booking
@inject AttendeePageOptions PageOptions

<PageTitle>Choose a time</PageTitle>


<section class="booking-page" aria-busy="@_loading">
    @if (_loading)
    {
        <div class="booking-page__status" aria-label="Loading booking link">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
        </div>
    }
    else if (_confirmed is not null)
    {
        <div class="booking-page__intro">
            <h1>Booking confirmed</h1>
            <p class="booking-page__lead">You're booked in for:</p>
        </div>
        <div class="booking-page__status">
            @if (_confirmed.DeliveryStatus == "Sent")
            {
                <p class="booking-page__banner booking-page__banner--success" role="status"><strong>Booked.</strong> Your confirmation email has been sent.</p>
            }
            else
            {
                <p class="booking-page__banner booking-page__banner--success" role="status"><strong>Booked.</strong> Your booking is confirmed. We could not confirm email delivery; use your management link below.</p>
            }
            <p class="booking-page__confirmed-window">@_confirmed.Date.ToString("dddd, dd MMM yyyy") · @_confirmed.StartTime.ToString("HH\\:mm") – @_confirmed.EndTime.ToString("HH\\:mm")</p>
            <p class="booking-page__note">Need to change or cancel? <a href="@ManageUrl">Use your booking management link.</a></p>
        </div>
    }
    else if (_expired)
    {
        <div class="booking-page__status booking-page__expired">
            <p class="booking-page__note"><strong>This link has expired.</strong><br />Contact your coordinator at @PageOptions.CoordinatorContact and they’ll send you a fresh one.</p>
        </div>
    }
    else if (_invite is null)
    {
        <div class="booking-page__intro">
            <h1>We couldn’t load your booking link</h1>
        </div>
        <div class="booking-page__status">
            <p class="booking-page__banner" role="alert">@(_error ?? "Something went wrong. Please try again.")</p>
            <button class="booking-page__button" @onclick="ReloadAsync">Try again</button>
        </div>
    }
    else
    {
        <div class="booking-page__intro">
            <h1>@BookHeading(_invite)</h1>
            <p class="booking-page__lead">Hi <strong>@_invite.AttendeeName</strong> — pick a time for your <strong>@string.Join(" and ", _invite.AppointmentTypeNames)</strong> appointment.</p>
        </div>

        @if (_error is not null)
        {
            <p class="booking-page__banner" role="alert"><strong>Something went wrong confirming that time.</strong> @_error</p>
        }

        @if (_invite.Options.Count == 0)
        {
            <div class="booking-page__status">
                <p class="booking-page__note"><strong>Nothing fits right now.</strong> There’s no time currently open for your appointment type. Please contact @PageOptions.CoordinatorContact and the team will be in touch to arrange one.</p>
            </div>
        }
        else
        {
            <p class="booking-page__note booking-page__choose-hint">
                Each option is a four-hour window. Once you confirm, you can still cancel or move
                it using the link in your confirmation email.
            </p>

            <div class="booking-page__card">
                <fieldset class="booking-page__options">
                    <legend class="visually-hidden">Available appointment times</legend>
                    @foreach (var option in _invite.Options)
                    {
                        <label class="booking-page__option">
                            <input type="radio"
                                   name="event"
                                   checked="@(_chosen == option.EventId)"
                                   @onchange="() => _chosen = option.EventId" />
                            <span class="booking-page__option-copy">
                                <span class="booking-page__option-date">@option.Date.ToString("dddd, dd MMM yyyy")</span>
                                <span class="booking-page__option-time">@option.StartTime.ToString("HH\\:mm") – @option.EndTime.ToString("HH\\:mm")</span>
                            </span>
                        </label>
                    }
                </fieldset>
            </div>

            <button class="booking-page__button" @onclick="ConfirmAsync" disabled="@(_busy || _chosen is null)">
                @(_busy ? "Confirming…" : "Confirm this time")
            </button>
        }

        <p class="booking-page__note booking-page__footnote">This link is single-use and only works for you — no sign-in required. Please do not forward it.</p>
    }
</section>

@code {
    [Parameter]
    public string Token { get; set; } = string.Empty;

    private InviteDto? _invite;
    private ConfirmedBookingDto? _confirmed;
    private Guid? _chosen;
    private string? _error;
    private bool _expired;
    private bool _loading = true;
    private bool _busy;
    private int _requestVersion;

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
        _chosen = null;
        _error = null;
        _expired = false;

        try
        {
            var outcome = await Booking.GetInviteAsync(Token, CancellationToken.None);
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
            var outcome = await Booking.ConfirmAsync(Token, chosen, CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            if (outcome.IsSuccess)
            {
                _confirmed = outcome.Value;
                _invite = null;
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
`````

## before — src/EventBooking.Web/Services/BookingClient.cs — 1/1

<!-- retirement-file: {"id":15,"file":"src/EventBooking.Web/Services/BookingClient.cs","beforeSha":"2c13d2480e709d053c6ac735fa14fff363f1e22cf21a50652c3894c5e20a270b","afterSha":"c0ce9daf8b88c3fc7d8800e8d9feb5559fbf6ac2e7bac407aa09fd866e837d8a","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record InviteOptionDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

public sealed record InviteDto(
    Guid InviteId,
    string AttendeeName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionDto> Options,
    bool IsRecovery = false);

/// <summary>Attendee-facing booking confirmation including the actual email outcome.</summary>
/// <param name="BookingId">The active booking identifier.</param>
/// <param name="Date">The event date.</param>
/// <param name="StartTime">The event start time.</param>
/// <param name="EndTime">The derived four-hour end time.</param>
/// <param name="ManageToken">The raw management token used by the attendee page.</param>
/// <param name="DeliveryStatus">The durable confirmation-email outcome.</param>
/// <param name="TransitionalLocationAddress">
/// The transitional-location address the API configured for attendee emails; empty when none is set.
/// </param>
public sealed record ConfirmedBookingDto(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending",
    string TransitionalLocationAddress = "");

public sealed record BookingDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string AttendeeName);

/// <summary>Attendee-facing cancellation result and replacement-delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or null when no replacement was requested.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when available.</param>
public sealed record CancelOutcomeDto(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Deployment strings the attendee pages need. Bound from the app's own settings file.</summary>
/// <param name="CoordinatorContact">The recruitment contact attendees are told to reach when a link fails.</param>
/// <remarks>
/// The transitional-location address is deliberately absent: it arrives on the booking confirmation from the
/// API, so the page and the confirmation email cannot name different addresses.
/// </remarks>
public sealed record AttendeePageOptions(string CoordinatorContact);

/// <summary>
/// Talks to the anonymous attendee routes. This client is deliberately constructed from the plain
/// named HTTP client: attendees authorise with their URL token, never an Entra ID access token.
/// </summary>
public sealed class BookingClient(HttpClient http)
{
    public const string ClientName = "EventBooking.Anonymous";

    public async Task<ApiOutcome<InviteDto>> GetInviteAsync(
        string token, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}", cancellationToken);

        return await ApiCall.ReadAsync<InviteDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<ConfirmedBookingDto>> ConfirmAsync(
        string token, Guid eventId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}/confirm",
            new { EventId = eventId },
            cancellationToken);

        return await ApiCall.ReadAsync<ConfirmedBookingDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<BookingDto>> GetBookingAsync(
        string manageToken, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}", cancellationToken);

        return await ApiCall.ReadAsync<BookingDto>(response, cancellationToken);
    }

    /// <summary>Cancels or rebooks through the anonymous manage-token route.</summary>
    public async Task<ApiOutcome<CancelOutcomeDto>> CancelAsync(
        string manageToken, bool rebook, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}/cancel",
            new { Rebook = rebook },
            cancellationToken);

        var outcome = await ApiCall.ReadAsync<CancelOutcomeDto>(response, cancellationToken);

        return outcome;
    }
}
`````

## after — src/EventBooking.Web/Services/BookingClient.cs — 1/1

<!-- retirement-file: {"id":15,"file":"src/EventBooking.Web/Services/BookingClient.cs","beforeSha":"2c13d2480e709d053c6ac735fa14fff363f1e22cf21a50652c3894c5e20a270b","afterSha":"c0ce9daf8b88c3fc7d8800e8d9feb5559fbf6ac2e7bac407aa09fd866e837d8a","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record InviteOptionDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

public sealed record InviteDto(
    Guid InviteId,
    string AttendeeName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionDto> Options,
    bool IsRecovery = false);

/// <summary>Attendee-facing booking confirmation including the actual email outcome.</summary>
/// <param name="BookingId">The active booking identifier.</param>
/// <param name="Date">The event date.</param>
/// <param name="StartTime">The event start time.</param>
/// <param name="EndTime">The derived four-hour end time.</param>
/// <param name="ManageToken">The raw management token used by the attendee page.</param>
/// <param name="DeliveryStatus">The durable confirmation-email outcome.</param>
public sealed record ConfirmedBookingDto(
    Guid BookingId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string ManageToken,
    string DeliveryStatus = "Pending");

public sealed record BookingDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display,
    string AttendeeName);

/// <summary>Attendee-facing cancellation result and replacement-delivery outcome.</summary>
/// <param name="Reinvited">Whether a replacement invite was created.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or null when no replacement was requested.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when available.</param>
public sealed record CancelOutcomeDto(
    bool Reinvited,
    bool InviteCreated = false,
    string? DeliveryStatus = null,
    Guid? DeliveryId = null);

/// <summary>Deployment strings the attendee pages need. Bound from the app's own settings file.</summary>
/// <param name="CoordinatorContact">The recruitment contact attendees are told to reach when a link fails.</param>
/// <remarks>
/// The transitional-location address is deliberately absent: it arrives on the booking confirmation from the
/// API, so the page and the confirmation email cannot name different addresses.
/// </remarks>
public sealed record AttendeePageOptions(string CoordinatorContact);

/// <summary>
/// Talks to the anonymous attendee routes. This client is deliberately constructed from the plain
/// named HTTP client: attendees authorise with their URL token, never an Entra ID access token.
/// </summary>
public sealed class BookingClient(HttpClient http)
{
    public const string ClientName = "EventBooking.Anonymous";

    public async Task<ApiOutcome<InviteDto>> GetInviteAsync(
        string token, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}", cancellationToken);

        return await ApiCall.ReadAsync<InviteDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<ConfirmedBookingDto>> ConfirmAsync(
        string token, Guid eventId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/{Uri.EscapeDataString(token)}/confirm",
            new { EventId = eventId },
            cancellationToken);

        return await ApiCall.ReadAsync<ConfirmedBookingDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<BookingDto>> GetBookingAsync(
        string manageToken, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}", cancellationToken);

        return await ApiCall.ReadAsync<BookingDto>(response, cancellationToken);
    }

    /// <summary>Cancels or rebooks through the anonymous manage-token route.</summary>
    public async Task<ApiOutcome<CancelOutcomeDto>> CancelAsync(
        string manageToken, bool rebook, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/booking/manage/{Uri.EscapeDataString(manageToken)}/cancel",
            new { Rebook = rebook },
            cancellationToken);

        var outcome = await ApiCall.ReadAsync<CancelOutcomeDto>(response, cancellationToken);

        return outcome;
    }
}
`````

## before — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":16,"file":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"d84f30f27c86a84153ae109466cd7bef51a2ce60dbd355dc9cd06d1fe9100d4f","afterSha":"524a12fb368a2c0ac770619b69c68920fa2b54b10c88764ccb6e755b7c400063","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A attendee can confirm one of the offered events.</summary>
    [Fact]
    public async Task AAttendeeCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen.EventId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>The confirmation names the API's configured transitional location, the one the email uses.</summary>
    [Fact]
    public async Task TheConfirmationCarriesTheConfiguredTransitionalLocationAddress()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = view!.Options[0].EventId });

        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        var configured = factory.Services.GetRequiredService<AttendeePortalOptions>().TransitionalLocationAddress;
        Assert.False(string.IsNullOrWhiteSpace(configured));
        Assert.Equal(configured, outcome!.TransitionalLocationAddress);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = view!.Options[0].EventId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].EventId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A event absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingAEventThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = invite.UnofferedEventId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus,
        string TransitionalLocationAddress);

    private async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredEventIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(date, startTime), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0)), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedEventId = Guid.NewGuid();
        context.EventProposals.Add(unofferedProposal);
        context.Events.Add(Event.CreateFrom(unofferedEventId, unofferedProposal));

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            offeredEventIds,
            attendee.RequiredAppointmentTypeIds, 0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedEventId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedEventId);
}
`````

## after — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":16,"file":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"d84f30f27c86a84153ae109466cd7bef51a2ce60dbd355dc9cd06d1fe9100d4f","afterSha":"524a12fb368a2c0ac770619b69c68920fa2b54b10c88764ccb6e755b7c400063","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A attendee can confirm one of the offered events.</summary>
    [Fact]
    public async Task AAttendeeCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen.EventId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = view!.Options[0].EventId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].EventId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A event absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingAEventThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = invite.UnofferedEventId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus);

    private async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredEventIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(date, startTime), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0)), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedEventId = Guid.NewGuid();
        context.EventProposals.Add(unofferedProposal);
        context.Events.Add(Event.CreateFrom(unofferedEventId, unofferedProposal));

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            offeredEventIds,
            attendee.RequiredAppointmentTypeIds, 0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedEventId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedEventId);
}
`````

## before — tests/EventBooking.Api.Tests/HealthTests.cs — 1/1

<!-- retirement-file: {"id":17,"file":"tests/EventBooking.Api.Tests/HealthTests.cs","beforeSha":"8dea831ce12a68db79ae37c9e070e4e94ecb81f7c01fe9e356a398a149b7ce9a","afterSha":"4fea52a1994148a28ceee6c978d889c3fe47118f9932d1e1f779f2f97794b169","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task TheHostStartsAndAnswersHealthAnonymously()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void MissingConfigurationNamesEverySettingThatIsAbsent()
    {
        var empty = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => EventBookingConfiguration.Read(empty));

        Assert.Contains("ConnectionStrings:EventBooking", ex.Message);
        Assert.Contains("Tokens:SigningKey", ex.Message);
        Assert.Contains("Portal:BaseUrl", ex.Message);
        Assert.Contains("Auth:Provider", ex.Message);
        Assert.Contains("Email:Provider", ex.Message);
    }

    /// <summary>Ensures only the documented exact email-provider literals are accepted.</summary>
    [Theory]
    [InlineData("Fax")]
    [InlineData("999")]
    [InlineData("ses")]
    [InlineData("smtp")]
    public void AnInvalidEmailProviderIsRejectedWithAReadableMessage(string emailProvider)
    {
        var configuration = ConfigurationWith(emailProvider: emailProvider);

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Email:Provider", ex.Message);
    }

    [Fact]
    public void AnInvalidAuthProviderIsRejectedWithAReadableMessage()
    {
        var configuration = ConfigurationWith(authProvider: "Auth0");

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Auth:Provider", ex.Message);
    }

    /// <summary>Every required key present and valid, except the one override under test.</summary>
    private static IConfiguration ConfigurationWith(
        string emailProvider = "Smtp", string authProvider = "Local") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=x;Username=x;Password=x",
                ["TransitionalLocation:TimeZoneId"] = "Europe/London",
                ["TransitionalLocation:Address"] = "1 Example Street",
                ["Tokens:SigningKey"] = "a-signing-key-that-is-long-enough-to-be-safe",
                ["Email:FromAddress"] = "recruitment@example.com",
                ["Email:FromName"] = "Recruitment Team",
                ["Email:Provider"] = emailProvider,
                ["Auth:Provider"] = authProvider,
                ["Portal:BaseUrl"] = "https://localhost:5001",
                ["Portal:CoordinatorContact"] = "recruitment@example.com",
            })
            .Build();
}
`````

## after — tests/EventBooking.Api.Tests/HealthTests.cs — 1/1

<!-- retirement-file: {"id":17,"file":"tests/EventBooking.Api.Tests/HealthTests.cs","beforeSha":"8dea831ce12a68db79ae37c9e070e4e94ecb81f7c01fe9e356a398a149b7ce9a","afterSha":"4fea52a1994148a28ceee6c978d889c3fe47118f9932d1e1f779f2f97794b169","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task TheHostStartsAndAnswersHealthAnonymously()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void MissingConfigurationNamesEverySettingThatIsAbsent()
    {
        var empty = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => EventBookingConfiguration.Read(empty));

        Assert.Contains("ConnectionStrings:EventBooking", ex.Message);
        Assert.Contains("Tokens:SigningKey", ex.Message);
        Assert.Contains("Portal:BaseUrl", ex.Message);
        Assert.Contains("Auth:Provider", ex.Message);
        Assert.Contains("Email:Provider", ex.Message);
    }

    /// <summary>Ensures only the documented exact email-provider literals are accepted.</summary>
    [Theory]
    [InlineData("Fax")]
    [InlineData("999")]
    [InlineData("ses")]
    [InlineData("smtp")]
    public void AnInvalidEmailProviderIsRejectedWithAReadableMessage(string emailProvider)
    {
        var configuration = ConfigurationWith(emailProvider: emailProvider);

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Email:Provider", ex.Message);
    }

    [Fact]
    public void AnInvalidAuthProviderIsRejectedWithAReadableMessage()
    {
        var configuration = ConfigurationWith(authProvider: "Auth0");

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Auth:Provider", ex.Message);
    }

    /// <summary>Every required key present and valid, except the one override under test.</summary>
    private static IConfiguration ConfigurationWith(
        string emailProvider = "Smtp", string authProvider = "Local") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=x;Username=x;Password=x",
                ["Clock:TimeZoneId"] = "Europe/London",
                ["Tokens:SigningKey"] = "a-signing-key-that-is-long-enough-to-be-safe",
                ["Email:FromAddress"] = "recruitment@example.com",
                ["Email:FromName"] = "Recruitment Team",
                ["Email:Provider"] = emailProvider,
                ["Auth:Provider"] = authProvider,
                ["Portal:BaseUrl"] = "https://localhost:5001",
                ["Portal:CoordinatorContact"] = "recruitment@example.com",
            })
            .Build();
}
`````

## after — tests/EventBooking.Api.Tests/RetiredLocationConfigurationTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Api.Tests/RetiredLocationConfigurationTests.cs","beforeSha":null,"afterSha":"a2103cd9883496c5b49f0b03bf55131ca53de637aa67321197b0e30c966b470b","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Time;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

public sealed class RetiredLocationConfigurationTests
{
    [Fact]
    public void Startup_configuration_needs_no_retired_location_section()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Clock:TimeZoneId"] = "Europe/London",
            ["Tokens:SigningKey"] = "a-test-signing-key-that-is-at-least-32-characters",
            ["Email:FromAddress"] = "test@example.test",
            ["Email:FromName"] = "Test sender",
            ["Email:Provider"] = "Smtp",
            ["Auth:Provider"] = "Local",
            ["Portal:BaseUrl"] = "http://localhost:5002",
            ["Portal:CoordinatorContact"] = "help@example.test",
        }).Build();

        var values = EventBookingConfiguration.Read(configuration);

        Assert.Equal("http://localhost:5002", values.Portal.BaseUrl);
        Assert.DoesNotContain(typeof(SystemClock).Assembly.GetTypes(),
            type => type.Name is "TransitionalLocationOptions" or "HeadOfficeOptions");
        Assert.DoesNotContain(typeof(AttendeePortalOptions).GetProperties(),
            property => property.Name.EndsWith("LocationAddress", StringComparison.Ordinal));
    }
}
`````
