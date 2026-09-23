using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
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
    public async Task RoleMatrixProtectsTheSlotList(Role[] roles, HttpStatusCode expected)
    {
        Guid? scope = roles.Any(role => role is Role.Manager or Role.AppointmentStaff)
            ? AppointmentTypeIds.DrugAndAlcoholTesting
            : null;
        factory.SignedInAs = await factory.GivenStaffAsync(roles, scope);

        using var response = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/slots");

        Assert.Equal(expected, response.StatusCode);
    }

    /// <summary>Verifies anonymous and unassigned identities receive no workspace data.</summary>
    [Fact]
    public async Task AnonymousAndUnassignedCallersAreRejected()
    {
        factory.SignedInAs = null;
        using var anonymous = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/slots");
        factory.SignedInAs = Guid.NewGuid();
        using var unassigned = await factory.CreateClient()
            .GetAsync("/api/appointment-workspace/slots");

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
            "/api/appointment-workspace/slots"));
        AssertKeys(list.RootElement, "appointmentTypeName", "slots", "_links");
        var slot = Assert.Single(
            list.RootElement.GetProperty("slots").EnumerateArray(),
            item => item.GetProperty("confirmedSlotId").GetString() == data.SlotId.ToString());
        AssertKeys(slot, "confirmedSlotId", "date", "startTime", "endTime", "counts", "_links");
        AssertKeys(slot.GetProperty("counts"), "expected", "checkedIn", "completed", "noShow");

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "confirmedSlotId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "candidateName", "candidateEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.Equal("Expected", row.GetProperty("status").GetString());
        Assert.DoesNotContain("candidateId", detail.RootElement.GetRawText());
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

    /// <summary>Seeds one active slot, candidate, booking, and scoped appointment row.</summary>

    /// <summary>Verifies the roster route is protected by the same role matrix as the slot list.</summary>
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
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

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
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

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
            "Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At",
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
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(data.AppointmentId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Version", body, StringComparison.Ordinal);
    }

    /// <summary>Verifies a caller scoped to another appointment type cannot learn the slot exists.</summary>
    [Fact]
    public async Task RosterCrossTypeRequestReturnsNotFound()
    {
        var data = await GivenWorkspaceAsync(AppointmentTypeIds.DrugAndAlcoholTesting);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an unknown slot is refused the same way the JSON detail route refuses it.</summary>
    [Fact]
    public async Task RosterMissingSlotReturnsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies an anonymous caller is challenged before any candidate data is read.</summary>
    [Fact]
    public async Task RosterAnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{Guid.NewGuid()}/roster");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Verifies a free-text candidate name with a comma and a quote survives the CSV.</summary>
    [Fact]
    public async Task RosterEscapesCommaAndQuoteInCandidateName()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, "Okafor, Ada \"Bisi\"");
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient()
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

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
            .GetAsync($"/api/appointment-workspace/slots/{inScope.SlotId}/roster");
        using var refused = await client
            .GetAsync($"/api/appointment-workspace/slots/{outOfScope.SlotId}/roster");

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
            .GetAsync($"/api/appointment-workspace/slots/{data.SlotId}/roster");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid SlotId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        string candidateName = "Alex Morgan")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today, new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? EmployeeGroupIds.GroundOperationsAgent
            : EmployeeGroupIds.Pilots;
        var group = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var candidate = Candidate.Create(
            Guid.NewGuid(), candidateName, $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(slot, candidate, booking, appointment);
        await context.SaveChangesAsync();
        return (slot.Id, appointment.Id);
    }

    /// <summary>Asserts a JSON object carries exactly the approved property names.</summary>
    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
