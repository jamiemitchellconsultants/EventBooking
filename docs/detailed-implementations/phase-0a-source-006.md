# 00a — Port source 6 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Application/Access/StaffAccessHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Access/StaffAccessHandler.cs","encoding":"utf8","sha256":"b5eeafba038f82d84e0d3f2e69bcc7939cc76e9b2a183b6cabf9ac36986e12d5","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Access;

/// <summary>Projects an access profile with its optional enterprise staff number.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="StaffId">The staff id.</param>
/// <param name="Roles">The roles.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="AppointmentTypeName">The appointment type name.</param>
/// <param name="Version">The version.</param>
/// <param name="DisplayName">The display name.</param>
public sealed record StaffAccessProfileView(
    Guid StaffUserId,
    StaffId? StaffId,
    IReadOnlyList<Role> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName,
    long Version,
    // <summary>
    // The human-readable name mirrored from the identity provider, or null when the identity
    // carries none. Presentation data only; never authorization-relevant.
    // </summary>
    string? DisplayName = null);

/// <summary>Requests atomic replacement of one existing profile's appointment-type scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="AppointmentTypeId">The appointment type id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ReplaceStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    Guid? AppointmentTypeId,
    long ExpectedVersion);

/// <summary>Requests clearing one existing profile's Admin-owned scope.</summary>
/// <param name="ActorStaffUserId">The actor staff user id.</param>
/// <param name="TargetStaffUserId">The target staff user id.</param>
/// <param name="ExpectedVersion">The expected version.</param>
public sealed record ClearStaffAccessProfileScopeCommand(
    Guid ActorStaffUserId,
    Guid TargetStaffUserId,
    long ExpectedVersion);

/// <summary>Returns a profile mutation and any displaced manager identity.</summary>
/// <param name="Profile">The profile.</param>
/// <param name="FormerManagerStaffUserId">The former manager staff user id.</param>
public sealed record StaffAccessMutationView(
    StaffAccessProfileView Profile,
    Guid? FormerManagerStaffUserId);

/// <summary>Authorizes and applies complete staff-access administration operations.</summary>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class StaffAccessHandler(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Defines list async for the current use case.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<StaffAccessProfileView>>> ListAsync(
        Guid actorStaffUserId,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<StaffAccessProfileView>>.Failure(authorized.Error);
        }

        var current = await profiles.ListAsync(cancellationToken);
        // One listing serves both the staff number and the name; no extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);
        return Result<IReadOnlyList<StaffAccessProfileView>>.Success(
            current.Select(profile =>
            {
                var identity = identityByUserId.GetValueOrDefault(profile.StaffUserId);
                return ToView(profile, identity?.StaffId, identity?.DisplayName);
            }).ToList());
    }

    /// <summary>Resolves an observed staff number after checking administration capability.</summary>
    /// <param name="actorStaffUserId">The actor staff user id.</param>
    /// <param name="staffIdText">The staff id text.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> ResolveIdentityAsync(
        Guid actorStaffUserId,
        string staffIdText,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            actorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        if (!StaffId.TryParse(staffIdText, out var staffId))
        {
            return Result<Guid>.Failure(Error.Validation("Enter a valid staff number."));
        }

        var identity = await identities.GetByStaffIdAsync(staffId!, cancellationToken);
        return identity is null
            ? Result<Guid>.Failure(Error.NotFound(
                "No one with that staff number has signed in yet."))
            : Result<Guid>.Success(identity.StaffUserId);
    }

    /// <summary>Replaces scope without accepting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StaffAccessMutationView>> ReplaceScopeAsync(
        ReplaceStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StaffAccessMutationView>.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = (await profiles.LockAllAsync(cancellationToken)).ToList();
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result<StaffAccessMutationView>.Failure(
                Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result<StaffAccessMutationView>.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Conflict<StaffAccessMutationView>(
                "The staff profile was changed by another administrator.");
        }

        // Blocks moving an already-scoped Manager to a different type directly — matching the former
        // AbandonsManagedType guard from Issue #71, since roles no longer change here so the only way
        // this handler can abandon a managed type is by moving its Manager's scope away from it.
        if (current.IsManager
            && current.AppointmentTypeId is not null
            && command.AppointmentTypeId != current.AppointmentTypeId)
        {
            return Conflict<StaffAccessMutationView>(
                "Assign a replacement Manager for the current appointment type first.");
        }

        var previous = AuditStateOf(current);
        Guid? formerManagerId = null;
        if (current.IsManager && command.AppointmentTypeId is not null)
        {
            var former = locked.SingleOrDefault(profile =>
                profile.StaffUserId != current.StaffUserId
                && profile.IsManager
                && profile.AppointmentTypeId == command.AppointmentTypeId);

            if (former is not null)
            {
                formerManagerId = former.StaffUserId;
                var formerBefore = AuditStateOf(former);
                // Roles are identity-provider-owned and cannot be cleared here, so scope is nulled
                // unconditionally: leaving it set would let the displaced Manager keep passing
                // StaffAccessAuthorizer's IsManager + AppointmentTypeId match for this type, silently
                // un-displacing them and recreating two Managers for the same appointment type.
                former.Replace(former.Roles, null);
                Record(command.ActorStaffUserId, former.StaffUserId,
                    AuditAction.StaffAccessChanged, formerBefore, AuditStateOf(former));
            }
        }

        try
        {
            current.Replace(current.Roles, command.AppointmentTypeId);
        }
        catch (DomainException exception)
        {
            return Result<StaffAccessMutationView>.Failure(Error.Validation(exception.Message));
        }

        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<StaffAccessMutationView>.Success(
            new StaffAccessMutationView(ToView(current, null), formerManagerId));
    }

    /// <summary>Clears scope without deleting or changing identity-provider roles.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> ClearScopeAsync(
        ClearStaffAccessProfileScopeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ActorStaffUserId, StaffCapability.ManageStaffAccess, null, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var locked = await profiles.LockAllAsync(cancellationToken);
        if (!locked.Any(profile =>
                profile.StaffUserId == command.ActorStaffUserId
                && profile.IsAdmin
                && profile.IsValid()))
        {
            return Result.Failure(Error.Forbidden("Only an Admin can manage staff access."));
        }

        var current = locked.SingleOrDefault(
            profile => profile.StaffUserId == command.TargetStaffUserId);
        if (current is null)
        {
            return Result.Failure(Error.NotFound("No such staff profile."));
        }

        if (current.Version != command.ExpectedVersion)
        {
            return Result.Failure(
                Error.Conflict("The staff profile was changed by another administrator."));
        }

        if (current.AppointmentTypeId is null)
        {
            return Result.Failure(
                Error.Validation("This profile has no appointment-type scope to clear."));
        }

        if (current.IsManager)
        {
            return Result.Failure(Error.Conflict(
                "Assign a replacement Manager for the current appointment type first."));
        }

        var previous = AuditStateOf(current);
        current.Replace(current.Roles, null);
        Record(command.ActorStaffUserId, current.StaffUserId,
            AuditAction.StaffAccessChanged, previous, AuditStateOf(current));

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static Result<T> Conflict<T>(string message) =>
        Result<T>.Failure(Error.Conflict(message));

    private static StaffAccessProfileView ToView(
        StaffAccessProfile profile,
        StaffId? staffId,
        string? displayName = null) => new(
        profile.StaffUserId,
        staffId,
        profile.Roles.OrderBy(role => role).ToList(),
        profile.AppointmentTypeId,
        profile.AppointmentTypeId is null
            ? null
            : AppointmentTypeIds.NameOf(profile.AppointmentTypeId.Value),
        profile.Version,
        displayName);

    private void Record(
        Guid actorStaffUserId,
        Guid targetStaffUserId,
        AuditAction action,
        AuditState? previous,
        AuditState? current) =>
        audit.Record(
            AuditEntityTypes.StaffAccessProfile,
            targetStaffUserId,
            action,
            ActorType.Staff,
            actorStaffUserId.ToString(),
            JsonSerializer.Serialize(new { previous, current }));

    private static AuditState AuditStateOf(StaffAccessProfile profile) => new(
        profile.Roles.OrderBy(role => role).Select(role => role.ToString()).ToList(),
        profile.AppointmentTypeId,
        profile.Version);

    private sealed record AuditState(
        IReadOnlyList<string> Roles,
        Guid? AppointmentTypeId,
        long Version);
}
`````

## src/EventBooking.Application/Access/StaffCapability.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Access/StaffCapability.cs","encoding":"utf8","sha256":"83bcb416738f92d06e9666399db50854e75ab0f187f700e5d554dedf9cf74afc","parts":1,"part":1} -->

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

## src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs","encoding":"utf8","sha256":"bec9eac94c688c58841eca4c0c8c5648da98c4585a7f1eec5bca6176522933cf","parts":1,"part":1} -->

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

## src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs","encoding":"utf8","sha256":"237af0625f81bae5c6988985f5c0dc6405a3dba823d7ca75081610fc5fed305e","parts":1,"part":1} -->

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

## src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs","encoding":"utf8","sha256":"b2f664729d74090a6539f7d719807a1d68b36f04f52536780b1a14517c69035f","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Application.Appointments;

/// <summary>The recent-past allowance applied to appointment workspace reads.</summary>
public static class AppointmentWorkspaceAllowance
{
    /// <summary>Number of calendar days of recently past slots the workspace retains.</summary>
    public const int RecentPastDays = 7;
}
`````

## src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs","encoding":"utf8","sha256":"bb08484e8ccda495febc07e44fc568f5c4fcbbb868c3722f5ad5bdafd480effc","parts":1,"part":1} -->

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

## src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs","encoding":"utf8","sha256":"1b745891822541c4bec09439280b0a9674171853cfe9f0a4bd563163d3292cfb","parts":1,"part":1} -->

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

## src/EventBooking.Application/Appointments/RecoveryBookingOutcomeCoordinator.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Appointments/RecoveryBookingOutcomeCoordinator.cs","encoding":"utf8","sha256":"0d85ccf653d5feaa0af4e79bf1dcaa7e5465601ba67ccaac3dfefb568d0631ff","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Appointments;

/// <summary>Moves a recovery Booking with its own locked appointment outcomes.</summary>
public sealed class RecoveryBookingOutcomeCoordinator
{
    /// <summary>Concludes or reopens a recovery Booking from its locked appointment collection.</summary>
    /// <param name="booking">The locked recovery Booking to synchronize.</param>
    /// <param name="appointments">The Booking's locked appointments, in any order.</param>
    /// <param name="laterRecoveryExists">Whether a later recovery covers the corrected type.</param>
    /// <returns>Whether the Booking status changed.</returns>
    public bool Synchronize(
        Booking booking,
        IReadOnlyCollection<BookingAppointment> appointments,
        bool laterRecoveryExists)
    {
        if (booking.IsOriginal)
        {
            return false;
        }

        if (booking.Status == BookingStatus.Active
            && appointments.Count > 0
            && appointments.All(IsTerminal))
        {
            booking.Conclude();
            return true;
        }

        if (booking.Status == BookingStatus.Concluded
            && appointments.Any(appointment => !IsTerminal(appointment)))
        {
            if (laterRecoveryExists)
            {
                throw new DomainException(
                    "A later recovery covers this appointment type. Cancel the recovery first.");
            }

            booking.Reopen();
            return true;
        }

        return false;
    }

    private static bool IsTerminal(BookingAppointment appointment) =>
        appointment.Status is BookingAppointmentStatus.Completed or BookingAppointmentStatus.NoShow;
}
`````

## src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs","encoding":"utf8","sha256":"2dabc48919b5f8ca9b834e29818ebbd7f343f7282dd0ea9e4e132366f8f7700f","parts":1,"part":1} -->

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

## src/EventBooking.Application/Bookings/BookingCanceller.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Bookings/BookingCanceller.cs","encoding":"utf8","sha256":"5395fd970f149470bc7b00257fa24e3e7173abc56909f456e26618f2690c11f7","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>
/// Voids one booking and returns the capacity it held, under a row lock on the capacity rows.
/// Shared by candidate deletion (Task 32), candidate cancellation (Task 41) and slot cancellation
/// (Task 42) — all three release capacity in exactly the same way, and a second implementation
/// would be a second chance to get the locking wrong.
/// </summary>
/// <param name="appointments">The appointments.</param>
/// <param name="capacities">The capacities.</param>
/// <param name="audit">The audit.</param>
public sealed class BookingCanceller(
    IBookingAppointmentRepository appointments,
    ISlotCapacityRepository capacities,
    IAuditLogger audit)
{
    /// <summary>Cancels one locked Booking and returns capacity for its own Appointment snapshot.</summary>
    /// <param name="booking">The booking.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="actorType">The actor type.</param>
    /// <param name="actorId">The actor id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<Guid>>> CancelLockedAsync(
        Booking booking,
        ConfirmedSlot slot,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        var snapshot = (await appointments.ListForBookingAsync(booking.Id, cancellationToken))
            .Select(appointment => appointment.AppointmentTypeId)
            .Distinct()
            .Order()
            .ToList();

        if (snapshot.Count == 0)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The booking has no appointments to release."));
        }

        IReadOnlyList<SlotCapacity> locked;
        try
        {
            foreach (var appointmentTypeId in snapshot)
            {
                slot.CapacityFor(appointmentTypeId);
            }

            locked = (await capacities.LockForUpdateAsync(slot.Id, snapshot, cancellationToken))
                .OrderBy(capacity => capacity.AppointmentTypeId)
                .ToList();
        }
        catch (DomainException ex)
        {
            return Result<IReadOnlyList<Guid>>.Failure(Error.Conflict(ex.Message));
        }

        if (locked.Count != snapshot.Count)
        {
            return Result<IReadOnlyList<Guid>>.Failure(
                Error.Conflict("The slot is missing capacity counters for the booking."));
        }

        booking.Cancel();

        foreach (var capacity in locked)
        {
            capacity.Increment();

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.CapacityIncremented,
                actorType,
                actorId,
                $"{capacity.AppointmentTypeId} now {capacity.RemainingCapacity}");
        }

        audit.Record(
            AuditEntityTypes.Booking,
            booking.Id,
            AuditAction.BookingCancelled,
            actorType,
            actorId,
            null);

        return Result<IReadOnlyList<Guid>>.Success(snapshot);
    }
}
`````
