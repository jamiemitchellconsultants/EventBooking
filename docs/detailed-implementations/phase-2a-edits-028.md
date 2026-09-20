# 02a — Deterministic attendee links and the token version counter, edits 28 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs — 1/1

<!-- retirement-file: {"id":69,"file":"tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs","beforeSha":"5b075df7aca19076308a373a4ad04aa74d94ed7eb5b36a4eb9307da4b14cc2bf","afterSha":"68b44c278b930367a050596b81bcb9ad040f659c6db005c6677ab2de00197a61","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class DashboardQueryTests(PostgresFixture fixture)
{
    private sealed class MovableLondonClock(DateTimeOffset now) : IClock
    {
        private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

        public DateTimeOffset UtcNow { get; set; } = now;

        /// <summary>Gets the current instant converted to the London transitional-location time zone.</summary>
        public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, London);

        public DateOnly TodayAtTransitionalLocation => DateAtTransitionalLocation(UtcNow);

        public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, London).DateTime);

        public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
            TimeZoneInfo.ConvertTime(instant, London);
    }

    [Fact]
    public async Task AwaitingAttendeesUseTransitionalLocationDatesAcrossTheUtcMidnightBoundary()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var awaitingGroup = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_DAT_MED", "Dashboard DAT MED", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp]);
            write.AttendeeGroups.Add(awaitingGroup);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "C. Diallo",
                "c.diallo@mail.com",
                awaitingGroup,
                clock.UtcNow);
            attendee.MarkAwaitingAvailability(clock.UtcNow);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 23, 30, 0, TimeSpan.Zero);

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .AwaitingAvailabilityAsync(CancellationToken.None));

        Assert.Equal("C. Diallo", row.Name);
        Assert.Equal(new[] { "DAT", "MED" }, row.RequiredCodes);
        Assert.Equal(new DateOnly(2026, 9, 3), row.WaitingSince);
        Assert.Equal(1, row.DaysWaiting);
    }

    [Fact]
    public async Task TheStatusStampIsWrittenOnAddAndMovesOnlyWhenStatusMoves()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI", "Dashboard UNI", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "B. Chen",
                "b.chen@mail.com",
                uniformOnly,
                clock.UtcNow);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var added = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, added.StatusChangedAt);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("B. Chen-Smith", "b.chen@mail.com");
            await rename.SaveChangesAsync();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var renamed = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                renamed.StatusChangedAt);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability(clock.UtcNow);
            await statusChange.SaveChangesAsync();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changed.StatusChangedAt);
    }

    [Fact]
    public async Task TheSynchronousSavePathStampsAddsAndStatusChangesButNotUnrelatedEdits()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero));

        Guid attendeeId;
        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_TWO", "Dashboard UNI two", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "S. Patel",
                "s.patel@mail.com",
                uniformOnly,
                clock.UtcNow);
            write.Attendees.Add(attendee);
            write.SaveChanges();
            attendeeId = attendee.Id;
        }

        await using (var addedRead = NewContext(clock))
        {
            var attendee = await addedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(clock.UtcNow, attendee.StatusChangedAt);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var rename = NewContext(clock))
        {
            var attendee = await rename.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.UpdateDetails("S. Patel-Jones", "s.patel@mail.com");
            rename.SaveChanges();
        }

        await using (var renamedRead = NewContext(clock))
        {
            var attendee = await renamedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
            Assert.Equal(
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                attendee.StatusChangedAt);
        }

        clock.UtcNow = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        await using (var statusChange = NewContext(clock))
        {
            var attendee = await statusChange.Attendees.SingleAsync(c => c.Id == attendeeId);
            attendee.MarkAwaitingAvailability(clock.UtcNow);
            statusChange.SaveChanges();
        }

        await using var changedRead = NewContext(clock);
        var changed = await changedRead.Attendees.SingleAsync(c => c.Id == attendeeId);
        Assert.Equal(clock.UtcNow, changed.StatusChangedAt);
    }

    [Fact]
    public async Task TheFollowUpListUsesTheTransitionalLocationDayTheAutoRetryGaveUp()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 2, 23, 30, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var groundOps = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.GroundOperationsAgent);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "D. Reyes",
                "d.reyes@mail.com",
                groundOps,
                clock.UtcNow);
            attendee.MarkInvited(clock.UtcNow);
            attendee.MarkNoResponse(clock.UtcNow);
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(await new DashboardQueries(read, clock)
            .NoResponseAsync(CancellationToken.None));

        Assert.Equal("D. Reyes", row.Name);
        Assert.Equal(new DateOnly(2026, 9, 3), row.GaveUpOn);
    }

    [Fact]
    public async Task TheEventsOverviewShowsCapacityAndActiveBookingCount()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var events = new[]
            {
                EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 22), new TimeOnly(9, 0)),
                EventFor(new DateOnly(2026, 9, 23), new TimeOnly(9, 0)),
            };
            events[0].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();

            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "E. Martin",
                "e.martin@mail.com",
                pilots,
                clock.UtcNow);
            attendee.MarkInvited(clock.UtcNow);
            var invite = Invite.CreateInitial(
                Guid.NewGuid(),
                attendee.Id,
                clock.UtcNow.AddDays(4),
                [ProposalFixture.LocationId],
                events.Select(s => s.Id),
                attendee.RequiredAppointmentTypeIds,
                0);
            var booking = Booking.Create(
                Guid.NewGuid(), invite, events[0].Id, clock.UtcNow);

            write.Events.AddRange(events);
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            write.Bookings.Add(booking);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None);
        var row = rows.Single(s => s.Date == new DateOnly(2026, 9, 21));

        Assert.Equal(new TimeOnly(13, 0), row.EndTime);
        Assert.Equal(1, row.ActiveBookings);
        Assert.Equal(new[] { "DAT", "MED", "UNI" }, row.Capacities.Select(c => c.Code));
        var drugAndAlcohol = row.Capacities.Single(c => c.Code == "DAT");
        Assert.Equal(10, drugAndAlcohol.TotalHeadcount);
        Assert.Equal(9, drugAndAlcohol.RemainingCapacity);
    }

    [Fact]
    public async Task ACancelledEventIsNotOnTheOverview()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var eventItem = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
            eventItem.CancelBeforeStart();
            write.Events.Add(eventItem);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(await new DashboardQueries(read, clock).EventsOverviewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnlyTheLatestEmailLogRowPerAttendeeIsReturned()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var attendeeId = Guid.NewGuid();

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var attendee = Attendee.Create(
                attendeeId,
                "A. Novak",
                "a.novak@mail.com",
                pilots,
                clock.UtcNow);
            write.Attendees.Add(attendee);
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeInvite,
                new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), EmailStatus.Resolved));
            write.EmailLogs.Add(EmailLog.Record(
                Guid.NewGuid(), attendeeId, EmailTemplate.AttendeeReinvite,
                new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), EmailStatus.Sent));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var row = Assert.Single(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));

        Assert.Equal(attendeeId, row.AttendeeId);
        Assert.Equal(EmailTemplate.AttendeeReinvite, row.TemplateName);
        Assert.Equal(EmailStatus.Sent, row.Status);
    }

    [Fact]
    public async Task AAttendeeWithNoEmailLogRowsIsAbsent()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));

        await using (var write = NewContext(clock))
        {
            var uniformOnly = AttendeeGroup.Define(
                Guid.NewGuid(), "DASH_UNI_THREE", "Dashboard UNI three", true,
                [AppointmentTypeIds.UniformFitting]);
            write.AttendeeGroups.Add(uniformOnly);
            write.Attendees.Add(Attendee.Create(
                Guid.NewGuid(),
                "B. Chen",
                "b.chen@mail.com",
                uniformOnly,
                clock.UtcNow));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        Assert.Empty(
            await new DashboardQueries(read, clock).LatestEmailStatusAsync(CancellationToken.None));
    }

    /// <summary>Retry visibility follows the latest delivery's current attendee and event context.</summary>
    [Fact]
    public async Task LatestEmailStatusMarksOnlyActionableDeliveryContextAsRetryable()
    {
        await fixture.ResetAsync();
        var clock = new MovableLondonClock(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
        var actionableId = Guid.NewGuid();
        var staleId = Guid.NewGuid();
        var cancelledEvent = EventFor(new DateOnly(2026, 9, 21), new TimeOnly(9, 0));
        cancelledEvent.CancelBeforeStart();
        // Production stages the cancellation notice with its booking identifier, so the
        // retryable delivery carries one; the stale delivery below omits it on purpose.
        var cancelledBookingId = Guid.NewGuid();
        var outstanding = EmailLog.RecordPending(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.EventCancelledRebookingNeeded,
            clock.UtcNow,
            bookingId: cancelledBookingId,
            eventId: cancelledEvent.Id);
        var laterSent = EmailLog.Record(
            Guid.NewGuid(),
            actionableId,
            EmailTemplate.AttendeeInvite,
            clock.UtcNow.AddMinutes(3),
            EmailStatus.Sent);

        await using (var write = NewContext(clock))
        {
            var pilots = write.AttendeeGroups.Include(g => g.Requirements).Single(g => g.Id == AttendeeGroupIds.Pilots);
            var actionableAttendee = Attendee.Create(
                actionableId,
                "Actionable",
                "actionable@mail.com",
                pilots,
                clock.UtcNow);
            actionableAttendee.MarkAwaitingAvailability(clock.UtcNow);
            var staleAttendee = Attendee.Create(
                staleId,
                "Stale",
                "stale@mail.com",
                pilots,
                clock.UtcNow);
            var cancelledInvite = Invite.CreateInitial(
                Guid.NewGuid(),
                actionableId,
                clock.UtcNow.AddDays(4),
                [ProposalFixture.LocationId],
                [cancelledEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
                actionableAttendee.RequiredAppointmentTypeIds,
                0);
            var cancelledBooking = Booking.Create(
                cancelledBookingId, cancelledInvite, cancelledEvent.Id, clock.UtcNow);
            cancelledBooking.Cancel();
            write.Attendees.AddRange(actionableAttendee, staleAttendee);
            write.Events.Add(cancelledEvent);
            write.Invites.Add(cancelledInvite);
            write.Bookings.Add(cancelledBooking);
            write.EmailLogs.AddRange(
                outstanding,
                laterSent,
                EmailLog.RecordPending(
                    Guid.NewGuid(),
                    staleAttendee.Id,
                    EmailTemplate.EventCancelledRebookingNeeded,
                    clock.UtcNow,
                    eventId: cancelledEvent.Id));
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(clock);
        var rows = await new DashboardQueries(read, clock)
            .LatestEmailStatusAsync(CancellationToken.None);

        var actionable = rows.Single(row => row.AttendeeId == actionableId);
        Assert.True(actionable.CanRetry);
        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, actionable.TemplateName);
        Assert.Equal(EmailStatus.Pending, actionable.Status);
        Assert.False(rows.Single(row => row.AttendeeId == staleId).CanRetry);

        await using (var resolve = NewContext(clock))
        {
            var persisted = await resolve.EmailLogs.SingleAsync(delivery => delivery.Id == outstanding.Id);
            persisted.MarkResolved(clock.UtcNow.AddMinutes(2));
            await resolve.SaveChangesAsync();
        }

        await using var reread = NewContext(clock);
        var terminal = (await new DashboardQueries(reread, clock)
            .LatestEmailStatusAsync(CancellationToken.None))
            .Single(row => row.AttendeeId == actionableId);
        Assert.Equal(EmailTemplate.AttendeeInvite, terminal.TemplateName);
        Assert.Equal(EmailStatus.Sent, terminal.Status);
        Assert.False(terminal.CanRetry);
    }

    private EventBookingDbContext NewContext(IClock clock)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new EventBookingDbContext(options);
    }

    private static Event EventFor(DateOnly date, TimeOnly startTime)
    {
        var proposal = ProposalFixture.Create(Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs — 1/1

<!-- retirement-file: {"id":70,"file":"tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs","beforeSha":"3b27fc5b3bd92a69af2dfe6f1add39eaad4d4e6a441eaf5c531ff382de0aea85","afterSha":"e8a240b092979cf309ddb27cdd52d800bd0b9f10d28abb1a7853a5f9259d0b40","side":"before","part":1,"parts":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Infrastructure.Tests;

public class HmacTokenServiceTests
{
    private const string SigningKey = "a-signing-key-that-is-long-enough-to-be-safe";

    private static readonly TokenOptions Options =
        new(SigningKey);

    private readonly HmacTokenService _service = new(Options);

    [Fact]
    public void AnIssuedTokenRoundTripsToItsIdentifier()
    {
        var id = Guid.NewGuid();

        var issued = _service.Issue(id);

        Assert.True(_service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
    }

    [Fact]
    public void TheStoredValueIsAHashNotTheToken()
    {
        var issued = _service.Issue(Guid.NewGuid());

        Assert.NotEqual(issued.Token, issued.TokenHash);
        Assert.Equal(issued.TokenHash, _service.Hash(issued.Token));
        Assert.Equal(64, issued.TokenHash.Length);
        Assert.DoesNotContain(issued.TokenHash, issued.Token);
    }

    [Fact]
    public void TwoTokensForTheSameIdentifierAreDifferent()
    {
        var id = Guid.NewGuid();

        var first = _service.Issue(id);
        var second = _service.Issue(id);

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
        Assert.True(_service.TryRead(first.Token, out var a));
        Assert.True(_service.TryRead(second.Token, out var b));
        Assert.Equal(a, b);
    }

    [Fact]
    public void ATamperedIdentifierFailsVerification()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');
        var forged = $"{Guid.NewGuid():N}.{parts[1]}.{parts[2]}";

        Assert.False(_service.TryRead(forged, out _));
    }

    [Fact]
    public void ATamperedSignatureFailsVerification()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');

        Assert.False(_service.TryRead($"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}", out _));
    }

    [Fact]
    public void ATokenSignedWithAnotherKeyIsRejected()
    {
        var other = new HmacTokenService(new TokenOptions("a-completely-different-signing-key-value"));
        var issued = other.Issue(Guid.NewGuid());

        Assert.False(_service.TryRead(issued.Token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("one-part")]
    [InlineData("two.parts")]
    [InlineData("a.b.c.d")]
    [InlineData("not-a-guid.nonce.signature")]
    public void AMalformedTokenIsRejectedWithoutThrowing(string? token)
    {
        Assert.False(_service.TryRead(token, out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void TheTokenIsUrlSafe()
    {
        var token = _service.Issue(Guid.NewGuid()).Token;

        Assert.Equal(token, Uri.EscapeDataString(token));
    }

    [Fact]
    public void AShortSigningKeyIsRejectedAtConstruction()
    {
        var ex = Assert.Throws<ArgumentException>(() => new HmacTokenService(new TokenOptions("too-short")));
        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public void OptionsToStringDoesNotRevealTheSigningKey()
    {
        Assert.DoesNotContain(SigningKey, Options.ToString());
    }

    [Fact]
    public void NullOptionsAreRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(null!));

        Assert.Equal("options", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    [Fact]
    public void ARuntimeNullSigningKeyIsRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(new TokenOptions(null!)));

        Assert.Equal("SigningKey", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    [Fact]
    public void ATokenWithAnUppercaseIdentifierIsRejectedEvenWhenSignedWithTheKnownKey()
    {
        var id = Guid.NewGuid();
        var nonce = "AAAAAAAAAAAAAAAAAAAAAA";
        var token = CreateKnownKeyToken(id.ToString("N").ToUpperInvariant(), nonce);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Theory]
    [InlineData("!AAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAB")]
    public void ATokenWithANonCanonicalNonceIsRejectedEvenWhenSignedWithTheKnownKey(string nonce)
    {
        var token = CreateKnownKeyToken(Guid.NewGuid().ToString("N"), nonce);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Theory]
    [InlineData("invalid-character")]
    [InlineData("padding")]
    [InlineData("wrong-length")]
    public void ATokenWithAMalformedSignatureEncodingIsRejected(string kind)
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');
        var malformedSignature = kind switch
        {
            "invalid-character" => $"!{parts[2][1..]}",
            "padding" => $"{parts[2][..^1]}=",
            "wrong-length" => parts[2][..^1],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.False(_service.TryRead($"{parts[0]}.{parts[1]}.{malformedSignature}", out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Fact]
    public void ATokenWithANonCanonicalSignatureIsRejected()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var finalCharacter = issued.Token[^1];
        const string base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var alternateCharacter = base64UrlAlphabet[base64UrlAlphabet.IndexOf(finalCharacter) + 1];
        var token = $"{issued.Token[..^1]}{alternateCharacter}";

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Fact]
    public void TokensOutsideTheCanonicalLengthAreRejected()
    {
        var token = CreateKnownKeyToken(Guid.NewGuid().ToString("N"), new string('A', 10_000));

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    private static string CreateKnownKeyToken(string identifier, string nonce)
    {
        var payload = $"{identifier}.{nonce}";
        var signature = ToBase64Url(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SigningKey),
            Encoding.UTF8.GetBytes(payload)));

        return $"{payload}.{signature}";
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
`````

## after — tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs — 1/1

<!-- retirement-file: {"id":70,"file":"tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs","beforeSha":"3b27fc5b3bd92a69af2dfe6f1add39eaad4d4e6a441eaf5c531ff382de0aea85","afterSha":"e8a240b092979cf309ddb27cdd52d800bd0b9f10d28abb1a7853a5f9259d0b40","side":"after","part":1,"parts":1} -->

`````csharp
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Infrastructure.Tests;

public class HmacTokenServiceTests
{
    private const string SigningKey = "a-signing-key-that-is-long-enough-to-be-safe";

    /// <summary>purpose (1) + identifier (16) + version (4) + HMAC-SHA256 (32), base64url, unpadded.</summary>
    private const int TokenLength = 71;

    private static readonly TokenOptions Options =
        new(SigningKey);

    private readonly HmacTokenService _service = new(Options);

    [Theory]
    [InlineData(TokenPurpose.Book)]
    [InlineData(TokenPurpose.Manage)]
    public void AnIssuedTokenRoundTripsToItsPurposeIdentifierAndVersion(TokenPurpose purpose)
    {
        var id = Guid.NewGuid();

        var token = _service.Issue(purpose, id, 4);

        Assert.True(_service.TryRead(token, out var read));
        Assert.Equal(new TokenReference(purpose, id, 4), read);
    }

    /// <summary>
    /// Determinism is what lets the confirmation page and the confirmation email carry the same
    /// manage link without either of them storing it.
    /// </summary>
    [Fact]
    public void TheSameInputsAlwaysProduceTheSameToken()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            _service.Issue(TokenPurpose.Manage, id, 1),
            _service.Issue(TokenPurpose.Manage, id, 1));
    }

    [Fact]
    public void ABookTokenIsNotAManageTokenForTheSameIdentifier()
    {
        var id = Guid.NewGuid();

        var book = _service.Issue(TokenPurpose.Book, id, 1);
        var manage = _service.Issue(TokenPurpose.Manage, id, 1);

        Assert.NotEqual(book, manage);
        Assert.True(_service.TryRead(book, out var readBook));
        Assert.Equal(TokenPurpose.Book, readBook.Purpose);
        Assert.True(_service.TryRead(manage, out var readManage));
        Assert.Equal(TokenPurpose.Manage, readManage.Purpose);
    }

    [Fact]
    public void EachVersionOfOneIdentifierIsADifferentToken()
    {
        var id = Guid.NewGuid();

        var first = _service.Issue(TokenPurpose.Book, id, 1);
        var second = _service.Issue(TokenPurpose.Book, id, 2);

        Assert.NotEqual(first, second);
        Assert.True(_service.TryRead(first, out var readFirst));
        Assert.Equal(1, readFirst.Version);
        Assert.True(_service.TryRead(second, out var readSecond));
        Assert.Equal(2, readSecond.Version);
    }

    /// <summary>Nothing derived from the token is stored, so the service offers no hash of it.</summary>
    [Fact]
    public void TheServiceOffersNoWayToDeriveAStoredValueFromAToken()
    {
        Assert.DoesNotContain(
            typeof(ITokenService).GetMethods(),
            method => method.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ANonPositiveVersionIsRefusedAtIssue()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 0));

        Assert.Equal("version", ex.ParamName);
    }

    [Fact]
    public void AnUnknownPurposeIsRefusedAtIssue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Issue((TokenPurpose)7, Guid.NewGuid(), 1));
    }

    [Fact]
    public void ATokenCarryingANonPositiveVersionIsRejected()
    {
        var token = CreateKnownKeyToken((byte)TokenPurpose.Book, Guid.NewGuid(), 0);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATokenCarryingAnUnknownPurposeIsRejected()
    {
        var token = CreateKnownKeyToken(7, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATamperedTokenFailsVerification()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var replacement = Alphabet[(Alphabet.IndexOf(token[0], StringComparison.Ordinal) + 1) % Alphabet.Length];

        Assert.False(_service.TryRead($"{replacement}{token[1..]}", out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATokenSignedWithAnotherKeyIsRejected()
    {
        var other = new HmacTokenService(new TokenOptions("a-completely-different-signing-key-value"));
        var token = other.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead(token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    [InlineData("a.b.c")]
    public void AMalformedTokenIsRejectedWithoutThrowing(string? token)
    {
        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    /// <summary>
    /// The final base64url character carries only two significant bits; the other three spellings
    /// decode to the same bytes, and accepting them would make one link answer to four URLs.
    /// </summary>
    [Fact]
    public void ATokenWithANonCanonicalEncodingIsRejected()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var index = Alphabet.IndexOf(token[^1], StringComparison.Ordinal);
        var alternate = Alphabet[(index & ~0b11) | ((index + 1) & 0b11)];

        Assert.False(_service.TryRead($"{token[..^1]}{alternate}", out _));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("=")]
    public void ATokenWithACharacterOutsideTheBase64UrlAlphabetIsRejected(string character)
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead($"{character}{token[1..]}", out _));
    }

    [Fact]
    public void TokensOutsideTheCanonicalLengthAreRejected()
    {
        Assert.False(_service.TryRead(new string('A', 10_000), out _));
        Assert.False(_service.TryRead(new string('A', TokenLength - 1), out _));
        Assert.False(_service.TryRead(new string('A', TokenLength + 1), out _));
    }

    [Fact]
    public void TheTokenIsUrlSafeAndOfTheCanonicalLength()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.Equal(TokenLength, token.Length);
        Assert.Equal(token, Uri.EscapeDataString(token));
    }

    [Fact]
    public void AShortSigningKeyIsRejectedAtConstruction()
    {
        var ex = Assert.Throws<ArgumentException>(() => new HmacTokenService(new TokenOptions("too-short")));
        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public void OptionsToStringDoesNotRevealTheSigningKey()
    {
        Assert.DoesNotContain(SigningKey, Options.ToString());
    }

    [Fact]
    public void NullOptionsAreRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(null!));

        Assert.Equal("options", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    [Fact]
    public void ARuntimeNullSigningKeyIsRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(new TokenOptions(null!)));

        Assert.Equal("SigningKey", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    private static string CreateKnownKeyToken(byte purpose, Guid id, int version)
    {
        var payload = new byte[21];
        payload[0] = purpose;
        id.TryWriteBytes(payload.AsSpan(1, 16), bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(17, 4), version);

        var signed = new byte[53];
        payload.CopyTo(signed, 0);
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningKey), payload).CopyTo(signed, 21);

        return Convert.ToBase64String(signed).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":71,"file":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","beforeSha":"5792e45df22557dca992eb044e3f630f7d2b5b09be365839c8e009db7e4a6e70","afterSha":"ee651be7d22f5a96b2504575a59b35dc812bac007d362e7c87fbe96eb3a14300","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(attendee.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }

    /// <summary>An Invite reloads the location set every later offer must be drawn from.</summary>
    [Fact]
    public async Task TheLocationSetRoundTrips()
    {
        await fixture.ResetAsync();
        var second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Bo", "bo@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            "hash-of-a-two-location-invite",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId, second],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Locations)
            .SingleAsync(value => value.Id == invite.Id);

        Assert.Equal([ProposalFixture.LocationId, second], actual.LocationIds.Order());
        Assert.All(actual.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    /// <summary>The attendee's status stamp is the aggregate's own, and survives a round trip.</summary>
    [Fact]
    public async Task TheAttendeeStatusStampRoundTrips()
    {
        await fixture.ResetAsync();
        var stampedAt = new DateTimeOffset(2026, 9, 4, 11, 0, 0, TimeSpan.Zero);
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Cass", "cass@example.com", group, ProposalFixture.Now);
        attendee.MarkAwaitingAvailability(stampedAt);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Attendees.SingleAsync(value => value.Id == attendee.Id);

        Assert.Equal(AttendeeStatus.AwaitingAvailability, actual.Status);
        Assert.Equal(stampedAt, actual.StatusChangedAt);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":71,"file":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","beforeSha":"5792e45df22557dca992eb044e3f630f7d2b5b09be365839c8e009db7e4a6e70","afterSha":"ee651be7d22f5a96b2504575a59b35dc812bac007d362e7c87fbe96eb3a14300","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amara", "amara@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(attendee.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }

    /// <summary>An Invite reloads the location set every later offer must be drawn from.</summary>
    [Fact]
    public async Task TheLocationSetRoundTrips()
    {
        await fixture.ResetAsync();
        var second = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Bo", "bo@example.com", group, ProposalFixture.Now);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendee.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId, second],
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            attendee.RequiredAppointmentTypeIds,
            0);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Locations)
            .SingleAsync(value => value.Id == invite.Id);

        Assert.Equal([ProposalFixture.LocationId, second], actual.LocationIds.Order());
        Assert.All(actual.Locations, location => Assert.Equal(invite.Id, location.InviteId));
    }

    /// <summary>The attendee's status stamp is the aggregate's own, and survives a round trip.</summary>
    [Fact]
    public async Task TheAttendeeStatusStampRoundTrips()
    {
        await fixture.ResetAsync();
        var stampedAt = new DateTimeOffset(2026, 9, 4, 11, 0, 0, TimeSpan.Zero);
        var group = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Cass", "cass@example.com", group, ProposalFixture.Now);
        attendee.MarkAwaitingAvailability(stampedAt);

        await using (var write = fixture.NewContext())
        {
            write.Attendees.Add(attendee);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Attendees.SingleAsync(value => value.Id == attendee.Id);

        Assert.Equal(AttendeeStatus.AwaitingAvailability, actual.Status);
        Assert.Equal(stampedAt, actual.StatusChangedAt);
    }
}
`````

## before — tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":72,"file":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","beforeSha":"5594d185e30c3b1d0a78ed9902e6e8f2991db96a849e0c9bc2e794b3bca65325","afterSha":"87eac8085e90d01935ab719f7b9e086607e8511861fcf68b1507b455f96426c1","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies recovery journey links, ordering, and active uniqueness backstops.</summary>
[Collection("postgres")]
public sealed class RecoveryBookingPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies an original and two concluded recoveries reload as one ordered journey.</summary>
    [Fact]
    public async Task JourneyLinksPersistAndReloadInCreationOrder()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventId, "manage-original", DateTimeOffset.UtcNow);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));
        first.Conclude();
        second.Conclude();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, first, second);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var journey = await new BookingRepository(read)
            .ListJourneyAsync(original.Id, CancellationToken.None);

        Assert.Equal([original.Id, first.Id, second.Id], journey.Select(b => b.Id));
        Assert.Null(journey[0].RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, journey[0].Status);
        Assert.Equal([original.Id, original.Id], journey.Skip(1).Select(b => b.RecoveryOfBookingId));
        Assert.All(journey.Skip(1), b => Assert.Equal(BookingStatus.Concluded, b.Status));
    }

    /// <summary>Verifies a second active original for one attendee violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveOriginalViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var first = OriginalFor(attendeeId);
        var second = OriginalFor(attendeeId);

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies a second active recovery for one root violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveRecoveryViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var original = OriginalFor(attendeeId);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(original, first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Booking OriginalFor(Guid attendeeId)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            "initial",
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        return Booking.Create(Guid.NewGuid(), invite, eventId, "manage", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            "recovery",
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
`````

## after — tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs — 1/1

<!-- retirement-file: {"id":72,"file":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","beforeSha":"5594d185e30c3b1d0a78ed9902e6e8f2991db96a849e0c9bc2e794b3bca65325","afterSha":"87eac8085e90d01935ab719f7b9e086607e8511861fcf68b1507b455f96426c1","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies recovery journey links, ordering, and active uniqueness backstops.</summary>
[Collection("postgres")]
public sealed class RecoveryBookingPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies an original and two concluded recoveries reload as one ordered journey.</summary>
    [Fact]
    public async Task JourneyLinksPersistAndReloadInCreationOrder()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, eventId, DateTimeOffset.UtcNow);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));
        first.Conclude();
        second.Conclude();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, first, second);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var journey = await new BookingRepository(read)
            .ListJourneyAsync(original.Id, CancellationToken.None);

        Assert.Equal([original.Id, first.Id, second.Id], journey.Select(b => b.Id));
        Assert.Null(journey[0].RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, journey[0].Status);
        Assert.Equal([original.Id, original.Id], journey.Skip(1).Select(b => b.RecoveryOfBookingId));
        Assert.All(journey.Skip(1), b => Assert.Equal(BookingStatus.Concluded, b.Status));
    }

    /// <summary>Verifies a second active original for one attendee violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveOriginalViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var first = OriginalFor(attendeeId);
        var second = OriginalFor(attendeeId);

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies a second active recovery for one root violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveRecoveryViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var attendeeId = Guid.NewGuid();
        var original = OriginalFor(attendeeId);
        var first = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(attendeeId, original, DateTimeOffset.UtcNow.AddHours(2));

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(original, first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Booking OriginalFor(Guid attendeeId)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(),
            attendeeId,
            DateTimeOffset.UtcNow.AddDays(1),
            [ProposalFixture.LocationId],
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp],
            0);
        return Booking.Create(Guid.NewGuid(), invite, eventId, DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid attendeeId, Booking original, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(),
            attendeeId,
            original.Id,
            DateTimeOffset.UtcNow.AddDays(2),
            ProposalFixture.LocationId,
            null,
            [eventId, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, eventId, createdAt);
    }
}
`````
