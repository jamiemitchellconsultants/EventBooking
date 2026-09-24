using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Appointments;

public sealed class AppointmentsPageTests : BunitContext
{
    [Fact]
    public void HeadingUsesServerAssignedTypeAndSelectorGroupsByLocation()
    {
        Services.AddSingleton<IAppointmentsClient>(new FakeAppointmentsClient());
        var cut = Render<EventBooking.Web.Pages.Appointments>();
        Assert.Contains("Appointments — Medical check", cut.Markup);
        Assert.Equal(2, cut.FindAll("select[name='event'] optgroup").Count);
        Assert.Empty(cut.FindAll("select[name='appointmentType']"));
    }

    [Fact]
    public void StatusActionsComeOnlyFromRowLinks()
    {
        Services.AddSingleton<IAppointmentsClient>(new FakeAppointmentsClient());
        var cut = Render<EventBooking.Web.Pages.Appointments>();
        Assert.Single(cut.FindAll("[data-action='check-in']"));
        Assert.Empty(cut.FindAll("[data-action='no-show']"));
        Assert.Contains("No-show is available after the event ends", cut.Markup);
    }

    [Fact]
    public async Task ConflictReloadsAndAnnouncesOnlyTheAffectedRow()
    {
        var api = new FakeAppointmentsClient { ConflictOnStatus = true };
        Services.AddSingleton<IAppointmentsClient>(api);
        var cut = Render<EventBooking.Web.Pages.Appointments>();
        await cut.Find("[data-action='check-in']").ClickAsync(new());
        Assert.Contains("R. Singh changed elsewhere; the row has been refreshed.", cut.Markup);
        Assert.Equal(2, api.RosterReads);
    }

    private sealed class FakeAppointmentsClient : IAppointmentsClient
    {
        private readonly Guid _appointmentId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        public bool ConflictOnStatus { get; init; }
        public int RosterReads { get; private set; }
        public Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct) => Task.FromResult(ApiOutcome<WorkspaceContextDto>.Success(new("MED", "Medical check")));
        public Task<ApiOutcome<PageDto<WorkspaceEventDto>>> ListEventsAsync(Guid? locationId, CancellationToken ct) => Task.FromResult(ApiOutcome<PageDto<WorkspaceEventDto>>.Success(new([
            new(_eventId, Guid.NewGuid(), "London HQ", TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active", new Dictionary<string,ApiLink>()),
            new(Guid.NewGuid(), Guid.NewGuid(), "Dublin Centre", TestContractFactory.EventTime("Wed 15 Oct 2026, 13:00–17:00 IST"), "Active", new Dictionary<string,ApiLink>())], null)));
        public Task<ApiOutcome<PageDto<WorkspaceRosterRowDto>>> GetRosterAsync(Guid eventId, CancellationToken ct) { RosterReads++; return Task.FromResult(ApiOutcome<PageDto<WorkspaceRosterRowDto>>.Success(new([
            new(_appointmentId, "R. Singh", "r@example.org", "MED", "Expected", null, RosterReads,
                new Dictionary<string,ApiLink> { ["checkIn"] = new("/status", "PUT", "setAppointmentStatus") })], null))); }
        public Task<ApiOutcome<object>> SetStatusAsync(Guid appointmentId, string targetStatus, long expectedVersion, CancellationToken ct) => Task.FromResult(ConflictOnStatus ? ApiOutcome<object>.Failure(ApiProblem.FromSlug("version-conflict", "Changed elsewhere.")) : ApiOutcome<object>.Success(new()));
        public Uri RosterCsvUri(Guid eventId) => new($"https://api.test/api/appointment-workspace/events/{eventId}/roster.csv");
    }
}
