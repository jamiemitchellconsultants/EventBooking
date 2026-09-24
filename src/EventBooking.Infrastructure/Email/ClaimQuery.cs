namespace EventBooking.Infrastructure.Email;

/// <summary>The skip-locked claim statement and its bounds.</summary>
public static class ClaimQuery
{
    /// <summary>How many rows one pass claims at most.</summary>
    public const int BatchSize = 20;

    /// <summary>How long a claim is honoured before another worker may reclaim the row.</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    /// <summary>How many claims a transient row survives before it ends failed.</summary>
    public const int MaxClaims = 3;

    /// <summary>
    /// Claims up to 20 pending rows whose claim expired or was never taken and whose
    /// backoff has passed, oldest first, skipping rows locked by another dispatcher.
    /// The correlation is write-once — a staged request identifier survives the claim —
    /// so ownership is the returned claim count, which no two passes can share.
    /// </summary>
    public const string Sql = """
        UPDATE email_log SET claimed_at = @now, claim_count = claim_count + 1,
            correlation_id = COALESCE(correlation_id, @correlationId)
         WHERE id IN (
            SELECT id FROM email_log
             WHERE status = 3
               AND (claimed_at IS NULL OR claimed_at < @now - make_interval(mins => 5))
               AND (not_before IS NULL OR not_before <= @now)
             ORDER BY id LIMIT 20 FOR UPDATE SKIP LOCKED)
        RETURNING id, claim_count;
        """;
}
