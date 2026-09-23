using System.Reflection;
using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.SeedData;

/// <summary>The deterministic lifecycle state constructed for a demo Attendee.</summary>
public enum DemoAttendeeJourney
{
    /// <summary>The Attendee has a group but no Booking.</summary>
    Unbooked = 1,
    /// <summary>Every current requirement has a Completed attempt.</summary>
    Ready = 2,
    /// <summary>At least one current requirement is Expected or CheckedIn.</summary>
    Outstanding = 3,
    /// <summary>At least one current requirement has a recoverable NoShow.</summary>
    NoShow = 4,
    /// <summary>A recovery Booking has completed a previously missed requirement.</summary>
    RecoveryCompleted = 5,
}

/// <summary>One deterministic Attendee seed assignment and requested demo journey.</summary>
/// <param name="Name">The demo Attendee full name.</param>
/// <param name="Email">The demo Attendee email address.</param>
/// <param name="AttendeeGroupCode">The canonical Attendee Group code.</param>
/// <param name="Journey">The deterministic lifecycle state to construct.</param>
public sealed record AttendeeSpec(
    string Name,
    string Email,
    string AttendeeGroupCode,
    DemoAttendeeJourney Journey);

public sealed record AgreedEventSpec(DateOnly Date, TimeOnly StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

public sealed record OpenProposalSpec(
    DateOnly Date,
    TimeOnly StartTime,
    Guid CreatedByManagerUserId,
    int? DatHeadcount,
    int? MedHeadcount,
    int? UniHeadcount);



/// <summary>One deterministic demo access profile and its provider identity fields.</summary>
/// <param name="Username">The unique Keycloak username created for the demo identity.</param>
/// <param name="UserId">The stable identity-provider object identifier.</param>
/// <param name="StaffId">The canonical HR-issued staff number.</param>
/// <param name="Roles">The identity-provider roles mirrored for the staff user.</param>
/// <param name="AppointmentTypeId">The optional EventBooking-owned appointment-type scope.</param>
public sealed record StaffProfileSpec(
    string Username,
    Guid UserId,
    StaffId StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId)
{
    /// <summary>Gets the unique Keycloak username created for the demo identity.</summary>
    public string Username { get; init; } = Username;

    /// <summary>Gets the stable identity-provider object identifier.</summary>
    public Guid UserId { get; init; } = UserId;

    /// <summary>Gets the canonical HR-issued staff number.</summary>
    public StaffId StaffId { get; init; } = StaffId;

    /// <summary>Gets the identity-provider roles mirrored for the staff user.</summary>
    public IReadOnlyList<Role> Roles { get; init; } = Roles;

    /// <summary>Gets the EventBooking-owned appointment-type scope, or null when unscoped.</summary>
    public Guid? AppointmentTypeId { get; init; } = AppointmentTypeId;
}

/// <summary>
/// The demo dataset, loaded from the embedded demo-seed.json so it can be edited without
/// touching code. Day offsets resolve against the file's anchor date, so every run matches
/// the same windows and re-runs only ever fill in newly edited placeholders.
/// </summary>
public static class DemoSeedSpec
{
    private static Lazy<SeedDocument> Document = new(Load);

    private static DateOnly? AnchorOverride;

    private static string? StaffIdPatternOverride;

    /// <summary>
    /// Gets the fixed calendar date against which the demo dataset's day offsets resolve.
    /// </summary>
    public static DateOnly AnchorDate() => AnchorOverride ?? Document.Value.Anchor;

    /// <summary>Gets whether a run override replaced the file anchor.</summary>
    public static bool AnchorOverridden => AnchorOverride.HasValue;

    /// <summary>
    /// Overrides the file anchor for this run (the --reanchor option), or restores file
    /// behavior with null. Applies to agreed events, proposals, and journey windows alike.
    /// </summary>
    public static void OverrideAnchor(DateOnly? anchor) => AnchorOverride = anchor;

    /// <summary>
    /// Validates seed staff numbers against the deployment's staff-ID policy
    /// (<c>Identity__StaffIdPattern</c>) instead of the default format, or restores the default
    /// with null. Re-parses the dataset so the policy takes effect on the next read.
    /// </summary>
    public static void ConfigureStaffIdPattern(string? pattern)
    {
        StaffIdPatternOverride = pattern;
        Document = new(Load);
    }

    public static IReadOnlyList<StaffProfileSpec> Staff() => Document.Value.Staff;

    public static Guid AdminUserId() =>
        Document.Value.Staff.Single(profile => profile.Roles.SequenceEqual([Role.Admin])).UserId;

    public static Guid CoordinatorUserId() =>
        Document.Value.Staff.Single(profile =>
            profile.Roles.Contains(Role.Coordinator)
            && !profile.Roles.Contains(Role.Manager)
            && !profile.Roles.Contains(Role.AppointmentStaff)).UserId;

    public static IReadOnlyDictionary<Guid, Guid> ManagerForType() =>
        Document.Value.Staff
            .Where(profile => profile.Roles.Contains(Role.Manager))
            .ToDictionary(profile => profile.AppointmentTypeId!.Value, profile => profile.UserId);

    public static IReadOnlyList<AgreedEventSpec> AgreedEvents() =>
        Document.Value.AgreedEvents
            .Select(s => new AgreedEventSpec(
                AnchorDate().AddDays(s.DaysOffset), ParseStartTime(s.StartTime),
                s.DatHeadcount, s.MedHeadcount, s.UniHeadcount))
            .ToList();

    public static IReadOnlyList<OpenProposalSpec> OpenProposals() =>
        Document.Value.OpenProposals
            .Select(p => new OpenProposalSpec(
                AnchorDate().AddDays(p.DaysOffset), ParseStartTime(p.StartTime),
                ManagerId(p.CreatedBy), p.DatHeadcount, p.MedHeadcount, p.UniHeadcount))
            .ToList();

    public static IReadOnlyList<AttendeeSpec> Attendees() =>
        Document.Value.Attendees
            .Select(c => new AttendeeSpec(
                c.Name, c.Email, GroupCode(c.AttendeeGroup), Journey(c.Journey)))
            .ToList();

    private static string GroupCode(string code) =>
        AttendeeGroupIds.TryFromCode(code, out var id)
            ? AttendeeGroupIds.CodeOf(id)
            : throw new SeedException($"Unknown attendee group code '{code}' in demo-seed.json.");

    private static DemoAttendeeJourney Journey(string value) =>
        Enum.TryParse<DemoAttendeeJourney>(value, ignoreCase: true, out var journey)
            && Enum.IsDefined(journey)
            ? journey
            : throw new SeedException(
                $"Journey '{value}' in demo-seed.json must be one of Unbooked, Ready, Outstanding, NoShow, or RecoveryCompleted.");

    private static Guid ManagerId(string username) =>
        Document.Value.StaffByUsername.TryGetValue(username, out var userId)
            ? userId
            : throw new SeedException($"Unknown staff username '{username}' in demo-seed.json.");

    private static Guid TypeId(string code) =>
        AppointmentTypeIds.TryFromCode(code, out var id)
            ? id
            : throw new SeedException($"Unknown appointment type code '{code}' in demo-seed.json.");

    private static TimeOnly ParseStartTime(string value) =>
        TimeOnly.TryParseExact(
            value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var startTime)
            ? startTime
            : throw new SeedException($"Start time '{value}' in demo-seed.json must read HH:mm.");

    private static SeedDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EventBooking.SeedData.demo-seed.json")
            ?? throw new SeedException("Embedded demo-seed.json is missing.");

        var document = JsonSerializer.Deserialize<SeedFile>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new SeedException("demo-seed.json is empty.");

        var staffById = new HashSet<Guid>();
        var staffIds = new HashSet<StaffId>();
        var staff = document.Staff.Select(row =>
        {
            if (!Guid.TryParse(row.UserId, out var userId) || userId == Guid.Empty)
            {
                throw new SeedException($"Staff entry '{row.Username}' has an invalid userId.");
            }

            if (!staffById.Add(userId))
            {
                throw new SeedException($"Duplicate staff userId '{row.UserId}'.");
            }

            var parsedRoles = row.Roles.Select(roleName =>
            {
                if (!Enum.TryParse<Role>(roleName, ignoreCase: true, out var role)
                    || !Enum.IsDefined(role))
                {
                    throw new SeedException(
                        $"Staff entry '{row.Username}' has an unknown role '{roleName}'.");
                }

                return role;
            }).ToList();

            Guid? appointmentTypeId = row.AppointmentType is null
                ? null
                : TypeId(row.AppointmentType);

            try
            {
                var staffId = new StaffId(
                    row.StaffId, StaffIdPatternOverride ?? StaffId.DefaultPattern);
                if (!staffIds.Add(staffId))
                {
                    throw new SeedException($"Duplicate staffId '{row.StaffId}'.");
                }

                var validated = StaffAccessProfile.Create(
                    userId, parsedRoles, appointmentTypeId);
                return new StaffRow(
                    row.Username,
                    new StaffProfileSpec(
                        row.Username,
                        userId,
                        staffId,
                        validated.Roles.OrderBy(role => role).ToList(),
                        validated.AppointmentTypeId));
            }
            catch (DomainException exception)
            {
                throw new SeedException(
                    $"Staff entry '{row.Username}' is invalid: {exception.Message}");
            }
        }).ToList();

        if (!DateOnly.TryParseExact(
                document.AnchorDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var anchor))
        {
            throw new SeedException("anchorDate in demo-seed.json must read yyyy-MM-dd.");
        }

        return new SeedDocument(
            anchor,
            staff.Select(s => s.Assignment).ToList(),
            staff.ToDictionary(s => s.Username, s => s.Assignment.UserId),
            document.AgreedEvents,
            document.OpenProposals,
            document.Attendees);
    }

    private sealed record StaffRow(string Username, StaffProfileSpec Assignment);

    private sealed record SeedDocument(
        DateOnly Anchor,
        IReadOnlyList<StaffProfileSpec> Staff,
        IReadOnlyDictionary<string, Guid> StaffByUsername,
        IReadOnlyList<AgreedEventRow> AgreedEvents,
        IReadOnlyList<OpenProposalRow> OpenProposals,
        IReadOnlyList<AttendeeRow> Attendees);

    private sealed record SeedFile(
        string AnchorDate,
        List<StaffRowFile> Staff,
        List<AgreedEventRow> AgreedEvents,
        List<OpenProposalRow> OpenProposals,
        List<AttendeeRow> Attendees);

    private sealed record StaffRowFile(
        string Username,
        string UserId,
        string StaffId,
        List<string> Roles,
        string? AppointmentType);

    private sealed record AgreedEventRow(
        int DaysOffset, string StartTime, int DatHeadcount, int MedHeadcount, int UniHeadcount);

    private sealed record OpenProposalRow(
        int DaysOffset, string StartTime, string CreatedBy,
        int? DatHeadcount, int? MedHeadcount, int? UniHeadcount);

    private sealed record AttendeeRow(string Name, string Email, string AttendeeGroup, string Journey);
}
