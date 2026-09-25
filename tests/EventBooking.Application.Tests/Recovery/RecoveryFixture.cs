using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Tests.Recovery;

public sealed class RecoveryZones : IEventWindowZones
{
    public static readonly RecoveryZones Instance = new();
    public bool IsKnownZone(string timeZoneId) =>
        timeZoneId is "Europe/London" or "Europe/Dublin" or "Asia/Tokyo";
    public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        LocalTimeValidity.Unique;
    public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        new(new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified),
            TimeSpan.Zero);
    public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
        // Hard-coded summer offsets for the June test date, not a zone database: Dublin
        // observes IST (UTC+1) in June, Tokyo JST (UTC+9) year-round.
        timeZoneId switch
        {
            "Asia/Tokyo" => DateOnly.FromDateTime(instant.UtcDateTime.AddHours(9)),
            "Europe/Dublin" => DateOnly.FromDateTime(instant.UtcDateTime.AddHours(1)),
            _ => DateOnly.FromDateTime(instant.UtcDateTime),
        };
    public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "T";
}

public sealed class StubRecoveryEligibility : IEventEligibilityQuery
{
    public IReadOnlyList<Guid> EligibleInOrder { get; set; } = [];
    public Task<IReadOnlyList<Guid>> FindEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        int count, DateTimeOffset asOf, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(EligibleInOrder
            .Where(id => !excludeEventIds.Contains(id)).Take(count).ToList());
    public Task<int> CountEligibleEventsAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        IReadOnlyCollection<Guid> locationIds,
        IReadOnlyCollection<Guid> excludeEventIds,
        DateTimeOffset asOf, CancellationToken cancellationToken) =>
        Task.FromResult(EligibleInOrder.Count(id => !excludeEventIds.Contains(id)));
}

public sealed class RecoveryFixture
{
    public static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    public InMemoryAttendeeRepository Attendees = new();
    public InMemoryInviteRepository Invites = new();
    public InMemoryBookingRepository Bookings = new();
    public InMemoryBookingAppointmentRepository Appointments = null!;
    public InMemoryEventRepository Events = new();
    public InMemoryEventCapacityRepository Capacities = null!;
    public InMemoryLocationRepository Locations = new();
    public InMemoryAppointmentTypeRepository Types = new();
    public InMemoryAttendeeGroupRepository Groups = new();
    public InMemorySystemSettingsRepository Settings = new();
    public InMemoryEmailDeliveryRepository Emails = new();
    public InMemoryStaffAccessProfileRepository Profiles = new();
    public FakeUnitOfWork UnitOfWork = new();
    public RecordingAuditLogger Audit = new();
    public FakeClock Clock = new(Now);
    public FakeTokenService Tokens = new();
    public StubRecoveryEligibility Eligibility = new();

    public Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    public Guid MedStaff = Guid.Parse("d0000001-0000-0000-0000-000000000001");
    public Guid AttendeeId;
    public Guid BookingId;
    public Guid LondonId;
    public Guid TokyoId;
    public Guid DublinId;
    public Guid MedId;
    public Guid FitId;
    public bool ManageAttendees = true;

    public static RecoveryFixture Create()
    {
        var fixture = new RecoveryFixture();
        fixture.Appointments = new InMemoryBookingAppointmentRepository(fixture.Bookings);
        fixture.Capacities = new InMemoryEventCapacityRepository(fixture.Events);
        fixture.Types.Items.Clear();
        var med = AppointmentType.Create(Guid.NewGuid(), "MED", "Medical");
        var fit = AppointmentType.Create(Guid.NewGuid(), "FIT", "Fitness");
        fixture.Types.Items.AddRange([med, fit]);
        fixture.MedId = med.Id;
        fixture.FitId = fit.Id;
        var london = Location.Create(Guid.NewGuid(), "LONDON_HQ", "London HQ", "1 High St",
            "Europe/London", RecoveryZones.Instance);
        var tokyo = Location.Create(Guid.NewGuid(), "TOKYO", "Tokyo", "2 Shibuya",
            "Asia/Tokyo", RecoveryZones.Instance);
        var dublin = Location.Create(Guid.NewGuid(), "DUBLIN", "Dublin", "3 Grafton St",
            "Europe/Dublin", RecoveryZones.Instance);
        fixture.Locations.Items.AddRange([london, tokyo, dublin]);
        fixture.LondonId = london.Id;
        fixture.TokyoId = tokyo.Id;
        fixture.DublinId = dublin.Id;
        var group = AttendeeGroup.Create(Guid.NewGuid(), "NHS", "NHS staff",
            [med.Id, fit.Id], [med.Id, fit.Id]);
        fixture.Groups.Items.Add(group);
        var attendee = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", group, Now);
        fixture.Attendees.Items.Add(attendee);
        fixture.AttendeeId = attendee.Id;

        var proposal = EventProposal.Propose(Guid.NewGuid(), london.Id, true, "Europe/London",
            new EventWindow(new DateOnly(2026, 11, 4), new TimeOnly(9, 30), 90),
            RecoveryZones.Instance, Now,
            [new ProposableAppointmentType(med.Id, "MED", true, true),
             new ProposableAppointmentType(fit.Id, "FIT", true, true)],
            med.Id, fixture.Coordinator, 10);
        proposal.Accept(fit.Id, fixture.Coordinator, 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        fixture.Events.Items.Add(eventItem);

        var invite = Invite.CreateInitial(Guid.NewGuid(), attendee.Id, Now.AddDays(7),
            [london.Id], [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], [med.Id, fit.Id], 0);
        fixture.Invites.Items.Add(invite);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, Now);
        invite.MarkUsed();
        fixture.Bookings.Items.Add(booking);
        fixture.BookingId = booking.Id;
        foreach (var typeId in new[] { med.Id, fit.Id })
            fixture.Appointments.Items.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        attendee.MarkInvited(Now);
        attendee.MarkBooked(Now);
        Apps(fixture).Single(a => a.AppointmentTypeId == med.Id)
            .TransitionTo(BookingAppointmentStatus.NoShow, fixture.Coordinator, Now, false, true);
        fixture.Profiles.Add(StaffAccessProfile.Create(fixture.Coordinator, Role.Coordinator, null));
        fixture.Profiles.Add(StaffAccessProfile.Create(fixture.MedStaff, Role.AppointmentStaff, med.Id));
        fixture.Settings.Settings.Update(7, 2, 1, 48);
        return fixture;
    }

    public static List<BookingAppointment> Apps(RecoveryFixture fixture) =>
        fixture.Appointments.Items.Where(a => a.BookingId == fixture.BookingId).ToList();

    // A Dublin event 00:30–01:30 local on 15 June with its own booked appointment,
    // returned for zone-judged check-in tests.
    public Guid WithDublinBooking()
    {
        var dublin = Locations.Items.Single(l => l.Id == DublinId);
        var proposal = EventProposal.Propose(Guid.NewGuid(), dublin.Id, true, "Europe/Dublin",
            new EventWindow(new DateOnly(2026, 6, 15), new TimeOnly(0, 30), 60),
            RecoveryZones.Instance, new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            [new ProposableAppointmentType(MedId, "MED", true, true),
             new ProposableAppointmentType(FitId, "FIT", true, true)],
            MedId, Coordinator, 5);
        proposal.Accept(FitId, Coordinator, 5);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        Events.Items.Add(eventItem);
        var invite = Invite.CreateInitial(Guid.NewGuid(), AttendeeId, Now.AddDays(700),
            [dublin.Id], [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], [MedId, FitId], 0);
        Invites.Items.Add(invite);
        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, Now);
        invite.MarkUsed();
        Bookings.Items.Add(booking);
        var appointment = BookingAppointment.Create(Guid.NewGuid(), booking.Id, MedId);
        Appointments.Items.Add(appointment);
        return appointment.Id;
    }

    public void RemoveCoordinator() =>
        Profiles.Items.RemoveAll(p => p.StaffUserId == Coordinator);
}
