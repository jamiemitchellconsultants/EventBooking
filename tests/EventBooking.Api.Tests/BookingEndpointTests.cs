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
            Guid.NewGuid(),
            "Amara Novak",
            $"{Guid.NewGuid():N}@mail.com",
            pilots,
            ProposalFixture.Now);
        attendee.MarkInvited(ProposalFixture.Now);
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(TokenPurpose.Book, inviteId, Invite.InitialTokenVersion);
        context.Invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            [ProposalFixture.LocationId],
            offeredEvents.Select(eventItem => eventItem.Id).ToList(),
            attendee.RequiredAppointmentTypeIds,
            0));

        await context.SaveChangesAsync();

        return new InviteFixture(issued, inviteId);
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
