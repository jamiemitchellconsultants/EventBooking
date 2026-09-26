using Microsoft.AspNetCore.Components;
using EventBooking.Web.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class EventGroups
{
    [Inject] private IEventGroupsClient Groups { get; set; } = default!;
    [Inject] private AdminClient Admin { get; set; } = default!;
    [Inject] private IEventsClient Events { get; set; } = default!;
    [Inject] private IMeClient CurrentStaff { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private List<EventGroupDto> Rows { get; } = [];
    private EventGroupForm? _edit;
    private bool _creating, _canCreate, _loading = true, _busy, _adding;
    private string? _error, _candidateNote;
    private long? _conflictVersion;
    private Guid? _candidateEventId;
    private IdempotencySubmission? _submission;
    private IReadOnlyList<AttendeeGroupDto> GroupOptions { get; set; } = [];
    private IReadOnlyList<EventDto> FutureEvents { get; set; } = [];
    private IReadOnlyList<AppointmentTypeDto> TypeOptions { get; set; } = [];
    private IReadOnlyList<TableColumn<EventGroupDto>> Columns => [
        new("Title", x => b => b.AddContent(0, x.Title)),
        new("Publication", x => b => b.AddContent(0, x.IsOpen ? "Open" : "Closed")),
        new("Groups", x => b => b.AddContent(0, x.AttendeeGroupIds.Count)),
        new("Events", x => b => b.AddContent(0, x.Events.Count)),
        new("Actions", x => b =>
        {
            if (_canCreate)
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "data-action", "edit");
                b.AddAttribute(2, "class", "button");
                b.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () => Edit(x)));
                b.AddContent(4, "Edit");
                b.CloseElement();
            }
        }),
    ];

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            var context = await CurrentStaff.GetAsync(CancellationToken.None);
            _canCreate = context.IsSuccess
                && context.Value!.Links.Allows("createEventGroup");
            var groups = await Groups.ListAsync(CancellationToken.None);
            if (groups.IsSuccess) { Rows.Clear(); Rows.AddRange(groups.Value!.Items); }
            else _error = groups.ErrorMessage;
            var attendeeGroups = await Admin.ListAttendeeGroupsAsync(false, CancellationToken.None);
            if (attendeeGroups.IsSuccess) GroupOptions = attendeeGroups.Value!.Items;
            var types = await Admin.ListAppointmentTypesAsync(false, CancellationToken.None);
            if (types.IsSuccess) TypeOptions = types.Value!.Items;
            var events = await Events.ListEventsAsync(null, DateOnly.FromDateTime(DateTime.Today), null, null, CancellationToken.None);
            if (events.IsSuccess) FutureEvents = events.Value!.Items;
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _loading = false;
    }

    private void New()
    {
        _creating = true;
        _conflictVersion = null;
        _submission = IdempotencySubmission.Start();
        _candidateEventId = null;
        _candidateNote = null;
        _edit = new EventGroupForm();
    }

    private void Edit(EventGroupDto row)
    {
        _creating = false;
        _conflictVersion = null;
        _candidateEventId = null;
        _candidateNote = null;
        _edit = EventGroupForm.From(row);
    }

    private void Cancel()
    {
        _edit = null;
        _creating = false;
        _conflictVersion = null;
        _candidateEventId = null;
        _candidateNote = null;
    }

    private void ToggleGroup(Guid id, bool selected) =>
        _edit!.AttendeeGroupIds = selected
            ? [.. _edit.AttendeeGroupIds, id]
            : [.. _edit.AttendeeGroupIds.Where(x => x != id)];

    private string DerivedTypeNames(EventGroupForm form)
    {
        var ids = GroupOptions
            .Where(g => form.AttendeeGroupIds.Contains(g.Id))
            .SelectMany(g => g.RequirementTypeIds)
            .Distinct().Order().ToArray();
        if (ids.Length == 0) return "none — select at least one group";
        return string.Join(", ", ids.Select(TypeName));
    }

    private string TypeName(Guid id) =>
        TypeOptions.FirstOrDefault(t => t.Id == id)?.Name ?? id.ToString("D");

    private string EventLabel(Guid id)
    {
        var found = FutureEvents.FirstOrDefault(e => e.Id == id);
        return found is null ? id.ToString("D") : $"{found.LocationName} — {found.Time}";
    }

    private IReadOnlyList<(Guid Id, string Label, bool Compatible)> CandidateEvents()
    {
        if (_edit is null) return [];
        var memberIds = _edit.Events.Select(m => m.EventId).ToHashSet();
        var expected = GroupOptions
            .Where(g => _edit.AttendeeGroupIds.Contains(g.Id))
            .SelectMany(g => g.RequirementTypeIds).Distinct().Order().ToArray();
        return FutureEvents
            .Where(e => !memberIds.Contains(e.Id) && e.Status == "Active")
            .Select(e =>
            {
                var actual = e.Capacities.Select(c => c.AppointmentTypeId).Order().ToArray();
                var compatible = actual.SequenceEqual(expected) && expected.Length > 0;
                var label = compatible
                    ? EventLabel(e.Id)
                    : $"{EventLabel(e.Id)} — needs {string.Join(", ", expected.Select(TypeName))}, event offers {string.Join(", ", actual.Select(TypeName))}";
                return (e.Id, label, compatible);
            }).ToArray();
    }

    private async Task SaveAsync()
    {
        if (_edit is null) return;
        _busy = true;
        _error = null;
        try
        {
            var outcome = _creating
                ? await Groups.CreateAsync(_edit.Title, _edit.Description, _edit.AttendeeGroupIds, _submission!, CancellationToken.None)
                : await Groups.UpdateAsync(_edit.ToDto(), CancellationToken.None);
            if (outcome.IsSuccess) { Cancel(); await LoadAsync(); }
            else
            {
                _error = outcome.ErrorMessage;
                if (outcome.ErrorCode == "version-conflict" && outcome.Problem?.Current is { } current
                    && current.TryGetProperty("currentVersion", out var version) && version.TryGetInt64(out var number))
                {
                    _conflictVersion = number;
                    _edit.Version = number;
                }
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _busy = false;
    }

    private async Task ToggleMemberAsync(Guid eventId, bool open)
    {
        if (_edit is null) return;
        _error = null;
        var outcome = await Groups.SetEventOpenAsync(_edit.Id, eventId, open, _edit.Version, CancellationToken.None);
        if (outcome.IsSuccess && outcome.Value is not null)
        {
            _edit = EventGroupForm.From(outcome.Value);
            var row = Rows.FindIndex(r => r.Id == _edit.Id);
            if (row >= 0) Rows[row] = outcome.Value;
        }
        else
        {
            _error = outcome.ErrorMessage;
            await LoadAsync();
            var reloaded = Rows.FirstOrDefault(r => r.Id == _edit.Id);
            if (reloaded is not null) _edit = EventGroupForm.From(reloaded);
        }
    }

    private async Task AddEventAsync()
    {
        if (_edit is null || _candidateEventId is null) return;
        _adding = true;
        _error = null;
        _candidateNote = null;
        var outcome = await Groups.AddEventAsync(_edit.Id, _candidateEventId.Value, _edit.Version, CancellationToken.None);
        if (outcome.IsSuccess && outcome.Value is not null)
        {
            _edit = EventGroupForm.From(outcome.Value);
            var row = Rows.FindIndex(r => r.Id == _edit.Id);
            if (row >= 0) Rows[row] = outcome.Value;
            _candidateEventId = null;
        }
        else
        {
            if (outcome.ErrorCode == "version-conflict" && outcome.Problem?.Current is { } current
                && current.TryGetProperty("currentVersion", out var version) && version.TryGetInt64(out var number))
            {
                _conflictVersion = number;
                _edit.Version = number;
            }
            else
            {
                _candidateNote = outcome.ErrorMessage;
            }
            _error = outcome.ErrorMessage;
        }
        _adding = false;
    }

    private async Task RemoveEventAsync(Guid eventId)
    {
        if (_edit is null) return;
        _error = null;
        var outcome = await Groups.RemoveEventAsync(_edit.Id, eventId, _edit.Version, CancellationToken.None);
        if (outcome.IsSuccess && outcome.Value is not null)
        {
            _edit = EventGroupForm.From(outcome.Value);
            var row = Rows.FindIndex(r => r.Id == _edit.Id);
            if (row >= 0) Rows[row] = outcome.Value;
        }
        else
        {
            _error = outcome.ErrorMessage;
        }
    }

    private sealed class EventGroupForm
    {
        public Guid Id { get; init; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsOpen { get; set; }
        public long Version { get; set; }
        public IReadOnlyList<Guid> AttendeeGroupIds { get; set; } = [];
        public List<EventGroupEventDto> Events { get; set; } = [];
        public static EventGroupForm From(EventGroupDto x) => new()
        {
            Id = x.Id,
            Title = x.Title,
            Description = x.Description ?? "",
            IsOpen = x.IsOpen,
            Version = x.Version,
            AttendeeGroupIds = x.AttendeeGroupIds,
            Events = [.. x.Events.Select(e => new EventGroupEventDto(e.EventId, e.IsOpen))],
        };
        public EventGroupDto ToDto() => new(Id, Title, Description, IsOpen, Version,
            AttendeeGroupIds, [], [.. Events.Select(e => new EventGroupEventDto(e.EventId, e.IsOpen))]);
    }
}
