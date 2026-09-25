// src/EventBooking.SeedData/LoadFixtureSeeder.cs (complete)
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

public sealed record LoadFixtureManifest(Guid EventId, IReadOnlyList<string> BookTokens);

public sealed class LoadFixtureSeeder(
    EventBookingDbContext database,
    IAttendeeGroupRepository groups,
    ITokenService tokens,
    IClock clock,
    IEventWindowZones zones)
{
    private static readonly Guid ProposalId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid EventId = Guid.Parse("80000000-0000-0000-0000-000000000002");

    public async Task<LoadFixtureManifest> RunAsync(CancellationToken ct)
    {
        if (await database.Events.AnyAsync(x => x.Id == EventId, ct)
            || await database.Attendees.AnyAsync(x => x.Email.StartsWith("load.attendee."), ct))
            throw new SeedException("The load fixture already exists; recreate the disposable load stack.");

        var location = await database.Locations.SingleAsync(x => x.Code == "LONDON", ct);
        var type = await database.AppointmentTypes.SingleAsync(x => x.Code == "IND", ct);
        var group = await groups.GetByCodeAsync("IND_ONLY", ct)
            ?? throw new SeedException("The IND_ONLY demo group is missing; run --demo first.");
        if (!location.IsActive || !type.IsActive || group.RequiredAppointmentTypeIds.Count != 1
            || group.RequiredAppointmentTypeIds[0] != type.Id)
            throw new SeedException("The IND_ONLY load fixture inputs have changed.");

        var data = DemoSeedSpec.Build();
        var alternativeIds = data.Events
            .Where(x => x.LocationCode == "LONDON" && x.TypeCodes.Contains("IND"))
            .Take(2).Select(x => x.Id).ToArray();
        if (alternativeIds.Length != 2 || await database.Events
            .CountAsync(x => alternativeIds.Contains(x.Id), ct) != 2)
            throw new SeedException("Two future London IND alternatives are required.");

        var date = zones.LocalDateOf(clock.UtcNow, location.TimeZoneId).AddDays(28);
        var window = new EventWindow(date, new TimeOnly(10, 0), 60);
        var manager = DemoSeedSpec.ManagerForType()[type.Id];
        var proposal = EventProposal.Propose(
            ProposalId, location.Id, true, location.TimeZoneId, window, zones, clock.UtcNow,
            [new ProposableAppointmentType(type.Id, type.Code, true, true)],
            type.Id, manager, 100);
        var eventItem = Event.CreateFrom(EventId, proposal);
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        database.EventProposals.Add(proposal);
        database.Events.Add(eventItem);
        await database.SaveChangesAsync(ct);

        var bookTokens = new List<string>(500);
        for (var number = 1; number <= 500; number++)
        {
            var attendee = Attendee.Create(
                Id(81, number), $"Load participant {number}",
                $"load.attendee.{number:000}@example.test", group, clock.UtcNow);
            var invite = Invite.CreateInitial(
                InviteId(number), attendee.Id, clock.UtcNow.AddDays(7), [location.Id],
                [EventId, .. alternativeIds], group.RequiredAppointmentTypeIds, 0);
            attendee.MarkInvited(clock.UtcNow);
            database.Attendees.Add(attendee);
            database.Invites.Add(invite);
            bookTokens.Add(tokens.Issue(TokenPurpose.Book, invite.Id, invite.TokenVersion));
        }
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new LoadFixtureManifest(EventId, bookTokens);
    }

    private static Guid Id(int family, int number) =>
        Guid.Parse($"{family:00}000000-0000-0000-0000-{number:000000000000}");

    // Task 21 partitions attendee-token requests on the first 12 encoded characters. Put the
    // sequence in the first GUID segment so all 500 tokens land in separate 10/min partitions.
    private static Guid InviteId(int number) =>
        Guid.Parse($"{number:00000000}-0000-0000-0000-820000000000");
}
