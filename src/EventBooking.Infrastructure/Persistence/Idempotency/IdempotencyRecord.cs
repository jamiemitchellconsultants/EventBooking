using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Idempotency;

/// <summary>
/// One retained create-endpoint response. This is infrastructure, not a domain concept: it
/// carries no business meaning, is keyed by a header value, and expires. It is deliberately
/// absent from the ontology for that reason.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>Gets the calling staff identity.</summary>
    public Guid StaffUserId { get; init; }

    /// <summary>Gets the route pattern the key was used on.</summary>
    public string Route { get; init; } = string.Empty;

    /// <summary>Gets the caller's key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Gets the hash of the request body the key was first used with.</summary>
    public string RequestHash { get; init; } = string.Empty;

    /// <summary>Gets the status the first call returned.</summary>
    public int StatusCode { get; init; }

    /// <summary>Gets the body the first call returned.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Gets the instant the row was written.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Maps the retention table.</summary>
public sealed class IdempotencyRecordConfiguration
    : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<IdempotencyRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("idempotency_record");
        builder.HasKey(x => new { x.StaffUserId, x.Route, x.Key });
        builder.Property(x => x.StaffUserId).HasColumnName("staff_user_id");
        builder.Property(x => x.Route).HasColumnName("route").HasMaxLength(200);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(200);
        builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(x => x.StatusCode).HasColumnName("status_code");
        builder.Property(x => x.Body).HasColumnName("body");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_idempotency_record_created_at");
    }
}
