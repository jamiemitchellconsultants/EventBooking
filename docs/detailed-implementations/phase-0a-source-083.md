# 00a — Port source 83 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs","encoding":"utf8","sha256":"6ff1802554cc3c452043e0efcf855badd809fa226391692eb1fa7fcf04845895","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class SlotsClientHeadcountRevisionTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(ResponseFactory(request));
        }
    }

    private static (SlotsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        };
        return (new SlotsClient(http), handler);
    }

    [Fact]
    public async Task TheBoardReadsMyCurrentAcceptedHeadcount()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotBoardDto(
                [
                    new OpenProposalDto(
                        proposalId,
                        new DateOnly(2026, 9, 10),
                        new TimeOnly(9, 0),
                        new TimeOnly(13, 0),
                        ["Drug & Alcohol Testing"],
                        12,
                        true,
                        false),
                ],
                [])),
        };

        var outcome = await client.GetBoardAsync(CancellationToken.None);

        var proposal = Assert.Single(outcome.Value!.OpenProposals);
        Assert.True(proposal.AcceptedByMe);
        Assert.Equal(12, proposal.MyAcceptedHeadcount);
    }

    [Fact]
    public async Task AcceptAndUpdatePostToTheSameAcceptanceResource()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.AcceptAsync(proposalId, 10, CancellationToken.None);
        await client.AcceptAsync(proposalId, 12, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                $"/api/slots/proposals/{proposalId}/acceptance",
                request.RequestUri!.AbsolutePath);
        });
        Assert.Contains("10", await handler.Requests[0].Content!.ReadAsStringAsync());
        Assert.Contains("12", await handler.Requests[1].Content!.ReadAsStringAsync());
    }
}
`````

## tests/EventBooking.Web.Tests/SlotsClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/SlotsClientTests.cs","encoding":"utf8","sha256":"3702763afb81a6b3257ef6291e57e3c32647086876bebc514e49a8a3c7987beb","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class SlotsClientTests
{
    /// <summary>Records what was asked for and answers with whatever the test set up.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                Responses.Count > 0 ? Responses.Dequeue() : ResponseFactory(request));
        }
    }

    private sealed class TrackingContent(string body) : HttpContent
    {
        public bool WasRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            WasRead = true;
            return stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = Encoding.UTF8.GetByteCount(body);
            return true;
        }
    }

    private sealed class TrackingResponseMessage : HttpResponseMessage
    {
        private readonly TrackingContent? _trackingContent;

        public TrackingResponseMessage(HttpStatusCode statusCode, TrackingContent? trackingContent = null)
            : base(statusCode)
        {
            _trackingContent = trackingContent;
            if (trackingContent is not null)
            {
                trackingContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Content = trackingContent;
            }
        }

        public bool WasDisposed { get; private set; }

        public bool WasDisposedAfterContentWasRead { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                WasDisposed = true;
                WasDisposedAfterContentWasRead = _trackingContent is null || _trackingContent.WasRead;
            }

            base.Dispose(disposing);
        }
    }

    private static (SlotsClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new SlotsClient(http), handler);
    }

    [Fact]
    public async Task TheBoardIsFetchedFromTheBoardRoute()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotBoardDto([], [])),
        };

        var outcome = await client.GetBoardAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("/api/slots/board", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ProposingPostsTheDateAndStartTime()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(Guid.NewGuid()),
        };

        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/slots/proposals", request.RequestUri!.AbsolutePath);
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("2026-09-10", body);
        Assert.Contains("09:00", body);
    }

    [Fact]
    public async Task AcceptingPostsTheHeadcountToTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.AcceptAsync(proposalId, 10, CancellationToken.None);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/slots/proposals/{proposalId}/acceptance", request.RequestUri!.AbsolutePath);
        Assert.Contains("10", await request.Content!.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithdrawingAnAcceptanceDeletesTheAcceptanceRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal(
            $"/api/slots/proposals/{proposalId}/acceptance",
            handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task WithdrawingAProposalDeletesTheProposalRoute()
    {
        var (client, handler) = Given();
        var proposalId = Guid.NewGuid();

        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);

        Assert.Equal($"/api/slots/proposals/{proposalId}", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CancellingASlotPassesTheConfirmFlagInTheQueryString()
    {
        var (client, handler) = Given();
        var slotId = Guid.NewGuid();

        await client.CancelConfirmedSlotAsync(slotId, true, CancellationToken.None);

        Assert.Equal($"/api/slots/confirmed/{slotId}", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("?confirm=true", handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task TheFirstUnconfirmedCancelSurfacesTheWarningText()
    {
        var (client, handler) = Given();
        handler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"Cancelling this slot will cancel 6 confirmed bookings. Affected candidates will be notified and re-invited. Confirm to proceed.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.CancelConfirmedSlotAsync(Guid.NewGuid(), false, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("6 confirmed bookings", outcome.ErrorMessage);
    }

    [Fact]
    public async Task EveryResponseIsDisposedOnlyAfterTheClientHasConsumedItsContent()
    {
        var (client, handler) = Given();
        var boardContent = new TrackingContent(JsonSerializer.Serialize(new SlotBoardDto([], [])));
        var proposalContent = new TrackingContent(JsonSerializer.Serialize(Guid.NewGuid()));
        var boardResponse = new TrackingResponseMessage(HttpStatusCode.OK, boardContent);
        var proposalResponse = new TrackingResponseMessage(HttpStatusCode.Created, proposalContent);
        var acceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnAcceptanceResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var withdrawnProposalResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var cancelledSlotResponse = new TrackingResponseMessage(HttpStatusCode.NoContent);
        var responses = new[]
        {
            boardResponse,
            proposalResponse,
            acceptanceResponse,
            withdrawnAcceptanceResponse,
            withdrawnProposalResponse,
            cancelledSlotResponse,
        };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        var proposalId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        await client.GetBoardAsync(CancellationToken.None);
        await client.ProposeAsync(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), CancellationToken.None);
        await client.AcceptAsync(proposalId, 1, CancellationToken.None);
        await client.WithdrawAcceptanceAsync(proposalId, CancellationToken.None);
        await client.WithdrawProposalAsync(proposalId, CancellationToken.None);
        await client.CancelConfirmedSlotAsync(slotId, false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(boardContent.WasRead);
        Assert.True(proposalContent.WasRead);
        Assert.True(boardResponse.WasDisposedAfterContentWasRead);
        Assert.True(proposalResponse.WasDisposedAfterContentWasRead);
    }

    [Fact]
    public async Task GetSlotOperationsCallsTheOperationsRouteAndMapsRows()
    {
        var handler = new StubHandler();
        var slotId = Guid.NewGuid();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new SlotOperationsDto(
            [
                new SlotOperationDto(
                    slotId,
                    new DateOnly(2026, 9, 10),
                    new TimeOnly(9, 0),
                    new TimeOnly(13, 0),
                    [new SlotOperationCapacityDto("DAT", 10, 9)],
                    1),
            ])),
        });
        var client = new SlotsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetSlotOperationsAsync(CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var slot = Assert.Single(outcome.Value!.Slots);
        Assert.Equal("/api/slots/operations", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(slotId, slot.ConfirmedSlotId);
        Assert.Equal(1, slot.ActiveBookings);
        Assert.Equal("DAT", Assert.Single(slot.Capacities).Code);
    }

    [Fact]
    public async Task GetSlotOperationsSurfacesAForbiddenResponseAsAFailure()
    {
        var handler = new StubHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
        var client = new SlotsClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") });

        var outcome = await client.GetSlotOperationsAsync(CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal((int)HttpStatusCode.Forbidden, outcome.StatusCode);
    }
}
`````

## tests/EventBooking.Web.Tests/StaffAccessClientTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/StaffAccessClientTests.cs","encoding":"utf8","sha256":"16a89c15c76403ffc621947c5604fa4820fb18c581ffedc44ddff6f9223e6019","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class StaffAccessClientTests
{
    [Fact]
    public async Task ListReplaceScopeAndClearScopeUseTheScopeOnlyContract()
    {
        var target = Guid.NewGuid();
        var handler = new RecordingHandler();
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create<IReadOnlyList<StaffAccessProfileDto>>([]),
        });
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new StaffAccessMutationDto(
                new StaffAccessProfileDto(
                    target, ["Manager"], Guid.NewGuid(), "Medical Check-up", 2),
                null)),
        });
        handler.Responses.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new StaffAccessClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        });

        await client.ListAsync(CancellationToken.None);
        await client.ReplaceScopeAsync(target, Guid.NewGuid(), 1, CancellationToken.None);
        await client.ClearScopeAsync(target, 2, CancellationToken.None);

        Assert.Collection(
            handler.Requests,
            request => Assert.Equal("/api/admin/staff-access", request.RequestUri!.PathAndQuery),
            request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal($"/api/admin/staff-access/{target}", request.RequestUri!.PathAndQuery);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Delete, request.Method);
                Assert.Equal(
                    $"/api/admin/staff-access/{target}?expectedVersion=2",
                    request.RequestUri!.PathAndQuery);
            });
        Assert.DoesNotContain("roles", handler.Bodies[1]);
        Assert.Contains("\"expectedVersion\":1", handler.Bodies[1]);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public Queue<HttpResponseMessage> Responses { get; } = [];

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
`````

## tests/EventBooking.Web.Tests/StaffAccessPageTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/StaffAccessPageTests.cs","encoding":"utf8","sha256":"19192939a8150ae8ea9bc7355e3a26bf0bfd88dec8b8c7eb160f0f9155d28b0d","parts":1,"part":1} -->

`````csharp
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
`````

## tests/EventBooking.Web.Tests/StaffNavigationTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/StaffNavigationTests.cs","encoding":"utf8","sha256":"13798a580596891efd3c96be8b7d0925b5f1a88419f78485376c06f7bac2d6fa","parts":1,"part":1} -->

`````csharp
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the deterministic union of staff navigation links per role combination.</summary>
public class StaffNavigationTests
{
    /// <summary>Gets every valid role shape with its exact expected link union.</summary>
    public static TheoryData<string[], string[]> EveryValidShape => new()
    {
        { ["Admin"], ["/settings", "/staff-access", "/confirmed-slots", "/audit"] },
        { ["Coordinator"], ["/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
        { ["Manager"], ["/slots", "/appointments"] },
        { ["AppointmentStaff"], ["/appointments"] },
        { ["Manager", "Coordinator"], ["/slots", "/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
        { ["Coordinator", "AppointmentStaff"], ["/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
        { ["Manager", "AppointmentStaff"], ["/slots", "/appointments"] },
        { ["Manager", "Coordinator", "AppointmentStaff"], ["/slots", "/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"] },
    };

    /// <summary>Verifies every valid profile shape receives its exact link union.</summary>
    [Theory]
    [MemberData(nameof(EveryValidShape))]
    public void EveryValidProfileShapeGetsItsExactLinkUnion(
        string[] roles,
        string[] expectedRoutes)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            roles,
            roles.Contains("Manager") || roles.Contains("AppointmentStaff")
                ? Guid.NewGuid()
                : null,
            null));

        Assert.Equal(expectedRoutes, links.Select(link => link.Href));
    }

    /// <summary>Verifies administrators keep only administrative and slot-only links.</summary>
    [Fact]
    public void AdminHasOnlyAdministrativeAndSlotOnlyLinks()
    {
        var links = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null));

        Assert.Equal(
            ["/settings", "/staff-access", "/confirmed-slots", "/audit"],
            links.Select(link => link.Href));
        Assert.DoesNotContain(links, link => link.Href is "/candidates" or "/dashboards");
    }

    /// <summary>Verifies the Staff access link describes scope assignment, since roles come from the identity provider.</summary>
    [Fact]
    public void StaffAccessLinkDescribesScopeAssignmentNotRoleAssignment()
    {
        var staffAccess = StaffNavigation.LinksFor(new MeDto(["Admin"], null, null))
            .Single(link => link.Href == "/staff-access");

        Assert.Equal("Set appointment-type scope", staffAccess.Description);
    }

    /// <summary>Verifies a coordinator-manager keeps the union without duplicates.</summary>
    [Fact]
    public void CoordinatorManagerGetsTheUnionWithoutDuplicates()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["Manager", "Coordinator"],
            Guid.NewGuid(),
            "Medical Check-up"));

        Assert.Equal(
            ["/slots", "/appointments", "/candidates", "/dashboards", "/confirmed-slots", "/audit"],
            links.Select(link => link.Href));
        Assert.Equal(links.Count, links.Select(link => link.Href).Distinct().Count());
    }

    /// <summary>Verifies appointment-only staff see only the appointment workspace.</summary>
    [Fact]
    public void AppointmentStaffHasOnlyTheAppointmentWorkspace()
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            ["AppointmentStaff"], Guid.NewGuid(), "Uniform Fitting"));

        Assert.Equal(["/appointments"], links.Select(link => link.Href));
    }

    /// <summary>Verifies empty roles receive no links.</summary>
    [Fact]
    public void EmptyRolesHaveNoLinks()
    {
        Assert.Empty(StaffNavigation.LinksFor(new MeDto([], null, null)));
    }

    /// <summary>Verifies the audit trail is offered only to the roles that may search it.</summary>
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Coordinator", true)]
    [InlineData("Manager", false)]
    [InlineData("AppointmentStaff", false)]
    public void TheAuditTrailIsOfferedOnlyToRolesThatMaySearchIt(string role, bool expected)
    {
        var links = StaffNavigation.LinksFor(new MeDto(
            [role],
            role is "Manager" or "AppointmentStaff" ? Guid.NewGuid() : null,
            null));

        Assert.Equal(expected, links.Any(link => link.Href == "/audit"));
    }
}
`````

## tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs","encoding":"utf8","sha256":"243011c494022f0634d4eca71c2908e6711f08af3de405e3969bec2d7c548547","parts":1,"part":1} -->

`````csharp
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the role-to-guide mapping bundled from docs/user-guides.</summary>
public class UserGuideCatalogTests
{
    [Fact]
    public void AnyRecognisedRoleGetsEveryGuideIncludingCandidate()
    {
        var guides = UserGuideCatalog.GuidesFor(["Admin"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Candidate"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void CombinedRolesStillGetEveryGuideNotJustTheirOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator", "Manager", "AppointmentStaff"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Candidate"],
            guides.Select(g => g.RoleKey));
    }

    [Fact]
    public void SingleRoleGetsEveryGuideNotJustItsOwn()
    {
        var guides = UserGuideCatalog.GuidesFor(["Coordinator"]);

        Assert.Equal(
            ["Admin", "Manager", "AppointmentStaff", "Coordinator", "Candidate"],
            guides.Select(g => g.RoleKey));
        Assert.Contains(guides, g => g.RoleKey == "Coordinator" && g.Html.Contains("Candidates"));
    }

    [Fact]
    public void MissingOrUnrecognisedRolesGetNoGuide()
    {
        Assert.Empty(UserGuideCatalog.GuidesFor([]));
        Assert.Empty(UserGuideCatalog.GuidesFor(["SomeFutureRole"]));
    }

    [Fact]
    public void EachGuideRendersMarkdownHeadingsAsHtml()
    {
        var guide = UserGuideCatalog.GuidesFor(["Admin"]).Single(g => g.RoleKey == "Admin");

        Assert.Contains("<h1", guide.Html);
        Assert.Contains("<h2", guide.Html);
    }

    [Fact]
    public void CrossGuideLinksAreRewrittenAwayFromBareMarkdownFilenames()
    {
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.DoesNotContain("appointment-staff-guide.md", manager.Html);
        Assert.DoesNotContain("README.md", manager.Html);
        Assert.DoesNotContain("All user guides", manager.Html);
    }

    [Fact]
    public void CrossGuideAnchorLinksCarryTheHelpPathSoTheyDoNotResolveAgainstBaseHref()
    {
        // Blazor's <base href="/"> resolves a bare "#anchor" against "/" (Home), not against the
        // current route, so every in-page cross-link must be an absolute "/help#..." path.
        var manager = UserGuideCatalog.GuidesFor(["Manager"]).Single(g => g.RoleKey == "Manager");

        Assert.Contains("href=\"/help#appointmentstaff\"", manager.Html);
    }

    [Fact]
    public void CandidateGuideIsAvailableOutsideTheRoleMap()
    {
        var guide = UserGuideCatalog.CandidateGuide();

        Assert.Equal("Candidate", guide.RoleKey);
        Assert.Contains("<h1", guide.Html);
        Assert.DoesNotContain("README.md", guide.Html);
    }
}
`````
