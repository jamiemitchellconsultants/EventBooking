using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies authorization, shape, validation, and updates at the workspace HTTP boundary.</summary>
[Collection("api")]
public sealed class AppointmentWorkspaceEndpointTests(ApiFactory factory)
{
    /// <summary>Defines the single and combined profiles that may or may not use the workspace.</summary>
    public static TheoryData<Role[], HttpStatusCode> ReadMatrix => new()
    {
        { [Role.Manager], HttpStatusCode.OK },
        { [Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Manager, Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Coordinator, Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Coordinator, Role.Manager, Role.AppointmentStaff], HttpStatusCode.OK },
        { [Role.Coordinator], HttpStatusCode.Forbidden },
        { [Role.Admin], HttpStatusCode.Forbidden },
    };

    /// <summary>Verifies the role union while keeping one trusted appointment-type scope.</summary>
    [Theory]
    [MemberData(nameof(ReadMatrix))]
    public async Task RoleMatrixProtectsTheEventList(Role[] roles, HttpStatusCode expected)
    {
        Guid? scope = roles.Any(role => role is Role.Manager or Role.AppointmentStaff)
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(roles, scope);

        using var response = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/events");

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies anonymous and unassigned identities receive no workspace data.</summary>
    [Fact]
    public async Task AnonymousAndUnassignedCallersAreRejected()
    {
        factory.SignedInAs = null;
        using var anonymous = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/events");
        factory.SignedInAs = Guid.NewGuid();
        using var unassigned = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/events");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
    }

    /// <summary>
    /// Verifies list/detail JSON contains exactly the approved properties. The workspace event
    /// carries the one event-time representation; each roster row carries its stable command
    /// identifier plus the status actions currently available for it, and still no attendee,
    /// booking or requirement identifiers.
    /// </summary>
    [Fact]
    public async Task ResponsesHaveTheExactApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = JsonDocument.Parse(await client.GetStringAsync(
            "/api/appointment-workspace/events"));
        AssertKeys(list.RootElement, "items", "nextCursor");
        var eventItem = Assert.Single(
            list.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("eventId").GetString() == data.EventId.ToString());
        AssertKeys(eventItem, "eventId", "locationId", "locationName", "time", "status", "_links");
        var entryLinks = eventItem.GetProperty("_links");
        Assert.Equal(
            $"/api/appointment-workspace/events/{data.EventId}",
            entryLinks.GetProperty("roster").GetProperty("href").GetString());
        Assert.Equal(
            $"/api/appointment-workspace/events/{data.EventId}/roster.csv",
            entryLinks.GetProperty("rosterCsv").GetProperty("href").GetString());

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement, "items", "nextCursor");
        var row = Assert.Single(detail.RootElement.GetProperty("items").EnumerateArray());
        AssertKeys(row, "appointmentId", "name", "email", "scopeTypeCode",
            "appointmentStatus", "checkedInAt", "version", "_links");
        Assert.Equal("Expected", row.GetProperty("appointmentStatus").GetString());
        Assert.Equal(data.AppointmentId, row.GetProperty("appointmentId").GetGuid());
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("requirement", detail.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkspaceEventsCarryTheCompleteTimeContract()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = JsonDocument.Parse(await client.GetStringAsync(
            "/api/appointment-workspace/events"));
        var eventItem = Assert.Single(
            list.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("eventId").GetString() == data.EventId.ToString());
        var time = eventItem.GetProperty("time");

        foreach (var member in new[]
        {
            "date", "startTime", "durationMinutes", "startLocal", "endLocal",
            "startUtc", "endUtc", "timeZoneId", "zoneAbbreviation",
        })
        {
            Assert.True(time.TryGetProperty(member, out _), member);
        }
    }

    [Fact]
    public async Task AnExpectedRowOffersCheckInAndItsLinksFollowStatusChanges()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var links = (await RosterRowAsync(client, data.EventId)).GetProperty("_links");
        Assert.True(links.TryGetProperty("checkIn", out var checkIn));
        Assert.Equal(
            $"/api/appointment-workspace/appointments/{data.AppointmentId}/status",
            checkIn.GetProperty("href").GetString());
        Assert.False(links.TryGetProperty("complete", out _));
        Assert.False(links.TryGetProperty("reopen", out _));

        using var checkedIn = await client.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{data.AppointmentId}/status",
            new { targetStatus = "CheckedIn", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, checkedIn.StatusCode);

        var after = (await RosterRowAsync(client, data.EventId)).GetProperty("_links");
        Assert.False(after.TryGetProperty("checkIn", out _));
        Assert.True(after.TryGetProperty("complete", out _));
        Assert.True(after.TryGetProperty("reopen", out _));
        Assert.False(after.TryGetProperty("noShow", out _));
    }

    [Fact]
    public async Task AFutureEventOffersNeitherCheckInNorNoShow()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysOffset: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var links = (await RosterRowAsync(client, data.EventId)).GetProperty("_links");

        Assert.False(links.TryGetProperty("checkIn", out _));
        Assert.False(links.TryGetProperty("noShow", out _));
    }

    [Fact]
    public async Task APastEventOffersNoShowButNotCheckIn()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysOffset: -1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var links = (await RosterRowAsync(client, data.EventId)).GetProperty("_links");

        Assert.False(links.TryGetProperty("checkIn", out _));
        Assert.True(links.TryGetProperty("noShow", out _));
    }

    private static async Task<JsonElement> RosterRowAsync(HttpClient client, Guid eventId)
    {
        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{eventId}"));
        var rows = detail.RootElement.GetProperty("items").EnumerateArray().ToArray();
        return Assert.Single(rows).Clone();
    }

    /// <summary>Verifies the update body is parsed, applied, and returned as a named state.</summary>
    [Fact]
    public async Task CheckInReturnsOnlyTheRowLocalState()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{data.AppointmentId}/status",
            new { targetStatus = "CheckedIn", expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        AssertKeys(json.RootElement,
            "bookingAppointmentId", "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.Equal("CheckedIn", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("version").GetInt64());
    }

    /// <summary>Verifies malformed bodies fail with validation and expose no record existence.</summary>
    [Theory]
    [InlineData("Unknown", 1)]
    [InlineData("CheckedIn", 0)]
    public async Task MalformedUpdateReturnsUnprocessableEntity(string targetStatus, long expectedVersion)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { targetStatus, expectedVersion });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>Verifies missing and cross-type identifiers have indistinguishable responses.</summary>
    [Fact]
    public async Task CrossTypeAndMissingAppointmentsShareOneNotFoundResponse()
    {
        var other = await GivenWorkspaceAsync(AppointmentTypeIds.MedicalCheckUp);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var crossType = await client.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{other.AppointmentId}/status",
            new { targetStatus = "CheckedIn", expectedVersion = 1 });
        using var missing = await client.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { targetStatus = "CheckedIn", expectedVersion = 1 });

        Assert.Equal(HttpStatusCode.NotFound, crossType.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var crossProblem = JsonDocument.Parse(await crossType.Content.ReadAsStringAsync());
        using var missingProblem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.Equal(
            missingProblem.RootElement.GetProperty("title").GetString(),
            crossProblem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            missingProblem.RootElement.GetProperty("detail").GetString(),
            crossProblem.RootElement.GetProperty("detail").GetString());
    }

    /// <summary>Seeds one active eventItem, attendee, booking, and scoped appointment row.</summary>

    /// <summary>Verifies the roster route is protected by the same role matrix as the event list.</summary>
    [Theory]
    [MemberData(nameof(ReadMatrix))]
    public async Task RoleMatrixProtectsTheRoster(Role[] roles, HttpStatusCode expected)
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        Guid? scope = roles.Any(role => role is Role.Manager or Role.AppointmentStaff)
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(roles, scope);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster.csv");

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies a scoped caller downloads the roster with CSV headers and a named file.</summary>
    [Fact]
    public async Task RosterReturnsScopedCsvWithHeaders()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster.csv");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType!.CharSet);

        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        var fileName = (disposition.FileNameStar ?? disposition.FileName)!.Trim('"');
        Assert.Equal($"roster-{data.EventId:D}.csv", fileName);

        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Name,Email,Status,CheckedInAt", lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Alex Morgan,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",Expected,", lines[1], StringComparison.Ordinal);
        Assert.Equal(4, lines[1].Split(',').Length);
    }

    /// <summary>Verifies the roster never carries the write path's command target or version.</summary>
    [Fact]
    public async Task RosterOmitsTheCommandTargetAndConcurrencyToken()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster.csv");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(data.AppointmentId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Version", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a caller whose type the event does not list is refused outright. The old
    /// surface answered 404 here; the roster handlers distinguish "not your type" (403)
    /// from "none of your type booked" (200, headers only) so an empty roster stays
    /// consistent with the event list, which shows every event listing the caller's type.
    /// </summary>
    [Fact]
    public async Task RosterForAnEventOutsideTheCallersScopeIsForbidden()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting,
            listedTypes: [AppointmentTypeIds.DrugAndAlcoholTesting]);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster.csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a listed type with nothing booked downloads headers only.</summary>
    [Fact]
    public async Task RosterForAListedTypeWithoutAppointmentsDownloadsHeadersOnly()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster.csv");

        response.EnsureSuccessStatusCode();
        var lines = (await response.Content.ReadAsStringAsync())
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Name,Email,Status,CheckedInAt"], lines);
    }

    /// <summary>Verifies an unknown eventItem is refused the same way the JSON detail route refuses it.</summary>
    [Fact]
    public async Task RosterMissingEventReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster.csv");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an anonymous caller is challenged before any attendee data is read.</summary>
    [Fact]
    public async Task RosterAnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster.csv");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies a free-text attendee name with a comma and a quote survives the CSV.</summary>
    [Fact]
    public async Task RosterEscapesCommaAndQuoteInAttendeeName()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, "Okafor, Ada \"Bisi\"");
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster.csv");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Okafor, Ada \"\"Bisi\"\"\",", body, StringComparison.Ordinal);
    }


    /// <summary>Verifies appointment staff download their own scope and cannot reach another's.</summary>
    [Fact]
    public async Task RosterAppointmentStaffIsScopedToItsOwnAppointmentType()
    {
        var inScope = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        var outOfScope = await GivenWorkspaceAsync(
            AppointmentTypeIds.MedicalCheckUp,
            listedTypes: [AppointmentTypeIds.MedicalCheckUp]);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var allowed = await client
            .GetAsync($"/api/appointment-workspace/events/{inScope.EventId}/roster.csv");
        using var refused = await client
            .GetAsync($"/api/appointment-workspace/events/{outOfScope.EventId}/roster.csv");

        allowed.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", allowed.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    /// <summary>Verifies a signed-in identity with no access profile downloads nothing.</summary>
    [Fact]
    public async Task RosterUnassignedProfileIsForbidden()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster.csv");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        string attendeeName = "Alex Morgan",
        IEnumerable<Guid>? listedTypes = null,
        int daysOffset = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var listed = listedTypes ?? AppointmentTypeIds.All;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(daysOffset), new TimeOnly(9, 0), 240),
            listed.ToDictionary(id => id, _ => 20), listed);
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            attendeeName,
            $"alex-{Guid.NewGuid():N}@example.com",
            group,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(eventItem, attendee, booking, appointment);
        await context.SaveChangesAsync();
        return (eventItem.Id, appointment.Id);
    }

    /// <summary>Asserts a JSON object carries exactly the approved property names.</summary>
    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
