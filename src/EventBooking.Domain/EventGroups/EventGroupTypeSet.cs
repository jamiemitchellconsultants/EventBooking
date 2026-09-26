using EventBooking.Domain.Common;

namespace EventBooking.Domain.EventGroups;

/// <summary>Derives an Event Group's appointment-type set from its selected Attendee Groups.</summary>
public static class EventGroupTypeSet
{
    /// <summary>Unions the selected groups' requirements and requires every member Event to match it exactly.</summary>
    /// <param name="requirements">The selected group identifiers with their required type identifiers.</param>
    /// <param name="memberEventTypes">The capacity type identifiers of every member Event.</param>
    /// <returns>The union in stable identifier order.</returns>
    public static IReadOnlyList<Guid> Validate(
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> requirements,
        IEnumerable<IReadOnlyCollection<Guid>> memberEventTypes)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(memberEventTypes);

        var expected = requirements.Values.SelectMany(ids => ids).Distinct().Order().ToArray();
        if (requirements.Count == 0 || requirements.Values.Any(ids => ids.Count == 0))
            throw new DomainException("An event group needs active attendee groups with requirements.");
        foreach (var actual in memberEventTypes)
            if (!actual.Order().SequenceEqual(expected))
                throw new DomainException("Every event must list exactly the group's appointment types.");
        return expected;
    }
}
