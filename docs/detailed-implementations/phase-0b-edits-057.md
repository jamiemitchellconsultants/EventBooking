# 00b — Vocabulary edits 57 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Web/Pages/Audit.razor — 1/1

<!-- vocabulary-file: {"id":190,"oldPath":"src/EventBooking.Web/Pages/Audit.razor","newPath":"src/EventBooking.Web/Pages/Audit.razor","beforeSha":"76f1b8f337f0bbe4317174f7456cde24ee0ed29059ed2d4f37e1c86a5342272e","afterSha":"4f9b04878a6d724c0eca51ac7fef918937a281f970023a2ae28e22996cc7d02a","side":"before","part":1,"parts":1} -->

`````razor
@page "/audit"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AuditClient Audits
@inject MeClient Me
@inject HeadOfficeTimePresentation TimePresentation

<PageTitle>Audit trail</PageTitle>

<section class="page audit-page" aria-labelledby="audit-heading">
    <div class="page-header">
        <div>
            <span class="eyebrow">Assurance</span>
            <h1 id="audit-heading">Audit trail</h1>
            <p>Search what changed, who changed it, and when.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-body audit-filters" aria-label="Audit search filters">
            <div class="field">
                <label class="field-label" for="audit-from">From</label>
                <input id="audit-from" type="date" @bind="_from"
                       title="Only shows changes recorded on or after this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-to">To</label>
                <input id="audit-to" type="date" @bind="_to"
                       title="Only shows changes recorded on or before this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-actor-type">Actor</label>
                <select id="audit-actor-type" @bind="_actorType"
                        title="Who caused the change: a staff member, a candidate's link, or the system.">
                    <option value="">Any actor</option>
                    @foreach (var actorType in ActorTypes)
                    {
                        <option value="@actorType">@actorType</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-action">Action</label>
                <select id="audit-action" @bind="_action"
                        title="The recorded change to look for.">
                    <option value="">Any action</option>
                    @foreach (var action in Actions)
                    {
                        <option value="@action">@action</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-identifier">Identifier</label>
                <input id="audit-identifier" @bind="_identifier" placeholder="Entity or actor id"
                       title="Matches an audited entity identifier or an actor identifier exactly." />
            </div>
            @if (_showEntityType)
            {
                <div class="field">
                    <label class="field-label" for="audit-entity-type">Entity</label>
                    <select id="audit-entity-type" @bind="_entityType"
                            title="Narrows the search to one kind of audited entity.">
                        <option value="">All entities</option>
                        @foreach (var entityType in EntityTypes)
                        {
                            <option value="@entityType">@entityType</option>
                        }
                    </select>
                </div>
            }
            <button id="audit-search" type="button" class="button button-primary" @onclick="SearchAsync" disabled="@_busy">
                Search
            </button>
        </div>
    </div>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (_rows.Count == 0 && !_busy)
    {
        <p>Nothing matches these filters.</p>
    }

    @if (_rows.Count > 0)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr>
                        <th scope="col">When</th>
                        <th scope="col">What</th>
                        <th scope="col">Who</th>
                        <th scope="col">Details</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.EntityType @row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }

    @if (_nextCursor is not null)
    {
        <button id="audit-load-more" type="button" class="button" @onclick="LoadMoreAsync" disabled="@_busy">
            Load more
        </button>
    }
</section>

@code {
    // The web app deliberately does not reference the domain assembly, so the server's enum names
    // are repeated here as plain strings; the API rejects anything it does not recognise.
    private static readonly string[] ActorTypes = ["Staff", "CandidateToken", "System"];

    private static readonly string[] Actions =
    [
        "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
        "SlotConfirmed", "SlotCancelled", "CapacityDecremented", "CapacityIncremented",
        "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
        "BookingCreated", "BookingCancelled", "CapacityAdjusted", "SlotImported",
        "StaffAccessChanged", "StaffAccessRemoved", "AppointmentCheckedIn", "AppointmentCompleted",
        "AppointmentMarkedNoShow", "AppointmentStatusCorrected", "EmployeeGroupAssigned",
        "EmployeeGroupChanged", "RecoveryInviteCreated", "RecoveryInviteCancelled",
        "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
    ];

    private static readonly string[] EntityTypes =
    [
        "SlotProposal", "ConfirmedSlot", "Invite", "Booking",
        "StaffAccessProfile", "BookingAppointment", "Candidate",
    ];

    private const int PageSize = 50;

    private readonly List<AuditRowDto> _rows = [];
    private DateTime? _from;
    private DateTime? _to;
    private string? _actorType;
    private string? _action;
    private string? _identifier;
    private string? _entityType;
    private string? _nextCursor;
    private string? _error;
    private bool _busy;
    private bool _showEntityType;

    protected override async Task OnInitializedAsync()
    {
        var me = await Me.GetAsync(CancellationToken.None);
        _showEntityType = me.Value?.Roles.Contains("Coordinator") == true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _rows.Clear();
        _nextCursor = null;
        await LoadMoreAsync();
    }

    private async Task LoadMoreAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var outcome = await Audits.SearchAsync(
                new AuditSearchFilterDto(
                    ToOffset(_from),
                    ToOffset(_to),
                    EmptyToNull(_actorType),
                    EmptyToNull(_action),
                    EmptyToNull(_identifier),
                    EmptyToNull(_entityType),
                    _nextCursor,
                    PageSize),
                CancellationToken.None);

            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.AddRange(outcome.Value.Rows);
                _nextCursor = outcome.Value.NextCursor;
            }
            else
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
            }
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

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
`````

## after — src/EventBooking.Web/Pages/Audit.razor — 1/1

<!-- vocabulary-file: {"id":190,"oldPath":"src/EventBooking.Web/Pages/Audit.razor","newPath":"src/EventBooking.Web/Pages/Audit.razor","beforeSha":"76f1b8f337f0bbe4317174f7456cde24ee0ed29059ed2d4f37e1c86a5342272e","afterSha":"4f9b04878a6d724c0eca51ac7fef918937a281f970023a2ae28e22996cc7d02a","side":"after","part":1,"parts":1} -->

`````razor
@page "/audit"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AuditClient Audits
@inject MeClient Me
@inject TransitionalLocationTimePresentation TimePresentation

<PageTitle>Audit trail</PageTitle>

<section class="page audit-page" aria-labelledby="audit-heading">
    <div class="page-header">
        <div>
            <span class="eyebrow">Assurance</span>
            <h1 id="audit-heading">Audit trail</h1>
            <p>Search what changed, who changed it, and when.</p>
        </div>
    </div>

    <div class="card">
        <div class="card-body audit-filters" aria-label="Audit search filters">
            <div class="field">
                <label class="field-label" for="audit-from">From</label>
                <input id="audit-from" type="date" @bind="_from"
                       title="Only shows changes recorded on or after this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-to">To</label>
                <input id="audit-to" type="date" @bind="_to"
                       title="Only shows changes recorded on or before this date." />
            </div>
            <div class="field">
                <label class="field-label" for="audit-actor-type">Actor</label>
                <select id="audit-actor-type" @bind="_actorType"
                        title="Who caused the change: a staff member, a attendee's link, or the system.">
                    <option value="">Any actor</option>
                    @foreach (var actorType in ActorTypes)
                    {
                        <option value="@actorType">@actorType</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-action">Action</label>
                <select id="audit-action" @bind="_action"
                        title="The recorded change to look for.">
                    <option value="">Any action</option>
                    @foreach (var action in Actions)
                    {
                        <option value="@action">@action</option>
                    }
                </select>
            </div>
            <div class="field">
                <label class="field-label" for="audit-identifier">Identifier</label>
                <input id="audit-identifier" @bind="_identifier" placeholder="Entity or actor id"
                       title="Matches an audited entity identifier or an actor identifier exactly." />
            </div>
            @if (_showEntityType)
            {
                <div class="field">
                    <label class="field-label" for="audit-entity-type">Entity</label>
                    <select id="audit-entity-type" @bind="_entityType"
                            title="Narrows the search to one kind of audited entity.">
                        <option value="">All entities</option>
                        @foreach (var entityType in EntityTypes)
                        {
                            <option value="@entityType">@entityType</option>
                        }
                    </select>
                </div>
            }
            <button id="audit-search" type="button" class="button button-primary" @onclick="SearchAsync" disabled="@_busy">
                Search
            </button>
        </div>
    </div>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (_rows.Count == 0 && !_busy)
    {
        <p>Nothing matches these filters.</p>
    }

    @if (_rows.Count > 0)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr>
                        <th scope="col">When</th>
                        <th scope="col">What</th>
                        <th scope="col">Who</th>
                        <th scope="col">Details</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.EntityType @row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }

    @if (_nextCursor is not null)
    {
        <button id="audit-load-more" type="button" class="button" @onclick="LoadMoreAsync" disabled="@_busy">
            Load more
        </button>
    }
</section>

@code {
    // The web app deliberately does not reference the domain assembly, so the server's enum names
    // are repeated here as plain strings; the API rejects anything it does not recognise.
    private static readonly string[] ActorTypes = ["Staff", "AttendeeToken", "System"];

    private static readonly string[] Actions =
    [
        "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
        "EventConfirmed", "EventCancelled", "CapacityDecremented", "CapacityIncremented",
        "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
        "BookingCreated", "BookingCancelled", "CapacityAdjusted", "EventImported",
        "StaffAccessChanged", "StaffAccessRemoved", "AppointmentCheckedIn", "AppointmentCompleted",
        "AppointmentMarkedNoShow", "AppointmentStatusCorrected", "AttendeeGroupAssigned",
        "AttendeeGroupReassigned", "RecoveryInviteCreated", "RecoveryInviteCancelled",
        "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
    ];

    private static readonly string[] EntityTypes =
    [
        "EventProposal", "Event", "Invite", "Booking",
        "StaffAccessProfile", "BookingAppointment", "Attendee",
    ];

    private const int PageSize = 50;

    private readonly List<AuditRowDto> _rows = [];
    private DateTime? _from;
    private DateTime? _to;
    private string? _actorType;
    private string? _action;
    private string? _identifier;
    private string? _entityType;
    private string? _nextCursor;
    private string? _error;
    private bool _busy;
    private bool _showEntityType;

    protected override async Task OnInitializedAsync()
    {
        var me = await Me.GetAsync(CancellationToken.None);
        _showEntityType = me.Value?.Roles.Contains("Coordinator") == true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _rows.Clear();
        _nextCursor = null;
        await LoadMoreAsync();
    }

    private async Task LoadMoreAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var outcome = await Audits.SearchAsync(
                new AuditSearchFilterDto(
                    ToOffset(_from),
                    ToOffset(_to),
                    EmptyToNull(_actorType),
                    EmptyToNull(_action),
                    EmptyToNull(_identifier),
                    EmptyToNull(_entityType),
                    _nextCursor,
                    PageSize),
                CancellationToken.None);

            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.AddRange(outcome.Value.Rows);
                _nextCursor = outcome.Value.NextCursor;
            }
            else
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
            }
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

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
`````

## before — src/EventBooking.Web/Pages/Book.razor — 1/1

<!-- vocabulary-file: {"id":191,"oldPath":"src/EventBooking.Web/Pages/Book.razor","newPath":"src/EventBooking.Web/Pages/Book.razor","beforeSha":"f8c6b2f57dbf2de3f9980524125154459c622ccacadb23accf5a5a449e38fa05","afterSha":"d1ab9a5ecd0aa08a66faee4dce3de57fef93caaff44306d19e244e11aee30451","side":"before","part":1,"parts":1} -->

`````razor
@page "/book/{Token}"
@attribute [Microsoft.AspNetCore.Authorization.AllowAnonymous]
@using EventBooking.Web.Services
@layout EventBooking.Web.Layout.CandidateLayout
@inject BookingClient Booking
@inject CandidatePageOptions PageOptions

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
            @if (!string.IsNullOrWhiteSpace(_confirmed.HeadOfficeAddress))
            {
                <p class="booking-page__note">Head office: @_confirmed.HeadOfficeAddress</p>
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
            <p class="booking-page__lead">Hi <strong>@_invite.CandidateName</strong> — pick a time for your <strong>@string.Join(" and ", _invite.AppointmentTypeNames)</strong> appointment.</p>
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
                                   name="slot"
                                   checked="@(_chosen == option.ConfirmedSlotId)"
                                   @onchange="() => _chosen = option.ConfirmedSlotId" />
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

<!-- vocabulary-file: {"id":191,"oldPath":"src/EventBooking.Web/Pages/Book.razor","newPath":"src/EventBooking.Web/Pages/Book.razor","beforeSha":"f8c6b2f57dbf2de3f9980524125154459c622ccacadb23accf5a5a449e38fa05","afterSha":"d1ab9a5ecd0aa08a66faee4dce3de57fef93caaff44306d19e244e11aee30451","side":"after","part":1,"parts":1} -->

`````razor
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
