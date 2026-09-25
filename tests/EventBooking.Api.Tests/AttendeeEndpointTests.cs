using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.AspNetCore.Mvc;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class AttendeeEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ACoordinatorCanCreateAndListAttendees()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var created = await client.PostAsJsonAsync(
            "/api/attendees",
            new
            {
                Name = "Amara Novak",
                Email = email,
                AttendeeGroupId = AttendeeGroupIds.Pilots,
            });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var listed = await client.GetFromJsonAsync<AttendeeListResponse>($"/api/attendees?search={email}");
        Assert.NotNull(listed);
        Assert.Single(listed!.Items);
        Assert.Equal("Not yet invited", listed!.Items[0].StatusDisplay);
    }

    [Fact]
    public async Task AManagerIsForbiddenFromTheAttendeeRoutes()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/attendees");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARejectedImportComesBackWithItsRowErrors()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var csv = "name,email,attendee_group\nAmara Novak,a.novak@mail.com,XYZ";
        var response = await ImportAsync(client, csv);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonProblem>();
        Assert.Equal("validation-failed", problem!.Type);
        var error = Assert.Single(problem.Errors);
        Assert.Equal(2, error.Line);
    }

    [Fact]
    public async Task AnAdminCanReadAndChangeTheSettings()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var read = await client.GetFromJsonAsync<SettingsValues>("/api/settings");
        var updated = await client.PutAsJsonAsync(
            "/api/settings",
            new
            {
                InviteExpiryDays = 7,
                MaxAutoRetryCount = 1,
                InviteOptionCount = 3,
                PendingRegistrationExpiryHours = 48,
                ExpectedVersion = read!.Version,
            });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var settings = await client.GetFromJsonAsync<SettingsValues>("/api/settings");
        Assert.Equal(7, settings!.InviteExpiryDays);
        Assert.Equal(1, settings.MaxAutoRetryCount);
        Assert.Equal(3, settings.InviteOptionCount);
    }

    [Fact]
    public async Task ACoordinatorCannotChangeTheSettings()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/settings", new { InviteExpiryDays = 7, MaxAutoRetryCount = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MissingOrNullGroupsAreValidationErrorsOnCreateAndUpdate()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var missingOnCreate = await client.PostAsJsonAsync(
            "/api/attendees", new { Name = "Amara Novak", Email = email });
        var nullOnCreate = await client.PostAsJsonAsync(
            "/api/attendees",
            new { Name = "Amara Novak", Email = email, AttendeeGroupId = (Guid?)null });

        var created = await client.PostAsJsonAsync(
            "/api/attendees",
            new
            {
                Name = "Amara Novak",
                Email = email,
                AttendeeGroupId = AttendeeGroupIds.Pilots,
            });
        var attendeeId = await created.Content.ReadFromJsonAsync<Guid>();

        var missingOnUpdate = await client.PutAsJsonAsync(
            $"/api/attendees/{attendeeId}", new { Name = "Amara Updated", Email = email });
        var nullOnUpdate = await client.PutAsJsonAsync(
            $"/api/attendees/{attendeeId}",
            new { Name = "Amara Updated", Email = email, AttendeeGroupId = (Guid?)null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingOnCreate.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nullOnCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingOnUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nullOnUpdate.StatusCode);
    }

    [Fact]
    public async Task RequirementOnlyJsonIsAnAttendeeGroupErrorAndPersistsNothing()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var response = await client.PostAsJsonAsync(
            "/api/attendees",
            new
            {
                Name = "Amara Novak",
                Email = email,
                AppointmentTypeIds = new[] { AppointmentTypeIds.DrugAndAlcoholTesting },
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("validation-failed", problem!.Type);

        var listed = await client.GetFromJsonAsync<AttendeeListResponse>($"/api/attendees?search={email}");
        Assert.Empty(listed!.Items);
    }

    [Fact]
    public async Task ACoordinatorCanListAttendeeGroups()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<AttendeeGroupPage>("/api/attendee-groups");

        Assert.NotNull(page);
        Assert.Contains(page!.Items, group => group.Code == "PILOTS");
        Assert.All(page.Items, group => Assert.True(group.IsActive));
    }

    /// <summary>
    /// The reference-data group list is open to any staff member: a Manager reads it too.
    /// The old assignable-groups gate this case used to assert went with the ported route.
    /// </summary>
    [Fact]
    public async Task AManagerMayReadTheAttendeeGroupRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/attendee-groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ACoordinatorCanUpdateAAttendee()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var created = await client.PostAsJsonAsync(
            "/api/attendees",
            new
            {
                Name = "Amara Novak",
                Email = email,
                AttendeeGroupId = AttendeeGroupIds.Pilots,
            });
        var attendeeId = await created.Content.ReadFromJsonAsync<Guid>();

        var updated = await client.PutAsJsonAsync(
            $"/api/attendees/{attendeeId}",
            new
            {
                Name = "Amara Smith",
                Email = email,
                AttendeeGroupId = AttendeeGroupIds.Engineering,
            });

        var listed = await client.GetFromJsonAsync<AttendeeListResponse>($"/api/attendees?search={email}");
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        var attendee = Assert.Single(listed!.Items);
        Assert.Equal("Amara Smith", attendee.Name);
    }

    [Fact]
    public async Task ImportsRequireAMultipartFileAndAnAtMostOneMiBBody()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        const string csv = "name,email,attendee_group\nAmara Novak,a.novak@mail.com,PILOTS";

        var notMultipart = await client.PostAsync(
            "/api/attendees/import", new StringContent(csv, Encoding.UTF8, "text/csv"));
        var tooLarge = await ImportAsync(client, new string('x', 1_048_577));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, notMultipart.StatusCode);
        Assert.Equal("multipart-required",
            (await notMultipart.Content.ReadFromJsonAsync<JsonProblem>())!.Errors[0].Code);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLarge.StatusCode);
        Assert.Equal("file-too-large",
            (await tooLarge.Content.ReadFromJsonAsync<JsonProblem>())!.Errors[0].Code);
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

        var assignedBody = await assigned.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            assignedBody.GetProperty("appointmentTypeId").GetGuid());
        Assert.Equal(
            managerUserId, assignedBody.GetProperty("targetStaffUserId").GetGuid());

        var staff = await client.GetFromJsonAsync<JsonElement>("/api/staff-access");
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            StaffRow(staff, managerUserId).GetProperty("appointmentTypeId").GetGuid());
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

        var staff = await client.GetFromJsonAsync<JsonElement>("/api/staff-access");
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            StaffRow(staff, replacementUserId).GetProperty("appointmentTypeId").GetGuid());
        Assert.Equal(
            AppointmentTypeIds.MedicalCheckUp,
            StaffRow(staff, managerUserId).GetProperty("appointmentTypeId").GetGuid());
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

        var staff = await client.GetFromJsonAsync<JsonElement>("/api/staff-access");
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            StaffRow(staff, replacementManagerUserId).GetProperty("appointmentTypeId").GetGuid());
        var former = StaffRow(staff, formerManagerUserId);
        Assert.Equal(JsonValueKind.Null, former.GetProperty("appointmentTypeId").ValueKind);
        Assert.Contains(
            "Manager",
            former.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));

        factory.SignedInAs = formerManagerUserId;
        factory.RolesClaim = ["Manager"];
        var formerManagerProposals = await client.GetAsync("/api/event-proposals");
        Assert.Equal(HttpStatusCode.Forbidden, formerManagerProposals.StatusCode);
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
        var staff = await client.GetFromJsonAsync<JsonElement>("/api/staff-access");
        Assert.Equal(
            AppointmentTypeIds.UniformFitting,
            StaffRow(staff, replacementUserId).GetProperty("appointmentTypeId").GetGuid());
        Assert.Equal(
            JsonValueKind.Null,
            StaffRow(staff, managerUserId).GetProperty("appointmentTypeId").ValueKind);
    }

    [Fact]
    public async Task ACoordinatorCanTriggerAnInviteAndConfirmItsCascadeDeletion()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        await GivenEligibleEventsAsync();
        var email = $"{Guid.NewGuid():N}@mail.com";

        var created = await client.PostAsJsonAsync(
            "/api/attendees",
            new
            {
                Name = "Amara Novak",
                Email = email,
                AttendeeGroupId = AttendeeGroupIds.Pilots,
            });
        var attendeeId = await created.Content.ReadFromJsonAsync<Guid>();

        var invited = await client.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/invites",
            new { LocationIds = new[] { EventBooking.Domain.Locations.TransitionalLocation.Id } });
        var unconfirmedDeletion = await client.DeleteAsync($"/api/attendees/{attendeeId}");
        var confirmedDeletion = await client.DeleteAsync($"/api/attendees/{attendeeId}?confirm=true");

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmedDeletion.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, confirmedDeletion.StatusCode);
    }

    [Fact]
    public async Task ACoordinatorInviteWithNoBodyDefaultsToEveryActiveLocation()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        await GivenEligibleEventsAsync();
        var created = await client.PostAsJsonAsync(
            "/api/attendees",
            new
            {
                Name = "Bo Lind",
                Email = $"{Guid.NewGuid():N}@mail.com",
                AttendeeGroupId = AttendeeGroupIds.Pilots,
            });
        var attendeeId = await created.Content.ReadFromJsonAsync<Guid>();

        var invited = await client.PostAsync($"/api/attendees/{attendeeId}/invites", null);

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var invite = await context.Invites.SingleAsync(item => item.AttendeeId == attendeeId);
        var invitedLocationIds = await context.Set<InviteLocation>()
            .Where(link => link.InviteId == invite.Id)
            .Select(link => link.LocationId)
            .ToListAsync();
        var activeLocationIds = await context.Locations
            .Where(location => location.IsActive)
            .Select(location => location.Id)
            .ToListAsync();
        Assert.Equal(
            activeLocationIds.OrderBy(id => id),
            invitedLocationIds.OrderBy(id => id));
    }

    /// <summary>Staff retry stages a fresh delivery; the dispatcher sends the same link.</summary>
    [Fact]
    public async Task ACoordinatorCanRetryAFailedInviteWithAFreshHashedToken()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var (attendeeId, link, deliveryId, email) = await GivenFailedInviteAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            $"/api/attendees/{attendeeId}/email-retry?emailLogId={deliveryId}", null);
        var outcome = await response.Content.ReadFromJsonAsync<RetryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, outcome!.EmailLogId);

        // The endpoint stages; the dispatcher sends. The test host runs no loop, so the
        // test drives one pass explicitly.
        using (var dispatch = factory.Services.CreateScope())
        {
            await dispatch.ServiceProvider.GetRequiredService<OutboxDispatcher>()
                .DispatchOnceAsync();
        }

        var message = Assert.Single(
            factory.EmailTransport.Sent,
            sent => sent.Recipient == email);
        Assert.Contains("/book/", message.TextBody);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var invite = await context.Invites.SingleAsync(item => item.AttendeeId == attendeeId);
        // A resend reuses the current link (design 06), so the version does not move.
        Assert.Equal(Invite.InitialTokenVersion, invite.TokenVersion);
        Assert.Contains($"/book/{link}", message.TextBody, StringComparison.Ordinal);
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
            $"/api/staff-access/{Guid.NewGuid()}/scope",
            new { Roles = new[] { "SuperUser" }, AppointmentTypeId = (Guid?)null, ExpectedVersion = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, coordinatorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
    }

    private sealed record AttendeeResponse(Guid AttendeeId, string Name, string StatusDisplay);

    private sealed record AttendeeListResponse(IReadOnlyList<AttendeeResponse> Items, string? NextCursor);

    private sealed record AttendeeGroupResponse(
        Guid AttendeeGroupId, string Code, string Name);

    private sealed record RetryResponse(Guid EmailLogId);

    private sealed record ImportError(int LineNumber, string Message);

    private sealed record ImportResponse(bool Accepted, int ImportedCount, IReadOnlyList<ImportError> Errors);

    private sealed record EventImportError(int LineNumber, string Message);

    private sealed record EventImportResponse(bool Accepted, int ImportedCount, IReadOnlyList<EventImportError> Errors);

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
            $"/api/staff-access/{staffUserId}/scope",
            new { AppointmentTypeId = appointmentTypeId, ExpectedVersion = expectedVersion });

    private static JsonElement StaffRow(JsonElement list, Guid staffUserId) =>
        list.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("staffUserId").GetGuid() == staffUserId);

    private static Task<HttpResponseMessage> ImportAsync(HttpClient client, string csv)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "attendees.csv" },
        };
        return client.PostAsync("/api/attendees/import", content);
    }

    private sealed record AttendeeGroupPage(List<AttendeeGroupItem> Items);

    private sealed record AttendeeGroupItem(Guid Id, string Code, string Name, bool IsActive);

    private sealed record SettingsValues(
        int InviteExpiryDays, int MaxAutoRetryCount, int InviteOptionCount,
        int PendingRegistrationExpiryHours, long Version);

    private sealed record JsonProblem(string Type, List<JsonProblemError> Errors);

    private sealed record JsonProblemError(string Code, int? Line);

    private async Task GivenEligibleEventsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var date = new DateOnly(2030, 1, 14);

        for (var index = 0; index < 3; index++)
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(date.AddDays(index), new TimeOnly(9, 0), 240), Guid.NewGuid());
            foreach (var appointmentTypeId in AppointmentTypeIds.All)
            {
                proposal.Accept(appointmentTypeId, Guid.NewGuid(), 8);
            }

            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(Guid.NewGuid(), proposal));
        }

        await context.SaveChangesAsync();
    }

    private async Task<(Guid AttendeeId, string OldHash, Guid DeliveryId, string Email)> GivenFailedInviteAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var eventIds = new List<Guid>();

        foreach (var day in new[] { 14, 15, 16 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(),
                new EventWindow(new DateOnly(2030, 1, day), new TimeOnly(9, 0), 240),
                Guid.NewGuid());
            foreach (var appointmentTypeId in AppointmentTypeIds.All)
            {
                proposal.Accept(appointmentTypeId, Guid.NewGuid(), 8);
            }

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            eventIds.Add(eventId);
        }

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var email = $"{Guid.NewGuid():N}@mail.com";
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Retry Attendee",
            email,
            pilots,
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        var invite = Invite.CreateInitial(
            inviteId,
            attendee.Id,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            [ProposalFixture.LocationId],
            eventIds,
            attendee.RequiredAppointmentTypeIds,
            0);
        context.Invites.Add(invite);

        var delivery = EmailLog.RecordPending(
            Guid.NewGuid(),
            attendee.Id,
            EmailTemplate.AttendeeInvite,
            new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            inviteId: invite.Id);
        delivery.MarkFailed(new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.Zero));
        context.EmailLogs.Add(delivery);
        await context.SaveChangesAsync();

        return (attendee.Id, issued, delivery.Id, email);
    }
}
