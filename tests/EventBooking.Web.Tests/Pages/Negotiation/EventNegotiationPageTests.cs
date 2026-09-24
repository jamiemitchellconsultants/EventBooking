using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Negotiation;

public sealed class EventNegotiationPageTests : BunitContext
{
    [Fact]
    public async Task DialogPreviewsEndAndFieldErrorsWithoutLosingInputs()
    {
        var api = new FakeEventsClient
        {
            ProposeResult = ApiOutcome<ProposeEventOutcome>.Failure(
                ApiProblem.Validation(new Dictionary<string, string[]>
                {
                    ["date"] = ["Choose a future date."],
                    ["headcount"] = ["Headcount must be at least one."],
                }))
        };
        Services.AddSingleton<IEventsClient>(api);

        var cut = Render<EventNegotiation>();
        await cut.Find("[data-action='propose']").ClickAsync(new());
        cut.Find("input[name='start']").Change("09:30");
        cut.Find("input[name='duration']").Change("90");
        cut.Find("input[name='headcount']").Change("0");
        Assert.Contains("ends 11:00 BST", cut.Markup);

        await cut.Find("[data-action='submit-proposal']").ClickAsync(new());
        Assert.Contains("Choose a future date.", cut.Markup);
        Assert.Contains("Headcount must be at least one.", cut.Markup);
        Assert.Equal("09:30", cut.Find("input[name='start']").GetAttribute("value"));
        Assert.Equal("90", cut.Find("input[name='duration']").GetAttribute("value"));
    }

    [Fact]
    public void BoardShowsAcceptanceCountButNeverAnotherTypesHeadcount()
    {
        var api = new FakeEventsClient();
        api.Proposals.Add(new EventProposalDto(Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Open", 3, 2, 4, true,
            false, [new("MED", "Medical check"), new("FIT", "Equipment fitting"), new("IND", "Induction")],
            Links(("withdrawAcceptance", "DELETE"))));
        Services.AddSingleton<IEventsClient>(api);

        var cut = Render<EventNegotiation>();

        Assert.Contains("2 of 3", cut.Markup);
        Assert.Contains("value=\"4\"", cut.Markup);
        Assert.DoesNotContain("FIT headcount", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IND headcount", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CapacityConflictRetainsInputAndShowsServerMinimum()
    {
        var api = new FakeEventsClient
        {
            CapacityResult = ApiOutcome<AdjustEventCapacityOutcome>.Failure(
                ApiProblem.FromSlug("capacity-below-bookings", "Capacity is too low.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["minimum"] = 5,
                        ["current"] = new { totalHeadcount = 8, remainingCapacity = 3 },
                    }))
        };
        api.Events.Add(FakeEventsClient.EventWithCapacityLinks(("adjust", "PUT")));
        Services.AddSingleton<IEventsClient>(api);
        var cut = Render<EventNegotiation>();

        cut.Find("input[name='totalHeadcount']").Change("3");
        await cut.Find("[data-action='save-capacity']").ClickAsync(new());

        Assert.Equal("3", cut.Find("input[name='totalHeadcount']").GetAttribute("value"));
        Assert.Contains("minimum is 5", cut.Markup);
        Assert.Contains("server total is 8", cut.Markup);
    }

    [Fact]
    public async Task SavingOneCapacityKeepsUnsavedEditsInOtherRows()
    {
        var api = new FakeEventsClient();
        var adjust = new Dictionary<string, ApiLink> { ["adjust"] = new("/test", "PUT", "adjust") };
        api.Events.Add(new EventDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active",
            [
                new(Guid.NewGuid(), "MED", "Medical check", 4, 4, adjust),
                new(Guid.NewGuid(), "FIT", "Equipment fitting", 6, 6, adjust),
            ],
            0, new Dictionary<string, ApiLink>()));
        Services.AddSingleton<IEventsClient>(api);
        var cut = Render<EventNegotiation>();

        cut.FindAll("input[name='totalHeadcount']")[0].Change("5");
        cut.FindAll("input[name='totalHeadcount']")[1].Change("9");
        await cut.FindAll("[data-action='save-capacity']")[0].ClickAsync(new());

        Assert.Equal("9", cut.FindAll("input[name='totalHeadcount']")[1].GetAttribute("value"));
    }

    [Fact]
    public async Task ReloadingAfterASaveKeepsEveryLoadedPage()
    {
        var api = new FakeEventsClient { PageSize = 1 };
        api.Events.Add(FakeEventsClient.EventWithCapacityLinks(("adjust", "PUT")));
        api.Events.Add(FakeEventsClient.EventWithCapacityLinks(("adjust", "PUT")));
        Services.AddSingleton<IEventsClient>(api);
        var cut = Render<EventNegotiation>();
        await cut.FindAll("button").Single(x => x.TextContent.Trim() == "Load more").ClickAsync(new());
        Assert.Equal(2, cut.FindAll("[data-action='save-capacity']").Count);

        await cut.FindAll("[data-action='save-capacity']")[0].ClickAsync(new());

        Assert.Equal(2, cut.FindAll("[data-action='save-capacity']").Count);
    }

    private static IReadOnlyDictionary<string, ApiLink> Links(params (string Rel, string Method)[] values) =>
        values.ToDictionary(x => x.Rel, x => new ApiLink("/test", x.Method, x.Rel));

    private sealed class FakeEventsClient : IEventsClient
    {
        private static readonly Guid CallerTypeId = Guid.Parse("90000000-0000-0000-0000-000000000009");
        public List<EventProposalDto> Proposals { get; } = [];
        public List<EventDto> Events { get; } = [];
        public ApiOutcome<ProposeEventOutcome> ProposeResult { get; set; } =
            ApiOutcome<ProposeEventOutcome>.Success(new(Guid.NewGuid(), "Open", null));
        public ApiOutcome<AdjustEventCapacityOutcome> CapacityResult { get; set; } =
            ApiOutcome<AdjustEventCapacityOutcome>.Success(new(Guid.NewGuid(), 4, 4, true));
        public Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct) =>
            Task.FromResult(ApiOutcome<NegotiationReferenceData>.Success(new(
                [new(Guid.NewGuid(), "London HQ", "1 Example St", "Europe/London", "BST", true)],
                [new(CallerTypeId, "MED", "Medical check", true, true)], CallerTypeId,
                new Dictionary<string, ApiLink>
                {
                    ["proposeEvent"] = new("/api/event-proposals", "POST", "proposeEvent"),
                })));
        public Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PageDto<EventProposalDto>>.Success(new(Proposals, null)));
        public Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ProposeResult);
        public Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(Guid proposalId, int headcount, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<RecordAcceptanceOutcome>.Success(new(proposalId, "Open", null, true)));
        public Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct) => Task.FromResult(ApiOutcome<object>.Success(new()));
        public Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct) => Task.FromResult(ApiOutcome<object>.Success(new()));
        public int PageSize { get; init; } = int.MaxValue;
        public Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(Guid? locationId, DateOnly? from, DateOnly? to, string? cursor, CancellationToken ct)
        {
            var start = cursor is null ? 0 : int.Parse(cursor);
            var page = Events.Skip(start).Take(PageSize).ToArray();
            var next = start + page.Length < Events.Count ? (start + page.Length).ToString() : null;
            return Task.FromResult(ApiOutcome<PageDto<EventDto>>.Success(new(page, next)));
        }
        public Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct) => Task.FromResult(CapacityResult);
        public Task<ApiOutcome<CancelEventOutcome>> CancelAsync(Guid eventId, bool confirm, CancellationToken ct) => Task.FromResult(ApiOutcome<CancelEventOutcome>.Success(new(1, 1, 0)));
        public static EventDto EventWithCapacityLinks(params (string Rel, string Method)[] links) => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active",
            [new(Guid.NewGuid(), "MED", "Medical check", 4, 4,
                links.ToDictionary(x => x.Rel, x => new ApiLink("/test", x.Method, x.Rel)))],
            0, new Dictionary<string, ApiLink>());
    }
}
