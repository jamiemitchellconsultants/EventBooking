# 01e — Location-restricted invites and closed attendee transitions, edits 10 (Task 8)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs — 1/1

<!-- retirement-file: {"id":22,"file":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","beforeSha":"97d3eab78b8f80eeb02846d5bb67426c8917ac3a56d85da385e98d5e2b826d35","afterSha":"39866de1de5e66db6632ddd8bac33bbfea7766537847aebc2fa8bd5febae71bc","side":"before","part":1,"parts":1} -->

`````csharp
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

    /// <summary>Verifies list/detail JSON contains exactly the approved minimum-data properties.</summary>
    [Fact]
    public async Task ResponsesHaveTheExactApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = JsonDocument.Parse(await client.GetStringAsync(
            "/api/appointment-workspace/events"));
        AssertKeys(list.RootElement, "appointmentTypeName", "events", "_links");
        var eventItem = Assert.Single(
            list.RootElement.GetProperty("events").EnumerateArray(),
            item => item.GetProperty("eventId").GetString() == data.EventId.ToString());
        AssertKeys(eventItem, "eventId", "date", "startTime", "endTime", "counts", "_links");
        AssertKeys(eventItem.GetProperty("counts"), "expected", "checkedIn", "completed", "noShow");

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.Equal("Expected", row.GetProperty("status").GetString());
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("requirement", detail.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
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
            new { status = "CheckedIn", expectedVersion = 1 });

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
    public async Task MalformedUpdateReturnsBadRequest(string status, long expectedVersion)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { status, expectedVersion });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            new { status = "CheckedIn", expectedVersion = 1 });
        using var missing = await client.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { status = "CheckedIn", expectedVersion = 1 });

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType!.CharSet);

        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        var fileName = (disposition.FileNameStar ?? disposition.FileName)!.Trim('"');
        Assert.StartsWith("roster-drug-&-alcohol-testing-", fileName, StringComparison.Ordinal);
        Assert.EndsWith("-0900.csv", fileName, StringComparison.Ordinal);

        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At",
            lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Alex Morgan,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,Expected,", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",,", lines[1], StringComparison.Ordinal);
    }

    /// <summary>Verifies the roster never carries the write path's command target or version.</summary>
    [Fact]
    public async Task RosterOmitsTheCommandTargetAndConcurrencyToken()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(data.AppointmentId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Version", body, StringComparison.Ordinal);
    }

    /// <summary>Verifies a caller scoped to another appointment type cannot learn the event exists.</summary>
    [Fact]
    public async Task RosterCrossTypeRequestReturnsNotFound()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an unknown eventItem is refused the same way the JSON detail route refuses it.</summary>
    [Fact]
    public async Task RosterMissingEventReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an anonymous caller is challenged before any attendee data is read.</summary>
    [Fact]
    public async Task RosterAnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster");

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Okafor, Ada \"\"Bisi\"\"\",", body, StringComparison.Ordinal);
    }


    /// <summary>Verifies appointment staff download their own scope and cannot reach another's.</summary>
    [Fact]
    public async Task RosterAppointmentStaffIsScopedToItsOwnAppointmentType()
    {
        var inScope = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        var outOfScope = await GivenWorkspaceAsync(AppointmentTypeIds.MedicalCheckUp);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var allowed = await client
            .GetAsync($"/api/appointment-workspace/events/{inScope.EventId}/roster");
        using var refused = await client
            .GetAsync($"/api/appointment-workspace/events/{outOfScope.EventId}/roster");

        allowed.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", allowed.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    /// <summary>Verifies a signed-in identity with no access profile downloads nothing.</summary>
    [Fact]
    public async Task RosterUnassignedProfileIsForbidden()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        string attendeeName = "Alex Morgan")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), attendeeName, $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
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
`````

## after — tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs — 1/1

<!-- retirement-file: {"id":22,"file":"tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs","beforeSha":"97d3eab78b8f80eeb02846d5bb67426c8917ac3a56d85da385e98d5e2b826d35","afterSha":"39866de1de5e66db6632ddd8bac33bbfea7766537847aebc2fa8bd5febae71bc","side":"after","part":1,"parts":1} -->

`````csharp
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

    /// <summary>Verifies list/detail JSON contains exactly the approved minimum-data properties.</summary>
    [Fact]
    public async Task ResponsesHaveTheExactApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = JsonDocument.Parse(await client.GetStringAsync(
            "/api/appointment-workspace/events"));
        AssertKeys(list.RootElement, "appointmentTypeName", "events", "_links");
        var eventItem = Assert.Single(
            list.RootElement.GetProperty("events").EnumerateArray(),
            item => item.GetProperty("eventId").GetString() == data.EventId.ToString());
        AssertKeys(eventItem, "eventId", "date", "startTime", "endTime", "counts", "_links");
        AssertKeys(eventItem.GetProperty("counts"), "expected", "checkedIn", "completed", "noShow");

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.Equal("Expected", row.GetProperty("status").GetString());
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("requirement", detail.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
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
            new { status = "CheckedIn", expectedVersion = 1 });

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
    public async Task MalformedUpdateReturnsBadRequest(string status, long expectedVersion)
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { status, expectedVersion });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
            new { status = "CheckedIn", expectedVersion = 1 });
        using var missing = await client.PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{Guid.NewGuid()}/status",
            new { status = "CheckedIn", expectedVersion = 1 });

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType!.CharSet);

        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        var fileName = (disposition.FileNameStar ?? disposition.FileName)!.Trim('"');
        Assert.StartsWith("roster-drug-&-alcohol-testing-", fileName, StringComparison.Ordinal);
        Assert.EndsWith("-0900.csv", fileName, StringComparison.Ordinal);

        var body = await response.Content.ReadAsStringAsync();
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At",
            lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Alex Morgan,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,Expected,", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",,", lines[1], StringComparison.Ordinal);
    }

    /// <summary>Verifies the roster never carries the write path's command target or version.</summary>
    [Fact]
    public async Task RosterOmitsTheCommandTargetAndConcurrencyToken()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(data.AppointmentId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Version", body, StringComparison.Ordinal);
    }

    /// <summary>Verifies a caller scoped to another appointment type cannot learn the event exists.</summary>
    [Fact]
    public async Task RosterCrossTypeRequestReturnsNotFound()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an unknown eventItem is refused the same way the JSON detail route refuses it.</summary>
    [Fact]
    public async Task RosterMissingEventReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an anonymous caller is challenged before any attendee data is read.</summary>
    [Fact]
    public async Task RosterAnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{Guid.NewGuid()}/roster");

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
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"Okafor, Ada \"\"Bisi\"\"\",", body, StringComparison.Ordinal);
    }


    /// <summary>Verifies appointment staff download their own scope and cannot reach another's.</summary>
    [Fact]
    public async Task RosterAppointmentStaffIsScopedToItsOwnAppointmentType()
    {
        var inScope = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        var outOfScope = await GivenWorkspaceAsync(AppointmentTypeIds.MedicalCheckUp);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var allowed = await client
            .GetAsync($"/api/appointment-workspace/events/{inScope.EventId}/roster");
        using var refused = await client
            .GetAsync($"/api/appointment-workspace/events/{outOfScope.EventId}/roster");

        allowed.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", allowed.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    /// <summary>Verifies a signed-in identity with no access profile downloads nothing.</summary>
    [Fact]
    public async Task RosterUnassignedProfileIsForbidden()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/events/{data.EventId}/roster");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        string attendeeName = "Alex Morgan")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today, new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
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
            $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
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
`````

## before — tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs — 1/1

<!-- retirement-file: {"id":23,"file":"tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs","beforeSha":"7da5e72bf8de51d8f85d1251678e048219b188c2bb784ce2e5b243e99124dfed","afterSha":"e3df48472764f0f8fdcbbf3828987b7c7d305917db686d75ffb3dce4fcd330d8","side":"before","part":1,"parts":1} -->

`````csharp
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

/// <summary>Verifies the staff booking-cancellation and active-booking-listing routes and their auth.</summary>
[Collection("api")]
public sealed class AttendeeBookingCancellationEndpointTests(ApiFactory factory)
{
    private sealed record CancelResponse(
        bool Reinvited, bool InviteCreated, string? DeliveryStatus, Guid? DeliveryId);

    private sealed record BookingRow(
        Guid BookingId, bool IsOriginal, DateOnly EventDate, TimeOnly EventStartTime, TimeOnly EventEndTime);

    [Fact]
    public async Task CoordinatorCanCancelAnOriginalBooking()
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(body!.Reinvited);
        Assert.False(body.InviteCreated);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task CoordinatorCanCancelAndRebookAnOriginalBooking()
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(body!.Reinvited);
        Assert.True(body.InviteCreated);
        Assert.NotNull(body.DeliveryStatus);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task RebookTrueOnARecoveryBookingIsAConflict()
    {
        var (attendeeId, originalId) = await GivenBookedAttendeeAsync();
        var recoveryId = await GivenActiveRecoveryAsync(attendeeId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{recoveryId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(recoveryId));
    }

    [Fact]
    public async Task UnknownBookingIdIsNotFound()
    {
        var (attendeeId, _) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("AppointmentStaff")]
    [InlineData("Admin")]
    public async Task NonCoordinatorRolesAreForbidden(string role)
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = role switch
        {
            "Manager" => await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.UniformFitting),
            "AppointmentStaff" => await factory.GivenStaffAsync(
                Role.AppointmentStaff, AppointmentTypeIds.UniformFitting),
            _ => await factory.GivenStaffAsync(Role.Admin),
        };
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/attendees/{attendeeId}/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task AnUnassignedProfileIsForbidden()
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/attendees/{Guid.NewGuid()}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/attendees/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCanListActiveBookings()
    {
        var (attendeeId, originalId) = await GivenBookedAttendeeAsync();
        var recoveryId = await GivenActiveRecoveryAsync(attendeeId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<BookingRow>>(
            $"/api/attendees/{attendeeId}/bookings");

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.True(rows[0].IsOriginal);
        Assert.Equal(originalId, rows[0].BookingId);
        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recoveryId, rows[1].BookingId);
        Assert.Equal(rows[0].EventStartTime.AddHours(4), rows[0].EventEndTime);
    }

    [Fact]
    public async Task ListingForAnUnknownAttendeeIsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/attendees/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingStatus> StatusOfAsync(Guid bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        return await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => b.Status)
            .SingleAsync();
    }

    /// <summary>Seeds a attendee holding one active original booking plus spare future events.</summary>
    private async Task<(Guid AttendeeId, Guid BookingId)> GivenBookedAttendeeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(30), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(31), start, 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, spareEvents[0].Id, spareEvents[1].Id],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        attendee.MarkInvited();
        invite.MarkUsed();
        attendee.MarkBooked();

        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, invite, booking);
        foreach (var typeId in attendee.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedEvent.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (attendee.Id, booking.Id);
    }

    /// <summary>Seeds one active recovery booking on a later event for an existing original.</summary>
    private async Task<Guid> GivenActiveRecoveryAsync(Guid attendeeId, Guid originalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var recoveryEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(40), new TimeOnly(13, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), attendeeId, originalId, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id,
            $"manage-recovery-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddHours(1));
        recoveryInvite.MarkUsed();

        context.Add(recoveryEvent);
        context.AddRange(recoveryInvite, recovery);
        context.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp));
        recoveryEvent.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        await context.SaveChangesAsync();
        return recovery.Id;
    }
}
`````

## after — tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs — 1/1

<!-- retirement-file: {"id":23,"file":"tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs","beforeSha":"7da5e72bf8de51d8f85d1251678e048219b188c2bb784ce2e5b243e99124dfed","afterSha":"e3df48472764f0f8fdcbbf3828987b7c7d305917db686d75ffb3dce4fcd330d8","side":"after","part":1,"parts":1} -->

`````csharp
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

/// <summary>Verifies the staff booking-cancellation and active-booking-listing routes and their auth.</summary>
[Collection("api")]
public sealed class AttendeeBookingCancellationEndpointTests(ApiFactory factory)
{
    private sealed record CancelResponse(
        bool Reinvited, bool InviteCreated, string? DeliveryStatus, Guid? DeliveryId);

    private sealed record BookingRow(
        Guid BookingId, bool IsOriginal, DateOnly EventDate, TimeOnly EventStartTime, TimeOnly EventEndTime);

    [Fact]
    public async Task CoordinatorCanCancelAnOriginalBooking()
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(body!.Reinvited);
        Assert.False(body.InviteCreated);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task CoordinatorCanCancelAndRebookAnOriginalBooking()
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(body!.Reinvited);
        Assert.True(body.InviteCreated);
        Assert.NotNull(body.DeliveryStatus);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task RebookTrueOnARecoveryBookingIsAConflict()
    {
        var (attendeeId, originalId) = await GivenBookedAttendeeAsync();
        var recoveryId = await GivenActiveRecoveryAsync(attendeeId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{recoveryId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(recoveryId));
    }

    [Fact]
    public async Task UnknownBookingIdIsNotFound()
    {
        var (attendeeId, _) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("AppointmentStaff")]
    [InlineData("Admin")]
    public async Task NonCoordinatorRolesAreForbidden(string role)
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = role switch
        {
            "Manager" => await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.UniformFitting),
            "AppointmentStaff" => await factory.GivenStaffAsync(
                Role.AppointmentStaff, AppointmentTypeIds.UniformFitting),
            _ => await factory.GivenStaffAsync(Role.Admin),
        };
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/attendees/{attendeeId}/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task AnUnassignedProfileIsForbidden()
    {
        var (attendeeId, bookingId) = await GivenBookedAttendeeAsync();
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/attendees/{Guid.NewGuid()}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/attendees/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCanListActiveBookings()
    {
        var (attendeeId, originalId) = await GivenBookedAttendeeAsync();
        var recoveryId = await GivenActiveRecoveryAsync(attendeeId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<BookingRow>>(
            $"/api/attendees/{attendeeId}/bookings");

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.True(rows[0].IsOriginal);
        Assert.Equal(originalId, rows[0].BookingId);
        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recoveryId, rows[1].BookingId);
        Assert.Equal(rows[0].EventStartTime.AddHours(4), rows[0].EventEndTime);
    }

    [Fact]
    public async Task ListingForAnUnknownAttendeeIsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/attendees/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingStatus> StatusOfAsync(Guid bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        return await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => b.Status)
            .SingleAsync();
    }

    /// <summary>Seeds a attendee holding one active original booking plus spare future events.</summary>
    private async Task<(Guid AttendeeId, Guid BookingId)> GivenBookedAttendeeAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(30), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => EventFixture.Create(
                Guid.NewGuid(), new EventWindow(today.AddDays(31), start, 240),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Alex Morgan",
            $"alex-{Guid.NewGuid():N}@example.com",
            group,
            ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [bookedEvent.Id, spareEvents[0].Id, spareEvents[1].Id],
            attendee.RequiredAppointmentTypeIds,
            0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        attendee.MarkInvited(ProposalFixture.Now);
        invite.MarkUsed();
        attendee.MarkBooked(ProposalFixture.Now);

        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, invite, booking);
        foreach (var typeId in attendee.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedEvent.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (attendee.Id, booking.Id);
    }

    /// <summary>Seeds one active recovery booking on a later event for an existing original.</summary>
    private async Task<Guid> GivenActiveRecoveryAsync(Guid attendeeId, Guid originalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;

        var recoveryEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(40), new TimeOnly(13, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            originalId,
            $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id,
            $"manage-recovery-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddHours(1));
        recoveryInvite.MarkUsed();

        context.Add(recoveryEvent);
        context.AddRange(recoveryInvite, recovery);
        context.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp));
        recoveryEvent.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        await context.SaveChangesAsync();
        return recovery.Id;
    }
}
`````
