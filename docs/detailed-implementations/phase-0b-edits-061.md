# 00b — Vocabulary edits 61 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Web/Pages/Help.razor — 1/1

<!-- vocabulary-file: {"id":197,"oldPath":"src/EventBooking.Web/Pages/Help.razor","newPath":"src/EventBooking.Web/Pages/Help.razor","beforeSha":"03a75e6f69aa1300ab3f26ee49479e3846acf417d66e1b0bd2f4a30c55384a1b","afterSha":"4ca4ea9f6274af9f60033be88f4ee603675848093649bde673161a22d7efe6a9","side":"after","part":1,"parts":1} -->

`````razor
@page "/help"
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services

<PageTitle>Help</PageTitle>

<AuthorizeView>
    <Authorized>
        <section class="page help-page" aria-labelledby="help-heading" aria-busy="@(Loading ? "true" : "false")">
            <div class="page-header">
                <div>
                    <span class="eyebrow">User guide</span>
                    <h1 id="help-heading">Help</h1>
                    <p>Guidance for the roles on your profile.</p>
                </div>
            </div>

            @if (Loading)
            {
                <div class="card loading-block" role="status">
                    <span class="loading-line loading-line-short"></span>
                    <span class="loading-line"></span>
                    <span class="loading-line loading-line-medium"></span>
                    <span class="visually-hidden">Loading…</span>
                </div>
            }
            else if (Error is not null)
            {
                <p class="landing-hint">@Error</p>
            }
            else if (Guides.Count == 0)
            {
                <p class="landing-hint">
                    Your account has not been assigned a role yet. Contact an administrator to be given
                    access as Manager, Coordinator, Admin, or Appointment staff.
                </p>
            }
            else
            {
                @if (Guides.Count > 1)
                {
                    <nav class="card guide-toc" aria-label="Guides on this page">
                        <ul>
                            @foreach (var guide in Guides)
                            {
                                <li><a href="/help#@guide.AnchorId">@guide.Title</a></li>
                            }
                        </ul>
                    </nav>
                }

                @foreach (var guide in Guides)
                {
                    <section class="card guide-section" id="@guide.AnchorId" aria-label="@guide.Title">
                        @((MarkupString)guide.Html)
                    </section>
                }
            }
        </section>
    </Authorized>
    <NotAuthorized>
        <section class="page help-page" aria-labelledby="help-heading">
            <div class="page-header">
                <div>
                    <span class="eyebrow">User guide</span>
                    <h1 id="help-heading">Help</h1>
                    <p>Guidance for booking and managing your appointment.</p>
                </div>
            </div>
            <section class="card guide-section" id="@AttendeeGuide.AnchorId" aria-label="@AttendeeGuide.Title">
                @((MarkupString)AttendeeGuide.Html)
            </section>
        </section>
    </NotAuthorized>
</AuthorizeView>

@code {
    // Cascaded by MainLayout, which fetches it once for both the nav bar and Home — reused here
    // rather than issued as a second concurrent call to the same token-protected endpoint.
    [CascadingParameter]
    private ApiOutcome<MeDto>? MeOutcome { get; set; }

    private static readonly UserGuide AttendeeGuide = UserGuideCatalog.AttendeeGuide();

    private bool Loading => MeOutcome is null;
    private string? Error => MeOutcome is { IsSuccess: false } ? MeOutcome.ErrorMessage : null;
    private IReadOnlyList<UserGuide> Guides =>
        MeOutcome?.Value is { } me ? UserGuideCatalog.GuidesFor(me.Roles) : [];
}
`````

## before — src/EventBooking.Web/Pages/Home.razor — 1/1

<!-- vocabulary-file: {"id":198,"oldPath":"src/EventBooking.Web/Pages/Home.razor","newPath":"src/EventBooking.Web/Pages/Home.razor","beforeSha":"04fb6736a76dda6d6d8e9440dfad1b5b859111c802ba94e1ad6255d3bef593f1","afterSha":"5899c760c7916601866e1535ea97f7cf366942c34db30fa1ca946ffd97390df8","side":"before","part":1,"parts":1} -->

`````razor
@page "/"
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services

<PageTitle>EventBooking</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="landing">
            <div class="landing-intro">
                <span class="eyebrow">British Airways staff workspace</span>
                <h1>Hi @context.User.Identity?.Name</h1>
                <p>Everything below is scoped to the roles you hold. Pick a workspace to carry on.</p>
            </div>

            @if (Loading)
            {
                <div class="card loading-block" role="status">
                    <span class="loading-line loading-line-short"></span>
                    <span class="loading-line"></span>
                    <span class="loading-line loading-line-medium"></span>
                    <span class="visually-hidden">Loading…</span>
                </div>
            }
            else if (Error is not null)
            {
                <p class="landing-hint">@Error</p>
            }
            else if (Me is null || Me.Roles.Count == 0)
            {
                <p class="landing-hint">
                    Your account has not been assigned a role yet. Contact an administrator to be given
                    access as Manager, Coordinator, Admin, or Appointment staff.
                </p>
            }
            else
            {
                <div class="access-summary" aria-label="Your access">
                    <p>
                        Roles: @string.Join(", ", Me.Roles)
                        @if (Me.AppointmentTypeName is not null)
                        {
                            <span> · Appointment type: @Me.AppointmentTypeName</span>
                        }
                        <span class="tip" tabindex="0" role="note"
                              aria-label="Your roles decide which workspaces appear below. An appointment type scopes what you see to that one type of appointment."
                              data-tip="Your roles decide which workspaces appear below. An appointment type scopes what you see to that one type of appointment."></span>
                    </p>
                </div>

                @if (StaffNavigation.LinksFor(Me).Count == 0)
                {
                    <p class="landing-hint">
                        Your appointment workspace will appear here when appointment delivery is enabled.
                    </p>
                }
                else
                {
                    <nav class="landing-links" aria-label="Workspace sections">
                        @foreach (var link in StaffNavigation.LinksFor(Me))
                        {
                            <a class="link-card" href="@link.Href">
                                <span>@link.Label<span class="landing-sub">@link.Description</span></span>
                                <span class="arrow" aria-hidden="true">→</span>
                            </a>
                        }
                        <a class="link-card" href="/help">
                            <span>Help<span class="landing-sub">Read the guide for your role</span></span>
                            <span class="arrow" aria-hidden="true">→</span>
                        </a>
                    </nav>
                }
            }
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="landing landing-signed-out">
            <div class="landing-intro">
                <span class="eyebrow">British Airways staff workspace</span>
                <h1>EventBooking</h1>
                <p>Coordinate candidate appointments across drug &amp; alcohol testing, medical check-ups and uniform fittings — without overbooking anyone.</p>
                <a class="sign-in-button" href="authentication/login">Sign in</a>
                <p class="hint">Candidates do not sign in — they use the personal booking link emailed to them.</p>
                <p class="hint"><a href="/help">Read the candidate guide</a></p>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    // Fetched once by MainLayout and cascaded here, rather than fetched again by this page —
    // two concurrent calls to the same token-protected endpoint on first load raced the WASM
    // auth token acquisition and intermittently failed one of them.
    [CascadingParameter]
    private ApiOutcome<MeDto>? MeOutcome { get; set; }

    private bool Loading => MeOutcome is null;
    private string? Error => MeOutcome is { IsSuccess: false } ? MeOutcome.ErrorMessage : null;
    private MeDto? Me => MeOutcome?.Value;
}
`````

## after — src/EventBooking.Web/Pages/Home.razor — 1/1

<!-- vocabulary-file: {"id":198,"oldPath":"src/EventBooking.Web/Pages/Home.razor","newPath":"src/EventBooking.Web/Pages/Home.razor","beforeSha":"04fb6736a76dda6d6d8e9440dfad1b5b859111c802ba94e1ad6255d3bef593f1","afterSha":"5899c760c7916601866e1535ea97f7cf366942c34db30fa1ca946ffd97390df8","side":"after","part":1,"parts":1} -->

`````razor
@page "/"
@using Microsoft.AspNetCore.Components.Authorization
@using EventBooking.Web.Services

<PageTitle>EventBooking</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="landing">
            <div class="landing-intro">
                <span class="eyebrow">British Airways staff workspace</span>
                <h1>Hi @context.User.Identity?.Name</h1>
                <p>Everything below is scoped to the roles you hold. Pick a workspace to carry on.</p>
            </div>

            @if (Loading)
            {
                <div class="card loading-block" role="status">
                    <span class="loading-line loading-line-short"></span>
                    <span class="loading-line"></span>
                    <span class="loading-line loading-line-medium"></span>
                    <span class="visually-hidden">Loading…</span>
                </div>
            }
            else if (Error is not null)
            {
                <p class="landing-hint">@Error</p>
            }
            else if (Me is null || Me.Roles.Count == 0)
            {
                <p class="landing-hint">
                    Your account has not been assigned a role yet. Contact an administrator to be given
                    access as Manager, Coordinator, Admin, or Appointment staff.
                </p>
            }
            else
            {
                <div class="access-summary" aria-label="Your access">
                    <p>
                        Roles: @string.Join(", ", Me.Roles)
                        @if (Me.AppointmentTypeName is not null)
                        {
                            <span> · Appointment type: @Me.AppointmentTypeName</span>
                        }
                        <span class="tip" tabindex="0" role="note"
                              aria-label="Your roles decide which workspaces appear below. An appointment type scopes what you see to that one type of appointment."
                              data-tip="Your roles decide which workspaces appear below. An appointment type scopes what you see to that one type of appointment."></span>
                    </p>
                </div>

                @if (StaffNavigation.LinksFor(Me).Count == 0)
                {
                    <p class="landing-hint">
                        Your appointment workspace will appear here when appointment delivery is enabled.
                    </p>
                }
                else
                {
                    <nav class="landing-links" aria-label="Workspace sections">
                        @foreach (var link in StaffNavigation.LinksFor(Me))
                        {
                            <a class="link-card" href="@link.Href">
                                <span>@link.Label<span class="landing-sub">@link.Description</span></span>
                                <span class="arrow" aria-hidden="true">→</span>
                            </a>
                        }
                        <a class="link-card" href="/help">
                            <span>Help<span class="landing-sub">Read the guide for your role</span></span>
                            <span class="arrow" aria-hidden="true">→</span>
                        </a>
                    </nav>
                }
            }
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="landing landing-signed-out">
            <div class="landing-intro">
                <span class="eyebrow">British Airways staff workspace</span>
                <h1>EventBooking</h1>
                <p>Coordinate attendee appointments across drug &amp; alcohol testing, medical check-ups and uniform fittings — without overbooking anyone.</p>
                <a class="sign-in-button" href="authentication/login">Sign in</a>
                <p class="hint">Attendees do not sign in — they use the personal booking link emailed to them.</p>
                <p class="hint"><a href="/help">Read the attendee guide</a></p>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    // Fetched once by MainLayout and cascaded here, rather than fetched again by this page —
    // two concurrent calls to the same token-protected endpoint on first load raced the WASM
    // auth token acquisition and intermittently failed one of them.
    [CascadingParameter]
    private ApiOutcome<MeDto>? MeOutcome { get; set; }

    private bool Loading => MeOutcome is null;
    private string? Error => MeOutcome is { IsSuccess: false } ? MeOutcome.ErrorMessage : null;
    private MeDto? Me => MeOutcome?.Value;
}
`````

## before — src/EventBooking.Web/Pages/ManageBooking.razor — 1/1

<!-- vocabulary-file: {"id":199,"oldPath":"src/EventBooking.Web/Pages/ManageBooking.razor","newPath":"src/EventBooking.Web/Pages/ManageBooking.razor","beforeSha":"7fc727d9d0e6ce986913e6194ded74029f80ffd182c9b577d37ce430e71d2c7c","afterSha":"5eeddcbe90b7d8691b894513dd1d626f50f251ca4b583b55f7e986cf653fe937","side":"before","part":1,"parts":1} -->

`````razor
@page "/manage/{Token}"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@using EventBooking.Web.Services
@layout EventBooking.Web.Layout.CandidateLayout
@inject BookingClient Booking
@inject CandidatePageOptions PageOptions

<PageTitle>Manage your booking</PageTitle>


<section class="manage-booking-page" aria-busy="@(_loading || _busy)">
    @if (_loading)
    {
        <div class="manage-booking-page__status" aria-label="Loading booking details">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
        </div>
    }
    else if (_cancelled)
    {
        <div class="manage-booking-page__intro">
            <h1>Booking cancelled</h1>
        </div>
        <div class="manage-booking-page__status manage-booking-page__status--cancelled" role="status">
            @if (_reinvited && _deliveryStatus == "Sent")
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled and a new invitation with fresh times has been sent.</p>
            }
            else if (_rebookRequested)
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled. @(_inviteCreated ? "We could not confirm delivery of a replacement invitation; " : "There are no times available right now; ")the recruitment team will be in touch with the next available times.</p>
            }
            else
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled. If that was a mistake, contact the recruitment team at @PageOptions.CoordinatorContact.</p>
            }
        </div>
    }
    else if (_expired)
    {
        <div class="manage-booking-page__status manage-booking-page__status--expired">
            <h1>This link has expired</h1>
            <p class="manage-booking-page__note">This booking link is no longer valid. Please contact the recruitment team at @PageOptions.CoordinatorContact for help.</p>
        </div>
    }
    else if (_booking is null)
    {
        <div class="manage-booking-page__intro">
            <h1>We couldn’t load your booking</h1>
        </div>
        <div class="manage-booking-page__status">
            <p class="manage-booking-page__banner" role="alert">@(_error ?? "Something went wrong. Please try again.")</p>
            <button class="manage-booking-page__button" @onclick="ReloadAsync">Try again</button>
        </div>
    }
    else
    {
        <div class="manage-booking-page__intro">
            <h1>Manage your booking</h1>
            <p class="manage-booking-page__lead">Review your appointment or cancel it below.</p>
        </div>

        @if (_error is not null)
        {
            <p class="manage-booking-page__banner" role="alert"><strong>We couldn’t cancel that booking.</strong> @_error</p>
        }

        <div class="manage-booking-page__card">
            <span class="manage-booking-page__label">Your appointment</span>
            <span class="manage-booking-page__window">@_booking.Display</span>
        </div>

        <div class="manage-booking-page__actions">
            <button class="manage-booking-page__button manage-booking-page__button--danger" @onclick="() => CancelAsync(false)" disabled="@_busy"
                    title="Releases your place. No replacement times are offered — the recruitment team will follow up.">@(_busy ? "Cancelling…" : "Cancel booking")</button>
            <button class="manage-booking-page__button manage-booking-page__button--primary" @onclick="() => CancelAsync(true)" disabled="@_busy"
                    title="Releases your place and emails you a fresh link with the times still open.">@(_busy ? "Cancelling…" : "Cancel and choose a new time")</button>
        </div>
        <p class="manage-booking-page__note manage-booking-page__footnote">This link is single-use and only works for you — no sign-in required. Please do not forward it.</p>
    }
</section>

@code {
    [Parameter]
    public string Token { get; set; } = string.Empty;

    private BookingDto? _booking;
    private string? _error;
    private bool _expired;
    private bool _loading = true;
    private bool _busy;
    private bool _cancelled;
    private bool _rebookRequested;
    private bool _reinvited;
    private bool _inviteCreated;
    private string? _deliveryStatus;
    private int _requestVersion;

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
        _rebookRequested = false;
        _reinvited = false;
        _inviteCreated = false;
        _deliveryStatus = null;

        try
        {
            var outcome = await Booking.GetBookingAsync(Token, CancellationToken.None);
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
        _rebookRequested = rebook;
        _busy = true;
        _error = null;

        try
        {
            var outcome = await Booking.CancelAsync(Token, rebook, CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            if (outcome.IsSuccess)
            {
                _inviteCreated = rebook && outcome.Value?.InviteCreated == true;
                _reinvited = _inviteCreated;
                _deliveryStatus = outcome.Value?.DeliveryStatus;
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
`````

## after — src/EventBooking.Web/Pages/ManageBooking.razor — 1/1

<!-- vocabulary-file: {"id":199,"oldPath":"src/EventBooking.Web/Pages/ManageBooking.razor","newPath":"src/EventBooking.Web/Pages/ManageBooking.razor","beforeSha":"7fc727d9d0e6ce986913e6194ded74029f80ffd182c9b577d37ce430e71d2c7c","afterSha":"5eeddcbe90b7d8691b894513dd1d626f50f251ca4b583b55f7e986cf653fe937","side":"after","part":1,"parts":1} -->

`````razor
@page "/manage/{Token}"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@using EventBooking.Web.Services
@layout EventBooking.Web.Layout.AttendeeLayout
@inject BookingClient Booking
@inject AttendeePageOptions PageOptions

<PageTitle>Manage your booking</PageTitle>


<section class="manage-booking-page" aria-busy="@(_loading || _busy)">
    @if (_loading)
    {
        <div class="manage-booking-page__status" aria-label="Loading booking details">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
        </div>
    }
    else if (_cancelled)
    {
        <div class="manage-booking-page__intro">
            <h1>Booking cancelled</h1>
        </div>
        <div class="manage-booking-page__status manage-booking-page__status--cancelled" role="status">
            @if (_reinvited && _deliveryStatus == "Sent")
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled and a new invitation with fresh times has been sent.</p>
            }
            else if (_rebookRequested)
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled. @(_inviteCreated ? "We could not confirm delivery of a replacement invitation; " : "There are no times available right now; ")the recruitment team will be in touch with the next available times.</p>
            }
            else
            {
                <p class="manage-booking-page__lead">Your booking has been cancelled. If that was a mistake, contact the recruitment team at @PageOptions.CoordinatorContact.</p>
            }
        </div>
    }
    else if (_expired)
    {
        <div class="manage-booking-page__status manage-booking-page__status--expired">
            <h1>This link has expired</h1>
            <p class="manage-booking-page__note">This booking link is no longer valid. Please contact the recruitment team at @PageOptions.CoordinatorContact for help.</p>
        </div>
    }
    else if (_booking is null)
    {
        <div class="manage-booking-page__intro">
            <h1>We couldn’t load your booking</h1>
        </div>
        <div class="manage-booking-page__status">
            <p class="manage-booking-page__banner" role="alert">@(_error ?? "Something went wrong. Please try again.")</p>
            <button class="manage-booking-page__button" @onclick="ReloadAsync">Try again</button>
        </div>
    }
    else
    {
        <div class="manage-booking-page__intro">
            <h1>Manage your booking</h1>
            <p class="manage-booking-page__lead">Review your appointment or cancel it below.</p>
        </div>

        @if (_error is not null)
        {
            <p class="manage-booking-page__banner" role="alert"><strong>We couldn’t cancel that booking.</strong> @_error</p>
        }

        <div class="manage-booking-page__card">
            <span class="manage-booking-page__label">Your appointment</span>
            <span class="manage-booking-page__window">@_booking.Display</span>
        </div>

        <div class="manage-booking-page__actions">
            <button class="manage-booking-page__button manage-booking-page__button--danger" @onclick="() => CancelAsync(false)" disabled="@_busy"
                    title="Releases your place. No replacement times are offered — the recruitment team will follow up.">@(_busy ? "Cancelling…" : "Cancel booking")</button>
            <button class="manage-booking-page__button manage-booking-page__button--primary" @onclick="() => CancelAsync(true)" disabled="@_busy"
                    title="Releases your place and emails you a fresh link with the times still open.">@(_busy ? "Cancelling…" : "Cancel and choose a new time")</button>
        </div>
        <p class="manage-booking-page__note manage-booking-page__footnote">This link is single-use and only works for you — no sign-in required. Please do not forward it.</p>
    }
</section>

@code {
    [Parameter]
    public string Token { get; set; } = string.Empty;

    private BookingDto? _booking;
    private string? _error;
    private bool _expired;
    private bool _loading = true;
    private bool _busy;
    private bool _cancelled;
    private bool _rebookRequested;
    private bool _reinvited;
    private bool _inviteCreated;
    private string? _deliveryStatus;
    private int _requestVersion;

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
        _rebookRequested = false;
        _reinvited = false;
        _inviteCreated = false;
        _deliveryStatus = null;

        try
        {
            var outcome = await Booking.GetBookingAsync(Token, CancellationToken.None);
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
        _rebookRequested = rebook;
        _busy = true;
        _error = null;

        try
        {
            var outcome = await Booking.CancelAsync(Token, rebook, CancellationToken.None);
            if (version != _requestVersion)
            {
                return;
            }

            if (outcome.IsSuccess)
            {
                _inviteCreated = rebook && outcome.Value?.InviteCreated == true;
                _reinvited = _inviteCreated;
                _deliveryStatus = outcome.Value?.DeliveryStatus;
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
`````

## before — src/EventBooking.Web/Pages/Settings.razor — 1/1

<!-- vocabulary-file: {"id":200,"oldPath":"src/EventBooking.Web/Pages/Settings.razor","newPath":"src/EventBooking.Web/Pages/Settings.razor","beforeSha":"a1149952c348f5f3e33f3ddfd6e020fa3980a3549abbc3170ec904f775e7eda1","afterSha":"0c59837532d4ecaadd43e1e60f508800a8f65d32a570457a084c8af6e4913113","side":"before","part":1,"parts":1} -->

`````razor
@page "/settings"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AdminClient Admin

<PageTitle>System settings</PageTitle>

<section class="page settings-page" aria-labelledby="settings-heading" aria-busy="@(_isLoading || _busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Administration</span>
            <h1 id="settings-heading">System settings</h1>
            <p>Fixed appointment types and invitation timing.</p>
        </div>
    </div>

    @if (_error is not null)
    {
        <div class="banner error" role="alert">
            <strong>@(_settings is null ? "Couldn’t load settings." : "Couldn’t refresh settings.")</strong>
            @_error
        </div>
    }

    @if (_saved)
    {
        <div class="banner saved" role="status">
            <strong>Saved.</strong> These changes apply to invites created from now on.
        </div>
    }

    @if (_isLoading && _settings is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
            <span class="visually-hidden">Loading system settings…</span>
        </div>
    }
    else if (_settings is null)
    {
        <div class="card">
            <div class="empty-state">
                <strong>Settings could not be loaded.</strong>
                <p>Try again to fetch the current appointment types and invite timing.</p>
                <button class="button" @onclick="ReloadPageAsync" disabled="@(_isLoading || _busy)">Try again</button>
            </div>
        </div>
    }
    else
    {
        <section class="card" aria-labelledby="types-heading">
            <div class="card-heading">
                <h2 id="types-heading">
                    Fixed appointment types
                    <span class="tip" tabindex="0" role="note"
                          aria-label="The three appointment types are fixed by the system. Only the manager assignment changes."
                          data-tip="The three appointment types are fixed by the system. Only the manager assignment changes."></span>
                </h2>
            </div>
            <div class="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Appointment type</th>
                            <th scope="col" title="The staff account that negotiates slots for this appointment type.">Manager identifier</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var type in _settings.AppointmentTypes)
                        {
                            <tr @key="type.Id">
                                <td data-label="Appointment type">
                                    <strong>@type.Name</strong>
                                    <span class="type-code">@type.Code</span>
                                </td>
                                <td data-label="Manager identifier">
                                    @ManagerLabel(type)
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </section>

        <section class="card" aria-labelledby="invite-timing-heading">
            <div class="card-heading">
                <h2 id="invite-timing-heading">
                    Invite timing
                    <span class="tip" tabindex="0" role="note"
                          aria-label="These two values shape how long a candidate has to respond and how hard the system chases them."
                          data-tip="These two values shape how long a candidate has to respond and how hard the system chases them."></span>
                </h2>
            </div>
            <div class="card-body settings-form-row">
                <div class="field">
                    <label for="invite-expiry-days">Invite expiry window (days)</label>
                    <input id="invite-expiry-days" class="number-input" type="number" min="1" @bind="_inviteExpiryDays" disabled="@_busy" />
                    <span class="hint">How long a booking link stays usable.</span>
                </div>
                <div class="field">
                    <label for="max-auto-retries">Max auto-retry count</label>
                    <input id="max-auto-retries" class="number-input" type="number" min="0" @bind="_maxAutoRetryCount" disabled="@_busy" />
                    <span class="hint">Automatic re-invites for an unanswered invitation before a human is asked to step in.</span>
                </div>
                <button class="button button-primary" @onclick="SaveAsync" disabled="@_busy"
                        title="Applies to invites created after you save.">Save changes</button>
            </div>
            <p class="callout settings-callout">
                <strong>Applies going forward:</strong> a change here governs invites created after you save — it does not reach back and change invites already sent.
            </p>
        </section>

    }
</section>

@code {
    // The staff number stays visible next to the name: it is the identifier an admin acts on.
    private static string ManagerLabel(AppointmentTypeDto type) => type switch
    {
        { ManagerDisplayName: not null, ManagerStaffId: not null } =>
            $"{type.ManagerDisplayName} ({type.ManagerStaffId})",
        { ManagerStaffId: not null } => type.ManagerStaffId,
        { ManagerUserId: null } => "Unassigned",
        _ => "Assigned (pending identity sync)",
    };

    private const string UnexpectedError = "Something went wrong. Please try again.";

    private SettingsDto? _settings;
    private string? _error;
    private bool _busy;
    private bool _isLoading;
    private bool _saved;
    private int _inviteExpiryDays;
    private int _maxAutoRetryCount;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadPageAsync()
    {
        await ReloadAsync();
    }

    private Task<bool> ReloadAsync()
    {
        if (_isLoading || _busy)
        {
            return Task.FromResult(false);
        }

        return ReloadCoreAsync();
    }

    private async Task<bool> ReloadCoreAsync()
    {
        _isLoading = true;
        try
        {
            var outcome = await Admin.GetAsync(CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _settings = outcome.Value;
                _inviteExpiryDays = _settings.InviteExpiryDays;
                _maxAutoRetryCount = _settings.MaxAutoRetryCount;
                _error = null;
                return true;
            }

            _error = outcome.ErrorMessage ?? UnexpectedError;
            return false;
        }
        catch (Exception)
        {
            _error = UnexpectedError;
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_busy || _isLoading)
        {
            return;
        }

        _saved = false;
        _error = null;
        _busy = true;
        try
        {
            var outcome = await Admin.UpdateAsync(
                _inviteExpiryDays,
                _maxAutoRetryCount,
                CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _saved = true;
            await ReloadCoreAsync();
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
}
`````

## after — src/EventBooking.Web/Pages/Settings.razor — 1/1

<!-- vocabulary-file: {"id":200,"oldPath":"src/EventBooking.Web/Pages/Settings.razor","newPath":"src/EventBooking.Web/Pages/Settings.razor","beforeSha":"a1149952c348f5f3e33f3ddfd6e020fa3980a3549abbc3170ec904f775e7eda1","afterSha":"0c59837532d4ecaadd43e1e60f508800a8f65d32a570457a084c8af6e4913113","side":"after","part":1,"parts":1} -->

`````razor
@page "/settings"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AdminClient Admin

<PageTitle>System settings</PageTitle>

<section class="page settings-page" aria-labelledby="settings-heading" aria-busy="@(_isLoading || _busy ? "true" : "false")">
    <div class="page-header">
        <div>
            <span class="eyebrow">Administration</span>
            <h1 id="settings-heading">System settings</h1>
            <p>Fixed appointment types and invitation timing.</p>
        </div>
    </div>

    @if (_error is not null)
    {
        <div class="banner error" role="alert">
            <strong>@(_settings is null ? "Couldn’t load settings." : "Couldn’t refresh settings.")</strong>
            @_error
        </div>
    }

    @if (_saved)
    {
        <div class="banner saved" role="status">
            <strong>Saved.</strong> These changes apply to invites created from now on.
        </div>
    }

    @if (_isLoading && _settings is null)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
            <span class="visually-hidden">Loading system settings…</span>
        </div>
    }
    else if (_settings is null)
    {
        <div class="card">
            <div class="empty-state">
                <strong>Settings could not be loaded.</strong>
                <p>Try again to fetch the current appointment types and invite timing.</p>
                <button class="button" @onclick="ReloadPageAsync" disabled="@(_isLoading || _busy)">Try again</button>
            </div>
        </div>
    }
    else
    {
        <section class="card" aria-labelledby="types-heading">
            <div class="card-heading">
                <h2 id="types-heading">
                    Fixed appointment types
                    <span class="tip" tabindex="0" role="note"
                          aria-label="The three appointment types are fixed by the system. Only the manager assignment changes."
                          data-tip="The three appointment types are fixed by the system. Only the manager assignment changes."></span>
                </h2>
            </div>
            <div class="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Appointment type</th>
                            <th scope="col" title="The staff account that negotiates events for this appointment type.">Manager identifier</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var type in _settings.AppointmentTypes)
                        {
                            <tr @key="type.Id">
                                <td data-label="Appointment type">
                                    <strong>@type.Name</strong>
                                    <span class="type-code">@type.Code</span>
                                </td>
                                <td data-label="Manager identifier">
                                    @ManagerLabel(type)
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </section>

        <section class="card" aria-labelledby="invite-timing-heading">
            <div class="card-heading">
                <h2 id="invite-timing-heading">
                    Invite timing
                    <span class="tip" tabindex="0" role="note"
                          aria-label="These two values shape how long a attendee has to respond and how hard the system chases them."
                          data-tip="These two values shape how long a attendee has to respond and how hard the system chases them."></span>
                </h2>
            </div>
            <div class="card-body settings-form-row">
                <div class="field">
                    <label for="invite-expiry-days">Invite expiry window (days)</label>
                    <input id="invite-expiry-days" class="number-input" type="number" min="1" @bind="_inviteExpiryDays" disabled="@_busy" />
                    <span class="hint">How long a booking link stays usable.</span>
                </div>
                <div class="field">
                    <label for="max-auto-retries">Max auto-retry count</label>
                    <input id="max-auto-retries" class="number-input" type="number" min="0" @bind="_maxAutoRetryCount" disabled="@_busy" />
                    <span class="hint">Automatic re-invites for an unanswered invitation before a human is asked to step in.</span>
                </div>
                <button class="button button-primary" @onclick="SaveAsync" disabled="@_busy"
                        title="Applies to invites created after you save.">Save changes</button>
            </div>
            <p class="callout settings-callout">
                <strong>Applies going forward:</strong> a change here governs invites created after you save — it does not reach back and change invites already sent.
            </p>
        </section>

    }
</section>

@code {
    // The staff number stays visible next to the name: it is the identifier an admin acts on.
    private static string ManagerLabel(AppointmentTypeDto type) => type switch
    {
        { ManagerDisplayName: not null, ManagerStaffId: not null } =>
            $"{type.ManagerDisplayName} ({type.ManagerStaffId})",
        { ManagerStaffId: not null } => type.ManagerStaffId,
        { ManagerUserId: null } => "Unassigned",
        _ => "Assigned (pending identity sync)",
    };

    private const string UnexpectedError = "Something went wrong. Please try again.";

    private SettingsDto? _settings;
    private string? _error;
    private bool _busy;
    private bool _isLoading;
    private bool _saved;
    private int _inviteExpiryDays;
    private int _maxAutoRetryCount;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadPageAsync()
    {
        await ReloadAsync();
    }

    private Task<bool> ReloadAsync()
    {
        if (_isLoading || _busy)
        {
            return Task.FromResult(false);
        }

        return ReloadCoreAsync();
    }

    private async Task<bool> ReloadCoreAsync()
    {
        _isLoading = true;
        try
        {
            var outcome = await Admin.GetAsync(CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _settings = outcome.Value;
                _inviteExpiryDays = _settings.InviteExpiryDays;
                _maxAutoRetryCount = _settings.MaxAutoRetryCount;
                _error = null;
                return true;
            }

            _error = outcome.ErrorMessage ?? UnexpectedError;
            return false;
        }
        catch (Exception)
        {
            _error = UnexpectedError;
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_busy || _isLoading)
        {
            return;
        }

        _saved = false;
        _error = null;
        _busy = true;
        try
        {
            var outcome = await Admin.UpdateAsync(
                _inviteExpiryDays,
                _maxAutoRetryCount,
                CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _saved = true;
            await ReloadCoreAsync();
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
}
`````
