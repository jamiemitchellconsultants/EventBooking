using EventBooking.Application.Abstractions;
using EventBooking.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Persists a candidate's status timestamp in the same save as the status change.
/// </summary>
public sealed class StatusStampingInterceptor(IClock clock) : SaveChangesInterceptor
{
    public const string ShadowProperty = "StatusChangedAt";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<Candidate>())
        {
            if (entry.State == EntityState.Added ||
                (entry.State == EntityState.Modified && entry.Property(c => c.Status).IsModified))
            {
                entry.Property<DateTimeOffset>(ShadowProperty).CurrentValue = clock.UtcNow;
            }
        }
    }
}
