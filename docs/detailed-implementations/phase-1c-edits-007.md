# 01c — Negotiation across any number of types, edits 7 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs — 1/1

<!-- retirement-file: {"id":17,"file":"tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs","beforeSha":"dde63366f878b50d8f01bbf7e8547e891093c2252b598b1dd692d21c511a4b7e","afterSha":"7d07cfc68e4892c0f73fdb465ec0ad61c2b9ff0631162190d28ef4ba1b1935c4","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.AspNetCore.Mvc;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
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

        var listed = await client.GetFromJsonAsync<List<AttendeeResponse>>($"/api/attendees?search={email}");
        Assert.NotNull(listed);
        Assert.Single(listed!);
        Assert.Equal("Not yet invited", listed![0].StatusDisplay);
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
        var response = await client.PostAsync(
            "/api/attendees/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

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

        Assert.Equal(HttpStatusCode.BadRequest, missingOnCreate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nullOnCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingOnUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nullOnUpdate.StatusCode);
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("attendee_group_required", problem!.Title);

        var listed = await client.GetFromJsonAsync<List<AttendeeResponse>>($"/api/attendees?search={email}");
        Assert.Empty(listed!);
    }

    [Fact]
    public async Task ACoordinatorCanListAttendeeGroups()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var groups = await client.GetFromJsonAsync<List<AttendeeGroupResponse>>("/api/attendee-groups");

        Assert.NotNull(groups);
        Assert.Equal(5, groups!.Count);
        Assert.Contains(groups, group => group.Code == "PILOTS");
    }

    [Fact]
    public async Task AManagerIsForbiddenFromTheAttendeeGroupRoute()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.UniformFitting);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/attendee-groups");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

        var listed = await client.GetFromJsonAsync<List<AttendeeResponse>>($"/api/attendees?search={email}");
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        var attendee = Assert.Single(listed!);
        Assert.Equal("Amara Smith", attendee.Name);
    }

    [Fact]
    public async Task ImportsRequireCsvContentTypeAndAnAtMostOneMiBBody()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        const string csv = "name,email,attendee_group\nAmara Novak,a.novak@mail.com,PILOTS";

        var wrongContentType = await client.PostAsync(
            "/api/attendees/import", new StringContent(csv, Encoding.UTF8, "text/plain"));
        var tooLarge = await client.PostAsync(
            "/api/attendees/import",
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
            Enumerable.Repeat("Attendee,duplicate@mail.com,PILOTS", 10_001));
        var csv = $"name,email,attendee_group\n{rows}";

        var response = await client.PostAsync(
            "/api/attendees/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

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
        var formerManagerBoard = await client.GetAsync("/api/events/board");
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

        var invited = await client.PostAsync($"/api/attendees/{attendeeId}/invite", null);
        var unconfirmedDeletion = await client.DeleteAsync($"/api/attendees/{attendeeId}");
        var confirmedDeletion = await client.DeleteAsync($"/api/attendees/{attendeeId}?confirm=true");

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmedDeletion.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, confirmedDeletion.StatusCode);
    }

    /// <summary>Staff retry derives the failed invite template and context on the server.</summary>
    [Fact]
    public async Task ACoordinatorCanRetryAFailedInviteWithAFreshHashedToken()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var (attendeeId, oldHash) = await GivenFailedInviteAsync();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/attendees/{attendeeId}/email-retry", null);
        var outcome = await response.Content.ReadFromJsonAsync<RetryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sent", outcome!.DeliveryStatus);
        var message = Assert.Single(
            factory.EmailTransport.Sent,
            sent => sent.AttendeeId == attendeeId && sent.Template == EmailTemplate.AttendeeInvite);
        Assert.Contains("/book/", message.TextBody);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var invite = await context.Invites.SingleAsync(item => item.AttendeeId == attendeeId);
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

    private sealed record AttendeeResponse(Guid AttendeeId, string Name, string StatusDisplay);

    private sealed record AttendeeGroupResponse(
        Guid AttendeeGroupId, string Code, string Name);

    private sealed record RetryResponse(string DeliveryStatus, Guid DeliveryId);

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
            $"/api/admin/staff-access/{staffUserId}",
            new { AppointmentTypeId = appointmentTypeId, ExpectedVersion = expectedVersion });

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

    private async Task<(Guid AttendeeId, string OldHash)> GivenFailedInviteAsync()
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
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Retry Attendee",
            $"{Guid.NewGuid():N}@mail.com",
            pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
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

        return (attendee.Id, issued.TokenHash);
    }
}
`````

## before — tests/EventBooking.Api.Tests/BookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","beforeSha":"94e053faff467630f05138b0c80fc31d8314c1a699b256afc03e1590e1de9b62","afterSha":"e8e4d04f6dc2991764ff1f61ba1e1458eb20f08bf05faf011b4d8a4cbc5c05d5","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class BookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AValidTokenReturnsOnlyTheAttendeeFacingOptionsEarliestFirst()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{invite.Token}");
        var json = await response.Content.ReadAsStringAsync();
        var view = JsonSerializer.Deserialize<InviteResponse>(json, JsonSerializerOptions.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(view);

        // Catches an incomplete root projection that drops the invite or attendee identity.
        Assert.Equal(invite.Id, view!.InviteId);
        Assert.Equal("Amara Novak", view!.AttendeeName);

        // Catches projecting no appointment types or types from the wrong source.
        Assert.Equal(["Drug & Alcohol Testing", "Uniform Fitting"], view.AppointmentTypeNames);

        // Catches removing chronological ordering or ordering by the offered list or event ID.
        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                Guid.Parse("00000000-0000-0000-0000-000000000090"),
                Guid.Parse("00000000-0000-0000-0000-000000000050"),
            ],
            view.Options.Select(option => option.EventId));
        Assert.Equal(
            [new DateOnly(2030, 1, 14), new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16)],
            view.Options.Select(option => option.Date));

        // Catches omitted or incorrectly mapped attendee-facing window fields.
        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(13, 0)],
            view.Options.Select(option => option.StartTime));
        Assert.Equal(
            [new TimeOnly(13, 0), new TimeOnly(15, 0), new TimeOnly(17, 0)],
            view.Options.Select(option => option.EndTime));
        Assert.Equal(
            [
                "Monday 14 Jan 2030, 09:00-13:00",
                "Tuesday 15 Jan 2030, 11:00-15:00",
                "Wednesday 16 Jan 2030, 13:00-17:00",
            ],
            view.Options.Select(option => option.Display));

        // Catches returning a domain event/capacity object instead of the attendee-facing projection.
        using var document = JsonDocument.Parse(json);
        var links = document.RootElement.GetProperty("_links");
        var confirm = links.GetProperty("confirm");
        Assert.Equal($"/api/booking/{Uri.EscapeDataString(invite.Token)}/confirm", confirm.GetProperty("href").GetString());
        Assert.Equal("POST", confirm.GetProperty("method").GetString());
        Assert.Equal("confirmBooking", confirm.GetProperty("operationId").GetString());
        Assert.DoesNotContain("tokenHash", json, StringComparison.OrdinalIgnoreCase);
        AssertNoPropertiesNamed(
            document.RootElement,
            "capacities",
            "remainingCapacity",
            "totalHeadcount",
            "headcount",
            "status",
            "id",
            "proposalId",
            "window",
            "acceptances",
            "createdByManagerUserId",
            "attendeeId",
            "tokenHash",
            "offeredEventIds",
            "expiresAt",
            "usedAt");
    }

    [Fact]
    public async Task AnUnknownTokenIsNotFoundWithTheGenericMessage()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("no longer valid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheRouteNeedsNoAuthentication()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Seeds three deliberately non-ID-ordered events, a attendee and a pending invite.</summary>
    internal async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var offeredEvents = new[]
        {
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000050"), new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000010"), new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000090"), new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
        };

        foreach (var offeredEvent in offeredEvents)
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(),
                new EventWindow(offeredEvent.Date, offeredEvent.StartTime, 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventItem = Event.CreateFrom(offeredEvent.Id, proposal);
            context.EventProposals.Add(proposal);
            context.Events.Add(eventItem);
        }

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            offeredEvents.Select(eventItem => eventItem.Id).ToList(),
            attendee.RequiredAppointmentTypeIds, 0));

        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, inviteId);
    }

    private static void AssertNoPropertiesNamed(JsonElement element, params string[] forbiddenNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, forbiddenNames);
                AssertNoPropertiesNamed(property.Value, forbiddenNames);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoPropertiesNamed(item, forbiddenNames);
            }
        }
    }

    internal sealed record InviteFixture(string Token, Guid Id);

    internal sealed record InviteOptionFixture(Guid Id, DateOnly Date, TimeOnly StartTime);

    internal sealed record InviteOptionResponse(
        Guid EventId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Display);

    internal sealed record InviteResponse(
        Guid InviteId,
        string AttendeeName,
        IReadOnlyList<string> AppointmentTypeNames,
        IReadOnlyList<InviteOptionResponse> Options);
}
`````

## after — tests/EventBooking.Api.Tests/BookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Api.Tests/BookingEndpointTests.cs","beforeSha":"94e053faff467630f05138b0c80fc31d8314c1a699b256afc03e1590e1de9b62","afterSha":"e8e4d04f6dc2991764ff1f61ba1e1458eb20f08bf05faf011b4d8a4cbc5c05d5","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class BookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AValidTokenReturnsOnlyTheAttendeeFacingOptionsEarliestFirst()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/booking/{invite.Token}");
        var json = await response.Content.ReadAsStringAsync();
        var view = JsonSerializer.Deserialize<InviteResponse>(json, JsonSerializerOptions.Web);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(view);

        // Catches an incomplete root projection that drops the invite or attendee identity.
        Assert.Equal(invite.Id, view!.InviteId);
        Assert.Equal("Amara Novak", view!.AttendeeName);

        // Catches projecting no appointment types or types from the wrong source.
        Assert.Equal(["Drug & Alcohol Testing", "Uniform Fitting"], view.AppointmentTypeNames);

        // Catches removing chronological ordering or ordering by the offered list or event ID.
        Assert.Equal(
            [
                Guid.Parse("00000000-0000-0000-0000-000000000010"),
                Guid.Parse("00000000-0000-0000-0000-000000000090"),
                Guid.Parse("00000000-0000-0000-0000-000000000050"),
            ],
            view.Options.Select(option => option.EventId));
        Assert.Equal(
            [new DateOnly(2030, 1, 14), new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16)],
            view.Options.Select(option => option.Date));

        // Catches omitted or incorrectly mapped attendee-facing window fields.
        Assert.Equal(
            [new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(13, 0)],
            view.Options.Select(option => option.StartTime));
        Assert.Equal(
            [new TimeOnly(13, 0), new TimeOnly(15, 0), new TimeOnly(17, 0)],
            view.Options.Select(option => option.EndTime));
        Assert.Equal(
            [
                "Monday 14 Jan 2030, 09:00-13:00",
                "Tuesday 15 Jan 2030, 11:00-15:00",
                "Wednesday 16 Jan 2030, 13:00-17:00",
            ],
            view.Options.Select(option => option.Display));

        // Catches returning a domain event/capacity object instead of the attendee-facing projection.
        using var document = JsonDocument.Parse(json);
        var links = document.RootElement.GetProperty("_links");
        var confirm = links.GetProperty("confirm");
        Assert.Equal($"/api/booking/{Uri.EscapeDataString(invite.Token)}/confirm", confirm.GetProperty("href").GetString());
        Assert.Equal("POST", confirm.GetProperty("method").GetString());
        Assert.Equal("confirmBooking", confirm.GetProperty("operationId").GetString());
        Assert.DoesNotContain("tokenHash", json, StringComparison.OrdinalIgnoreCase);
        AssertNoPropertiesNamed(
            document.RootElement,
            "capacities",
            "remainingCapacity",
            "totalHeadcount",
            "headcount",
            "status",
            "id",
            "proposalId",
            "window",
            "acceptances",
            "createdByManagerUserId",
            "attendeeId",
            "tokenHash",
            "offeredEventIds",
            "expiresAt",
            "usedAt");
    }

    [Fact]
    public async Task AnUnknownTokenIsNotFoundWithTheGenericMessage()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("no longer valid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheRouteNeedsNoAuthentication()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/nonsense");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Seeds three deliberately non-ID-ordered events, a attendee and a pending invite.</summary>
    internal async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var offeredEvents = new[]
        {
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000050"), new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000010"), new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
            new InviteOptionFixture(
                Guid.Parse("00000000-0000-0000-0000-000000000090"), new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
        };

        foreach (var offeredEvent in offeredEvents)
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(),
                new EventWindow(offeredEvent.Date, offeredEvent.StartTime, 240),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventItem = Event.CreateFrom(offeredEvent.Id, proposal);
            context.EventProposals.Add(proposal);
            context.Events.Add(eventItem);
        }

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            offeredEvents.Select(eventItem => eventItem.Id).ToList(),
            attendee.RequiredAppointmentTypeIds, 0));

        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, inviteId);
    }

    private static void AssertNoPropertiesNamed(JsonElement element, params string[] forbiddenNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, forbiddenNames);
                AssertNoPropertiesNamed(property.Value, forbiddenNames);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoPropertiesNamed(item, forbiddenNames);
            }
        }
    }

    internal sealed record InviteFixture(string Token, Guid Id);

    internal sealed record InviteOptionFixture(Guid Id, DateOnly Date, TimeOnly StartTime);

    internal sealed record InviteOptionResponse(
        Guid EventId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Display);

    internal sealed record InviteResponse(
        Guid InviteId,
        string AttendeeName,
        IReadOnlyList<string> AppointmentTypeNames,
        IReadOnlyList<InviteOptionResponse> Options);
}
`````

## before — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"1239ccdc4e522ff529f2e6f4fc47b3cfd8fb3e124f94c9ee37c710cf3961b1ba","afterSha":"6827350b3ff4f99bdad2e5f99578e1c43afaa36dfffe526a70886e1af25f3f2e","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A attendee can confirm one of the offered events.</summary>
    [Fact]
    public async Task AAttendeeCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen.EventId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = view!.Options[0].EventId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].EventId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A event absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingAEventThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = invite.UnofferedEventId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus);

    private async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredEventIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0), 240), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedEventId = Guid.NewGuid();
        context.EventProposals.Add(unofferedProposal);
        context.Events.Add(Event.CreateFrom(unofferedEventId, unofferedProposal));

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            offeredEventIds,
            attendee.RequiredAppointmentTypeIds, 0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedEventId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedEventId);
}
`````

## after — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"1239ccdc4e522ff529f2e6f4fc47b3cfd8fb3e124f94c9ee37c710cf3961b1ba","afterSha":"6827350b3ff4f99bdad2e5f99578e1c43afaa36dfffe526a70886e1af25f3f2e","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using EventBooking.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies booking confirmation delivery outcomes at the HTTP boundary.</summary>
[Collection("api")]
public class ConfirmBookingEndpointTests(ApiFactory factory)
{
    /// <summary>A attendee can confirm one of the offered events.</summary>
    [Fact]
    public async Task AAttendeeCanConfirmOneOfTheirOptions()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[1];

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen.EventId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        Assert.NotEqual(Guid.Empty, outcome!.BookingId);
        Assert.Equal(chosen.Date, outcome.Date);
        Assert.Equal(chosen.StartTime, outcome.StartTime);
        Assert.Equal(chosen.EndTime, outcome.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
        Assert.Equal("Sent", outcome.DeliveryStatus);
    }

    /// <summary>Booking confirmation remains successful while a provider rejection is reported.</summary>
    [Fact]
    public async Task AProviderFailureReturnsAConfirmedBookingAndFailedDeliveryStatus()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.EmailTransport.FailNextSend = true;
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = view!.Options[0].EventId });
        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Failed", outcome!.DeliveryStatus);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ManageToken));
    }

    /// <summary>The same confirmation link cannot be consumed twice.</summary>
    [Fact]
    public async Task TheSameLinkCannotBeUsedTwice()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");
        var chosen = view!.Options[0].EventId;

        var first = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });
        var second = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = chosen });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    /// <summary>A event absent from the invitation is rejected as a conflict.</summary>
    [Fact]
    public async Task ChoosingAEventThatWasNeverOfferedIsAConflict()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = invite.UnofferedEventId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record ConfirmResponse(
        Guid BookingId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string ManageToken,
        string DeliveryStatus);

    private async Task<InviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var offeredEventIds = new List<Guid>();

        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0), 240), Guid.NewGuid());
        unofferedProposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        unofferedProposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        unofferedProposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var unofferedEventId = Guid.NewGuid();
        context.EventProposals.Add(unofferedProposal);
        context.Events.Add(Event.CreateFrom(unofferedEventId, unofferedProposal));

        var pilots = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com", pilots);
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            issued.TokenHash,
            new DateTimeOffset(2030, 1, 20, 0, 0, 0, TimeSpan.Zero),
            offeredEventIds,
            attendee.RequiredAppointmentTypeIds, 0));
        await context.SaveChangesAsync();

        return new InviteFixture(issued.Token, unofferedEventId);
    }

    private sealed record InviteFixture(string Token, Guid UnofferedEventId);
}
`````
