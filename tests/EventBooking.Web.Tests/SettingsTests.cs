using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class SettingsTests : BunitContext
{
    [Fact]
    public void SettingsContainsOnlyFixedTypesAndInvitationTiming()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SettingsDto(
                4,
                2,
                [new AppointmentTypeDto(
                    AppointmentTypeIds.DrugAndAlcoholTesting,
                    "DAT",
                    "Drug & Alcohol Testing",
                    Guid.NewGuid())])),
        });
        Services.AddSingleton(new AdminClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));

        var cut = Render<Settings>();

        cut.WaitForAssertion(() => Assert.Contains("Drug & Alcohol Testing", cut.Find("table").TextContent));
        Assert.Contains("System settings", cut.Markup);
        Assert.DoesNotContain("Assign manager", cut.Markup);
        Assert.DoesNotContain("Import Confirmed Events", cut.Markup);
        Assert.Single(handler.Requests);
        Assert.Equal("/api/admin/settings", handler.Requests[0].RequestUri!.AbsolutePath);
    }


    /// <summary>Verifies the manager column prefers the name and always keeps the staff number.</summary>
    [Theory]
    [InlineData("Dana Datson", "U000002", "Dana Datson (U000002)")]
    [InlineData(null, "U000002", "U000002")]
    public void ManagerColumnRendersNameStates(string? displayName, string? staffId, string expected)
    {
        var cut = RenderWithManager(new AppointmentTypeDto(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            "DAT",
            "Drug & Alcohol Testing",
            Guid.NewGuid(),
            staffId,
            displayName));

        cut.WaitForAssertion(() => Assert.Contains(expected, ManagerCell(cut)));
    }

    /// <summary>Verifies an appointment type with no manager reads as unassigned.</summary>
    [Fact]
    public void ManagerColumnRendersUnassigned()
    {
        var cut = RenderWithManager(new AppointmentTypeDto(
            AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", "Drug & Alcohol Testing", null));

        cut.WaitForAssertion(() => Assert.Contains("Unassigned", ManagerCell(cut)));
    }

    /// <summary>Verifies an assigned manager with no recorded identity reads as pending sync.</summary>
    [Fact]
    public void ManagerColumnRendersPendingSync()
    {
        var cut = RenderWithManager(new AppointmentTypeDto(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            "DAT",
            "Drug & Alcohol Testing",
            Guid.NewGuid()));

        cut.WaitForAssertion(() =>
            Assert.Contains("Assigned (pending identity sync)", ManagerCell(cut)));
    }

    private static string ManagerCell(IRenderedComponent<Settings> cut) =>
        cut.Find("td[data-label='Manager identifier']").TextContent;

    private IRenderedComponent<Settings> RenderWithManager(AppointmentTypeDto type)
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SettingsDto(4, 2, [type])),
        });
        Services.AddSingleton(new AdminClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));

        return Render<Settings>();
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response);
        }
    }
}
