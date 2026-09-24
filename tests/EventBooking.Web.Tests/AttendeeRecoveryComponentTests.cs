using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies recovery start/cancel visibility, exact copy, and outcomes on the attendee page.</summary>
public class AttendeeRecoveryComponentTests : BunitContext
{
    [Fact]
    public void ArrangeButtonIsHiddenWhenNothingIsRecoverable()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("DAT", "Drug & Alcohol Testing", false)]),
            start: null,
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("ul.readiness-types li")));

        Assert.DoesNotContain(
            cut.FindAll("button"),
            button => button.TextContent.Trim() == "Arrange missed appointments");
    }

    [Fact]
    public void ArrangeStartsRecoveryAndShowsTheSingularOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var requests = new List<HttpRequestMessage>();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: request =>
            {
                requests.Add(request);
                return OutcomeJson(inviteId, [Guid.NewGuid()]);
            },
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 1 missed appointment: Medical Check-Up.",
                cut.Find("span.recovery-outcome").TextContent));
        Assert.Equal(
            $"/api/attendees/{attendeeId}/recovery-invites",
            requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        FindButton(cut, "Cancel recovery");
    }

    [Fact]
    public void OutcomeUsesThePluralForTwoRecoverableTypes()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [
                ("MED", "Medical Check-Up", true),
                ("UNI", "Uniform Fitting", true),
            ]),
            start: _ => OutcomeJson(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]),
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "Recovery started for 2 missed appointments: Medical Check-Up, Uniform Fitting.",
                cut.Find("span.recovery-outcome").TextContent));
    }

    [Fact]
    public void FailedStartShowsAnAlertWithoutAnOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    """{"title":"recovery_not_available","status":409}""",
                    Encoding.UTF8,
                    "application/problem+json"),
            },
            cancel: null);

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("span.recovery-error[role=alert]")));
        Assert.Empty(cut.FindAll("span.recovery-outcome"));
    }

    [Fact]
    public void CancelRequiresConfirmationBeforeDeleting()
    {
        var attendeeId = Guid.NewGuid();
        var inviteId = Guid.NewGuid();
        var deletes = new List<HttpRequestMessage>();
        var cut = RenderAttendees(
            attendeeId,
            Outstanding("Appointments outstanding", [("MED", "Medical Check-Up", true)]),
            start: _ => OutcomeJson(inviteId, [Guid.NewGuid()]),
            cancel: request =>
            {
                deletes.Add(request);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

        cut.Find("button.readiness-badge").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Arrange missed appointments"));
        FindButton(cut, "Arrange missed appointments").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Cancel recovery"));

        FindButton(cut, "Cancel recovery").Click();
        cut.WaitForAssertion(() => FindButton(cut, "Confirm cancel"));
        Assert.Empty(deletes);

        FindButton(cut, "Confirm cancel").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Recovery cancelled.", cut.Find("span.recovery-outcome").TextContent));
        Assert.Equal(
            $"/api/attendees/{attendeeId}/recovery-invites/{inviteId}",
            deletes[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Delete, deletes[0].Method);
    }

    private static AttendeeReadinessDto Outstanding(
        string display, (string Code, string Name, bool IsRecoverable)[] types) =>
        new(
            Guid.NewGuid(),
            "AppointmentsOutstanding",
            display,
            types
                .Select(type => new OutstandingAppointmentTypeDto(type.Code, type.Name, type.IsRecoverable))
                .ToList(),
            new Dictionary<string, ApiLink>());

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<Attendees> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    private IRenderedComponent<Attendees> RenderAttendees(
        Guid attendeeId,
        AttendeeReadinessDto readiness,
        Func<HttpRequestMessage, HttpResponseMessage>? start,
        Func<HttpRequestMessage, HttpResponseMessage>? cancel)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        var stub = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path == $"/api/attendees/{attendeeId}/recovery-invites"
                && start is not null)
            {
                return Task.FromResult(start(request));
            }

            if (request.Method == HttpMethod.Delete
                && path.StartsWith($"/api/attendees/{attendeeId}/recovery-invites/", StringComparison.Ordinal)
                && cancel is not null)
            {
                return Task.FromResult(cancel(request));
            }

            if (path.EndsWith("/readiness", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(readiness));
            }

            if (path == "/api/me")
            {
                return Task.FromResult(Json(new MeDto(["Coordinator"], null, null)));
            }

            if (path is "/api/locations")
            {
                return Task.FromResult(Json(new PageDto<LocationDto>([], null)));
            }

            if (path == "/api/attendee-groups")
            {
                return Task.FromResult(Json(new PageDto<AttendeeGroupDto>([], null)));
            }

            if (path is "/api/appointment-types")
            {
                return Task.FromResult(Json(new PageDto<AppointmentTypeDto>([], null)));
            }

            if (path == "/api/attendees")
            {
                return Task.FromResult(Json(new PageDto<AttendeeDto>(
                    [
                        new AttendeeDto(
                            attendeeId, "Amara Novak", "a.novak@mail.com", "NotYetInvited",
                            "Not yet invited", "MED", "NoActiveBooking", [], null, "cursor",
                            null, new Dictionary<string, ApiLink>()),
                    ],
                    null)));
            }

            return Task.FromResult(Json(new PageDto<AuditRowDto>([], null)));
        });
        Services.AddSingleton<IAttendeesClient>(
            new AttendeesClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton<IAuditClient>(
            new AuditClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost") }));

        return Render<Attendees>();
    }

    private static HttpResponseMessage OutcomeJson(Guid inviteId, Guid[] typeIds) =>
        Json(new { recoveryInviteId = inviteId, locationIds = new[] { Guid.NewGuid() }, recoverableTypeIds = typeIds });

    private static HttpResponseMessage Json(object? value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Encoding.UTF8,
                "application/json"),
        };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            respond(request);
    }

    /// <summary>Verifies each attendee row offers its own audit history panel.</summary>
    [Fact]
    public void AttendeesPageRendersHistoryPanelPerRow()
    {
        var cut = RenderAttendees(
            Guid.NewGuid(),
            Outstanding("Appointments outstanding", [("DAT", "Drug & Alcohol Testing", false)]),
            start: null,
            cancel: null);

        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        Assert.Single(cut.FindAll("section.audit-history"));
    }
}
