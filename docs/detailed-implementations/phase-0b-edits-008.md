# 00b — Vocabulary edits 8 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Application/Access/StaffAccessAuthorizer.cs — 1/1

<!-- vocabulary-file: {"id":42,"oldPath":"src/EventBooking.Application/Access/StaffAccessAuthorizer.cs","newPath":"src/EventBooking.Application/Access/StaffAccessAuthorizer.cs","beforeSha":"da274031e8a6e69e4d21dbb6dac39236a2eeb52a62b563395fd4c22cffb1409d","afterSha":"49ce24da2329dcc629c46b8e6903e08a15e408e9a7682059eedb047e6eede5de","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Defines staff access context for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
public sealed record StaffAccessContext(
    Guid StaffUserId,
    IReadOnlySet<Role> Roles,
    Guid? AppointmentTypeId);

/// <summary>Defines istaff access authorizer for the current use case.</summary>
public interface IStaffAccessAuthorizer
{
    /// <summary>Provides authorize async within this contract.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken);
}

/// <summary>Defines staff access authorizer for the current use case.</summary>
/// <param name="profiles">The profiles.</param>
public sealed class StaffAccessAuthorizer(IStaffAccessProfileRepository profiles)
    : IStaffAccessAuthorizer
{
    private static readonly Error Denied = Error.Forbidden("This staff profile cannot perform this operation.");

    /// <summary>Defines authorize async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="capability">The capability.</param>
    /// <param name="requiredAppointmentTypeId">The required appointment type id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessContext>> AuthorizeAsync(
        Guid staffUserId,
        StaffCapability capability,
        Guid? requiredAppointmentTypeId,
        CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(staffUserId, cancellationToken);
        if (profile is null || !profile.IsValid() || !IsAllowed(profile, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        // A Manager or AppointmentStaff profile with no appointment-type scope grants no
        // capabilities on its own: the null scope denies every capability unless another role
        // on the same profile grants it.
        if (StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null
            && !IsAllowed(profile.IsAdmin, profile.IsCoordinator, false, false, capability))
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        var scopedCapability = capability is
            StaffCapability.ManageEventNegotiation or
            StaffCapability.ViewEventOperations or
            StaffCapability.ConductAppointments;

        if (scopedCapability
            && StaffAccessProfile.NeedsScope(profile.Roles)
            && profile.AppointmentTypeId is null)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        if (requiredAppointmentTypeId is not null
            && scopedCapability
            && profile.AppointmentTypeId != requiredAppointmentTypeId)
        {
            return Result<StaffAccessContext>.Failure(Denied);
        }

        return Result<StaffAccessContext>.Success(new StaffAccessContext(
            profile.StaffUserId,
            profile.Roles,
            profile.AppointmentTypeId));
    }

    private static bool IsAllowed(StaffAccessProfile profile, StaffCapability capability) =>
        IsAllowed(profile.IsAdmin, profile.IsCoordinator, profile.IsManager, profile.IsAppointmentStaff, capability);

    private static bool IsAllowed(bool isAdmin, bool isCoordinator, bool isManager, bool isAppointmentStaff, StaffCapability capability)
    {
        // Explicit attendee-data deny for Admin is retained even though valid profiles make Admin
        // exclusive. It fails closed if invalid data reaches this method in a future refactor.
        if (isAdmin && capability is
            StaffCapability.ManageAttendees or
            StaffCapability.ViewAttendeeDashboards or
            StaffCapability.ViewAttendeeAudit)
        {
            return false;
        }

        return capability switch
        {
            StaffCapability.ManageSettings => isAdmin,
            StaffCapability.ManageStaffAccess => isAdmin,
            StaffCapability.ImportEvents => isAdmin || isCoordinator,
            StaffCapability.ManageAttendees => isCoordinator,
            StaffCapability.ViewAttendeeDashboards => isCoordinator,
            StaffCapability.ViewAttendeeAudit => isCoordinator,
            StaffCapability.ViewEventAudit => isAdmin || isCoordinator,
            StaffCapability.ManageEventNegotiation => isManager,
            StaffCapability.ViewEventOperations =>
                isAdmin || isCoordinator || isManager || isAppointmentStaff,
            StaffCapability.CancelEvent =>
                isAdmin || isCoordinator || isManager,
            StaffCapability.ConductAppointments => isManager || isAppointmentStaff,
            _ => false,
        };
    }
}
`````

## before — src/EventBooking.Application/Access/StaffCapability.cs — 1/1

<!-- vocabulary-file: {"id":43,"oldPath":"src/EventBooking.Application/Access/StaffCapability.cs","newPath":"src/EventBooking.Application/Access/StaffCapability.cs","beforeSha":"83bcb416738f92d06e9666399db50854e75ab0f187f700e5d554dedf9cf74afc","afterSha":"7f1ee3d8f41a7d559c7e708277c7619cd998329aba3eaa90186b600d130973e7","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Access;

/// <summary>Defines staff capability for the current use case.</summary>
public enum StaffCapability
{
    /// <summary>Defines contract for the current use case.</summary>
    ManageSettings,
    /// <summary>Defines contract for the current use case.</summary>
    ManageStaffAccess,
    /// <summary>Defines contract for the current use case.</summary>
    ImportConfirmedSlots,
    /// <summary>Defines contract for the current use case.</summary>
    ManageCandidates,
    /// <summary>Defines contract for the current use case.</summary>
    ViewCandidateDashboards,
    /// <summary>Defines contract for the current use case.</summary>
    ViewCandidateAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ViewSlotAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ManageSlotNegotiation,
    /// <summary>Defines contract for the current use case.</summary>
    ViewSlotOperations,
    /// <summary>Defines contract for the current use case.</summary>
    CancelConfirmedSlot,
    /// <summary>Defines contract for the current use case.</summary>
    ConductAppointments,
}
`````

## after — src/EventBooking.Application/Access/StaffCapability.cs — 1/1

<!-- vocabulary-file: {"id":43,"oldPath":"src/EventBooking.Application/Access/StaffCapability.cs","newPath":"src/EventBooking.Application/Access/StaffCapability.cs","beforeSha":"83bcb416738f92d06e9666399db50854e75ab0f187f700e5d554dedf9cf74afc","afterSha":"7f1ee3d8f41a7d559c7e708277c7619cd998329aba3eaa90186b600d130973e7","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Access;

/// <summary>Defines staff capability for the current use case.</summary>
public enum StaffCapability
{
    /// <summary>Defines contract for the current use case.</summary>
    ManageSettings,
    /// <summary>Defines contract for the current use case.</summary>
    ManageStaffAccess,
    /// <summary>Defines contract for the current use case.</summary>
    ImportEvents,
    /// <summary>Defines contract for the current use case.</summary>
    ManageAttendees,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeDashboards,
    /// <summary>Defines contract for the current use case.</summary>
    ViewAttendeeAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventAudit,
    /// <summary>Defines contract for the current use case.</summary>
    ManageEventNegotiation,
    /// <summary>Defines contract for the current use case.</summary>
    ViewEventOperations,
    /// <summary>Defines contract for the current use case.</summary>
    CancelEvent,
    /// <summary>Defines contract for the current use case.</summary>
    ConductAppointments,
}
`````

## before — src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs — 1/1

<!-- vocabulary-file: {"id":44,"oldPath":"src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs","newPath":"src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs","beforeSha":"bec9eac94c688c58841eca4c0c8c5648da98c4585a7f1eec5bca6176522933cf","afterSha":"29a2571ef1b0c9424c42af2bfae53235e2bbd9c65217ec0db033a11c167ac8c6","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Access;

/// <summary>Mirrors a staff identity's identity-provider-asserted roles onto its
/// <c>StaffAccessProfile</c>, deriving dependent scope changes and never overwriting a previously-valid
/// profile with an invalid candidate shape.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="logger">The logger.</param>
public sealed class SyncStaffAccessProfileRolesHandler(
    IStaffAccessProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ILogger<SyncStaffAccessProfileRolesHandler> logger)
{
    /// <summary>Applies one sync pass and returns the resulting profile, or null when the identity
    /// has (or ends up with) no profile at all.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="claimedRoles">The claimed roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<StaffAccessProfile?> SyncAsync(
        Guid staffUserId,
        IReadOnlySet<Role> claimedRoles,
        CancellationToken cancellationToken)
    {
        // Cheap unlocked read first: the overwhelming majority of calls (every authenticated
        // page/session bootstrap) find nothing has changed since the last sync, and should not
        // pay for a transaction plus a table-wide advisory lock to discover that.
        var existing = await profiles.GetAsync(staffUserId, cancellationToken);
        if (existing is not null && existing.Roles.ToHashSet().SetEquals(claimedRoles))
        {
            return existing;
        }

        if (existing is null && claimedRoles.Count == 0)
        {
            return null;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = await profiles.LockAllAsync(cancellationToken);
        var current = locked.SingleOrDefault(value => value.StaffUserId == staffUserId);

        if (current is not null && current.Roles.ToHashSet().SetEquals(claimedRoles))
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }

        if (current is null && claimedRoles.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current is not null
            && current.IsAdmin
            && !claimedRoles.Contains(Role.Admin)
            && locked.Count(profile => profile.IsAdmin) == 1)
        {
            logger.LogWarning(
                "Refused to sync {StaffUserId} off the Admin role: they are the last remaining Admin.",
                staffUserId);
            await transaction.RollbackAsync(cancellationToken);
            return current;
        }

        var previous = current is null ? null : AuditStateOf(current);
        if (claimedRoles.Count == 0)
        {
            profiles.Remove(current!);
            Record(staffUserId, previous, new AuditState([], null));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var desiredScope = StaffAccessProfile.NeedsScope(claimedRoles)
            ? current?.AppointmentTypeId
            : null;

        try
        {
            if (current is null)
            {
                current = StaffAccessProfile.Create(staffUserId, claimedRoles, desiredScope);
                profiles.Add(current);
            }
            else
            {
                current.Replace(claimedRoles, desiredScope);
            }
        }
        catch (DomainException)
        {
            logger.LogWarning(
                "Rejected role sync for {StaffUserId}: claimed roles {ClaimedRoles} are not a valid combination.",
                staffUserId,
                string.Join(",", claimedRoles.OrderBy(role => role)));
            await transaction.RollbackAsync(cancellationToken);
            return current;
        }

        Record(staffUserId, previous, AuditStateOf(current));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return current;
    }

    private void Record(Guid staffUserId, AuditState? previous, AuditState? current) =>
        audit.Record(
            AuditEntityTypes.StaffAccessProfile,
            staffUserId,
            AuditAction.StaffRolesSynced,
            ActorType.System,
            actorId: null,
            JsonSerializer.Serialize(new { previous, current }));

    private static AuditState AuditStateOf(StaffAccessProfile profile) => new(
        profile.Roles.OrderBy(role => role).Select(role => role.ToString()).ToList(),
        profile.AppointmentTypeId);

    private sealed record AuditState(IReadOnlyList<string> Roles, Guid? AppointmentTypeId);
}
`````

## after — src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs — 1/1

<!-- vocabulary-file: {"id":44,"oldPath":"src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs","newPath":"src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs","beforeSha":"bec9eac94c688c58841eca4c0c8c5648da98c4585a7f1eec5bca6176522933cf","afterSha":"29a2571ef1b0c9424c42af2bfae53235e2bbd9c65217ec0db033a11c167ac8c6","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Access;

/// <summary>Mirrors a staff identity's identity-provider-asserted roles onto its
/// <c>StaffAccessProfile</c>, deriving dependent scope changes and never overwriting a previously-valid
/// profile with an invalid attendee shape.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="logger">The logger.</param>
public sealed class SyncStaffAccessProfileRolesHandler(
    IStaffAccessProfileRepository profiles,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ILogger<SyncStaffAccessProfileRolesHandler> logger)
{
    /// <summary>Applies one sync pass and returns the resulting profile, or null when the identity
    /// has (or ends up with) no profile at all.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="claimedRoles">The claimed roles.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<StaffAccessProfile?> SyncAsync(
        Guid staffUserId,
        IReadOnlySet<Role> claimedRoles,
        CancellationToken cancellationToken)
    {
        // Cheap unlocked read first: the overwhelming majority of calls (every authenticated
        // page/session bootstrap) find nothing has changed since the last sync, and should not
        // pay for a transaction plus a table-wide advisory lock to discover that.
        var existing = await profiles.GetAsync(staffUserId, cancellationToken);
        if (existing is not null && existing.Roles.ToHashSet().SetEquals(claimedRoles))
        {
            return existing;
        }

        if (existing is null && claimedRoles.Count == 0)
        {
            return null;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = await profiles.LockAllAsync(cancellationToken);
        var current = locked.SingleOrDefault(value => value.StaffUserId == staffUserId);

        if (current is not null && current.Roles.ToHashSet().SetEquals(claimedRoles))
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }

        if (current is null && claimedRoles.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (current is not null
            && current.IsAdmin
            && !claimedRoles.Contains(Role.Admin)
            && locked.Count(profile => profile.IsAdmin) == 1)
        {
            logger.LogWarning(
                "Refused to sync {StaffUserId} off the Admin role: they are the last remaining Admin.",
                staffUserId);
            await transaction.RollbackAsync(cancellationToken);
            return current;
        }

        var previous = current is null ? null : AuditStateOf(current);
        if (claimedRoles.Count == 0)
        {
            profiles.Remove(current!);
            Record(staffUserId, previous, new AuditState([], null));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var desiredScope = StaffAccessProfile.NeedsScope(claimedRoles)
            ? current?.AppointmentTypeId
            : null;

        try
        {
            if (current is null)
            {
                current = StaffAccessProfile.Create(staffUserId, claimedRoles, desiredScope);
                profiles.Add(current);
            }
            else
            {
                current.Replace(claimedRoles, desiredScope);
            }
        }
        catch (DomainException)
        {
            logger.LogWarning(
                "Rejected role sync for {StaffUserId}: claimed roles {ClaimedRoles} are not a valid combination.",
                staffUserId,
                string.Join(",", claimedRoles.OrderBy(role => role)));
            await transaction.RollbackAsync(cancellationToken);
            return current;
        }

        Record(staffUserId, previous, AuditStateOf(current));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return current;
    }

    private void Record(Guid staffUserId, AuditState? previous, AuditState? current) =>
        audit.Record(
            AuditEntityTypes.StaffAccessProfile,
            staffUserId,
            AuditAction.StaffRolesSynced,
            ActorType.System,
            actorId: null,
            JsonSerializer.Serialize(new { previous, current }));

    private static AuditState AuditStateOf(StaffAccessProfile profile) => new(
        profile.Roles.OrderBy(role => role).Select(role => role.ToString()).ToList(),
        profile.AppointmentTypeId);

    private sealed record AuditState(IReadOnlyList<string> Roles, Guid? AppointmentTypeId);
}
`````

## before — src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs — 1/1

<!-- vocabulary-file: {"id":45,"oldPath":"src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs","newPath":"src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs","beforeSha":"237af0625f81bae5c6988985f5c0dc6405a3dba823d7ca75081610fc5fed305e","afterSha":"8de0b0c03edfdcc274330ebe3481f9f8e0944e3b708cf69f63e96b474e230337","side":"before","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Appointments;

/// <summary>The formatted roster: CSV body text and its filesystem-safe download filename.</summary>
public sealed record RosterCsvResult
{
    /// <summary>Gets the full CSV body including the header row and a trailing line break.</summary>
    public required string CsvText { get; init; }

    /// <summary>Gets the download filename shaped roster-slug-yyyy-MM-dd-HHmm.csv.</summary>
    public required string FileName { get; init; }
}

/// <summary>
/// Projects a scoped appointment-workspace slot detail into roster CSV text plus a filename. It
/// consumes only the slot detail the workspace handler already returns, so it cannot expose a
/// field the JSON slot-detail route does not already expose.
/// </summary>
/// <param name="clock">The clock.</param>
public sealed class AppointmentRosterCsvFormatter(IClock clock)
{
    private static readonly string[] HeaderFields =
    [
        "Candidate Name",
        "Candidate Email",
        "Appointment Type",
        "Status",
        "Checked In At",
        "Outcome At",
    ];

    /// <summary>Formats the given slot detail as CSV text with its download filename.</summary>
    /// <param name="detail">The scoped slot detail already returned by the workspace handler.</param>
    /// <returns>The CSV body text and the filesystem-safe download filename.</returns>
    public RosterCsvResult Format(AppointmentSlotDetail detail)
    {
        // A literal line feed rather than AppendLine: the body must not vary with the host's
        // newline convention, and both CSV parsers in this codebase normalise either ending.
        var builder = new StringBuilder();
        builder.Append(string.Join(',', HeaderFields)).Append('\n');

        // The rows are emitted in the order the workspace returned them; never re-sorted or filtered.
        foreach (var row in detail.Appointments)
        {
            builder
                .Append(string.Join(
                    ',',
                    Escape(row.CandidateName),
                    Escape(row.CandidateEmail),
                    Escape(detail.AppointmentTypeName),
                    Escape(row.Status.ToString()),
                    Escape(FormatInstant(row.CheckedInAt)),
                    Escape(FormatInstant(row.OutcomeAt))))
                .Append('\n');
        }

        return new RosterCsvResult
        {
            CsvText = builder.ToString(),
            FileName = BuildFileName(detail),
        };
    }

    /// <summary>Formats a nullable instant as head-office ISO 8601, or empty when null.</summary>
    private string FormatInstant(DateTimeOffset? instant) =>
        instant is null
            ? string.Empty
            : clock.InstantAtHeadOffice(instant.Value).ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Builds the download filename from the slot's type slug, date, and start time.</summary>
    private static string BuildFileName(AppointmentSlotDetail detail)
    {
        // Spaces to hyphens and lower-cased, nothing else altered or removed.
        var slug = detail.AppointmentTypeName.Replace(' ', '-').ToLowerInvariant();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"roster-{slug}-{detail.Date:yyyy-MM-dd}-{detail.StartTime:HHmm}.csv");
    }

    /// <summary>Escapes one CSV field per RFC 4180 and neutralises spreadsheet formula injection.</summary>
    private static string Escape(string value)
    {
        // OWASP: prefix cells starting with a formula trigger so Excel/Sheets treat them as text.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
`````

## after — src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs — 1/1

<!-- vocabulary-file: {"id":45,"oldPath":"src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs","newPath":"src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs","beforeSha":"237af0625f81bae5c6988985f5c0dc6405a3dba823d7ca75081610fc5fed305e","afterSha":"8de0b0c03edfdcc274330ebe3481f9f8e0944e3b708cf69f63e96b474e230337","side":"after","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Application.Appointments;

/// <summary>The formatted roster: CSV body text and its filesystem-safe download filename.</summary>
public sealed record RosterCsvResult
{
    /// <summary>Gets the full CSV body including the header row and a trailing line break.</summary>
    public required string CsvText { get; init; }

    /// <summary>Gets the download filename shaped roster-slug-yyyy-MM-dd-HHmm.csv.</summary>
    public required string FileName { get; init; }
}

/// <summary>
/// Projects a scoped appointment-workspace event detail into roster CSV text plus a filename. It
/// consumes only the event detail the workspace handler already returns, so it cannot expose a
/// field the JSON event-detail route does not already expose.
/// </summary>
/// <param name="clock">The clock.</param>
public sealed class AppointmentRosterCsvFormatter(IClock clock)
{
    private static readonly string[] HeaderFields =
    [
        "Attendee Name",
        "Attendee Email",
        "Appointment Type",
        "Status",
        "Checked In At",
        "Outcome At",
    ];

    /// <summary>Formats the given event detail as CSV text with its download filename.</summary>
    /// <param name="detail">The scoped event detail already returned by the workspace handler.</param>
    /// <returns>The CSV body text and the filesystem-safe download filename.</returns>
    public RosterCsvResult Format(AppointmentEventDetail detail)
    {
        // A literal line feed rather than AppendLine: the body must not vary with the host's
        // newline convention, and both CSV parsers in this codebase normalise either ending.
        var builder = new StringBuilder();
        builder.Append(string.Join(',', HeaderFields)).Append('\n');

        // The rows are emitted in the order the workspace returned them; never re-sorted or filtered.
        foreach (var row in detail.Appointments)
        {
            builder
                .Append(string.Join(
                    ',',
                    Escape(row.AttendeeName),
                    Escape(row.AttendeeEmail),
                    Escape(detail.AppointmentTypeName),
                    Escape(row.Status.ToString()),
                    Escape(FormatInstant(row.CheckedInAt)),
                    Escape(FormatInstant(row.OutcomeAt))))
                .Append('\n');
        }

        return new RosterCsvResult
        {
            CsvText = builder.ToString(),
            FileName = BuildFileName(detail),
        };
    }

    /// <summary>Formats a nullable instant as transitional-location ISO 8601, or empty when null.</summary>
    private string FormatInstant(DateTimeOffset? instant) =>
        instant is null
            ? string.Empty
            : clock.InstantAtTransitionalLocation(instant.Value).ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Builds the download filename from the event's type slug, date, and start time.</summary>
    private static string BuildFileName(AppointmentEventDetail detail)
    {
        // Spaces to hyphens and lower-cased, nothing else altered or removed.
        var slug = detail.AppointmentTypeName.Replace(' ', '-').ToLowerInvariant();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"roster-{slug}-{detail.Date:yyyy-MM-dd}-{detail.StartTime:HHmm}.csv");
    }

    /// <summary>Escapes one CSV field per RFC 4180 and neutralises spreadsheet formula injection.</summary>
    private static string Escape(string value)
    {
        // OWASP: prefix cells starting with a formula trigger so Excel/Sheets treat them as text.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
`````

## before — src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs — 1/1

<!-- vocabulary-file: {"id":46,"oldPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs","newPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs","beforeSha":"b2f664729d74090a6539f7d719807a1d68b36f04f52536780b1a14517c69035f","afterSha":"74a8ee1874ee8ad2baf5de57630b6062780bf4a32da2ce7ec9ecb94f5eaee5b1","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Appointments;

/// <summary>The recent-past allowance applied to appointment workspace reads.</summary>
public static class AppointmentWorkspaceAllowance
{
    /// <summary>Number of calendar days of recently past slots the workspace retains.</summary>
    public const int RecentPastDays = 7;
}
`````

## after — src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs — 1/1

<!-- vocabulary-file: {"id":46,"oldPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs","newPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs","beforeSha":"b2f664729d74090a6539f7d719807a1d68b36f04f52536780b1a14517c69035f","afterSha":"74a8ee1874ee8ad2baf5de57630b6062780bf4a32da2ce7ec9ecb94f5eaee5b1","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Appointments;

/// <summary>The recent-past allowance applied to appointment workspace reads.</summary>
public static class AppointmentWorkspaceAllowance
{
    /// <summary>Number of calendar days of recently past events the workspace retains.</summary>
    public const int RecentPastDays = 7;
}
`````

## before — src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs — 1/1

<!-- vocabulary-file: {"id":47,"oldPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs","newPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs","beforeSha":"bb08484e8ccda495febc07e44fc568f5c4fcbbb868c3722f5ad5bdafd480effc","afterSha":"0fc6ddfaf21206ac3f2957085e3f9d3e2bf030a5c8454f61a0bd758773bfc4a5","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Appointments;

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCounts
{
    /// <summary>Gets candidates booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }
    /// <summary>Gets candidates checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }
    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }
    /// <summary>Gets candidates recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }
}

/// <summary>Describes one selectable active slot without candidate rows.</summary>
public sealed record AppointmentSlotSummary
{
    /// <summary>Gets the confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }
    /// <summary>Gets the slot's head-office calendar date.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }
    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }
    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCounts Counts { get; init; }
}

/// <summary>Returns the trusted appointment-type name and its selectable active slots.</summary>
public sealed record AppointmentWorkspaceSlotList
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }
    /// <summary>Gets current and upcoming active slots containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentSlotSummary> Slots { get; init; }
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRow
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }
    /// <summary>Gets the candidate name used for primary human identification.</summary>
    public required string CandidateName { get; init; }
    /// <summary>Gets the candidate email used for secondary human identification.</summary>
    public required string CandidateEmail { get; init; }
    /// <summary>Gets this appointment's independent operational status.</summary>
    public required BookingAppointmentStatus Status { get; init; }
    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public required DateTimeOffset? CheckedInAt { get; init; }
    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public required DateTimeOffset? OutcomeAt { get; init; }
    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }
}

/// <summary>Returns one scoped active slot and only its minimum-data operational rows.</summary>
public sealed record AppointmentSlotDetail
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }
    /// <summary>Gets the selected confirmed slot identifier.</summary>
    public required Guid ConfirmedSlotId { get; init; }
    /// <summary>Gets the slot's head-office calendar date.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }
    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }
    /// <summary>Gets scoped active-booking appointment rows ordered for staff identification.</summary>
    public required IReadOnlyList<BookingAppointmentRow> Appointments { get; init; }
}
`````

## after — src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs — 1/1

<!-- vocabulary-file: {"id":47,"oldPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs","newPath":"src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs","beforeSha":"bb08484e8ccda495febc07e44fc568f5c4fcbbb868c3722f5ad5bdafd480effc","afterSha":"0fc6ddfaf21206ac3f2957085e3f9d3e2bf030a5c8454f61a0bd758773bfc4a5","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Appointments;

/// <summary>Counts scoped booking appointments in each operational state.</summary>
public sealed record AppointmentStatusCounts
{
    /// <summary>Gets attendees booked but not checked in for this appointment.</summary>
    public required int Expected { get; init; }
    /// <summary>Gets attendees checked in for this appointment.</summary>
    public required int CheckedIn { get; init; }
    /// <summary>Gets required appointments completed after check-in.</summary>
    public required int Completed { get; init; }
    /// <summary>Gets attendees recorded as not attending this required appointment.</summary>
    public required int NoShow { get; init; }
}

/// <summary>Describes one selectable active event without attendee rows.</summary>
public sealed record AppointmentEventSummary
{
    /// <summary>Gets the event identifier.</summary>
    public required Guid EventId { get; init; }
    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }
    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }
    /// <summary>Gets scoped counts grouped by independent operational state.</summary>
    public required AppointmentStatusCounts Counts { get; init; }
}

/// <summary>Returns the trusted appointment-type name and its selectable active events.</summary>
public sealed record AppointmentWorkspaceEventList
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }
    /// <summary>Gets current and upcoming active events containing scoped active bookings.</summary>
    public required IReadOnlyList<AppointmentEventSummary> Events { get; init; }
}

/// <summary>Contains only the fields needed to identify and conduct one booked appointment.</summary>
public sealed record BookingAppointmentRow
{
    /// <summary>Gets the stable booking-appointment command identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }
    /// <summary>Gets the attendee name used for primary human identification.</summary>
    public required string AttendeeName { get; init; }
    /// <summary>Gets the attendee email used for secondary human identification.</summary>
    public required string AttendeeEmail { get; init; }
    /// <summary>Gets this appointment's independent operational status.</summary>
    public required BookingAppointmentStatus Status { get; init; }
    /// <summary>Gets when staff checked the attendee in, or null until check-in.</summary>
    public required DateTimeOffset? CheckedInAt { get; init; }
    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public required DateTimeOffset? OutcomeAt { get; init; }
    /// <summary>Gets the positive concurrency version required by a status command.</summary>
    public required long Version { get; init; }
}

/// <summary>Returns one scoped active event and only its minimum-data operational rows.</summary>
public sealed record AppointmentEventDetail
{
    /// <summary>Gets the fixed appointment-type name for the caller's trusted scope.</summary>
    public required string AppointmentTypeName { get; init; }
    /// <summary>Gets the selected event identifier.</summary>
    public required Guid EventId { get; init; }
    /// <summary>Gets the event's transitional-location calendar date.</summary>
    public required DateOnly Date { get; init; }
    /// <summary>Gets the start of the shared four-hour window.</summary>
    public required TimeOnly StartTime { get; init; }
    /// <summary>Gets the derived end of the shared four-hour window.</summary>
    public required TimeOnly EndTime { get; init; }
    /// <summary>Gets scoped active-booking appointment rows ordered for staff identification.</summary>
    public required IReadOnlyList<BookingAppointmentRow> Appointments { get; init; }
}
`````

## before — src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs — 1/1

<!-- vocabulary-file: {"id":48,"oldPath":"src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs","newPath":"src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs","beforeSha":"1b745891822541c4bec09439280b0a9674171853cfe9f0a4bd563163d3292cfb","afterSha":"d0a11033147c02e75389d985e5746f0f3dab722b1d6eba61e3916fb29e8d3adb","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Appointments;

/// <summary>Authorizes and serves the scoped minimum-data appointment workspace.</summary>
/// <param name="access">The access.</param>
/// <param name="queries">The queries.</param>
/// <param name="clock">The clock.</param>
public sealed class GetAppointmentWorkspaceHandler(
    IStaffAccessAuthorizer access,
    IAppointmentWorkspaceQueries queries,
    IClock clock)
{
    private static readonly Error MissingSlot =
        Error.NotFound("No such appointment workspace slot.");

    /// <summary>Lists recent-past, current, and upcoming slots for the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentWorkspaceSlotList>> ListSlotsAsync(
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<AppointmentWorkspaceSlotList>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentWorkspaceSlotList>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        return Result<AppointmentWorkspaceSlotList>.Success(
            await queries.ListSlotsAsync(
                appointmentTypeId, clock.TodayAtHeadOffice, cancellationToken));
    }

    /// <summary>Gets one active slot inside the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="confirmedSlotId">The confirmed slot id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentSlotDetail>> GetSlotAsync(
        Guid staffUserId,
        Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<AppointmentSlotDetail>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentSlotDetail>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        var detail = await queries.GetSlotAsync(
            appointmentTypeId, confirmedSlotId, cancellationToken);
        return detail is null
            ? Result<AppointmentSlotDetail>.Failure(MissingSlot)
            : Result<AppointmentSlotDetail>.Success(detail);
    }
}
`````

## after — src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs — 1/1

<!-- vocabulary-file: {"id":48,"oldPath":"src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs","newPath":"src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs","beforeSha":"1b745891822541c4bec09439280b0a9674171853cfe9f0a4bd563163d3292cfb","afterSha":"d0a11033147c02e75389d985e5746f0f3dab722b1d6eba61e3916fb29e8d3adb","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;

namespace EventBooking.Application.Appointments;

/// <summary>Authorizes and serves the scoped minimum-data appointment workspace.</summary>
/// <param name="access">The access.</param>
/// <param name="queries">The queries.</param>
/// <param name="clock">The clock.</param>
public sealed class GetAppointmentWorkspaceHandler(
    IStaffAccessAuthorizer access,
    IAppointmentWorkspaceQueries queries,
    IClock clock)
{
    private static readonly Error MissingEvent =
        Error.NotFound("No such appointment workspace eventItem.");

    /// <summary>Lists recent-past, current, and upcoming events for the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentWorkspaceEventList>> ListEventsAsync(
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<AppointmentWorkspaceEventList>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentWorkspaceEventList>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        return Result<AppointmentWorkspaceEventList>.Success(
            await queries.ListEventsAsync(
                appointmentTypeId, clock.TodayAtTransitionalLocation, cancellationToken));
    }

    /// <summary>Gets one active event inside the caller's trusted appointment type.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<AppointmentEventDetail>> GetEventAsync(
        Guid staffUserId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<AppointmentEventDetail>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<AppointmentEventDetail>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        var detail = await queries.GetEventAsync(
            appointmentTypeId, eventId, cancellationToken);
        return detail is null
            ? Result<AppointmentEventDetail>.Failure(MissingEvent)
            : Result<AppointmentEventDetail>.Success(detail);
    }
}
`````

## before — src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs — 1/1

<!-- vocabulary-file: {"id":49,"oldPath":"src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs","newPath":"src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs","beforeSha":"2dabc48919b5f8ca9b834e29818ebbd7f343f7282dd0ea9e4e132366f8f7700f","afterSha":"fcc3f522a5eedf80ef1f31bf00938b11316db90cd263b73d6f47f6a7e7254dea","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Appointments;

/// <summary>Requests one scoped appointment transition using the caller's expected version.</summary>
public sealed record UpdateBookingAppointmentStatusCommand
{
    /// <summary>Gets the authenticated staff identity requesting the transition.</summary>
    public required Guid StaffUserId { get; init; }
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }
    /// <summary>Gets the requested independent operational status.</summary>
    public required BookingAppointmentStatus Status { get; init; }
    /// <summary>Gets the positive version last observed by the caller.</summary>
    public required long ExpectedVersion { get; init; }
}

/// <summary>Returns only the changed appointment row state needed by the workspace.</summary>
public sealed record BookingAppointmentUpdateView
{
    /// <summary>Gets the stable booking-appointment identifier.</summary>
    public required Guid BookingAppointmentId { get; init; }
    /// <summary>Gets this appointment's current independent operational status.</summary>
    public required BookingAppointmentStatus Status { get; init; }
    /// <summary>Gets when staff checked the candidate in, or null until check-in.</summary>
    public required DateTimeOffset? CheckedInAt { get; init; }
    /// <summary>Gets when staff recorded completion or no-show, or null before an outcome.</summary>
    public required DateTimeOffset? OutcomeAt { get; init; }
    /// <summary>Gets the current positive concurrency version.</summary>
    public required long Version { get; init; }
}

/// <summary>Authorizes, locks, validates, applies, and audits one appointment transition.</summary>
/// <param name="access">The access.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="invites">The invites.</param>
/// <param name="slots">The slots.</param>
/// <param name="outcomes">The outcomes.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class UpdateBookingAppointmentStatusHandler(
    IStaffAccessAuthorizer access,
    IBookingAppointmentRepository appointments,
    IBookingRepository bookings,
    ICandidateRepository candidates,
    IInviteRepository invites,
    IConfirmedSlotRepository slots,
    RecoveryBookingOutcomeCoordinator outcomes,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private static readonly Error MissingAppointment =
        Error.NotFound("No such booking appointment.");

    /// <summary>Applies one scoped status transition with idempotency and version protection.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<BookingAppointmentUpdateView>> HandleAsync(
        UpdateBookingAppointmentStatusCommand command,
        CancellationToken cancellationToken)
    {
        var authorization = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ConductAppointments,
            requiredAppointmentTypeId: null,
            cancellationToken);
        if (authorization.IsFailure)
        {
            return Result<BookingAppointmentUpdateView>.Failure(authorization.Error);
        }

        if (authorization.Value.AppointmentTypeId is not Guid appointmentTypeId)
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Forbidden("This staff profile cannot perform this operation."));
        }

        if (command.ExpectedVersion <= 0 || !Enum.IsDefined(command.Status))
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Validation("A recognised status and positive expectedVersion are required."));
        }

        var locator = await appointments.FindLocatorInScopeAsync(
            command.BookingAppointmentId, appointmentTypeId, cancellationToken);
        if (locator is null)
        {
            return Result<BookingAppointmentUpdateView>.Failure(MissingAppointment);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Candidate-first lifecycle order: Candidate, ordered pending Invites, original
        // Booking, addressed Booking, Confirmed Slot, then ordered Booking Appointments.
        // The locator above only established this order; every relationship is re-read here.
        var candidate = await candidates.LockForUpdateAsync(locator.CandidateId, cancellationToken);
        var pending = await invites.LockPendingListForCandidateAsync(locator.CandidateId, cancellationToken);
        var original = await bookings.LockForUpdateAsync(locator.OriginalBookingId, cancellationToken);
        var booking = await bookings.LockForUpdateAsync(locator.BookingId, cancellationToken);
        var slot = await slots.LockForUpdateAsync(locator.ConfirmedSlotId, cancellationToken);
        var lockedAppointments = await appointments.LockForBookingAsync(
            locator.BookingId, cancellationToken);
        var appointment = lockedAppointments.SingleOrDefault(value =>
            value.Id == command.BookingAppointmentId && value.AppointmentTypeId == appointmentTypeId);

        if (candidate is null || original is null || booking is null || appointment is null || slot is null)
        {
            return Result<BookingAppointmentUpdateView>.Failure(MissingAppointment);
        }

        if (booking.CandidateId != candidate.Id
            || original.CandidateId != candidate.Id
            || appointment.BookingId != booking.Id
            || booking.ConfirmedSlotId != slot.Id
            || (booking.IsOriginal ? booking.Id != original.Id : booking.RecoveryOfBookingId != original.Id))
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Conflict("The booking appointment no longer matches its active requirement."));
        }

        if (booking.Status == BookingStatus.Cancelled || slot.Status != ConfirmedSlotStatus.Active)
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Conflict("Cancelled bookings and slots cannot be updated."));
        }

        if (appointment.Status == command.Status)
        {
            return Result<BookingAppointmentUpdateView>.Success(View(appointment));
        }

        if (appointment.Version != command.ExpectedVersion)
        {
            return Result<BookingAppointmentUpdateView>.Failure(Error.AppointmentVersionConflict(
                $"This appointment is now {appointment.Status} at version {appointment.Version}. Refresh and try again."));
        }

        var laterRecoveryExists = booking is { IsOriginal: false } || command.Status is
            BookingAppointmentStatus.Expected or BookingAppointmentStatus.CheckedIn
            ? await LaterRecoveryExistsAsync(
                original, booking, pending, appointmentTypeId, cancellationToken)
            : false;

        if (appointment.Status == BookingAppointmentStatus.NoShow
            && command.Status == BookingAppointmentStatus.Expected
            && laterRecoveryExists)
        {
            return Result<BookingAppointmentUpdateView>.Failure(Error.Conflict(
                "A later recovery covers this appointment type. Cancel the recovery first, then correct the no-show."));
        }

        var localNow = clock.NowAtHeadOffice;
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        var checkInAllowed = slot.Window.Date == localDate;
        var noShowAllowed = slot.Window.Date < localDate
            || (slot.Window.Date == localDate && localTime >= slot.Window.EndTime);
        var previous = appointment.Status;

        try
        {
            appointment.TransitionTo(
                command.Status,
                command.StaffUserId,
                clock.UtcNow,
                checkInAllowed,
                noShowAllowed);
        }
        catch (DomainException exception)
        {
            return Result<BookingAppointmentUpdateView>.Failure(
                Error.Conflict(exception.Message));
        }

        var changed = false;
        if (!booking.IsOriginal)
        {
            try
            {
                changed = outcomes.Synchronize(booking, lockedAppointments, laterRecoveryExists);
            }
            catch (DomainException exception)
            {
                return Result<BookingAppointmentUpdateView>.Failure(
                    Error.Conflict(exception.Message));
            }
        }

        var reopened = changed && booking.Status == BookingStatus.Active;
        audit.Record(
            AuditEntityTypes.BookingAppointment,
            appointment.Id,
            ActionFor(previous, appointment.Status),
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"status:{previous}->{appointment.Status};appointmentTypeId:{appointmentTypeId};confirmedSlotId:{slot.Id}"
                + (reopened ? $";booking:{BookingStatus.Concluded}->{BookingStatus.Active}" : null));

        if (changed && booking.Status == BookingStatus.Concluded)
        {
            audit.Record(
                AuditEntityTypes.Booking,
                booking.Id,
                AuditAction.RecoveryBookingConcluded,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"root {booking.RecoveryOfBookingId} {slot.Window}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<BookingAppointmentUpdateView>.Success(View(appointment));
    }

    /// <summary>
    /// Reports whether a later pending recovery Invite or non-cancelled recovery Booking
    /// covers the appointment type, making a correction stale or a reopen unsafe.
    /// </summary>
    private async Task<bool> LaterRecoveryExistsAsync(
        Booking original,
        Booking addressed,
        IReadOnlyList<Invite> pending,
        Guid appointmentTypeId,
        CancellationToken cancellationToken)
    {
        if (pending.Any(invite =>
                invite.RecoveryOfBookingId.HasValue
                && invite.RequiredAppointmentTypeIds.Contains(appointmentTypeId)))
        {
            return true;
        }

        var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
        var rows = await appointments.ListForBookingsAsync(
            journey.Select(entry => entry.Id).ToList(),
            cancellationToken);
        var typesByBooking = rows
            .GroupBy(row => row.BookingId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.AppointmentTypeId).ToHashSet());

        return journey.Any(entry =>
            !entry.IsOriginal
            && entry.Id != addressed.Id
            && entry.Status != BookingStatus.Cancelled
            && entry.CreatedAt > addressed.CreatedAt
            && typesByBooking.TryGetValue(entry.Id, out var types)
            && types.Contains(appointmentTypeId));
    }

    private static AuditAction ActionFor(
        BookingAppointmentStatus previous,
        BookingAppointmentStatus current) => (previous, current) switch
    {
        (BookingAppointmentStatus.Expected, BookingAppointmentStatus.CheckedIn) =>
            AuditAction.AppointmentCheckedIn,
        (BookingAppointmentStatus.CheckedIn, BookingAppointmentStatus.Completed) =>
            AuditAction.AppointmentCompleted,
        (BookingAppointmentStatus.Expected, BookingAppointmentStatus.NoShow) =>
            AuditAction.AppointmentMarkedNoShow,
        _ => AuditAction.AppointmentStatusCorrected,
    };

    private static BookingAppointmentUpdateView View(BookingAppointment appointment) => new()
    {
        BookingAppointmentId = appointment.Id,
        Status = appointment.Status,
        CheckedInAt = appointment.CheckedInAt,
        OutcomeAt = appointment.OutcomeAt,
        Version = appointment.Version,
    };
}
`````
