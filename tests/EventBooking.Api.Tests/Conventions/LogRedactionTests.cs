using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventBooking.Api.Tests.Conventions;

/// <summary>Design 08: operational logs never carry a name, email address or token.</summary>
[Collection("api")]
public sealed class LogRedactionTests(ApiFactory factory)
{
    [Fact]
    public async Task ABookingRequestLogsNeitherItsTokenNorAnyEmailAddress()
    {
        var sink = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(sink)));
        var client = host.CreateClient();
        var token = "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

        await client.GetAsync($"/api/booking/{token}");

        var logged = sink.Text();
        Assert.DoesNotContain(token, logged, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACreateAttendeeRequestLogsNeitherTheNameNorTheEmail()
    {
        var sink = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(sink)));
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        factory.StaffIdClaim = "U500003";
        var client = host.CreateClient();

        await client.PostAsJsonAsync("/api/attendees", new
        {
            name = "Marguerite Hathaway",
            email = "marguerite.hathaway@example.com",
            attendeeGroupId = Guid.NewGuid(),
        });

        var logged = sink.Text();
        Assert.DoesNotContain("Marguerite", logged, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("marguerite.hathaway@example.com", logged, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every request log line carries the correlation identifier it was served under.</summary>
    [Fact]
    public async Task EveryRequestCarriesACorrelationIdentifierInItsResponseAndItsLogs()
    {
        var sink = new CapturingLoggerProvider();
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<ILoggerProvider>(sink)));
        factory.SignedInAs = null;
        var client = host.CreateClient();

        var response = await client.GetAsync("/health/live");

        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-Id"));
        Assert.False(string.IsNullOrWhiteSpace(correlation));
        Assert.Contains(correlation, sink.Text(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The application's own redaction is only half of it: the framework's request logging
    /// formats the raw path itself. Program.cs raises that category to Warning, and this is
    /// what stops a later change quietly putting it back.
    /// </summary>
    [Fact]
    public void TheFrameworksOwnRequestLoggingIsSilencedAtInformation()
    {
        var factories = factory.Services.GetRequiredService<ILoggerFactory>();

        var hosting = factories.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");

        Assert.False(hosting.IsEnabled(LogLevel.Information));
        Assert.True(hosting.IsEnabled(LogLevel.Warning));
    }

    /// <summary>A supplied traceparent is adopted rather than replaced.</summary>
    [Fact]
    public async Task ASuppliedTraceparentBecomesTheCorrelationIdentifier()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("traceparent", $"00-{TraceId}-00f067aa0ba902b7-01");

        var response = await client.SendAsync(request);

        Assert.Equal(TraceId, Assert.Single(response.Headers.GetValues("X-Correlation-Id")));
    }

    /// <summary>Captures every log line the host writes while one case runs.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _lines = [];

        public string Text()
        {
            lock (_lines)
            {
                return string.Join('\n', _lines);
            }
        }

        /// <summary>
    /// Only this application's own categories are captured. The framework's hosting logger
    /// puts the raw request path on its request scope and EF Core renders parameter names
    /// like @__p_0 in its command logs; capturing either would make these cases assert
    /// something about ASP.NET Core rather than about this application's redaction.
    /// </summary>
    /// <param name="categoryName">The logger category.</param>
    /// <returns>The sink, or a logger that records nothing.</returns>
    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith("EventBooking", StringComparison.Ordinal)
            ? new Sink(_lines)
            : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
        }

        private sealed class Sink(List<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                lock (lines)
                {
                    if (state is IEnumerable<KeyValuePair<string, object>> values)
                    {
                        lines.Add(string.Join(";", values.Select(x => $"{x.Key}={x.Value}")));
                    }
                    else
                    {
                        lines.Add(state.ToString() ?? string.Empty);
                    }
                }

                return null;
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (lines)
                {
                    lines.Add(formatter(state, exception));
                    if (exception is not null)
                    {
                        lines.Add(exception.ToString());
                    }
                }
            }
        }
    }
}
