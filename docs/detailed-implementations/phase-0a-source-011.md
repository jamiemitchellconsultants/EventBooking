# 00a — Port source 11 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs","encoding":"utf8","sha256":"dab740fb627391cbe1ba02cb1f53c57e5e7454e9a97891b35030d1f4939e7747","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Requests cancellation of a confirmed slot and, when authorized, its bookings.</summary>
/// <param name="StaffUserId">The staff identity performing the cancellation.</param>
/// <param name="ConfirmedSlotId">The confirmed slot to cancel.</param>
/// <param name="ConfirmCascade">Whether cancellation of active bookings is authorized.</param>
public sealed record CancelConfirmedSlotCommand(
    Guid StaffUserId,
    Guid ConfirmedSlotId,
    bool ConfirmCascade);

/// <summary>Reports the durable booking and reinvite changes made by slot cancellation.</summary>
/// <param name="BookingsVoided">The number of active bookings transitioned to cancelled.</param>
/// <param name="CandidatesReinvited">The number of affected candidates issued a replacement invite.</param>
public sealed record CancelSlotOutcome(int BookingsVoided, int CandidatesReinvited);

/// <summary>Cancels a confirmed slot while serializing the candidate lifecycle before slot state.</summary>
/// <param name="slots">Loads and locks confirmed-slot rows.</param>
/// <param name="bookings">Reads the affected bookings and candidate-ID worklist.</param>
/// <param name="invites">Locks pending invites before booking rows.</param>
/// <param name="candidates">Locks candidate lifecycle roots and returns tracked candidates.</param>
/// 
/// <param name="bookingCanceller">Releases booking capacity under the slot lock.</param>
/// <param name="issuer">Creates replacement invites after cancellation.</param>
/// <param name="deliveries">Stages and dispatches cancellation and replacement notifications.</param>
/// <param name="audit">Records cancellation and capacity changes.</param>
/// <param name="unitOfWork">Owns the transaction enclosing the lifecycle transitions.</param>
/// <param name="access">The access.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="clock">The clock.</param>
public sealed class CancelConfirmedSlotHandler(
    IConfirmedSlotRepository slots,
    IBookingRepository bookings,
    IInviteRepository invites,
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    BookingCanceller bookingCanceller,
    InviteIssuer issuer,
    EligibleSlotFinder slotFinder,
    IBookingAppointmentRepository appointments,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Cancels the slot and its current active bookings, preserving notification and reinvite
    /// behavior while taking candidate lifecycle locks before the confirmed-slot lock.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<CancelSlotOutcome>> HandleAsync(
        CancelConfirmedSlotCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.CancelConfirmedSlot,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<CancelSlotOutcome>.Failure(authorized.Error);
        }

        // This read is only a candidate-id worklist. It is deliberately non-authoritative and
        // no returned booking entity is ever mutated; each identifier is re-read under the
        // candidate lifecycle lock below.
        var affectedCandidateIds = (await bookings.ListActiveCandidateIdsForSlotAsync(
                command.ConfirmedSlotId,
                cancellationToken))
            .Distinct()
            .OrderBy(candidateId => candidateId)
            .ToArray();

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var lockedCandidates = new Dictionary<Guid, Candidate>();
        var lockedPending = new Dictionary<Guid, IReadOnlyList<Invite>>();
        var lockedOriginals = new Dictionary<Guid, Booking?>();
        var lockedRecoveries = new Dictionary<Guid, Booking?>();

        // Every candidate lifecycle transition uses Candidate -> Invite -> Booking (original,
        // then active recovery). Candidate identifiers are sorted so a slot cancellation
        // touching multiple candidates cannot deadlock another slot cancellation that touches
        // the same set in a different order.
        foreach (var candidateId in affectedCandidateIds)
        {
            var candidate = await candidates.LockForUpdateAsync(candidateId, cancellationToken);
            if (candidate is null)
            {
                continue;
            }

            lockedCandidates[candidate.Id] = candidate;
            lockedPending[candidate.Id] = await invites.LockPendingListForCandidateAsync(
                candidate.Id,
                cancellationToken);
            var original = await bookings.LockActiveOriginalForCandidateAsync(
                candidate.Id,
                cancellationToken);
            lockedOriginals[candidate.Id] = original;
            lockedRecoveries[candidate.Id] = original is null
                ? null
                : await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        }

        var slot = await slots.LockForUpdateAsync(command.ConfirmedSlotId, cancellationToken);
        if (slot is null)
        {
            return Result<CancelSlotOutcome>.Failure(Error.NotFound("No such slot."));
        }

        if (slot.Status == ConfirmedSlotStatus.Cancelled)
        {
            return Result<CancelSlotOutcome>.Failure(
                Error.Conflict("This slot has already been cancelled."));
        }

        // A slot can no longer be cancelled once its date has started.
        if (slot.Window.Date < clock.TodayAtHeadOffice)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                "This appointment has already taken place and can no longer be cancelled."));
        }

        // Re-read after all lifecycle locks and the slot guard. Only the entities returned by the
        // candidate/booking lock calls are eligible for mutation, preventing a stale pre-lock
        // booking instance from being changed.
        var authoritativeBookings = await bookings.ListActiveForSlotAsync(slot.Id, cancellationToken);
        var affected = new List<AffectedJourneyBooking>(authoritativeBookings.Count);
        foreach (var authoritativeBooking in authoritativeBookings)
        {
            if (!lockedCandidates.TryGetValue(authoritativeBooking.CandidateId, out var candidate))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                    "The slot changed while it was being cancelled. Please retry."));
            }

            Booking? locked = null;
            if (lockedOriginals[candidate.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedOriginals[candidate.Id];
            }
            else if (lockedRecoveries[candidate.Id]?.Id == authoritativeBooking.Id)
            {
                locked = lockedRecoveries[candidate.Id];
            }

            if (locked is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                    "The slot changed while it was being cancelled. Please retry."));
            }

            affected.Add(new AffectedJourneyBooking(candidate, locked));
        }

        affected.Sort((left, right) => left.Booking.Id.CompareTo(right.Booking.Id));

        if (!command.ConfirmCascade && affected.Count > 0)
        {
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(
                $"Cancelling this slot will cancel {affected.Count} confirmed bookings. "
                + "Affected candidates will be notified and re-invited. Confirm to proceed."));
        }

        var actorId = command.StaffUserId.ToString();
        var reinvited = 0;
        var dispatches = new List<SlotCancellationDispatch>();

        try
        {
            // Cancel first: the re-invites below must not be able to offer this slot back.
            slot.Cancel();

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.SlotCancelled,
                ActorType.Staff,
                actorId,
                $"{affected.Count} bookings voided");

            foreach (var (candidate, booking) in affected)
            {
                if (booking.IsOriginal)
                {
                    var released = await bookingCanceller.CancelLockedAsync(
                        booking,
                        slot,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (released.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(released.Error);
                    }

                    candidate.ResetToNotYetInvited();

                    var issueResult = await issuer.IssueInitialAsync(
                        candidate, 0, ActorType.Staff, actorId, isReinvite: false, cancellationToken);
                    if (issueResult.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(issueResult.Error);
                    }

                    var issued = issueResult.Value;
                    if (issued.Invited)
                    {
                        reinvited++;
                    }

                    var cancellation = deliveries.StagePending(
                        candidate.Id,
                        EmailTemplate.SlotCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        confirmedSlotId: slot.Id);
                    deliveries.ClaimForDispatch(cancellation);
                    dispatches.Add(new SlotCancellationDispatch(
                        candidate,
                        released.Value,
                        slot,
                        cancellation.Id,
                        issued.DispatchPlan));
                }
                else
                {
                    var releasedRecovery = await bookingCanceller.CancelLockedAsync(
                        booking,
                        slot,
                        ActorType.Staff,
                        actorId,
                        cancellationToken);
                    if (releasedRecovery.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(releasedRecovery.Error);
                    }

                    var replacementPlan = await TryIssueReplacementRecoveryAsync(
                        candidate,
                        booking,
                        lockedPending[candidate.Id],
                        actorId,
                        cancellationToken);
                    if (replacementPlan.IsFailure)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<CancelSlotOutcome>.Failure(replacementPlan.Error);
                    }

                    if (replacementPlan.Value is not null)
                    {
                        reinvited++;
                    }

                    var recoveryCancellation = deliveries.StagePending(
                        candidate.Id,
                        EmailTemplate.SlotCancelledRebookingNeeded,
                        bookingId: booking.Id,
                        confirmedSlotId: slot.Id);
                    deliveries.ClaimForDispatch(recoveryCancellation);
                    dispatches.Add(new SlotCancellationDispatch(
                        candidate,
                        releasedRecovery.Value,
                        slot,
                        recoveryCancellation.Id,
                        replacementPlan.Value));
                }
            }
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<CancelSlotOutcome>.Failure(Error.Conflict(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        foreach (var dispatch in dispatches)
        {
            var replacementSent = false;
            if (dispatch.InvitePlan is { } invitePlan)
            {
                var inviteStatus = await deliveries.DispatchClaimedAsync(
                    invitePlan.DeliveryId, invitePlan.Message, cancellationToken, invitePlan.OnSent);
                replacementSent = inviteStatus == EmailStatus.Sent;
            }

            await deliveries.DispatchClaimedAsync(
                dispatch.CancellationDeliveryId,
                CandidateEmailComposer.SlotCancelled(
                    dispatch.Candidate,
                    dispatch.AppointmentTypeIds,
                    dispatch.Slot,
                    replacementSent),
                cancellationToken);
        }

        return Result<CancelSlotOutcome>.Success(new CancelSlotOutcome(affected.Count, reinvited));
    }

    /// <summary>
    /// Issues a replacement recovery Invite for the cancelled recovery's still-outstanding types,
    /// or returns no plan when three options are unavailable so the Coordinator can act later.
    /// </summary>
    private async Task<Result<EmailDispatchPlan?>> TryIssueReplacementRecoveryAsync(
        Candidate candidate,
        Booking booking,
        IReadOnlyList<Invite> pending,
        string actorId,
        CancellationToken cancellationToken)
    {
        var rootId = booking.RecoveryOfBookingId!.Value;
        var journey = await bookings.ListJourneyAsync(rootId, cancellationToken);
        var rows = await appointments.ListForBookingsAsync(
            journey.Select(entry => entry.Id).ToList(),
            cancellationToken);
        var covered = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();
        var selected = new RecoveryRequirementSelector().Select(
            candidate.RequiredAppointmentTypeIds,
            RecoveryConfirmationValidator.BuildAttempts(journey, rows),
            covered);

        if (selected.Count == 0)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var options = await slotFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
            return Result<EmailDispatchPlan?>.Success(null);
        }

        var replacement = await issuer.IssueRecoveryAsync(
            candidate,
            rootId,
            selected,
            options,
            ActorType.Staff,
            actorId,
            cancellationToken);
        if (replacement.IsFailure)
        {
            return Result<EmailDispatchPlan?>.Failure(replacement.Error);
        }

        return Result<EmailDispatchPlan?>.Success(replacement.Value.DispatchPlan);
    }
}

/// <summary>One locked candidate and the locked journey Booking cancelled with the slot.</summary>
/// <param name="Candidate">The lifecycle-locked candidate.</param>
/// <param name="Booking">The locked original or recovery Booking on the cancelled slot.</param>
internal sealed record AffectedJourneyBooking(Candidate Candidate, Booking Booking);

internal sealed record SlotCancellationDispatch(
    Candidate Candidate,
    IReadOnlyList<Guid> AppointmentTypeIds,
    ConfirmedSlot Slot,
    Guid CancellationDeliveryId,
    EmailDispatchPlan? InvitePlan);
`````

## src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs","encoding":"utf8","sha256":"57e01370a4a8ce0696b410dde59506705c4cee1053606b025e9fec4a7b301e88","parts":1,"part":1} -->

`````csharp
using System.Globalization;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines confirmed slot import row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Window">The window.</param>
/// <param name="HeadcountsByAppointmentType">The headcounts by appointment type.</param>
public sealed record ConfirmedSlotImportRow(
    int LineNumber, SlotWindow Window, IReadOnlyDictionary<Guid, int> HeadcountsByAppointmentType);

/// <summary>Defines confirmed slot import error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record ConfirmedSlotImportError(int LineNumber, string Message);

/// <summary>Defines confirmed slot import parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record ConfirmedSlotImportParseResult(
    IReadOnlyList<ConfirmedSlotImportRow> Rows,
    IReadOnlyList<ConfirmedSlotImportError> Errors);

/// <summary>
/// Structural validation only, mirroring CandidateCsvParser (Task 29): header, field count,
/// parseable date/time, a positive integer per fixed AppointmentType column, duplicate windows
/// within the file, and a row-count ceiling. Nothing here reads the database.
/// </summary>
public static class ConfirmedSlotImportParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "date,startTime,DAT,MED,UNI";
    /// <summary>Defines max rows for the current use case.</summary>
    public const int MaxRows = 200;

    private static readonly (string Code, Guid Id)[] TypeColumns =
    [
        ("DAT", AppointmentTypeIds.DrugAndAlcoholTesting),
        ("MED", AppointmentTypeIds.MedicalCheckUp),
        ("UNI", AppointmentTypeIds.UniformFitting),
    ];

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    /// <param name="today">The today.</param>
    public static ConfirmedSlotImportParseResult Parse(string? content, DateOnly? today = null)
    {
        var rows = new List<ConfirmedSlotImportRow>();
        var errors = new List<ConfirmedSlotImportError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new ConfirmedSlotImportError(0, "The file is empty."));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ConfirmedSlotImportError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var dataLineNumbers = new List<int>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                dataLineNumbers.Add(index);
            }
        }

        if (dataLineNumbers.Count > MaxRows)
        {
            errors.Add(new ConfirmedSlotImportError(0, $"A file may contain at most 200 rows."));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var seenWindows = new Dictionary<(DateOnly Date, TimeOnly StartTime), int>();

        foreach (var index in dataLineNumbers)
        {
            var lineNumber = index + 1;
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, "Expected 5 comma-separated fields: date,startTime,DAT,MED,UNI."));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    fields[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, $"{fields[0].Trim()} is not a valid date (expected yyyy-MM-dd)."));
                continue;
            }

            if (!TimeOnly.TryParseExact(
                    fields[1].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, $"{fields[1].Trim()} is not a valid startTime (expected HH:mm)."));
                continue;
            }

            if (!TryReadHeadcounts(fields, lineNumber, errors, out var headcounts))
            {
                continue;
            }

            var window = (date, startTime);

            SlotWindow slotWindow;
            try
            {
                slotWindow = new SlotWindow(date, startTime);
            }
            catch (DomainException ex)
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, ex.Message));
                continue;
            }

            // Imported slots follow the same future-date rule as slot proposals.
            if (today.HasValue && !slotWindow.StartsAfter(today.Value))
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, "The slot date must be in the future."));
                continue;
            }

            if (seenWindows.TryGetValue(window, out var firstLine))
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, $"Duplicate slot window — already used on line {firstLine}."));
                continue;
            }

            seenWindows.Add(window, lineNumber);
            rows.Add(new ConfirmedSlotImportRow(lineNumber, slotWindow, headcounts));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new ConfirmedSlotImportError(1, "The file contains no slot rows."));
        }

        return new ConfirmedSlotImportParseResult(rows, errors);
    }

    private static bool TryReadHeadcounts(
        string[] fields,
        int lineNumber,
        List<ConfirmedSlotImportError> errors,
        out IReadOnlyDictionary<Guid, int> headcounts)
    {
        var result = new Dictionary<Guid, int>();
        headcounts = result;

        for (var column = 0; column < TypeColumns.Length; column++)
        {
            var (code, id) = TypeColumns[column];
            var raw = fields[column + 2].Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var headcount)
                || headcount <= 0)
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, $"{code} headcount must be a positive whole number."));
                return false;
            }

            result[id] = headcount;
        }

        return true;
    }
}
`````

## src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs","encoding":"utf8","sha256":"eda6266bc12628b1cf21f1b0627e0f65ee118adc0b9b204083955bc290ddec21","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Slots;

/// <summary>Defines open proposal view for the current use case.</summary>
/// <param name="ProposalId">The proposal id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="AcceptedByAppointmentTypeNames">The accepted by appointment type names.</param>
/// <param name="MyAcceptedHeadcount">The my accepted headcount.</param>
/// <param name="AcceptedByMe">The accepted by me.</param>
/// <param name="CreatedByMe">The created by me.</param>
public sealed record OpenProposalView(
    Guid ProposalId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

/// <summary>Defines manager confirmed slot view for the current use case.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
/// <param name="EndTime">The end time.</param>
/// <param name="MyHeadcount">The my headcount.</param>
/// <param name="MyRemainingCapacity">The my remaining capacity.</param>
public sealed record ManagerConfirmedSlotView(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

/// <summary>Defines manager slot board for the current use case.</summary>
/// <param name="OpenProposals">The open proposals.</param>
/// <param name="ConfirmedSlots">The confirmed slots.</param>
public sealed record ManagerSlotBoard(
    IReadOnlyList<OpenProposalView> OpenProposals,
    IReadOnlyList<ManagerConfirmedSlotView> ConfirmedSlots);

/// <summary>Defines get manager slot board query for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
public sealed record GetManagerSlotBoardQuery(Guid ManagerUserId);

/// <summary>Defines get manager slot board handler for the current use case.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="confirmedSlots">The confirmed slots.</param>
/// <param name="access">The access.</param>
/// <param name="clock">The clock.</param>
public sealed class GetManagerSlotBoardHandler(
    ISlotProposalRepository proposals,
    IConfirmedSlotRepository confirmedSlots,
    IStaffAccessAuthorizer access,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ManagerSlotBoard>> HandleAsync(
        GetManagerSlotBoardQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ManagerSlotBoard>.Failure(authorized.Error);
        }

        var myType = authorized.Value.AppointmentTypeId!.Value;

        var open = (await proposals.ListOpenAsync(cancellationToken))
            .OrderBy(proposal => proposal.Window)
            .Select(proposal =>
            {
                var myAcceptance = proposal.Acceptances.SingleOrDefault(
                    acceptance =>
                        acceptance.AppointmentTypeId == myType
                        && acceptance.ManagerUserId == query.ManagerUserId);

                return new OpenProposalView(
                    proposal.Id,
                    proposal.Window.Date,
                    proposal.Window.StartTime,
                    proposal.Window.EndTime,
                    proposal.Acceptances
                        .Select(acceptance =>
                            AppointmentTypeIds.NameOf(acceptance.AppointmentTypeId))
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToList(),
                    myAcceptance?.Headcount,
                    myAcceptance is not null,
                    proposal.CreatedByManagerUserId == query.ManagerUserId);
            })
            .ToList();

        var confirmed = (await confirmedSlots.ListActiveAsync(clock.TodayAtHeadOffice, cancellationToken))
            .OrderBy(s => s.Window)
            .Select(s =>
            {
                var capacity = s.CapacityFor(myType);
                return new ManagerConfirmedSlotView(
                    s.Id,
                    s.Window.Date,
                    s.Window.StartTime,
                    s.Window.EndTime,
                    capacity.TotalHeadcount,
                    capacity.RemainingCapacity);
            })
            .ToList();

        return Result<ManagerSlotBoard>.Success(new ManagerSlotBoard(open, confirmed));
    }
}
`````

## src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs","encoding":"utf8","sha256":"e3185882c4e4da885498241da3a82990a9092201518af303ef3df46243f9d1cf","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <param name="StaffUserId">The staff identity performing the import.</param>
/// <param name="CsvContent">The raw CSV file content.</param>
/// <param name="AllowPastDates">Whether historical slot dates are accepted. Only the demo
/// seeder sets this: its agreed slots are deliberately historical, while the user-facing
/// import requires future dates like slot proposals do.</param>
public sealed record ImportConfirmedSlotsCommand(Guid StaffUserId, string? CsvContent, bool AllowPastDates = false);

/// <summary>Defines confirmed slot import outcome for the current use case.</summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record ConfirmedSlotImportOutcome(
    bool Accepted, int ImportedCount, IReadOnlyList<ConfirmedSlotImportError> Errors);

/// <summary>Defines import confirmed slots handler for the current use case.</summary>
/// <param name="confirmedSlots">The confirmed slots.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ImportConfirmedSlotsHandler(
    IConfirmedSlotRepository confirmedSlots,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ConfirmedSlotImportOutcome>> HandleAsync(
        ImportConfirmedSlotsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ImportConfirmedSlots,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ConfirmedSlotImportOutcome>.Failure(authorized.Error);
        }

        var parsed = ConfirmedSlotImportParser.Parse(
            command.CsvContent,
            command.AllowPastDates ? null : clock.TodayAtHeadOffice);
        if (parsed.Errors.Count > 0)
        {
            return Result<ConfirmedSlotImportOutcome>.Success(
                new ConfirmedSlotImportOutcome(false, 0, parsed.Errors));
        }

        foreach (var row in parsed.Rows)
        {
            var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), row.Window, row.HeadcountsByAppointmentType);
            confirmedSlots.Add(slot);

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.SlotImported,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"Imported from CSV line {row.LineNumber}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ConfirmedSlotImportOutcome>.Success(
            new ConfirmedSlotImportOutcome(true, parsed.Rows.Count, []));
    }
}
`````

## src/EventBooking.Application/Slots/ProposeSlotHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/ProposeSlotHandler.cs","encoding":"utf8","sha256":"bf9be5d3612db3db5ed1cdf07fb73921293a119049fcb4dc738df0b58c5ed7b1","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines propose slot command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
public sealed record ProposeSlotCommand(Guid ManagerUserId, DateOnly Date, TimeOnly StartTime);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ProposeSlotHandler(
    ISlotProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeSlotCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        SlotWindow window;
        try
        {
            window = new SlotWindow(command.Date, command.StartTime);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtHeadOffice))
        {
            return Result<Guid>.Failure(Error.Validation("A slot must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        SlotProposal proposal;
        try
        {
            proposal = SlotProposal.Create(id, window, command.ManagerUserId);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.SlotProposal,
                id,
                AuditAction.ProposalCreated,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                window.ToString());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        return Result<Guid>.Success(id);
    }
}
`````

## src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs","encoding":"utf8","sha256":"7daf22bdf4b0f57422b0a957391697ee57f6ff48b69e30b5c366ed3227cca919","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines withdraw acceptance command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
public sealed record WithdrawAcceptanceCommand(Guid ManagerUserId, Guid ProposalId);

/// <summary>Withdraws an acceptance while holding the affected proposal row lock.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class WithdrawAcceptanceHandler(
    ISlotProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Removes the caller's appointment-type acceptance or returns the relevant stable failure.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        WithdrawAcceptanceCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result.Failure(Error.NotFound("No such proposal."));
        }

        // A confirmed proposal is a conflict rather than a validation error: nothing about the
        // request is malformed, the world moved on.
        if (proposal.Status != SlotProposalStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("An acceptance can only be withdrawn while the proposal is still open."));
        }

        try
        {
            proposal.WithdrawAcceptance(authorized.Value.AppointmentTypeId!.Value, command.ManagerUserId);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.SlotProposal,
            proposal.Id,
            AuditAction.AcceptanceWithdrawn,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
`````

## src/EventBooking.Application/Slots/WithdrawProposalHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Slots/WithdrawProposalHandler.cs","encoding":"utf8","sha256":"cb9011b43cfe5b9304e8a25bfca3701b4e66bbee9942b09f5ac04b4236da06d5","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines withdraw proposal command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="ProposalId">The proposal id.</param>
public sealed record WithdrawProposalCommand(Guid ManagerUserId, Guid ProposalId);

/// <summary>Withdraws an open proposal while holding its lifecycle row lock.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
public sealed class WithdrawProposalHandler(
    ISlotProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit)
{
    /// <summary>Withdraws an open proposal when the caller is its appointment-type Manager or an Admin.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        WithdrawProposalCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageSlotNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            var admin = await access.AuthorizeAsync(
                command.ManagerUserId,
                StaffCapability.ManageSettings,
                null,
                cancellationToken);
            if (admin.IsFailure)
            {
                return Result.Failure(authorized.Error);
            }
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var proposal = await proposals.LockForUpdateAsync(command.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Result.Failure(Error.NotFound("No such proposal."));
        }

        if (proposal.Status != SlotProposalStatus.Open)
        {
            return Result.Failure(Error.Conflict("Only an open proposal can be withdrawn."));
        }

        try
        {
            proposal.Withdraw(command.ManagerUserId);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.SlotProposal,
            proposal.Id,
            AuditAction.ProposalWithdrawn,
            ActorType.Staff,
            command.ManagerUserId.ToString(),
            null);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
`````

## src/EventBooking.Domain/Access/Role.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Access/Role.cs","encoding":"utf8","sha256":"a09c64b56e7e1463faccba1feaed4b003fe00ecb5d495a881d5621ff0e133fba","parts":1,"part":1} -->

`````csharp
namespace EventBooking.Domain.Access;

/// <summary>Defines role for the current use case.</summary>
public enum Role
{
    /// <summary>Defines manager for the current use case.</summary>
    Manager = 1,
    /// <summary>Defines coordinator for the current use case.</summary>
    Coordinator = 2,
    /// <summary>Defines admin for the current use case.</summary>
    Admin = 3,
    /// <summary>Defines appointment staff for the current use case.</summary>
    AppointmentStaff = 4,
}
`````

## src/EventBooking.Domain/Access/StaffAccessProfile.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Access/StaffAccessProfile.cs","encoding":"utf8","sha256":"f826509ba05dcee7e06e8c8d44878881d7b1d774ecc7d5eb69c4fa4c9effa592","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>Defines staff access profile for the current use case.</summary>
public sealed class StaffAccessProfile
{
    private StaffAccessProfile()
    {
    }

    /// <summary>Defines staff user id for the current use case.</summary>
    public Guid StaffUserId { get; private set; }

    /// <summary>Defines is manager for the current use case.</summary>
    public bool IsManager { get; private set; }

    /// <summary>Defines is coordinator for the current use case.</summary>
    public bool IsCoordinator { get; private set; }

    /// <summary>Defines is admin for the current use case.</summary>
    public bool IsAdmin { get; private set; }

    /// <summary>Defines is appointment staff for the current use case.</summary>
    public bool IsAppointmentStaff { get; private set; }

    /// <summary>Defines appointment type id for the current use case.</summary>
    public Guid? AppointmentTypeId { get; private set; }

    /// <summary>Defines version for the current use case.</summary>
    public long Version { get; private set; }

    /// <summary>Defines roles for the current use case.</summary>
    public IReadOnlySet<Role> Roles => RoleSet();

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="roles">The roles.</param>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public static StaffAccessProfile Create(
        Guid staffUserId,
        IReadOnlyCollection<Role> roles,
        Guid? appointmentTypeId)
    {
        Guard.Against(staffUserId == Guid.Empty, "staffUserId must not be empty.");
        var roleSet = Validate(roles, appointmentTypeId);

        var profile = new StaffAccessProfile
        {
            StaffUserId = staffUserId,
            Version = 1,
        };
        profile.Apply(roleSet, appointmentTypeId);
        return profile;
    }

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="role">The role.</param>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public static StaffAccessProfile Create(
        Guid staffUserId,
        Role role,
        Guid? appointmentTypeId) =>
        Create(staffUserId, [role], appointmentTypeId);

    /// <summary>Whether the given role set requires an appointment-type scope
    /// (Manager and AppointmentStaff are scoped roles; Admin and Coordinator are not).</summary>
    /// <param name="roles">The roles.</param>
    public static bool NeedsScope(IReadOnlyCollection<Role> roles) =>
        roles.Contains(Role.Manager) || roles.Contains(Role.AppointmentStaff);

    /// <summary>Defines has role for the current use case.</summary>
    /// <param name="role">The role.</param>
    public bool HasRole(Role role) => role switch
    {
        Role.Manager => IsManager,
        Role.Coordinator => IsCoordinator,
        Role.Admin => IsAdmin,
        Role.AppointmentStaff => IsAppointmentStaff,
        _ => false,
    };

    /// <summary>Defines is valid for the current use case.</summary>
    public bool IsValid()
    {
        try
        {
            _ = Validate(RoleSet(), AppointmentTypeId);
            return StaffUserId != Guid.Empty && Version > 0;
        }
        catch (DomainException)
        {
            return false;
        }
    }

    /// <summary>Defines replace for the current use case.</summary>
    /// <param name="roles">The roles.</param>
    /// <param name="appointmentTypeId">The appointment type id.</param>
    public void Replace(IReadOnlyCollection<Role> roles, Guid? appointmentTypeId)
    {
        var roleSet = Validate(roles, appointmentTypeId);
        Apply(roleSet, appointmentTypeId);
        Version++;
    }

    /// <summary>Defines remove manager role for the current use case.</summary>
    public bool RemoveManagerRole()
    {
        Guard.Against(!IsManager, "The profile is not a Manager.");
        IsManager = false;
        if (!IsAppointmentStaff)
        {
            AppointmentTypeId = null;
        }

        Version++;
        return RoleSet().Count == 0;
    }

    private static HashSet<Role> Validate(
        IReadOnlyCollection<Role>? roles,
        Guid? appointmentTypeId)
    {
        Guard.Against(roles is null || roles.Count == 0, "At least one role is required.");
        var roleSet = roles!.ToHashSet();
        Guard.Against(
            roleSet.Any(role => !Enum.IsDefined(role)),
            "Every role must be recognised.");

        if (roleSet.Contains(Role.Admin))
        {
            Guard.Against(roleSet.Count != 1, "Admin cannot be combined with another role.");
            Guard.Against(appointmentTypeId is not null, "Admin cannot have an appointment type.");
            return roleSet;
        }

        Guard.Against(
            !NeedsScope(roleSet) && appointmentTypeId is not null,
            "Only Manager or AppointmentStaff can have an appointment type.");

        if (appointmentTypeId is not null)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId.Value);
        }

        return roleSet;
    }

    private void Apply(IReadOnlySet<Role> roles, Guid? appointmentTypeId)
    {
        IsManager = roles.Contains(Role.Manager);
        IsCoordinator = roles.Contains(Role.Coordinator);
        IsAdmin = roles.Contains(Role.Admin);
        IsAppointmentStaff = roles.Contains(Role.AppointmentStaff);
        AppointmentTypeId = appointmentTypeId;
    }

    private HashSet<Role> RoleSet()
    {
        var roles = new HashSet<Role>();
        if (IsManager) roles.Add(Role.Manager);
        if (IsCoordinator) roles.Add(Role.Coordinator);
        if (IsAdmin) roles.Add(Role.Admin);
        if (IsAppointmentStaff) roles.Add(Role.AppointmentStaff);
        return roles;
    }
}
`````

## src/EventBooking.Domain/Access/StaffId.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Domain/Access/StaffId.cs","encoding":"utf8","sha256":"99ab9f5ec3723bb5c864fa15aef6d786f03c390e41f0007487ef678ed912b171","parts":1,"part":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>
/// The enterprise staff number issued by HR: <c>U</c> or <c>N</c> followed by six digits.
/// Input is case-insensitive and the stored value is always uppercase.
/// </summary>
public sealed partial record StaffId
{
    /// <summary>Creates a canonical staff number from a valid seven-character value.</summary>
    /// <param name="value">The untrimmed value to validate without accepting surrounding whitespace.</param>
    /// <exception cref="DomainException">Thrown when the value is absent or malformed.</exception>
    public StaffId(string value)
    {
        Guard.Against(value is null || !StaffIdPattern().IsMatch(value),
            "staffId must be U or N followed by 6 digits.");
        Value = value!.ToUpperInvariant();
    }

    /// <summary>Gets the canonical uppercase seven-character staff number.</summary>
    public string Value { get; }

    /// <summary>Parses a staff number, throwing when the value is malformed.</summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>The canonical staff number.</returns>
    public static StaffId Parse(string value) => new(value);

    /// <summary>Attempts to parse untrusted input without throwing.</summary>
    /// <param name="value">The potentially absent or malformed value.</param>
    /// <param name="staffId">The canonical staff number when parsing succeeds; otherwise null.</param>
    /// <returns><see langword="true"/> only when the value has the required shape.</returns>
    public static bool TryParse(string? value, out StaffId? staffId)
    {
        if (value is not null && StaffIdPattern().IsMatch(value))
        {
            staffId = new StaffId(value);
            return true;
        }

        staffId = null;
        return false;
    }

    /// <summary>Returns the canonical uppercase staff number.</summary>
    /// <returns>The same value exposed by <see cref="Value"/>.</returns>
    public override string ToString() => Value;

    [GeneratedRegex("^[UuNn][0-9]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex StaffIdPattern();
}
`````
