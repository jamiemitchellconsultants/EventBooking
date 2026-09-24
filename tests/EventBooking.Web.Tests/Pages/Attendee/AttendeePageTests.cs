using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Attendee;

public sealed class AttendeePageTests : BunitContext
{
    [Fact]
    public async Task CapacityRaceRemovesOnlyFilledOptionAndKeepsTheRest()
    {
        var api = new FakeBookingClient { ConfirmProblem = ApiProblem.FromSlug("capacity-exhausted", "Full.") };
        Services.AddSingleton<IBookingClient>(api);
        var cut = Render<Book>(p => p.Add(x => x.Token, "book-token"));
        cut.WaitForElement("input[value='10000000-0000-0000-0000-000000000001']").Change(true);
        await cut.Find("[data-action='confirm-booking']").ClickAsync(new());

        Assert.Contains("That time has just filled up. Please choose another.", cut.Markup);
        Assert.DoesNotContain("London HQ", cut.Markup);
        Assert.Contains("Dublin Centre", cut.Markup);
    }

    [Theory]
    [InlineData("reinvited", "A new invitation is ready")]
    [InlineData("reinvitePending", "A replacement invitation is already being arranged")]
    [InlineData("noEligibleEvents", "We'll be in touch when another time is available")]
    [InlineData("cancelled", "Your booking is cancelled")]
    public async Task ManageRendersEachTruthfulOutcome(string outcome, string expected)
    {
        Services.AddSingleton<IBookingClient>(new FakeBookingClient { Outcome = outcome });
        var cut = Render<ManageBooking>(p => p.Add(x => x.Token, "manage-token"));
        await cut.WaitForElement("[data-action='cancel-booking']").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Markup, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StartedBookingShowsContactAndNoCancelControl()
    {
        Services.AddSingleton<IBookingClient>(new FakeBookingClient(includeCancelLink: false));
        Services.AddSingleton(new ProductOptions("EventBooking", null, "events@example.org"));
        var cut = Render<ManageBooking>(p => p.Add(x => x.Token, "manage-token"));
        cut.WaitForAssertion(() => Assert.Contains("events@example.org", cut.Markup));
        Assert.Contains("already started", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindAll("[data-action='cancel-booking']"));
    }

    private sealed class FakeBookingClient(bool includeCancelLink = true) : IBookingClient
    {
        private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
        private static readonly Guid Dublin = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public ApiProblem? ConfirmProblem { get; init; }
        public string Outcome { get; init; } = "cancelled";
        public Task<ApiOutcome<InviteDto>> ViewInviteAsync(string token, CancellationToken ct) => Task.FromResult(ApiOutcome<InviteDto>.Success(new(Guid.NewGuid(), "Ravi", ["Medical check"], [
            new(London, "London HQ", "1 Example St", TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST")),
            new(Dublin, "Dublin Centre", "2 Sample Rd", TestContractFactory.EventTime("Wed 15 Oct 2026, 13:00–17:00 IST"))], false,
            new Dictionary<string, ApiLink>
            {
                ["confirm"] = new("/api/booking/book-token/confirm", "POST", "confirmBooking"),
            })));
        public Task<ApiOutcome<ConfirmBookingOutcomeDto>> ConfirmAsync(string token, Guid eventId, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ConfirmProblem is null ? ApiOutcome<ConfirmBookingOutcomeDto>.Success(new(Guid.NewGuid(), "manage-token")) : ApiOutcome<ConfirmBookingOutcomeDto>.Failure(ConfirmProblem));
        public Task<ApiOutcome<ManagedBookingDto>> ViewManagedAsync(string token, CancellationToken ct) => Task.FromResult(ApiOutcome<ManagedBookingDto>.Success(new("Ravi", "London HQ", "1 Example St", TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), ["Medical check"], includeCancelLink ? new Dictionary<string, ApiLink> { ["cancel"] = new("/cancel", "POST", "cancelManagedBooking") } : new Dictionary<string, ApiLink>())));
        public Task<ApiOutcome<CancelBookingOutcomeDto>> CancelAsync(string token, bool requestNewTime, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ApiOutcome<CancelBookingOutcomeDto>.Success(new(Outcome, Outcome == "reinvited" ? Guid.NewGuid() : null)));
    }
}
