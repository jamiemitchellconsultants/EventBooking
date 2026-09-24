using EventBooking.Domain.Access;

namespace EventBooking.Api.Tests.Catalogue;

/// <summary>
/// Design 05: a representation carries links to the actions the caller may currently take.
/// Two callers of different capability must therefore get different link sets from one row,
/// which is the assertion that makes _links load-bearing rather than decorative.
/// </summary>
[Collection("api")]
public sealed class LinkTests(ApiFactory factory) : CatalogueSuite(factory)
{
    [Fact]
    public async Task AnAdminSeesTheUpdateLinkOnALocationAndACoordinatorDoesNot()
    {
        // The factory's identity is request-time state, not per-client: flipping SignedInAs
        // between the two fetches is what makes them two callers.
        Factory.SignedInAs = await Factory.GivenStaffAsync([Role.Admin], null);
        Factory.StaffIdClaim = "U700401";
        var client = Factory.CreateClient();
        var created = await BodyAsync(await PostAsync(client, "/api/locations", new
        {
            code = "LNK_LON", name = "London", address = "1 Test Street",
            timeZoneId = "Europe/London",
        }));
        var id = created.GetProperty("id").GetGuid();

        var asAdmin = await LinksOfAsync(client, id);

        Factory.SignedInAs = await Factory.GivenStaffAsync([Role.Coordinator], null);
        Factory.StaffIdClaim = "U700402";
        var asCoordinator = await LinksOfAsync(client, id);

        Assert.Contains("update", asAdmin);
        Assert.Contains("self", asAdmin);
        Assert.DoesNotContain("update", asCoordinator);
        Assert.Contains("self", asCoordinator);
    }

    [Fact]
    public async Task TheApiIndexListsEveryTopLevelResource()
    {
        Factory.SignedInAs = null;

        var body = await BodyAsync(await Factory.CreateClient().GetAsync("/api"));
        var links = body.GetProperty("_links").EnumerateObject().Select(x => x.Name).ToList();

        foreach (var relation in new[]
        {
            "self", "openapi", "swagger", "health", "me", "locations", "appointmentTypes",
            "attendeeGroups", "settings", "staffAccess", "eventProposals", "events",
            "attendees", "dashboards", "audit", "appointmentWorkspace",
        })
        {
            Assert.Contains(relation, links);
        }
    }

    /// <summary>Every link's href must be a route the catalogue knows.</summary>
    [Fact]
    public async Task EveryIndexLinkNamesACataloguedOperation()
    {
        Factory.SignedInAs = null;

        var body = await BodyAsync(await Factory.CreateClient().GetAsync("/api"));

        foreach (var link in body.GetProperty("_links").EnumerateObject())
        {
            var operationId = link.Value.GetProperty("operationId").GetString()!;
            Assert.True(
                OpenApi.AgentOperationCatalog.All.ContainsKey(operationId), operationId);
        }
    }

    private static async Task<IReadOnlyList<string>> LinksOfAsync(HttpClient client, Guid id)
    {
        var body = await BodyAsync(await client.GetAsync("/api/locations?includeInactive=true"));
        var row = body.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);
        return [.. row.GetProperty("_links").EnumerateObject().Select(x => x.Name)];
    }
}
