# 00c — Configurable staff identity, edits 2 (Task 3a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Domain/Access/StaffId.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Domain/Access/StaffId.cs","beforeSha":"99ab9f5ec3723bb5c864fa15aef6d786f03c390e41f0007487ef678ed912b171","afterSha":"7356d779bd0805feff5356412ca6106d366ad225b0e54856af4e8289110ac34b","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Domain/Access/StaffId.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Domain/Access/StaffId.cs","beforeSha":"99ab9f5ec3723bb5c864fa15aef6d786f03c390e41f0007487ef678ed912b171","afterSha":"7356d779bd0805feff5356412ca6106d366ad225b0e54856af4e8289110ac34b","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>A canonical staff number validated against the deployment's configured format.</summary>
public sealed record StaffId
{
    /// <summary>The organisation-neutral format used when no deployment override is supplied.</summary>
    public const string DefaultPattern = "^[A-Z0-9]{1,32}$";

    /// <summary>Trims, uppercases and validates an identity-provider staff number.</summary>
    /// <param name="value">The untrusted input, including any surrounding whitespace.</param>
    /// <param name="pattern">The deployment's complete-value validation expression.</param>
    /// <exception cref="DomainException">The input violates the configured format or size bound.</exception>
    public StaffId(string? value, string pattern = DefaultPattern)
    {
        var canonical = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Matches(canonical, pattern))
            throw new DomainException("staffId does not match the configured format.");
        Value = canonical;
    }

    /// <summary>Gets the normalized identifier; equality compares this value.</summary>
    public string Value { get; }

    /// <summary>Parses input using the deployment's format.</summary>
    /// <param name="value">The untrusted staff number.</param>
    /// <param name="pattern">The deployment's complete-value expression.</param>
    /// <returns>The canonical staff number.</returns>
    public static StaffId Parse(string? value, string pattern = DefaultPattern) => new(value, pattern);

    /// <summary>Validates input without throwing for malformed staff numbers.</summary>
    /// <param name="value">The untrusted staff number.</param>
    /// <param name="staffId">The parsed value, or null on refusal.</param>
    /// <param name="pattern">The deployment's complete-value expression.</param>
    /// <returns>True when the input is valid under the supplied policy.</returns>
    public static bool TryParse(string? value, out StaffId? staffId, string pattern = DefaultPattern)
    {
        try { staffId = new StaffId(value, pattern); return true; }
        catch (DomainException) { staffId = null; return false; }
    }

    /// <summary>Rehydrates a stored identifier without applying a later deployment policy.</summary>
    /// <param name="value">The canonical identifier stored by an earlier authenticated request.</param>
    /// <returns>The same stored identifier without changing its representation.</returns>
    public static StaffId FromPersisted(string value)
    {
        if (value != value.Trim().ToUpperInvariant())
            throw new DomainException("Stored staffId is not canonical.");
        return new StaffId(value, "^.{1,32}$");
    }

    /// <summary>Returns the canonical identifier.</summary>
    /// <returns>The value, without a presentation prefix.</returns>
    public override string ToString() => Value;

    private static bool Matches(string canonical, string pattern)
    {
        if (canonical.Length is < 1 or > 32) return false;
        try
        {
            var match = Regex.Match(canonical, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return match.Success && match.Index == 0 && match.Length == canonical.Length;
        }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs","beforeSha":"682ea88e3b414dbf02c4453491b47ac2a685da7ec3869c9af98070f73c38517c","afterSha":"97a9f0e00117fdddc84a98fc140a6d199abe4923f25492b938df22e6f1f4851b","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs","beforeSha":"682ea88e3b414dbf02c4453491b47ac2a685da7ec3869c9af98070f73c38517c","afterSha":"97a9f0e00117fdddc84a98fc140a6d199abe4923f25492b938df22e6f1f4851b","side":"after","part":1,"parts":1} -->

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
            "char_length(staff_id) BETWEEN 1 AND 32 AND staff_id = btrim(staff_id)"));

        builder.HasKey(identity => identity.StaffUserId);
        builder.Property(identity => identity.StaffUserId)
            .HasColumnName("staff_user_id")
            .ValueGeneratedNever();
        builder.Property(identity => identity.StaffId)
            .HasConversion(staffId => staffId.Value, value => StaffId.FromPersisted(value))
            .HasColumnName("staff_id")
            .HasColumnType("character varying(32)")
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

## after — src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.Designer.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.Designer.cs","beforeSha":null,"afterSha":"10e911bd46b4511eccd7a6ce08dca5dfae8a389880772fae1056d60ecae176f5","side":"after","part":1,"parts":1} -->

`````csharp
﻿// <auto-generated />
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
    [Migration("20260920060813_GeneralizeStaffIdentifiers")]
    partial class GeneralizeStaffIdentifiers
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("EventBooking.Domain.Access.StaffAccessProfile", b =>
                {
                    b.Property<Guid>("StaffUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("staff_user_id");

                    b.Property<Guid?>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("boolean")
                        .HasColumnName("is_admin");

                    b.Property<bool>("IsAppointmentStaff")
                        .HasColumnType("boolean")
                        .HasColumnName("is_appointment_staff");

                    b.Property<bool>("IsCoordinator")
                        .HasColumnType("boolean")
                        .HasColumnName("is_coordinator");

                    b.Property<bool>("IsManager")
                        .HasColumnType("boolean")
                        .HasColumnName("is_manager");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("StaffUserId");

                    b.HasIndex("AppointmentTypeId")
                        .IsUnique()
                        .HasDatabaseName("ux_staff_access_profile_manager_appointment_type")
                        .HasFilter("is_manager");

                    b.ToTable("staff_access_profile", null, t =>
                        {
                            t.HasCheckConstraint("ck_staff_access_profile_admin_exclusive", "NOT is_admin OR (NOT is_coordinator AND NOT is_manager AND NOT is_appointment_staff AND appointment_type_id IS NULL)");

                            t.HasCheckConstraint("ck_staff_access_profile_has_role", "is_admin OR is_coordinator OR is_manager OR is_appointment_staff");

                            t.HasCheckConstraint("ck_staff_access_profile_scope", "appointment_type_id IS NULL OR (is_manager OR is_appointment_staff)");

                            t.HasCheckConstraint("ck_staff_access_profile_version", "version > 0");
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.Access.StaffIdentity", b =>
                {
                    b.Property<Guid>("StaffUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("staff_user_id");

                    b.Property<string>("DisplayName")
                        .HasColumnType("text")
                        .HasColumnName("display_name");

                    b.Property<DateTimeOffset>("LastSeenAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_seen_at");

                    b.Property<string>("StaffId")
                        .IsRequired()
                        .HasColumnType("character varying(32)")
                        .HasColumnName("staff_id");

                    b.HasKey("StaffUserId");

                    b.HasIndex("StaffId")
                        .IsUnique()
                        .HasDatabaseName("ux_staff_identity_staff_id");

                    b.ToTable("staff_identity", null, t =>
                        {
                            t.HasCheckConstraint("ck_staff_identity_format", "char_length(staff_id) BETWEEN 1 AND 32 AND staff_id = btrim(staff_id)");
                        });
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

            modelBuilder.Entity("EventBooking.Domain.AttendeeGroups.AttendeeGroup", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("code");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("name");

                    b.HasKey("Id");

                    b.HasIndex("Code")
                        .IsUnique();

                    b.ToTable("attendee_group", null, t =>
                        {
                            t.HasCheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");

                            t.HasCheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.AttendeeGroups.AttendeeGroupRequirement", b =>
                {
                    b.Property<Guid>("AttendeeGroupId")
                        .HasColumnType("uuid")
                        .HasColumnName("attendee_group_id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.HasKey("AttendeeGroupId", "AppointmentTypeId");

                    b.HasIndex("AppointmentTypeId");

                    b.ToTable("attendee_group_requirement", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Attendees.Attendee", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AttendeeGroupId")
                        .HasColumnType("uuid")
                        .HasColumnName("attendee_group_id");

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

                    b.Property<DateTimeOffset>("StatusChangedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("status_changed_at");

                    b.HasKey("Id");

                    b.HasIndex("AttendeeGroupId");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.HasIndex("Status");

                    b.ToTable("attendee", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Attendees.AttendeeRequirement", b =>
                {
                    b.Property<Guid>("AttendeeId")
                        .HasColumnType("uuid")
                        .HasColumnName("attendee_id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.HasKey("AttendeeId", "AppointmentTypeId");

                    b.ToTable("attendee_requirement", (string)null);
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

                    b.HasIndex("Timestamp", "Id")
                        .IsDescending()
                        .HasDatabaseName("ix_audit_log_timestamp");

                    b.ToTable("audit_log", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Bookings.Booking", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AttendeeId")
                        .HasColumnType("uuid")
                        .HasColumnName("attendee_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<Guid>("EventId")
                        .HasColumnType("uuid")
                        .HasColumnName("event_id");

                    b.Property<Guid>("InviteId")
                        .HasColumnType("uuid")
                        .HasColumnName("invite_id");

                    b.Property<string>("ManageTokenHash")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("manage_token_hash");

                    b.Property<Guid?>("RecoveryOfBookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("recovery_of_booking_id");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.HasKey("Id");

                    b.HasIndex("AttendeeId")
                        .IsUnique()
                        .HasDatabaseName("ux_booking_active_original_attendee")
                        .HasFilter("status = 1 AND recovery_of_booking_id IS NULL");

                    b.HasIndex("ManageTokenHash")
                        .IsUnique();

                    b.HasIndex("RecoveryOfBookingId")
                        .IsUnique()
                        .HasDatabaseName("ux_booking_active_recovery")
                        .HasFilter("status = 1 AND recovery_of_booking_id IS NOT NULL");

                    b.HasIndex("AttendeeId", "Status");

                    b.HasIndex("EventId", "Status");

                    b.ToTable("booking", null, t =>
                        {
                            t.HasCheckConstraint("ck_booking_no_self_recovery", "recovery_of_booking_id IS NULL OR recovery_of_booking_id <> id");
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.Bookings.BookingAppointment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.Property<Guid>("BookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("booking_id");

                    b.Property<DateTimeOffset?>("CheckedInAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("checked_in_at");

                    b.Property<DateTimeOffset?>("LastChangedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_changed_at");

                    b.Property<Guid?>("LastChangedByStaffUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("last_changed_by_staff_user_id");

                    b.Property<DateTimeOffset?>("OutcomeAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("outcome_at");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<long>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("bigint")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("AppointmentTypeId", "Status");

                    b.HasIndex("BookingId", "AppointmentTypeId")
                        .IsUnique();

                    b.ToTable("booking_appointment", null, t =>
                        {
                            t.HasCheckConstraint("CK_booking_appointment_last_change_pair", "(last_changed_by_staff_user_id IS NULL) = (last_changed_at IS NULL)");

                            t.HasCheckConstraint("CK_booking_appointment_status_timestamps", "(status = 1 AND checked_in_at IS NULL AND outcome_at IS NULL)\nOR (status = 2 AND checked_in_at IS NOT NULL AND outcome_at IS NULL)\nOR (status = 3 AND checked_in_at IS NOT NULL AND outcome_at IS NOT NULL\n    AND outcome_at >= checked_in_at)\nOR (status = 4 AND checked_in_at IS NULL AND outcome_at IS NOT NULL)");

                            t.HasCheckConstraint("CK_booking_appointment_version", "version > 0");
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.Events.Event", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid?>("ProposalId")
                        .HasColumnType("uuid")
                        .HasColumnName("proposal_id");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.HasKey("Id");

                    b.HasIndex("ProposalId")
                        .IsUnique();

                    b.HasIndex("Status");

                    b.ToTable("event", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Events.EventCapacity", b =>
                {
                    b.Property<Guid>("EventId")
                        .HasColumnType("uuid")
                        .HasColumnName("event_id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.Property<int>("RemainingCapacity")
                        .HasColumnType("integer")
                        .HasColumnName("remaining_capacity");

                    b.Property<int>("TotalHeadcount")
                        .HasColumnType("integer")
                        .HasColumnName("total_headcount");

                    b.HasKey("EventId", "AppointmentTypeId");

                    b.ToTable("event_capacity", null, t =>
                        {
                            t.HasCheckConstraint("ck_event_capacity_within_bounds", "remaining_capacity >= 0 AND remaining_capacity <= total_headcount");
                        });
                });

            modelBuilder.Entity("EventBooking.Domain.Events.EventProposal", b =>
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

                    b.ToTable("event_proposal", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Events.ProposalAcceptance", b =>
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

            modelBuilder.Entity("EventBooking.Domain.Invites.Invite", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AttendeeId")
                        .HasColumnType("uuid")
                        .HasColumnName("attendee_id");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<Guid?>("RecoveryOfBookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("recovery_of_booking_id");

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

                    b.HasIndex("AttendeeId")
                        .IsUnique()
                        .HasDatabaseName("ux_invite_pending_attendee")
                        .HasFilter("status = 1");

                    b.HasIndex("RecoveryOfBookingId");

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

                    b.Property<Guid>("EventId")
                        .HasColumnType("uuid")
                        .HasColumnName("event_id");

                    b.HasKey("InviteId", "EventId");

                    b.ToTable("invite_option", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.InviteRequirement", b =>
                {
                    b.Property<Guid>("InviteId")
                        .HasColumnType("uuid")
                        .HasColumnName("invite_id");

                    b.Property<Guid>("AppointmentTypeId")
                        .HasColumnType("uuid")
                        .HasColumnName("appointment_type_id");

                    b.HasKey("InviteId", "AppointmentTypeId");

                    b.HasIndex("AppointmentTypeId");

                    b.ToTable("invite_requirement", (string)null);
                });

            modelBuilder.Entity("EventBooking.Domain.Notifications.EmailLog", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AttendeeId")
                        .HasColumnType("uuid")
                        .HasColumnName("attendee_id");

                    b.Property<Guid?>("BookingId")
                        .HasColumnType("uuid")
                        .HasColumnName("booking_id");

                    b.Property<DateTimeOffset?>("ClaimedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("claimed_at");

                    b.Property<Guid?>("EventId")
                        .HasColumnType("uuid")
                        .HasColumnName("event_id");

                    b.Property<Guid?>("InviteId")
                        .HasColumnType("uuid")
                        .HasColumnName("invite_id");

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

                    b.HasIndex("AttendeeId");

                    b.HasIndex("AttendeeId", "SentAt");

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

            modelBuilder.Entity("EventBooking.Domain.Access.StaffAccessProfile", b =>
                {
                    b.HasOne("EventBooking.Domain.AppointmentTypes.AppointmentType", null)
                        .WithMany()
                        .HasForeignKey("AppointmentTypeId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("EventBooking.Domain.AttendeeGroups.AttendeeGroupRequirement", b =>
                {
                    b.HasOne("EventBooking.Domain.AppointmentTypes.AppointmentType", null)
                        .WithMany()
                        .HasForeignKey("AppointmentTypeId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("EventBooking.Domain.AttendeeGroups.AttendeeGroup", null)
                        .WithMany("Requirements")
                        .HasForeignKey("AttendeeGroupId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Attendees.Attendee", b =>
                {
                    b.HasOne("EventBooking.Domain.AttendeeGroups.AttendeeGroup", null)
                        .WithMany()
                        .HasForeignKey("AttendeeGroupId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Attendees.AttendeeRequirement", b =>
                {
                    b.HasOne("EventBooking.Domain.Attendees.Attendee", null)
                        .WithMany("Requirements")
                        .HasForeignKey("AttendeeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Bookings.Booking", b =>
                {
                    b.HasOne("EventBooking.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("RecoveryOfBookingId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("EventBooking.Domain.Bookings.BookingAppointment", b =>
                {
                    b.HasOne("EventBooking.Domain.AppointmentTypes.AppointmentType", null)
                        .WithMany()
                        .HasForeignKey("AppointmentTypeId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("EventBooking.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("BookingId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Events.Event", b =>
                {
                    b.OwnsOne("EventBooking.Domain.Events.EventWindow", "Window", b1 =>
                        {
                            b1.Property<Guid>("EventId")
                                .HasColumnType("uuid");

                            b1.Property<DateOnly>("Date")
                                .HasColumnType("date")
                                .HasColumnName("date");

                            b1.Property<TimeOnly>("StartTime")
                                .HasColumnType("time without time zone")
                                .HasColumnName("start_time");

                            b1.HasKey("EventId");

                            b1.ToTable("event");

                            b1.WithOwner()
                                .HasForeignKey("EventId");
                        });

                    b.Navigation("Window")
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Events.EventCapacity", b =>
                {
                    b.HasOne("EventBooking.Domain.Events.Event", null)
                        .WithMany("Capacities")
                        .HasForeignKey("EventId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Events.EventProposal", b =>
                {
                    b.OwnsOne("EventBooking.Domain.Events.EventWindow", "Window", b1 =>
                        {
                            b1.Property<Guid>("EventProposalId")
                                .HasColumnType("uuid");

                            b1.Property<DateOnly>("Date")
                                .HasColumnType("date")
                                .HasColumnName("date");

                            b1.Property<TimeOnly>("StartTime")
                                .HasColumnType("time without time zone")
                                .HasColumnName("start_time");

                            b1.HasKey("EventProposalId");

                            b1.HasIndex("Date", "StartTime")
                                .IsUnique()
                                .HasDatabaseName("ux_event_proposal_open_window")
                                .HasFilter("status = 1");

                            b1.ToTable("event_proposal");

                            b1.WithOwner()
                                .HasForeignKey("EventProposalId");
                        });

                    b.Navigation("Window")
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Events.ProposalAcceptance", b =>
                {
                    b.HasOne("EventBooking.Domain.Events.EventProposal", null)
                        .WithMany("Acceptances")
                        .HasForeignKey("ProposalId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.Invite", b =>
                {
                    b.HasOne("EventBooking.Domain.Bookings.Booking", null)
                        .WithMany()
                        .HasForeignKey("RecoveryOfBookingId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.InviteOption", b =>
                {
                    b.HasOne("EventBooking.Domain.Invites.Invite", null)
                        .WithMany("Options")
                        .HasForeignKey("InviteId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.InviteRequirement", b =>
                {
                    b.HasOne("EventBooking.Domain.AppointmentTypes.AppointmentType", null)
                        .WithMany()
                        .HasForeignKey("AppointmentTypeId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("EventBooking.Domain.Invites.Invite", null)
                        .WithMany("Requirements")
                        .HasForeignKey("InviteId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("EventBooking.Domain.AttendeeGroups.AttendeeGroup", b =>
                {
                    b.Navigation("Requirements");
                });

            modelBuilder.Entity("EventBooking.Domain.Attendees.Attendee", b =>
                {
                    b.Navigation("Requirements");
                });

            modelBuilder.Entity("EventBooking.Domain.Events.Event", b =>
                {
                    b.Navigation("Capacities");
                });

            modelBuilder.Entity("EventBooking.Domain.Events.EventProposal", b =>
                {
                    b.Navigation("Acceptances");
                });

            modelBuilder.Entity("EventBooking.Domain.Invites.Invite", b =>
                {
                    b.Navigation("Options");

                    b.Navigation("Requirements");
                });
#pragma warning restore 612, 618
        }
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Infrastructure/Persistence/Migrations/20260920060813_GeneralizeStaffIdentifiers.cs","beforeSha":null,"afterSha":"f1f223ede7099fe8bbd87c14838bfc4cc317e647b5a638d826b116478acc1b2b","side":"after","part":1,"parts":1} -->

`````csharp
﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeStaffIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity");

            migrationBuilder.AlterColumn<string>(
                name: "staff_id",
                table: "staff_identity",
                type: "character varying(32)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(7)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity",
                sql: "char_length(staff_id) BETWEEN 1 AND 32 AND staff_id = btrim(staff_id)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity");

            migrationBuilder.AlterColumn<string>(
                name: "staff_id",
                table: "staff_identity",
                type: "character(7)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_identity_format",
                table: "staff_identity",
                sql: "staff_id ~* '^[UN][0-9]{6}$'");
        }
    }
}
`````
