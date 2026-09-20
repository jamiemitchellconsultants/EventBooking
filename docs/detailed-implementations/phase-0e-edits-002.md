# 00e — Require an attendee group, edits 2 (Task 3c)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Attendees/AttendeeReadiness.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Attendees/AttendeeReadiness.cs","beforeSha":"3ee8c1525b87908cbbe9bb86646e4cc56f25767dc7c0f27b1c735fa4e40d7e5e","afterSha":"b5ce42f063618072dcdd336dd453e5b023b7d46283814bf11e6b43b125d52bc2","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Attendees;

/// <summary>Explains whether EventBooking has completed every current Attendee requirement.</summary>
public enum AttendeeReadinessCode
{
    /// <summary>Every current requirement has a Completed non-cancelled attempt.</summary>
    Ready = 1,
    /// <summary>The Attendee has no assigned Attendee Group during Release 1 reconciliation.</summary>
    AttendeeGroupUnassigned = 2,
    /// <summary>The Attendee has no Active original Booking journey.</summary>
    NoActiveBooking = 3,
    /// <summary>The current requirements differ from the journey's Appointment Type snapshots.</summary>
    RequirementSnapshotMismatch = 4,
    /// <summary>At least one current requirement has no Completed non-cancelled attempt.</summary>
    AppointmentsOutstanding = 5,
}

/// <summary>One booked attempt used to choose the latest non-cancelled result per type.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The attempted appointment type.</param>
/// <param name="Status">The appointment's operational status.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="BookingStatus">The parent booking lifecycle status.</param>
/// <param name="BookingCreatedAt">When the parent booking was created.</param>
public sealed record AttendeeReadinessAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    EventBooking.Domain.Bookings.BookingAppointmentStatus Status,
    Guid BookingId,
    EventBooking.Domain.Bookings.BookingStatus BookingStatus,
    DateTimeOffset BookingCreatedAt);

/// <summary>The authorized persistence projection consumed by the readiness calculator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="AttendeeGroupId">The assigned group, or null during reconciliation.</param>
/// <param name="CurrentRequirementTypeIds">The group's current requirement set.</param>
/// <param name="ActiveOriginalBookingId">The active journey root, or null when absent.</param>
/// <param name="Attempts">Every booked attempt in the original and recovery journey.</param>
public sealed record AttendeeReadinessSnapshot(
    Guid AttendeeId,
    Guid? AttendeeGroupId,
    IReadOnlyList<Guid> CurrentRequirementTypeIds,
    Guid? ActiveOriginalBookingId,
    IReadOnlyList<AttendeeReadinessAttempt> Attempts);

/// <summary>Minimum canonical detail for one incomplete current Appointment Type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether the latest attempt is a recoverable no-show.</param>
public sealed record OutstandingAppointmentType(
    string Code,
    string Name,
    bool IsRecoverable);

/// <summary>The internal EventBooking readiness result shown to a Coordinator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="Code">The machine-readable readiness reason.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete types sorted by code.</param>
public sealed record AttendeeReadiness(
    Guid AttendeeId,
    AttendeeReadinessCode Code,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);
`````

## after — src/EventBooking.Application/Attendees/AttendeeReadiness.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Application/Attendees/AttendeeReadiness.cs","beforeSha":"3ee8c1525b87908cbbe9bb86646e4cc56f25767dc7c0f27b1c735fa4e40d7e5e","afterSha":"b5ce42f063618072dcdd336dd453e5b023b7d46283814bf11e6b43b125d52bc2","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Attendees;

/// <summary>Explains whether EventBooking has completed every current Attendee requirement.</summary>
public enum AttendeeReadinessCode
{
    /// <summary>Every current requirement has a Completed non-cancelled attempt.</summary>
    Ready = 1,
    /// <summary>The Attendee has no Active original Booking journey.</summary>
    NoActiveBooking = 3,
    /// <summary>The current requirements differ from the journey's Appointment Type snapshots.</summary>
    RequirementSnapshotMismatch = 4,
    /// <summary>At least one current requirement has no Completed non-cancelled attempt.</summary>
    AppointmentsOutstanding = 5,
}

/// <summary>One booked attempt used to choose the latest non-cancelled result per type.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The attempted appointment type.</param>
/// <param name="Status">The appointment's operational status.</param>
/// <param name="BookingId">The parent booking identifier.</param>
/// <param name="BookingStatus">The parent booking lifecycle status.</param>
/// <param name="BookingCreatedAt">When the parent booking was created.</param>
public sealed record AttendeeReadinessAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    EventBooking.Domain.Bookings.BookingAppointmentStatus Status,
    Guid BookingId,
    EventBooking.Domain.Bookings.BookingStatus BookingStatus,
    DateTimeOffset BookingCreatedAt);

/// <summary>The authorized persistence projection consumed by the readiness calculator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="AttendeeGroupId">The required assigned group.</param>
/// <param name="CurrentRequirementTypeIds">The group's current requirement set.</param>
/// <param name="ActiveOriginalBookingId">The active journey root, or null when absent.</param>
/// <param name="Attempts">Every booked attempt in the original and recovery journey.</param>
public sealed record AttendeeReadinessSnapshot(
    Guid AttendeeId,
    Guid AttendeeGroupId,
    IReadOnlyList<Guid> CurrentRequirementTypeIds,
    Guid? ActiveOriginalBookingId,
    IReadOnlyList<AttendeeReadinessAttempt> Attempts);

/// <summary>Minimum canonical detail for one incomplete current Appointment Type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether the latest attempt is a recoverable no-show.</param>
public sealed record OutstandingAppointmentType(
    string Code,
    string Name,
    bool IsRecoverable);

/// <summary>The internal EventBooking readiness result shown to a Coordinator.</summary>
/// <param name="AttendeeId">The attendee identifier.</param>
/// <param name="Code">The machine-readable readiness reason.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete types sorted by code.</param>
public sealed record AttendeeReadiness(
    Guid AttendeeId,
    AttendeeReadinessCode Code,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);
`````

## before — src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs","beforeSha":"55f9f7684b5e648cfdd97e85f3abf1e05dc89b236d5453b19713949a99bbc409","afterSha":"06df37e2a42491547a01f9635a5e4b1bc29d1b32debb821d9e091175508cef97","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Attendees;

/// <summary>Calculates deterministic readiness from one persistence snapshot.</summary>
public sealed class AttendeeReadinessCalculator
{
    /// <summary>Calculates one deterministic result without inferring an Attendee Group.</summary>
    /// <param name="snapshot">The authorized journey projection.</param>
    /// <returns>Ready, or the highest-precedence failure with outstanding types.</returns>
    public AttendeeReadiness Calculate(AttendeeReadinessSnapshot snapshot)
    {
        if (!snapshot.AttendeeGroupId.HasValue)
        {
            return new AttendeeReadiness(
                snapshot.AttendeeId, AttendeeReadinessCode.AttendeeGroupUnassigned, []);
        }

        if (!snapshot.ActiveOriginalBookingId.HasValue)
        {
            return new AttendeeReadiness(
                snapshot.AttendeeId, AttendeeReadinessCode.NoActiveBooking, []);
        }

        var current = snapshot.CurrentRequirementTypeIds;
        if (current.Count == 0
            || current.Distinct().Count() != current.Count
            || current.Any(id => !AppointmentTypeIds.All.Contains(id)))
        {
            return Mismatch(snapshot);
        }

        var live = snapshot.Attempts
            .Where(attempt => attempt.BookingStatus != BookingStatus.Cancelled)
            .ToList();

        var originalTypes = live
            .Where(attempt => attempt.BookingId == snapshot.ActiveOriginalBookingId.Value)
            .Select(attempt => attempt.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (!originalTypes.SequenceEqual(current.Order())
            || live.Any(attempt => !originalTypes.Contains(attempt.AppointmentTypeId)))
        {
            return Mismatch(snapshot);
        }

        var outstanding = new List<OutstandingAppointmentType>();
        foreach (var typeId in current.Order())
        {
            var attempts = live
                .Where(attempt => attempt.AppointmentTypeId == typeId)
                .OrderBy(attempt => attempt.BookingCreatedAt)
                .ThenBy(attempt => attempt.BookingAppointmentId)
                .ToList();

            if (attempts.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            {
                continue;
            }

            var latest = attempts.Count == 0 ? null : attempts[^1];
            outstanding.Add(new OutstandingAppointmentType(
                AppointmentTypeIds.CodeOf(typeId),
                AppointmentTypeIds.NameOf(typeId),
                latest is not null && latest.Status == BookingAppointmentStatus.NoShow));
        }

        outstanding.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));

        return outstanding.Count == 0
            ? new AttendeeReadiness(snapshot.AttendeeId, AttendeeReadinessCode.Ready, [])
            : new AttendeeReadiness(
                snapshot.AttendeeId, AttendeeReadinessCode.AppointmentsOutstanding, outstanding);
    }

    private static AttendeeReadiness Mismatch(AttendeeReadinessSnapshot snapshot) =>
        new(snapshot.AttendeeId, AttendeeReadinessCode.RequirementSnapshotMismatch, []);
}
`````

## after — src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs","beforeSha":"55f9f7684b5e648cfdd97e85f3abf1e05dc89b236d5453b19713949a99bbc409","afterSha":"06df37e2a42491547a01f9635a5e4b1bc29d1b32debb821d9e091175508cef97","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Attendees;

/// <summary>Calculates deterministic readiness from one persistence snapshot.</summary>
public sealed class AttendeeReadinessCalculator
{
    /// <summary>Calculates one deterministic result without inferring an Attendee Group.</summary>
    /// <param name="snapshot">The authorized journey projection.</param>
    /// <returns>Ready, or the highest-precedence failure with outstanding types.</returns>
    public AttendeeReadiness Calculate(AttendeeReadinessSnapshot snapshot)
    {
        if (!snapshot.ActiveOriginalBookingId.HasValue)
        {
            return new AttendeeReadiness(
                snapshot.AttendeeId, AttendeeReadinessCode.NoActiveBooking, []);
        }

        var current = snapshot.CurrentRequirementTypeIds;
        if (current.Count == 0
            || current.Distinct().Count() != current.Count
            || current.Any(id => !AppointmentTypeIds.All.Contains(id)))
        {
            return Mismatch(snapshot);
        }

        var live = snapshot.Attempts
            .Where(attempt => attempt.BookingStatus != BookingStatus.Cancelled)
            .ToList();

        var originalTypes = live
            .Where(attempt => attempt.BookingId == snapshot.ActiveOriginalBookingId.Value)
            .Select(attempt => attempt.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (!originalTypes.SequenceEqual(current.Order())
            || live.Any(attempt => !originalTypes.Contains(attempt.AppointmentTypeId)))
        {
            return Mismatch(snapshot);
        }

        var outstanding = new List<OutstandingAppointmentType>();
        foreach (var typeId in current.Order())
        {
            var attempts = live
                .Where(attempt => attempt.AppointmentTypeId == typeId)
                .OrderBy(attempt => attempt.BookingCreatedAt)
                .ThenBy(attempt => attempt.BookingAppointmentId)
                .ToList();

            if (attempts.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            {
                continue;
            }

            var latest = attempts.Count == 0 ? null : attempts[^1];
            outstanding.Add(new OutstandingAppointmentType(
                AppointmentTypeIds.CodeOf(typeId),
                AppointmentTypeIds.NameOf(typeId),
                latest is not null && latest.Status == BookingAppointmentStatus.NoShow));
        }

        outstanding.Sort((left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));

        return outstanding.Count == 0
            ? new AttendeeReadiness(snapshot.AttendeeId, AttendeeReadinessCode.Ready, [])
            : new AttendeeReadiness(
                snapshot.AttendeeId, AttendeeReadinessCode.AppointmentsOutstanding, outstanding);
    }

    private static AttendeeReadiness Mismatch(AttendeeReadinessSnapshot snapshot) =>
        new(snapshot.AttendeeId, AttendeeReadinessCode.RequirementSnapshotMismatch, []);
}
`````

## before — src/EventBooking.Application/Attendees/ListAttendeesHandler.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Attendees/ListAttendeesHandler.cs","beforeSha":"973d25e90d630c53c6f38fd6519512fc12dbf421e93bb88a6a95f7a04be73275","afterSha":"ea549126f7e9b6e3e9f57db71b56e79f7d1180df7671890d1688befaca1d3838","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>One Attendee with its assigned Attendee Group and derived requirements.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="AttendeeGroupCode">The attendee group code.</param>
/// <param name="AttendeeGroupName">The attendee group name.</param>
/// <param name="RequiresAttendeeGroupReconciliation">The requires attendee group reconciliation.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
/// <param name="Status">The status.</param>
/// <param name="StatusDisplay">The status display.</param>
public sealed record AttendeeListItem(
    Guid AttendeeId,
    string Name,
    string Email,
    Guid? AttendeeGroupId,
    string? AttendeeGroupCode,
    string? AttendeeGroupName,
    bool RequiresAttendeeGroupReconciliation,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    AttendeeStatus Status,
    string StatusDisplay);

/// <summary>Requests Attendees, optionally narrowed by status or search text.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Status">The status.</param>
/// <param name="Search">The search.</param>
public sealed record ListAttendeesQuery(Guid StaffUserId, AttendeeStatus? Status, string? Search);

/// <summary>Lists attendees with their assigned Attendee Group and derived requirements.</summary>
/// <param name="attendees">Reads attendee rows.</param>
/// <param name="groups">Resolves assigned Attendee Group identity.</param>
/// <param name="access">Authorizes attendee management.</param>
public sealed class ListAttendeesHandler(
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>The Area C screen wording for each status.</summary>
    /// <param name="status">The status.</param>
    public static string DisplayOf(AttendeeStatus status) => status switch
    {
        AttendeeStatus.NotYetInvited => "Not yet invited",
        AttendeeStatus.AwaitingAvailability => "Awaiting availability",
        AttendeeStatus.Invited => "Invited (pending response)",
        AttendeeStatus.Booked => "Booked",
        AttendeeStatus.NoResponseNeedsFollowUp => "No response - needs follow-up",
        _ => status.ToString(),
    };

    /// <summary>Returns matching Attendees ordered by name with group identity.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AttendeeListItem>>> HandleAsync(
        ListAttendeesQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AttendeeListItem>>.Failure(authorized.Error);
        }

        var all = await attendees.ListAsync(query.Status, cancellationToken);

        var search = query.Search?.Trim();
        var filtered = string.IsNullOrEmpty(search)
            ? all
            : all
                .Where(c =>
                    c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || c.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // The five reference rows are loaded once; only exceptional unlisted groups fall back
        // to an individual lookup.
        var reference = (await groups.ListActiveAsync(cancellationToken))
            .ToDictionary(group => group.Id);
        foreach (var missing in filtered
            .Select(attendee => attendee.AttendeeGroupId)
            .Where(id => id.HasValue && !reference.ContainsKey(id.Value))
            .Select(id => id!.Value)
            .Distinct()
            .ToList())
        {
            var group = await groups.GetAsync(missing, cancellationToken);
            if (group is not null)
            {
                reference[missing] = group;
            }
        }

        var items = filtered
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new AttendeeListItem(
                c.Id,
                c.Name,
                c.Email,
                c.AttendeeGroupId,
                c.AttendeeGroupId.HasValue && reference.TryGetValue(c.AttendeeGroupId.Value, out var group)
                    ? group.Code
                    : null,
                c.AttendeeGroupId.HasValue && reference.TryGetValue(c.AttendeeGroupId.Value, out var named)
                    ? named.Name
                    : null,
                !c.AttendeeGroupId.HasValue,
                c.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList(),
                c.Status,
                DisplayOf(c.Status)))
            .ToList();

        return Result<IReadOnlyList<AttendeeListItem>>.Success(items);
    }
}
`````

## after — src/EventBooking.Application/Attendees/ListAttendeesHandler.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Attendees/ListAttendeesHandler.cs","beforeSha":"973d25e90d630c53c6f38fd6519512fc12dbf421e93bb88a6a95f7a04be73275","afterSha":"ea549126f7e9b6e3e9f57db71b56e79f7d1180df7671890d1688befaca1d3838","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>One Attendee with its assigned Attendee Group and derived requirements.</summary>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="AttendeeGroupCode">The attendee group code.</param>
/// <param name="AttendeeGroupName">The attendee group name.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
/// <param name="Status">The status.</param>
/// <param name="StatusDisplay">The status display.</param>
public sealed record AttendeeListItem(
    Guid AttendeeId,
    string Name,
    string Email,
    Guid AttendeeGroupId,
    string AttendeeGroupCode,
    string AttendeeGroupName,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes,
    AttendeeStatus Status,
    string StatusDisplay);

/// <summary>Requests Attendees, optionally narrowed by status or search text.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Status">The status.</param>
/// <param name="Search">The search.</param>
public sealed record ListAttendeesQuery(Guid StaffUserId, AttendeeStatus? Status, string? Search);

/// <summary>Lists attendees with their assigned Attendee Group and derived requirements.</summary>
/// <param name="attendees">Reads attendee rows.</param>
/// <param name="groups">Resolves assigned Attendee Group identity.</param>
/// <param name="access">Authorizes attendee management.</param>
public sealed class ListAttendeesHandler(
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    IStaffAccessAuthorizer access)
{
    /// <summary>The Area C screen wording for each status.</summary>
    /// <param name="status">The status.</param>
    public static string DisplayOf(AttendeeStatus status) => status switch
    {
        AttendeeStatus.NotYetInvited => "Not yet invited",
        AttendeeStatus.AwaitingAvailability => "Awaiting availability",
        AttendeeStatus.Invited => "Invited (pending response)",
        AttendeeStatus.Booked => "Booked",
        AttendeeStatus.NoResponseNeedsFollowUp => "No response - needs follow-up",
        _ => status.ToString(),
    };

    /// <summary>Returns matching Attendees ordered by name with group identity.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<AttendeeListItem>>> HandleAsync(
        ListAttendeesQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<AttendeeListItem>>.Failure(authorized.Error);
        }

        var all = await attendees.ListAsync(query.Status, cancellationToken);

        var search = query.Search?.Trim();
        var filtered = string.IsNullOrEmpty(search)
            ? all
            : all
                .Where(c =>
                    c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || c.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        // The five reference rows are loaded once; only exceptional unlisted groups fall back
        // to an individual lookup.
        var reference = (await groups.ListActiveAsync(cancellationToken))
            .ToDictionary(group => group.Id);
        foreach (var missing in filtered
            .Select(attendee => attendee.AttendeeGroupId)
            .Where(id => !reference.ContainsKey(id))
            .Distinct()
            .ToList())
        {
            var group = await groups.GetAsync(missing, cancellationToken);
            if (group is not null)
            {
                reference[missing] = group;
            }
        }

        var items = filtered
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new AttendeeListItem(
                c.Id,
                c.Name,
                c.Email,
                c.AttendeeGroupId,
                reference[c.AttendeeGroupId].Code,
                reference[c.AttendeeGroupId].Name,
                c.RequiredAppointmentTypeIds
                    .Select(typeId => new AppointmentTypeSummary(
                        AppointmentTypeIds.CodeOf(typeId), AppointmentTypeIds.NameOf(typeId)))
                    .OrderBy(summary => summary.Code, StringComparer.Ordinal)
                    .ToList(),
                c.Status,
                DisplayOf(c.Status)))
            .ToList();

        return Result<IReadOnlyList<AttendeeListItem>>.Success(items);
    }
}
`````

## before — src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs","beforeSha":"5972e8ec6386ef1ce8b34be8dca959a1ce99d6f3dbff155ea4a1393f6e478059","afterSha":"31345607e76e4112c64dce27ed7c303250bb8feedf849f2257a7f65ea54aad89","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>Requests creation of a Attendee in one Attendee Group.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
public sealed record CreateAttendeeCommand(
    Guid StaffUserId,
    string? Name,
    string? Email,
    Guid? AttendeeGroupId);

/// <summary>Requests detail and Attendee Group changes for one Attendee.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
public sealed record UpdateAttendeeCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    string? Name,
    string? Email,
    Guid? AttendeeGroupId);

/// <summary>
/// Creates and updates attendees from one active Attendee Group, serializing group edits with
/// the Attendee lifecycle under one Attendee-first lock order.
/// </summary>
public sealed class SaveAttendeeHandler
{
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAttendeeRepository _attendees;
    private readonly IAttendeeGroupRepository _groups;
    private readonly IInviteRepository _invites;
    private readonly IBookingRepository _bookings;
    private readonly IStaffAccessAuthorizer _access;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Creates handler dependencies for group-derived attendee management.</summary>
    /// <param name="attendees">Persists attendee rows.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="invites">Locks the attendee's pending invite for lifecycle work.</param>
    /// <param name="bookings">Locks the attendee's active booking for the requirement invariant.</param>
    /// <param name="access">Authorizes attendee management.</param>
    /// <param name="audit">Records Attendee Group assignment.</param>
    /// <param name="unitOfWork">Owns the attendee save.</param>
    public SaveAttendeeHandler(
        IAttendeeRepository attendees,
        IAttendeeGroupRepository groups,
        IInviteRepository invites,
        IBookingRepository bookings,
        IStaffAccessAuthorizer access,
        IAuditLogger audit,
        IUnitOfWork unitOfWork)
    {
        _attendees = attendees;
        _groups = groups;
        _invites = invites;
        _bookings = bookings;
        _access = access;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Creates one attendee and derives every requirement from its Attendee Group.</summary>
    /// <param name="command">The staff creation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attendee identifier.</returns>
    public async Task<Result<Guid>> CreateAsync(
        CreateAttendeeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        var resolved = await ResolveGroupAsync(command.AttendeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<Guid>.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (await _attendees.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return Result<Guid>.Failure(Error.Conflict($"{email} is already a attendee."));
        }

        var id = Guid.NewGuid();

        Attendee attendee;
        try
        {
            attendee = Attendee.Create(id, command.Name, command.Email, resolved.Value);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        _attendees.Add(attendee);
        _audit.Record(
            AuditEntityTypes.Attendee,
            id,
            AuditAction.AttendeeGroupAssigned,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(null, resolved.Value.Code, [], RequirementCodes(resolved.Value)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(id);
    }

    /// <summary>
    /// Updates attendee details under the Attendee lifecycle lock, superseding a pending Invite
    /// and resetting status when the derived set changes, and rejecting set changes that would
    /// alter an active original Booking.
    /// </summary>
    /// <param name="command">The staff update request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success, or a conflict when requirements drift from the active booking snapshot.</returns>
    public async Task<Result> UpdateAsync(
        UpdateAttendeeCommand command,
        CancellationToken cancellationToken)
    {
        var updateAuthorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (updateAuthorized.IsFailure)
        {
            return Result.Failure(updateAuthorized.Error);
        }

        if (await _attendees.GetAsync(command.AttendeeId, cancellationToken) is null)
        {
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await _attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var resolved = await ResolveGroupAsync(command.AttendeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        var owner = await _attendees.GetByEmailAsync(email, cancellationToken);
        if (owner is not null && owner.Id != attendee.Id)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict($"{email} is already a attendee."));
        }

        var pendingInvite = await _invites.LockPendingInitialForAttendeeAsync(
            attendee.Id, cancellationToken);
        var activeBooking = await _bookings.LockActiveOriginalForAttendeeAsync(
            attendee.Id, cancellationToken);

        var submittedRequirements = resolved.Value.RequiredAppointmentTypeIds.ToHashSet();
        var currentRequirements = attendee.RequiredAppointmentTypeIds.ToHashSet();
        var setChanged = !submittedRequirements.SetEquals(currentRequirements);
        var detailsChanged =
            !string.Equals((command.Name ?? string.Empty).Trim(), attendee.Name, StringComparison.Ordinal)
            || !string.Equals(email, attendee.Email, StringComparison.Ordinal);
        var groupChanged = attendee.AttendeeGroupId != resolved.Value.Id;

        if (!setChanged && !detailsChanged && !groupChanged)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        if (setChanged && activeBooking is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.AttendeeGroupActiveBookingConflict(
                "Appointment requirements cannot change while the attendee has an active booking. "
                + "Cancel and rebook first."));
        }

        string? oldGroupCode = null;
        if (attendee.AttendeeGroupId.HasValue)
        {
            oldGroupCode = (await _groups.GetAsync(attendee.AttendeeGroupId.Value, cancellationToken))?.Code;
        }

        var oldRequirementCodes = RequirementCodes(attendee.RequiredAppointmentTypeIds);
        var newRequirementCodes = RequirementCodes(resolved.Value.RequiredAppointmentTypeIds);

        try
        {
            attendee.UpdateDetails(command.Name, command.Email);

            if (setChanged)
            {
                pendingInvite?.MarkSuperseded();
                attendee.ResetAfterRequirementChange();
            }

            attendee.AssignAttendeeGroup(resolved.Value);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        _audit.Record(
            AuditEntityTypes.Attendee,
            attendee.Id,
            AuditAction.AttendeeGroupReassigned,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(oldGroupCode, resolved.Value.Code, oldRequirementCodes, newRequirementCodes));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<AttendeeGroup>> ResolveGroupAsync(
        Guid? attendeeGroupId,
        CancellationToken cancellationToken)
    {
        if (attendeeGroupId is null)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_required", "An attendee group is required."));
        }

        var group = await _groups.GetAsync(attendeeGroupId.Value, cancellationToken);
        if (group is null)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_unknown", "The attendee group is not known."));
        }

        if (!group.IsActive)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_inactive", "The attendee group is not active."));
        }

        if (group.RequiredAppointmentTypeIds.Count == 0)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_unmapped", "The attendee group has no mapped appointment types."));
        }

        return Result<AttendeeGroup>.Success(group);
    }

    private static IReadOnlyList<string> RequirementCodes(AttendeeGroup group) =>
        RequirementCodes(group.RequiredAppointmentTypeIds);

    private static IReadOnlyList<string> RequirementCodes(IEnumerable<Guid> appointmentTypeIds) =>
        appointmentTypeIds.Select(AppointmentTypeIds.CodeOf).Order(StringComparer.Ordinal).ToList();

    private static string SerializeAssignment(
        string? oldGroupCode,
        string newGroupCode,
        IReadOnlyList<string> oldRequirementCodes,
        IReadOnlyList<string> newRequirementCodes) =>
        JsonSerializer.Serialize(
            new GroupAssignmentAudit(oldGroupCode, newGroupCode, oldRequirementCodes, newRequirementCodes),
            AuditJson);

    private sealed record GroupAssignmentAudit(
        string? OldGroupCode,
        string NewGroupCode,
        IReadOnlyList<string> OldRequirementCodes,
        IReadOnlyList<string> NewRequirementCodes);
}
`````

## after — src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs","beforeSha":"5972e8ec6386ef1ce8b34be8dca959a1ce99d6f3dbff155ea4a1393f6e478059","afterSha":"31345607e76e4112c64dce27ed7c303250bb8feedf849f2257a7f65ea54aad89","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Attendees;

/// <summary>Requests creation of a Attendee in one Attendee Group.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
public sealed record CreateAttendeeCommand(
    Guid StaffUserId,
    string? Name,
    string? Email,
    Guid? AttendeeGroupId);

/// <summary>Requests detail and Attendee Group changes for one Attendee.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupId">The attendee group id.</param>
public sealed record UpdateAttendeeCommand(
    Guid StaffUserId,
    Guid AttendeeId,
    string? Name,
    string? Email,
    Guid? AttendeeGroupId);

/// <summary>
/// Creates and updates attendees from one active Attendee Group, serializing group edits with
/// the Attendee lifecycle under one Attendee-first lock order.
/// </summary>
public sealed class SaveAttendeeHandler
{
    private static readonly JsonSerializerOptions AuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAttendeeRepository _attendees;
    private readonly IAttendeeGroupRepository _groups;
    private readonly IInviteRepository _invites;
    private readonly IBookingRepository _bookings;
    private readonly IStaffAccessAuthorizer _access;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>Creates handler dependencies for group-derived attendee management.</summary>
    /// <param name="attendees">Persists attendee rows.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="invites">Locks the attendee's pending invite for lifecycle work.</param>
    /// <param name="bookings">Locks the attendee's active booking for the requirement invariant.</param>
    /// <param name="access">Authorizes attendee management.</param>
    /// <param name="audit">Records Attendee Group assignment.</param>
    /// <param name="unitOfWork">Owns the attendee save.</param>
    public SaveAttendeeHandler(
        IAttendeeRepository attendees,
        IAttendeeGroupRepository groups,
        IInviteRepository invites,
        IBookingRepository bookings,
        IStaffAccessAuthorizer access,
        IAuditLogger audit,
        IUnitOfWork unitOfWork)
    {
        _attendees = attendees;
        _groups = groups;
        _invites = invites;
        _bookings = bookings;
        _access = access;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Creates one attendee and derives every requirement from its Attendee Group.</summary>
    /// <param name="command">The staff creation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attendee identifier.</returns>
    public async Task<Result<Guid>> CreateAsync(
        CreateAttendeeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        var resolved = await ResolveGroupAsync(command.AttendeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<Guid>.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (await _attendees.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return Result<Guid>.Failure(Error.Conflict($"{email} is already a attendee."));
        }

        var id = Guid.NewGuid();

        Attendee attendee;
        try
        {
            attendee = Attendee.Create(id, command.Name, command.Email, resolved.Value);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        _attendees.Add(attendee);
        _audit.Record(
            AuditEntityTypes.Attendee,
            id,
            AuditAction.AttendeeGroupAssigned,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(null, resolved.Value.Code, [], RequirementCodes(resolved.Value)));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(id);
    }

    /// <summary>
    /// Updates attendee details under the Attendee lifecycle lock, superseding a pending Invite
    /// and resetting status when the derived set changes, and rejecting set changes that would
    /// alter an active original Booking.
    /// </summary>
    /// <param name="command">The staff update request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Success, or a conflict when requirements drift from the active booking snapshot.</returns>
    public async Task<Result> UpdateAsync(
        UpdateAttendeeCommand command,
        CancellationToken cancellationToken)
    {
        var updateAuthorized = await _access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (updateAuthorized.IsFailure)
        {
            return Result.Failure(updateAuthorized.Error);
        }

        if (await _attendees.GetAsync(command.AttendeeId, cancellationToken) is null)
        {
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var attendee = await _attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var resolved = await ResolveGroupAsync(command.AttendeeGroupId, cancellationToken);
        if (resolved.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(resolved.Error);
        }

        var email = (command.Email ?? string.Empty).Trim().ToLowerInvariant();
        var owner = await _attendees.GetByEmailAsync(email, cancellationToken);
        if (owner is not null && owner.Id != attendee.Id)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict($"{email} is already a attendee."));
        }

        var pendingInvite = await _invites.LockPendingInitialForAttendeeAsync(
            attendee.Id, cancellationToken);
        var activeBooking = await _bookings.LockActiveOriginalForAttendeeAsync(
            attendee.Id, cancellationToken);

        var submittedRequirements = resolved.Value.RequiredAppointmentTypeIds.ToHashSet();
        var currentRequirements = attendee.RequiredAppointmentTypeIds.ToHashSet();
        var setChanged = !submittedRequirements.SetEquals(currentRequirements);
        var detailsChanged =
            !string.Equals((command.Name ?? string.Empty).Trim(), attendee.Name, StringComparison.Ordinal)
            || !string.Equals(email, attendee.Email, StringComparison.Ordinal);
        var groupChanged = attendee.AttendeeGroupId != resolved.Value.Id;

        if (!setChanged && !detailsChanged && !groupChanged)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        if (setChanged && activeBooking is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.AttendeeGroupActiveBookingConflict(
                "Appointment requirements cannot change while the attendee has an active booking. "
                + "Cancel and rebook first."));
        }

        var oldGroupCode = (await _groups.GetAsync(attendee.AttendeeGroupId, cancellationToken))?.Code;

        var oldRequirementCodes = RequirementCodes(attendee.RequiredAppointmentTypeIds);
        var newRequirementCodes = RequirementCodes(resolved.Value.RequiredAppointmentTypeIds);

        try
        {
            attendee.UpdateDetails(command.Name, command.Email);

            if (setChanged)
            {
                pendingInvite?.MarkSuperseded();
                attendee.ResetAfterRequirementChange();
            }

            attendee.AssignAttendeeGroup(resolved.Value);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        _audit.Record(
            AuditEntityTypes.Attendee,
            attendee.Id,
            AuditAction.AttendeeGroupReassigned,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            SerializeAssignment(oldGroupCode, resolved.Value.Code, oldRequirementCodes, newRequirementCodes));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<AttendeeGroup>> ResolveGroupAsync(
        Guid? attendeeGroupId,
        CancellationToken cancellationToken)
    {
        if (attendeeGroupId is null)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_required", "An attendee group is required."));
        }

        var group = await _groups.GetAsync(attendeeGroupId.Value, cancellationToken);
        if (group is null)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_unknown", "The attendee group is not known."));
        }

        if (!group.IsActive)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_inactive", "The attendee group is not active."));
        }

        if (group.RequiredAppointmentTypeIds.Count == 0)
        {
            return Result<AttendeeGroup>.Failure(
                new Error("attendee_group_unmapped", "The attendee group has no mapped appointment types."));
        }

        return Result<AttendeeGroup>.Success(group);
    }

    private static IReadOnlyList<string> RequirementCodes(AttendeeGroup group) =>
        RequirementCodes(group.RequiredAppointmentTypeIds);

    private static IReadOnlyList<string> RequirementCodes(IEnumerable<Guid> appointmentTypeIds) =>
        appointmentTypeIds.Select(AppointmentTypeIds.CodeOf).Order(StringComparer.Ordinal).ToList();

    private static string SerializeAssignment(
        string? oldGroupCode,
        string newGroupCode,
        IReadOnlyList<string> oldRequirementCodes,
        IReadOnlyList<string> newRequirementCodes) =>
        JsonSerializer.Serialize(
            new GroupAssignmentAudit(oldGroupCode, newGroupCode, oldRequirementCodes, newRequirementCodes),
            AuditJson);

    private sealed record GroupAssignmentAudit(
        string? OldGroupCode,
        string NewGroupCode,
        IReadOnlyList<string> OldRequirementCodes,
        IReadOnlyList<string> NewRequirementCodes);
}
`````

## before — src/EventBooking.Application/Common/Error.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Common/Error.cs","beforeSha":"2a315f16dddbcde1aa683f5aeb04df4f98696839c1519efbbd64cf579321fdb3","afterSha":"a0d048e09334ab21175a17dd039941b5b895db498541d32f1d5a36d7a79cd8ff","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Common;

/// <summary>A machine-readable code plus text safe to show a user.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Identifies a stale booking-appointment version conflict.</summary>
    public const string AppointmentVersionConflictCode = "appointment_version_conflict";

    /// <summary>Defines none for the current use case.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Defines validation for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Validation(string message) => new("validation", message);

    /// <summary>Defines not found for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>Defines conflict for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>Creates a stale booking-appointment version conflict.</summary>
    /// <param name="message">The message.</param>
    public static Error AppointmentVersionConflict(string message) =>
        new(AppointmentVersionConflictCode, message);

    /// <summary>Identifies a group change that would alter an active Booking's requirements.</summary>
    public const string AttendeeGroupActiveBookingConflictCode = "attendee_group_active_booking_conflict";

    /// <summary>Creates a group change rejected by an active Booking.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeGroupActiveBookingConflict(string message) =>
        new(AttendeeGroupActiveBookingConflictCode, message);

    /// <summary>Identifies an Invite or readiness action for a legacy unassigned Attendee.</summary>
    public const string AttendeeReconciliationRequiredCode = "attendee_reconciliation_required";

    /// <summary>Creates a reconciliation hold for a legacy unassigned Attendee.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeReconciliationRequired(string message) =>
        new(AttendeeReconciliationRequiredCode, message);

    /// <summary>Identifies materialized requirements disagreeing with authoritative state.</summary>
    public const string AttendeeRequirementSnapshotMismatchCode = "attendee_requirement_snapshot_mismatch";

    /// <summary>Creates a mismatch between materialized and authoritative requirements.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeRequirementSnapshotMismatch(string message) =>
        new(AttendeeRequirementSnapshotMismatchCode, message);

    /// <summary>Defines forbidden for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Forbidden(string message) => new("forbidden", message);

    /// <summary>Identifies a recovery request while a pending Invite or active recovery exists.</summary>
    public const string RecoveryAlreadyPendingCode = "recovery_already_pending";

    /// <summary>Creates a recovery request rejected by an already-pending recovery.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryAlreadyPending(string message) =>
        new(RecoveryAlreadyPendingCode, message);

    /// <summary>Identifies a recovery request with no recoverable no-show type.</summary>
    public const string RecoveryNotAvailableCode = "recovery_not_available";

    /// <summary>Creates a recovery request with nothing eligible to recover.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryNotAvailable(string message) =>
        new(RecoveryNotAvailableCode, message);

    /// <summary>Identifies a recovery request invalidated by a concurrent eligibility change.</summary>
    public const string RecoveryStateChangedCode = "recovery_state_changed";

    /// <summary>Creates a recovery request invalidated by a concurrent eligibility change.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryStateChanged(string message) =>
        new(RecoveryStateChangedCode, message);
}
`````

## after — src/EventBooking.Application/Common/Error.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Application/Common/Error.cs","beforeSha":"2a315f16dddbcde1aa683f5aeb04df4f98696839c1519efbbd64cf579321fdb3","afterSha":"a0d048e09334ab21175a17dd039941b5b895db498541d32f1d5a36d7a79cd8ff","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Common;

/// <summary>A machine-readable code plus text safe to show a user.</summary>
/// <param name="Code">The code.</param>
/// <param name="Message">The message.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Identifies a stale booking-appointment version conflict.</summary>
    public const string AppointmentVersionConflictCode = "appointment_version_conflict";

    /// <summary>Defines none for the current use case.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Defines validation for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Validation(string message) => new("validation", message);

    /// <summary>Defines not found for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error NotFound(string message) => new("not_found", message);

    /// <summary>Defines conflict for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Conflict(string message) => new("conflict", message);

    /// <summary>Creates a stale booking-appointment version conflict.</summary>
    /// <param name="message">The message.</param>
    public static Error AppointmentVersionConflict(string message) =>
        new(AppointmentVersionConflictCode, message);

    /// <summary>Identifies a group change that would alter an active Booking's requirements.</summary>
    public const string AttendeeGroupActiveBookingConflictCode = "attendee_group_active_booking_conflict";

    /// <summary>Creates a group change rejected by an active Booking.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeGroupActiveBookingConflict(string message) =>
        new(AttendeeGroupActiveBookingConflictCode, message);

    /// <summary>Identifies materialized requirements disagreeing with authoritative state.</summary>
    public const string AttendeeRequirementSnapshotMismatchCode = "attendee_requirement_snapshot_mismatch";

    /// <summary>Creates a mismatch between materialized and authoritative requirements.</summary>
    /// <param name="message">The message.</param>
    public static Error AttendeeRequirementSnapshotMismatch(string message) =>
        new(AttendeeRequirementSnapshotMismatchCode, message);

    /// <summary>Defines forbidden for the current use case.</summary>
    /// <param name="message">The message.</param>
    public static Error Forbidden(string message) => new("forbidden", message);

    /// <summary>Identifies a recovery request while a pending Invite or active recovery exists.</summary>
    public const string RecoveryAlreadyPendingCode = "recovery_already_pending";

    /// <summary>Creates a recovery request rejected by an already-pending recovery.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryAlreadyPending(string message) =>
        new(RecoveryAlreadyPendingCode, message);

    /// <summary>Identifies a recovery request with no recoverable no-show type.</summary>
    public const string RecoveryNotAvailableCode = "recovery_not_available";

    /// <summary>Creates a recovery request with nothing eligible to recover.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryNotAvailable(string message) =>
        new(RecoveryNotAvailableCode, message);

    /// <summary>Identifies a recovery request invalidated by a concurrent eligibility change.</summary>
    public const string RecoveryStateChangedCode = "recovery_state_changed";

    /// <summary>Creates a recovery request invalidated by a concurrent eligibility change.</summary>
    /// <param name="message">The message.</param>
    public static Error RecoveryStateChanged(string message) =>
        new(RecoveryStateChangedCode, message);
}
`````
