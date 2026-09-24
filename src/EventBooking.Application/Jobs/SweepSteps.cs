using EventBooking.Application.Abstractions;
using EventBooking.Application.Invites;
using EventBooking.Application.Recovery;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;
using Microsoft.Extensions.Logging;

namespace EventBooking.Application.Jobs;

/// <summary>One sweep run's per-step item counts, plus the items that threw.</summary>
/// <param name="ExpiredInvites">How many due invites were processed.</param>
/// <param name="WithdrawnProposals">How many open proposals were processed.</param>
/// <param name="ConcludedRecoveries">How many concluding candidates were processed.</param>
/// <param name="Failures">How many items threw instead of completing.</param>
public sealed record SweepMetrics(
    int ExpiredInvites, int WithdrawnProposals, int ConcludedRecoveries, int Failures);

/// <summary>One sweep run executes three steps. Each item commits or rolls back on its own; the run aggregates metrics only. Steps delegate to their task handlers: expiry to Task 14's per-item handler, withdrawal through TryWithdrawStarted under the proposal lock, conclusion to Task 16's conclude handler.</summary>
public interface ISweepSteps
{
    /// <summary>Expires one due invite.</summary>
    /// <param name="inviteId">The invite.</param>
    /// <param name="ct">The cancellation token.</param>
    Task ExpireOneAsync(Guid inviteId, CancellationToken ct);

    /// <summary>Withdraws one open proposal whose window has started, if it has.</summary>
    /// <param name="proposalId">The proposal.</param>
    /// <param name="ct">The cancellation token.</param>
    Task WithdrawOneAsync(Guid proposalId, CancellationToken ct);

    /// <summary>Concludes one recovery booking whose appointments are all terminal.</summary>
    /// <param name="bookingId">The booking.</param>
    /// <param name="ct">The cancellation token.</param>
    Task ConcludeOneAsync(Guid bookingId, CancellationToken ct);
}

/// <summary>The sweep's three steps, each delegating to its task's handler.</summary>
/// <param name="invites">The invite repository.</param>
/// <param name="attendees">The attendee repository.</param>
/// <param name="proposals">The proposal repository.</param>
/// <param name="locations">The location repository.</param>
/// <param name="bookings">The booking repository.</param>
/// <param name="appointments">The appointment repository.</param>
/// <param name="eligibility">The event eligibility query.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit logger.</param>
/// <param name="clock">The clock.</param>
/// <param name="zones">The zone abstraction.</param>
/// <param name="issuer">The invite issuer.</param>
public sealed class SweepSteps(
    IInviteRepository invites,
    IAttendeeRepository attendees,
    IEventProposalRepository proposals,
    ILocationRepository locations,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    IEventEligibilityQuery eligibility,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IEventWindowZones zones,
    IInviteIssuer issuer) : ISweepSteps
{
    /// <inheritdoc />
    public async Task ExpireOneAsync(Guid inviteId, CancellationToken ct)
    {
        var handler = new ExpireInviteHandler(
            invites, attendees, eligibility, unitOfWork, audit, clock, issuer);
        var result = await handler.HandleAsync(new ExpireInviteCommand(inviteId), ct);
        if (result.IsFailure)
            throw new DomainException(result.Error.Message);
    }

    /// <inheritdoc />
    public async Task WithdrawOneAsync(Guid proposalId, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var proposal = await proposals.LockForUpdateAsync(proposalId, ct);
        if (proposal is null) return;
        var location = await locations.GetAsync(proposal.LocationId, ct);
        if (location is null) return;
        if (proposal.TryWithdrawStarted(zones, location.TimeZoneId, clock.UtcNow))
        {
            audit.Record(AuditEntityTypes.EventProposal, proposal.Id,
                AuditAction.ProposalWithdrawn, ActorType.System, null, "window started");
            await unitOfWork.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    /// <inheritdoc />
    public async Task ConcludeOneAsync(Guid bookingId, CancellationToken ct)
    {
        var handler = new ConcludeRecoveryHandler(bookings, appointments, unitOfWork, audit);
        var result = await handler.HandleAsync(bookingId, ct);
        if (result.IsFailure)
            throw new DomainException(result.Error.Message);
    }
}

/// <summary>Runs one sweep pass over the three item sets, aggregating metrics.</summary>
/// <param name="steps">The steps.</param>
/// <param name="logger">The logger.</param>
public sealed class SweepRunner(ISweepSteps steps, ILogger<SweepRunner> logger)
{
    /// <summary>Runs one pass unless another run holds the lock.</summary>
    /// <param name="tryAcquire">Acquires the sweep lock.</param>
    /// <param name="expiredInvites">Lists the due invite ids.</param>
    /// <param name="openProposals">Lists the open proposal ids.</param>
    /// <param name="concludingBookings">Lists the concluding booking ids.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<(bool Started, SweepMetrics Metrics)> RunOnceAsync(
        Func<CancellationToken, Task<bool>> tryAcquire,
        Func<CancellationToken, Task<IReadOnlyList<Guid>>> expiredInvites,
        Func<CancellationToken, Task<IReadOnlyList<Guid>>> openProposals,
        Func<CancellationToken, Task<IReadOnlyList<Guid>>> concludingBookings,
        CancellationToken ct)
    {
        if (!await tryAcquire(ct))
            return (false, new SweepMetrics(0, 0, 0, 0));

        var metrics = new SweepMetrics(0, 0, 0, 0);
        metrics = await RunItemsAsync(expiredInvites,
            (id, token) => steps.ExpireOneAsync(id, token), ct, metrics,
            (m, n) => m with { ExpiredInvites = n });
        metrics = await RunItemsAsync(openProposals,
            (id, token) => steps.WithdrawOneAsync(id, token), ct, metrics,
            (m, n) => m with { WithdrawnProposals = n });
        metrics = await RunItemsAsync(concludingBookings,
            (id, token) => steps.ConcludeOneAsync(id, token), ct, metrics,
            (m, n) => m with { ConcludedRecoveries = n });
        return (true, metrics);
    }

    private async Task<SweepMetrics> RunItemsAsync(
        Func<CancellationToken, Task<IReadOnlyList<Guid>>> list,
        Func<Guid, CancellationToken, Task> run,
        CancellationToken ct, SweepMetrics metrics,
        Func<SweepMetrics, int, SweepMetrics> set)
    {
        var done = 0;
        foreach (var id in await list(ct))
        {
            try
            {
                await run(id, ct);
                done++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sweep item failed.");
                metrics = metrics with { Failures = metrics.Failures + 1 };
            }
        }

        return set(metrics, done);
    }
}
