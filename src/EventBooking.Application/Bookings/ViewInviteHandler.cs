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
