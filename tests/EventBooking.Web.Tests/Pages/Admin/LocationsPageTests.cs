using System.Net;
using System.Text;
using Bunit;
using EventBooking.Web.Pages.Admin;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Admin;

public sealed class LocationsPageTests : BunitContext
{
    [Fact]
    public void EmptyStateUsesTheDesignCopy()
    {
        Register(HttpStatusCode.OK, """{"items":[],"nextCursor":null}""");
        var cut = Render<Locations>();
        cut.WaitForAssertion(() => Assert.Contains(
            "No locations yet. Add one before Managers can propose events.", cut.Markup));
        Assert.Contains("New location", cut.Markup);
    }

    [Fact]
    public void ReadOnlyResponseHasNoMutationControls()
    {
        AdminPageFixture.RegisterReadOnly(Services,
            Response(HttpStatusCode.OK, Page("{}")));
        var cut = Render<Locations>();
        cut.WaitForAssertion(() => Assert.DoesNotContain("New location", cut.Markup));
        Assert.Empty(cut.FindAll("button[data-action='edit']"));
    }

    [Fact]
    public void VersionConflictKeepsTypedValuesAndShowsCurrentState()
    {
        // The real handler carries only the current version number under `current`
        // (Error.VersionConflict), never the row's name — the page names the version
        // the server is on and keeps every typed value for resubmission.
        var handler = new QueueHandler(
            Response(HttpStatusCode.OK, Page("{\"update\":{\"href\":\"/api/locations/10000000-0000-0000-0000-000000000001\",\"method\":\"PUT\",\"operationId\":\"updateLocation\"}}")),
            Response(HttpStatusCode.Conflict, """{"type":"version-conflict","title":"Changed","status":409,"detail":"Another Admin changed this location.","current":{"currentVersion":2}}"""));
        Services.AddSingleton(new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") }));
        AdminPageFixture.RegisterContext(Services, "createLocation");
        var cut = Render<Locations>();
        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("input[name='name']").Change("My unsaved name");
        cut.Find("button[data-action='save']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Another Admin changed this location.", cut.Markup));
        Assert.Equal("My unsaved name", cut.Find("input[name='name']").GetAttribute("value"));
        Assert.Contains("version 2", cut.Markup);
    }

    [Fact]
    public void InUseZoneChangeIsDisabledWithBlockingCounts()
    {
        var handler = new QueueHandler(
            Response(HttpStatusCode.OK, Page("{\"update\":{\"href\":\"/api/locations/10000000-0000-0000-0000-000000000001\",\"method\":\"PUT\",\"operationId\":\"updateLocation\"}}")),
            Response(HttpStatusCode.Conflict, """{"type":"in-use","title":"In use","status":409,"detail":"The zone is in use.","blocking":{"openProposals":2,"futureEvents":3}}"""));
        Services.AddSingleton(new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") }));
        AdminPageFixture.RegisterContext(Services, "createLocation");
        var cut = Render<Locations>();
        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("select[name='timeZoneId']").Change("Asia/Tokyo");
        cut.Find("button[data-action='save']").Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find("select[name='timeZoneId']").HasAttribute("disabled")));
        cut.WaitForAssertion(() => Assert.Contains("2 open proposals and 3 future events", cut.Markup));
    }

    [Fact]
    public void InUseZoneChangeRestoresTheStoredZone()
    {
        var cut = RenderWithInUseOnSave();
        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("select[name='timeZoneId']").Change("Asia/Tokyo");
        cut.Find("button[data-action='save']").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "Europe/London", cut.Find("select[name='timeZoneId']").GetAttribute("value")));
    }

    [Fact]
    public void InUseDeactivationKeepsZoneEditableAndRestoresActive()
    {
        var cut = RenderWithInUseOnSave();
        cut.WaitForElement("button[data-action='edit']").Click();
        cut.Find("input[type='checkbox']").Change(false);
        cut.Find("button[data-action='save']").Click();

        cut.WaitForAssertion(() => Assert.Contains("cannot be deactivated", cut.Markup));
        Assert.False(cut.Find("select[name='timeZoneId']").HasAttribute("disabled"));
        Assert.True(cut.Find("input[type='checkbox']").HasAttribute("checked"));
        Assert.DoesNotContain("zone cannot change", cut.Markup);
    }

    [Fact]
    public void EditingAnotherLocationClearsTheZoneBlock()
    {
        var update = "{\"update\":{\"href\":\"/api/locations/x\",\"method\":\"PUT\",\"operationId\":\"updateLocation\"}}";
        var twoRows = $$"""{"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"LONDON_HQ","name":"London HQ","address":"1 Example St","timeZoneId":"Europe/London","isActive":true,"version":1,"_links":{{update}}},{"id":"20000000-0000-0000-0000-000000000002","code":"DUBLIN","name":"Dublin","address":"2 Sample Rd","timeZoneId":"Europe/Dublin","isActive":true,"version":1,"_links":{{update}}}],"nextCursor":null}""";
        var handler = new QueueHandler(
            Response(HttpStatusCode.OK, twoRows),
            Response(HttpStatusCode.Conflict, """{"type":"in-use","title":"In use","status":409,"detail":"The zone is in use.","blocking":{"openProposals":2,"futureEvents":3}}"""));
        Services.AddSingleton(new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") }));
        AdminPageFixture.RegisterContext(Services, "createLocation");
        var cut = Render<Locations>();
        cut.WaitForElements("button[data-action='edit']")[0].Click();
        cut.Find("select[name='timeZoneId']").Change("Asia/Tokyo");
        cut.Find("button[data-action='save']").Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find("select[name='timeZoneId']").HasAttribute("disabled")));

        cut.FindAll("button[data-action='edit']")[1].Click();

        Assert.False(cut.Find("select[name='timeZoneId']").HasAttribute("disabled"));
        Assert.Equal("Europe/Dublin", cut.Find("select[name='timeZoneId']").GetAttribute("value"));
    }

    [Fact]
    public void AStoredZoneOutsideTheDefaultsIsOfferedAndSelected()
    {
        var update = "{\"update\":{\"href\":\"/api/locations/x\",\"method\":\"PUT\",\"operationId\":\"updateLocation\"}}";
        var row = $$"""{"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"NYC","name":"New York","address":"1 Broadway","timeZoneId":"America/New_York","isActive":true,"version":1,"_links":{{update}}}],"nextCursor":null}""";
        Register(HttpStatusCode.OK, row);
        var cut = Render<Locations>();
        cut.WaitForElement("button[data-action='edit']").Click();

        Assert.Single(cut.FindAll("select[name='timeZoneId'] option[value='America/New_York']"));
        Assert.Equal("America/New_York", cut.Find("select[name='timeZoneId']").GetAttribute("value"));
    }

    private IRenderedComponent<Locations> RenderWithInUseOnSave()
    {
        var handler = new QueueHandler(
            Response(HttpStatusCode.OK, Page("{\"update\":{\"href\":\"/api/locations/10000000-0000-0000-0000-000000000001\",\"method\":\"PUT\",\"operationId\":\"updateLocation\"}}")),
            Response(HttpStatusCode.Conflict, """{"type":"in-use","title":"In use","status":409,"detail":"This is still in use.","blocking":{"openProposals":2,"futureEvents":3}}"""));
        Services.AddSingleton(new AdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example") }));
        AdminPageFixture.RegisterContext(Services, "createLocation");
        return Render<Locations>();
    }

    private void Register(HttpStatusCode status, string body) =>
        AdminPageFixture.Register(Services, Response(status, body));
    private static string Page(string links) => $$"""{"items":[{"id":"10000000-0000-0000-0000-000000000001","code":"LONDON_HQ","name":"London HQ","address":"1 Example St","timeZoneId":"Europe/London","isActive":true,"version":1,"_links":{{links}}}],"nextCursor":null}""";
    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, status == HttpStatusCode.OK ? "application/json" : "application/problem+json") };
    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(_responses.Dequeue());
    }
}
