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
