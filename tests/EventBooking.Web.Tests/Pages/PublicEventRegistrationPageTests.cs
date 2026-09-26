using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages;

public sealed class PublicEventRegistrationPageTests : BunitContext
{
    private static readonly Guid GroupId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid EventId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid AttendeeGroupId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public void InvalidFormShowsErrorWithoutCallingTheApi()
    {
        var api = new FakeRegistrationClient();
        Services.AddSingleton<IPublicEventGroupsClient>(api);

        var cut = Render<PublicEventRegistration>(p => p
            .Add(x => x.GroupId, GroupId)
            .Add(x => x.EventId, EventId));

        cut.WaitForElement("[data-action='send-request']").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[role='alert']")));
        Assert.Contains("valid email", cut.Find("[role='alert']").TextContent);
        Assert.Empty(api.Requests);
    }

    [Fact]
    public void AcceptedRequestShowsNeutralCheckEmailCopy()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(new FakeRegistrationClient());

        var cut = Render<PublicEventRegistration>(p => p
            .Add(x => x.GroupId, GroupId)
            .Add(x => x.EventId, EventId));

        cut.WaitForElement("input[name='name']").Change("Amara Novak");
        cut.Find("input[name='email']").Change("amara@example.test");
        cut.Find("select[name='attendee-group']").Change(AttendeeGroupId.ToString("D"));
        cut.Find("[data-action='send-request']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Check your email", cut.Markup));
        Assert.Contains("before it expires", cut.Markup);
    }

    [Fact]
    public void GroupFullRefusalShowsAnAccessibleError()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(
            new FakeRegistrationClient(requestError: ("group-full", "Only 0 Field staff places are bookable.")));

        var cut = Render<PublicEventRegistration>(p => p
            .Add(x => x.GroupId, GroupId)
            .Add(x => x.EventId, EventId));

        cut.WaitForElement("input[name='name']").Change("Amara Novak");
        cut.Find("input[name='email']").Change("amara@example.test");
        cut.Find("select[name='attendee-group']").Change(AttendeeGroupId.ToString("D"));
        cut.Find("[data-action='send-request']").Click();

        cut.WaitForAssertion(() => Assert.Contains("bookable", cut.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("Check your email", cut.Markup);
    }

    [Fact]
    public void AGroupWithNoSpaceIsListedButNotSelectable()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(new FakeRegistrationClient(full: true));

        var cut = Render<PublicEventRegistration>(p => p
            .Add(x => x.GroupId, GroupId)
            .Add(x => x.EventId, EventId));

        var option = cut.WaitForElement($"option[value='{AttendeeGroupId:D}']");
        Assert.True(option.HasAttribute("disabled"));
        Assert.Contains("(full)", option.TextContent);
    }

    private static PublicEventGroupDto Group(bool full = false) => new(
        GroupId, "Open days", "Choose a date.",
        [new PublicAttendeeGroupDto(AttendeeGroupId, "Field staff", "Field folk.")],
        [new PublicEventDto(EventId, "London HQ", "1 Main St",
            new DateOnly(2026, 10, 5), new TimeOnly(9, 0), 60,
            ["Medical check"], full ? [] : [AttendeeGroupId])]);

    private sealed class FakeRegistrationClient(
        (string Code, string Detail)? requestError = null, bool full = false)
        : IPublicEventGroupsClient
    {
        public List<(Guid GroupId, Guid EventId, string Name, string Email, Guid GroupChoice)> Requests { get; } = [];

        public Task<ApiOutcome<PublicEventGroupDto>> GetGroupAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PublicEventGroupDto>.Success(Group(full)));

        public Task<ApiOutcome<PublicEventDto>> GetEventAsync(Guid groupId, Guid eventId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<bool>> RequestAsync(Guid groupId, Guid eventId, string name,
            string email, Guid attendeeGroupId, CancellationToken ct)
        {
            Requests.Add((groupId, eventId, name, email, attendeeGroupId));
            if (requestError is { } error)
                return Task.FromResult(ApiOutcome<bool>.Failure(new ApiProblem(
                    error.Code, "Request refused", 409, error.Detail,
                    [], null, null, null, null)));
            return Task.FromResult(ApiOutcome<bool>.Success(true, 201));
        }

        public Task<ApiOutcome<SelfRegistrationSummaryDto>> ViewConfirmationAsync(
            string token, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<ConfirmSelfRegistrationOutcomeDto>> ConfirmAsync(
            string token, IdempotencySubmission submission, CancellationToken ct) =>
            throw new NotImplementedException();
    }
}
