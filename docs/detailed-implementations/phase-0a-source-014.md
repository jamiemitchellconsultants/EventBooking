# 00a — Port source 14 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj","encoding":"utf8","sha256":"8ad942183f3244a8a66e0ea0bac2a8ef10940a683ccf22dba8eb4cefaa181d84","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
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

## src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs","encoding":"utf8","sha256":"5ddad5a3cac85b467e9aa59fe64ded6e3bc55a8845da10a95deae02e9e951b0a","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AppointmentTypeConfiguration : IEntityTypeConfiguration<AppointmentType>
{
    public void Configure(EntityTypeBuilder<AppointmentType> builder)
    {
        builder.ToTable("appointment_type");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(8).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();

        // The 3 types are fixed by the spec, so they are seeded rather than created at run time.
        builder.HasData(
            new { Id = AppointmentTypeIds.DrugAndAlcoholTesting, Code = "DAT", Name = "Drug & Alcohol Testing" },
            new { Id = AppointmentTypeIds.MedicalCheckUp, Code = "MED", Name = "Medical Check-up" },
            new { Id = AppointmentTypeIds.UniformFitting, Code = "UNI", Name = "Uniform Fitting" });
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs","encoding":"utf8","sha256":"876b0eda93b1d537c36361ced116120cddc28cbe2b287a00648440e9f31b97e5","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id");
        builder.Property(a => a.Action).HasColumnName("action").HasConversion<int>();
        builder.Property(a => a.ActorType).HasColumnName("actor_type").HasConversion<int>();
        builder.Property(a => a.ActorId).HasColumnName("actor_id").HasMaxLength(100);
        builder.Property(a => a.Timestamp).HasColumnName("timestamp");
        builder.Property(a => a.Details).HasColumnName("details").HasMaxLength(1000);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => new { a.Timestamp, a.Id })
            .HasDatabaseName("ix_audit_log_timestamp")
            .IsDescending(true, true);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/BookingAppointmentConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/BookingAppointmentConfiguration.cs","encoding":"utf8","sha256":"e0ece83c6a01cef77dd18feecd34e0a103a770e6a0ce4d8fab0319d3d7b27872","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps booking-appointment identity, lifecycle fields, constraints, and indexes.</summary>
public sealed class BookingAppointmentConfiguration : IEntityTypeConfiguration<BookingAppointment>
{
    /// <summary>Configures booking-appointment columns, constraints, and indexes.</summary>
    /// <param name="builder">The entity-type builder.</param>
    public void Configure(EntityTypeBuilder<BookingAppointment> builder)
    {
        builder.ToTable("booking_appointment", table =>
        {
            table.HasCheckConstraint("CK_booking_appointment_version", "version > 0");
            table.HasCheckConstraint(
                "CK_booking_appointment_last_change_pair",
                "(last_changed_by_staff_user_id IS NULL) = (last_changed_at IS NULL)");
            table.HasCheckConstraint(
                "CK_booking_appointment_status_timestamps",
                """
                (status = 1 AND checked_in_at IS NULL AND outcome_at IS NULL)
                OR (status = 2 AND checked_in_at IS NOT NULL AND outcome_at IS NULL)
                OR (status = 3 AND checked_in_at IS NOT NULL AND outcome_at IS NOT NULL
                    AND outcome_at >= checked_in_at)
                OR (status = 4 AND checked_in_at IS NULL AND outcome_at IS NOT NULL)
                """);
        });

        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id");
        builder.Property(value => value.BookingId).HasColumnName("booking_id");
        builder.Property(value => value.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(value => value.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(value => value.CheckedInAt).HasColumnName("checked_in_at");
        builder.Property(value => value.OutcomeAt).HasColumnName("outcome_at");
        builder.Property(value => value.LastChangedByStaffUserId)
            .HasColumnName("last_changed_by_staff_user_id");
        builder.Property(value => value.LastChangedAt).HasColumnName("last_changed_at");
        builder.Property(value => value.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(value => value.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(value => value.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(value => new { value.BookingId, value.AppointmentTypeId }).IsUnique();
        builder.HasIndex(value => new { value.AppointmentTypeId, value.Status });
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs","encoding":"utf8","sha256":"59cb0f90f0a4ca23a0fd09b0b51b7a10f7fe36fdfa014defbe88f96b76e5d5c1","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps booking persistence and its one-active-booking-per-candidate backstop.</summary>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    /// <summary>Configures booking columns and the filtered active-booking index.</summary>
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable(
            "booking",
            table => table.HasCheckConstraint(
                "ck_booking_no_self_recovery",
                "recovery_of_booking_id IS NULL OR recovery_of_booking_id <> id"));
        builder.HasKey(b => b.Id);
        builder.Ignore(b => b.IsOriginal);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.CandidateId).HasColumnName("candidate_id");
        builder.Property(b => b.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(b => b.InviteId).HasColumnName("invite_id");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(b => b.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");
        builder
            .Property(b => b.ManageTokenHash)
            .HasColumnName("manage_token_hash")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(b => b.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.ManageTokenHash).IsUnique();
        builder.HasIndex(b => new { b.ConfirmedSlotId, b.Status });
        builder.HasIndex(b => new { b.CandidateId, b.Status });
        builder.HasIndex(b => b.RecoveryOfBookingId);
        builder.HasIndex(b => b.CandidateId)
            .HasDatabaseName("ux_booking_active_original_candidate")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NULL")
            .IsUnique();
        builder.HasIndex(b => b.RecoveryOfBookingId)
            .HasDatabaseName("ux_booking_active_recovery")
            .HasFilter("status = 1 AND recovery_of_booking_id IS NOT NULL")
            .IsUnique();
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs","encoding":"utf8","sha256":"93b49d2c01202cc457447fa44ea194cc02a2e65895385ad30c252935bd18c1ad","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("candidate");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(c => c.EmployeeGroupId).HasColumnName("employee_group_id").IsRequired();
        builder.HasOne<EmployeeGroup>()
            .WithMany()
            .HasForeignKey(c => c.EmployeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.EmployeeGroupId);
        builder.Property<DateTimeOffset>(StatusStampingInterceptor.ShadowProperty)
            .HasColumnName("status_changed_at");

        builder.Ignore(c => c.RequiredAppointmentTypeIds);

        builder
            .HasMany(c => c.Requirements)
            .WithOne()
            .HasForeignKey(r => r.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(c => c.Email).IsUnique();
        builder.HasIndex(c => c.Status);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs","encoding":"utf8","sha256":"1a3b8ac5bc422437960029faa6b1ab679fb1faab03303c52c8ba85d40a5ebfc7","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class CandidateRequirementConfiguration : IEntityTypeConfiguration<CandidateRequirement>
{
    public void Configure(EntityTypeBuilder<CandidateRequirement> builder)
    {
        builder.ToTable("candidate_requirement");
        builder.HasKey(r => new { r.CandidateId, r.AppointmentTypeId });

        builder.Property(r => r.CandidateId).HasColumnName("candidate_id");
        builder.Property(r => r.AppointmentTypeId).HasColumnName("appointment_type_id");
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs","encoding":"utf8","sha256":"35deded7339cc21afaa0eeed73e9d7eb080f1abb8e7c9139a53baac8dd22f953","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class ConfirmedSlotConfiguration : IEntityTypeConfiguration<ConfirmedSlot>
{
    public void Configure(EntityTypeBuilder<ConfirmedSlot> builder)
    {
        builder.ToTable("confirmed_slot");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired(false);
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.ConfirmedSlotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs","encoding":"utf8","sha256":"8ca10e21ff56a5a74fbb07892c335b0055f525f0feb13f4ee6b6926839423c10","parts":1,"part":1} -->

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
        builder.Property(e => e.CandidateId).HasColumnName("candidate_id");
        builder.Property(e => e.TemplateName).HasColumnName("template_name").HasConversion<int>();
        builder.Property(e => e.SentAt).HasColumnName("sent_at");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(e => e.InviteId).HasColumnName("invite_id");
        builder.Property(e => e.BookingId).HasColumnName("booking_id");
        builder.Property(e => e.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(e => e.ClaimedAt).HasColumnName("claimed_at");

        builder.HasIndex(e => e.CandidateId);
        builder.HasIndex(e => new { e.CandidateId, e.SentAt });
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs","encoding":"utf8","sha256":"4b95b027cb68a71069836718f2df3ef4f6da90d64ce269bad5e6ad8b38d44fc4","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the change-controlled Employee Group reference table.</summary>
public sealed class EmployeeGroupConfiguration : IEntityTypeConfiguration<EmployeeGroup>
{
    /// <summary>Configures the employee group table, constraints, and mapping collection.</summary>
    public void Configure(EntityTypeBuilder<EmployeeGroup> builder)
    {
        builder.ToTable("employee_group", table =>
        {
            table.HasCheckConstraint("ck_employee_group_code_nonblank", "code <> ''");
            table.HasCheckConstraint("ck_employee_group_name_nonblank", "name <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(group => group.IsActive).HasColumnName("is_active");

        builder.Ignore(group => group.RequiredAppointmentTypeIds);

        builder
            .HasMany(group => group.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.EmployeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.Code).IsUnique();
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs","encoding":"utf8","sha256":"9d341648a65dc9defbcec729e8c369a8fb61f3aca46459ff4c01c6cde6ca3487","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one Employee Group to one fixed Appointment Type.</summary>
public sealed class EmployeeGroupRequirementConfiguration : IEntityTypeConfiguration<EmployeeGroupRequirement>
{
    /// <summary>Configures the composite mapping key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<EmployeeGroupRequirement> builder)
    {
        builder.ToTable("employee_group_requirement");
        builder.HasKey(requirement => new { requirement.EmployeeGroupId, requirement.AppointmentTypeId });

        builder.Property(requirement => requirement.EmployeeGroupId).HasColumnName("employee_group_id");
        builder.Property(requirement => requirement.AppointmentTypeId).HasColumnName("appointment_type_id");

        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(requirement => requirement.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs","encoding":"utf8","sha256":"f91ee2066df9d1c1ab54e4ac7cf2676d3751a5887f7e721ef573e1102a175b25","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps invite persistence and its one-pending-invite-per-candidate backstop.</summary>
public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    /// <summary>Configures invite columns, options, and the filtered pending-invite index.</summary>
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invite");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.CandidateId).HasColumnName("candidate_id");
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(i => i.RetryCount).HasColumnName("retry_count");
        builder.Property(i => i.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");

        builder.Ignore(i => i.OfferedSlotIds);
        builder.Ignore(i => i.RequiredAppointmentTypeIds);

        builder
            .HasMany(i => i.Options)
            .WithOne()
            .HasForeignKey(o => o.InviteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Options).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(i => i.Requirements)
            .WithOne()
            .HasForeignKey(r => r.InviteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(i => i.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(i => i.RecoveryOfBookingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.RecoveryOfBookingId);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.Status, i.ExpiresAt });
        builder.HasIndex(i => i.CandidateId);
        builder.HasIndex(i => i.CandidateId)
            .HasDatabaseName("ux_invite_pending_candidate")
            .HasFilter("status = 1")
            .IsUnique();
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs","encoding":"utf8","sha256":"7079bc5f3c984b05be96f8de40802e85245781c4fc8853c30e844e7e1576d22c","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class InviteOptionConfiguration : IEntityTypeConfiguration<InviteOption>
{
    public void Configure(EntityTypeBuilder<InviteOption> builder)
    {
        builder.ToTable("invite_option");
        builder.HasKey(o => new { o.InviteId, o.ConfirmedSlotId });

        builder.Property(o => o.InviteId).HasColumnName("invite_id");
        builder.Property(o => o.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/InviteRequirementConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteRequirementConfiguration.cs","encoding":"utf8","sha256":"197f272a67aff3b0a806b884ec79c5498fb0de7c4d4b56eaaba6a961e3bc0645","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps one snapshotted Appointment Type owned by an Invite.</summary>
public sealed class InviteRequirementConfiguration : IEntityTypeConfiguration<InviteRequirement>
{
    /// <summary>Configures the composite snapshot key and restrictive reference keys.</summary>
    public void Configure(EntityTypeBuilder<InviteRequirement> builder)
    {
        builder.ToTable("invite_requirement");
        builder.HasKey(requirement => new { requirement.InviteId, requirement.AppointmentTypeId });

        builder.Property(requirement => requirement.InviteId).HasColumnName("invite_id");
        builder.Property(requirement => requirement.AppointmentTypeId).HasColumnName("appointment_type_id");

        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(requirement => requirement.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs","encoding":"utf8","sha256":"f123f9f4a78db87befbb2de6bc6613fa11b9456ce3e8b46a7c4b63f23b1f5671","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class ProposalAcceptanceConfiguration : IEntityTypeConfiguration<ProposalAcceptance>
{
    public void Configure(EntityTypeBuilder<ProposalAcceptance> builder)
    {
        builder.ToTable("proposal_acceptance");

        // One acceptance per appointment type per proposal — the composite key is the invariant.
        builder.HasKey(a => new { a.ProposalId, a.AppointmentTypeId });

        builder.Property(a => a.ProposalId).HasColumnName("proposal_id");
        builder.Property(a => a.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(a => a.ManagerUserId).HasColumnName("manager_user_id");
        builder.Property(a => a.Headcount).HasColumnName("headcount");
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs","encoding":"utf8","sha256":"613fe8560507f2341172c2751cb326ddef8a1265802abc3ce8dcf7cd33d4c932","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class SlotCapacityConfiguration : IEntityTypeConfiguration<SlotCapacity>
{
    public void Configure(EntityTypeBuilder<SlotCapacity> builder)
    {
        // The table and column names here are written out in raw SQL in Task 47. Changing either
        // one means changing that query in the same commit.
        builder.ToTable("slot_capacity");

        builder.HasKey(c => new { c.ConfirmedSlotId, c.AppointmentTypeId });

        builder.Property(c => c.ConfirmedSlotId).HasColumnName("confirmed_slot_id");
        builder.Property(c => c.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(c => c.TotalHeadcount).HasColumnName("total_headcount");
        builder.Property(c => c.RemainingCapacity).HasColumnName("remaining_capacity");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_slot_capacity_within_bounds",
            "remaining_capacity >= 0 AND remaining_capacity <= total_headcount"));
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs","encoding":"utf8","sha256":"9f470f087fdd1bdcfb8163eec81215b01438b8a0845ef34d586da17137e8b0b8","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps proposal persistence and its one-open-window uniqueness backstop.</summary>
public sealed class SlotProposalConfiguration : IEntityTypeConfiguration<SlotProposal>
{
    /// <summary>Configures proposal columns, acceptances, and the filtered window index.</summary>
    public void Configure(EntityTypeBuilder<SlotProposal> builder)
    {
        builder.ToTable("slot_proposal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(p => p.CreatedByManagerUserId).HasColumnName("created_by_manager_user_id");

        // The 4-hour window lives in this table's own date and start_time columns.
        builder.OwnsOne(p => p.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Ignore(w => w.EndTime);
            window.HasIndex(item => new { item.Date, item.StartTime })
                .HasDatabaseName("ux_slot_proposal_open_window")
                .HasFilter("status = 1")
                .IsUnique();
        });
        builder.Navigation(p => p.Window).IsRequired();

        builder
            .HasMany(p => p.Acceptances)
            .WithOne()
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Acceptances).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.Status);

    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/StaffAccessProfileConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/StaffAccessProfileConfiguration.cs","encoding":"utf8","sha256":"12adcf3e9d9d4d421fb76284d55337458c6c120aa0ea998a71ae70a65bc077f8","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class StaffAccessProfileConfiguration : IEntityTypeConfiguration<StaffAccessProfile>
{
    public void Configure(EntityTypeBuilder<StaffAccessProfile> builder)
    {
        builder.ToTable("staff_access_profile", table =>
        {
            table.HasCheckConstraint(
                "ck_staff_access_profile_has_role",
                "is_admin OR is_coordinator OR is_manager OR is_appointment_staff");
            table.HasCheckConstraint(
                "ck_staff_access_profile_admin_exclusive",
                "NOT is_admin OR (NOT is_coordinator AND NOT is_manager "
                + "AND NOT is_appointment_staff AND appointment_type_id IS NULL)");
            table.HasCheckConstraint(
                "ck_staff_access_profile_scope",
                "appointment_type_id IS NULL OR (is_manager OR is_appointment_staff)");
            table.HasCheckConstraint("ck_staff_access_profile_version", "version > 0");
        });

        builder.HasKey(profile => profile.StaffUserId);
        builder.Property(profile => profile.StaffUserId)
            .HasColumnName("staff_user_id")
            .ValueGeneratedNever();
        builder.Property(profile => profile.IsAdmin).HasColumnName("is_admin");
        builder.Property(profile => profile.IsCoordinator).HasColumnName("is_coordinator");
        builder.Property(profile => profile.IsManager).HasColumnName("is_manager");
        builder.Property(profile => profile.IsAppointmentStaff).HasColumnName("is_appointment_staff");
        builder.Property(profile => profile.AppointmentTypeId).HasColumnName("appointment_type_id");
        builder.Property(profile => profile.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

        builder.Ignore(profile => profile.Roles);
        builder.HasOne<AppointmentType>()
            .WithMany()
            .HasForeignKey(profile => profile.AppointmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(profile => profile.AppointmentTypeId)
            .IsUnique()
            .HasFilter("is_manager")
            .HasDatabaseName("ux_staff_access_profile_manager_appointment_type");
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs","encoding":"utf8","sha256":"682ea88e3b414dbf02c4453491b47ac2a685da7ec3869c9af98070f73c38517c","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the identity-provider mirror and its format and uniqueness backstops.</summary>
public sealed class StaffIdentityConfiguration : IEntityTypeConfiguration<StaffIdentity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<StaffIdentity> builder)
    {
        builder.ToTable("staff_identity", table => table.HasCheckConstraint(
            "ck_staff_identity_format",
            "staff_id ~* '^[UN][0-9]{6}$'"));

        builder.HasKey(identity => identity.StaffUserId);
        builder.Property(identity => identity.StaffUserId)
            .HasColumnName("staff_user_id")
            .ValueGeneratedNever();
        builder.Property(identity => identity.StaffId)
            .HasConversion(staffId => staffId.Value, value => new StaffId(value))
            .HasColumnName("staff_id")
            .HasColumnType("character(7)")
            .IsRequired();
        builder.Property(identity => identity.LastSeenAt)
            .HasColumnName("last_seen_at")
            .IsRequired();
        // No check constraint: unlike the staff number, a provider-observed name has no format.
        builder.Property(identity => identity.DisplayName)
            .HasColumnName("display_name")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasIndex(identity => identity.StaffId)
            .IsUnique()
            .HasDatabaseName("ux_staff_identity_staff_id");
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs","encoding":"utf8","sha256":"438722e621190d431d430512c823ee9d72a28ba7e36542ddb6424123184e2cbf","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.InviteExpiryDays).HasColumnName("invite_expiry_days");
        builder.Property(s => s.MaxAutoRetryCount).HasColumnName("max_auto_retry_count");

        builder.HasData(
            new { Id = Domain.Settings.SystemSettings.SingletonId, InviteExpiryDays = 4, MaxAutoRetryCount = 2 });
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/DesignTimeDbContextFactory.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/DesignTimeDbContextFactory.cs","encoding":"utf8","sha256":"a7ccf80c82a4b7bdaeb26d2044c503bca1edc11c64d80ddf2c0c8a46e026c01e","parts":1,"part":1} -->

`````csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EventBooking.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tooling, so migrations can be generated from the infrastructure
/// project alone without starting the API. The connection string here is never used at run time.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EventBookingDbContext>
{
    public EventBookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql("Host=localhost;Database=eventbooking_design_time;Username=postgres;Password=postgres")
            .Options;

        return new EventBookingDbContext(options);
    }
}
`````

## src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","encoding":"utf8","sha256":"502acd2ee698d2af35911bb330135762de4200689d5a1636935c792bc71cadc5","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Slots;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence;

public sealed class EventBookingDbContext(DbContextOptions<EventBookingDbContext> options)
    : DbContext(options)
{
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();

    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    public DbSet<SlotProposal> SlotProposals => Set<SlotProposal>();

    public DbSet<ConfirmedSlot> ConfirmedSlots => Set<ConfirmedSlot>();

    public DbSet<SlotCapacity> SlotCapacities => Set<SlotCapacity>();

    /// <summary>Gets Employee Group reference rows and their required Appointment Type mappings.</summary>
    public DbSet<EmployeeGroup> EmployeeGroups => Set<EmployeeGroup>();

    public DbSet<Candidate> Candidates => Set<Candidate>();

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

## src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs","encoding":"utf8","sha256":"9c50ce78fd67720d12afe4678ed036b6bf4cc1d8504fc5e42b6a06849ce9e629","parts":1,"part":1} -->

`````csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointment_type",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointment_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    actor_type = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    manage_token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "confirmed_slot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_confirmed_slot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_name = table.Column<int>(type: "integer", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invite",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "slot_proposal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by_manager_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slot_proposal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    invite_expiry_days = table.Column<int>(type: "integer", nullable: false),
                    max_auto_retry_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_role_assignment",
                columns: table => new
                {
                    entra_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_assignment", x => x.entra_object_id);
                });

            migrationBuilder.CreateTable(
                name: "candidate_requirement",
                columns: table => new
                {
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_requirement", x => new { x.candidate_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_candidate_requirement_candidate_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidate",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "slot_capacity",
                columns: table => new
                {
                    confirmed_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_headcount = table.Column<int>(type: "integer", nullable: false),
                    remaining_capacity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slot_capacity", x => new { x.confirmed_slot_id, x.appointment_type_id });
                    table.CheckConstraint("ck_slot_capacity_within_bounds", "remaining_capacity >= 0 AND remaining_capacity <= total_headcount");
                    table.ForeignKey(
                        name: "FK_slot_capacity_confirmed_slot_confirmed_slot_id",
                        column: x => x.confirmed_slot_id,
                        principalTable: "confirmed_slot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invite_option",
                columns: table => new
                {
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_slot_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invite_option", x => new { x.invite_id, x.confirmed_slot_id });
                    table.ForeignKey(
                        name: "FK_invite_option_invite_invite_id",
                        column: x => x.invite_id,
                        principalTable: "invite",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proposal_acceptance",
                columns: table => new
                {
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headcount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proposal_acceptance", x => new { x.proposal_id, x.appointment_type_id });
                    table.ForeignKey(
                        name: "FK_proposal_acceptance_slot_proposal_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "slot_proposal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "appointment_type",
                columns: new[] { "id", "code", "manager_user_id", "name" },
                values: new object[,]
                {
                    { new Guid("a0000001-0000-0000-0000-000000000001"), "DAT", null, "Drug & Alcohol Testing" },
                    { new Guid("a0000002-0000-0000-0000-000000000002"), "MED", null, "Medical Check-up" },
                    { new Guid("a0000003-0000-0000-0000-000000000003"), "UNI", null, "Uniform Fitting" }
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "id", "invite_expiry_days", "max_auto_retry_count" },
                values: new object[] { 1, 4, 2 });

            migrationBuilder.CreateIndex(
                name: "IX_appointment_type_code",
                table: "appointment_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entity_type_entity_id",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_candidate_id_status",
                table: "booking",
                columns: new[] { "candidate_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_confirmed_slot_id_status",
                table: "booking",
                columns: new[] { "confirmed_slot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_manage_token_hash",
                table: "booking",
                column: "manage_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_email",
                table: "candidate",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_status",
                table: "candidate",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_confirmed_slot_proposal_id",
                table: "confirmed_slot",
                column: "proposal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_confirmed_slot_status",
                table: "confirmed_slot",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_email_log_candidate_id",
                table: "email_log",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_invite_candidate_id",
                table: "invite",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_invite_status_expires_at",
                table: "invite",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_invite_token_hash",
                table: "invite",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_slot_proposal_status",
                table: "slot_proposal",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_type");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "booking");

            migrationBuilder.DropTable(
                name: "candidate_requirement");

            migrationBuilder.DropTable(
                name: "email_log");

            migrationBuilder.DropTable(
                name: "invite_option");

            migrationBuilder.DropTable(
                name: "proposal_acceptance");

            migrationBuilder.DropTable(
                name: "slot_capacity");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "user_role_assignment");

            migrationBuilder.DropTable(
                name: "candidate");

            migrationBuilder.DropTable(
                name: "invite");

            migrationBuilder.DropTable(
                name: "slot_proposal");

            migrationBuilder.DropTable(
                name: "confirmed_slot");
        }
    }
}
`````
