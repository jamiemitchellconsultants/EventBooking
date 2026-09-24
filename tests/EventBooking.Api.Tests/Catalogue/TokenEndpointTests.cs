using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.TestSupport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests.Catalogue;

/// <summary>
/// The four anonymous token routes. Two of them carry the distinction Task 21 settled: a
/// forged token and an expired one are different answers, and everything else is one.
/// </summary>
[Collection("api")]
public sealed class TokenEndpointTests(ApiFactory factory)
    : CatalogueSuite(factory)
{
    [Fact]
    public async Task AnUnknownBookTokenIsFourOhFourTokenInvalid()
    {
        Factory.SignedInAs = null;
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{UnknownToken()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("token-invalid", (await BodyAsync(response)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task AnUnknownManageTokenIsFourOhFourTokenInvalid()
    {
        Factory.SignedInAs = null;
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/manage/{UnknownToken()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("token-invalid", (await BodyAsync(response)).GetProperty("type").GetString());
    }

    /// <summary>
    /// A real link whose invite has lapsed. The token verifies, the row resolves and the
    /// version matches, so this is the one token failure the holder is told about.
    /// </summary>
    [Fact]
    public async Task AnExpiredBookTokenIsFourTenTokenExpired()
    {
        var token = await GivenExpiredInviteTokenAsync();
        Factory.SignedInAs = null;
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{token}");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("token-expired", (await BodyAsync(response)).GetProperty("type").GetString());
    }

    /// <summary>
    /// A superseded invite stays indistinguishable from a forgery: its state is not the
    /// holder's doing, and disclosing it would say something about another link.
    /// </summary>
    [Fact]
    public async Task ASupersededBookTokenStaysIndistinguishableFromAForgery()
    {
        var token = await GivenSupersededInviteTokenAsync();
        Factory.SignedInAs = null;
        var client = Factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{token}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("token-invalid", (await BodyAsync(response)).GetProperty("type").GetString());
    }

    /// <summary>
    /// Design 05's worked example, asserted member by member. A confirmation against an event
    /// whose required type has no place left is the one refusal the design writes out in full.
    /// </summary>
    [Fact]
    public async Task ConfirmingAgainstAFullEventMatchesTheDesignsExampleBody()
    {
        var (token, eventId) = await GivenInviteOnAFullEventAsync();
        Factory.SignedInAs = null;
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{token}/confirm", new { eventId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await BodyAsync(response);
        Assert.Equal("capacity-exhausted", problem.GetProperty("type").GetString());
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.Equal(
            "capacity-exhausted",
            problem.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    /// <summary>
    /// Eleven requests on one token from an isolated address: the eleventh is refused by the
    /// ten-per-minute token policy, and the address window is untouched because the derived
    /// host carries its own. Hammering the shared client here would burn the "unknown"
    /// partition every other anonymous case also uses.
    /// </summary>
    [Fact]
    public async Task TheTokenPolicyRefusesTheEleventhRequestOnOneToken()
    {
        using var host = WithRemoteAddress();
        Factory.SignedInAs = null;
        var client = host.CreateClient();
        var token = UnknownToken();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            last = await SendAsync(client, $"/api/booking/{token}");
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.NotNull(last.Headers.RetryAfter);
    }

    private static string UnknownToken() =>
        "b" + Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(RemoteAddressStartupFilter.Header, "203.0.113.44");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await client.SendAsync(request);
    }

    private WebApplicationFactoryHost WithRemoteAddress() =>
        new(Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter, RemoteAddressStartupFilter>())));

    /// <summary>Owns the derived host so the case disposes its own pipeline.</summary>
    private sealed class WebApplicationFactoryHost(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> inner) : IDisposable
    {
        public HttpClient CreateClient() => inner.CreateClient();

        public void Dispose() => inner.Dispose();
    }

    /// <summary>
    /// Test-only: the test server sets no client address, so the limiter would see every
    /// request as the one unknown partition. This copies the header onto the connection
    /// before anything else in the pipeline runs.
    /// </summary>
    private sealed class RemoteAddressStartupFilter : IStartupFilter
    {
        public const string Header = "X-Test-Remote-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, following) =>
                {
                    if (context.Request.Headers.TryGetValue(Header, out var value) &&
                        System.Net.IPAddress.TryParse(value.ToString(), out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await following(context);
                });
                next(app);
            };
    }

    /// <summary>
    /// An invite whose expiry has passed. The token is issued by the host's own token
    /// service, never assembled here: a token a test builds itself proves only that the test
    /// and the service agree with each other.
    /// </summary>
    /// <returns>The book token.</returns>
    private async Task<string> GivenExpiredInviteTokenAsync()
    {
        var invite = await GivenPendingInviteAsync(
            "TOK_EXP", expiresAt: Seeded.AddDays(-1), options: []);
        return Issue(invite);
    }

    /// <summary>An invite that has been replaced, which is not the holder's doing.</summary>
    /// <returns>The book token.</returns>
    private async Task<string> GivenSupersededInviteTokenAsync()
    {
        var inviteId = await GivenPendingInviteAsync(
            "TOK_SUP", expiresAt: Seeded.AddDays(7), options: []);
        await using var scoped = NewScope();
        var invite = await scoped.Context.Invites.SingleAsync(i => i.Id == inviteId);
        invite.MarkSuperseded();
        await scoped.Context.SaveChangesAsync();
        return Issue(inviteId);
    }

    /// <summary>
    /// An invite offering one event whose required type has no place left, which is the state
    /// design 05's worked example describes.
    /// </summary>
    /// <returns>The book token and the offered event.</returns>
    private async Task<(string Token, Guid EventId)> GivenInviteOnAFullEventAsync()
    {
        var type = await GivenAppointmentTypeAsync("TF1");
        var location = await GivenLocationAsync("TOK_FULL_LOC", "Europe/London");
        var manager = Guid.NewGuid();
        var proposal = EventProposal.Propose(
            Guid.NewGuid(),
            location,
            locationIsActive: true,
            "Europe/London",
            new EventWindow(new DateOnly(2026, 11, 26), new TimeOnly(9, 30), 240),
            ProposalFixture.Zones,
            Seeded.AddDays(-30),
            [new ProposableAppointmentType(type, "TF1", true, true)],
            type,
            manager,
            headcount: 1);
        proposal.Accept(type, manager, 1);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        await using (var seed = NewScope())
        {
            seed.Context.EventProposals.Add(proposal);
            seed.Context.Events.Add(eventItem);
            await seed.Context.SaveChangesAsync();

            // The single place is taken, so the only option this invite offers is full. The
            // capacity row is written directly because taking the place through a booking
            // would need a second attendee and a second invite to no purpose.
            await seed.Context.Database.ExecuteSqlRawAsync(
                "UPDATE event_capacity SET remaining_capacity = 0 WHERE event_id = {0}",
                eventItem.Id);
        }

        var inviteId = await GivenPendingInviteAsync(
            "TOK_FULL", Seeded.AddDays(7), [eventItem.Id], type, location);
        return (Issue(inviteId), eventItem.Id);
    }

    /// <summary>Seeds one pending invite for a fresh attendee and returns its identifier.</summary>
    /// <param name="prefix">A per-case code prefix, so the suite's rows never collide.</param>
    /// <param name="expiresAt">When the invite stops being usable.</param>
    /// <param name="options">The events offered.</param>
    /// <param name="appointmentTypeId">The snapshotted requirement, or null to seed one.</param>
    /// <param name="locationId">The invite's location, or null to seed one.</param>
    /// <returns>The invite identifier.</returns>
    private async Task<Guid> GivenPendingInviteAsync(
        string prefix, DateTimeOffset expiresAt, IReadOnlyList<Guid> options,
        Guid? appointmentTypeId = null, Guid? locationId = null)
    {
        var type = appointmentTypeId ?? await GivenAppointmentTypeAsync(prefix switch
        {
            "TOK_EXP" => "TE1",
            "TOK_SUP" => "TS1",
            _ => throw new InvalidOperationException($"No short type code for {prefix}."),
        });
        var location = locationId ?? await GivenLocationAsync($"{prefix}_LOC");
        var group = await GivenAttendeeGroupAsync($"{prefix}_GRP", type);
        var attendee = await GivenAttendeeAsync(group, $"{prefix.ToLowerInvariant()}@example.com");

        await using var scoped = NewScope();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee, expiresAt, [location], options, [type], retryCount: 0,
            inviteOptionCount: options.Count);
        scoped.Context.Invites.Add(invite);
        await scoped.Context.SaveChangesAsync();
        return invite.Id;
    }

    /// <summary>Issues the link the attendee would have been emailed.</summary>
    /// <param name="inviteId">The invite.</param>
    /// <returns>The book token.</returns>
    private string Issue(Guid inviteId)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITokenService>()
            .Issue(TokenPurpose.Book, inviteId, version: 1);
    }
}
