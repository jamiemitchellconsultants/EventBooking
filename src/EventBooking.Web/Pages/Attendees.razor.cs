using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class Attendees
{
    [Inject] private IAttendeesClient AttendeesApi { get; set; } = default!;

    private const int MaximumUploadBytes = 1024 * 1024;
    private const string UnexpectedError = "Something went wrong. Please try again.";

    private List<AttendeeDto>? _attendees;
    private List<CoordinatorLocationDto> _locations = [];
    private List<CoordinatorGroupDto> _groups = [];
    private List<TypeSummaryDto> _types = [];
    private IReadOnlyDictionary<string, ApiLink> _collectionLinks =
        new Dictionary<string, ApiLink>();
    private AttendeeDto? _inviting;
    private EligibleEventCountDto? _eligibleCount;
    private readonly HashSet<Guid> _inviteLocationSelection = [];
    private bool _showImport;
    private IBrowserFile? _importFile;
    private ImportOutcomeDto? _uploadResult;
    private string? _error;
    private bool _busy;
    private readonly PendingSubmission _createSubmission = new();
    private readonly PendingSubmission _inviteSubmission = new();
    private readonly PendingSubmission _importSubmission = new();
    private bool _isLoading = true;
    private bool _showAdd;

    private string _search = string.Empty;
    private string? _statusFilter;
    private Guid? _groupId;
    private string? _readinessFilter;
    private readonly Dictionary<Guid, AttendeeReadinessDto> _readiness = new();
    private readonly HashSet<Guid> _expandedReadiness = new();
    private readonly HashSet<Guid> _readinessLoading = new();
    private readonly Dictionary<Guid, string> _readinessErrors = new();
    private readonly Dictionary<Guid, Guid> _pendingRecoveryInvites = new();
    private readonly Dictionary<Guid, string> _recoveryOutcomes = new();
    private readonly Dictionary<Guid, string> _recoveryErrors = new();
    private Guid? _recoveryCancelAwaitingConfirmation;
    private readonly Dictionary<Guid, List<AttendeeBookingDto>> _bookings = new();
    private readonly HashSet<Guid> _expandedBookings = new();
    private readonly HashSet<Guid> _bookingsLoading = new();
    private readonly Dictionary<Guid, string> _bookingOutcomes = new();
    private readonly Dictionary<Guid, string> _bookingErrors = new();
    private Guid? _bookingCancelAwaitingConfirmation;

    private static readonly (string Value, string Label)[] StatusChoices =
    [
        ("NotYetInvited", "Not yet invited"),
        ("AwaitingAvailability", "Awaiting availability"),
        ("Invited", "Invited (pending response)"),
        ("Booked", "Booked"),
        ("NoResponseNeedsFollowUp", "No response - needs follow-up"),
    ];

    // The list's readiness filter is a status predicate in the API: only these two
    // labels select rows, and anything else matches nothing.
    private static readonly (string Value, string Label)[] ReadinessChoices =
    [
        ("Booked", "Booked"),
        ("NoActiveBooking", "No active booking"),
    ];

    private string _newName = string.Empty;
    private string _newEmail = string.Empty;
    private string _newAttendeeGroupId = string.Empty;
    private Guid? _deleteAwaitingConfirmation;
    private Guid? _editingId;
    private string _editName = string.Empty;
    private string _editEmail = string.Empty;
    private string _editAttendeeGroupId = string.Empty;

    // Visible to the component's test assembly so its busy-event guard can be exercised directly.
    internal bool IsBusyForTesting
    {
        get => _busy;
        set => _busy = value;
    }

    internal string? ErrorForTesting => _error;

    internal bool IsReadinessExpandedForTesting(Guid attendeeId) =>
        _expandedReadiness.Contains(attendeeId);

    internal AttendeeReadinessDto? ReadinessForTesting(Guid attendeeId) =>
        _readiness.TryGetValue(attendeeId, out var readiness) ? readiness : null;

    internal string? ReadinessErrorForTesting(Guid attendeeId) =>
        _readinessErrors.TryGetValue(attendeeId, out var error) ? error : null;

    internal Task ToggleReadinessForTestingAsync(Guid attendeeId) =>
        ToggleReadinessAsync(attendeeId);

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task OnGroupChanged(ChangeEventArgs args)
    {
        _groupId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : null;
        await ReloadAsync();
    }

    private async Task OnReadinessFilterChanged(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();
        _readinessFilter = string.IsNullOrEmpty(value) ? null : value;
        await ReloadAsync();
    }

    private async Task ToggleReadinessAsync(Guid attendeeId)
    {
        if (_busy)
        {
            return;
        }

        if (_expandedReadiness.Contains(attendeeId))
        {
            _expandedReadiness.Remove(attendeeId);
            return;
        }

        _expandedReadiness.Add(attendeeId);
        if (!_readiness.ContainsKey(attendeeId))
        {
            await LoadReadinessAsync(attendeeId);
        }
    }

    private async Task LoadReadinessAsync(Guid attendeeId)
    {
        if (_busy || _readinessLoading.Contains(attendeeId))
        {
            return;
        }

        _readinessLoading.Add(attendeeId);
        _readinessErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.GetReadinessAsync(attendeeId, CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _readiness[attendeeId] = outcome.Value;
            }
            else
            {
                _readinessErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
            }
        }
        catch (Exception)
        {
            _readinessErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _readinessLoading.Remove(attendeeId);
        }
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            var reference = await AttendeesApi.GetReferenceDataAsync(CancellationToken.None);
            if (!reference.IsSuccess || reference.Value is null)
            {
                _error = reference.ErrorMessage ?? UnexpectedError;
                return;
            }

            _locations = reference.Value.Locations.ToList();
            _groups = reference.Value.Groups.ToList();
            _types = reference.Value.AppointmentTypes.ToList();
            _collectionLinks = reference.Value.CollectionLinks;

            // The list is keyset-paged; the page walks every page so the coordinator
            // keeps seeing the whole filtered set.
            var attendees = new List<AttendeeDto>();
            string? cursor = null;
            do
            {
                var outcome = await AttendeesApi.ListAsync(
                    EmptyToNull(_statusFilter), _groupId, _readinessFilter,
                    EmptyToNull(_search), cursor, CancellationToken.None);
                if (!outcome.IsSuccess)
                {
                    _error = outcome.ErrorMessage ?? UnexpectedError;
                    break;
                }

                attendees.AddRange(outcome.Value?.Items ?? []);
                cursor = outcome.Value?.NextCursor;
            } while (cursor is not null);

            if (_error is null)
            {
                _attendees = attendees;
            }
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

    private IReadOnlyList<string> SelectedGroupTypes(string groupIdValue)
    {
        if (!Guid.TryParse(groupIdValue, out var groupId))
        {
            return [];
        }

        var group = _groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
        {
            return [];
        }

        return _types
            .Where(type => group.RequirementTypeIds.Contains(type.Id))
            .Select(type => type.Code)
            .ToArray();
    }

    private static Guid? ParseAttendeeGroupId(string value) =>
        Guid.TryParse(value, out var groupId) ? groupId : null;

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task CreateAsync()
    {
        await RunAsync(
            () => AttendeesApi.CreateAsync(
                _newName, _newEmail, ParseAttendeeGroupId(_newAttendeeGroupId),
                _createSubmission.For((_newName, _newEmail, _newAttendeeGroupId)),
                CancellationToken.None),
            () =>
            {
                _createSubmission.Complete();
                _newName = string.Empty;
                _newEmail = string.Empty;
                _newAttendeeGroupId = string.Empty;
                _showAdd = false;
                return Task.CompletedTask;
            });
    }

    private void StartEdit(AttendeeDto attendee)
    {
        _editingId = attendee.AttendeeId;
        _editName = attendee.Name;
        _editEmail = attendee.Email;
        _editAttendeeGroupId = _groups
            .Single(group => group.Code == attendee.GroupCode).Id.ToString();
    }

    private void CancelEdit() => _editingId = null;

    private Task SaveEditAsync()
    {
        var id = _editingId!.Value;
        return RunAsync(
            () => AttendeesApi.UpdateAsync(
                id, _editName, _editEmail, ParseAttendeeGroupId(_editAttendeeGroupId), CancellationToken.None),
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
            var outcome = await AttendeesApi.DeleteAsync(id, confirm, CancellationToken.None);
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

    private Task RetryEmailAsync(Guid id) =>
        RunAsync(() => AttendeesApi.RetryEmailAsync(id, CancellationToken.None));

    private async Task StartInviteAsync(AttendeeDto attendee)
    {
        // Start from every active location: narrowing is the exception, so the coordinator
        // unticks rather than ticks.
        _inviting = attendee;
        _inviteLocationSelection.Clear();
        _inviteLocationSelection.UnionWith(_locations.Select(location => location.Id));
        _eligibleCount = null;
        await RefreshInviteCountAsync();
    }

    private void CloseInvite()
    {
        _inviting = null;
        _eligibleCount = null;
    }

    private async Task OnInviteLocationChangedAsync(Guid locationId, bool selected)
    {
        if (selected)
        {
            _inviteLocationSelection.Add(locationId);
        }
        else
        {
            _inviteLocationSelection.Remove(locationId);
        }

        await RefreshInviteCountAsync();
    }

    private async Task RefreshInviteCountAsync()
    {
        if (_inviting is null || _inviteLocationSelection.Count == 0)
        {
            _eligibleCount = null;
            return;
        }

        try
        {
            var outcome = await AttendeesApi.CountEligibleAsync(
                _inviting.AttendeeId, _inviteLocationSelection.ToList(), CancellationToken.None);
            _eligibleCount = outcome.IsSuccess ? outcome.Value : null;
        }
        catch (Exception)
        {
            _eligibleCount = null;
        }
    }

    private string InviteCountText()
    {
        if (_inviteLocationSelection.Count == 0)
        {
            return "Tick at least one location to see how many events the invite would offer.";
        }

        if (_eligibleCount is null)
        {
            return "Counting eligible events…";
        }

        return _eligibleCount.Count < _eligibleCount.RequiredOptionCount
            ? $"There are only {_eligibleCount.Count} eligible events at these locations — " +
              $"need {_eligibleCount.RequiredOptionCount} before an invite can be sent."
            : $"{_eligibleCount.Count} eligible events at these locations.";
    }

    private async Task SendInviteAsync()
    {
        if (_inviting is null || _inviteLocationSelection.Count == 0)
        {
            return;
        }

        var attendee = _inviting;
        _busy = true;

        try
        {
            var locationIds = _inviteLocationSelection.Order().ToList();
            var outcome = await AttendeesApi.InviteAsync(
                attendee.AttendeeId, locationIds,
                _inviteSubmission.For((attendee.AttendeeId, string.Join(',', locationIds))),
                CancellationToken.None);
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;
            if (outcome.IsSuccess)
            {
                _inviteSubmission.Complete();
                CloseInvite();
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

    private void OpenImport()
    {
        _showImport = true;
        _importFile = null;
        _uploadResult = null;
    }

    private void CloseImport()
    {
        _showImport = false;
        _importFile = null;
        _uploadResult = null;
    }

    private IReadOnlyList<FieldProblem> _uploadErrors =>
        _uploadResult?.Errors
            .Select(error => new FieldProblem(null, error.LineNumber, "invalid", error.Message))
            .ToArray()
        ?? [];

    private static List<string> RecoverableTypeNames(AttendeeReadinessDto readiness) =>
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

    private async Task StartRecoveryAsync(Guid attendeeId, List<string> recoverableNames)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _recoveryErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.StartRecoveryAsync(attendeeId, CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _recoveryErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            var result = outcome.Value;
            var scope = DescribeRecoveryScope(recoverableNames, result.RecoverableTypeIds.Count);
            _pendingRecoveryInvites[attendeeId] = result.RecoveryInviteId;
            _recoveryOutcomes[attendeeId] = $"Recovery started for {scope}.";
        }
        catch (Exception)
        {
            _recoveryErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }

        await LoadReadinessAsync(attendeeId);
    }

    /// <summary>Collapsed label: nothing to act on reads as a dash, otherwise the active count.</summary>
    private bool BookingsExpanded(Guid attendeeId) => _expandedBookings.Contains(attendeeId);

    private List<AttendeeBookingDto>? BookingsFor(Guid attendeeId) =>
        _bookings.TryGetValue(attendeeId, out var loaded) ? loaded : null;

    private static string BookingSummary(List<AttendeeBookingDto>? bookings) => bookings switch
    {
        null => "Bookings",
        { Count: 0 } => "No active booking",
        { Count: 1 } => "1 active booking",
        _ => $"{bookings.Count} active bookings",
    };

    private async Task ToggleBookingsAsync(Guid attendeeId)
    {
        if (_busy)
        {
            return;
        }

        if (_expandedBookings.Contains(attendeeId))
        {
            _expandedBookings.Remove(attendeeId);
            return;
        }

        _expandedBookings.Add(attendeeId);
        if (!_bookings.ContainsKey(attendeeId))
        {
            await LoadBookingsAsync(attendeeId);
        }
    }

    private async Task LoadBookingsAsync(Guid attendeeId)
    {
        if (_bookingsLoading.Contains(attendeeId))
        {
            return;
        }

        _bookingsLoading.Add(attendeeId);
        _bookingErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.GetBookingsAsync(attendeeId, CancellationToken.None);
            if (outcome is { IsSuccess: true, Value: not null })
            {
                _bookings[attendeeId] = outcome.Value.Items.ToList();
            }
            else
            {
                _bookingErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
            }
        }
        catch (Exception)
        {
            _bookingErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _bookingsLoading.Remove(attendeeId);
            StateHasChanged();
        }
    }

    // The first click arms the button; the second carries the API confirmation.
    private async Task CancelBookingAsync(Guid attendeeId, Guid bookingId)
    {
        if (_busy)
        {
            return;
        }

        if (_bookingCancelAwaitingConfirmation != bookingId)
        {
            _bookingCancelAwaitingConfirmation = bookingId;
            return;
        }

        _bookingCancelAwaitingConfirmation = null;
        _busy = true;
        _bookingErrors.Remove(attendeeId);
        _bookingOutcomes.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.CancelBookingAsync(
                attendeeId, bookingId, true, CancellationToken.None);
            if (outcome is not { IsSuccess: true, Value: not null })
            {
                _bookingErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _bookingOutcomes[attendeeId] = "Booking cancelled.";
        }
        catch (Exception)
        {
            _bookingErrors[attendeeId] = UnexpectedError;
            return;
        }
        finally
        {
            _busy = false;
        }

        _bookings.Remove(attendeeId);
        await LoadBookingsAsync(attendeeId);
        await ReloadAsync();
    }

    private async Task CancelRecoveryAsync(Guid attendeeId)
    {
        if (_busy)
        {
            return;
        }

        if (_recoveryCancelAwaitingConfirmation != attendeeId)
        {
            _recoveryCancelAwaitingConfirmation = attendeeId;
            return;
        }

        _recoveryCancelAwaitingConfirmation = null;
        if (!_pendingRecoveryInvites.TryGetValue(attendeeId, out var inviteId))
        {
            return;
        }

        _busy = true;
        _recoveryErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.CancelRecoveryAsync(
                attendeeId, inviteId, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _recoveryErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _pendingRecoveryInvites.Remove(attendeeId);
            _recoveryOutcomes[attendeeId] = "Recovery cancelled.";
        }
        catch (Exception)
        {
            _recoveryErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }

        await LoadReadinessAsync(attendeeId);
    }

    internal Task OnFileChosenAsync(InputFileChangeEventArgs args)
    {
        if (_busy)
        {
            return Task.CompletedTask;
        }

        _error = null;
        _uploadResult = null;
        _importFile = args.File;
        return Task.CompletedTask;
    }

    private async Task UploadAsync()
    {
        if (_busy || _importFile is null)
        {
            return;
        }

        _error = null;
        _uploadResult = null;
        _busy = true;

        try
        {
            if (!_importFile.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                _error = "Choose a CSV file to upload.";
                return;
            }

            if (_importFile.Size > MaximumUploadBytes)
            {
                _error = "The CSV must be 1 MiB or smaller.";
                return;
            }

            await using var stream = _importFile.OpenReadStream(MaximumUploadBytes);
            var outcome = await AttendeesApi.ImportAsync(
                stream, _importFile.Name,
                _importSubmission.For((_importFile.Name, _importFile.Size, _importFile.LastModified)),
                CancellationToken.None);
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;
            if (outcome.IsSuccess)
            {
                _importSubmission.Complete();
            }

            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _uploadResult = outcome.Value;
                if (outcome.Value.Accepted)
                {
                    await ReloadAsync();
                }
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
