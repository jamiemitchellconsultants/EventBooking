using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests.Catalogue;

/// <summary>
/// Seeding every catalogue suite shares. Each helper returns identifiers rather than entities:
/// a test that holds an aggregate is a test that can assert against the write model instead of
/// the wire, which is the one thing these suites must not do.
/// </summary>
public abstract class CatalogueSuite(ApiFactory factory)
{
    /// <summary>The instant every seeded row is stamped with.</summary>
    protected static readonly DateTimeOffset Seeded = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the shared factory. Suites use this rather than their own parameter.</summary>
    protected ApiFactory Factory => factory;

    /// <summary>Gives the tests a signed-in Admin and returns a client.</summary>
    /// <param name="staffId">The staff number claim.</param>
    /// <returns>The client.</returns>
    protected async Task<HttpClient> AdminAsync(string staffId = "U700001")
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        factory.StaffIdClaim = staffId;
        return factory.CreateClient();
    }

    /// <summary>Gives the tests a signed-in Coordinator and returns a client.</summary>
    /// <param name="staffId">The staff number claim.</param>
    /// <returns>The client.</returns>
    protected async Task<HttpClient> CoordinatorAsync(string staffId = "U700002")
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        factory.StaffIdClaim = staffId;
        return factory.CreateClient();
    }

    /// <summary>Gives the tests a Manager scoped to one type and returns a client.</summary>
    /// <param name="appointmentTypeId">The scope.</param>
    /// <param name="staffId">The staff number claim.</param>
    /// <returns>The client.</returns>
    protected async Task<HttpClient> ManagerAsync(Guid appointmentTypeId, string staffId = "U700003")
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Manager], appointmentTypeId);
        factory.StaffIdClaim = staffId;
        return factory.CreateClient();
    }

    /// <summary>Seeds an active appointment type and returns its identifier.</summary>
    /// <param name="code">The canonical code.</param>
    /// <returns>The identifier.</returns>
    protected async Task<Guid> GivenAppointmentTypeAsync(string code)
    {
        await using var scoped = NewScope();
        var type = AppointmentType.Create(Guid.NewGuid(), code, code);
        scoped.Context.AppointmentTypes.Add(type);
        await scoped.Context.SaveChangesAsync();
        return type.Id;
    }

    /// <summary>Seeds an active location and returns its identifier.</summary>
    /// <param name="code">The canonical code.</param>
    /// <param name="timeZoneId">The IANA zone.</param>
    /// <returns>The identifier.</returns>
    protected async Task<Guid> GivenLocationAsync(string code, string timeZoneId = "Europe/London")
    {
        await using var scoped = NewScope();
        var location = Location.Create(
            Guid.NewGuid(), code, code, "1 Test Street", timeZoneId, ProposalFixture.Zones);
        scoped.Context.Locations.Add(location);
        await scoped.Context.SaveChangesAsync();
        return location.Id;
    }

    /// <summary>Seeds an active group mapping one type, and returns its identifier.</summary>
    /// <param name="code">The canonical code.</param>
    /// <param name="appointmentTypeId">The type its members require.</param>
    /// <returns>The identifier.</returns>
    protected async Task<Guid> GivenAttendeeGroupAsync(string code, Guid appointmentTypeId)
    {
        await using var scoped = NewScope();
        var group = AttendeeGroup.Create(
            Guid.NewGuid(), code, code, [appointmentTypeId], [appointmentTypeId]);
        scoped.Context.AttendeeGroups.Add(group);
        await scoped.Context.SaveChangesAsync();
        return group.Id;
    }

    /// <summary>Seeds one attendee in a group and returns its identifier.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="email">The unique email.</param>
    /// <returns>The identifier.</returns>
    protected async Task<Guid> GivenAttendeeAsync(Guid groupId, string email)
    {
        await using var scoped = NewScope();
        var group = await scoped.Context.AttendeeGroups
            .Include(g => g.Requirements)
            .SingleAsync(g => g.Id == groupId);
        var attendee = Attendee.Create(Guid.NewGuid(), "Test Attendee", email, group, Seeded);
        scoped.Context.Attendees.Add(attendee);
        await scoped.Context.SaveChangesAsync();
        return attendee.Id;
    }

    /// <summary>Reads a JSON response body as an element, whatever its status.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The body.</returns>
    protected static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Sends a request carrying an idempotency key, for the create endpoints.</summary>
    /// <param name="client">The client.</param>
    /// <param name="url">The route.</param>
    /// <param name="body">The request body.</param>
    /// <returns>The response.</returns>
    protected static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await client.SendAsync(request);
    }

    /// <summary>
    /// A context on the factory's own database, owning the scope it came from. Resolving a
    /// scoped context and letting the scope fall out of use disposes the context underneath
    /// the caller, so the two are disposed together here.
    /// </summary>
    /// <returns>The scoped context.</returns>
    protected ScopedContext NewScope() => new(factory.Services.CreateScope());

    /// <summary>A scoped database context that disposes its scope with it.</summary>
    /// <param name="scope">The scope to own.</param>
    protected sealed class ScopedContext(IServiceScope scope) : IAsyncDisposable
    {
        /// <summary>Gets the context.</summary>
        public EventBookingDbContext Context { get; } =
            scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            scope.Dispose();
        }
    }
}
