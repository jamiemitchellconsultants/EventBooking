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
