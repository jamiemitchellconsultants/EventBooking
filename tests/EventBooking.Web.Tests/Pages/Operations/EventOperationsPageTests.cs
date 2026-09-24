using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Operations;

public sealed class EventOperationsPageTests : BunitContext
{
    [Fact]
    public async Task CancellationIsTwoStepAndNamesAffectedBookings()
    {
        var api = new FakeEventsClient();
        Services.AddSingleton<IEventsClient>(api);
        var cut = Render<EventOperations>();

        await cut.Find("[data-action='cancel-event']").ClickAsync(new());
        Assert.Contains("Cancel 3 active bookings?", cut.Markup);
        Assert.Empty(api.Confirms);

        await cut.Find("[data-action='confirm-cancel-event']").ClickAsync(new());
        Assert.Equal([true], api.Confirms);
    }

    [Fact]
    public void CancellationControlComesOnlyFromTheLink()
    {
        Services.AddSingleton<IEventsClient>(new FakeEventsClient(includeCancelLink: false));
        var cut = Render<EventOperations>();
        Assert.Empty(cut.FindAll("[data-action='cancel-event']"));
        Assert.Contains("Event already started", cut.Markup);
    }

    private sealed class FakeEventsClient(bool includeCancelLink = true) : IEventsClient
    {
        public List<bool> Confirms { get; } = [];
        private EventDto Event => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LON", "London HQ",
            TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active",
            [new(Guid.NewGuid(), "MED", "Medical check", 6, 3, new Dictionary<string, ApiLink>())], 3,
            includeCancelLink ? new Dictionary<string, ApiLink> { ["cancel"] = new("/cancel", "POST", "cancelEvent") } : new Dictionary<string, ApiLink>());
        public Task<ApiOutcome<PageDto<EventDto>>> ListEventsAsync(Guid? locationId, DateOnly? from, DateOnly? to, string? cursor, CancellationToken ct) => Task.FromResult(ApiOutcome<PageDto<EventDto>>.Success(new([Event], null)));
        public Task<ApiOutcome<CancelEventOutcome>> CancelAsync(Guid eventId, bool confirm, CancellationToken ct) { Confirms.Add(confirm); return Task.FromResult(ApiOutcome<CancelEventOutcome>.Success(new(3, 2, 1))); }
        public Task<ApiOutcome<NegotiationReferenceData>> GetReferenceDataAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<PageDto<EventProposalDto>>> ListProposalsAsync(string? cursor, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<ProposeEventOutcome>> ProposeAsync(ProposeEventRequest request, IdempotencySubmission submission, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<RecordAcceptanceOutcome>> RecordAcceptanceAsync(Guid proposalId, int headcount, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<object>> WithdrawAcceptanceAsync(Guid proposalId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<object>> WithdrawProposalAsync(Guid proposalId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApiOutcome<AdjustEventCapacityOutcome>> AdjustCapacityAsync(Guid eventId, Guid appointmentTypeId, int totalHeadcount, CancellationToken ct) => throw new NotSupportedException();
    }
}
