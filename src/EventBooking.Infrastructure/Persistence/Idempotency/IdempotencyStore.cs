using EventBooking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Idempotency;

/// <summary>The PostgreSQL retention store.</summary>
/// <param name="context">The database context.</param>
public sealed class IdempotencyStore(EventBookingDbContext context) : IIdempotencyStore
{
    /// <inheritdoc />
    public async Task<IdempotentResponse?> TryGetAsync(
        Guid staffUserId, string route, string key, DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - IIdempotencyStore.Retention;
        var row = await context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.StaffUserId == staffUserId && x.Route == route && x.Key == key &&
                     x.CreatedAt > cutoff,
                ct);

        return row is null
            ? null
            : new IdempotentResponse(
                row.RequestHash, row.StatusCode, row.Body, row.ContentType, row.Location);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        Guid staffUserId, string route, string key, IdempotentResponse response,
        DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);

        // Delete the expired instance of this exact primary key before adding its replacement.
        // Doing this after Add would fail the 25-hour reuse case at the unique key.
        var cutoff = now - IIdempotencyStore.Retention;
        await context.IdempotencyRecords
            .Where(x => x.StaffUserId == staffUserId && x.Route == route && x.Key == key &&
                x.CreatedAt <= cutoff)
            .ExecuteDeleteAsync(ct);

        context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            StaffUserId = staffUserId,
            Route = route,
            Key = key,
            RequestHash = response.RequestHash,
            StatusCode = response.StatusCode,
            Body = response.Body,
            ContentType = response.ContentType,
            Location = response.Location,
            CreatedAt = now,
        });
        await context.SaveChangesAsync(ct);

        // Opportunistic pruning on the write path, keyed by the created-at index. A retention
        // table that only ever grows is the failure mode this avoids, and a sweep step would
        // put an unrelated concern into the Task 19 run.
        await context.IdempotencyRecords
            .Where(x => x.CreatedAt <= cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
