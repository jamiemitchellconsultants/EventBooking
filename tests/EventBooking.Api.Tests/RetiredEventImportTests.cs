using System.Text.Json;
using EventBooking.Application.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class RetiredEventImportTests(ApiFactory factory)
{
    [Fact]
    public async Task Event_import_is_absent_but_attendee_csv_import_remains()
    {
        using var document = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("/api/events/import", paths);
        Assert.Contains("/api/attendees/import", paths);
    }

    [Fact]
    public void No_retired_capability_action_or_proposalless_factory_remains()
    {
        Assert.DoesNotContain("ImportEvents", Enum.GetNames<StaffCapability>());
        Assert.DoesNotContain("EventImported", Enum.GetNames<AuditAction>());
        Assert.DoesNotContain("StaffAccessRemoved", Enum.GetNames<AuditAction>());
        Assert.DoesNotContain(typeof(Event).GetMethods(), method => method.Name == "CreateImported");
    }
}
