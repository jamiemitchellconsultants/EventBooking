# 02b — One fresh schema, and the roles that keep the audit trail append-only, edits 1 (Task 9b)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Domain/Notifications/EmailLog.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Domain/Notifications/EmailLog.cs","beforeSha":"d1fca126627655b6034d20628b72d3e8c2a78b059c5c9e5ccf639bcc07486469","afterSha":"68618ffc01d30a05d71d10a967143d0b7279a8590be13b1d42a21906d7d7eb35","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Notifications;

/// <summary>
/// A durable record of one attendee email delivery attempt. Its identity and safe context are
/// immutable while claim and outcome fields transition; raw tokens, URLs, and bodies never belong
/// in this record.
/// </summary>
public sealed class EmailLog
{
    private EmailLog()
    {
    }

    /// <summary>The durable identifier of this delivery attempt.</summary>
    public Guid Id { get; private set; }

    /// <summary>The attendee who is the recipient of this delivery attempt.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>The attendee-facing template this attempt renders.</summary>
    public EmailTemplate TemplateName { get; private set; }

    /// <summary>The timestamp of the current or most recent attempt, supplied by <c>IClock</c>.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>The durable provider outcome, including <see cref="EmailStatus.Pending"/>.</summary>
    public EmailStatus Status { get; private set; }

    /// <summary>The invite context used by invite and re-invite templates, when applicable.</summary>
    public Guid? InviteId { get; private set; }

    /// <summary>The booking context used by a booking-confirmation template, when applicable.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>The event context used by a cancellation template, when applicable.</summary>
    public Guid? EventId { get; private set; }

    /// <summary>The in-progress claim timestamp used to prevent duplicate concurrent sends.</summary>
    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>Creates a legacy email attempt without a regeneration context.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="sentAt">The sent at.</param>
    /// <param name="status">The status.</param>
    public static EmailLog Record(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status)
        => PendingOrRecorded(id, attendeeId, templateName, sentAt, status, null, null, null);

    /// <summary>
    /// Creates a pending delivery with only safe context identifiers. The caller saves it in the
    /// same business transaction as the state change that caused the notification.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="createdAt">The created at.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="eventId">The event id.</param>
    public static EmailLog RecordPending(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset createdAt,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? eventId = null)
        => PendingOrRecorded(
            id,
            attendeeId,
            templateName,
            createdAt,
            EmailStatus.Pending,
            inviteId,
            bookingId,
            eventId);

    /// <summary>Claims a pending delivery unless another worker holds a fresh claim.</summary>
    /// <param name="now">The now.</param>
    /// <param name="lease">The lease.</param>
    public bool TryClaim(DateTimeOffset now, TimeSpan lease)
    {
        if (Status is EmailStatus.Sent or EmailStatus.Resolved)
        {
            return false;
        }

        if (ClaimedAt is not null && now - ClaimedAt.Value < lease)
        {
            return false;
        }

        ClaimedAt = now;
        return true;
    }

    /// <summary>Marks the claimed delivery as successfully sent.</summary>
    /// <param name="sentAt">The sent at.</param>
    public void MarkSent(DateTimeOffset sentAt)
    {
        Status = EmailStatus.Sent;
        SentAt = sentAt > SentAt ? sentAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks the claimed delivery as failed while retaining it for staff retry.</summary>
    /// <param name="failedAt">The failed at.</param>
    public void MarkFailed(DateTimeOffset failedAt)
    {
        Status = EmailStatus.Failed;
        SentAt = failedAt > SentAt ? failedAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks an outstanding attempt as superseded by a newer durable retry attempt.</summary>
    /// <param name="resolvedAt">The resolved at.</param>
    public void MarkResolved(DateTimeOffset resolvedAt)
    {
        Guard.Against(
            Status is EmailStatus.Sent or EmailStatus.Resolved,
            "Only an outstanding email delivery can be resolved.");
        Status = EmailStatus.Resolved;
        SentAt = resolvedAt > SentAt ? resolvedAt : SentAt;
        ClaimedAt = null;
    }

    private static EmailLog PendingOrRecorded(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status,
        Guid? inviteId,
        Guid? bookingId,
        Guid? eventId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        return new EmailLog
        {
            Id = id,
            AttendeeId = attendeeId,
            TemplateName = templateName,
            SentAt = sentAt,
            Status = status,
            InviteId = inviteId,
            BookingId = bookingId,
            EventId = eventId,
        };
    }
}
`````

## after — src/EventBooking.Domain/Notifications/EmailLog.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Domain/Notifications/EmailLog.cs","beforeSha":"d1fca126627655b6034d20628b72d3e8c2a78b059c5c9e5ccf639bcc07486469","afterSha":"68618ffc01d30a05d71d10a967143d0b7279a8590be13b1d42a21906d7d7eb35","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Notifications;

/// <summary>
/// A durable record of one attendee email delivery attempt. Its identity and safe context are
/// immutable while claim and outcome fields transition; raw tokens, URLs, and bodies never belong
/// in this record.
/// </summary>
public sealed class EmailLog
{
    private EmailLog()
    {
    }

    /// <summary>The durable identifier of this delivery attempt.</summary>
    public Guid Id { get; private set; }

    /// <summary>The attendee who is the recipient of this delivery attempt.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>The attendee-facing template this attempt renders.</summary>
    public EmailTemplate TemplateName { get; private set; }

    /// <summary>The timestamp of the current or most recent attempt, supplied by <c>IClock</c>.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>The durable provider outcome, including <see cref="EmailStatus.Pending"/>.</summary>
    public EmailStatus Status { get; private set; }

    /// <summary>The invite context used by invite and re-invite templates, when applicable.</summary>
    public Guid? InviteId { get; private set; }

    /// <summary>The booking context used by a booking-confirmation template, when applicable.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>The event context used by a cancellation template, when applicable.</summary>
    public Guid? EventId { get; private set; }

    /// <summary>The in-progress claim timestamp used to prevent duplicate concurrent sends.</summary>
    public DateTimeOffset? ClaimedAt { get; private set; }

    /// <summary>How many times a worker has claimed this attempt. The input to the backoff the
    /// dispatcher gains in Task 18; here it only has to be recorded.</summary>
    public int ClaimCount { get; private set; }

    /// <summary>Creates a legacy email attempt without a regeneration context.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="sentAt">The sent at.</param>
    /// <param name="status">The status.</param>
    public static EmailLog Record(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status)
        => PendingOrRecorded(id, attendeeId, templateName, sentAt, status, null, null, null);

    /// <summary>
    /// Creates a pending delivery with only safe context identifiers. The caller saves it in the
    /// same business transaction as the state change that caused the notification.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="templateName">The template name.</param>
    /// <param name="createdAt">The created at.</param>
    /// <param name="inviteId">The invite id.</param>
    /// <param name="bookingId">The booking id.</param>
    /// <param name="eventId">The event id.</param>
    public static EmailLog RecordPending(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset createdAt,
        Guid? inviteId = null,
        Guid? bookingId = null,
        Guid? eventId = null)
        => PendingOrRecorded(
            id,
            attendeeId,
            templateName,
            createdAt,
            EmailStatus.Pending,
            inviteId,
            bookingId,
            eventId);

    /// <summary>Claims a pending delivery unless another worker holds a fresh claim.</summary>
    /// <param name="now">The now.</param>
    /// <param name="lease">The lease.</param>
    public bool TryClaim(DateTimeOffset now, TimeSpan lease)
    {
        if (Status is EmailStatus.Sent or EmailStatus.Resolved)
        {
            return false;
        }

        if (ClaimedAt is not null && now - ClaimedAt.Value < lease)
        {
            return false;
        }

        ClaimedAt = now;
        ClaimCount++;
        return true;
    }

    /// <summary>Marks the claimed delivery as successfully sent.</summary>
    /// <param name="sentAt">The sent at.</param>
    public void MarkSent(DateTimeOffset sentAt)
    {
        Status = EmailStatus.Sent;
        SentAt = sentAt > SentAt ? sentAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks the claimed delivery as failed while retaining it for staff retry.</summary>
    /// <param name="failedAt">The failed at.</param>
    public void MarkFailed(DateTimeOffset failedAt)
    {
        Status = EmailStatus.Failed;
        SentAt = failedAt > SentAt ? failedAt : SentAt;
        ClaimedAt = null;
    }

    /// <summary>Marks an outstanding attempt as superseded by a newer durable retry attempt.</summary>
    /// <param name="resolvedAt">The resolved at.</param>
    public void MarkResolved(DateTimeOffset resolvedAt)
    {
        Guard.Against(
            Status is EmailStatus.Sent or EmailStatus.Resolved,
            "Only an outstanding email delivery can be resolved.");
        Status = EmailStatus.Resolved;
        SentAt = resolvedAt > SentAt ? resolvedAt : SentAt;
        ClaimedAt = null;
    }

    private static EmailLog PendingOrRecorded(
        Guid id,
        Guid attendeeId,
        EmailTemplate templateName,
        DateTimeOffset sentAt,
        EmailStatus status,
        Guid? inviteId,
        Guid? bookingId,
        Guid? eventId)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        return new EmailLog
        {
            Id = id,
            AttendeeId = attendeeId,
            TemplateName = templateName,
            SentAt = sentAt,
            Status = status,
            InviteId = inviteId,
            BookingId = bookingId,
            EventId = eventId,
        };
    }
}
`````

## before — src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj","beforeSha":"64ee70bf7ccd21edc796e6042a5b348c36636a6035434a33dae379d9b2c50ec9","afterSha":"556884ba365806e3f668c117cc196af2a38deaa15f3546d5a258315d4c206c7b","side":"before","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="NodaTime" />
    <PackageReference Include="MailKit" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

</Project>
`````

## after — src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj","beforeSha":"64ee70bf7ccd21edc796e6042a5b348c36636a6035434a33dae379d9b2c50ec9","afterSha":"556884ba365806e3f668c117cc196af2a38deaa15f3546d5a258315d4c206c7b","side":"after","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Persistence\Sql\roles.sql" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="NodaTime" />
    <PackageReference Include="MailKit" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

</Project>
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs","beforeSha":"88b58e313b119aae524b28f747d1bd7dddc8483e2b2d10926bf7f7305eb0b02e","afterSha":"e7bd5d5904f236e7a3996506d69e7b6769980ce0290c406605990aefad7caaa2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AttendeeConfiguration : IEntityTypeConfiguration<Attendee>
{
    public void Configure(EntityTypeBuilder<Attendee> builder)
    {
        builder.ToTable("attendee");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(c => c.AttendeeGroupId).HasColumnName("attendee_group_id").IsRequired();
        builder.HasOne<AttendeeGroup>()
            .WithMany()
            .HasForeignKey(c => c.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.AttendeeGroupId);
        builder.Property(c => c.StatusChangedAt).HasColumnName("status_changed_at");

        builder.Ignore(c => c.RequiredAppointmentTypeIds);

        builder
            .HasMany(c => c.Requirements)
            .WithOne()
            .HasForeignKey(r => r.AttendeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Status);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs","beforeSha":"88b58e313b119aae524b28f747d1bd7dddc8483e2b2d10926bf7f7305eb0b02e","afterSha":"e7bd5d5904f236e7a3996506d69e7b6769980ce0290c406605990aefad7caaa2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AttendeeConfiguration : IEntityTypeConfiguration<Attendee>
{
    public void Configure(EntityTypeBuilder<Attendee> builder)
    {
        builder.ToTable("attendee");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(c => c.AttendeeGroupId).HasColumnName("attendee_group_id").IsRequired();
        builder.HasOne<AttendeeGroup>()
            .WithMany()
            .HasForeignKey(c => c.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.AttendeeGroupId);
        builder.Property(c => c.StatusChangedAt).HasColumnName("status_changed_at");

        builder.Ignore(c => c.RequiredAppointmentTypeIds);

        builder
            .HasMany(c => c.Requirements)
            .WithOne()
            .HasForeignKey(r => r.AttendeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The unique index is on lower(email): EF cannot express a functional index, so the
        // initial migration creates ux_attendee_email_lower by hand.
        builder.HasIndex(c => c.Status);
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs","beforeSha":"1176d7a3e6a57146ebebf15ce3260d866547e32368afb7c13dda19c287537a96","afterSha":"8a592ce9ad57e1073920fb2b63d8e4d54d0d508d9f1c492937025daba65f04e5","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the change-controlled Attendee Group reference table.</summary>
public sealed class AttendeeGroupConfiguration : IEntityTypeConfiguration<AttendeeGroup>
{
    /// <summary>Configures the attendee group table, constraints, and mapping collection.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroup> builder)
    {
        builder.ToTable("attendee_group", table =>
        {
            table.HasCheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");
            table.HasCheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(group => group.IsActive).HasColumnName("is_active");
        builder.Property(group => group.Version).HasColumnName("version").IsConcurrencyToken();

        builder.Ignore(group => group.RequiredAppointmentTypeIds);

        builder
            .HasMany(group => group.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.Code).IsUnique();
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs","beforeSha":"1176d7a3e6a57146ebebf15ce3260d866547e32368afb7c13dda19c287537a96","afterSha":"8a592ce9ad57e1073920fb2b63d8e4d54d0d508d9f1c492937025daba65f04e5","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the change-controlled Attendee Group reference table.</summary>
public sealed class AttendeeGroupConfiguration : IEntityTypeConfiguration<AttendeeGroup>
{
    /// <summary>Configures the attendee group table, constraints, and mapping collection.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroup> builder)
    {
        builder.ToTable("attendee_group", table =>
        {
            table.HasCheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");
            table.HasCheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(group => group.IsActive).HasColumnName("is_active");
        builder.Property(group => group.Version).HasColumnName("version").IsConcurrencyToken();

        builder.Ignore(group => group.RequiredAppointmentTypeIds);

        builder
            .HasMany(group => group.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.Code).IsUnique();

        // Change-controlled reference data the predecessor seeded from a migration, kept here so
        // the fresh schema carries it without a second release. Retires with the fixed appointment
        // types in Phase 3.
        builder.HasData(
            new { Id = AttendeeGroupIds.CabinCrew, Code = "CABIN_CREW", Name = "Cabin Crew", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.Pilots, Code = "PILOTS", Name = "Pilots", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.GroundOperationsAgent, Code = "GROUND_OPERATIONS_AGENT", Name = "Ground Operations Agent", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.Engineering, Code = "ENGINEERING", Name = "Engineering", IsActive = true, Version = 1L },
            new { Id = AttendeeGroupIds.GroundTransportServices, Code = "GROUND_TRANSPORT_SERVICES", Name = "Ground Transport Services", IsActive = true, Version = 1L });
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs","beforeSha":"a1816404f5702e90c2cb076fb2c4c4ec2f876f6d52ae22126a95b9d3150cff9b","afterSha":"0e6b93b725110fe011860574d6f87c772e7e9674188ba8713efa6ef16a701e38","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Attendee Group to one fixed Appointment Type.</summary>
public sealed class AttendeeGroupRequirementConfiguration : IEntityTypeConfiguration<AttendeeGroupRequirement>
{
    /// <summary>Configures the composite mapping key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroupRequirement> builder)
    {
        builder.ToTable("attendee_group_requirement");
        builder.HasKey(requirement => new { requirement.AttendeeGroupId, requirement.AppointmentTypeId });

        builder.Property(requirement => requirement.AttendeeGroupId).HasColumnName("attendee_group_id");
        builder.Property(requirement => requirement.AppointmentTypeId).HasColumnName("appointment_type_id");

        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(requirement => requirement.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs","beforeSha":"a1816404f5702e90c2cb076fb2c4c4ec2f876f6d52ae22126a95b9d3150cff9b","afterSha":"0e6b93b725110fe011860574d6f87c772e7e9674188ba8713efa6ef16a701e38","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Attendee Group to one fixed Appointment Type.</summary>
public sealed class AttendeeGroupRequirementConfiguration : IEntityTypeConfiguration<AttendeeGroupRequirement>
{
    /// <summary>Configures the composite mapping key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroupRequirement> builder)
    {
        builder.ToTable("attendee_group_requirement");
        builder.HasKey(requirement => new { requirement.AttendeeGroupId, requirement.AppointmentTypeId });

        builder.Property(requirement => requirement.AttendeeGroupId).HasColumnName("attendee_group_id");
        builder.Property(requirement => requirement.AppointmentTypeId).HasColumnName("appointment_type_id");

        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(requirement => requirement.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            Requirement(AttendeeGroupIds.CabinCrew, AppointmentTypeIds.DrugAndAlcoholTesting),
            Requirement(AttendeeGroupIds.CabinCrew, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.CabinCrew, AppointmentTypeIds.UniformFitting),
            Requirement(AttendeeGroupIds.Pilots, AppointmentTypeIds.DrugAndAlcoholTesting),
            Requirement(AttendeeGroupIds.Pilots, AppointmentTypeIds.UniformFitting),
            Requirement(AttendeeGroupIds.GroundOperationsAgent, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.Engineering, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.GroundTransportServices, AppointmentTypeIds.DrugAndAlcoholTesting),
            Requirement(AttendeeGroupIds.GroundTransportServices, AppointmentTypeIds.MedicalCheckUp),
            Requirement(AttendeeGroupIds.GroundTransportServices, AppointmentTypeIds.UniformFitting));
    }

    private static object Requirement(Guid attendeeGroupId, Guid appointmentTypeId) =>
        new { AttendeeGroupId = attendeeGroupId, AppointmentTypeId = appointmentTypeId };
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs","beforeSha":"cd6a3efedda6ac316895b3d0f972b7837302cacbc33ef44f58d38cd656a84add","afterSha":"45ba8fbd511ad58ee5e738b43ba42ef139714586c9d363664b4ff634b3e8b63e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_log");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.AttendeeId).HasColumnName("attendee_id");
        builder.Property(e => e.TemplateName).HasColumnName("template_name").HasConversion<int>();
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(e => e.InviteId).HasColumnName("invite_id");
        builder.Property(e => e.BookingId).HasColumnName("booking_id");
        builder.Property(e => e.EventId).HasColumnName("event_id");
        builder.Property(e => e.ClaimedAt).HasColumnName("claimed_at");

        builder.HasIndex(e => e.AttendeeId);
        builder.HasIndex(e => new { e.AttendeeId, e.SentAt });
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs","beforeSha":"cd6a3efedda6ac316895b3d0f972b7837302cacbc33ef44f58d38cd656a84add","afterSha":"45ba8fbd511ad58ee5e738b43ba42ef139714586c9d363664b4ff634b3e8b63e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_log");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.AttendeeId).HasColumnName("attendee_id");
        builder.Property(e => e.TemplateName).HasColumnName("template_name").HasConversion<int>();
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(e => e.InviteId).HasColumnName("invite_id");
        builder.Property(e => e.BookingId).HasColumnName("booking_id");
        builder.Property(e => e.EventId).HasColumnName("event_id");
        builder.Property(e => e.ClaimedAt).HasColumnName("claimed_at");
        builder.Property(e => e.ClaimCount).HasColumnName("claim_count");

        builder.HasIndex(e => e.AttendeeId);
        builder.HasIndex(e => new { e.AttendeeId, e.SentAt });
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs","beforeSha":"7512d697cda9a10ec7de18f0bf2294a6f6ca8abeddd55ef3b5b06d91a5e6f48f","afterSha":"c1414617d2e5a13677f3c0a03b8d5a72969c2892a9d33a533b2303126d7dfcff","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventCapacityConfiguration : IEntityTypeConfiguration<EventCapacity>
{
    public void Configure(EntityTypeBuilder<EventCapacity> builder)
    {
        // The table and column names here are written out in raw SQL in Task 47. Changing either
        // one means changing that query in the same commit.
        builder.ToTable("event_capacity");

        builder.HasKey(c => new { c.EventId, c.AppointmentTypeId });

        builder.Property(c => c.EventId).HasColumnName("event_id");
        builder.Property(c => c.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(c => c.TotalHeadcount).HasColumnName("total_headcount");
        builder.Property(c => c.RemainingCapacity).HasColumnName("remaining_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_event_capacity_within_bounds",
            "remaining_capacity >= 0 AND remaining_capacity <= total_headcount"));
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs","beforeSha":"7512d697cda9a10ec7de18f0bf2294a6f6ca8abeddd55ef3b5b06d91a5e6f48f","afterSha":"c1414617d2e5a13677f3c0a03b8d5a72969c2892a9d33a533b2303126d7dfcff","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventCapacityConfiguration : IEntityTypeConfiguration<EventCapacity>
{
    public void Configure(EntityTypeBuilder<EventCapacity> builder)
    {
        // The table and column names here are written out in raw SQL in Task 47. Changing either
        // one means changing that query in the same commit.
        builder.ToTable("event_capacity");

        builder.HasKey(c => new { c.EventId, c.AppointmentTypeId });

        builder.Property(c => c.EventId).HasColumnName("event_id");
        builder.Property(c => c.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(c => c.TotalHeadcount).HasColumnName("total_headcount");
        builder.Property(c => c.RemainingCapacity).HasColumnName("remaining_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_event_capacity_bounds",
            "remaining_capacity >= 0 AND remaining_capacity <= total_headcount AND total_headcount > 0"));
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"71ae27e342883b3d08c50b2bccabba544c19dacc2164160f6c1e9c8363d3e611","afterSha":"9b5ff97642ca9c5d32d53e48f394b7d43eb5227478fd7ed021206f0b39f7fb4c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.LocationId).HasColumnName("location_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"71ae27e342883b3d08c50b2bccabba544c19dacc2164160f6c1e9c8363d3e611","afterSha":"9b5ff97642ca9c5d32d53e48f394b7d43eb5227478fd7ed021206f0b39f7fb4c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.LocationId).HasColumnName("location_id");
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        // A derived persistence column, not domain data (design 04). It exists to index and order
        // the eligibility query, and the domain never reads it as the source of truth; PostgreSQL
        // cannot evaluate IANA rules in a generated column, so the application computes it.
        //
        // Nullable until Task 11, which is where the repository writes it in the same transaction
        // as the insert and makes the column required. A non-nullable column here would take EF's
        // default of 0001-01-01 for every row nothing has computed yet, and the eligibility query
        // filters on start_utc: a wrong instant would quietly hide the event rather than fail.
        builder.Property<DateTimeOffset?>("StartUtc").HasColumnName("start_utc");

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
        builder
            .HasIndex(nameof(Event.Status), nameof(Event.LocationId), "StartUtc")
            .HasDatabaseName("ix_event_eligibility");
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/LocationConfiguration.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/LocationConfiguration.cs","beforeSha":null,"afterSha":"f3e9765bcbe91ab7f9559c95075dec1cd6b738c6cf3509221ad4659ceee81a67","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Events;
using EventBooking.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("location");
        builder.HasKey(location => location.Id);

        builder.Property(location => location.Id).HasColumnName("id");
        builder.Property(location => location.Code)
            .HasColumnName("code")
            .HasMaxLength(Location.MaximumCodeLength)
            .IsRequired();
        builder.Property(location => location.Name)
            .HasColumnName("name")
            .HasMaxLength(Location.MaximumNameLength)
            .IsRequired();
        builder.Property(location => location.Address)
            .HasColumnName("address")
            .HasMaxLength(Location.MaximumAddressLength)
            .IsRequired();

        // The IANA identifier, not an offset: an offset cannot answer "was this in summer time".
        builder.Property(location => location.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(location => location.IsActive).HasColumnName("is_active");
        builder.Property(location => location.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(location => location.Code).IsUnique();

        // The single site every proposal, event and invite is made at until Phase 3, matching the
        // transitional clock's zone. It is seeded here for the same reason the three appointment
        // types are: the rest of the schema references it, and nothing manages locations yet.
        builder.HasData(new
        {
            Id = TransitionalLocation.Id,
            Code = "TRANSITIONAL",
            Name = "Transitional location",
            Address = "Recorded against the transitional site until Phase 3.",
            TimeZoneId = TransitionalLocation.TimeZoneId,
            IsActive = true,
            Version = 1L,
        });
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/DatabaseRoles.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Infrastructure/Persistence/DatabaseRoles.cs","beforeSha":null,"afterSha":"b6a35c4bdcb5f03b906226b9ca660eb6c5862bf1920ac81a719962d5ecfd97f2","side":"after","part":1,"parts":1} -->

`````csharp
using System.Reflection;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// The role script that has to run before migrations, because the initial migration grants to a
/// role it does not create. Shipped as an embedded resource so a deployment cannot apply a copy
/// that has drifted from the schema it guards.
/// </summary>
public static class DatabaseRoles
{
    private const string ResourceName = "EventBooking.Infrastructure.Persistence.Sql.roles.sql";

    private static readonly Lazy<string> Contents = new(() =>
    {
        using var stream = typeof(DatabaseRoles).GetTypeInfo().Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>Gets the role script, verbatim.</summary>
    public static string Script => Contents.Value;
}
`````

## before — src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","beforeSha":"5a3e600cec6a563bf539553c33be38e84c81ef69e6408787877705bd64690882","afterSha":"3a842a313b009d10021ed506ddd977408355bfdd171539c43588cbb0d9a341c0","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

public sealed class EventBookingDbContext(DbContextOptions<EventBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    public DbSet<EventProposal> EventProposals => Set<EventProposal>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventCapacity> EventCapacities => Set<EventCapacity>();

    /// <summary>Gets Attendee Group reference rows and their required Appointment Type mappings.</summary>
    public DbSet<AttendeeGroup> AttendeeGroups => Set<AttendeeGroup>();

    public DbSet<Attendee> Attendees => Set<Attendee>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Gets independently progressing required appointments for persisted bookings.</summary>
    public DbSet<BookingAppointment> BookingAppointments => Set<BookingAppointment>();

    public DbSet<StaffAccessProfile> StaffAccessProfiles => Set<StaffAccessProfile>();

    /// <summary>Gets identity-provider pairs learned from authenticated staff tokens.</summary>
    public DbSet<StaffIdentity> StaffIdentities => Set<StaffIdentity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventBookingDbContext).Assembly);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","beforeSha":"5a3e600cec6a563bf539553c33be38e84c81ef69e6408787877705bd64690882","afterSha":"3a842a313b009d10021ed506ddd977408355bfdd171539c43588cbb0d9a341c0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

public sealed class EventBookingDbContext(DbContextOptions<EventBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    public DbSet<EventProposal> EventProposals => Set<EventProposal>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventCapacity> EventCapacities => Set<EventCapacity>();

    /// <summary>Gets Attendee Group reference rows and their required Appointment Type mappings.</summary>
    public DbSet<Location> Locations => Set<Location>();

    public DbSet<AttendeeGroup> AttendeeGroups => Set<AttendeeGroup>();

    public DbSet<Attendee> Attendees => Set<Attendee>();

    public DbSet<Invite> Invites => Set<Invite>();

    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>Gets independently progressing required appointments for persisted bookings.</summary>
    public DbSet<BookingAppointment> BookingAppointments => Set<BookingAppointment>();

    public DbSet<StaffAccessProfile> StaffAccessProfiles => Set<StaffAccessProfile>();

    /// <summary>Gets identity-provider pairs learned from authenticated staff tokens.</summary>
    public DbSet<StaffIdentity> StaffIdentities => Set<StaffIdentity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventBookingDbContext).Assembly);
    }
}
`````
