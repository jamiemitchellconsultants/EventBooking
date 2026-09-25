using EventBooking.Domain.Common;
using EventBooking.Domain.EventGroups;

namespace EventBooking.Domain.Tests.EventGroups;

public sealed class EventGroupTests
{
    private static readonly Guid Medical = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Uniform = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid GroupA = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid GroupB = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private static Dictionary<Guid, IReadOnlyCollection<Guid>> Groups() => new()
    {
        [GroupA] = [Medical],
        [GroupB] = [Medical, Uniform],
    };

    [Fact]
    public void EveryMemberEventMustEqualTheUnionOfSelectedRequirements()
    {
        var group = EventGroup.Create(Guid.NewGuid(), "  Autumn intake  ", "  Two dates  ", Groups());
        Assert.Equal("Autumn intake", group.Title);
        Assert.False(group.IsOpen);
        var eventId = Guid.NewGuid();
        group.AddEvent(eventId, [Uniform, Medical], Groups(), true);
        Assert.False(group.Events.Single().IsOpen);
        Assert.Throws<DomainException>(() => group.AddEvent(Guid.NewGuid(), [Medical], Groups(), true));
        Assert.Throws<DomainException>(() => group.AddEvent(Guid.NewGuid(),
            [Medical, Uniform, Guid.NewGuid()], Groups(), true));
    }

    [Fact]
    public void GatesAndMembershipsAreIndependent()
    {
        var eventId = Guid.NewGuid();
        var first = EventGroup.Create(Guid.NewGuid(), "First", null, Groups());
        var second = EventGroup.Create(Guid.NewGuid(), "Second", null, Groups());
        first.AddEvent(eventId, [Medical, Uniform], Groups(), true);
        second.AddEvent(eventId, [Medical, Uniform], Groups(), true);
        first.SetOpen(true);
        first.SetEventOpen(eventId, true);
        Assert.True(first.Events.Single().IsOpen);
        Assert.False(second.Events.Single().IsOpen);
        Assert.Throws<DomainException>(() => first.AddEvent(eventId, [Medical, Uniform], Groups(), true));
    }

    [Fact]
    public void TitleAndDescriptionBoundsAreEnforced()
    {
        Assert.Throws<DomainException>(() => EventGroup.Create(Guid.NewGuid(), "  ", null, Groups()));
        Assert.Throws<DomainException>(() => EventGroup.Create(
            Guid.NewGuid(), new string('x', 161), null, Groups()));
        Assert.Throws<DomainException>(() => EventGroup.Create(
            Guid.NewGuid(), "Title", new string('x', 2001), Groups()));
        var group = EventGroup.Create(Guid.NewGuid(), "Title", null, Groups());
        Assert.Equal("", group.Description);
        Assert.Equal(1, group.Version);
    }

    [Fact]
    public void AGroupNeedsSelectedGroupsWithRequirements()
    {
        Assert.Throws<DomainException>(() => EventGroup.Create(
            Guid.NewGuid(), "Title", null, new Dictionary<Guid, IReadOnlyCollection<Guid>>()));
        Assert.Throws<DomainException>(() => EventGroup.Create(
            Guid.NewGuid(), "Title", null,
            new Dictionary<Guid, IReadOnlyCollection<Guid>> { [GroupA] = [] }));
    }

    [Fact]
    public void OnlyFutureEventsJoinAndMembershipsAreUnique()
    {
        var group = EventGroup.Create(Guid.NewGuid(), "Title", null, Groups());
        var eventId = Guid.NewGuid();
        Assert.Throws<DomainException>(() => group.AddEvent(
            eventId, [Medical, Uniform], Groups(), false));
        Assert.Throws<DomainException>(() => group.RemoveEvent(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => group.SetEventOpen(Guid.NewGuid(), true));
        group.AddEvent(eventId, [Medical, Uniform], Groups(), true);
        var version = group.Version;
        group.SetEventOpen(eventId, false);
        Assert.Equal(version, group.Version);
        group.RemoveEvent(eventId);
        Assert.Empty(group.Events);
    }

    [Fact]
    public void ReplacingGroupsWithAChangedUnionIsRefused()
    {
        var group = EventGroup.Create(Guid.NewGuid(), "Title", null, Groups());
        var eventId = Guid.NewGuid();
        group.AddEvent(eventId, [Medical, Uniform], Groups(), true);
        var version = group.Version;
        Assert.Throws<DomainException>(() => group.Edit(
            "Title", null,
            new Dictionary<Guid, IReadOnlyCollection<Guid>> { [GroupA] = [Medical] },
            new Dictionary<Guid, IReadOnlyCollection<Guid>> { [eventId] = [Medical, Uniform] }));
        Assert.Equal(version, group.Version);
        group.Edit("New title", "New description.", Groups(),
            new Dictionary<Guid, IReadOnlyCollection<Guid>> { [eventId] = [Medical, Uniform] });
        Assert.Equal("New title", group.Title);
        Assert.Equal(version + 1, group.Version);
        group.Edit("New title", "New description.", Groups(),
            new Dictionary<Guid, IReadOnlyCollection<Guid>> { [eventId] = [Medical, Uniform] });
        Assert.Equal(version + 1, group.Version);
    }
}
