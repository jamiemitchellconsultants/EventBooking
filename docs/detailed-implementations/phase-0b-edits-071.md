# 00b — Vocabulary edits 71 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs — 1/1

<!-- vocabulary-file: {"id":228,"oldPath":"tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs","newPath":"tests/EventBooking.Api.Tests/AttendeeHypermediaTests.cs","beforeSha":"2f1776e9889c6479c89c2e8d5bfec96df34961a141fc6cbf757513bbf2d5661e","afterSha":"db93185d8404b069670ffe2f551b6b2c468f96d1ef346585a333bfcb96af91af","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/AttendeeHypermediaTests.cs — 1/1

<!-- vocabulary-file: {"id":228,"oldPath":"tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs","newPath":"tests/EventBooking.Api.Tests/AttendeeHypermediaTests.cs","beforeSha":"2f1776e9889c6479c89c2e8d5bfec96df34961a141fc6cbf757513bbf2d5661e","afterSha":"db93185d8404b069670ffe2f551b6b2c468f96d1ef346585a333bfcb96af91af","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class AttendeeHypermediaTests(ApiFactory factory)
{
    [Fact]
    public async Task AttendeeArrayStaysArrayAndItemsCarryActions()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/attendees/", new
        {
            name = "Hyper Media",
            email = $"hyper-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.Pilots,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/attendees/"));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        var attendee = document.RootElement.EnumerateArray()
            .Single(x => x.GetProperty("attendeeId").GetGuid() == id);
        var links = attendee.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/attendees/{id}/bookings", "GET", "listAttendeeBookings");
        AssertLink(links, "readiness", $"/api/attendees/{id}/readiness", "GET", "getAttendeeReadiness");
        AssertLink(links, "audit", $"/api/audit/attendee/{id}", "GET", "getAttendeeAuditHistory");
        AssertLink(links, "update", $"/api/attendees/{id}", "PUT", "updateAttendee");
        AssertLink(links, "delete", $"/api/attendees/{id}", "DELETE", "deleteAttendee");
        AssertLink(links, "invite", $"/api/attendees/{id}/invite", "POST", "triggerAttendeeInvite");
    }

    [Fact]
    public async Task GuidAndNoContentContractsRemainUnchanged()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/attendees/", new
        {
            name = "Compatibility",
            email = $"compat-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.CabinCrew,
        });
        var text = await created.Content.ReadAsStringAsync();
        Assert.True(Guid.TryParse(JsonSerializer.Deserialize<string>(text) ?? text.Trim('"'), out var id));
        using var updated = await client.PutAsJsonAsync($"/api/attendees/{id}", new
        {
            name = "Compatibility Two",
            email = $"compat2-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.CabinCrew,
        });
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal(string.Empty, await updated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadinessLinksBackToAttendeeWorkflows()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/attendees/", new
        {
            name = "Ready Links",
            email = $"ready-{Guid.NewGuid():N}@example.com",
            attendeeGroupId = AttendeeGroupIds.Engineering,
        });
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        using var json = JsonDocument.Parse(await client.GetStringAsync($"/api/attendees/{id}/readiness"));
        var links = json.RootElement.GetProperty("_links");
        AssertLink(links, "bookings", $"/api/attendees/{id}/bookings", "GET", "listAttendeeBookings");
        AssertLink(links, "readiness", $"/api/attendees/{id}/readiness", "GET", "getAttendeeReadiness");
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

## before — tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":229,"oldPath":"tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/AttendeeReadinessEndpointTests.cs","beforeSha":"74d6857d8c39f9784b840c7d8e0a97d1982a51d602b61e99bd9a717f78060df2","afterSha":"afcfc9dfb6891f7bb4ce512b510811741304d66d754bcd45da42f11d60cb44d4","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/AttendeeReadinessEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":229,"oldPath":"tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/AttendeeReadinessEndpointTests.cs","beforeSha":"74d6857d8c39f9784b840c7d8e0a97d1982a51d602b61e99bd9a717f78060df2","afterSha":"afcfc9dfb6891f7bb4ce512b510811741304d66d754bcd45da42f11d60cb44d4","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Api.Tests;

/// <summary>Verifies readiness is a Coordinator-only EventBooking response.</summary>
[Collection("api")]
public sealed class AttendeeReadinessEndpointTests(ApiFactory factory)
{
    private sealed record OutstandingResponse(string Code, string Name, bool IsRecoverable);

    private sealed record ReadinessResponse(
        Guid AttendeeId,
        string Code,
        string Display,
        IReadOnlyList<OutstandingResponse> OutstandingAppointmentTypes);

    /// <summary>A Coordinator receives the stable no-booking reason for a grouped Attendee.</summary>
    [Fact]
    public async Task CoordinatorCanReadNoActiveBookingReason()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/attendees", new
        {
            Name = "Amara Novak",
            Email = $"{Guid.NewGuid():N}@example.com",
            AttendeeGroupId = AttendeeGroupIds.CabinCrew,
        });
        var attendeeId = await created.Content.ReadFromJsonAsync<Guid>();

        var response = await client.GetAsync($"/api/attendees/{attendeeId}/readiness");
        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("NoActiveBooking", body!.Code);
        Assert.Empty(body.OutstandingAppointmentTypes);
    }

    /// <summary>An Admin is forbidden from receiving any Attendee readiness payload.</summary>
    [Fact]
    public async Task AdminIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        var response = await factory.CreateClient()
            .GetAsync($"/api/attendees/{Guid.NewGuid()}/readiness");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
`````

## before — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":230,"oldPath":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"aed53346e9eb5b9487fc5134121bdbca16d1bcb938b2d6fbc1c07fea53437b3d","afterSha":"d84f30f27c86a84153ae109466cd7bef51a2ce60dbd355dc9cd06d1fe9100d4f","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":230,"oldPath":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs","beforeSha":"aed53346e9eb5b9487fc5134121bdbca16d1bcb938b2d6fbc1c07fea53437b3d","afterSha":"d84f30f27c86a84153ae109466cd7bef51a2ce60dbd355dc9cd06d1fe9100d4f","side":"after","part":1,"parts":1} -->

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

    /// <summary>The confirmation names the API's configured transitional location, the one the email uses.</summary>
    [Fact]
    public async Task TheConfirmationCarriesTheConfiguredTransitionalLocationAddress()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();
        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var response = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm", new { EventId = view!.Options[0].EventId });

        var outcome = await response.Content.ReadFromJsonAsync<ConfirmResponse>();
        var configured = factory.Services.GetRequiredService<AttendeePortalOptions>().TransitionalLocationAddress;
        Assert.False(string.IsNullOrWhiteSpace(configured));
        Assert.Equal(configured, outcome!.TransitionalLocationAddress);
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
        string DeliveryStatus,
        string TransitionalLocationAddress);

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
                Guid.NewGuid(), new EventWindow(date, startTime), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            offeredEventIds.Add(eventId);
        }

        var unofferedProposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2030, 1, 17), new TimeOnly(9, 0)), Guid.NewGuid());
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

## before — tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":231,"oldPath":"tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs","beforeSha":"4c3b50a753057e7718db148d009fab294ff30d5333eff12cbd4ad5368cb4a5fe","afterSha":"dc2ae3dd025213c733ebcc9e549efe21180b5915beb63e877bd8c01ae72b45b6","side":"before","part":1,"parts":1} -->

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

## after — tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":231,"oldPath":"tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs","beforeSha":"4c3b50a753057e7718db148d009fab294ff30d5333eff12cbd4ad5368cb4a5fe","afterSha":"dc2ae3dd025213c733ebcc9e549efe21180b5915beb63e877bd8c01ae72b45b6","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class EventCapacityAdjustmentEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task AManagerCanReplaceTheirOwnConfirmedTotal()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var eventId = await GivenEventAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<AdjustmentResponse>();
        Assert.Equal(12, outcome!.TotalHeadcount);
        Assert.Equal(6, outcome.RemainingCapacity);

        var board = await client.GetFromJsonAsync<BoardResponse>("/api/events/board");
        var eventItem = Assert.Single(
            board!.Events,
            item => item.EventId == eventId);
        Assert.Equal(12, eventItem.MyHeadcount);
        Assert.Equal(6, eventItem.MyRemainingCapacity);
    }

    [Fact]
    public async Task ADecreaseBelowActiveBookingsReturnsTheirCountAndChangesNothing()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager,
            AppointmentTypeIds.DrugAndAlcoholTesting);
        var eventId = await GivenEventAsync(totalHeadcount: 10, occupied: 6);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal(
            "Headcount cannot be lower than the active-booking count of 6.",
            problem!.Detail);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var capacity = await context.EventCapacities.AsNoTracking().SingleAsync(item =>
            item.EventId == eventId
            && item.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        Assert.Equal(10, capacity.TotalHeadcount);
        Assert.Equal(4, capacity.RemainingCapacity);
    }

    [Fact]
    public async Task ACoordinatorCannotAdjustConfirmedCapacity()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var eventId = await GivenEventAsync(totalHeadcount: 10, occupied: 0);
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = 12 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> GivenEventAsync(int totalHeadcount, int occupied)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(),
            new EventWindow(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60), new TimeOnly(9, 0)),
            Guid.NewGuid());
        proposal.Accept(
            AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), totalHeadcount);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 20);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 20);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);

        for (var index = 0; index < occupied; index++)
        {
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        context.EventProposals.Add(proposal);
        context.Events.Add(eventItem);
        await context.SaveChangesAsync();
        return eventItem.Id;
    }

    private sealed record AdjustmentResponse(
        Guid EventId,
        int TotalHeadcount,
        int RemainingCapacity);

    private sealed record BoardResponse(
        IReadOnlyList<EventResponse> Events);

    private sealed record EventResponse(
        Guid EventId,
        int MyHeadcount,
        int MyRemainingCapacity);

    private sealed record ProblemResponse(string? Detail);
}
`````

## before — tests/EventBooking.Api.Tests/DashboardEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":232,"oldPath":"tests/EventBooking.Api.Tests/DashboardEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/DashboardEndpointTests.cs","beforeSha":"77ca7463440b0a9cdb9479098ebb7a3b00bfa9b3554eaa70bb32229d5909777d","afterSha":"2757ac7189cca8e0853fe7d2db3b83c375d699c10a42c847dd4c3651328c7f9e","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class DashboardEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ACoordinatorGetsAllThreeViewsInOneResponse()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");
        var dashboards = await response.Content.ReadFromJsonAsync<DashboardsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboards);
        Assert.NotNull(dashboards!.AwaitingAvailability);
        Assert.NotNull(dashboards.NoResponse);
        Assert.NotNull(dashboards.Slots);
    }

    [Fact]
    public async Task AnAdminIsForbiddenTheDashboards()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The Dashboards page binds to Email, RequiredCodes, WaitingSince/DaysWaiting, GaveUpOn and the
    /// slot Capacities list. Earlier coverage only asserted the row lists were non-null, so a DTO
    /// field could be renamed or dropped without failing a test — the page would simply render blank
    /// cells. This seeds one row of each kind and checks every field the page actually reads.
    /// </summary>
    [Fact]
    public async Task TheResponseCarriesEveryFieldThePageBindsTo()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var awaitingId = Guid.NewGuid();
        var noResponseId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var cabinCrew = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.CabinCrew);
            var awaiting = Candidate.Create(
                awaitingId, "A. Waiting", "a.waiting@mail.com", cabinCrew);
            awaiting.MarkAwaitingAvailability();
            context.Candidates.Add(awaiting);

            var groundOps = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.GroundOperationsAgent);
            var noResponse = Candidate.Create(
                noResponseId, "B. Stuck", "b.stuck@mail.com", groundOps);
            noResponse.MarkInvited();
            noResponse.MarkNoResponse();
            context.Candidates.Add(noResponse);

            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(today.AddDays(30), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
            var slot = ConfirmedSlot.CreateFrom(slotId, proposal);
            slot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            context.SlotProposals.Add(proposal);
            context.ConfirmedSlots.Add(slot);

            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var dashboards = await client.GetFromJsonAsync<DashboardsResponse>("/api/dashboards");

        Assert.NotNull(dashboards);

        var awaitingRow = Assert.Single(dashboards!.AwaitingAvailability, r => r.CandidateId == awaitingId);
        Assert.Equal("A. Waiting", awaitingRow.Name);
        Assert.Equal("a.waiting@mail.com", awaitingRow.Email);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, awaitingRow.RequiredCodes);
        Assert.Equal(today, awaitingRow.WaitingSince);
        Assert.Equal(0, awaitingRow.DaysWaiting);

        var noResponseRow = Assert.Single(dashboards.NoResponse, r => r.CandidateId == noResponseId);
        Assert.Equal("B. Stuck", noResponseRow.Name);
        Assert.Equal("b.stuck@mail.com", noResponseRow.Email);
        Assert.Equal(new[] { "MED" }, noResponseRow.RequiredCodes);
        Assert.Equal(today, noResponseRow.GaveUpOn);

        var slotRow = Assert.Single(dashboards.Slots, s => s.ConfirmedSlotId == slotId);
        Assert.Equal(today.AddDays(30), slotRow.Date);
        Assert.Equal(new TimeOnly(9, 0), slotRow.StartTime);
        Assert.Equal(new TimeOnly(13, 0), slotRow.EndTime);
        Assert.Equal(0, slotRow.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, slotRow.Capacities.Select(c => c.Code));
        var drugAndAlcohol = slotRow.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    private sealed record RowResponse(
        Guid CandidateId,
        string Name,
        string Email,
        IReadOnlyList<string> RequiredCodes,
        DateOnly? WaitingSince,
        int? DaysWaiting,
        DateOnly? GaveUpOn);

    private sealed record SlotCapacityResponse(string Code, int TotalHeadcount, int RemainingCapacity);

    private sealed record SlotResponse(
        Guid ConfirmedSlotId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        IReadOnlyList<SlotCapacityResponse> Capacities,
        int ActiveBookings);

    private sealed record DashboardsResponse(
        IReadOnlyList<RowResponse> AwaitingAvailability,
        IReadOnlyList<RowResponse> NoResponse,
        IReadOnlyList<SlotResponse> Slots);
}
`````

## after — tests/EventBooking.Api.Tests/DashboardEndpointTests.cs — 1/1

<!-- vocabulary-file: {"id":232,"oldPath":"tests/EventBooking.Api.Tests/DashboardEndpointTests.cs","newPath":"tests/EventBooking.Api.Tests/DashboardEndpointTests.cs","beforeSha":"77ca7463440b0a9cdb9479098ebb7a3b00bfa9b3554eaa70bb32229d5909777d","afterSha":"2757ac7189cca8e0853fe7d2db3b83c375d699c10a42c847dd4c3651328c7f9e","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class DashboardEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task ACoordinatorGetsAllThreeViewsInOneResponse()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");
        var dashboards = await response.Content.ReadFromJsonAsync<DashboardsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboards);
        Assert.NotNull(dashboards!.AwaitingAvailability);
        Assert.NotNull(dashboards.NoResponse);
        Assert.NotNull(dashboards.Events);
    }

    [Fact]
    public async Task AnAdminIsForbiddenTheDashboards()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerIsForbidden()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            Role.Manager, AppointmentTypeIds.MedicalCheckUp);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnauthenticatedCallerIsChallenged()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The Dashboards page binds to Email, RequiredCodes, WaitingSince/DaysWaiting, GaveUpOn and the
    /// event Capacities list. Earlier coverage only asserted the row lists were non-null, so a DTO
    /// field could be renamed or dropped without failing a test — the page would simply render blank
    /// cells. This seeds one row of each kind and checks every field the page actually reads.
    /// </summary>
    [Fact]
    public async Task TheResponseCarriesEveryFieldThePageBindsTo()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var awaitingId = Guid.NewGuid();
        var noResponseId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

            var cabinCrew = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.CabinCrew);
            var awaiting = Attendee.Create(
                awaitingId, "A. Waiting", "a.waiting@mail.com", cabinCrew);
            awaiting.MarkAwaitingAvailability();
            context.Attendees.Add(awaiting);

            var groundOps = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var noResponse = Attendee.Create(
                noResponseId, "B. Stuck", "b.stuck@mail.com", groundOps);
            noResponse.MarkInvited();
            noResponse.MarkNoResponse();
            context.Attendees.Add(noResponse);

            var proposal = EventProposal.Create(
                Guid.NewGuid(),
                new EventWindow(today.AddDays(30), new TimeOnly(9, 0)),
                Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
            var eventItem = Event.CreateFrom(eventId, proposal);
            eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            context.EventProposals.Add(proposal);
            context.Events.Add(eventItem);

            await context.SaveChangesAsync();
        }

        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        var dashboards = await client.GetFromJsonAsync<DashboardsResponse>("/api/dashboards");

        Assert.NotNull(dashboards);

        var awaitingRow = Assert.Single(dashboards!.AwaitingAvailability, r => r.AttendeeId == awaitingId);
        Assert.Equal("A. Waiting", awaitingRow.Name);
        Assert.Equal("a.waiting@mail.com", awaitingRow.Email);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, awaitingRow.RequiredCodes);
        Assert.Equal(today, awaitingRow.WaitingSince);
        Assert.Equal(0, awaitingRow.DaysWaiting);

        var noResponseRow = Assert.Single(dashboards.NoResponse, r => r.AttendeeId == noResponseId);
        Assert.Equal("B. Stuck", noResponseRow.Name);
        Assert.Equal("b.stuck@mail.com", noResponseRow.Email);
        Assert.Equal(new[] { "MED" }, noResponseRow.RequiredCodes);
        Assert.Equal(today, noResponseRow.GaveUpOn);

        var eventRow = Assert.Single(dashboards.Events, s => s.EventId == eventId);
        Assert.Equal(today.AddDays(30), eventRow.Date);
        Assert.Equal(new TimeOnly(9, 0), eventRow.StartTime);
        Assert.Equal(new TimeOnly(13, 0), eventRow.EndTime);
        Assert.Equal(0, eventRow.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, eventRow.Capacities.Select(c => c.Code));
        var drugAndAlcohol = eventRow.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    private sealed record RowResponse(
        Guid AttendeeId,
        string Name,
        string Email,
        IReadOnlyList<string> RequiredCodes,
        DateOnly? WaitingSince,
        int? DaysWaiting,
        DateOnly? GaveUpOn);

    private sealed record EventCapacityResponse(string Code, int TotalHeadcount, int RemainingCapacity);

    private sealed record EventResponse(
        Guid EventId,
        DateOnly Date,
        TimeOnly StartTime,
        TimeOnly EndTime,
        IReadOnlyList<EventCapacityResponse> Capacities,
        int ActiveBookings);

    private sealed record DashboardsResponse(
        IReadOnlyList<RowResponse> AwaitingAvailability,
        IReadOnlyList<RowResponse> NoResponse,
        IReadOnlyList<EventResponse> Events);
}
`````

## before — tests/EventBooking.Api.Tests/HealthTests.cs — 1/1

<!-- vocabulary-file: {"id":233,"oldPath":"tests/EventBooking.Api.Tests/HealthTests.cs","newPath":"tests/EventBooking.Api.Tests/HealthTests.cs","beforeSha":"d84a47c60ec5b73b03fa62c08484a9e487bb745f1c10c680d37ec76c70ca8afd","afterSha":"8dea831ce12a68db79ae37c9e070e4e94ecb81f7c01fe9e356a398a149b7ce9a","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using Microsoft.Extensions.Configuration;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task TheHostStartsAndAnswersHealthAnonymously()
    {
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void MissingConfigurationNamesEverySettingThatIsAbsent()
    {
        var empty = new ConfigurationBuilder().Build();

        var ex = Assert.Throws<InvalidOperationException>(() => EventBookingConfiguration.Read(empty));

        Assert.Contains("ConnectionStrings:EventBooking", ex.Message);
        Assert.Contains("Tokens:SigningKey", ex.Message);
        Assert.Contains("Portal:BaseUrl", ex.Message);
        Assert.Contains("Auth:Provider", ex.Message);
        Assert.Contains("Email:Provider", ex.Message);
    }

    /// <summary>Ensures only the documented exact email-provider literals are accepted.</summary>
    [Theory]
    [InlineData("Fax")]
    [InlineData("999")]
    [InlineData("ses")]
    [InlineData("smtp")]
    public void AnInvalidEmailProviderIsRejectedWithAReadableMessage(string emailProvider)
    {
        var configuration = ConfigurationWith(emailProvider: emailProvider);

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Email:Provider", ex.Message);
    }

    [Fact]
    public void AnInvalidAuthProviderIsRejectedWithAReadableMessage()
    {
        var configuration = ConfigurationWith(authProvider: "Auth0");

        var ex = Assert.Throws<InvalidOperationException>(
            () => EventBookingConfiguration.Read(configuration));

        Assert.Contains("Auth:Provider", ex.Message);
    }

    /// <summary>Every required key present and valid, except the one override under test.</summary>
    private static IConfiguration ConfigurationWith(
        string emailProvider = "Smtp", string authProvider = "Local") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:EventBooking"] = "Host=localhost;Database=x;Username=x;Password=x",
                ["HeadOffice:TimeZoneId"] = "Europe/London",
                ["HeadOffice:Address"] = "1 Example Street",
                ["Tokens:SigningKey"] = "a-signing-key-that-is-long-enough-to-be-safe",
                ["Email:FromAddress"] = "recruitment@example.com",
                ["Email:FromName"] = "Recruitment Team",
                ["Email:Provider"] = emailProvider,
                ["Auth:Provider"] = authProvider,
                ["Portal:BaseUrl"] = "https://localhost:5001",
                ["Portal:CoordinatorContact"] = "recruitment@example.com",
            })
            .Build();
}
`````
