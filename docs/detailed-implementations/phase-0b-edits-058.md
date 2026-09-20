# 00b — Vocabulary edits 58 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Web/Pages/Candidates.razor — 1/1

<!-- vocabulary-file: {"id":192,"oldPath":"src/EventBooking.Web/Pages/Candidates.razor","newPath":"src/EventBooking.Web/Pages/Attendees.razor","beforeSha":"56dccdbfbd22b12c392a35db285380b21f8cc5db4c1d06e2edeeba1a45c74742","afterSha":"1c194da4c4afa4869e218c33d58934df7535776a54441904740d6662d24ca4fb","side":"before","part":1,"parts":1} -->

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
