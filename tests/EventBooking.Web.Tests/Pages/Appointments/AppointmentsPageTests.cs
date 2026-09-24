using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

    [Fact]
    public async Task RosterDownloadIsFetchedWithTheStaffClientAndSavedLocally()
    {
        // A plain link would navigate without the bearer token and receive a 401.
        var api = new FakeAppointmentsClient();
        Services.AddSingleton<IAppointmentsClient>(api);
        var save = JSInterop.SetupVoid("saveTextFile", _ => true);
        var cut = Render<EventBooking.Web.Pages.Appointments>();

        Assert.Empty(cut.FindAll("a[href*='roster.csv']"));
        await cut.Find("[data-action='download-roster']").ClickAsync(new());

        var call = Assert.Single(save.Invocations);
        Assert.Equal("roster-1.csv", call.Arguments[0]);
        Assert.Equal("name,email\n", call.Arguments[1]);
    }

    [Fact]
    public async Task RosterDownloadFailureShowsTheProblemInsteadOfSaving()
    {
        var api = new FakeAppointmentsClient { DownloadFails = true };
        Services.AddSingleton<IAppointmentsClient>(api);
        var save = JSInterop.SetupVoid("saveTextFile", _ => true);
        var cut = Render<EventBooking.Web.Pages.Appointments>();

        await cut.Find("[data-action='download-roster']").ClickAsync(new());

        Assert.Empty(save.Invocations);
        Assert.Contains("Not allowed.", cut.Markup);
    }

    [Theory]
    [InlineData("attachment; filename=\"roster-abc.csv\"", "roster-abc.csv")]
    [InlineData(null, "roster-10000000-0000-0000-0000-000000000001.csv")]
    public async Task ClientReadsTheRosterCsvAndItsServerFileName(string? disposition, string expected)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("name,email\n", Encoding.UTF8, "text/csv"),
        };
        if (disposition is not null)
            response.Content.Headers.ContentDisposition = ContentDispositionHeaderValue.Parse(disposition);
        var handler = new RecordingHandler(response);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example") };
        var client = new AppointmentsClient(http);

        var file = await client.DownloadRosterCsvAsync(
            Guid.Parse("10000000-0000-0000-0000-000000000001"), CancellationToken.None);

        Assert.True(file.IsSuccess);
        Assert.Equal(expected, file.Value!.FileName);
        Assert.Equal("name,email\n", file.Value.Content);
        Assert.Equal("/api/appointment-workspace/events/10000000-0000-0000-0000-000000000001/roster.csv", handler.Path);
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath;
            return Task.FromResult(response);
        }
    }

    private sealed class FakeAppointmentsClient : IAppointmentsClient
    {
        private readonly Guid _appointmentId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        public bool ConflictOnStatus { get; init; }
        public bool DownloadFails { get; init; }
        public int RosterReads { get; private set; }
        public Task<ApiOutcome<WorkspaceContextDto>> GetContextAsync(CancellationToken ct) => Task.FromResult(ApiOutcome<WorkspaceContextDto>.Success(new("MED", "Medical check")));
        public Task<ApiOutcome<PageDto<WorkspaceEventDto>>> ListEventsAsync(Guid? locationId, CancellationToken ct) => Task.FromResult(ApiOutcome<PageDto<WorkspaceEventDto>>.Success(new([
            new(_eventId, Guid.NewGuid(), "London HQ", TestContractFactory.EventTime("Tue 14 Oct 2026, 09:30–11:00 BST"), "Active", new Dictionary<string,ApiLink>()),
            new(Guid.NewGuid(), Guid.NewGuid(), "Dublin Centre", TestContractFactory.EventTime("Wed 15 Oct 2026, 13:00–17:00 IST"), "Active", new Dictionary<string,ApiLink>())], null)));
        public Task<ApiOutcome<PageDto<WorkspaceRosterRowDto>>> GetRosterAsync(Guid eventId, CancellationToken ct) { RosterReads++; return Task.FromResult(ApiOutcome<PageDto<WorkspaceRosterRowDto>>.Success(new([
            new(_appointmentId, "R. Singh", "r@example.org", "MED", "Expected", null, RosterReads,
                new Dictionary<string,ApiLink> { ["checkIn"] = new("/status", "PUT", "setAppointmentStatus") })], null))); }
        public Task<ApiOutcome<object>> SetStatusAsync(Guid appointmentId, string targetStatus, long expectedVersion, CancellationToken ct) => Task.FromResult(ConflictOnStatus ? ApiOutcome<object>.Failure(ApiProblem.FromSlug("version-conflict", "Changed elsewhere.")) : ApiOutcome<object>.Success(new()));
        public Task<ApiOutcome<RosterCsvFile>> DownloadRosterCsvAsync(Guid eventId, CancellationToken ct) => Task.FromResult(DownloadFails
            ? ApiOutcome<RosterCsvFile>.Failure(ApiProblem.FromSlug("forbidden", "Not allowed."))
            : ApiOutcome<RosterCsvFile>.Success(new("roster-1.csv", "name,email\n")));
    }
}
