using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages;

public sealed class PublicEventGroupPageTests : BunitContext
{
    private static readonly Guid GroupId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid EarlyEventId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid LateEventId = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid AttendeeGroupId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public void PrivateGroupShowsAccessibleNotFoundWithContact()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(
            new FakePublicClient(groupStatus: 404, groupError: "This group is no longer open."));
        Services.AddSingleton(new AttendeePageOptions("events@example.org"));

        var cut = Render<PublicEventGroup>(p => p.Add(x => x.GroupId, GroupId));

        var alert = cut.WaitForElement("[role='alert']");
        Assert.Contains("no longer open", alert.TextContent);
        Assert.Contains("events@example.org", cut.Markup);
    }

    [Fact]
    public void NoOpenEventsShowsEmptyState()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(
            new FakePublicClient(group: Group([])));

        var cut = Render<PublicEventGroup>(p => p.Add(x => x.GroupId, GroupId));

        cut.WaitForAssertion(() => Assert.Contains("no open times", cut.Markup.ToLowerInvariant()));
        Assert.NotNull(cut.Find("[role='status']"));
    }

    [Fact]
    public void EventsAreOrderedByLocalStart()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(
            new FakePublicClient(group: Group([Late(), Early()])));

        var cut = Render<PublicEventGroup>(p => p.Add(x => x.GroupId, GroupId));

        cut.WaitForElement("a.public-group-page__event-link");
        var links = cut.FindAll("a.public-group-page__event-link");
        Assert.Equal(2, links.Count);
        Assert.Contains(EarlyEventId.ToString("D"), links[0].GetAttribute("href"));
        Assert.Contains(LateEventId.ToString("D"), links[1].GetAttribute("href"));
    }

    private static PublicEventGroupDto Group(IReadOnlyList<PublicEventDto> events) => new(
        GroupId, "Open days", "Choose a date.",
        [new PublicAttendeeGroupDto(AttendeeGroupId, "Field staff", "Field folk.")],
        events);

    private static PublicEventDto Early() => new(
        EarlyEventId, "London HQ", "1 Main St",
        new DateOnly(2026, 10, 5), new TimeOnly(9, 0), 60,
        ["Medical check"], [AttendeeGroupId]);

    private static PublicEventDto Late() => new(
        LateEventId, "London HQ", "1 Main St",
        new DateOnly(2026, 10, 6), new TimeOnly(9, 0), 60,
        ["Medical check"], [AttendeeGroupId]);

    private sealed class FakePublicClient(
        PublicEventGroupDto? group = null, int groupStatus = 200, string? groupError = null)
        : IPublicEventGroupsClient
    {
        public Task<ApiOutcome<PublicEventGroupDto>> GetGroupAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(group is not null
                ? ApiOutcome<PublicEventGroupDto>.Success(group, groupStatus)
                : ApiOutcome<PublicEventGroupDto>.Failure(new ApiProblem(
                    "not_found", "Not found", groupStatus, groupError ?? "Missing.",
                    [], null, null, null, null)));

        public Task<ApiOutcome<PublicEventDto>> GetEventAsync(Guid groupId, Guid eventId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<bool>> RequestAsync(Guid groupId, Guid eventId, string name,
            string email, Guid attendeeGroupId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<SelfRegistrationSummaryDto>> ViewConfirmationAsync(
            string token, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<ConfirmSelfRegistrationOutcomeDto>> ConfirmAsync(
            string token, IdempotencySubmission submission, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
