using EventBooking.Domain.Common;

namespace EventBooking.Domain.EventGroups;

/// <summary>An Admin- or Coordinator-managed collection of compatible Events for self-registration.</summary>
public sealed class EventGroup
{
    /// <summary>The longest title an event group may have.</summary>
    public const int MaximumTitleLength = 160;

    /// <summary>The longest description an event group may have.</summary>
    public const int MaximumDescriptionLength = 2000;

    private readonly List<EventGroupAttendeeGroup> _attendeeGroups = [];
    private readonly List<EventGroupEvent> _events = [];

    private EventGroup()
    {
        Title = string.Empty;
        Description = string.Empty;
    }

    /// <summary>Gets the stable identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the public title.</summary>
    public string Title { get; private set; }

    /// <summary>Gets the public description.</summary>
    public string Description { get; private set; }

    /// <summary>Gets whether anonymous registration is open for this group.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Gets the selected Attendee Groups.</summary>
    public IReadOnlyList<EventGroupAttendeeGroup> AttendeeGroups => _attendeeGroups;

    /// <summary>Gets the member Events with their own publication gates.</summary>
    public IReadOnlyList<EventGroupEvent> Events => _events;

    /// <summary>Creates a closed group serving the selected Attendee Groups.</summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="title">The public title.</param>
    /// <param name="description">The public description.</param>
    /// <param name="requirements">The selected group identifiers with their required type identifiers.</param>
    public static EventGroup Create(
        Guid id,
        string? title,
        string? description,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        EventGroupTypeSet.Validate(requirements, []);

        var group = new EventGroup
        {
            Id = id,
            Title = BoundedTitle(title),
            Description = BoundedDescription(description),
            IsOpen = false,
            Version = 1,
        };
        group.ReplaceSelectedGroups(requirements);
        return group;
    }

    /// <summary>Edits copy or selected groups, refusing a union change that breaks a member Event.</summary>
    /// <param name="title">The public title.</param>
    /// <param name="description">The public description.</param>
    /// <param name="requirements">The selected group identifiers with their required type identifiers.</param>
    /// <param name="memberEventTypes">The capacity type identifiers of every member Event.</param>
    public void Edit(
        string? title,
        string? description,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> memberEventTypes)
    {
        EventGroupTypeSet.Validate(requirements, memberEventTypes.Values);

        var nextTitle = BoundedTitle(title);
        var nextDescription = BoundedDescription(description);
        var nextSelected = requirements.Keys.Order().ToArray();
        if (nextTitle == Title && nextDescription == Description &&
            nextSelected.SequenceEqual(_attendeeGroups.Select(x => x.AttendeeGroupId).Order()))
            return;

        Title = nextTitle;
        Description = nextDescription;
        ReplaceSelectedGroups(requirements);
        Version++;
    }

    /// <summary>Adds a future compatible Event as a private membership.</summary>
    /// <param name="eventId">The member Event identifier.</param>
    /// <param name="eventTypes">The Event's capacity type identifiers.</param>
    /// <param name="requirements">The selected group identifiers with their required type identifiers.</param>
    /// <param name="isFuture">Whether the Event window starts in the future.</param>
    public void AddEvent(
        Guid eventId,
        IReadOnlyCollection<Guid> eventTypes,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements,
        bool isFuture)
    {
        Guard.Against(eventId == Guid.Empty, "eventId must not be empty.");
        Guard.Against(!isFuture, "Only future events can join an event group.");
        Guard.Against(
            _events.Any(x => x.EventId == eventId),
            "The event is already a member of this event group.");
        EventGroupTypeSet.Validate(requirements, [eventTypes]);

        _events.Add(EventGroupEvent.Join(Id, eventId));
        Version++;
    }

    /// <summary>Removes an Event membership; existing bookings stay valid.</summary>
    /// <param name="eventId">The member Event identifier.</param>
    public void RemoveEvent(Guid eventId)
    {
        var membership = _events.SingleOrDefault(x => x.EventId == eventId);
        Guard.Against(membership is null, "The event is not a member of this event group.");
        _events.Remove(membership!);
        Version++;
    }

    /// <summary>Opens or closes the group gate; member gates are independent.</summary>
    /// <param name="open">Whether anonymous registration is open for this group.</param>
    public void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        Version++;
    }

    /// <summary>Opens or closes one membership gate; the group gate is independent.</summary>
    /// <param name="eventId">The member Event identifier.</param>
    /// <param name="open">Whether anonymous registration is open for this membership.</param>
    public void SetEventOpen(Guid eventId, bool open)
    {
        var membership = _events.SingleOrDefault(x => x.EventId == eventId);
        Guard.Against(membership is null, "The event is not a member of this event group.");
        if (membership!.IsOpen == open) return;
        membership.SetOpen(open);
        Version++;
    }

    private void ReplaceSelectedGroups(IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements)
    {
        _attendeeGroups.Clear();
        foreach (var groupId in requirements.Keys.Order())
        {
            Guard.Against(groupId == Guid.Empty, "attendeeGroupId must not be empty.");
            _attendeeGroups.Add(EventGroupAttendeeGroup.For(Id, groupId));
        }
    }

    private static string BoundedTitle(string? title)
    {
        var text = Guard.NotBlank(title, "title");
        Guard.Against(
            text.Length > MaximumTitleLength,
            $"title must be at most {MaximumTitleLength} characters.");
        return text;
    }

    private static string BoundedDescription(string? description)
    {
        var text = (description ?? string.Empty).Trim();
        Guard.Against(
            text.Length > MaximumDescriptionLength,
            $"description must be at most {MaximumDescriptionLength} characters.");
        return text;
    }
}
