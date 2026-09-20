# 01a — Variable-length windows in the location's zone, edits 10 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":24,"file":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","beforeSha":"54908c6f32741b4592523812ceb5852722131d49badaa63eed8e3ff093fdbf33","afterSha":"7c9718e4c6718bd0fd0679a9b91623ffc7fcdbcf041caa49f7e71c26f69366ef","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
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

[Collection("api")]
public class ManageBookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TheManageLinkShowsTheBookedTime()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingResponse>(
            $"/api/booking/manage/{booking.ManageToken}");

        Assert.NotNull(view);
        Assert.Equal("Amara Novak", view!.AttendeeName);
        Assert.Contains("-", view.Display);

        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/booking/manage/{booking.ManageToken}"));
        var links = document.RootElement.GetProperty("_links");
        var cancel = links.GetProperty("cancel");
        Assert.Equal(
            $"/api/booking/manage/{Uri.EscapeDataString(booking.ManageToken)}/cancel",
            cancel.GetProperty("href").GetString());
        Assert.Equal("POST", cancel.GetProperty("method").GetString());
        Assert.Equal("cancelManagedBooking", cancel.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task CancellingWithoutRebookingReleasesTheBooking()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(outcome!.Reinvited);

        var afterwards = await client.GetAsync($"/api/booking/manage/{booking.ManageToken}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.NotYetInvited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        Assert.Empty(persisted.PendingInviteIds);
    }

    [Fact]
    public async Task CancelAndRebookIssuesAFreshInvite()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(outcome!.Reinvited);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.Invited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        var inviteId = Assert.Single(persisted.PendingInviteIds);
        Assert.NotEqual(booking.OriginalInviteId, inviteId);
    }

    [Fact]
    public async Task AnUnknownManageTokenIsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/manage/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingFixture> GivenABooking()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var eventId = view!.Options[0].EventId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = eventId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.AttendeeId,
            invite.Id,
            eventId);
    }

    private async Task<AttendeeInviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var eventIds = new List<Guid>();
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
            eventIds.Add(eventId);
        }

        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0));
        await context.SaveChangesAsync();

        return new AttendeeInviteFixture(issued.Token, attendee.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var attendee = await context.Attendees.SingleAsync(c => c.Id == booking.AttendeeId);
        var capacity = await context.EventCapacities.SingleAsync(c =>
            c.EventId == booking.EventId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.AttendeeId == booking.AttendeeId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            attendee.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record AttendeeInviteFixture(string Token, Guid AttendeeId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid AttendeeId,
        Guid OriginalInviteId,
        Guid EventId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        AttendeeStatus AttendeeStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingResponse(DateOnly Date, string Display, string AttendeeName);

    private sealed record CancelResponse(bool Reinvited);
}
`````

## before — tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs — 1/1

<!-- retirement-file: {"id":25,"file":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","beforeSha":"021d06e9cdcc2ecf0fac0eeddfda04c2f8f7fe4244a056bfce40a3f932d3d7c1","afterSha":"e821b2a0f779560c25b2fc8df821a72581da0137858a1d20a66f2d636d108076","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
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

/// <summary>Verifies recently past events keep the scoped, minimum-data workspace boundary.</summary>
[Collection("api")]
public sealed class RecentPastWorkspaceBoundaryTests(ApiFactory factory)
{
    /// <summary>Verifies Manager and AppointmentStaff both reach a recently past eventItem.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRolesReachRecentlyPastEvents(Role role)
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [role], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = await client.GetAsync("/api/appointment-workspace/events");
        using var detail = await client.GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains(data.EventId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a recently past event outside trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task CrossTypeRecentlyPastEventIsNotFound()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies unscoped callers are denied recently past events without data.</summary>
    [Fact]
    public async Task UnassignedCallerIsForbiddenRecentlyPastEvent()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a recently past event keeps the exact approved minimum-data shape.</summary>
    [Fact]
    public async Task RecentlyPastResponsesKeepTheApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        int daysBeforeToday)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(-daysBeforeToday), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
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

## after — tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs — 1/1

<!-- retirement-file: {"id":25,"file":"tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs","beforeSha":"021d06e9cdcc2ecf0fac0eeddfda04c2f8f7fe4244a056bfce40a3f932d3d7c1","afterSha":"e821b2a0f779560c25b2fc8df821a72581da0137858a1d20a66f2d636d108076","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
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

/// <summary>Verifies recently past events keep the scoped, minimum-data workspace boundary.</summary>
[Collection("api")]
public sealed class RecentPastWorkspaceBoundaryTests(ApiFactory factory)
{
    /// <summary>Verifies Manager and AppointmentStaff both reach a recently past eventItem.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRolesReachRecentlyPastEvents(Role role)
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [role], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = await client.GetAsync("/api/appointment-workspace/events");
        using var detail = await client.GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains(data.EventId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a recently past event outside trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task CrossTypeRecentlyPastEventIsNotFound()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies unscoped callers are denied recently past events without data.</summary>
    [Fact]
    public async Task UnassignedCallerIsForbiddenRecentlyPastEvent()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/events/{data.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a recently past event keeps the exact approved minimum-data shape.</summary>
    [Fact]
    public async Task RecentlyPastResponsesKeepTheApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/events/{data.EventId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "eventId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "attendeeName", "attendeeEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.DoesNotContain("attendeeId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
    }

    private async Task<(Guid EventId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        int daysBeforeToday)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var eventItem = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(-daysBeforeToday), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? AttendeeGroupIds.GroundOperationsAgent
            : AttendeeGroupIds.Pilots;
        var group = context.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
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

## before — tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","beforeSha":"aedac3818b8f765a3c3613b2be5989406312a8aaacff2d0a0bddbca85f2f15b5","afterSha":"7cbfdcf9e286abe3af5e40790ca5ff61de8a210cb83d4e8e226ffbf7465eac5b","side":"before","part":1,"parts":1} -->

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

/// <summary>Verifies recovery-invite start/cancel routes, auth, and the delivery-outcome projection.</summary>
[Collection("api")]
public sealed class RecoveryInviteEndpointTests(ApiFactory factory)
{
    private sealed record DeliveryOutcomeResponse(
        Guid InviteId,
        IReadOnlyList<Guid> AppointmentTypeIds,
        bool EmailSent);

    /// <summary>A Coordinator starts recovery for a missed appointment and gets the outcome.</summary>
    [Fact]
    public async Task CoordinatorStartsRecoveryAndReceivesDeliveryOutcome()
    {
        var attendeeId = await GivenAttendeeWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        var body = await response.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body!.InviteId);
        Assert.Contains(AppointmentTypeIds.MedicalCheckUp, body.AppointmentTypeIds);
    }

    /// <summary>A Coordinator learns nothing is recoverable through the stable error code.</summary>
    [Fact]
    public async Task CoordinatorReceivesConflictWhenNothingRecoverable()
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

        using var response = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "recovery_not_available",
            problem.RootElement.GetProperty("title").GetString());
    }

    /// <summary>Only Coordinators may start or cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Anonymous callers cannot start a recovery invite.</summary>
    [Fact]
    public async Task AnonymousCannotStartRecovery()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A Coordinator cancels a pending recovery, and a second cancel conflicts.</summary>
    [Fact]
    public async Task CoordinatorCancelsPendingRecovery()
    {
        var attendeeId = await GivenAttendeeWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var started = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        var outcome = await started.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        using var cancelled = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{outcome!.InviteId}");
        using var again = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{outcome.InviteId}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>Cancelling an unknown recovery invite is not found.</summary>
    [Fact]
    public async Task CancellingUnknownRecoveryReturnsNotFound()
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

        using var response = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Only Coordinators may cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Seeds a booked attendee with one Medical Check-up no-show and a spare eventItem.</summary>
    private async Task<Guid> GivenAttendeeWithNoShowAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[]
        {
            new TimeOnly(11, 0),
            new TimeOnly(13, 0),
            new TimeOnly(15, 0),
        }
        .Select(start => EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(2), start),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
        .ToList();
        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, booking, appointment);
        await context.SaveChangesAsync();

        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        using var marked = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{appointment.Id}/status",
            new { status = "NoShow", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        return attendee.Id;
    }
}
`````

## after — tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs","beforeSha":"aedac3818b8f765a3c3613b2be5989406312a8aaacff2d0a0bddbca85f2f15b5","afterSha":"7cbfdcf9e286abe3af5e40790ca5ff61de8a210cb83d4e8e226ffbf7465eac5b","side":"after","part":1,"parts":1} -->

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

/// <summary>Verifies recovery-invite start/cancel routes, auth, and the delivery-outcome projection.</summary>
[Collection("api")]
public sealed class RecoveryInviteEndpointTests(ApiFactory factory)
{
    private sealed record DeliveryOutcomeResponse(
        Guid InviteId,
        IReadOnlyList<Guid> AppointmentTypeIds,
        bool EmailSent);

    /// <summary>A Coordinator starts recovery for a missed appointment and gets the outcome.</summary>
    [Fact]
    public async Task CoordinatorStartsRecoveryAndReceivesDeliveryOutcome()
    {
        var attendeeId = await GivenAttendeeWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        var body = await response.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, body!.InviteId);
        Assert.Contains(AppointmentTypeIds.MedicalCheckUp, body.AppointmentTypeIds);
    }

    /// <summary>A Coordinator learns nothing is recoverable through the stable error code.</summary>
    [Fact]
    public async Task CoordinatorReceivesConflictWhenNothingRecoverable()
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

        using var response = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "recovery_not_available",
            problem.RootElement.GetProperty("title").GetString());
    }

    /// <summary>Only Coordinators may start or cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotStartRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Anonymous callers cannot start a recovery invite.</summary>
    [Fact]
    public async Task AnonymousCannotStartRecovery()
    {
        factory.SignedInAs = null;

        using var response = await factory.CreateClient().PostAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A Coordinator cancels a pending recovery, and a second cancel conflicts.</summary>
    [Fact]
    public async Task CoordinatorCancelsPendingRecovery()
    {
        var attendeeId = await GivenAttendeeWithNoShowAsync();
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        using var started = await client.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null);
        var outcome = await started.Content.ReadFromJsonAsync<DeliveryOutcomeResponse>();

        using var cancelled = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{outcome!.InviteId}");
        using var again = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{outcome.InviteId}");

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>Cancelling an unknown recovery invite is not found.</summary>
    [Fact]
    public async Task CancellingUnknownRecoveryReturnsNotFound()
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

        using var response = await client.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Only Coordinators may cancel a recovery invite.</summary>
    [Fact]
    public async Task AdminCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Admin);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>A scoped Manager still lacks the Coordinator-only recovery capability.</summary>
    [Fact]
    public async Task ManagerCannotCancelRecovery()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);

        using var response = await factory.CreateClient().DeleteAsync(
            $"/api/attendees/{Guid.NewGuid()}/recovery-invites/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Seeds a booked attendee with one Medical Check-up no-show and a spare eventItem.</summary>
    private async Task<Guid> GivenAttendeeWithNoShowAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtTransitionalLocation;
        var bookedEvent = EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(-1), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var spareEvents = new[]
        {
            new TimeOnly(11, 0),
            new TimeOnly(13, 0),
            new TimeOnly(15, 0),
        }
        .Select(start => EventFixture.Create(
            Guid.NewGuid(), new EventWindow(today.AddDays(2), start, 240),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20)))
        .ToList();
        var group = context.AttendeeGroups
            .Include(g => g.Requirements)
            .Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [bookedEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, bookedEvent.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.MedicalCheckUp);
        context.AddRange(bookedEvent);
        context.AddRange(spareEvents);
        context.AddRange(attendee, booking, appointment);
        await context.SaveChangesAsync();

        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.MedicalCheckUp);
        using var marked = await factory.CreateClient().PutAsJsonAsync(
            $"/api/appointment-workspace/appointments/{appointment.Id}/status",
            new { status = "NoShow", expectedVersion = 1 });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        return attendee.Id;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs","beforeSha":"a64f68d23562418f2002819c71536ebf61859c64c57903503affc16a63d2a50f","afterSha":"d393c6428643e4be07ab3b666a2cea215944b484a814c053947ab4b48f1d23b6","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies late outcomes on past events follow the existing timing and correction rules.</summary>
public sealed class LateNoShowOutcomeTests
{
    /// <summary>Verifies Expected to NoShow succeeds the day after the event date with version and audit.</summary>
    [Fact]
    public async Task NoShowDayAfterEventDateSucceeds()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.NoShow, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        Assert.NotNull(result.Value.OutcomeAt);
        Assert.Null(result.Value.CheckedInAt);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentMarkedNoShow, entry.Action);
    }

    /// <summary>Verifies check-in is rejected once the event date has passed.</summary>
    [Fact]
    public async Task CheckInAfterEventDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies a late NoShow corrects back to Expected while parents remain active.</summary>
    [Fact]
    public async Task LateNoShowCorrectsToExpected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.Expected, result.Value.Status);
        Assert.Equal(3, result.Value.Version);
        Assert.Null(result.Value.OutcomeAt);
        Assert.Contains(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = scenario.Appointment.Id,
        Status = status,
        ExpectedVersion = version,
    };

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork(operations);
        var clock = new FakeClock(now);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, attendee, eventItem, appointment, booking, audit, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Attendee Attendee,
        Event Event,
        BookingAppointment Appointment,
        Booking Booking,
        RecordingAuditLogger Audit,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## after — tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs","beforeSha":"a64f68d23562418f2002819c71536ebf61859c64c57903503affc16a63d2a50f","afterSha":"d393c6428643e4be07ab3b666a2cea215944b484a814c053947ab4b48f1d23b6","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies late outcomes on past events follow the existing timing and correction rules.</summary>
public sealed class LateNoShowOutcomeTests
{
    /// <summary>Verifies Expected to NoShow succeeds the day after the event date with version and audit.</summary>
    [Fact]
    public async Task NoShowDayAfterEventDateSucceeds()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.NoShow, result.Value.Status);
        Assert.Equal(2, result.Value.Version);
        Assert.NotNull(result.Value.OutcomeAt);
        Assert.Null(result.Value.CheckedInAt);
        var entry = Assert.Single(scenario.Audit.Entries);
        Assert.Equal(AuditAction.AppointmentMarkedNoShow, entry.Action);
    }

    /// <summary>Verifies check-in is rejected once the event date has passed.</summary>
    [Fact]
    public async Task CheckInAfterEventDateIsRejected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.CheckedIn, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingAppointmentStatus.Expected, scenario.Appointment.Status);
    }

    /// <summary>Verifies a late NoShow corrects back to Expected while parents remain active.</summary>
    [Fact]
    public async Task LateNoShowCorrectsToExpected()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));
        await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.NoShow, 1),
            CancellationToken.None);

        var result = await scenario.Handler.HandleAsync(
            Command(scenario, BookingAppointmentStatus.Expected, 2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.Expected, result.Value.Status);
        Assert.Equal(3, result.Value.Version);
        Assert.Null(result.Value.OutcomeAt);
        Assert.Contains(
            scenario.Audit.Entries, e => e.Action == AuditAction.AppointmentStatusCorrected);
    }

    private static UpdateBookingAppointmentStatusCommand Command(
        Scenario scenario,
        BookingAppointmentStatus status,
        long version) => new()
    {
        StaffUserId = scenario.StaffUserId,
        BookingAppointmentId = scenario.Appointment.Id,
        Status = status,
        ExpectedVersion = version,
    };

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0), 240),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var audit = new RecordingAuditLogger();
        var unitOfWork = new FakeUnitOfWork(operations);
        var clock = new FakeClock(now);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            audit,
            unitOfWork,
            clock);
        return new Scenario(
            staff, attendee, eventItem, appointment, booking, audit, operations, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Attendee Attendee,
        Event Event,
        BookingAppointment Appointment,
        Booking Booking,
        RecordingAuditLogger Audit,
        TransactionOperationLog Operations,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````

## before — tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs — 1/1

<!-- retirement-file: {"id":28,"file":"tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs","beforeSha":"a288d2b191298af351f093c253075a1a7bcec187fb9bf3bc30875f6071ee80c3","afterSha":"cdac86102ec21642056f6f8c4bf1656f732b8f8ec4742eb81a6ccc63a3209019","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies a late-recorded outcome makes its requirement recoverable.</summary>
public sealed class RecentPastRecoveryEligibilityTests
{
    /// <summary>Verifies the late NoShow latest attempt reads as outstanding and recoverable.</summary>
    [Fact]
    public async Task LateNoShowMakesRequirementRecoverable()
    {
        var scenario = GivenScenario(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero));
        var outcome = await scenario.Handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = scenario.StaffUserId,
                BookingAppointmentId = scenario.Appointment.Id,
                Status = BookingAppointmentStatus.NoShow,
                ExpectedVersion = 1,
            },
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var readiness = new AttendeeReadinessCalculator().Calculate(
            new AttendeeReadinessSnapshot(
                scenario.Attendee.Id,
                Guid.NewGuid(),
                [AppointmentTypeIds.DrugAndAlcoholTesting],
                scenario.Booking.Id,
                [
                    new AttendeeReadinessAttempt(
                        scenario.Appointment.Id,
                        AppointmentTypeIds.DrugAndAlcoholTesting,
                        scenario.Appointment.Status,
                        scenario.Booking.Id,
                        scenario.Booking.Status,
                        outcome.Value.OutcomeAt!.Value),
                ]));

        Assert.Equal(AttendeeReadinessCode.AppointmentsOutstanding, readiness.Code);
        var outstanding = Assert.Single(readiness.OutstandingAppointmentTypes);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>Builds a DAT-only group; the scenario needs a mapping, not an identity.</summary>
    private static AttendeeGroup DatOnly() =>
        AttendeeGroup.Define(
            Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);

    private static Scenario GivenScenario(DateTimeOffset now)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting));
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com", DatOnly());
        var operations = new TransactionOperationLog();
        var attendees = new InMemoryAttendeeRepository(operations);
        attendees.Add(attendee);
        var eventItem = EventFixture.Create(
            Guid.NewGuid(),
            new EventWindow(new DateOnly(2026, 9, 7), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(value => value, _ => 10));
        var events = new InMemoryEventRepository(operations);
        events.Add(eventItem);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), attendee.Id, "invite-token", now.AddDays(1),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], attendee.RequiredAppointmentTypeIds, 0);
        var invites = new InMemoryInviteRepository(operations);
        invites.Add(invite);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, eventItem.Id, "manage-token", now.AddDays(-1));
        var bookings = new InMemoryBookingRepository(operations);
        bookings.Add(booking);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting);
        var appointments = new InMemoryBookingAppointmentRepository(bookings, operations);
        appointments.Add(appointment);
        var handler = new UpdateBookingAppointmentStatusHandler(
            new StaffAccessAuthorizer(profiles),
            appointments,
            bookings,
            attendees,
            invites,
            events,
            new RecoveryBookingOutcomeCoordinator(),
            new RecordingAuditLogger(),
            new FakeUnitOfWork(operations),
            new FakeClock(now));
        return new Scenario(staff, attendee, booking, appointment, handler);
    }

    private sealed record Scenario(
        Guid StaffUserId,
        Attendee Attendee,
        Booking Booking,
        BookingAppointment Appointment,
        UpdateBookingAppointmentStatusHandler Handler);
}
`````
