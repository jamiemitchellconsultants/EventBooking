# 00b — Vocabulary edits 109 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":372,"oldPath":"tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs","beforeSha":"cbc6d581b39744a69e08727700bfc7bc6f96caf46e8ae194f0df2d293bc7ba12","afterSha":"395bb2d22e40bbf8511abd5686c1675ab0fbbe9df8116e3b5476ed49abb24700","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Services;
using EventBooking.Web.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>
/// The Task 70 audit panel is loaded lazily on first expand so a table of many slots does not fire
/// one request per row. This proves the lazy load actually happens, and only once.
/// </summary>
public class AuditHistoryComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>
    /// Verifies the audit panel issues one lazy request and renders the returned audit row.
    /// </summary>
    [Fact]
    public void TheHistoryIsFetchedOnFirstExpandOnlyAndShowsItsRows()
    {
        var rows = new List<AuditRowDto>
        {
            new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
                "ConfirmedSlot", Guid.NewGuid(), "SlotConfirmed", "Staff", "staff-1", "6 headcount"),
        };
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rows, options: CamelCase),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<AuditHistory>(parameters => parameters
            .Add(p => p.SlotId, Guid.NewGuid()));

        Assert.Equal(0, handler.RequestCount);

        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());
        cut.WaitForAssertion(() => Assert.Contains("6 headcount", cut.Markup));
        Assert.Equal(1, handler.RequestCount);

        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());
        Assert.Equal(1, handler.RequestCount);
    }
}
`````

## after — tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":372,"oldPath":"tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs","beforeSha":"cbc6d581b39744a69e08727700bfc7bc6f96caf46e8ae194f0df2d293bc7ba12","afterSha":"395bb2d22e40bbf8511abd5686c1675ab0fbbe9df8116e3b5476ed49abb24700","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Services;
using EventBooking.Web.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>
/// The Task 70 audit panel is loaded lazily on first expand so a table of many events does not fire
/// one request per row. This proves the lazy load actually happens, and only once.
/// </summary>
public class AuditHistoryComponentTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    /// <summary>
    /// Verifies the audit panel issues one lazy request and renders the returned audit row.
    /// </summary>
    [Fact]
    public void TheHistoryIsFetchedOnFirstExpandOnlyAndShowsItsRows()
    {
        var rows = new List<AuditRowDto>
        {
            new(new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
                "Event", Guid.NewGuid(), "EventConfirmed", "Staff", "staff-1", "6 headcount"),
        };
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(rows, options: CamelCase),
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        var cut = Render<AuditHistory>(parameters => parameters
            .Add(p => p.EventId, Guid.NewGuid()));

        Assert.Equal(0, handler.RequestCount);

        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());
        cut.WaitForAssertion(() => Assert.Contains("6 headcount", cut.Markup));
        Assert.Equal(1, handler.RequestCount);

        cut.Find("details").TriggerEvent("ontoggle", new EventArgs());
        Assert.Equal(1, handler.RequestCount);
    }
}
`````

## before — tests/EventBooking.Web.Tests/AuditPageTests.cs — 1/1

<!-- vocabulary-file: {"id":373,"oldPath":"tests/EventBooking.Web.Tests/AuditPageTests.cs","newPath":"tests/EventBooking.Web.Tests/AuditPageTests.cs","beforeSha":"0a7db81b03cd7bca946ffc03ad1445ec3de7b26444b0f454a806fc20392edb71","afterSha":"f5bd953dd212f23391d91121a0aebe67e5ff13ca746d260091f5d0c445c23a03","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the cross-cutting audit page filters, paginates, and respects the caller's bucket.</summary>
public class AuditPageTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class RouteHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static AuditRowDto Row(string action, Guid? id = null) => new(
        new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
        "ConfirmedSlot",
        id ?? Guid.NewGuid(),
        action,
        "Staff",
        "staff-1",
        $"{action} details");

    private RouteHandler Given(IReadOnlyList<string> roles, Queue<AuditSearchPageDto> pages)
    {
        var handler = new RouteHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/me"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new MeDto(roles, null, null), options: CamelCase),
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        pages.Count > 0 ? pages.Dequeue() : new AuditSearchPageDto([], null),
                        options: CamelCase),
                });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new MeClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));
        return handler;
    }

    [Fact]
    public void EntityTypeDropdownAbsentForAdminOnlyCaller()
    {
        Given(["Admin"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void EntityTypeDropdownPresentForCoordinator()
    {
        Given(["Coordinator"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void TheNewestPageIsLoadedOnArrival()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));
    }

    [Fact]
    public void LoadMoreAppendsRatherThanReplaces()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], "cursor-1"));
        pages.Enqueue(new AuditSearchPageDto([Row("SlotCancelled")], null));
        var handler = Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));

        cut.Find("#audit-load-more").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("SlotConfirmed details", cut.Markup);
            Assert.Contains("SlotCancelled details", cut.Markup);
        });
        Assert.Contains(handler.Requests, uri => uri.Query.Contains("cursor=cursor-1"));
    }

    [Fact]
    public void LoadMoreIsHiddenWhenThePageIsExhausted()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));
        Assert.Empty(cut.FindAll("#audit-load-more"));
    }

    [Fact]
    public void SearchingAgainReplacesThePreviousResults()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], null));
        pages.Enqueue(new AuditSearchPageDto([Row("SlotCancelled")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("SlotConfirmed details", cut.Markup));

        cut.Find("#audit-search").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("SlotCancelled details", cut.Markup);
            Assert.DoesNotContain("SlotConfirmed details", cut.Markup);
        });
    }

    [Fact]
    public void AFailedSearchShowsTheSafeErrorMessage()
    {
        var handler = new RouteHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/me"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new MeDto(["Coordinator"], null, null), options: CamelCase),
                }
                : new HttpResponseMessage(HttpStatusCode.Forbidden));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new MeClient(http));
        Services.AddSingleton(new HeadOfficeTimePresentation("Europe/London"));

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=alert]")));
    }
    [Fact]
    public void ActionButtonsUseTheSharedButtonStyles()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("SlotConfirmed")], "cursor-1"));
        Given(["Admin"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-load-more")));
        Assert.Equal(["button", "button-primary"], cut.Find("#audit-search").ClassList);
        Assert.Equal(["button"], cut.Find("#audit-load-more").ClassList);
    }

    [Fact]
    public void EachFilterCaptionIsSeparateFromItsControl()
    {
        Given(["Coordinator"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
        var fields = cut.FindAll(".audit-filters .field");
        Assert.Equal(6, fields.Count);
        Assert.All(fields, field =>
        {
            var label = field.QuerySelector("label.field-label");
            Assert.NotNull(label);
            var control = field.QuerySelector("input, select");
            Assert.NotNull(control);
            Assert.Equal(control!.Id, label!.GetAttribute("for"));
        });
    }
}
`````

## after — tests/EventBooking.Web.Tests/AuditPageTests.cs — 1/1

<!-- vocabulary-file: {"id":373,"oldPath":"tests/EventBooking.Web.Tests/AuditPageTests.cs","newPath":"tests/EventBooking.Web.Tests/AuditPageTests.cs","beforeSha":"0a7db81b03cd7bca946ffc03ad1445ec3de7b26444b0f454a806fc20392edb71","afterSha":"f5bd953dd212f23391d91121a0aebe67e5ff13ca746d260091f5d0c445c23a03","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the cross-cutting audit page filters, paginates, and respects the caller's bucket.</summary>
public class AuditPageTests : BunitContext
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class RouteHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static AuditRowDto Row(string action, Guid? id = null) => new(
        new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero),
        "Event",
        id ?? Guid.NewGuid(),
        action,
        "Staff",
        "staff-1",
        $"{action} details");

    private RouteHandler Given(IReadOnlyList<string> roles, Queue<AuditSearchPageDto> pages)
    {
        var handler = new RouteHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/me"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new MeDto(roles, null, null), options: CamelCase),
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(
                        pages.Count > 0 ? pages.Dequeue() : new AuditSearchPageDto([], null),
                        options: CamelCase),
                });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new MeClient(http));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));
        return handler;
    }

    [Fact]
    public void EntityTypeDropdownAbsentForAdminOnlyCaller()
    {
        Given(["Admin"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void EntityTypeDropdownPresentForCoordinator()
    {
        Given(["Coordinator"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
    }

    [Fact]
    public void TheNewestPageIsLoadedOnArrival()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("EventConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));
    }

    [Fact]
    public void LoadMoreAppendsRatherThanReplaces()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("EventConfirmed")], "cursor-1"));
        pages.Enqueue(new AuditSearchPageDto([Row("EventCancelled")], null));
        var handler = Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));

        cut.Find("#audit-load-more").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EventConfirmed details", cut.Markup);
            Assert.Contains("EventCancelled details", cut.Markup);
        });
        Assert.Contains(handler.Requests, uri => uri.Query.Contains("cursor=cursor-1"));
    }

    [Fact]
    public void LoadMoreIsHiddenWhenThePageIsExhausted()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("EventConfirmed")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));
        Assert.Empty(cut.FindAll("#audit-load-more"));
    }

    [Fact]
    public void SearchingAgainReplacesThePreviousResults()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("EventConfirmed")], null));
        pages.Enqueue(new AuditSearchPageDto([Row("EventCancelled")], null));
        Given(["Coordinator"], pages);

        var cut = Render<Audit>();
        cut.WaitForAssertion(() => Assert.Contains("EventConfirmed details", cut.Markup));

        cut.Find("#audit-search").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("EventCancelled details", cut.Markup);
            Assert.DoesNotContain("EventConfirmed details", cut.Markup);
        });
    }

    [Fact]
    public void AFailedSearchShowsTheSafeErrorMessage()
    {
        var handler = new RouteHandler(request =>
            request.RequestUri!.AbsolutePath == "/api/me"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new MeDto(["Coordinator"], null, null), options: CamelCase),
                }
                : new HttpResponseMessage(HttpStatusCode.Forbidden));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new MeClient(http));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role=alert]")));
    }
    [Fact]
    public void ActionButtonsUseTheSharedButtonStyles()
    {
        var pages = new Queue<AuditSearchPageDto>();
        pages.Enqueue(new AuditSearchPageDto([Row("EventConfirmed")], "cursor-1"));
        Given(["Admin"], pages);

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-load-more")));
        Assert.Equal(["button", "button-primary"], cut.Find("#audit-search").ClassList);
        Assert.Equal(["button"], cut.Find("#audit-load-more").ClassList);
    }

    [Fact]
    public void EachFilterCaptionIsSeparateFromItsControl()
    {
        Given(["Coordinator"], new Queue<AuditSearchPageDto>());

        var cut = Render<Audit>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#audit-entity-type")));
        var fields = cut.FindAll(".audit-filters .field");
        Assert.Equal(6, fields.Count);
        Assert.All(fields, field =>
        {
            var label = field.QuerySelector("label.field-label");
            Assert.NotNull(label);
            var control = field.QuerySelector("input, select");
            Assert.NotNull(control);
            Assert.Equal(control!.Id, label!.GetAttribute("for"));
        });
    }
}
`````

## before — tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs — 1/1

<!-- vocabulary-file: {"id":374,"oldPath":"tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs","newPath":"tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs","beforeSha":"481b8a6efbb948498b93020dd690d7fcb0f268d8133afc286a9147d379a3bfba","afterSha":"f944899122dca3691e241b373f4f93021bbb06e35f6b0e364f944d05b87b5c3f","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the book page distinguishes recovery visits with singular/plural copy.</summary>
public class BookRecoveryHeadingTests : BunitContext
{
    [Fact]
    public void InitialInviteKeepsTheChooseATimeHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: false));

        cut.WaitForAssertion(() =>
            Assert.Equal("Choose a time", cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesTheSingularMissedAppointmentHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointment",
                cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesThePluralHeadingForTwoTypes()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["Medical Check-Up", "Uniform Fitting"],
            [Option()],
            IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointments",
                cut.Find("h1").TextContent.Trim()));
    }

    private IRenderedComponent<Book> RenderBook(InviteDto invite)
    {
        var handler = new StubInviteHandler(invite);
        Services.AddSingleton(
            new BookingClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new CandidatePageOptions("recruitment@example.com"));

        return Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
    }

    private static InviteOptionDto Option() =>
        new(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00");

    private sealed class StubInviteHandler(InviteDto invite) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(invite),
            });
    }
}
`````

## after — tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs — 1/1

<!-- vocabulary-file: {"id":374,"oldPath":"tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs","newPath":"tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs","beforeSha":"481b8a6efbb948498b93020dd690d7fcb0f268d8133afc286a9147d379a3bfba","afterSha":"f944899122dca3691e241b373f4f93021bbb06e35f6b0e364f944d05b87b5c3f","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the book page distinguishes recovery visits with singular/plural copy.</summary>
public class BookRecoveryHeadingTests : BunitContext
{
    [Fact]
    public void InitialInviteKeepsTheChooseATimeHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: false));

        cut.WaitForAssertion(() =>
            Assert.Equal("Choose a time", cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesTheSingularMissedAppointmentHeading()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(), "Amara Novak", ["Medical Check-Up"], [Option()], IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointment",
                cut.Find("h1").TextContent.Trim()));
    }

    [Fact]
    public void RecoveryInviteUsesThePluralHeadingForTwoTypes()
    {
        var cut = RenderBook(new InviteDto(
            Guid.NewGuid(),
            "Amara Novak",
            ["Medical Check-Up", "Uniform Fitting"],
            [Option()],
            IsRecovery: true));

        cut.WaitForAssertion(() =>
            Assert.Equal(
                "Choose a new time for your missed appointments",
                cut.Find("h1").TextContent.Trim()));
    }

    private IRenderedComponent<Book> RenderBook(InviteDto invite)
    {
        var handler = new StubInviteHandler(invite);
        Services.AddSingleton(
            new BookingClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }));
        Services.AddSingleton(new AttendeePageOptions("recruitment@example.com"));

        return Render<Book>(parameters => parameters.Add(page => page.Token, "invite-token"));
    }

    private static InviteOptionDto Option() =>
        new(
            Guid.NewGuid(),
            new DateOnly(2030, 1, 14),
            new TimeOnly(9, 0),
            new TimeOnly(13, 0),
            "Monday 14 Jan 2030, 09:00-13:00");

    private sealed class StubInviteHandler(InviteDto invite) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(invite),
            });
    }
}
`````

## before — tests/EventBooking.Web.Tests/BookingClientTests.cs — 1/1

<!-- vocabulary-file: {"id":375,"oldPath":"tests/EventBooking.Web.Tests/BookingClientTests.cs","newPath":"tests/EventBooking.Web.Tests/BookingClientTests.cs","beforeSha":"d69a1a3ecb2225f6982860748d551126dc2caea27657474619c9bb4828568089","afterSha":"3b496ea553cf6daa363593533db40b794a34efd81d037a253e515d9dd1a03421","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class BookingClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.NoContent);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responses.Count > 0 ? Responses.Dequeue() : Response;
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

    private static (BookingClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new BookingClient(http), handler);
    }

    [Fact]
    public async Task TheInviteIsFetchedByTokenAndTheTokenIsEscaped()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new InviteDto(Guid.NewGuid(), "Amara Novak", ["Uniform Fitting"], [])),
        };

        await client.GetInviteAsync("abc.def/ghi", CancellationToken.None);

        Assert.Equal("/api/booking/abc.def%2Fghi", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task AnExpiredLinkComesBackAsTheGenericMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{"title":"not_found","detail":"This booking link is no longer valid.","status":404}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.GetInviteAsync("anything", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal("This booking link is no longer valid.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmingPostsTheChosenSlot()
    {
        var (client, handler) = Given();
        var slotId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ConfirmedBookingDto(
                Guid.NewGuid(), new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0), "manage-token")),
        };

        var outcome = await client.ConfirmAsync("tok", slotId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/tok/confirm", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains(slotId.ToString(), handler.Bodies[0]);
        Assert.Equal("manage-token", outcome.Value!.ManageToken);
    }

    [Fact]
    public async Task AnOptionThatFilledUpComesBackAsAConflictWithItsMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"That time filled up while you were choosing. Please pick from the updated options.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.ConfirmAsync("tok", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(409, outcome.StatusCode);
        Assert.Contains("filled up", outcome.ErrorMessage);
    }

    [Fact]
    public async Task TheBookingIsFetchedFromTheManageRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new BookingDto(
                new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0),
                "Friday 11 Sep 2026, 13:00-17:00", "Amara Novak")),
        };

        var outcome = await client.GetBookingAsync("mtok", CancellationToken.None);

        Assert.Equal("/api/booking/manage/mtok", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("Amara Novak", outcome.Value!.CandidateName);
    }

    [Fact]
    public async Task CancellingSendsTheRebookFlag()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(true)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/manage/mtok/cancel", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("true", handler.Bodies[0]);
        Assert.True(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task CancellingWithoutAvailableSlotsReportsThatNoInviteWasSent()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(false)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task EveryResponseIsDisposedAfterEachCandidateOperation()
    {
        var (client, handler) = Given();
        var inviteResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"inviteId":"00000000-0000-0000-0000-000000000001","candidateName":"Amara Novak","appointmentTypeNames":["Uniform Fitting"],"options":[]}"""));
        var confirmedResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"bookingId":"00000000-0000-0000-0000-000000000002","date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","manageToken":"manage-token"}"""));
        var bookingResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","display":"Friday 11 Sep 2026","candidateName":"Amara Novak"}"""));
        var cancelResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"reinvited":false}"""));
        var responses = new[] { inviteResponse, confirmedResponse, bookingResponse, cancelResponse };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        await client.GetInviteAsync("invite", CancellationToken.None);
        await client.ConfirmAsync("invite", Guid.NewGuid(), CancellationToken.None);
        await client.GetBookingAsync("manage", CancellationToken.None);
        await client.CancelAsync("manage", false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(inviteResponse.WasDisposedAfterContentWasRead);
        Assert.True(confirmedResponse.WasDisposedAfterContentWasRead);
        Assert.True(bookingResponse.WasDisposedAfterContentWasRead);
        Assert.True(cancelResponse.WasDisposedAfterContentWasRead);
    }
}
`````

## after — tests/EventBooking.Web.Tests/BookingClientTests.cs — 1/1

<!-- vocabulary-file: {"id":375,"oldPath":"tests/EventBooking.Web.Tests/BookingClientTests.cs","newPath":"tests/EventBooking.Web.Tests/BookingClientTests.cs","beforeSha":"d69a1a3ecb2225f6982860748d551126dc2caea27657474619c9bb4828568089","afterSha":"3b496ea553cf6daa363593533db40b794a34efd81d037a253e515d9dd1a03421","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class BookingClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public Queue<HttpResponseMessage> Responses { get; } = [];

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.NoContent);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return Responses.Count > 0 ? Responses.Dequeue() : Response;
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

    private static (BookingClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new BookingClient(http), handler);
    }

    [Fact]
    public async Task TheInviteIsFetchedByTokenAndTheTokenIsEscaped()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new InviteDto(Guid.NewGuid(), "Amara Novak", ["Uniform Fitting"], [])),
        };

        await client.GetInviteAsync("abc.def/ghi", CancellationToken.None);

        Assert.Equal("/api/booking/abc.def%2Fghi", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task AnExpiredLinkComesBackAsTheGenericMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                """{"title":"not_found","detail":"This booking link is no longer valid.","status":404}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.GetInviteAsync("anything", CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(404, outcome.StatusCode);
        Assert.Equal("This booking link is no longer valid.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task ConfirmingPostsTheChosenEvent()
    {
        var (client, handler) = Given();
        var eventId = Guid.NewGuid();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ConfirmedBookingDto(
                Guid.NewGuid(), new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0), "manage-token")),
        };

        var outcome = await client.ConfirmAsync("tok", eventId, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/tok/confirm", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains(eventId.ToString(), handler.Bodies[0]);
        Assert.Equal("manage-token", outcome.Value!.ManageToken);
    }

    [Fact]
    public async Task AnOptionThatFilledUpComesBackAsAConflictWithItsMessage()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"That time filled up while you were choosing. Please pick from the updated options.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await client.ConfirmAsync("tok", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(409, outcome.StatusCode);
        Assert.Contains("filled up", outcome.ErrorMessage);
    }

    [Fact]
    public async Task TheBookingIsFetchedFromTheManageRoute()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new BookingDto(
                new DateOnly(2026, 9, 11), new TimeOnly(13, 0), new TimeOnly(17, 0),
                "Friday 11 Sep 2026, 13:00-17:00", "Amara Novak")),
        };

        var outcome = await client.GetBookingAsync("mtok", CancellationToken.None);

        Assert.Equal("/api/booking/manage/mtok", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("Amara Novak", outcome.Value!.AttendeeName);
    }

    [Fact]
    public async Task CancellingSendsTheRebookFlag()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(true)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/api/booking/manage/mtok/cancel", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Contains("true", handler.Bodies[0]);
        Assert.True(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task CancellingWithoutAvailableEventsReportsThatNoInviteWasSent()
    {
        var (client, handler) = Given();
        handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new CancelOutcomeDto(false)),
        };

        var outcome = await client.CancelAsync("mtok", true, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.Value!.Reinvited);
    }

    [Fact]
    public async Task EveryResponseIsDisposedAfterEachAttendeeOperation()
    {
        var (client, handler) = Given();
        var inviteResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"inviteId":"00000000-0000-0000-0000-000000000001","attendeeName":"Amara Novak","appointmentTypeNames":["Uniform Fitting"],"options":[]}"""));
        var confirmedResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"bookingId":"00000000-0000-0000-0000-000000000002","date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","manageToken":"manage-token"}"""));
        var bookingResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"date":"2026-09-11","startTime":"13:00:00","endTime":"17:00:00","display":"Friday 11 Sep 2026","attendeeName":"Amara Novak"}"""));
        var cancelResponse = new TrackingResponseMessage(
            HttpStatusCode.OK,
            new TrackingContent("""{"reinvited":false}"""));
        var responses = new[] { inviteResponse, confirmedResponse, bookingResponse, cancelResponse };

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        await client.GetInviteAsync("invite", CancellationToken.None);
        await client.ConfirmAsync("invite", Guid.NewGuid(), CancellationToken.None);
        await client.GetBookingAsync("manage", CancellationToken.None);
        await client.CancelAsync("manage", false, CancellationToken.None);

        Assert.All(responses, response => Assert.True(response.WasDisposed));
        Assert.True(inviteResponse.WasDisposedAfterContentWasRead);
        Assert.True(confirmedResponse.WasDisposedAfterContentWasRead);
        Assert.True(bookingResponse.WasDisposedAfterContentWasRead);
        Assert.True(cancelResponse.WasDisposedAfterContentWasRead);
    }
}
`````
