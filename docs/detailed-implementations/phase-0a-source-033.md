# 00a — Port source 33 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Web/Pages/Authentication.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Authentication.razor","encoding":"utf8","sha256":"f7d081fe2b4c012cc1669bfb4840e463a2fa1c2b54480c4ca573c1c9248b5820","parts":1,"part":1} -->

`````razor
@page "/authentication/{action}"

<RemoteAuthenticatorView Action="@Action" />

@code {
    [Parameter] public string? Action { get; set; }
}
`````

## src/EventBooking.Web/Pages/Book.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Book.razor","encoding":"utf8","sha256":"f8c6b2f57dbf2de3f9980524125154459c622ccacadb23accf5a5a449e38fa05","parts":1,"part":1} -->

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

## src/EventBooking.Web/Pages/Book.razor.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Book.razor.css","encoding":"utf8","sha256":"60f5791310fd34002b4781d0562b38e6c91c478eabdbd14ff48704a91b6e0a7d","parts":1,"part":1} -->

`````text
.booking-page {
    margin: 0 auto;
    max-width: 580px;
    padding: 36px 28px 60px;
}

.booking-page__intro {
    display: grid;
    gap: 8px;
    margin-bottom: 20px;
}

.booking-page h1 {
    color: var(--ink-strong);
    font-size: 1.5rem;
    line-height: 1.2;
    margin: 0;
}

.booking-page__lead,
.booking-page__note {
    color: var(--sub);
    font-size: 0.875rem;
    line-height: 1.55;
    margin: 0;
}

.booking-page__card,
.booking-page__status {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    box-shadow: var(--shadow-sm);
}

.booking-page__card {
    overflow: hidden;
}

.booking-page__options {
    border: 0;
    margin: 0;
    padding: 0;
}

.booking-page__option {
    align-items: center;
    border-bottom: 1px solid var(--line);
    cursor: pointer;
    display: flex;
    gap: 14px;
    padding: 16px;
    transition: background-color 120ms ease;
}

.booking-page__option:last-child {
    border-bottom: 0;
}

.booking-page__option:hover {
    background: var(--surface-sunken);
}

.booking-page__option:has(input:focus-visible) {
    outline: 3px solid var(--focus);
    outline-offset: -3px;
}

.booking-page__option:has(input:checked) {
    background: var(--accent-soft);
    box-shadow: inset 4px 0 0 var(--ba-speedmarque-red);
}

.booking-page__option-copy {
    display: grid;
    gap: 2px;
}

.booking-page__option-date {
    color: var(--ink-strong);
    font-size: 0.9375rem;
    font-weight: 600;
}

.booking-page__option-time {
    color: var(--sub);
    font-size: 0.8125rem;
}

.booking-page__button {
    background: var(--accent);
    border: 1.5px solid var(--accent);
    border-radius: var(--radius-sm);
    box-shadow: var(--shadow-sm);
    color: #fff;
    cursor: pointer;
    font: inherit;
    font-size: 0.9375rem;
    font-weight: 600;
    margin-top: 20px;
    min-height: 46px;
    padding: 12px 24px;
}

.booking-page__button:hover:not(:disabled) {
    background: var(--accent-hover);
    border-color: var(--accent-hover);
}

.booking-page__button:disabled {
    cursor: not-allowed;
    opacity: 0.5;
}

.booking-page__banner {
    background: var(--error-bg);
    border: 1px solid var(--error-line);
    border-left-width: 5px;
    border-radius: var(--radius-sm);
    color: var(--error-ink);
    font-size: 0.8125rem;
    line-height: 1.5;
    margin: 0 0 18px;
    padding: 11px 14px;
}

.booking-page__banner--success {
    background: var(--success-bg);
    border-color: var(--success-line);
    color: var(--success-ink);
}

.booking-page__status {
    display: grid;
    gap: 12px;
    padding: 22px 20px;
}

.booking-page__confirmed-window {
    color: var(--ink-strong);
    font-size: 1.0625rem;
    font-weight: 600;
    line-height: 1.45;
    margin: 0;
}

.booking-page__expired {
    margin-top: 40px;
    padding: 34px 22px;
    text-align: center;
}

.booking-page__footnote {
    margin-top: 20px;
}

.booking-page__choose-hint {
    margin-bottom: 12px;
}

@media (max-width: 640px) {
    .booking-page {
        padding: 26px 18px 44px;
    }

    .booking-page__button {
        width: 100%;
    }
}
`````

## src/EventBooking.Web/Pages/Candidates.razor — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/Pages/Candidates.razor","encoding":"utf8","sha256":"56dccdbfbd22b12c392a35db285380b21f8cc5db4c1d06e2edeeba1a45c74742","parts":1,"part":1} -->

`````razor
@page "/candidates"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject CandidatesClient CandidatesApi
@inject DashboardsClient Dashboards
@inject HeadOfficeTimePresentation TimePresentation

<PageTitle>Candidates</PageTitle>

<section class="page candidates-page" aria-labelledby="candidates-heading">
    <div class="page-header">
        <div>
            <span class="eyebrow">Coordination</span>
            <h1 id="candidates-heading">Candidates</h1>
            <p>Manage people and the employee groups that set their appointments.</p>
        </div>
        <div class="candidates-toolbar" aria-label="Candidate list controls">
            <label class="field-label">
                Status
                <select @bind="_statusFilter" @bind:after="ReloadAsync"
                        title="Filters the list to one point in the invitation journey.">
                    <option value="">Any status</option>
                    @foreach (var (value, label) in StatusChoices)
                    {
                        <option value="@value">@label</option>
                    }
                </select>
            </label>
            <label class="search-field" for="candidate-search">
                <span class="visually-hidden">Search candidates</span>
                <input id="candidate-search" @bind="_search" @bind:event="oninput"
                       placeholder="Search by name or email…" />
            </label>
            <button class="button button-quiet button-icon-search" @onclick="ReloadAsync" disabled="@(_busy || _isLoading)"
                    title="Applies the status filter and search term.">
                Search
            </button>
            <label class="button button-quiet upload-button" for="candidate-csv"
                   title="The header line must read exactly: name,email,employee_group. The whole file is accepted or rejected.">
                Upload CSV
            </label>
            <InputFile id="candidate-csv" class="visually-hidden" OnChange="OnFileChosenAsync" accept=".csv,text/csv" disabled="@_busy" />
        </div>
    </div>

    @if (_error is not null)
    {
        <div class="banner @(_deleteAwaitingConfirmation.HasValue ? "warning" : "error")" role="alert">
            @_error
        </div>
    }

    @if (_importErrors.Count > 0)
    {
        <div class="banner error" role="alert">
            <strong>Nothing was imported.</strong> Fix the file and try again.
            <ul class="import-errors">
                @foreach (var error in _importErrors)
                {
                    <li><strong>Line @error.LineNumber:</strong> @error.Message</li>
                }
            </ul>
        </div>
    }

    @if (_isLoading)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
            <span class="visually-hidden">Loading candidates…</span>
        </div>
    }
    else if (_candidates is null)
    {
        <div class="card">
            <div class="empty-state">
                <strong>Couldn’t load candidates.</strong>
                <p>The candidate list could not be fetched. Try again to reload it.</p>
                <button class="button" @onclick="ReloadAsync">Try again</button>
            </div>
        </div>
    }
    else
    {
        <div class="card">
            <div class="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Name</th>
                            <th scope="col">Email</th>
                            <th scope="col" title="The appointment types the candidate's employee group requires.">Required types</th>
                            <th scope="col" title="Where the candidate has reached in the invitation journey.">Status</th>
                            <th scope="col" title="Whether the candidate still has appointments outstanding before they can start.">Readiness</th>
                            <th scope="col" title="The last invitation or confirmation email sent, and whether it was delivered.">Delivery</th>
                            <th scope="col" title="The candidate's active bookings, and the actions that cancel them.">Booking</th>
                            <th scope="col" title="Every recorded change for this candidate, with who made it.">History</th>
                            <th scope="col" class="actions-column">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr class="candidate-add-row">
                            <td data-label="Name">
                                <label class="visually-hidden" for="new-candidate-name">Full name</label>
                                <input id="new-candidate-name" @bind="_newName" placeholder="Full name" />
                            </td>
                            <td data-label="Email">
                                <label class="visually-hidden" for="new-candidate-email">Email address</label>
                                <input id="new-candidate-email" type="email" @bind="_newEmail" placeholder="name@example.com" />
                            </td>
                            <td data-label="Required types">
                                <label class="visually-hidden" for="new-candidate-group">Employee group</label>
                                <select id="new-candidate-group" class="group-select" @bind="_newEmployeeGroupId" disabled="@_busy" required
                                        title="The employee group decides which appointment types this candidate must attend.">
                                    <option value="">Select an employee group</option>
                                    @foreach (var group in _groups)
                                    {
                                        <option value="@group.EmployeeGroupId">@group.Name</option>
                                    }
                                </select>
                                <div class="chip-row group-preview" aria-live="polite">
                                    @foreach (var type in SelectedGroupTypes(_newEmployeeGroupId))
                                    {
                                        <span class="chip">@type.Code</span>
                                    }
                                </div>
                            </td>
                            <td data-label="Status"><span class="candidate-status status-new">New candidate</span></td>
                            <td data-label="Readiness"><span>—</span></td>
                            <td data-label="Delivery"><span>—</span></td>
                            <td data-label="Booking"><span>—</span></td>
                            <td data-label="History"><span>—</span></td>
                            <td data-label="Actions">
                                <button class="button button-primary button-small" @onclick="CreateAsync" disabled="@_busy"
                                        title="Adds the candidate without inviting them yet.">
                                    Save candidate
                                </button>
                            </td>
                        </tr>

                        @foreach (var candidate in _candidates)
                        {
                            var editing = _editingId == candidate.CandidateId;
                            var awaitingDeleteConfirmation = _deleteAwaitingConfirmation == candidate.CandidateId;
                            <tr @key="candidate.CandidateId" class="@(editing ? "candidate-edit-row" : awaitingDeleteConfirmation ? "awaiting-confirmation" : null)">
                                @if (editing)
                                {
                                    <td data-label="Name">
                                        <label class="visually-hidden" for="edit-candidate-name">Full name</label>
                                        <input id="edit-candidate-name" @bind="_editName" />
                                    </td>
                                    <td data-label="Email">
                                        <label class="visually-hidden" for="edit-candidate-email">Email address</label>
                                        <input id="edit-candidate-email" type="email" @bind="_editEmail" />
                                    </td>
                                    <td data-label="Required types">
                                        <label class="visually-hidden" for="edit-candidate-group">Employee group</label>
                                        <select id="edit-candidate-group" class="group-select" @bind="_editEmployeeGroupId" disabled="@_busy" required
                                            title="Changing the group changes the appointment types this candidate must attend.">
                                            <option value="">Select an employee group</option>
                                            @foreach (var group in _groups)
                                            {
                                                <option value="@group.EmployeeGroupId">@group.Name</option>
                                            }
                                        </select>
                                        <div class="chip-row group-preview" aria-live="polite">
                                            @foreach (var type in SelectedGroupTypes(_editEmployeeGroupId))
                                            {
                                                <span class="chip">@type.Code</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Status"><span class="candidate-status @CandidatePresentation.StatusCssClass(candidate.Status)">@candidate.StatusDisplay</span></td>
                                    <td data-label="Readiness"><span>—</span></td>
                                    <td data-label="Delivery">
                                        @if (_emailStatus.TryGetValue(candidate.CandidateId, out var email))
                                        {
                                            <span class="@(email.Status == "Failed" ? "error" : email.Status == "Pending" ? "warning" : "")">
                                                @email.TemplateDisplay @TimePresentation.Format(email.SentAt)
                                            </span>
                                            @if (email.Status is "Failed" or "Pending" && email.CanRetry)
                                            {
                                                <button class="button button-small" @onclick="() => RetryEmailAsync(candidate.CandidateId)" disabled="@_busy"
                                                        title="Sends the same email again to the same address.">Resend</button>
                                            }
                                        }
                                        else
                                        {
                                            <span>—</span>
                                        }
                                    </td>
                                    <td data-label="Booking"><span>—</span></td>
                                    <td data-label="History"><AuditHistory CandidateId="@candidate.CandidateId" /></td>
                                    <td data-label="Actions">
                                        <div class="row-actions">
                                            <button class="button button-primary button-small" @onclick="SaveEditAsync" disabled="@_busy"
                                                    title="Saves the edited details. Existing invitations are not resent.">Save</button>
                                            <button class="button button-quiet button-small" @onclick="CancelEdit" disabled="@_busy">Cancel</button>
                                        </div>
                                    </td>
                                }
                                else
                                {
                                    <td data-label="Name">@candidate.Name</td>
                                    <td data-label="Email"><a href="mailto:@candidate.Email">@candidate.Email</a></td>
                                    <td data-label="Required types">
                                        @if (candidate.RequiresEmployeeGroupReconciliation)
                                        {
                                            <span class="candidate-status status-warning">Employee Group required</span>
                                        }
                                        else
                                        {
                                            <div class="chip-row">
                                                @foreach (var requiredType in candidate.RequiredAppointmentTypes)
                                                {
                                                    <span class="chip">@requiredType.Code</span>
                                                }
                                            </div>
                                        }
                                    </td>
                                    <td data-label="Status"><span class="candidate-status @CandidatePresentation.StatusCssClass(candidate.Status)">@candidate.StatusDisplay</span></td>
                                    <td data-label="Readiness">
                                        @if (_readiness.TryGetValue(candidate.CandidateId, out var readiness))
                                        {
                                            var expanded = _expandedReadiness.Contains(candidate.CandidateId);
                                            <button class="button button-small readiness-badge @CandidatePresentation.ReadinessCssClass(readiness.Code)"
                                                    @onclick="() => ToggleReadinessAsync(candidate.CandidateId)"
                                                    aria-expanded="@(expanded ? "true" : "false")"
                                                    aria-controls="@(expanded ? $"readiness-{candidate.CandidateId}" : null)">
                                                <span aria-hidden="true">@CandidatePresentation.ReadinessIcon(readiness.Code)</span>
                                                @readiness.Display
                                            </button>
                                            @if (expanded)
                                            {
                                                <div class="readiness-detail" id="readiness-@candidate.CandidateId">
                                                    @if (readiness.OutstandingAppointmentTypes.Count == 0)
                                                    {
                                                        <span>@readiness.Display</span>
                                                    }
                                                    else
                                                    {
                                                        <span>@readiness.Display:</span>
                                                        <ul class="readiness-types">
                                                            @foreach (var type in readiness.OutstandingAppointmentTypes)
                                                            {
                                                                <li>@type.Name@(type.IsRecoverable ? " (recoverable)" : null)</li>
                                                            }
                                                        </ul>
                                                    }
                                                    @if (readiness.OutstandingAppointmentTypes.Any(type => type.IsRecoverable)
                                                        && !_pendingRecoveryInvites.ContainsKey(candidate.CandidateId))
                                                    {
                                                        <button class="button button-small" @onclick="() => StartRecoveryAsync(candidate.CandidateId, RecoverableTypeNames(readiness))" disabled="@_busy"
                                                                title="Emails a fresh booking link covering only the appointments the candidate still owes.">Arrange missed appointments</button>
                                                    }
                                                    @if (_pendingRecoveryInvites.ContainsKey(candidate.CandidateId))
                                                    {
                                                        <button class="button button-small button-danger" @onclick="() => CancelRecoveryAsync(candidate.CandidateId)" disabled="@_busy">
                                                            @(_recoveryCancelAwaitingConfirmation == candidate.CandidateId ? "Confirm cancel" : "Cancel recovery")
                                                        </button>
                                                    }
                                                    @if (_recoveryOutcomes.TryGetValue(candidate.CandidateId, out var recoveryOutcome))
                                                    {
                                                        <span class="recovery-outcome" role="status">@recoveryOutcome</span>
                                                    }
                                                    @if (_recoveryErrors.TryGetValue(candidate.CandidateId, out var recoveryError))
                                                    {
                                                        <span class="recovery-error" role="alert">@recoveryError</span>
                                                    }
                                                </div>
                                            }
                                        }
                                        else if (_readinessLoading.Contains(candidate.CandidateId))
                                        {
                                            <span class="readiness-loading" role="status" aria-live="polite">Loading readiness…</span>
                                        }
                                        else if (_readinessErrors.TryGetValue(candidate.CandidateId, out var readinessError))
                                        {
                                            <span class="readiness-error" role="alert">@readinessError</span>
                                            <button class="button button-small" @onclick="() => LoadReadinessAsync(candidate.CandidateId)" disabled="@_busy">Try again</button>
                                        }
                                        else
                                        {
                                            <button class="button button-quiet button-small readiness-badge status-neutral"
                                                    @onclick="() => ToggleReadinessAsync(candidate.CandidateId)"
                                                    title="Checks which appointments this candidate still owes before they can start."
                                                    aria-expanded="false">
                                                <span aria-hidden="true">?</span> Check readiness
                                            </button>
                                        }
                                    </td>
                                    <td data-label="Delivery">
                                        @if (_emailStatus.TryGetValue(candidate.CandidateId, out var email))
                                        {
                                            <span class="@(email.Status == "Failed" ? "error" : email.Status == "Pending" ? "warning" : "")">
                                                @email.TemplateDisplay @TimePresentation.Format(email.SentAt)
                                            </span>
                                            @if (email.Status is "Failed" or "Pending" && email.CanRetry)
                                            {
                                                <button class="button button-small" @onclick="() => RetryEmailAsync(candidate.CandidateId)" disabled="@_busy"
                                                        title="Sends the same email again to the same address.">Resend</button>
                                            }
                                        }
                                        else
                                        {
                                            <span>—</span>
                                        }
                                    </td>
                                    <td data-label="Booking">
                                        @{
                                            var bookingsExpanded = _expandedBookings.Contains(candidate.CandidateId);
                                            var candidateBookings = _bookings.TryGetValue(candidate.CandidateId, out var loaded) ? loaded : null;
                                        }
                                        <button class="button button-quiet button-small booking-badge"
                                                @onclick="() => ToggleBookingsAsync(candidate.CandidateId)"
                                                aria-expanded="@(bookingsExpanded ? "true" : "false")"
                                                aria-controls="@(bookingsExpanded ? $"bookings-{candidate.CandidateId}" : null)"
                                                title="Shows the candidate's active bookings and the actions that cancel them.">
                                            @BookingSummary(candidateBookings)
                                        </button>
                                        @if (bookingsExpanded)
                                        {
                                            <div class="booking-detail" id="bookings-@candidate.CandidateId">
                                                @if (_bookingsLoading.Contains(candidate.CandidateId))
                                                {
                                                    <span class="booking-loading" role="status" aria-live="polite">Loading bookings…</span>
                                                }
                                                else if (candidateBookings is { Count: 0 })
                                                {
                                                    <span>No active bookings.</span>
                                                }
                                                else if (candidateBookings is not null)
                                                {
                                                    <ul class="booking-list">
                                                        @foreach (var booking in candidateBookings)
                                                        {
                                                            <li>
                                                                <span class="booking-window">
                                                                    @booking.SlotDate.ToString("dd MMM yyyy")
                                                                    @booking.SlotStartTime.ToString("HH\\:mm")–@booking.SlotEndTime.ToString("HH\\:mm")
                                                                    @(booking.IsOriginal ? null : " (recovery)")
                                                                </span>
                                                                <button class="button button-danger button-small"
                                                                        @onclick="() => CancelBookingAsync(candidate.CandidateId, booking.BookingId, false)"
                                                                        disabled="@_busy"
                                                                        title="Cancels this booking and releases its places.">
                                                                    @(_bookingCancelAwaitingConfirmation == booking.BookingId ? "Confirm cancel" : "Cancel booking")
                                                                </button>
                                                                @if (booking.IsOriginal)
                                                                {
                                                                    <button class="button button-small"
                                                                            @onclick="() => CancelBookingAsync(candidate.CandidateId, booking.BookingId, true)"
                                                                            disabled="@_busy"
                                                                            title="Cancels this booking and emails a fresh booking link.">
                                                                        @(_bookingRebookAwaitingConfirmation == booking.BookingId ? "Confirm cancel" : "Cancel & rebook")
                                                                    </button>
                                                                }
                                                            </li>
                                                        }
                                                    </ul>
                                                }

                                                @if (_bookingOutcomes.TryGetValue(candidate.CandidateId, out var bookingOutcome))
                                                {
                                                    <span class="booking-outcome" role="status">@bookingOutcome</span>
                                                }
                                                @if (_bookingErrors.TryGetValue(candidate.CandidateId, out var bookingError))
                                                {
                                                    <span class="booking-error" role="alert">@bookingError</span>
                                                }
                                            </div>
                                        }
                                    </td>
                                    <td data-label="History"><AuditHistory CandidateId="@candidate.CandidateId" /></td>
                                    <td data-label="Actions">
                                        <div class="row-actions">
                                            <button class="button button-small button-icon-edit" @onclick="() => StartEdit(candidate)" disabled="@(_busy || _editingId.HasValue)"
                                                    title="Change the name, email address or employee group.">Edit</button>
                                            <button class="button button-primary button-small" @onclick="() => InviteAsync(candidate)" disabled="@(_busy || _editingId.HasValue)"
                                                    aria-disabled="@(candidate.RequiresEmployeeGroupReconciliation ? "true" : null)"
                                                    title="@(candidate.RequiresEmployeeGroupReconciliation ? "Assign an employee group before inviting" : "Emails a single-use booking link showing only the windows this candidate can take.")">Invite now</button>
                                            <button class="button button-danger button-small button-icon-delete" @onclick="() => DeleteAsync(candidate.CandidateId)" disabled="@(_busy || _editingId.HasValue)"
                                                    title="Removes the candidate. Press a second time to confirm.">
                                                @(awaitingDeleteConfirmation ? "Confirm delete" : "Delete")
                                            </button>
                                        </div>
                                        @if (candidate.RequiresEmployeeGroupReconciliation)
                                        {
                                            <span class="invite-help">Needs an employee group first.</span>
                                        }
                                    </td>
                                }
                            </tr>
                        }
                    </tbody>
                </table>
            </div>

            @if (_candidates.Count == 0)
            {
                <div class="empty-state">
                    <strong>No candidates yet</strong>
                    <p>Add one above, or upload a CSV to bring in several at once.</p>
                </div>
            }
        </div>
    }
</section>

@code {
    private const int MaximumUploadBytes = 1024 * 1024;
    private const string UnexpectedError = "Something went wrong. Please try again.";

    private List<CandidateDto>? _candidates;
    private List<EmployeeGroupOptionDto> _groups = [];
    private List<ImportErrorDto> _importErrors = [];
    private string? _error;
    private bool _busy;
    private bool _isLoading = true;

    private string _search = string.Empty;
    private int? _statusFilter;
    private IReadOnlyDictionary<Guid, CandidateEmailStatusDto> _emailStatus =
        new Dictionary<Guid, CandidateEmailStatusDto>();
    private readonly Dictionary<Guid, CandidateReadinessDto> _readiness = new();
    private readonly HashSet<Guid> _expandedReadiness = new();
    private readonly HashSet<Guid> _readinessLoading = new();
    private readonly Dictionary<Guid, string> _readinessErrors = new();
    private readonly Dictionary<Guid, Guid> _pendingRecoveryInvites = new();
    private readonly Dictionary<Guid, string> _recoveryOutcomes = new();
    private readonly Dictionary<Guid, string> _recoveryErrors = new();
    private Guid? _recoveryCancelAwaitingConfirmation;
    private readonly Dictionary<Guid, List<CandidateBookingDto>> _bookings = new();
    private readonly HashSet<Guid> _expandedBookings = new();
    private readonly HashSet<Guid> _bookingsLoading = new();
    private readonly Dictionary<Guid, string> _bookingOutcomes = new();
    private readonly Dictionary<Guid, string> _bookingErrors = new();
    private Guid? _bookingCancelAwaitingConfirmation;
    private Guid? _bookingRebookAwaitingConfirmation;

    private static readonly (int Value, string Label)[] StatusChoices =
    [
        (1, "Not yet invited"),
        (2, "Awaiting availability"),
        (3, "Invited (pending response)"),
        (4, "Booked"),
        (5, "No response - needs follow-up"),
    ];
    private string _newName = string.Empty;
    private string _newEmail = string.Empty;
    private string _newEmployeeGroupId = string.Empty;
    private Guid? _deleteAwaitingConfirmation;
    private Guid? _editingId;
    private string _editName = string.Empty;
    private string _editEmail = string.Empty;
    private string _editEmployeeGroupId = string.Empty;

    // Visible to the component's test assembly so its busy-event guard can be exercised directly.
    internal bool IsBusyForTesting
    {
        get => _busy;
        set => _busy = value;
    }

    internal string? ErrorForTesting => _error;

    internal bool IsReadinessExpandedForTesting(Guid candidateId) =>
        _expandedReadiness.Contains(candidateId);

    internal CandidateReadinessDto? ReadinessForTesting(Guid candidateId) =>
        _readiness.TryGetValue(candidateId, out var readiness) ? readiness : null;

    internal string? ReadinessErrorForTesting(Guid candidateId) =>
        _readinessErrors.TryGetValue(candidateId, out var error) ? error : null;

    internal Task ToggleReadinessForTestingAsync(Guid candidateId) =>
        ToggleReadinessAsync(candidateId);

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task ToggleReadinessAsync(Guid candidateId)
    {
        if (_busy)
        {
            return;
        }

        if (_expandedReadiness.Contains(candidateId))
        {
            _expandedReadiness.Remove(candidateId);
            return;
        }

        _expandedReadiness.Add(candidateId);
        if (!_readiness.ContainsKey(candidateId))
        {
            await LoadReadinessAsync(candidateId);
        }
    }

    private async Task LoadReadinessAsync(Guid candidateId)
    {
        if (_busy || _readinessLoading.Contains(candidateId))
        {
            return;
        }

        _readinessLoading.Add(candidateId);
        _readinessErrors.Remove(candidateId);

        try
        {
            var outcome = await CandidatesApi.GetReadinessAsync(candidateId, CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _readiness[candidateId] = outcome.Value;
            }
            else
            {
                _readinessErrors[candidateId] = outcome.ErrorMessage ?? UnexpectedError;
            }
        }
        catch (Exception)
        {
            _readinessErrors[candidateId] = UnexpectedError;
        }
        finally
        {
            _readinessLoading.Remove(candidateId);
        }
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;

        try
        {
            var candidatesTask = CandidatesApi.ListAsync(_statusFilter, _search, CancellationToken.None);
            var dashboardsTask = Dashboards.GetAsync(CancellationToken.None);
            var groupsTask = CandidatesApi.ListGroupsAsync(CancellationToken.None);
            await Task.WhenAll(candidatesTask, dashboardsTask, groupsTask);

            var outcome = candidatesTask.Result;
            if (outcome.IsSuccess)
            {
                _candidates = outcome.Value;
                _error = null;
            }
            else
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
            }

            var groupsOutcome = groupsTask.Result;
            if (groupsOutcome.IsSuccess)
            {
                _groups = groupsOutcome.Value ?? [];
            }
            else if (_error is null)
            {
                _error = groupsOutcome.ErrorMessage ?? UnexpectedError;
            }

            _emailStatus = dashboardsTask.Result.Value?.EmailStatuses
                .ToDictionary(e => e.CandidateId)
                ?? new Dictionary<Guid, CandidateEmailStatusDto>();
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

    private IReadOnlyList<AppointmentTypeSummaryDto> SelectedGroupTypes(string groupIdValue) =>
        Guid.TryParse(groupIdValue, out var groupId)
            ? _groups.FirstOrDefault(group => group.EmployeeGroupId == groupId)?.RequiredAppointmentTypes
                ?? []
            : [];

    private static Guid? ParseEmployeeGroupId(string value) =>
        Guid.TryParse(value, out var groupId) ? groupId : null;

    private async Task CreateAsync()
    {
        await RunAsync(
            () => CandidatesApi.CreateAsync(
                _newName, _newEmail, ParseEmployeeGroupId(_newEmployeeGroupId), CancellationToken.None),
            () =>
            {
                _newName = string.Empty;
                _newEmail = string.Empty;
                _newEmployeeGroupId = string.Empty;
                return Task.CompletedTask;
            });
    }

    private void StartEdit(CandidateDto candidate)
    {
        _editingId = candidate.CandidateId;
        _editName = candidate.Name;
        _editEmail = candidate.Email;
        _editEmployeeGroupId = candidate.EmployeeGroupId?.ToString() ?? string.Empty;
    }

    private void CancelEdit() => _editingId = null;

    private Task SaveEditAsync()
    {
        var id = _editingId!.Value;
        return RunAsync(
            () => CandidatesApi.UpdateAsync(
                id, _editName, _editEmail, ParseEmployeeGroupId(_editEmployeeGroupId), CancellationToken.None),
            () =>
            {
                _editingId = null;
                return Task.CompletedTask;
            });
    }

    private async Task DeleteAsync(Guid id)
    {
        var confirm = _deleteAwaitingConfirmation == id;
        _busy = true;

        try
        {
            var outcome = await CandidatesApi.DeleteAsync(id, confirm, CancellationToken.None);
            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _deleteAwaitingConfirmation = id;
                _error = $"{outcome.ErrorMessage ?? UnexpectedError} Click Delete again to confirm.";
                return;
            }

            _deleteAwaitingConfirmation = null;
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;
            if (outcome.IsSuccess)
            {
                await ReloadAsync();
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

    private Task InviteAsync(CandidateDto candidate)
    {
        if (candidate.RequiresEmployeeGroupReconciliation)
        {
            _error = "Assign an employee group before inviting.";
            return Task.CompletedTask;
        }

        return RunAsync(() => CandidatesApi.TriggerInviteAsync(candidate.CandidateId, CancellationToken.None));
    }

    private Task RetryEmailAsync(Guid id) =>
        RunAsync(() => CandidatesApi.RetryEmailAsync(id, CancellationToken.None));

    private static List<string> RecoverableTypeNames(CandidateReadinessDto readiness) =>
        readiness.OutstandingAppointmentTypes
            .Where(type => type.IsRecoverable)
            .Select(type => type.Name)
            .ToList();

    private static string DescribeRecoveryScope(IReadOnlyList<string> names, int selectedCount)
    {
        var count = Math.Max(selectedCount, names.Count);
        var noun = count == 1 ? "missed appointment" : "missed appointments";
        return names.Count == 0
            ? $"{count} {noun}"
            : $"{count} {noun}: {string.Join(", ", names)}";
    }

    private async Task StartRecoveryAsync(Guid candidateId, List<string> recoverableNames)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _recoveryErrors.Remove(candidateId);

        try
        {
            var outcome = await CandidatesApi.StartRecoveryAsync(candidateId, CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _recoveryErrors[candidateId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            var result = outcome.Value;
            var scope = DescribeRecoveryScope(recoverableNames, result.AppointmentTypeIds.Count);
            if (result.InviteId == Guid.Empty)
            {
                _recoveryOutcomes[candidateId] = $"No appointments are available yet for {scope}.";
                return;
            }

            _pendingRecoveryInvites[candidateId] = result.InviteId;
            _recoveryOutcomes[candidateId] = result.EmailSent
                ? $"Recovery started for {scope}. Email sent."
                : $"Recovery started for {scope}, but the email could not be sent.";
        }
        catch (Exception)
        {
            _recoveryErrors[candidateId] = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }

        await LoadReadinessAsync(candidateId);
    }

    /// <summary>Collapsed label: nothing to act on reads as a dash, otherwise the active count.</summary>
    private static string BookingSummary(List<CandidateBookingDto>? bookings) => bookings switch
    {
        null => "Bookings",
        { Count: 0 } => "No active booking",
        { Count: 1 } => "1 active booking",
        _ => $"{bookings.Count} active bookings",
    };

    private async Task ToggleBookingsAsync(Guid candidateId)
    {
        if (_busy)
        {
            return;
        }

        if (_expandedBookings.Contains(candidateId))
        {
            _expandedBookings.Remove(candidateId);
            return;
        }

        _expandedBookings.Add(candidateId);
        if (!_bookings.ContainsKey(candidateId))
        {
            await LoadBookingsAsync(candidateId);
        }
    }

    private async Task LoadBookingsAsync(Guid candidateId)
    {
        if (_bookingsLoading.Contains(candidateId))
        {
            return;
        }

        _bookingsLoading.Add(candidateId);
        _bookingErrors.Remove(candidateId);

        try
        {
            var outcome = await CandidatesApi.GetBookingsAsync(candidateId, CancellationToken.None);
            if (outcome is { IsSuccess: true, Value: not null })
            {
                _bookings[candidateId] = outcome.Value;
            }
            else
            {
                _bookingErrors[candidateId] = outcome.ErrorMessage ?? UnexpectedError;
            }
        }
        catch (Exception)
        {
            _bookingErrors[candidateId] = UnexpectedError;
        }
        finally
        {
            _bookingsLoading.Remove(candidateId);
            StateHasChanged();
        }
    }

    // Each action confirms on its own second click, and the two actions never share a pending
    // confirmation: arming one disarms the other.
    private async Task CancelBookingAsync(Guid candidateId, Guid bookingId, bool rebook)
    {
        if (_busy)
        {
            return;
        }

        var armed = rebook
            ? _bookingRebookAwaitingConfirmation == bookingId
            : _bookingCancelAwaitingConfirmation == bookingId;
        if (!armed)
        {
            _bookingCancelAwaitingConfirmation = rebook ? null : bookingId;
            _bookingRebookAwaitingConfirmation = rebook ? bookingId : null;
            return;
        }

        _bookingCancelAwaitingConfirmation = null;
        _bookingRebookAwaitingConfirmation = null;
        _busy = true;
        _bookingErrors.Remove(candidateId);
        _bookingOutcomes.Remove(candidateId);

        try
        {
            var outcome = await CandidatesApi.CancelBookingAsync(
                candidateId, bookingId, rebook, CancellationToken.None);
            if (outcome is not { IsSuccess: true, Value: not null })
            {
                _bookingErrors[candidateId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _bookingOutcomes[candidateId] = CancellationMessage(outcome.Value);
        }
        catch (Exception)
        {
            _bookingErrors[candidateId] = UnexpectedError;
            return;
        }
        finally
        {
            _busy = false;
        }

        _bookings.Remove(candidateId);
        await LoadBookingsAsync(candidateId);
        await ReloadAsync();
    }

    /// <summary>Distinguishes a plain cancellation from a delivered and an undelivered replacement.</summary>
    private static string CancellationMessage(CancelCandidateBookingDto outcome)
    {
        if (!outcome.Reinvited)
        {
            return "Booking cancelled.";
        }

        return outcome.DeliveryStatus == "Sent"
            ? "Booking cancelled; replacement invite sent."
            : "Booking cancelled; replacement invite could not be delivered.";
    }

    private async Task CancelRecoveryAsync(Guid candidateId)
    {
        if (_busy)
        {
            return;
        }

        if (_recoveryCancelAwaitingConfirmation != candidateId)
        {
            _recoveryCancelAwaitingConfirmation = candidateId;
            return;
        }

        _recoveryCancelAwaitingConfirmation = null;
        if (!_pendingRecoveryInvites.TryGetValue(candidateId, out var inviteId))
        {
            return;
        }

        _busy = true;
        _recoveryErrors.Remove(candidateId);

        try
        {
            var outcome = await CandidatesApi.CancelRecoveryAsync(
                candidateId, inviteId, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _recoveryErrors[candidateId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _pendingRecoveryInvites.Remove(candidateId);
            _recoveryOutcomes[candidateId] = "Recovery cancelled.";
        }
        catch (Exception)
        {
            _recoveryErrors[candidateId] = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }

        await LoadReadinessAsync(candidateId);
    }

    internal async Task OnFileChosenAsync(InputFileChangeEventArgs args)
    {
        if (_busy)
        {
            return;
        }

        _importErrors = [];
        _error = null;
        _busy = true;

        try
        {
            if (!args.File.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                _error = "Choose a CSV file to upload.";
                return;
            }

            if (args.File.Size > MaximumUploadBytes)
            {
                _error = "The CSV must be 1 MiB or smaller.";
                return;
            }

            using var stream = args.File.OpenReadStream(MaximumUploadBytes);
            using var reader = new StreamReader(stream);
            var csv = await reader.ReadToEndAsync();
            var outcome = await CandidatesApi.ImportAsync(csv, CancellationToken.None);
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;

            if (outcome.IsSuccess && outcome.Value is { Accepted: false } import)
            {
                _importErrors = import.Errors.ToList();
            }
            else if (outcome.IsSuccess)
            {
                await ReloadAsync();
            }
        }
        catch (Exception)
        {
            _error = "The CSV could not be read or uploaded. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunAsync<T>(Func<Task<ApiOutcome<T>>> action, Func<Task>? onSuccess = null)
    {
        _busy = true;

        try
        {
            var outcome = await action();
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;
            if (outcome.IsSuccess)
            {
                if (onSuccess is not null)
                {
                    await onSuccess();
                }

                await ReloadAsync();
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
}
`````
