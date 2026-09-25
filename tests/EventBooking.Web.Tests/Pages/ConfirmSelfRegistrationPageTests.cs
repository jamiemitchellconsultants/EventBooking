using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages;

public sealed class ConfirmSelfRegistrationPageTests : BunitContext
{
    private const string Token = "registration-token";

    [Fact]
    public void ExpiredTokenShowsAnAccessibleErrorWithContact()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(
            new FakeConfirmClient(viewStatus: 410, viewError: "This confirmation link has expired."));
        Services.AddSingleton(new AttendeePageOptions("events@example.org"));

        var cut = Render<ConfirmSelfRegistration>(p => p.Add(x => x.Token, Token));

        var alert = cut.WaitForElement("[role='alert']");
        Assert.Contains("expired", alert.TextContent);
        Assert.Contains("events@example.org", cut.Markup);
        Assert.Empty(cut.FindAll("[data-action='confirm-registration']"));
    }

    [Fact]
    public void SuccessShowsTheManageLinkNote()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(new FakeConfirmClient());

        var cut = Render<ConfirmSelfRegistration>(p => p.Add(x => x.Token, Token));

        cut.WaitForElement("[data-action='confirm-registration']").Click();

        cut.WaitForAssertion(() => Assert.Contains("booking-confirmation email", cut.Markup));
        Assert.Contains("manage link", cut.Markup);
    }

    [Fact]
    public void SummaryNeverRendersTheRawToken()
    {
        Services.AddSingleton<IPublicEventGroupsClient>(new FakeConfirmClient());

        var cut = Render<ConfirmSelfRegistration>(p => p.Add(x => x.Token, Token));

        cut.WaitForElement("[data-action='confirm-registration']");
        Assert.DoesNotContain(Token, cut.Markup);
    }

    private static SelfRegistrationSummaryDto Summary() => new(
        Guid.Parse("60000000-0000-0000-0000-000000000001"),
        Guid.Parse("50000000-0000-0000-0000-000000000001"),
        "Open days", "London HQ",
        new DateOnly(2026, 10, 5), new TimeOnly(9, 0), "Field staff");

    private sealed class FakeConfirmClient(int viewStatus = 200, string? viewError = null)
        : IPublicEventGroupsClient
    {
        public List<string> ConfirmKeys { get; } = [];

        public Task<ApiOutcome<PublicEventGroupDto>> GetGroupAsync(Guid id, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<PublicEventDto>> GetEventAsync(Guid groupId, Guid eventId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<bool>> RequestAsync(Guid groupId, Guid eventId, string name,
            string email, Guid attendeeGroupId, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<ApiOutcome<SelfRegistrationSummaryDto>> ViewConfirmationAsync(
            string token, CancellationToken ct) =>
            Task.FromResult(viewStatus == 200
                ? ApiOutcome<SelfRegistrationSummaryDto>.Success(Summary())
                : ApiOutcome<SelfRegistrationSummaryDto>.Failure(new ApiProblem(
                    "token-expired", "Link expired", viewStatus, viewError ?? "Expired.",
                    [], null, null, null, null)));

        public Task<ApiOutcome<ConfirmSelfRegistrationOutcomeDto>> ConfirmAsync(
            string token, IdempotencySubmission submission, CancellationToken ct)
        {
            ConfirmKeys.Add(submission.Key);
            return Task.FromResult(ApiOutcome<ConfirmSelfRegistrationOutcomeDto>.Success(
                new ConfirmSelfRegistrationOutcomeDto(Guid.NewGuid()), 201));
        }
    }
}
