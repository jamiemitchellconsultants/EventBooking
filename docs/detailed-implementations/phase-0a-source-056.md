# 00a — Port source 56 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs","encoding":"utf8","sha256":"091015b7821a42cc2006249956fd508fda1cee5fa47d9bde690c36bbe4708ec5","parts":1,"part":1} -->

`````csharp
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

/// <summary>Verifies the staff booking-cancellation and active-booking-listing routes and their auth.</summary>
[Collection("api")]
public sealed class CandidateBookingCancellationEndpointTests(ApiFactory factory)
{
    private sealed record CancelResponse(
        bool Reinvited, bool InviteCreated, string? DeliveryStatus, Guid? DeliveryId);

    private sealed record BookingRow(
        Guid BookingId, bool IsOriginal, DateOnly SlotDate, TimeOnly SlotStartTime, TimeOnly SlotEndTime);

    [Fact]
    public async Task CoordinatorCanCancelAnOriginalBooking()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(body!.Reinvited);
        Assert.False(body.InviteCreated);
        Assert.Equal(BookingStatus.Cancelled, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task CoordinatorCanCancelAndRebookAnOriginalBooking()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = true });

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
        var (candidateId, originalId) = await GivenBookedCandidateAsync();
        var recoveryId = await GivenActiveRecoveryAsync(candidateId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{recoveryId}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("conflict", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(recoveryId));
    }

    [Fact]
    public async Task UnknownBookingIdIsNotFound()
    {
        var (candidateId, _) = await GivenBookedCandidateAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("AppointmentStaff")]
    [InlineData("Admin")]
    public async Task NonCoordinatorRolesAreForbidden(string role)
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = role switch
        {
            "Manager" => await factory.GivenStaffAsync(Role.Manager, AppointmentTypeIds.UniformFitting),
            "AppointmentStaff" => await factory.GivenStaffAsync(
                Role.AppointmentStaff, AppointmentTypeIds.UniformFitting),
            _ => await factory.GivenStaffAsync(Role.Admin),
        };
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/candidates/{candidateId}/bookings");

        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(BookingStatus.Active, await StatusOfAsync(bookingId));
    }

    [Fact]
    public async Task AnUnassignedProfileIsForbidden()
    {
        var (candidateId, bookingId) = await GivenBookedCandidateAsync();
        factory.SignedInAs = Guid.NewGuid();
        factory.RolesClaim = [];
        var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{candidateId}/bookings/{bookingId}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousCallerIsUnauthorized()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        using var cancel = await client.PostAsJsonAsync(
            $"/api/candidates/{Guid.NewGuid()}/bookings/{Guid.NewGuid()}/cancel", new { Rebook = false });
        using var list = await client.GetAsync($"/api/candidates/{Guid.NewGuid()}/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task CoordinatorCanListActiveBookings()
    {
        var (candidateId, originalId) = await GivenBookedCandidateAsync();
        var recoveryId = await GivenActiveRecoveryAsync(candidateId, originalId);
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var rows = await client.GetFromJsonAsync<List<BookingRow>>(
            $"/api/candidates/{candidateId}/bookings");

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.True(rows[0].IsOriginal);
        Assert.Equal(originalId, rows[0].BookingId);
        Assert.False(rows[1].IsOriginal);
        Assert.Equal(recoveryId, rows[1].BookingId);
        Assert.Equal(rows[0].SlotStartTime.AddHours(4), rows[0].SlotEndTime);
    }

    [Fact]
    public async Task ListingForAnUnknownCandidateIsNotFound()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/candidates/{Guid.NewGuid()}/bookings");

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

    /// <summary>Seeds a candidate holding one active original booking plus spare future slots.</summary>
    private async Task<(Guid CandidateId, Guid BookingId)> GivenBookedCandidateAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;

        var bookedSlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(30), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareSlots = new[] { new TimeOnly(11, 0), new TimeOnly(13, 0), new TimeOnly(15, 0) }
            .Select(start => ConfirmedSlot.CreateImported(
                Guid.NewGuid(), new SlotWindow(today.AddDays(31), start),
                AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
            .ToList();

        var group = context.EmployeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedSlot.Id, spareSlots[0].Id, spareSlots[1].Id],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedSlot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        candidate.MarkInvited();
        invite.MarkUsed();
        candidate.MarkBooked();

        context.AddRange(bookedSlot);
        context.AddRange(spareSlots);
        context.AddRange(candidate, invite, booking);
        foreach (var typeId in candidate.RequiredAppointmentTypeIds)
        {
            context.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
            bookedSlot.CapacityFor(typeId).Decrement();
        }

        await context.SaveChangesAsync();
        return (candidate.Id, booking.Id);
    }

    /// <summary>Seeds one active recovery booking on a later slot for an existing original.</summary>
    private async Task<Guid> GivenActiveRecoveryAsync(Guid candidateId, Guid originalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;

        var recoverySlot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(40), new TimeOnly(13, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var original = await context.Bookings.SingleAsync(b => b.Id == originalId);
        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, originalId, $"recovery-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(2),
            [recoverySlot.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoverySlot.Id,
            $"manage-recovery-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddHours(1));
        recoveryInvite.MarkUsed();

        context.Add(recoverySlot);
        context.AddRange(recoveryInvite, recovery);
        context.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.MedicalCheckUp));
        recoverySlot.CapacityFor(AppointmentTypeIds.MedicalCheckUp).Decrement();

        await context.SaveChangesAsync();
        return recovery.Id;
    }
}
`````

## tests/EventBooking.Api.Tests/CandidateEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/CandidateEndpointTests.cs","encoding":"utf8","sha256":"19f7553f6b3cf7b22d5a2b246d9881ddb688c398acbf7496c665c6c590012158","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.AspNetCore.Mvc;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class CandidateEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ACoordinatorCanCreateAndListCandidates()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var created = await client.PostAsJsonAsync(
            "/api/candidates",
            new
            {
                Name = "Amara Novak",
                Email = email,
                EmployeeGroupId = EmployeeGroupIds.Pilots,
            });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var listed = await client.GetFromJsonAsync<List<CandidateResponse>>($"/api/candidates?search={email}");
        Assert.NotNull(listed);
        Assert.Single(listed!);
        Assert.Equal("Not yet invited", listed![0].StatusDisplay);
    }

    [Fact]
    public async Task AManagerIsForbiddenFromTheCandidateRoutes()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/candidates");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARejectedImportComesBackWithItsRowErrors()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var csv = "name,email,employee_group\nAmara Novak,a.novak@mail.com,XYZ";
        var response = await client.PostAsync(
            "/api/candidates/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.False(outcome!.Accepted);
        Assert.Single(outcome.Errors);
        Assert.Equal(2, outcome.Errors[0].LineNumber);
    }

    [Fact]
    public async Task AnAdminCanReadAndChangeTheSettings()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var updated = await client.PutAsJsonAsync(
            "/api/admin/settings", new { InviteExpiryDays = 7, MaxAutoRetryCount = 1 });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);

        var settings = await client.GetFromJsonAsync<SettingsResponse>("/api/admin/settings");
        Assert.Equal(7, settings!.InviteExpiryDays);
        Assert.Equal(3, settings.AppointmentTypes.Count);
    }

    [Fact]
    public async Task ACoordinatorCannotChangeTheSettings()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/admin/settings", new { InviteExpiryDays = 7, MaxAutoRetryCount = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnAdminCanImportConfirmedSlots()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd");
        var csv = $"date,startTime,DAT,MED,UNI\n{future},09:00,10,6,8";
        var response = await client.PostAsync(
            "/api/confirmed-slots/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<SlotImportResponse>();
        Assert.True(outcome!.Accepted);
        Assert.Equal(1, outcome.ImportedCount);
    }

    [Fact]
    public async Task ACoordinatorCanImportConfirmedSlots()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd");
        var csv = $"date,startTime,DAT,MED,UNI\n{future},09:00,10,6,8";
        var response = await client.PostAsync(
            "/api/confirmed-slots/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<SlotImportResponse>();
        Assert.True(outcome!.Accepted);
        Assert.Equal(1, outcome.ImportedCount);
    }

    [Fact]
    public async Task AManagerCannotImportConfirmedSlots()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd");
        var csv = $"date,startTime,DAT,MED,UNI\n{future},09:00,10,6,8";
        var response = await client.PostAsync(
            "/api/confirmed-slots/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MissingOrNullGroupsAreValidationErrorsOnCreateAndUpdate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var missingOnCreate = await client.PostAsJsonAsync(
            "/api/candidates", new { Name = "Amara Novak", Email = email });
        var nullOnCreate = await client.PostAsJsonAsync(
            "/api/candidates",
            new { Name = "Amara Novak", Email = email, EmployeeGroupId = (Guid?)null });

        var created = await client.PostAsJsonAsync(
            "/api/candidates",
            new
            {
                Name = "Amara Novak",
                Email = email,
                EmployeeGroupId = EmployeeGroupIds.Pilots,
            });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        var missingOnUpdate = await client.PutAsJsonAsync(
            $"/api/candidates/{candidateId}", new { Name = "Amara Updated", Email = email });
        var nullOnUpdate = await client.PutAsJsonAsync(
            $"/api/candidates/{candidateId}",
            new { Name = "Amara Updated", Email = email, EmployeeGroupId = (Guid?)null });

        Assert.Equal(HttpStatusCode.BadRequest, missingOnCreate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nullOnCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingOnUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nullOnUpdate.StatusCode);
    }

    [Fact]
    public async Task RequirementOnlyJsonIsAnEmployeeGroupErrorAndPersistsNothing()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var response = await client.PostAsJsonAsync(
            "/api/candidates",
            new
            {
                Name = "Amara Novak",
                Email = email,
                AppointmentTypeIds = new[] { AppointmentTypeIds.DrugAndAlcoholTesting },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("employee_group_required", problem!.Title);

        var listed = await client.GetFromJsonAsync<List<CandidateResponse>>($"/api/candidates?search={email}");
        Assert.Empty(listed!);
    }

    [Fact]
    public async Task ACoordinatorCanListEmployeeGroups()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var groups = await client.GetFromJsonAsync<List<EmployeeGroupResponse>>("/api/employee-groups");

        Assert.NotNull(groups);
        Assert.Equal(5, groups!.Count);
        Assert.Contains(groups, group => group.Code == "PILOTS");
    }

    [Fact]
    public async Task AManagerIsForbiddenFromTheEmployeeGroupRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employee-groups");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ACoordinatorCanUpdateACandidate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var created = await client.PostAsJsonAsync(
            "/api/candidates",
            new
            {
                Name = "Amara Novak",
                Email = email,
                EmployeeGroupId = EmployeeGroupIds.Pilots,
            });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        var updated = await client.PutAsJsonAsync(
            $"/api/candidates/{candidateId}",
            new
            {
                Name = "Amara Smith",
                Email = email,
                EmployeeGroupId = EmployeeGroupIds.Engineering,
            });

        var listed = await client.GetFromJsonAsync<List<CandidateResponse>>($"/api/candidates?search={email}");
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        var candidate = Assert.Single(listed!);
        Assert.Equal("Amara Smith", candidate.Name);
    }

    [Fact]
    public async Task ImportsRequireCsvContentTypeAndAnAtMostOneMiBBody()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        const string csv = "name,email,employee_group\nAmara Novak,a.novak@mail.com,PILOTS";

        var wrongContentType = await client.PostAsync(
            "/api/candidates/import", new StringContent(csv, Encoding.UTF8, "text/plain"));
        var tooLarge = await client.PostAsync(
            "/api/candidates/import",
            new StringContent(new string('x', 1_048_577), Encoding.UTF8, "text/csv"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongContentType.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
    }

    [Fact]
    public async Task ImportsWithMoreThanTenThousandDataRowsAreValidationErrors()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var rows = string.Join(
            '\n',
            Enumerable.Repeat("Candidate,duplicate@mail.com,PILOTS", 10_001));
        var csv = $"name,email,employee_group\n{rows}";

        var response = await client.PostAsync(
            "/api/candidates/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssigningAManagerAlsoUpdatesTheAppointmentTypeManager()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();
        var managerUserId = Guid.NewGuid();
        await factory.GivenStaffWithIdAsync(managerUserId, [Role.Manager], null);
        factory.RolesClaim = ["Admin"];

        var assigned = await PutStaffAccessAsync(
            client, managerUserId, AppointmentTypeIds.UniformFitting, 1);

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);

        var settings = await client.GetFromJsonAsync<SettingsResponse>("/api/admin/settings");
        var type = Assert.Single(settings!.AppointmentTypes, t => t.Id == AppointmentTypeIds.UniformFitting);
        Assert.Equal(managerUserId, type.ManagerUserId);
    }

    [Fact]
    public async Task ReassigningAManagerMovesThroughAReplacement()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();
        var managerUserId = Guid.NewGuid();
        var replacementUserId = Guid.NewGuid();
        await factory.GivenStaffWithIdAsync(managerUserId, [Role.Manager], null);
        await factory.GivenStaffWithIdAsync(replacementUserId, [Role.Manager], null);
        factory.RolesClaim = ["Admin"];

        await PutStaffAccessAsync(
            client, managerUserId, AppointmentTypeIds.UniformFitting, 1);
        await PutStaffAccessAsync(
            client, replacementUserId, AppointmentTypeIds.UniformFitting, 1);
        var moved = await PutStaffAccessAsync(
            client, managerUserId, AppointmentTypeIds.MedicalCheckUp, 3);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var settings = await client.GetFromJsonAsync<SettingsResponse>("/api/admin/settings");
        Assert.Equal(replacementUserId, Type(settings!, AppointmentTypeIds.UniformFitting).ManagerUserId);
        Assert.Equal(managerUserId, Type(settings!, AppointmentTypeIds.MedicalCheckUp).ManagerUserId);
    }

    [Fact]
    public async Task ReplacingAManagerClearsTheFormerManagersScopeButKeepsTheirRole()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();
        var formerManagerUserId = Guid.NewGuid();
        var replacementManagerUserId = Guid.NewGuid();
        await factory.GivenStaffWithIdAsync(formerManagerUserId, [Role.Manager], null);
        await factory.GivenStaffWithIdAsync(replacementManagerUserId, [Role.Manager], null);
        factory.RolesClaim = ["Admin"];

        await PutStaffAccessAsync(
            client, formerManagerUserId, AppointmentTypeIds.UniformFitting, 1);
        await PutStaffAccessAsync(
            client, replacementManagerUserId, AppointmentTypeIds.UniformFitting, 1);

        var settings = await client.GetFromJsonAsync<SettingsResponse>("/api/admin/settings");
        Assert.Equal(replacementManagerUserId, Type(settings!, AppointmentTypeIds.UniformFitting).ManagerUserId);

        factory.SignedInAs = formerManagerUserId;
        factory.RolesClaim = ["Manager"];
        var formerManagerBoard = await client.GetAsync("/api/slots/board");
        Assert.Equal(HttpStatusCode.Forbidden, formerManagerBoard.StatusCode);
    }

    [Fact]
    public async Task DemotingAManagerKeepsAReplacementAndClearsTheirScope()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();
        var managerUserId = Guid.NewGuid();
        var replacementUserId = Guid.NewGuid();
        await factory.GivenStaffWithIdAsync(managerUserId, [Role.Manager], null);
        await factory.GivenStaffWithIdAsync(replacementUserId, [Role.Manager], null);
        factory.RolesClaim = ["Admin"];

        await PutStaffAccessAsync(
            client, managerUserId, AppointmentTypeIds.UniformFitting, 1);
        await PutStaffAccessAsync(
            client, replacementUserId, AppointmentTypeIds.UniformFitting, 1);

        // Displacement already cleared the former manager's scope, so the
        // replacement is the only manager of the type.
        var settings = await client.GetFromJsonAsync<SettingsResponse>("/api/admin/settings");
        Assert.Equal(replacementUserId, Type(settings!, AppointmentTypeIds.UniformFitting).ManagerUserId);
    }

    [Fact]
    public async Task ACoordinatorCanTriggerAnInviteAndConfirmItsCascadeDeletion()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        await GivenEligibleSlotsAsync();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var created = await client.PostAsJsonAsync(
            "/api/candidates",
            new
            {
                Name = "Amara Novak",
                Email = email,
                EmployeeGroupId = EmployeeGroupIds.Pilots,
            });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        var invited = await client.PostAsync($"/api/candidates/{candidateId}/invite", null);
        var unconfirmedDeletion = await client.DeleteAsync($"/api/candidates/{candidateId}");
        var confirmedDeletion = await client.DeleteAsync($"/api/candidates/{candidateId}?confirm=true");

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmedDeletion.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, confirmedDeletion.StatusCode);
    }

    /// <summary>Staff retry derives the failed invite template and context on the server.</summary>
    [Fact]
    public async Task ACoordinatorCanRetryAFailedInviteWithAFreshHashedToken()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var (candidateId, oldHash) = await GivenFailedInviteAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/candidates/{candidateId}/email-retry", null);
        var outcome = await response.Content.ReadFromJsonAsync<RetryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sent", outcome!.DeliveryStatus);
        var message = Assert.Single(
            factory.EmailTransport.Sent,
            sent => sent.CandidateId == candidateId && sent.Template == EmailTemplate.CandidateInvite);
        Assert.Contains("/book/", message.TextBody);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var invite = await context.Invites.SingleAsync(item => item.CandidateId == candidateId);
        Assert.NotEqual(oldHash, invite.TokenHash);
        Assert.DoesNotContain(invite.TokenHash, message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyAnAdminCanWriteStaffAccessAndUnknownRolesAreBadRequests()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var coordinatorResponse = await PutStaffAccessAsync(
            client, Guid.NewGuid(), null, 1);

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var malformedResponse = await client.PutAsJsonAsync(
            $"/api/admin/staff-access/{Guid.NewGuid()}",
            new { Roles = new[] { "SuperUser" }, AppointmentTypeId = (Guid?)null, ExpectedVersion = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, coordinatorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
    }

    private sealed record CandidateResponse(Guid CandidateId, string Name, string StatusDisplay);

    private sealed record EmployeeGroupResponse(
        Guid EmployeeGroupId, string Code, string Name);

    private sealed record RetryResponse(string DeliveryStatus, Guid DeliveryId);

    private sealed record ImportError(int LineNumber, string Message);

    private sealed record ImportResponse(bool Accepted, int ImportedCount, IReadOnlyList<ImportError> Errors);

    private sealed record SlotImportError(int LineNumber, string Message);

    private sealed record SlotImportResponse(bool Accepted, int ImportedCount, IReadOnlyList<SlotImportError> Errors);

    private sealed record AppointmentTypeResponse(Guid Id, string Code, string Name, Guid? ManagerUserId);

    private sealed record SettingsResponse(
        int InviteExpiryDays, int MaxAutoRetryCount, IReadOnlyList<AppointmentTypeResponse> AppointmentTypes);

    private static AppointmentTypeResponse Type(SettingsResponse settings, Guid appointmentTypeId) =>
        Assert.Single(settings.AppointmentTypes, type => type.Id == appointmentTypeId);

    private static Task<HttpResponseMessage> PutStaffAccessAsync(
        HttpClient client,
        Guid staffUserId,
        Guid? appointmentTypeId,
        long expectedVersion) =>
        client.PutAsJsonAsync(
            $"/api/admin/staff-access/{staffUserId}",
            new { AppointmentTypeId = appointmentTypeId, ExpectedVersion = expectedVersion });

    private async Task GivenEligibleSlotsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var date = new DateOnly(2030, 1, 14);

        for (var index = 0; index < 3; index++)
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(date.AddDays(index), new TimeOnly(9, 0)), Guid.NewGuid());
            foreach (var appointmentTypeId in AppointmentTypeIds.All)
            {
                proposal.Accept(appointmentTypeId, Guid.NewGuid(), 8);
            }

            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal));
        }

        await context.SaveChangesAsync();
    }

    private async Task<(Guid CandidateId, string OldHash)> GivenFailedInviteAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var slotIds = new List<Guid>();

        foreach (var day in new[] { 14, 15, 16 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(new DateOnly(2030, 1, day), new TimeOnly(9, 0)),
                Guid.NewGuid());
            foreach (var appointmentTypeId in AppointmentTypeIds.All)
            {
                proposal.Accept(appointmentTypeId, Guid.NewGuid(), 8);
            }

            var slotId = Guid.NewGuid();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(slotId, proposal));
            slotIds.Add(slotId);
        }

        var pilots = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(),
            "Retry Candidate",
            $"{Guid.NewGuid():N}@mail.com",
            pilots);
        candidate.MarkInvited();
        context.Candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            candidate.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            slotIds,
            candidate.RequiredAppointmentTypeIds,
            0);
        context.Invites.Add(invite);

        var delivery = EmailLog.RecordPending(
            Guid.NewGuid(),
            candidate.Id,
            EmailTemplate.CandidateInvite,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            inviteId: invite.Id);
        delivery.MarkFailed(new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.Zero));
        context.EmailLogs.Add(delivery);
        await context.SaveChangesAsync();

        return (candidate.Id, issued.TokenHash);
    }
}
`````

## tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs","encoding":"utf8","sha256":"2f1776e9889c6479c89c2e8d5bfec96df34961a141fc6cbf757513bbf2d5661e","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class CandidateHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task CandidateArrayStaysArrayAndItemsCarryActions()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates/", new
        {
            name = "Hyper Media",
            email = $"hyper-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.Pilots,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/candidates/"));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        var candidate = document.RootElement.EnumerateArray()
            .Single(x => x.GetProperty("candidateId").GetGuid() == id);
        var links = candidate.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/candidates/{id}/bookings", "GET", "listCandidateBookings");
        AssertLink(links, "readiness", $"/api/candidates/{id}/readiness", "GET", "getCandidateReadiness");
        AssertLink(links, "audit", $"/api/audit/candidate/{id}", "GET", "getCandidateAuditHistory");
        AssertLink(links, "update", $"/api/candidates/{id}", "PUT", "updateCandidate");
        AssertLink(links, "delete", $"/api/candidates/{id}", "DELETE", "deleteCandidate");
        AssertLink(links, "invite", $"/api/candidates/{id}/invite", "POST", "triggerCandidateInvite");
    }

    [Fact]
    public async Task GuidAndNoContentContractsRemainUnchanged()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/candidates/", new
        {
            name = "Compatibility",
            email = $"compat-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var text = await created.Content.ReadAsStringAsync();
        Assert.True(Guid.TryParse(JsonSerializer.Deserialize<string>(text) ?? text.Trim('"'), out var id));
        using var updated = await client.PutAsJsonAsync($"/api/candidates/{id}", new
        {
            name = "Compatibility Two",
            email = $"compat2-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal(string.Empty, await updated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessLinksBackToCandidateWorkflows()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates/", new
        {
            name = "Ready Links",
            email = $"ready-{Guid.NewGuid():N}@example.com",
            employeeGroupId = EmployeeGroupIds.Engineering,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/candidates/{id}/readiness"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/candidates/{id}/bookings", "GET", "listCandidateBookings");
        AssertLink(links, "readiness", $"/api/candidates/{id}/readiness", "GET", "getCandidateReadiness");
        Assert.False(links.TryGetProperty("startRecovery", out _));
    }

    private static void AssertLink(JsonElement links, string relation, string href, string method, string operationId)
    {
        var link = links.GetProperty(relation);
        Assert.Equal(href, link.GetProperty("href").GetString());
        Assert.Equal(method, link.GetProperty("method").GetString());
        Assert.Equal(operationId, link.GetProperty("operationId").GetString());
    }
}
`````

## tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs","encoding":"utf8","sha256":"74d6857d8c39f9784b840c7d8e0a97d1982a51d602b61e99bd9a717f78060df2","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.EmployeeGroups;

namespace EventBooking.Api.Tests;

/// <summary>Verifies readiness is a Coordinator-only EventBooking response.</summary>
[Collection("api")]
public sealed class CandidateReadinessEndpointTests(ApiFactory factory)
{
    private sealed record OutstandingResponse(string Code, string Name, bool IsRecoverable);

    private sealed record ReadinessResponse(
        Guid CandidateId,
        string Code,
        string Display,
        IReadOnlyList<OutstandingResponse> OutstandingAppointmentTypes);

    /// <summary>A Coordinator receives the stable no-booking reason for a grouped Candidate.</summary>
    [Fact]
    public async Task CoordinatorCanReadNoActiveBookingReason()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/candidates", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            EmployeeGroupId = EmployeeGroupIds.CabinCrew,
        });
        var candidateId = await created.Content.ReadFromJsonAsync<Guid>();

        var response = await client.GetAsync($"/api/candidates/{candidateId}/readiness");
        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("NoActiveBooking", body!.Code);
        Assert.Empty(body.OutstandingAppointmentTypes);
    }

    /// <summary>An Admin is forbidden from receiving any Candidate readiness payload.</summary>
    [Fact]
    public async Task AdminIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        var response = await factory.CreateClient()
            .GetAsync($"/api/candidates/{Guid.NewGuid()}/readiness");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
`````

## tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","encoding":"utf8","sha256":"aed53346e9eb5b9487fc5134121bdbca16d1bcb938b2d6fbc1c07fea53437b3d","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A candidate can confirm one of the offered slots.</summary>
    [Fact]
    public async Task ACandidateCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = chosen.ConfirmedSlotId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>The confirmation names the API's configured head office, the one the email uses.</summary>
    [Fact]
    public async Task TheConfirmationCarriesTheConfiguredHeadOfficeAddress()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = view!.Options[0].ConfirmedSlotId });

        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        var configured = factory.Services.GetRequiredService<CandidatePortalOptions>().HeadOfficeAddress;
        Assert.False(string.IsNullOrWhiteSpace(configured));
        Assert.Equal(configured, outcome!.HeadOfficeAddress);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { ConfirmedSlotId = view!.Options[0].ConfirmedSlotId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].ConfirmedSlotId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A slot absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingASlotThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedCandidate();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { ConfirmedSlotId = invite.UnofferedSlotId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus,
        string HeadOfficeAddress);

    private async Task<InviteFixture> GivenAnInvitedCandidate()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredSlotIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(), new SlotWindow(date, startTime), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var slotId = Guid.NewGuid();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(slotId, proposal));
            offeredSlotIds.Add(slotId);
        }

        var unofferedProposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0)), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedSlotId = Guid.NewGuid();
        context.SlotProposals.Add(unofferedProposal);
        context.ConfirmedSlots.Add(ConfirmedSlot.CreateFrom(unofferedSlotId, unofferedProposal));

        var pilots = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        candidate.MarkInvited();
        context.Candidates.Add(candidate);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            candidate.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            offeredSlotIds,
            candidate.RequiredAppointmentTypeIds, 0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedSlotId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedSlotId);
}
`````

## tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs","encoding":"utf8","sha256":"4c3b50a753057e7718db148d009fab294ff30d5333eff12cbd4ad5368cb4a5fe","parts":1,"part":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ConfirmedSlotCapacityAdjustmentEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AManagerCanReplaceTheirOwnConfirmedTotal()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var slotId = await GivenSlotAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/slots/confirmed/{slotId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<AdjustmentResponse>();
        Assert.Equal(12, outcome!.TotalHeadcount);
        Assert.Equal(6, outcome.RemainingCapacity);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/slots/board");
        var slot = Assert.Single(
            board!.ConfirmedSlots,
            item => item.ConfirmedSlotId == slotId);
        Assert.Equal(12, slot.MyHeadcount);
        Assert.Equal(6, slot.MyRemainingCapacity);
    }

    [Fact]
    public async Task ADecreaseBelowActiveBookingsReturnsTheirCountAndChangesNothing()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var slotId = await GivenSlotAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/slots/confirmed/{slotId}/capacity",
            new { TotalHeadcount = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(
            "Headcount cannot be lower than the active-booking count of 6.",
            problem!.Detail);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var capacity = await context.SlotCapacities.AsNoTracking().SingleAsync(item =>
            item.ConfirmedSlotId == slotId
            && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustConfirmedCapacity()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var slotId = await GivenSlotAsync(totalHeadcount: 10, occupied: 0);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/slots/confirmed/{slotId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> GivenSlotAsync(int totalHeadcount, int occupied)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(),
            new SlotWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var slot = ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);

        for (var index = 0; index < occupied; index++)
        {
            slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.SlotProposals.Add(proposal);
        context.ConfirmedSlots.Add(slot);
        await context.SaveChangesAsync();
        return slot.Id;
    }

    private sealed record AdjustmentResponse(
        Guid ConfirmedSlotId,
        int TotalHeadcount,
        int RemainingCapacity);

    private sealed record BoardResponse(
        IReadOnlyList<ConfirmedSlotResponse> ConfirmedSlots);

    private sealed record ConfirmedSlotResponse(
        Guid ConfirmedSlotId,
        int MyHeadcount,
        int MyRemainingCapacity);

    private sealed record ProblemResponse(string? Detail);
}
`````

## tests/EventBooking.Api.Tests/CorsTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Api.Tests/CorsTests.cs","encoding":"utf8","sha256":"6421a201ded9fef66385ebab973aa58504fd57afb8535a6a8fdb8c25637ed153","parts":1,"part":1} -->

`````csharp
using System.Net;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class CorsTests(ApiFactory factory)
{
    private const string WebOrigin = "https://localhost:5002";

    [Fact]
    public async Task AllowedWebOriginIsReturnedOnAnAnonymousResponse()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/booking/manage/nonsense");
        request.Headers.Add("Origin", WebOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(WebOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task JsonPostPreflightAllowsTheConfiguredWebOrigin()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/booking/manage/nonsense/cancel");
        request.Headers.Add("Origin", WebOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(WebOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(
            "POST",
            response.Headers.GetValues("Access-Control-Allow-Methods").Single(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "content-type",
            response.Headers.GetValues("Access-Control-Allow-Headers").Single(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AllowedWebOriginResponseExposesContentDispositionForDownloads()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/booking/manage/nonsense");
        request.Headers.Add("Origin", WebOrigin);

        using var response = await client.SendAsync(request);

        Assert.Contains(
            "Content-Disposition",
            response.Headers.GetValues("Access-Control-Expose-Headers").Single(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnlistedOriginReceivesNoCorsPermission()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://unlisted.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
`````
