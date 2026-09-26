using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.SelfRegistrations;
using EventBooking.Domain.Access;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.SelfRegistrations;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class SelfRegistrationJourneyTests(ApiFactory factory)
{
    [Fact]
    public async Task SubmitViewConfirmDispatchesTheConfirmationEmail()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var groupId = await CreateGroupAsync(client, "Journey intake");
        var eventId = await SeedEventAsync(new DateOnly(2030, 8, 10));
        await AddEventAsync(client, groupId, eventId, 1);
        await OpenMemberAsync(client, groupId, eventId, 2);
        await OpenGroupAsync(client, groupId, "Journey intake", 3);
        var before = await CapacitiesAsync(eventId);

        factory.SignedInAs = null;
        var anonymous = factory.CreateClient();
        var submitted = await anonymous.PostAsJsonAsync(
            $"/api/public/event-groups/{groupId}/events/{eventId}/registrations", new
            {
                attendeeGroupId = AttendeeGroupIds.CabinCrew,
                name = "Robin Public",
                email = "robin@example.com",
            });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var receipt = await BodyAsync(submitted);
        Assert.False(receipt.TryGetProperty("confirmationToken", out _));
        var requestId = receipt.GetProperty("requestId").GetGuid();
        var token = await EmailedTokenAsync(requestId, "robin@example.com");

        using (var scope = factory.Services.CreateScope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            Assert.Equal(
                token,
                tokens.Issue(TokenPurpose.Registration, requestId, 1));
        }

        var viewed = await anonymous.GetAsync(
            $"/api/public/event-groups/confirm/{Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, viewed.StatusCode);
        var summary = await BodyAsync(viewed);
        Assert.Equal(groupId, summary.GetProperty("eventGroupId").GetGuid());
        Assert.Equal(eventId, summary.GetProperty("eventId").GetGuid());
        Assert.Equal("Journey intake", summary.GetProperty("eventGroupTitle").GetString());

        var confirmed = await anonymous.PostAsync(
            $"/api/public/event-groups/confirm/{Uri.EscapeDataString(token)}", null);
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
        var bookingId = (await BodyAsync(confirmed)).GetProperty("bookingId").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            var attendee = Assert.Single(
                await db.Attendees.Where(x => x.Email == "robin@example.com").ToListAsync());
            var booking = Assert.Single(
                await db.Bookings.Where(x => x.AttendeeId == attendee.Id).ToListAsync());
            Assert.Equal(bookingId, booking.Id);
            Assert.Equal(attendee.Id, booking.AttendeeId);
            Assert.Single(await db.Invites.Where(x => x.AttendeeId == attendee.Id).ToListAsync());
            Assert.Equal(3, await db.BookingAppointments.CountAsync(x => x.BookingId == bookingId));
            var after = await CapacitiesAsync(eventId);
            Assert.Equal(
                before.OrderBy(x => x.Key).Select(x => x.Value - 1),
                after.OrderBy(x => x.Key).Select(x => x.Value));
            var delivery = Assert.Single(await db.EmailLogs
                .Where(x => x.TemplateName == EmailTemplate.BookingConfirmation
                    && x.BookingId == bookingId)
                .ToListAsync());
            Assert.Equal(EmailStatus.Pending, delivery.Status);

            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
            Assert.Equal(1, await dispatcher.DispatchOneAsync(delivery.Id));
        }

        var posted = factory.EmailTransport.Sent.Last();
        Assert.Equal("robin@example.com", posted.Recipient);
        Assert.Contains("/manage/", posted.TextBody);

        var reconfirm = await anonymous.PostAsync(
            $"/api/public/event-groups/confirm/{Uri.EscapeDataString(token)}", null);
        Assert.Equal(HttpStatusCode.Conflict, reconfirm.StatusCode);
    }

    [Fact]
    public async Task RetentionPurgesTerminalRequestsPastThirtyDaysWithTheirEmails()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(Role.Coordinator);
        var client = factory.CreateClient();
        var groupId = await CreateGroupAsync(client, "Retention intake");
        var eventId = await SeedEventAsync(new DateOnly(2030, 9, 10));
        await AddEventAsync(client, groupId, eventId, 1);
        await OpenMemberAsync(client, groupId, eventId, 2);
        await OpenGroupAsync(client, groupId, "Retention intake", 3);

        factory.SignedInAs = null;
        var anonymous = factory.CreateClient();
        var oldToken = await SubmitAsync(anonymous, groupId, eventId, "Old Request", "old@example.com");
        var confirmOld = await anonymous.PostAsync(
            $"/api/public/event-groups/confirm/{Uri.EscapeDataString(oldToken)}", null);
        Assert.Equal(HttpStatusCode.Created, confirmOld.StatusCode);
        var liveToken = await SubmitAsync(anonymous, groupId, eventId, "Live Request", "live@example.com");
        var expiringToken = await SubmitAsync(
            anonymous, groupId, eventId, "Expiring Request", "expiring@example.com");
        _ = liveToken;
        _ = expiringToken;

        Guid oldRequestId;
        Guid expiringRequestId;
        List<Guid> oldBookingIds;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            oldRequestId = await db.PendingRegistrations
                .Where(x => x.Email == "old@example.com")
                .Select(x => x.RequestId).SingleAsync();
            var oldAttendeeId = await db.Attendees
                .Where(x => x.Email == "old@example.com")
                .Select(x => x.Id).SingleAsync();
            oldBookingIds = await db.Bookings
                .Where(x => x.AttendeeId == oldAttendeeId && x.EventId == eventId)
                .Select(x => x.Id).ToListAsync();
            var old = await db.PendingRegistrations.SingleAsync(x => x.RequestId == oldRequestId);
            var past = DateTimeOffset.UtcNow.AddDays(-30).AddHours(-1);
            db.Entry(old).Property("TerminalAt").CurrentValue = past;
            var email = await db.EmailLogs.SingleAsync(x => x.SelfRegistrationId == oldRequestId);
            db.Entry(email).Property("SentAt").CurrentValue = past;
            var expiring = await db.PendingRegistrations.SingleAsync(
                x => x.Email == "expiring@example.com");
            expiringRequestId = expiring.RequestId;
            db.Entry(expiring).Property("ExpiresAt").CurrentValue =
                DateTimeOffset.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var maintenance =
                scope.ServiceProvider.GetRequiredService<SelfRegistrationMaintenance>();
            await maintenance.RunAsync(CancellationToken.None);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
            Assert.Empty(await db.PendingRegistrations
                .Where(x => x.RequestId == oldRequestId).ToListAsync());
            Assert.Empty(await db.EmailLogs
                .Where(x => x.SelfRegistrationId == oldRequestId).ToListAsync());
            Assert.NotEmpty(await db.Attendees
                .Where(x => x.Email == "old@example.com").ToListAsync());
            Assert.NotEmpty(await db.Bookings
                .Where(x => oldBookingIds.Contains(x.Id)).ToListAsync());
            var live = await db.PendingRegistrations.SingleAsync(
                x => x.Email == "live@example.com");
            Assert.Equal(SelfRegistrationStatus.Pending, live.Status);
            var expired = await db.PendingRegistrations.SingleAsync(
                x => x.Email == "expiring@example.com");
            Assert.Equal(SelfRegistrationStatus.Expired, expired.Status);
            var audit = Assert.Single(await db.AuditLogs
                .Where(x => x.Action == AuditAction.SelfRegistrationExpired
                    && x.EntityId == expiringRequestId).ToListAsync());
            Assert.Equal(expired.RequestId, audit.EntityId);
            Assert.DoesNotContain("expiring@example.com", audit.Details ?? string.Empty);
        }
    }

    private async Task<string> SubmitAsync(
        HttpClient anonymous, Guid groupId, Guid eventId, string name, string email)
    {
        var submitted = await anonymous.PostAsJsonAsync(
            $"/api/public/event-groups/{groupId}/events/{eventId}/registrations", new
            {
                attendeeGroupId = AttendeeGroupIds.CabinCrew,
                name,
                email,
            });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var requestId = (await BodyAsync(submitted)).GetProperty("requestId").GetGuid();
        return await EmailedTokenAsync(requestId, email);
    }

    private async Task<string> EmailedTokenAsync(Guid requestId, string recipient)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var delivery = await db.EmailLogs
            .Where(x => x.SelfRegistrationId == requestId)
            .OrderByDescending(x => x.SentAt)
            .FirstAsync();
        Assert.Null(delivery.AttendeeId);
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        Assert.Equal(1, await dispatcher.DispatchOneAsync(delivery.Id));

        var mail = factory.EmailTransport.Sent.Last(x => x.Recipient == recipient);
        var match = System.Text.RegularExpressions.Regex.Match(
            mail.TextBody, @"/event-groups/confirm/(\S+)");
        Assert.True(match.Success, mail.TextBody);
        return match.Groups[1].Value;
    }

    private async Task<Dictionary<Guid, int>> CapacitiesAsync(Guid eventId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        return await db.EventCapacities
            .Where(x => x.EventId == eventId)
            .ToDictionaryAsync(x => x.AppointmentTypeId, x => x.RemainingCapacity);
    }

    private async Task<Guid> CreateGroupAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/event-groups", new
        {
            title,
            description = "Choose a date",
            attendeeGroupIds = new[] { AttendeeGroupIds.CabinCrew },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await BodyAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task AddEventAsync(HttpClient client, Guid groupId, Guid eventId, long version)
    {
        var response = await client.PutAsJsonAsync(
            $"/api/event-groups/{groupId}/events/{eventId}", new { expectedVersion = version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task OpenMemberAsync(
        HttpClient client, Guid groupId, Guid eventId, long version)
    {
        var response = await client.PatchAsJsonAsync(
            $"/api/event-groups/{groupId}/events/{eventId}",
            new { isOpen = true, expectedVersion = version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task OpenGroupAsync(
        HttpClient client, Guid groupId, string title, long version)
    {
        var response = await client.PutAsJsonAsync($"/api/event-groups/{groupId}", new
        {
            title,
            description = "Choose a date",
            attendeeGroupIds = new[] { AttendeeGroupIds.CabinCrew },
            isOpen = true,
            expectedVersion = version,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> SeedEventAsync(DateOnly date)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        foreach (var type in proposal.ListedAppointmentTypeIds) proposal.Accept(type, Guid.NewGuid(), 5);
        var eventId = Guid.NewGuid();
        db.EventProposals.Add(proposal);
        db.Events.Add(Event.CreateFrom(eventId, proposal));
        await db.SaveChangesAsync();
        return eventId;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
