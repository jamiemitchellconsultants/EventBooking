using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Tests.Invites;

public sealed class StubEligibility : IEventEligibilityQuery
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
        Task.FromResult(EligibleInOrder.Count);
}

public sealed class InviteFixture
{
    public static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    public InMemoryAttendeeRepository Attendees = new();
    public InMemoryInviteRepository Invites = new();
    public InMemoryLocationRepository Locations = new();
    public InMemoryAppointmentTypeRepository Types = new();
    public InMemoryAttendeeGroupRepository Groups = new();
    public InMemorySystemSettingsRepository Settings = new();
    public InMemoryEmailDeliveryRepository Emails = new();
    public InMemoryStaffAccessProfileRepository Profiles = new();
    public FakeUnitOfWork UnitOfWork = new();
    public RecordingAuditLogger Audit = new();
    public FakeClock Clock = new(Now);
    public StubEligibility Eligibility = new();

    public Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    public Guid AttendeeId;
    public Guid MedId;
    public Guid FitId;
    public List<Guid> LocationIds = [];

    public static InviteFixture Create(int optionCount = 3)
    {
        var fixture = new InviteFixture();
        fixture.Types.Items.Clear();
        var med = AppointmentType.Create(Guid.NewGuid(), "MED", "Medical");
        var fit = AppointmentType.Create(Guid.NewGuid(), "FIT", "Fitness");
        fixture.Types.Items.AddRange([med, fit]);
        fixture.MedId = med.Id;
        fixture.FitId = fit.Id;
        var group = AttendeeGroup.Create(Guid.NewGuid(), "NHS", "NHS staff", [med.Id, fit.Id], [med.Id, fit.Id]);
        fixture.Groups.Items.Add(group);
        foreach (var code in new[] { "LONDON_HQ", "TOKYO" })
        {
            var location = Location.Create(Guid.NewGuid(), code, code, "1 High St",
                code == "LONDON_HQ" ? "Europe/London" : "Asia/Tokyo", TestZonesDouble.Instance);
            fixture.Locations.Items.Add(location);
            fixture.LocationIds.Add(location.Id);
        }

        var attendee = Attendee.Create(Guid.NewGuid(), "Amy", "amy@example.invalid", group, Now);
        fixture.Attendees.Items.Add(attendee);
        fixture.AttendeeId = attendee.Id;
        fixture.Profiles.Add(StaffAccessProfile.Create(fixture.Coordinator, Role.Coordinator, null));
        fixture.Settings.Settings.Update(7, 2, optionCount, 48);
        return fixture;
    }

    public InviteFixture WithEligibleEvents(int count)
    {
        Eligibility.EligibleInOrder = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
        return this;
    }

    public InviteFixture WithInactiveLocation()
    {
        Locations.Items[0].Deactivate(LocationUsage.None);
        return this;
    }
}

public sealed class TestZonesDouble : IEventWindowZones
{
    public static readonly TestZonesDouble Instance = new();
    public bool IsKnownZone(string timeZoneId) =>
        timeZoneId is "Europe/London" or "Asia/Tokyo";
    public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        throw new NotImplementedException();
    public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        throw new NotImplementedException();
    public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
        throw new NotImplementedException();
    public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) =>
        throw new NotImplementedException();
}
