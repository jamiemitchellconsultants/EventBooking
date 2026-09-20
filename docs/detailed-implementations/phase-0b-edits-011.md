# 00b — Vocabulary edits 11 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Application/Bookings/ViewInviteHandler.cs — 1/1

<!-- vocabulary-file: {"id":56,"oldPath":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","newPath":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","beforeSha":"ae62716c097f82d42df5663feb34eafcd755c0e3b9a8ee49424f566118193341","afterSha":"472388a9ef5c33ff5cf9cfdf4e952ecb3b2cbd75048c540a70a775bc8d8b5fe1","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Bookings;

/// <summary>Defines invite option view for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
public sealed record InviteOptionView(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

/// <summary>Defines invite view for the current use case.</summary>
/// <param name="InviteId">The invite id.</param>
/// <param name="CandidateName">The candidate name.</param>
/// <param name="AppointmentTypeNames">The appointment type names.</param>
/// <param name="Options">The options.</param>
/// <param name="IsRecovery">The is recovery.</param>
public sealed record InviteView(
    Guid InviteId,
    string CandidateName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionView> Options,
    bool IsRecovery);

/// <summary>Defines view invite query for the current use case.</summary>
/// <param name="Token">The token.</param>
public sealed record ViewInviteQuery(string? Token);

/// <summary>Defines view invite handler for the current use case.</summary>
/// <param name="invites">The invites.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="slots">The slots.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
public sealed class ViewInviteHandler(
    IInviteRepository invites,
    ICandidateRepository candidates,
    IConfirmedSlotRepository slots,
    EligibleSlotFinder slotFinder,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    ITokenService tokens,
    IClock clock)
{
    /// <summary>
    /// One message for every failure. A caller must not be able to tell a forged token from an
    /// expired one.
    /// </summary>
    public const string InvalidLinkMessage = "This booking link is no longer valid.";

    /// <summary>
    /// Projects the usable future appointment options for the supplied candidate invite token.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteView>> HandleAsync(
        ViewInviteQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Token is null || !tokens.TryRead(query.Token, out _))
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var invite = await invites.GetByTokenHashAsync(tokens.Hash(query.Token), cancellationToken);
        if (invite is null || !invite.IsUsableAt(clock.UtcNow))
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var candidate = await candidates.GetAsync(invite.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var required = invite.RequiredAppointmentTypeIds;
        var today = clock.TodayAtHeadOffice;

        var options = new List<ConfirmedSlot>();
        var deadSlotIds = new List<Guid>();
        foreach (var slotId in invite.OfferedSlotIds)
        {
            var slot = await slots.GetAsync(slotId, cancellationToken);
            if (slot is not null
                && slot.Status == ConfirmedSlotStatus.Active
                && slot.Window.StartsAfter(today)
                && HasSpareFor(slot, required))
            {
                options.Add(slot);
            }
            else
            {
                deadSlotIds.Add(slotId);
            }
        }

        var mutated = false;
        if (deadSlotIds.Count > 0)
        {
            foreach (var deadSlotId in deadSlotIds)
            {
                invite.RemoveOption(deadSlotId);
            }

            var replacements = await slotFinder.FindAsync(
                required,
                deadSlotIds.Count,
                invite.OfferedSlotIds.Concat(deadSlotIds).ToList(),
                cancellationToken);

            foreach (var replacement in replacements)
            {
                if (replacement is null)
                {
                    continue;
                }

                invite.AddOption(replacement.Id);
                options.Add(replacement);
                mutated = true;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteOptionReplaced,
                    ActorType.CandidateToken,
                    invite.Id.ToString(),
                    $"{deadSlotIds.Count} lost option(s) replaced by {replacement.Id}");
            }

            mutated = true;
        }

        if (options.Count < Domain.Invites.Invite.RequiredOptionCount
            && candidate.Status == CandidateStatus.Invited)
        {
            candidate.MarkNoResponse();
            mutated = true;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.CandidateToken,
                invite.Id.ToString(),
                $"only {options.Count} live option(s) remain, candidate flagged for follow-up");
        }

        if (mutated)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var view = new InviteView(
            invite.Id,
            candidate.Name,
            invite.RequiredAppointmentTypeIds.Select(AppointmentTypeIds.NameOf).ToList(),
            options
                .OrderBy(s => s.Window)
                .Select(s => new InviteOptionView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    CandidateEmailComposer.FormatWindow(s.Window)))
                .ToList(),
            invite.RecoveryOfBookingId is not null);

        return Result<InviteView>.Success(view);
    }

    /// <summary>
    /// Determines whether the slot holds spare capacity for every snapshotted requirement.
    /// A missing capacity row is treated as no spare capacity rather than throwing.
    /// </summary>
    private static bool HasSpareFor(ConfirmedSlot slot, IReadOnlyList<Guid> required)
    {
        try
        {
            return slot.HasSpareCapacityForAll(required);
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
`````

## after — src/EventBooking.Application/Bookings/ViewInviteHandler.cs — 1/1

<!-- vocabulary-file: {"id":56,"oldPath":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","newPath":"src/EventBooking.Application/Bookings/ViewInviteHandler.cs","beforeSha":"ae62716c097f82d42df5663feb34eafcd755c0e3b9a8ee49424f566118193341","afterSha":"472388a9ef5c33ff5cf9cfdf4e952ecb3b2cbd75048c540a70a775bc8d8b5fe1","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Bookings;

/// <summary>Defines invite option view for the current use case.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="Display">The display.</param>
public sealed record InviteOptionView(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Display);

/// <summary>Defines invite view for the current use case.</summary>
/// <param name="InviteId">The invite id.</param>
/// <param name="AttendeeName">The attendee name.</param>
/// <param name="AppointmentTypeNames">The appointment type names.</param>
/// <param name="Options">The options.</param>
/// <param name="IsRecovery">The is recovery.</param>
public sealed record InviteView(
    Guid InviteId,
    string AttendeeName,
    IReadOnlyList<string> AppointmentTypeNames,
    IReadOnlyList<InviteOptionView> Options,
    bool IsRecovery);

/// <summary>Defines view invite query for the current use case.</summary>
/// <param name="Token">The token.</param>
public sealed record ViewInviteQuery(string? Token);

/// <summary>Defines view invite handler for the current use case.</summary>
/// <param name="invites">The invites.</param>
/// <param name="attendees">The attendees.</param>
/// <param name="events">The events.</param>
/// <param name="eventFinder">The event finder.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="clock">The clock.</param>
public sealed class ViewInviteHandler(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventRepository events,
    EligibleEventFinder eventFinder,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    ITokenService tokens,
    IClock clock)
{
    /// <summary>
    /// One message for every failure. A caller must not be able to tell a forged token from an
    /// expired one.
    /// </summary>
    public const string InvalidLinkMessage = "This booking link is no longer valid.";

    /// <summary>
    /// Projects the usable future appointment options for the supplied attendee invite token.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteView>> HandleAsync(
        ViewInviteQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Token is null || !tokens.TryRead(query.Token, out _))
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var invite = await invites.GetByTokenHashAsync(tokens.Hash(query.Token), cancellationToken);
        if (invite is null || !invite.IsUsableAt(clock.UtcNow))
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var attendee = await attendees.GetAsync(invite.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result<InviteView>.Failure(Error.NotFound(InvalidLinkMessage));
        }

        var required = invite.RequiredAppointmentTypeIds;
        var today = clock.TodayAtTransitionalLocation;

        var options = new List<Event>();
        var deadEventIds = new List<Guid>();
        foreach (var eventId in invite.OfferedEventIds)
        {
            var eventItem = await events.GetAsync(eventId, cancellationToken);
            if (eventItem is not null
                && eventItem.Status == EventStatus.Active
                && eventItem.Window.StartsAfter(today)
                && HasSpareFor(eventItem, required))
            {
                options.Add(eventItem);
            }
            else
            {
                deadEventIds.Add(eventId);
            }
        }

        var mutated = false;
        if (deadEventIds.Count > 0)
        {
            foreach (var deadEventId in deadEventIds)
            {
                invite.RemoveOption(deadEventId);
            }

            var replacements = await eventFinder.FindAsync(
                required,
                deadEventIds.Count,
                invite.OfferedEventIds.Concat(deadEventIds).ToList(),
                cancellationToken);

            foreach (var replacement in replacements)
            {
                if (replacement is null)
                {
                    continue;
                }

                invite.AddOption(replacement.Id);
                options.Add(replacement);
                mutated = true;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteOptionReplaced,
                    ActorType.AttendeeToken,
                    invite.Id.ToString(),
                    $"{deadEventIds.Count} lost option(s) replaced by {replacement.Id}");
            }

            mutated = true;
        }

        if (options.Count < Domain.Invites.Invite.RequiredOptionCount
            && attendee.Status == AttendeeStatus.Invited)
        {
            attendee.MarkNoResponse();
            mutated = true;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteOptionReplaced,
                ActorType.AttendeeToken,
                invite.Id.ToString(),
                $"only {options.Count} live option(s) remain, attendee flagged for follow-up");
        }

        if (mutated)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var view = new InviteView(
            invite.Id,
            attendee.Name,
            invite.RequiredAppointmentTypeIds.Select(AppointmentTypeIds.NameOf).ToList(),
            options
                .OrderBy(s => s.Window)
                .Select(s => new InviteOptionView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    AttendeeEmailComposer.FormatWindow(s.Window)))
                .ToList(),
            invite.RecoveryOfBookingId is not null);

        return Result<InviteView>.Success(view);
    }

    /// <summary>
    /// Determines whether the event holds spare capacity for every snapshotted requirement.
    /// A missing capacity row is treated as no spare capacity rather than throwing.
    /// </summary>
    private static bool HasSpareFor(Event eventItem, IReadOnlyList<Guid> required)
    {
        try
        {
            return eventItem.HasSpareCapacityForAll(required);
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
`````

## before — src/EventBooking.Application/Candidates/CandidateCsvParser.cs — 1/1

<!-- vocabulary-file: {"id":57,"oldPath":"src/EventBooking.Application/Candidates/CandidateCsvParser.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeCsvParser.cs","beforeSha":"60acea7f411fea84dec9ea9f56c3a5da16372cf5944bff887d5bd3aeb2957b8e","afterSha":"f1d2a20bb90a112e8f210aea6ee2d17fa170649e2b011d2ad54dc93aabac9a8c","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Candidates;

/// <summary>Defines candidate csv row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="EmployeeGroupCode">The employee group code.</param>
public sealed record CandidateCsvRow(
    int LineNumber,
    string Name,
    string Email,
    string EmployeeGroupCode);

/// <summary>Defines candidate csv error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record CandidateCsvError(int LineNumber, string Message);

/// <summary>Defines candidate csv parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record CandidateCsvParseResult(
    IReadOnlyList<CandidateCsvRow> Rows,
    IReadOnlyList<CandidateCsvError> Errors);

/// <summary>
/// Structural validation only — field count, blank fields, one canonicalized Employee Group code,
/// and duplicate emails within the file. Group existence is the import handler's rule.
/// </summary>
public static class CandidateCsvParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "name,email,employee_group";

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    public static CandidateCsvParseResult Parse(string? content)
    {
        var rows = new List<CandidateCsvRow>();
        var errors = new List<CandidateCsvError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new CandidateCsvError(0, "The file is empty."));
            return new CandidateCsvParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new CandidateCsvError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new CandidateCsvParseResult(rows, errors);
        }

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(',');
            if (fields.Length != 3)
            {
                errors.Add(new CandidateCsvError(
                    lineNumber, "Expected 3 comma-separated fields: name, email, employee_group."));
                continue;
            }

            var name = fields[0].Trim();
            var email = fields[1].Trim();
            var groupCode = fields[2].Trim();

            if (name.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Name is required."));
                continue;
            }

            if (email.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Email is required."));
                continue;
            }

            if (groupCode.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Employee group is required."));
                continue;
            }

            if (!seenEmails.Add(email))
            {
                errors.Add(new CandidateCsvError(
                    lineNumber, $"{email.ToLowerInvariant()} appears more than once in this file."));
                continue;
            }

            rows.Add(new CandidateCsvRow(
                lineNumber, name, email.ToLowerInvariant(), groupCode.ToUpperInvariant()));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new CandidateCsvError(1, "The file contains no candidate rows."));
        }

        return new CandidateCsvParseResult(rows, errors);
    }
}
`````

## after — src/EventBooking.Application/Attendees/AttendeeCsvParser.cs — 1/1

<!-- vocabulary-file: {"id":57,"oldPath":"src/EventBooking.Application/Candidates/CandidateCsvParser.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeCsvParser.cs","beforeSha":"60acea7f411fea84dec9ea9f56c3a5da16372cf5944bff887d5bd3aeb2957b8e","afterSha":"f1d2a20bb90a112e8f210aea6ee2d17fa170649e2b011d2ad54dc93aabac9a8c","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Attendees;

/// <summary>Defines attendee csv row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupCode">The attendee group code.</param>
public sealed record AttendeeCsvRow(
    int LineNumber,
    string Name,
    string Email,
    string AttendeeGroupCode);

/// <summary>Defines attendee csv error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record AttendeeCsvError(int LineNumber, string Message);

/// <summary>Defines attendee csv parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record AttendeeCsvParseResult(
    IReadOnlyList<AttendeeCsvRow> Rows,
    IReadOnlyList<AttendeeCsvError> Errors);

/// <summary>
/// Structural validation only — field count, blank fields, one canonicalized Attendee Group code,
/// and duplicate emails within the file. Group existence is the import handler's rule.
/// </summary>
public static class AttendeeCsvParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "name,email,attendee_group";

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    public static AttendeeCsvParseResult Parse(string? content)
    {
        var rows = new List<AttendeeCsvRow>();
        var errors = new List<AttendeeCsvError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new AttendeeCsvError(0, "The file is empty."));
            return new AttendeeCsvParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new AttendeeCsvError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new AttendeeCsvParseResult(rows, errors);
        }

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(',');
            if (fields.Length != 3)
            {
                errors.Add(new AttendeeCsvError(
                    lineNumber, "Expected 3 comma-separated fields: name, email, attendee_group."));
                continue;
            }

            var name = fields[0].Trim();
            var email = fields[1].Trim();
            var groupCode = fields[2].Trim();

            if (name.Length == 0)
            {
                errors.Add(new AttendeeCsvError(lineNumber, "Name is required."));
                continue;
            }

            if (email.Length == 0)
            {
                errors.Add(new AttendeeCsvError(lineNumber, "Email is required."));
                continue;
            }

            if (groupCode.Length == 0)
            {
                errors.Add(new AttendeeCsvError(lineNumber, "Employee group is required."));
                continue;
            }

            if (!seenEmails.Add(email))
            {
                errors.Add(new AttendeeCsvError(
                    lineNumber, $"{email.ToLowerInvariant()} appears more than once in this file."));
                continue;
            }

            rows.Add(new AttendeeCsvRow(
                lineNumber, name, email.ToLowerInvariant(), groupCode.ToUpperInvariant()));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new AttendeeCsvError(1, "The file contains no attendee rows."));
        }

        return new AttendeeCsvParseResult(rows, errors);
    }
}
`````

## before — src/EventBooking.Application/Candidates/CandidateReadiness.cs — 1/1

<!-- vocabulary-file: {"id":58,"oldPath":"src/EventBooking.Application/Candidates/CandidateReadiness.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeReadiness.cs","beforeSha":"927eea9ca97557c7b0e22f86d9d8c91ea49f7ab42230c06c4e2921b356ae49ea","afterSha":"3ee8c1525b87908cbbe9bb86646e4cc56f25767dc7c0f27b1c735fa4e40d7e5e","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Candidates;

/// <summary>Explains whether EventBooking has completed every current Candidate requirement.</summary>
public enum CandidateReadinessCode
{
    /// <summary>Every current requirement has a Completed non-cancelled attempt.</summary>
    Ready = 1,
    /// <summary>The Candidate has no assigned Employee Group during Release 1 reconciliation.</summary>
    EmployeeGroupUnassigned = 2,
    /// <summary>The Candidate has no Active original Booking journey.</summary>
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
public sealed record CandidateReadinessAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    EventBooking.Domain.Bookings.BookingAppointmentStatus Status,
    Guid BookingId,
    EventBooking.Domain.Bookings.BookingStatus BookingStatus,
    DateTimeOffset BookingCreatedAt);

/// <summary>The authorized persistence projection consumed by the readiness calculator.</summary>
/// <param name="CandidateId">The candidate identifier.</param>
/// <param name="EmployeeGroupId">The assigned group, or null during reconciliation.</param>
/// <param name="CurrentRequirementTypeIds">The group's current requirement set.</param>
/// <param name="ActiveOriginalBookingId">The active journey root, or null when absent.</param>
/// <param name="Attempts">Every booked attempt in the original and recovery journey.</param>
public sealed record CandidateReadinessSnapshot(
    Guid CandidateId,
    Guid? EmployeeGroupId,
    IReadOnlyList<Guid> CurrentRequirementTypeIds,
    Guid? ActiveOriginalBookingId,
    IReadOnlyList<CandidateReadinessAttempt> Attempts);

/// <summary>Minimum canonical detail for one incomplete current Appointment Type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether the latest attempt is a recoverable no-show.</param>
public sealed record OutstandingAppointmentType(
    string Code,
    string Name,
    bool IsRecoverable);

/// <summary>The internal EventBooking readiness result shown to a Coordinator.</summary>
/// <param name="CandidateId">The candidate identifier.</param>
/// <param name="Code">The machine-readable readiness reason.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete types sorted by code.</param>
public sealed record CandidateReadiness(
    Guid CandidateId,
    CandidateReadinessCode Code,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);
`````

## after — src/EventBooking.Application/Attendees/AttendeeReadiness.cs — 1/1

<!-- vocabulary-file: {"id":58,"oldPath":"src/EventBooking.Application/Candidates/CandidateReadiness.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeReadiness.cs","beforeSha":"927eea9ca97557c7b0e22f86d9d8c91ea49f7ab42230c06c4e2921b356ae49ea","afterSha":"3ee8c1525b87908cbbe9bb86646e4cc56f25767dc7c0f27b1c735fa4e40d7e5e","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs — 1/1

<!-- vocabulary-file: {"id":59,"oldPath":"src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs","beforeSha":"92f51c07edebd5430ba80bcdf5dffe831fa5714f31d334b394f4068d72bfd9be","afterSha":"55f9f7684b5e648cfdd97e85f3abf1e05dc89b236d5453b19713949a99bbc409","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Candidates;

/// <summary>Calculates deterministic readiness from one persistence snapshot.</summary>
public sealed class CandidateReadinessCalculator
{
    /// <summary>Calculates one deterministic result without inferring an Employee Group.</summary>
    /// <param name="snapshot">The authorized journey projection.</param>
    /// <returns>Ready, or the highest-precedence failure with outstanding types.</returns>
    public CandidateReadiness Calculate(CandidateReadinessSnapshot snapshot)
    {
        if (!snapshot.EmployeeGroupId.HasValue)
        {
            return new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.EmployeeGroupUnassigned, []);
        }

        if (!snapshot.ActiveOriginalBookingId.HasValue)
        {
            return new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.NoActiveBooking, []);
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
            ? new CandidateReadiness(snapshot.CandidateId, CandidateReadinessCode.Ready, [])
            : new CandidateReadiness(
                snapshot.CandidateId, CandidateReadinessCode.AppointmentsOutstanding, outstanding);
    }

    private static CandidateReadiness Mismatch(CandidateReadinessSnapshot snapshot) =>
        new(snapshot.CandidateId, CandidateReadinessCode.RequirementSnapshotMismatch, []);
}
`````

## after — src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs — 1/1

<!-- vocabulary-file: {"id":59,"oldPath":"src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs","beforeSha":"92f51c07edebd5430ba80bcdf5dffe831fa5714f31d334b394f4068d72bfd9be","afterSha":"55f9f7684b5e648cfdd97e85f3abf1e05dc89b236d5453b19713949a99bbc409","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs — 1/1

<!-- vocabulary-file: {"id":60,"oldPath":"src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs","newPath":"src/EventBooking.Application/Attendees/DeleteAttendeeHandler.cs","beforeSha":"8b7611cd3b0da50e3f2919cc4b327a46290e2e9e9a1d853b9c8040969805cc50","afterSha":"71e18a53acca11db629e55e81417984a4596c4c7e8bb7c5b8420693f9a314483","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Candidates;

/// <summary>Defines delete candidate command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CandidateId">The candidate id.</param>
/// <param name="ConfirmCascade">The confirm cascade.</param>
public sealed record DeleteCandidateCommand(Guid StaffUserId, Guid CandidateId, bool ConfirmCascade);

/// <summary>Deletes a candidate only after serializing and reconciling their current lifecycle rows.</summary>
/// <param name="candidates">The candidates.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="slots">The slots.</param>
/// <param name="access">The access.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class DeleteCandidateHandler(
    ICandidateRepository candidates,
    IInviteRepository invites,
    IBookingRepository bookings,
    IConfirmedSlotRepository slots,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Deletes the candidate and releases their active booking after confirmed cascade authorization.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        DeleteCandidateCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Candidate is the lifecycle root. Every authoritative cascade read occurs only after its
        // row lock is held, then follows Candidate -> Invite -> Booking.
        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        var invite = await invites.LockPendingForCandidateAsync(candidate.Id, cancellationToken);
        var booking = await bookings.LockActiveOriginalForCandidateAsync(candidate.Id, cancellationToken);

        // An active recovery booking holds its own slot capacity and is invisible to the
        // original-only lookup above, so it is locked and cascaded here as well.
        var activeRecovery = booking is not null && booking.IsOriginal
            ? await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken)
            : null;

        var bookingCount = (booking is null ? 0 : 1) + (activeRecovery is null ? 0 : 1);
        var inviteCount = invite is null ? 0 : 1;

        if (!command.ConfirmCascade && bookingCount + inviteCount > 0)
        {
            return Result.Failure(Error.Conflict(
                $"Deleting this candidate will cancel {bookingCount} booking and {inviteCount} pending invite, "
                + "and free the capacity they hold. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();

        try
        {
            if (activeRecovery is not null)
            {
                var recoverySlot = activeRecovery.ConfirmedSlotId == booking!.ConfirmedSlotId
                    ? await slots.LockForUpdateAsync(booking.ConfirmedSlotId, cancellationToken)
                    : await slots.LockForUpdateAsync(activeRecovery.ConfirmedSlotId, cancellationToken);
                if (recoverySlot is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such slot."));
                }

                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    activeRecovery,
                    recoverySlot,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(releasedRecovery.Error);
                }
            }

            if (booking is not null)
            {
                var slot = await slots.LockForUpdateAsync(booking.ConfirmedSlotId, cancellationToken);
                if (slot is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such slot."));
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    slot,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(released.Error);
                }
            }

            if (invite is not null)
            {
                invite.MarkSuperseded();
            }

            audit.Record(
                AuditEntityTypes.Candidate,
                candidate.Id,
                AuditAction.CandidateDeleted,
                ActorType.Staff,
                actorId,
                null);

            candidates.Remove(candidate);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
`````

## after — src/EventBooking.Application/Attendees/DeleteAttendeeHandler.cs — 1/1

<!-- vocabulary-file: {"id":60,"oldPath":"src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs","newPath":"src/EventBooking.Application/Attendees/DeleteAttendeeHandler.cs","beforeSha":"8b7611cd3b0da50e3f2919cc4b327a46290e2e9e9a1d853b9c8040969805cc50","afterSha":"71e18a53acca11db629e55e81417984a4596c4c7e8bb7c5b8420693f9a314483","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Attendees;

/// <summary>Defines delete attendee command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="AttendeeId">The attendee id.</param>
/// <param name="ConfirmCascade">The confirm cascade.</param>
public sealed record DeleteAttendeeCommand(Guid StaffUserId, Guid AttendeeId, bool ConfirmCascade);

/// <summary>Deletes a attendee only after serializing and reconciling their current lifecycle rows.</summary>
/// <param name="attendees">The attendees.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="bookingCanceller">The booking canceller.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class DeleteAttendeeHandler(
    IAttendeeRepository attendees,
    IInviteRepository invites,
    IBookingRepository bookings,
    IEventRepository events,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Deletes the attendee and releases their active booking after confirmed cascade authorization.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        DeleteAttendeeCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageAttendees,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Attendee is the lifecycle root. Every authoritative cascade read occurs only after its
        // row lock is held, then follows Attendee -> Invite -> Booking.
        var attendee = await attendees.LockForUpdateAsync(command.AttendeeId, cancellationToken);
        if (attendee is null)
        {
            return Result.Failure(Error.NotFound("No such attendee."));
        }

        var invite = await invites.LockPendingForAttendeeAsync(attendee.Id, cancellationToken);
        var booking = await bookings.LockActiveOriginalForAttendeeAsync(attendee.Id, cancellationToken);

        // An active recovery booking holds its own event capacity and is invisible to the
        // original-only lookup above, so it is locked and cascaded here as well.
        var activeRecovery = booking is not null && booking.IsOriginal
            ? await bookings.LockActiveRecoveryAsync(booking.Id, cancellationToken)
            : null;

        var bookingCount = (booking is null ? 0 : 1) + (activeRecovery is null ? 0 : 1);
        var inviteCount = invite is null ? 0 : 1;

        if (!command.ConfirmCascade && bookingCount + inviteCount > 0)
        {
            return Result.Failure(Error.Conflict(
                $"Deleting this attendee will cancel {bookingCount} booking and {inviteCount} pending invite, "
                + "and free the capacity they hold. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();

        try
        {
            if (activeRecovery is not null)
            {
                var recoveryEvent = activeRecovery.EventId == booking!.EventId
                    ? await events.LockForUpdateAsync(booking.EventId, cancellationToken)
                    : await events.LockForUpdateAsync(activeRecovery.EventId, cancellationToken);
                if (recoveryEvent is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such eventItem."));
                }

                var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                    activeRecovery,
                    recoveryEvent,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (releasedRecovery.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(releasedRecovery.Error);
                }
            }

            if (booking is not null)
            {
                var eventItem = await events.LockForUpdateAsync(booking.EventId, cancellationToken);
                if (eventItem is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(Error.NotFound("No such eventItem."));
                }

                var released = await bookingCanceller.CancelLockedAsync(
                    booking,
                    eventItem,
                    ActorType.Staff,
                    actorId,
                    cancellationToken);
                if (released.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result.Failure(released.Error);
                }
            }

            if (invite is not null)
            {
                invite.MarkSuperseded();
            }

            audit.Record(
                AuditEntityTypes.Attendee,
                attendee.Id,
                AuditAction.AttendeeDeleted,
                ActorType.Staff,
                actorId,
                null);

            attendees.Remove(attendee);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
`````

## before — src/EventBooking.Application/Candidates/EmployeeGroupModels.cs — 1/1

<!-- vocabulary-file: {"id":61,"oldPath":"src/EventBooking.Application/Candidates/EmployeeGroupModels.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeGroupModels.cs","beforeSha":"6d38f2cd8fefa2d2301e9e361a3eb3cbb94f124683b512b04e4e151ab30af087","afterSha":"ceeac584021cd4a8248ce83a5faa7ed688415a5f5d8b49d4d15731c6c6d8e704","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Candidates;

/// <summary>One fixed Appointment Type projected for group and Candidate read models.</summary>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public sealed record AppointmentTypeSummary(string Code, string Name);

/// <summary>One active Employee Group available for assignment.</summary>
/// <param name="EmployeeGroupId">The employee group id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
public sealed record EmployeeGroupListItem(
    Guid EmployeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes);

/// <summary>Requests active Employee Groups for one authorized Coordinator.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record ListEmployeeGroupsQuery(Guid StaffUserId);
`````

## after — src/EventBooking.Application/Attendees/AttendeeGroupModels.cs — 1/1

<!-- vocabulary-file: {"id":61,"oldPath":"src/EventBooking.Application/Candidates/EmployeeGroupModels.cs","newPath":"src/EventBooking.Application/Attendees/AttendeeGroupModels.cs","beforeSha":"6d38f2cd8fefa2d2301e9e361a3eb3cbb94f124683b512b04e4e151ab30af087","afterSha":"ceeac584021cd4a8248ce83a5faa7ed688415a5f5d8b49d4d15731c6c6d8e704","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Attendees;

/// <summary>One fixed Appointment Type projected for group and Attendee read models.</summary>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
public sealed record AppointmentTypeSummary(string Code, string Name);

/// <summary>One active Attendee Group available for assignment.</summary>
/// <param name="AttendeeGroupId">The attendee group id.</param>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="RequiredAppointmentTypes">The required appointment types.</param>
public sealed record AttendeeGroupListItem(
    Guid AttendeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes);

/// <summary>Requests active Attendee Groups for one authorized Coordinator.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record ListAttendeeGroupsQuery(Guid StaffUserId);
`````

## before — src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs — 1/1

<!-- vocabulary-file: {"id":62,"oldPath":"src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs","newPath":"src/EventBooking.Application/Attendees/GetAttendeeBookingsHandler.cs","beforeSha":"8953dcdce3c9bd9e81af2185ba9e9dbb8e24697ce5424fc9a78c380e1c11c418","afterSha":"04ec62a260ac5bf7b2c6046a505f121d46ebedcc9e569695a0197267026c6aaa","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Candidates;

/// <summary>One active booking a coordinator may cancel, without any management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="SlotDate">The date of the confirmed window the booking holds.</param>
/// <param name="SlotStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="SlotEndTime">The end of the confirmed window the booking holds.</param>
public sealed record CandidateBookingSummary(
    Guid BookingId,
    bool IsOriginal,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime);

/// <summary>Requests the active bookings a coordinator may cancel for one candidate.</summary>
/// <param name="StaffUserId">The staff member asking for the listing.</param>
/// <param name="CandidateId">The candidate whose bookings are listed.</param>
public sealed record GetCandidateBookingsQuery(Guid StaffUserId, Guid CandidateId);

/// <summary>Authorizes a coordinator before listing a candidate's active bookings.</summary>
/// <param name="access">Authorizes candidate management.</param>
/// <param name="queries">Loads the active-booking projection.</param>
public sealed class GetCandidateBookingsHandler(
    IStaffAccessAuthorizer access,
    ICandidateBookingQueries queries)
{
    /// <summary>Authorizes a coordinator before loading or returning candidate-linked state.</summary>
    /// <param name="query">The staff listing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The candidate's active bookings, a forbidden failure when the caller cannot manage
    /// candidates, or a not-found failure for an unknown candidate.
    /// </returns>
    public async Task<Result<IReadOnlyList<CandidateBookingSummary>>> HandleAsync(
        GetCandidateBookingsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<IReadOnlyList<CandidateBookingSummary>>.Failure(authorized.Error);
        }

        var rows = await queries.ListActiveForCandidateAsync(query.CandidateId, cancellationToken);
        if (rows is null)
        {
            return Result<IReadOnlyList<CandidateBookingSummary>>.Failure(
                Error.NotFound("No such candidate."));
        }

        return Result<IReadOnlyList<CandidateBookingSummary>>.Success(rows);
    }
}
`````
