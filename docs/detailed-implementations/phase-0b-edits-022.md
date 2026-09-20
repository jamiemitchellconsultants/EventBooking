# 00b — Vocabulary edits 22 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":126,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs","beforeSha":"8ca10e21ff56a5a74fbb07892c335b0055f525f0feb13f4ee6b6926839423c10","afterSha":"cd6a3efedda6ac316895b3d0f972b7837302cacbc33ef44f58d38cd656a84add","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":127,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs","beforeSha":"4b95b027cb68a71069836718f2df3ef4f6da90d64ce269bad5e6ad8b38d44fc4","afterSha":"2b176f65240554ca3b3557d18ccb9537b5528f4d2d2e01fe8bb25eb0837f53c2","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":127,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs","beforeSha":"4b95b027cb68a71069836718f2df3ef4f6da90d64ce269bad5e6ad8b38d44fc4","afterSha":"2b176f65240554ca3b3557d18ccb9537b5528f4d2d2e01fe8bb25eb0837f53c2","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":128,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs","beforeSha":"9d341648a65dc9defbcec729e8c369a8fb61f3aca46459ff4c01c6cde6ca3487","afterSha":"a1816404f5702e90c2cb076fb2c4c4ec2f876f6d52ae22126a95b9d3150cff9b","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":128,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs","beforeSha":"9d341648a65dc9defbcec729e8c369a8fb61f3aca46459ff4c01c6cde6ca3487","afterSha":"a1816404f5702e90c2cb076fb2c4c4ec2f876f6d52ae22126a95b9d3150cff9b","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":129,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs","beforeSha":"f91ee2066df9d1c1ab54e4ac7cf2676d3751a5887f7e721ef573e1102a175b25","afterSha":"5953576f7ed4becc505fed3a42765d1d410fe4a26b1258c7ec3f3d30c94bede0","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":129,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs","beforeSha":"f91ee2066df9d1c1ab54e4ac7cf2676d3751a5887f7e721ef573e1102a175b25","afterSha":"5953576f7ed4becc505fed3a42765d1d410fe4a26b1258c7ec3f3d30c94bede0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps invite persistence and its one-pending-invite-per-attendee backstop.</summary>
public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    /// <summary>Configures invite columns, options, and the filtered pending-invite index.</summary>
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invite");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.AttendeeId).HasColumnName("attendee_id");
        builder.Property(i => i.TokenHash).HasColumnName("token_hash").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(i => i.RetryCount).HasColumnName("retry_count");
        builder.Property(i => i.RecoveryOfBookingId).HasColumnName("recovery_of_booking_id");

        builder.Ignore(i => i.OfferedEventIds);
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
        builder.HasIndex(i => i.AttendeeId);
        builder.HasIndex(i => i.AttendeeId)
            .HasDatabaseName("ux_invite_pending_attendee")
            .HasFilter("status = 1")
            .IsUnique();
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":130,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs","beforeSha":"7079bc5f3c984b05be96f8de40802e85245781c4fc8853c30e844e7e1576d22c","afterSha":"5f3717532cce51ff7421de724c49ec54ce90d872ff94075d968b84d219b23e2d","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":130,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs","beforeSha":"7079bc5f3c984b05be96f8de40802e85245781c4fc8853c30e844e7e1576d22c","afterSha":"5f3717532cce51ff7421de724c49ec54ce90d872ff94075d968b84d219b23e2d","side":"after","part":1,"parts":1} -->

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
        builder.HasKey(o => new { o.InviteId, o.EventId });

        builder.Property(o => o.InviteId).HasColumnName("invite_id");
        builder.Property(o => o.EventId).HasColumnName("event_id");
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":131,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs","beforeSha":"f123f9f4a78db87befbb2de6bc6613fa11b9456ce3e8b46a7c4b63f23b1f5671","afterSha":"77ac728a350ba0e06005fbeb4b081d2819be5cb800a91c764927a1b4ebd2eb4d","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":131,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs","beforeSha":"f123f9f4a78db87befbb2de6bc6613fa11b9456ce3e8b46a7c4b63f23b1f5671","afterSha":"77ac728a350ba0e06005fbeb4b081d2819be5cb800a91c764927a1b4ebd2eb4d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
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

## before — src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":132,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs","beforeSha":"613fe8560507f2341172c2751cb326ddef8a1265802abc3ce8dcf7cd33d4c932","afterSha":"7512d697cda9a10ec7de18f0bf2294a6f6ca8abeddd55ef3b5b06d91a5e6f48f","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":132,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs","beforeSha":"613fe8560507f2341172c2751cb326ddef8a1265802abc3ce8dcf7cd33d4c932","afterSha":"7512d697cda9a10ec7de18f0bf2294a6f6ca8abeddd55ef3b5b06d91a5e6f48f","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":133,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs","beforeSha":"9f470f087fdd1bdcfb8163eec81215b01438b8a0845ef34d586da17137e8b0b8","afterSha":"1dc59b9c4b48c91c238a20bb095708ab2f2564b6e38c1e8e3ad3d709a58d34ad","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs — 1/1

<!-- vocabulary-file: {"id":133,"oldPath":"src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs","beforeSha":"9f470f087fdd1bdcfb8163eec81215b01438b8a0845ef34d586da17137e8b0b8","afterSha":"1dc59b9c4b48c91c238a20bb095708ab2f2564b6e38c1e8e3ad3d709a58d34ad","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps proposal persistence and its one-open-window uniqueness backstop.</summary>
public sealed class EventProposalConfiguration : IEntityTypeConfiguration<EventProposal>
{
    /// <summary>Configures proposal columns, acceptances, and the filtered window index.</summary>
    public void Configure(EntityTypeBuilder<EventProposal> builder)
    {
        builder.ToTable("event_proposal");
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
                .HasDatabaseName("ux_event_proposal_open_window")
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

## before — src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — 1/1

<!-- vocabulary-file: {"id":134,"oldPath":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","newPath":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","beforeSha":"502acd2ee698d2af35911bb330135762de4200689d5a1636935c792bc71cadc5","afterSha":"5a3e600cec6a563bf539553c33be38e84c81ef69e6408787877705bd64690882","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs — 1/1

<!-- vocabulary-file: {"id":134,"oldPath":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","newPath":"src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs","beforeSha":"502acd2ee698d2af35911bb330135762de4200689d5a1636935c792bc71cadc5","afterSha":"5a3e600cec6a563bf539553c33be38e84c81ef69e6408787877705bd64690882","side":"after","part":1,"parts":1} -->

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

## before — src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs — 1/1

<!-- vocabulary-file: {"id":135,"oldPath":"src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs","newPath":"src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs","beforeSha":"3e39fd5a039c71c6edd9e1b5483f7ccc8f8803d89facceace0b9e4e44b8ca78f","afterSha":"e2941138c3e1cfca5fac0673155c06d0d2f26d7343b6de16fad50027c880d88a","side":"before","part":1,"parts":1} -->

`````csharp
// <auto-generated />
using System;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(EventBookingDbContext))]
    [Migration("20260905060413_InitialSchema")]
    partial class InitialSchema
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("EventBooking.Domain.Access.UserRoleAssignment", b =>
                {
                    b.Property<Guid>("EntraObjectId")
                        .HasColumnType("uuid")
                        .HasColumnName("entra_object_id");

                    b.Property<Guid?>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.Property<int>("Role")
                        .HasColumnType("integer")
                        .HasColumnName("role");

                    b.HasKey("EntraObjectId");

                    b.ToTable("user_role_assignment", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.AppointmentTypes.AppointmentType", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(8)
                        .HasColumnType("character varying(8)")
                        .HasColumnName("code");

                    b.Property<Guid?>("ManagerUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("manager_user_id");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("name");

                    b.HasKey("Id");

                    b.HasIndex("Code")
                        .IsUnique();

                    b.ToTable("appointment_type", (string)null);

                    b.HasData(
                        new
                        {
                            Id = new Guid("a0000001-0000-0000-0000-000000000001"),
                            Code = "DAT",
                            Name = "Drug & Alcohol Testing"
                        },
                        new
                        {
                            Id = new Guid("a0000002-0000-0000-0000-000000000002"),
                            Code = "MED",
                            Name = "Medical Check-up"
                        },
                        new
                        {
                            Id = new Guid("a0000003-0000-0000-0000-000000000003"),
                            Code = "UNI",
                            Name = "Uniform Fitting"
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.Audit.AuditLog", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("Action")
                        .HasColumnType("integer")
                        .HasColumnName("action");

                    b.Property<string>("ActorId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("actor_id");

                    b.Property<int>("ActorType")
                        .HasColumnType("integer")
                        .HasColumnName("actor_type");

                    b.Property<string>("Details")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("details");

                    b.Property<Guid>("EntityId")
                        .HasColumnType("uuid")
                        .HasColumnName("entity_id");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)")
                        .HasColumnName("entity_type");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("timestamp");

                    b.HasKey("Id");

                    b.HasIndex("EntityType", "EntityId");

                    b.ToTable("audit_log", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Bookings.Booking", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("CandidateId")
                        .HasColumnType("uuid")
                        .HasColumnName("candidate_id");

                    b.Property<Guid>("ConfirmedSlotId")
                        .HasColumnType("uuid")
                        .HasColumnName("confirmed_slot_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<Guid>("InviteId")
                        .HasColumnType("uuid")
                        .HasColumnName("invite_id");

                    b.Property<string>("ManageTokenHash")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("manage_token_hash");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.HasKey("Id");

                    b.HasIndex("ManageTokenHash")
                        .IsUnique();

                    b.HasIndex("CandidateId", "Status");

                    b.HasIndex("ConfirmedSlotId", "Status");

                    b.ToTable("booking", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Candidates.Candidate", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(320)
                        .HasColumnType("character varying(320)")
                        .HasColumnName("email");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.HasKey("Id");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.HasIndex("Status");

                    b.ToTable("candidate", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Candidates.CandidateRequirement", b =>
                {
                    b.Property<Guid>("CandidateId")
                        .HasColumnType("uuid")
                        .HasColumnName("candidate_id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.HasKey("CandidateId", "AppointmentTypeId");

                    b.ToTable("candidate_requirement", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.Invite", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("CandidateId")
                        .HasColumnType("uuid")
                        .HasColumnName("candidate_id");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<int>("RetryCount")
                        .HasColumnType("integer")
                        .HasColumnName("retry_count");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("token_hash");

                    b.HasKey("Id");

                    b.HasIndex("CandidateId");

                    b.HasIndex("TokenHash")
                        .IsUnique();

                    b.HasIndex("Status", "ExpiresAt");

                    b.ToTable("invite", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.InviteOption", b =>
                {
                    b.Property<Guid>("InviteId")
                        .HasColumnType("uuid")
                        .HasColumnName("invite_id");

                    b.Property<Guid>("ConfirmedSlotId")
                        .HasColumnType("uuid")
                        .HasColumnName("confirmed_slot_id");

                    b.HasKey("InviteId", "ConfirmedSlotId");

                    b.ToTable("invite_option", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Notifications.EmailLog", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("CandidateId")
                        .HasColumnType("uuid")
                        .HasColumnName("candidate_id");

                    b.Property<DateTimeOffset>("SentAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("sent_at");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<int>("TemplateName")
                        .HasColumnType("integer")
                        .HasColumnName("template_name");

                    b.HasKey("Id");

                    b.HasIndex("CandidateId");

                    b.ToTable("email_log", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Settings.SystemSettings", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    b.Property<int>("InviteExpiryDays")
                        .HasColumnType("integer")
                        .HasColumnName("invite_expiry_days");

                    b.Property<int>("MaxAutoRetryCount")
                        .HasColumnType("integer")
                        .HasColumnName("max_auto_retry_count");

                    b.HasKey("Id");

                    b.ToTable("system_settings", (string)null);

                    b.HasData(
                        new
                        {
                            Id = 1,
                            InviteExpiryDays = 4,
                            MaxAutoRetryCount = 2
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.ConfirmedSlot", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("ProposalId")
                        .HasColumnType("uuid")
                        .HasColumnName("proposal_id");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.HasKey("Id");

                    b.HasIndex("ProposalId")
                        .IsUnique();

                    b.HasIndex("Status");

                    b.ToTable("confirmed_slot", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.ProposalAcceptance", b =>
                {
                    b.Property<Guid>("ProposalId")
                        .HasColumnType("uuid")
                        .HasColumnName("proposal_id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.Property<int>("Headcount")
                        .HasColumnType("integer")
                        .HasColumnName("headcount");

                    b.Property<Guid>("ManagerUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("manager_user_id");

                    b.HasKey("ProposalId", "AppointmentTypeId");

                    b.ToTable("proposal_acceptance", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.SlotCapacity", b =>
                {
                    b.Property<Guid>("ConfirmedSlotId")
                        .HasColumnType("uuid")
                        .HasColumnName("confirmed_slot_id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.Property<int>("RemainingCapacity")
                        .HasColumnType("integer")
                        .HasColumnName("remaining_capacity");

                    b.Property<int>("TotalHeadcount")
                        .HasColumnType("integer")
                        .HasColumnName("total_headcount");

                    b.HasKey("ConfirmedSlotId", "AppointmentTypeId");

                    b.ToTable("slot_capacity", null, t =>
                        {
                            t.HasCheckConstraint("ck_slot_capacity_within_bounds", "remaining_capacity >= 0 AND remaining_capacity <= total_headcount");
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.SlotProposal", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("CreatedByManagerUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("created_by_manager_user_id");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.HasKey("Id");

                    b.HasIndex("Status");

                    b.ToTable("slot_proposal", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Candidates.CandidateRequirement", b =>
                {
                    b.HasOne("EventBooking.Domain.Candidates.Candidate", null)
                        .WithMany("Requirements")
                        .HasForeignKey("CandidateId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.InviteOption", b =>
                {
                    b.HasOne("EventBooking.Domain.Invites.Invite", null)
                        .WithMany("Options")
                        .HasForeignKey("InviteId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.ConfirmedSlot", b =>
                {
                    b.OwnsOne("EventBooking.Domain.Slots.SlotWindow", "Window", b1 =>
                        {
                            b1.Property<Guid>("ConfirmedSlotId")
                                .HasColumnType("uuid");

                            b1.Property<DateOnly>("Date")
                                .HasColumnType("date")
                                .HasColumnName("date");

                            b1.Property<TimeOnly>("StartTime")
                                .HasColumnType("time without time zone")
                                .HasColumnName("start_time");

                            b1.HasKey("ConfirmedSlotId");

                            b1.ToTable("confirmed_slot");

                            b1.WithOwner()
                                .HasForeignKey("ConfirmedSlotId");
                        });

                    b.Navigation("Window")
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.ProposalAcceptance", b =>
                {
                    b.HasOne("EventBooking.Domain.Slots.SlotProposal", null)
                        .WithMany("Acceptances")
                        .HasForeignKey("ProposalId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.SlotCapacity", b =>
                {
                    b.HasOne("EventBooking.Domain.Slots.ConfirmedSlot", null)
                        .WithMany("Capacities")
                        .HasForeignKey("ConfirmedSlotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.SlotProposal", b =>
                {
                    b.OwnsOne("EventBooking.Domain.Slots.SlotWindow", "Window", b1 =>
                        {
                            b1.Property<Guid>("SlotProposalId")
                                .HasColumnType("uuid");

                            b1.Property<DateOnly>("Date")
                                .HasColumnType("date")
                                .HasColumnName("date");

                            b1.Property<TimeOnly>("StartTime")
                                .HasColumnType("time without time zone")
                                .HasColumnName("start_time");

                            b1.HasKey("SlotProposalId");

                            b1.ToTable("slot_proposal");

                            b1.WithOwner()
                                .HasForeignKey("SlotProposalId");
                        });

                    b.Navigation("Window")
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Candidates.Candidate", b =>
                {
                    b.Navigation("Requirements");
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.Invite", b =>
                {
                    b.Navigation("Options");
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.ConfirmedSlot", b =>
                {
                    b.Navigation("Capacities");
                });

            modelBuilder.Entity("EventBooking.Domain.Slots.SlotProposal", b =>
                {
                    b.Navigation("Acceptances");
                });
#pragma warning restore 612, 618
        }
    }
}
`````
