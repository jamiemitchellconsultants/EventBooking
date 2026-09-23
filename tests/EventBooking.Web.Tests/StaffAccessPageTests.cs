using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

public class StaffAccessPageTests : BunitContext
{
    [Fact]
    public void RolesRenderAsReadOnlyTextWithNoCheckboxes()
    {
        var handler = new QueueHandler();
        var typeId = Guid.NewGuid();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(
                Guid.NewGuid(), ["Coordinator", "Manager"], typeId, "Medical Check-up", 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(
            4, 2, [new AppointmentTypeDto(typeId, "MED", "Medical Check-up", null)])));
        Register(handler);

        var cut = Render<StaffAccess>();

        cut.WaitForAssertion(() => Assert.Contains("Coordinator", cut.Markup));
        Assert.Empty(cut.FindAll("input[type='checkbox']"));
        Assert.Empty(cut.FindAll("button.add-profile"));
    }

    [Fact]
    public void APendingScopeRoleShowsTheAwaitingAssignmentBadgeAndAnAssignAction()
    {
        var handler = new QueueHandler();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(Guid.NewGuid(), ["Manager"], null, null, 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(4, 2, [])));
        Register(handler);

        var cut = Render<StaffAccess>();

        cut.WaitForAssertion(() => Assert.Contains(
            "Awaiting appointment-type assignment", cut.Markup));
        Assert.Equal("Assign", cut.Find("button.edit-profile").TextContent.Trim());
    }

    [Fact]
    public void ARowWithNoScopedRoleHasNoAction()
    {
        var handler = new QueueHandler();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(Guid.NewGuid(), ["Coordinator"], null, null, 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(4, 2, [])));
        Register(handler);

        var cut = Render<StaffAccess>();

        cut.WaitForAssertion(() => Assert.Contains("Coordinator", cut.Markup));
        Assert.Empty(cut.FindAll("button.edit-profile"));
    }

    [Fact]
    public void SavingAnAppointmentTypeSendsScopeOnlyAndClosesTheEditor()
    {
        var handler = new QueueHandler();
        var typeId = Guid.NewGuid();
        var target = Guid.NewGuid();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(target, ["Manager"], null, null, 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(
            4, 2, [new AppointmentTypeDto(typeId, "MED", "Medical Check-up", null)])));
        handler.Responses.Enqueue(Json(new StaffAccessMutationDto(
            new StaffAccessProfileDto(target, ["Manager"], typeId, "Medical Check-up", 2), null)));
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(target, ["Manager"], typeId, "Medical Check-up", 2),
        }));
        Register(handler);

        var cut = Render<StaffAccess>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.edit-profile")));
        cut.Find("button.edit-profile").Click();
        cut.Find("select#appointment-type").Change(typeId.ToString());
        cut.Find("button.save-profile").Click();

        cut.WaitForAssertion(() => Assert.Contains("Appointment type saved", cut.Markup));
        Assert.DoesNotContain("roles", handler.Bodies.Last());
    }

    [Fact]
    public void ClearScopeIsOnlyOfferedWhenScopeIsSet()
    {
        var handler = new QueueHandler();
        var typeId = Guid.NewGuid();
        var scoped = Guid.NewGuid();
        var pending = Guid.NewGuid();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(scoped, ["Manager"], typeId, "Medical Check-up", 1),
            new StaffAccessProfileDto(pending, ["AppointmentStaff"], null, null, 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(
            4, 2, [new AppointmentTypeDto(typeId, "MED", "Medical Check-up", null)])));
        Register(handler);

        var cut = Render<StaffAccess>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("button.edit-profile").Count));

        cut.FindAll("button.edit-profile")[0].Click();
        Assert.Single(cut.FindAll("button.clear-scope"));
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").Click();

        cut.FindAll("button.edit-profile")[1].Click();
        Assert.Empty(cut.FindAll("button.clear-scope"));
    }

    [Fact]
    public void AConflictReloadsAndKeepsTheEditorReachable()
    {
        var handler = new QueueHandler();
        var typeId = Guid.NewGuid();
        var target = Guid.NewGuid();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(target, ["Manager"], null, null, 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(
            4, 2, [new AppointmentTypeDto(typeId, "MED", "Medical Check-up", null)])));
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new
            {
                title = "conflict",
                detail = "The staff profile was changed by another administrator.",
                status = 409,
            }),
        });
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(target, ["Manager"], null, null, 2),
        }));
        Register(handler);

        var cut = Render<StaffAccess>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.edit-profile")));
        cut.Find("button.edit-profile").Click();
        cut.Find("select#appointment-type").Change(typeId.ToString());
        cut.Find("button.save-profile").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            "changed by another administrator", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public void ProfileRowsPreferStaffNumberAndFallbackToProviderIdentifier()
    {
        var handler = new QueueHandler();
        var fallback = Guid.NewGuid();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(
                Guid.NewGuid(), ["Coordinator"], null, null, 1, StaffId: "U123456"),
            new StaffAccessProfileDto(fallback, ["Coordinator"], null, null, 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(4, 2, [])));
        Register(handler);

        var cut = Render<StaffAccess>();

        cut.WaitForAssertion(() => Assert.Contains("U123456", cut.Markup));
        Assert.Contains(fallback.ToString(), cut.Markup);
    }


    /// <summary>Verifies an observed name is shown alongside the staff number an admin acts on.</summary>
    [Fact]
    public void StaffAccessTableRendersNameWhenKnown()
    {
        var handler = new QueueHandler();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(
                Guid.NewGuid(), ["Coordinator"], null, null, 1, "U000002", "Dana Datson"),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(4, 2, [])));
        Register(handler);

        var cut = Render<StaffAccess>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Dana Datson (U000002)", StaffNumberCell(cut)));
    }

    /// <summary>Verifies an identity without a name falls back to the staff number alone.</summary>
    [Fact]
    public void StaffAccessTableFallsBackToStaffNumber()
    {
        var handler = new QueueHandler();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(Guid.NewGuid(), ["Coordinator"], null, null, 1, "U000003"),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(4, 2, [])));
        Register(handler);

        var cut = Render<StaffAccess>();

        cut.WaitForAssertion(() => Assert.Equal("U000003", StaffNumberCell(cut).Trim()));
    }

    /// <summary>Verifies a profile with no recorded identity still shows its provider key.</summary>
    [Fact]
    public void StaffAccessTableFallsBackToTheProviderKey()
    {
        var staffUserId = Guid.NewGuid();
        var handler = new QueueHandler();
        handler.Responses.Enqueue(Json(new[]
        {
            new StaffAccessProfileDto(staffUserId, ["Coordinator"], null, null, 1),
        }));
        handler.Responses.Enqueue(Json(new SettingsDto(4, 2, [])));
        Register(handler);

        var cut = Render<StaffAccess>();

        cut.WaitForAssertion(() =>
            Assert.Equal(staffUserId.ToString(), StaffNumberCell(cut).Trim()));
    }

    private static string StaffNumberCell(IRenderedComponent<StaffAccess> cut) =>
        cut.Find("td[data-label='Staff number']").TextContent;

    private void Register(QueueHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new StaffAccessClient(http));
        Services.AddSingleton(new AdminClient(http));
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(value),
    };

    private sealed class QueueHandler : HttpMessageHandler
    {
        public Queue<HttpResponseMessage> Responses { get; } = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responses.Dequeue();
        }
    }
}
