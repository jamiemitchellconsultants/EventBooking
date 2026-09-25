// src/EventBooking.SeedData/DemoSeedSpec.cs (complete)
using EventBooking.Domain.Access;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;

namespace EventBooking.SeedData;

public interface IDemoProposalScenario
{
    string LocationCode { get; }
    DateOnly Date { get; }
    TimeOnly StartTime { get; }
    int DurationMinutes { get; }
    IReadOnlyList<string> TypeCodes { get; }
    IReadOnlyList<string> AcceptedTypeCodes { get; }
}

public sealed record DemoLocationSpec(Guid Id, string Code, string Name, string Address, string TimeZoneId, bool IsActive);
public sealed record DemoAppointmentTypeSpec(Guid Id, string Code, string Name, bool IsActive, string? ManagerUsername);
public sealed record DemoAttendeeGroupSpec(Guid Id, string Code, string Name, IReadOnlyList<string> TypeCodes, bool IsActive);
public sealed record DemoEventSpec(
    Guid Id, string LocationCode, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<string> TypeCodes, IReadOnlyList<string> AcceptedTypeCodes) : IDemoProposalScenario;
public sealed record DemoProposalSpec(
    Guid Id, string LocationCode, DateOnly Date, TimeOnly StartTime, int DurationMinutes,
    IReadOnlyList<string> TypeCodes, IReadOnlyList<string> AcceptedTypeCodes) : IDemoProposalScenario;
public enum DemoRecovery { None, Pending, Completed }
public sealed record DemoAttendeeSpec(
    Guid Id, string Name, string Email, string GroupCode, AttendeeStatus Status,
    IReadOnlyList<BookingAppointmentStatus> AppointmentStatuses,
    DemoRecovery Recovery, bool SendInvitation = false);

public sealed record StaffProfileSpec(
    string Username, string GivenName, string FamilyName, string Email,
    Guid UserId, StaffId StaffId, IReadOnlyList<Role> Roles, Guid? AppointmentTypeId);

public sealed record DemoDataset(
    DateOnly Anchor,
    IReadOnlyList<DemoLocationSpec> Locations,
    IReadOnlyList<DemoAppointmentTypeSpec> AppointmentTypes,
    IReadOnlyList<DemoAttendeeGroupSpec> AttendeeGroups,
    IReadOnlyList<StaffProfileSpec> Staff,
    IReadOnlyList<DemoEventSpec> Events,
    IReadOnlyList<DemoProposalSpec> OpenProposals,
    IReadOnlyList<DemoAttendeeSpec> Attendees);

public static class DemoSeedSpec
{
    private static DateOnly? _anchor;
    private static IReadOnlyDictionary<string, Guid>? _providerIds;
    public static DateOnly AnchorDate() => _anchor ?? new DateOnly(2026, 9, 22);
    public static bool AnchorOverridden => _anchor.HasValue;
    public static void OverrideAnchor(DateOnly? anchor) => _anchor = anchor;

    /// <summary>Adopts Keycloak-assigned provider identifiers, which the provider generates
    /// server-side. Until overridden, the stable specification identifiers apply.</summary>
    public static void OverrideProviderIds(IReadOnlyDictionary<string, Guid>? providerIds) =>
        _providerIds = providerIds;

    /// <summary>The effective user identifier: the adopted provider id, or the specification
    /// identifier when Keycloak has not converged in this process.</summary>
    public static Guid ProviderUserId(string username)
    {
        var staff = Staff().Single(x => x.Username == username);
        return _providerIds is not null && _providerIds.TryGetValue(username, out var providerId)
            ? providerId
            : staff.UserId;
    }

    public static DemoDataset Build()
    {
        var anchor = AnchorDate();
        var locations = new[]
        {
            new DemoLocationSpec(Id(1, 1), "LONDON", "London Centre", "1 Example Street, London", "Europe/London", true),
            new DemoLocationSpec(Id(1, 2), "MANCHESTER", "Manchester Centre", "2 Example Street, Manchester", "Europe/London", false),
            new DemoLocationSpec(Id(1, 3), "DUBLIN", "Dublin Centre", "3 Example Street, Dublin", "Europe/Dublin", true),
        };
        var types = new[]
        {
            new DemoAppointmentTypeSpec(Id(2, 1), "MED", "Medical", true, "coordinator.med"),
            new DemoAppointmentTypeSpec(Id(2, 2), "FIT", "Fitness", true, "manager.fit"),
            new DemoAppointmentTypeSpec(Id(2, 3), "IND", "Induction", true, "manager.ind"),
            new DemoAppointmentTypeSpec(Id(2, 4), "LAB", "Laboratory", true, "manager.lab"),
            new DemoAppointmentTypeSpec(Id(2, 5), "ESC", "Escalation", true, null),
            new DemoAppointmentTypeSpec(Id(2, 6), "DOC", "Document review", false, null),
        };
        var typeByCode = types.ToDictionary(x => x.Code);
        var staff = new[]
        {
            Staff("admin", 1, "Ari", "Admin", [Role.Admin], null),
            Staff("coordinator", 2, "Casey", "Coordinator", [Role.Coordinator], null),
            Staff("coordinator.med", 3, "Morgan", "Medical", [Role.Coordinator, Role.Manager], typeByCode["MED"].Id),
            Staff("manager.fit", 4, "Frankie", "Fitness", [Role.Manager], typeByCode["FIT"].Id),
            Staff("manager.ind", 5, "Indra", "Induction", [Role.Manager], typeByCode["IND"].Id),
            Staff("manager.lab", 6, "Luca", "Laboratory", [Role.Manager], typeByCode["LAB"].Id),
            Staff("appointment.med", 7, "Sam", "Appointments", [Role.AppointmentStaff], typeByCode["MED"].Id),
            Staff("appointment.unscoped", 8, "Taylor", "Unscoped", [Role.AppointmentStaff], null),
        };
        var groups = new[]
        {
            new DemoAttendeeGroupSpec(Id(3, 1), "IND_ONLY", "Induction only", ["IND"], true),
            new DemoAttendeeGroupSpec(Id(3, 2), "CORE_THREE", "Medical fitness and induction", ["MED", "FIT", "IND"], true),
            new DemoAttendeeGroupSpec(Id(3, 3), "DOC_HISTORY", "Historical document review", ["MED", "DOC"], false),
            new DemoAttendeeGroupSpec(Id(3, 4), "FIT_ESC", "Fitness and escalation", ["FIT", "ESC"], true),
        };
        var dst = NextOffsetChange(anchor.AddDays(1), "Europe/Dublin");
        var events = new[]
        {
            Event(1, "LONDON", anchor.AddDays(3), 9, 0, 60, ["MED"]),
            Event(2, "DUBLIN", anchor.AddDays(6), 9, 30, 90, ["MED", "FIT"]),
            Event(3, "LONDON", anchor.AddDays(9), 10, 0, 240, ["MED", "FIT", "IND"]),
            Event(4, "DUBLIN", dst, 12, 0, 480, ["MED", "FIT", "IND", "LAB"]),
            Event(5, "LONDON", anchor.AddDays(12), 8, 0, 240, ["MED", "FIT", "IND", "LAB"]),
            Event(6, "DUBLIN", anchor.AddDays(15), 8, 30, 240, ["MED", "FIT", "IND", "LAB"]),
            Event(7, "LONDON", anchor.AddDays(18), 9, 0, 240, ["MED", "FIT", "IND", "LAB"]),
        };
        var proposals = new[]
        {
            new DemoProposalSpec(Id(6, 1), "LONDON", anchor.AddDays(20), new TimeOnly(9, 0), 90,
                ["MED", "FIT"], ["MED"]),
            new DemoProposalSpec(Id(6, 2), "DUBLIN", anchor.AddDays(22), new TimeOnly(10, 0), 240,
                ["MED", "FIT", "IND", "LAB"], ["MED", "FIT"]),
        };
        var attendees = new[]
        {
            Person(1, "Nia New", "IND_ONLY", AttendeeStatus.NotYetInvited, [], send: true),
            Person(2, "Avery Awaiting", "FIT_ESC", AttendeeStatus.AwaitingAvailability, []),
            Person(3, "Ira Invited", "IND_ONLY", AttendeeStatus.Invited, []),
            Person(4, "Blair Booked", "CORE_THREE", AttendeeStatus.Booked, [BookingAppointmentStatus.Expected]),
            Person(5, "Chris Checked", "CORE_THREE", AttendeeStatus.Booked, [BookingAppointmentStatus.CheckedIn]),
            Person(6, "Rae Ready", "CORE_THREE", AttendeeStatus.Booked, [BookingAppointmentStatus.Completed]),
            Person(7, "Noah No Response", "IND_ONLY", AttendeeStatus.NoResponseNeedsFollowUp, []),
            Person(8, "Parker Pending Recovery", "CORE_THREE", AttendeeStatus.Booked,
                [BookingAppointmentStatus.NoShow], DemoRecovery.Pending),
            Person(9, "Robin Recovered", "CORE_THREE", AttendeeStatus.Booked,
                [BookingAppointmentStatus.NoShow, BookingAppointmentStatus.Completed], DemoRecovery.Completed),
        };
        return new DemoDataset(anchor, locations, types, groups, staff, events, proposals, attendees);
    }

    public static IReadOnlyList<StaffProfileSpec> Staff() => Build().Staff;
    public static Guid AdminUserId() => ProviderUserId(
        Staff().Single(x => x.Roles.SequenceEqual([Role.Admin])).Username);
    public static Guid CoordinatorUserId() => ProviderUserId("coordinator");
    public static IReadOnlyDictionary<Guid, Guid> ManagerForType() => Staff()
        .Where(x => x.Roles.Contains(Role.Manager))
        .ToDictionary(x => x.AppointmentTypeId!.Value, x => ProviderUserId(x.Username));

    private static StaffProfileSpec Staff(
        string username, int number, string given, string family, IReadOnlyList<Role> roles, Guid? type) =>
        new(username, given, family, $"{username}@example.test", Id(4, number),
            new StaffId($"DEMO{number:000}"), roles, type);
    private static DemoEventSpec Event(
        int number, string location, DateOnly date, int hour, int minute, int duration, string[] types) =>
        new(Id(5, number), location, date, new TimeOnly(hour, minute), duration, types, types);
    private static DemoAttendeeSpec Person(
        int number, string name, string group, AttendeeStatus status,
        BookingAppointmentStatus[] appointments, DemoRecovery recovery = DemoRecovery.None, bool send = false) =>
        new(Id(7, number), name, $"demo.attendee.{number:00}@example.test", group, status,
            appointments, recovery, send);
    private static Guid Id(int family, int number) =>
        Guid.Parse($"{family}0000000-0000-0000-0000-{number:000000000000}");

    private static DateOnly NextOffsetChange(DateOnly first, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        for (var date = first; date < first.AddYears(2); date = date.AddDays(1))
        {
            var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            if (zone.GetUtcOffset(start) != zone.GetUtcOffset(start.AddDays(1))) return date;
        }
        throw new SeedException($"No offset change found for {timeZoneId} within two years of {first:yyyy-MM-dd}.");
    }
}
