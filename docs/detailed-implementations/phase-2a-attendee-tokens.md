# 02a — Deterministic attendee links and the token version counter (Task 9a)

[← Phase overview](phase-2-persistence.md) · [Plans overview](README.md) · [Ontology](../ontology.md)

This task opens Phase 2 and settles decision D14. The predecessor minted a random attendee link and stored its SHA-256 hash; design 06 signs the link from what it names, so the server can reproduce it and stores only a counter. This is the first half of master Task 9: the counter is a column, and the fresh schema in the next task writes it once rather than adding it to a chain it is about to delete.

> Use superpowers:executing-plans. Complete changed types and exact before/after files are embedded
> in the numbered companion volumes; apply them with the script in Step 3, never by hand.

**Goal:** A book link and a manage link are each `base64url(purpose ‖ id ‖ version ‖ HMAC-SHA256(key, purpose ‖ id ‖ version))`. An `Invite` stores tokenVersion and a `Booking` stores manageTokenVersion, both starting at 1; nothing derived from a token is written anywhere, and incrementing the counter revokes every outstanding link for that row.

**Architecture:** Three things follow from signing the identifier into the token instead of looking a hash up. The lookup is by primary key, so the four hash-keyed repository methods and their two unique indexes go: verify the signature in constant time first, load the row by id, then require that its stored version equals the token's. The purpose is inside the signature, so a book link can never be replayed as a manage link for the same identifier. And the link is reproducible, so a resend re-issues the same URL rather than rotating: that is what lets the confirmation page and the confirmation email carry one manage link, and it is why the retry path no longer touches the counter.

**Tech Stack:** .NET 10, xUnit, EF Core, PostgreSQL Testcontainers, bUnit.

**Spec:** [Master Task 9](../superpowers/plans/2026-09-19-eventbooking-implementation.md), [domain model](../design/01-domain-model.md), [functional requirements](../design/02-functional-requirements.md), [ontology](../ontology.md).

## Global constraints

The counter is domain state, not a service concern: the token service never reads or writes a row. Version 0 does not exist — it is refused at issue and rejected on read — so a generated backfill of 0 is wrong, and the migration here writes 1 and then drops the column default. Only the canonical base64url spelling verifies; the last character of the token carries two significant bits, and accepting its three siblings would make one link answer to four URLs. The migration this task adds is deliberately short-lived: the next task deletes the whole inherited chain.

## Review focus

STOP AND CHECK four things. The same purpose, identifier and version always produce the same string — if they do not, the confirmation email cannot share the page's link. A book token and a manage token for one identifier differ, and each reads back with its own purpose. A stale version is refused after the row loads, not before, so a forged or enumerated identifier never reaches the database. And every failure — bad signature, unknown purpose, version 0, wrong length, non-canonical encoding — returns the same single message to the caller, because a caller must not be able to tell a forgery from an expiry.

### Task 9a: Attendee links the server can reproduce, and the version that revokes them

**Files:**

- Modify: src/EventBooking.Application/Abstractions/IBookingRepository.cs
- Modify: src/EventBooking.Application/Abstractions/IInviteRepository.cs
- Modify: src/EventBooking.Application/Abstractions/ITokenService.cs
- Modify: src/EventBooking.Application/Bookings/CancelBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/ViewBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/ViewInviteHandler.cs
- Modify: src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs
- Modify: src/EventBooking.Application/Invites/InviteIssuer.cs
- Modify: src/EventBooking.Application/Notifications/RetryEmailHandler.cs
- Modify: src/EventBooking.Domain/Bookings/Booking.cs
- Modify: src/EventBooking.Domain/Invites/Invite.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs
- Modify: src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs
- Modify: src/EventBooking.SeedData/DemoSeeder.cs
- Modify: tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AuditEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/BookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/EventEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs
- Modify: tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs
- Modify: tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakeTokenService.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs
- Modify: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Modify: tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs
- Modify: tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs
- Modify: tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs
- Modify: tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs
- Modify: tests/EventBooking.Domain.Tests/Invites/InviteTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs
- Modify: tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs

**Interfaces:**

These complete types define the changed public boundary and its domain behavior. Apply them with the after files at Step 3, not before the failing test.

```csharp
namespace EventBooking.Application.Abstractions;

/// <summary>What an attendee link is for. Part of the signed payload, so a book token can never
/// be replayed as a manage token for the same identifier.</summary>
public enum TokenPurpose
{
    /// <summary>The link that opens an invite's options.</summary>
    Book = 1,

    /// <summary>The link that views, cancels or reschedules a booking.</summary>
    Manage = 2,
}

/// <summary>What a valid token names: the row to load and the version it must still be on.</summary>
/// <param name="Purpose">The purpose.</param>
/// <param name="EntityId">The invite or booking identifier.</param>
/// <param name="Version">The token version the link was issued against.</param>
public readonly record struct TokenReference(TokenPurpose Purpose, Guid EntityId, int Version);

/// <summary>
/// Deterministic attendee links (design 06). The token is reproducible from the row, so the
/// confirmation page and the confirmation email share one link and nothing derived from the token
/// is ever stored: the row keeps only the version counter that a link is revoked by incrementing.
/// </summary>
public interface ITokenService
{
    /// <summary>Issues the link for one purpose, identifier and version.</summary>
    /// <param name="purpose">The purpose.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="version">The current version counter on the row; one or more.</param>
    string Issue(TokenPurpose purpose, Guid entityId, int version);

    /// <summary>
    /// Verifies the signature in constant time and recovers what the token names, before any
    /// database access. False if the token is malformed or the signature does not verify.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="reference">What the token names, when it verifies.</param>
    bool TryRead(string? token, out TokenReference reference);
}
```

```csharp
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Tokens;

/// <summary>
/// base64url(purpose ‖ id ‖ version ‖ HMAC-SHA256(key, purpose ‖ id ‖ version)), per design 06.
/// </summary>
public sealed class HmacTokenService : ITokenService
{
    private const int MinimumKeyLength = 32;

    /// <summary>
    /// The placeholder signing key shipped in appsettings.json. It passes the length check,
    /// so it is rejected by value: anyone who can read this repository could forge tokens with it.
    /// </summary>
    private const string PlaceholderSigningKey = "replace-this-with-a-real-secret-of-at-least-32-characters";

    private const int PurposeBytes = 1;
    private const int IdentifierBytes = 16;
    private const int VersionBytes = 4;
    private const int PayloadBytes = PurposeBytes + IdentifierBytes + VersionBytes;
    private const int SignatureBytes = 32;
    private const int TokenBytes = PayloadBytes + SignatureBytes;

    /// <summary>53 bytes encode to 71 unpadded base64url characters.</summary>
    private const int TokenLength = 71;

    private readonly byte[] _key;

    public HmacTokenService(TokenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SigningKey, nameof(options.SigningKey));

        if (options.SigningKey.Length < MinimumKeyLength)
        {
            throw new ArgumentException(
                $"The token signing key must be at least {MinimumKeyLength} characters.",
                nameof(options));
        }

        if (string.Equals(options.SigningKey, PlaceholderSigningKey, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The token signing key is still the placeholder from appsettings.json. Configure Tokens:SigningKey.",
                nameof(options));
        }

        _key = Encoding.UTF8.GetBytes(options.SigningKey);
    }

    public string Issue(TokenPurpose purpose, Guid entityId, int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1, nameof(version));

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        Span<byte> token = stackalloc byte[TokenBytes];
        WritePayload(token, (byte)purpose, entityId, version);
        HMACSHA256.HashData(_key, token[..PayloadBytes], token[PayloadBytes..]);

        return ToBase64Url(token);
    }

    public bool TryRead(string? token, out TokenReference reference)
    {
        reference = default;

        if (token is null || token.Length != TokenLength)
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[TokenBytes];
        if (!TryDecodeBase64Url(token, decoded))
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[SignatureBytes];
        HMACSHA256.HashData(_key, decoded[..PayloadBytes], expected);

        // Constant time: a timing difference here would leak how much of a guess was right.
        if (!CryptographicOperations.FixedTimeEquals(expected, decoded[PayloadBytes..]))
        {
            return false;
        }

        var purpose = (TokenPurpose)decoded[0];
        if (!Enum.IsDefined(purpose))
        {
            return false;
        }

        var version = BinaryPrimitives.ReadInt32BigEndian(
            decoded.Slice(PurposeBytes + IdentifierBytes, VersionBytes));
        if (version < 1)
        {
            return false;
        }

        reference = new TokenReference(
            purpose,
            new Guid(decoded.Slice(PurposeBytes, IdentifierBytes), bigEndian: true),
            version);
        return true;
    }

    private static void WritePayload(Span<byte> destination, byte purpose, Guid entityId, int version)
    {
        destination[0] = purpose;
        entityId.TryWriteBytes(destination.Slice(PurposeBytes, IdentifierBytes), bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(
            destination.Slice(PurposeBytes + IdentifierBytes, VersionBytes), version);
    }

    /// <summary>Base64 with the two characters that are unsafe in a URL replaced, and no padding.</summary>
    private static string ToBase64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Decodes only the canonical spelling. Base64 has several encodings of the same bytes once
    /// padding bits are ignored, and accepting them would make one link answer to many URLs.
    /// </summary>
    private static bool TryDecodeBase64Url(string value, Span<byte> destination)
    {
        Span<char> base64 = stackalloc char[TokenLength + 1];
        for (var index = 0; index < value.Length; index++)
        {
            base64[index] = value[index] switch
            {
                '-' => '+',
                '_' => '/',
                var character when IsBase64UrlCharacter(character) => character,
                _ => '\0',
            };

            if (base64[index] == '\0')
            {
                return false;
            }
        }

        base64[TokenLength] = '=';

        return Convert.TryFromBase64Chars(base64, destination, out var written)
            && written == TokenBytes
            && string.Equals(ToBase64Url(destination), value, StringComparison.Ordinal);
    }

    private static bool IsBase64UrlCharacter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
}
```

```csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Invites;

/// <summary>An offer of event options carrying an immutable requirement snapshot.</summary>
public sealed class Invite
{
    /// <summary>Gets the number of event options every invite offers.</summary>
    public const int RequiredOptionCount = 3;

    /// <summary>The version every freshly issued invite's book link is signed against.</summary>
    public const int InitialTokenVersion = 1;

    /// <summary>The fewest locations an invite may be restricted to (design 08).</summary>
    public const int MinimumLocationCount = 1;

    /// <summary>The most locations an invite may be restricted to (design 08).</summary>
    public const int MaximumLocationCount = 50;

    private readonly List<InviteLocation> _locations = [];
    private readonly List<InviteOption> _options = [];
    private readonly List<InviteRequirement> _requirements = [];

    private Invite()
    {
        // Required by the persistence layer's constructor binding.
    }

    /// <summary>Gets the invite identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the invited attendee identifier.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Gets the original Booking recovered by this Invite, or null for an initial Invite.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>
    /// The version the book link is signed against. Only the counter is stored; the token itself
    /// is reproduced from it on demand and never written anywhere (design 06).
    /// </summary>
    public int TokenVersion { get; private set; } = InitialTokenVersion;

    /// <summary>Gets when the invite stops being usable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the invite lifecycle state.</summary>
    public InviteStatus Status { get; private set; } = InviteStatus.Pending;

    /// <summary>Gets how many retries preceded this invite.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Gets the locations every offer on this invite is drawn from.</summary>
    public IReadOnlyList<InviteLocation> Locations => _locations;

    /// <summary>Gets the selected location identifiers in stable order.</summary>
    public IReadOnlyList<Guid> LocationIds =>
        _locations.Select(l => l.LocationId).ToList();

    /// <summary>Gets the offered event options.</summary>
    public IReadOnlyList<InviteOption> Options => _options;

    /// <summary>Gets the offered event identifiers.</summary>
    public IReadOnlyList<Guid> OfferedEventIds => _options.Select(o => o.EventId).ToList();

    /// <summary>Gets the immutable requirement snapshot used by every downstream operation.</summary>
    public IReadOnlyList<InviteRequirement> Requirements => _requirements;

    /// <summary>Gets the snapshotted Appointment Type identifiers in stable order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(r => r.AppointmentTypeId).Order().ToList();

    /// <summary>Revokes every outstanding book link for this invite by moving to the next version.</summary>
    public void RotateToken()
    {
        EnsurePending("Only a pending invite token can be rotated.");
        TokenVersion++;
    }

    /// <summary>Creates an initial invite snapshotting every current derived requirement.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="locationIds">The locations the Coordinator selected; 1 to 50, no duplicates.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="retryCount">The retry count.</param>
    public static Invite CreateInitial(
        Guid id,
        Guid attendeeId,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount) =>
        Create(
            id, attendeeId, null, expiresAt,
            DistinctLocations(locationIds), eventIds, appointmentTypeIds, retryCount);

    /// <summary>
    /// Issues the next invite of the same journey, on the same locations. Expiry re-issue, top-up
    /// and event cancellation all go through here, so none of them can quietly widen the set the
    /// Coordinator chose.
    /// </summary>
    /// <param name="id">The new invite's id.</param>
    /// <param name="originating">The invite being replaced.</param>
    /// <param name="expiresAt">When the new invite stops being usable.</param>
    /// <param name="eventIds">The freshly chosen event options.</param>
    public static Invite Reissue(
        Guid id,
        Invite originating,
        DateTimeOffset expiresAt,
        IEnumerable<Guid> eventIds)
    {
        ArgumentNullException.ThrowIfNull(originating);

        return Create(
            id,
            originating.AttendeeId,
            originating.RecoveryOfBookingId,
            expiresAt,
            originating.LocationIds,
            eventIds,
            originating.RequiredAppointmentTypeIds,
            originating.RetryCount + 1);
    }

    /// <summary>Creates a recovery invite snapshotting only recoverable no-show types.</summary>
    /// <param name="id">The id.</param>
    /// <param name="attendeeId">The attendee id.</param>
    /// <param name="recoveryOfBookingId">The recovery of booking id.</param>
    /// <param name="expiresAt">The expires at.</param>
    /// <param name="originalLocationId">The location of the booking being recovered.</param>
    /// <param name="additionalLocationIds">Further locations the Coordinator opened up, or null.</param>
    /// <param name="eventIds">The event ids.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static Invite CreateRecovery(
        Guid id,
        Guid attendeeId,
        Guid recoveryOfBookingId,
        DateTimeOffset expiresAt,
        Guid originalLocationId,
        IEnumerable<Guid>? additionalLocationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(recoveryOfBookingId == Guid.Empty, "recoveryOfBookingId must not be empty.");
        Guard.Against(originalLocationId == Guid.Empty, "originalLocationId must not be empty.");

        // The attendee already travelled to the original booking's location, so it is always
        // offered. Anything the Coordinator adds joins it; a repeat of it is not an error.
        var locations = new List<Guid> { originalLocationId };
        foreach (var locationId in additionalLocationIds ?? [])
        {
            if (!locations.Contains(locationId))
            {
                locations.Add(locationId);
            }
        }

        return Create(
            id, attendeeId, recoveryOfBookingId, expiresAt,
            Bounded(locations), eventIds, appointmentTypeIds, 0);
    }

    /// <summary>Determines whether the invite can still be used at the supplied instant.</summary>
    /// <param name="now">The now.</param>
    public bool IsUsableAt(DateTimeOffset now) =>
        Status == InviteStatus.Pending && now < ExpiresAt;

    /// <summary>Determines whether the invite offers the supplied eventItem.</summary>
    /// <param name="eventId">The event id.</param>
    public bool Offers(Guid eventId) =>
        _options.Any(o => o.EventId == eventId);

    /// <summary>Removes one offered event from a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void RemoveOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");

        var option = _options.SingleOrDefault(o => o.EventId == eventId);
        Guard.Against(option is null, "This invite does not offer that eventItem.");

        _options.Remove(option!);
    }

    /// <summary>Adds one offered event to a pending invite.</summary>
    /// <param name="eventId">The event id.</param>
    public void AddOption(Guid eventId)
    {
        EnsurePending("Only a pending invite's options can change.");
        Guard.Against(
            _options.Count >= RequiredOptionCount,
            $"An invite cannot offer more than {RequiredOptionCount} event options.");
        Guard.Against(Offers(eventId), "An invite cannot offer the same event twice.");

        _options.Add(InviteOption.For(Id, eventId));
    }

    /// <summary>Moves a pending invite to used.</summary>
    public void MarkUsed() => TransitionFromPendingTo(InviteStatus.Used);

    /// <summary>Moves a pending invite to expired.</summary>
    public void MarkExpired() => TransitionFromPendingTo(InviteStatus.Expired);

    /// <summary>Moves a pending invite to superseded.</summary>
    public void MarkSuperseded() => TransitionFromPendingTo(InviteStatus.Superseded);

    /// <summary>Cancels a pending recovery Invite without changing capacity.</summary>
    public void CancelRecovery()
    {
        Guard.Against(RecoveryOfBookingId is null, "Only a recovery invite can be cancelled.");
        EnsurePending("Only a pending recovery invite can be cancelled.");
        Status = InviteStatus.Cancelled;
    }

    private static Invite Create(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        DateTimeOffset expiresAt,
        IReadOnlyList<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        IEnumerable<Guid> appointmentTypeIds,
        int retryCount)
    {
        var invite = CreateCore(
            id, attendeeId, recoveryOfBookingId, expiresAt, locationIds, eventIds, retryCount);

        var snapshot = appointmentTypeIds.ToList();
        Guard.Against(snapshot.Count == 0, "An invite must snapshot at least one appointment type.");
        Guard.Against(
            snapshot.Distinct().Count() != snapshot.Count,
            "An invite cannot snapshot the same appointment type twice.");

        foreach (var appointmentTypeId in snapshot)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        foreach (var appointmentTypeId in snapshot.Order())
        {
            invite._requirements.Add(InviteRequirement.For(id, appointmentTypeId));
        }

        return invite;
    }

    private static Invite CreateCore(
        Guid id,
        Guid attendeeId,
        Guid? recoveryOfBookingId,
        DateTimeOffset expiresAt,
        IReadOnlyList<Guid> locationIds,
        IEnumerable<Guid> eventIds,
        int retryCount)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(attendeeId == Guid.Empty, "attendeeId must not be empty.");

        var offeredEventIds = eventIds.ToList();
        Guard.Against(
            offeredEventIds.Count != RequiredOptionCount,
            $"An invite must offer exactly {RequiredOptionCount} event options.");
        Guard.Against(
            offeredEventIds.Distinct().Count() != offeredEventIds.Count,
            "An invite cannot offer the same event twice.");

        var invite = new Invite
        {
            Id = id,
            AttendeeId = attendeeId,
            RecoveryOfBookingId = recoveryOfBookingId,
            ExpiresAt = expiresAt,
            Status = InviteStatus.Pending,
            RetryCount = Guard.NotNegative(retryCount, "retryCount"),
        };

        foreach (var locationId in locationIds)
        {
            invite._locations.Add(InviteLocation.For(id, locationId));
        }

        foreach (var eventId in eventIds)
        {
            invite._options.Add(InviteOption.For(id, eventId));
        }

        return invite;
    }

    private static IReadOnlyList<Guid> DistinctLocations(IEnumerable<Guid> locationIds)
    {
        ArgumentNullException.ThrowIfNull(locationIds);

        var chosen = locationIds.ToList();
        Guard.Against(
            chosen.Distinct().Count() != chosen.Count,
            "An invite cannot be restricted to the same location twice.");

        return Bounded(chosen);
    }

    private static IReadOnlyList<Guid> Bounded(IReadOnlyList<Guid> locationIds)
    {
        Guard.Against(
            locationIds.Count < MinimumLocationCount,
            "An invite must be restricted to at least one location.");
        Guard.Against(
            locationIds.Count > MaximumLocationCount,
            $"An invite cannot be restricted to more than {MaximumLocationCount} locations.");
        Guard.Against(
            locationIds.Any(locationId => locationId == Guid.Empty),
            "A location id must not be empty.");

        return locationIds;
    }

    private void TransitionFromPendingTo(InviteStatus target)
    {
        EnsurePending($"An invite that is already {Status} cannot become {target}.");
        Status = target;
    }

    private void EnsurePending(string message) =>
        Guard.Against(Status != InviteStatus.Pending, message);
}
```

```csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Domain.Bookings;

/// <summary>Defines booking for the current use case.</summary>
public sealed class Booking
{
    /// <summary>The version every new booking's manage link is signed against.</summary>
    public const int InitialManageTokenVersion = 1;

    private Booking()
    {
        // Required by the persistence layer's constructor binding.
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }

    /// <summary>Defines attendee id for the current use case.</summary>
    public Guid AttendeeId { get; private set; }

    /// <summary>Defines event id for the current use case.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Defines invite id for the current use case.</summary>
    public Guid InviteId { get; private set; }

    /// <summary>Defines created at for the current use case.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Defines status for the current use case.</summary>
    public BookingStatus Status { get; private set; } = BookingStatus.Active;

    /// <summary>Gets the original active Booking ID, or null for the journey root.</summary>
    public Guid? RecoveryOfBookingId { get; private set; }

    /// <summary>Gets whether this Booking is the original journey root.</summary>
    public bool IsOriginal => RecoveryOfBookingId is null;

    /// <summary>
    /// The version the cancel/reschedule link is signed against. Only the counter is stored; the
    /// token is reproduced from it, which is how the confirmation page and the confirmation email
    /// carry the same link (design 06).
    /// </summary>
    public int ManageTokenVersion { get; private set; } = InitialManageTokenVersion;

    /// <summary>Revokes every outstanding manage link for this booking by moving to the next version.</summary>
    public void RotateManageToken()
    {
        Guard.Against(Status != BookingStatus.Active, "Only an active booking token can be rotated.");
        ManageTokenVersion++;
    }

    /// <summary>Defines create for the current use case.</summary>
    /// <param name="id">The id.</param>
    /// <param name="invite">The invite.</param>
    /// <param name="eventId">The event id.</param>
    /// <param name="createdAt">The created at.</param>
    public static Booking Create(
        Guid id,
        Invite invite,
        Guid eventId,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(invite is null, "invite must be supplied.");
        Guard.Against(invite!.Status != InviteStatus.Pending, "This invite can no longer be used.");
        Guard.Against(
            !invite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = invite.AttendeeId,
            EventId = eventId,
            InviteId = invite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
        };
    }

    /// <summary>Defines cancel for the current use case.</summary>
    public void Cancel()
    {
        Guard.Against(Status == BookingStatus.Cancelled, "This booking has already been cancelled.");
        Status = BookingStatus.Cancelled;
    }

    /// <summary>Creates a recovery Booking directly linked to the original Booking.</summary>
    /// <param name="id">The stable recovery booking identifier.</param>
    /// <param name="recoveryInvite">The pending recovery invite issued for the original Booking.</param>
    /// <param name="originalBooking">The active original journey root being recovered.</param>
    /// <param name="eventId">The recovery event offered by the invite.</param>
    /// <param name="createdAt">When the recovery booking is created.</param>
    /// <returns>An active recovery Booking pointing at the original root.</returns>
    public static Booking CreateRecovery(
        Guid id,
        Invite recoveryInvite,
        Booking originalBooking,
        Guid eventId,
        DateTimeOffset createdAt)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(recoveryInvite is null, "recoveryInvite must be supplied.");
        Guard.Against(originalBooking is null, "originalBooking must be supplied.");
        Guard.Against(!originalBooking!.IsOriginal, "A recovery booking cannot point at another recovery.");
        Guard.Against(
            originalBooking.Status != BookingStatus.Active,
            "A recovery booking requires an active original booking.");
        Guard.Against(
            recoveryInvite!.RecoveryOfBookingId != originalBooking.Id,
            "The recovery invite must point at the supplied original booking.");
        Guard.Against(
            recoveryInvite.AttendeeId != originalBooking.AttendeeId,
            "The recovery invite must belong to the original booking attendee.");
        Guard.Against(
            recoveryInvite.Status != InviteStatus.Pending,
            "This invite can no longer be used.");
        Guard.Against(
            !recoveryInvite.Offers(eventId),
            "The chosen eventItem is not one of this invite's options.");

        return new Booking
        {
            Id = id,
            AttendeeId = originalBooking.AttendeeId,
            EventId = eventId,
            InviteId = recoveryInvite.Id,
            CreatedAt = createdAt,
            Status = BookingStatus.Active,
            RecoveryOfBookingId = originalBooking.Id,
        };
    }

    /// <summary>Concludes an Active recovery Booking after all of its appointments are terminal.</summary>
    public void Conclude()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can conclude.");
        Guard.Against(Status != BookingStatus.Active, "Only an active recovery booking can conclude.");
        Status = BookingStatus.Concluded;
    }

    /// <summary>Reopens a Concluded recovery Booking after an allowed outcome correction.</summary>
    public void Reopen()
    {
        Guard.Against(IsOriginal, "Only a recovery booking can reopen.");
        Guard.Against(Status != BookingStatus.Concluded, "Only a concluded recovery booking can reopen.");
        Status = BookingStatus.Active;
    }
}
```

**Context you need**

- Design 06 (attendee authentication): the book token is issued when the `Invite` is created with tokenVersion 1, and is invalidated by the invite becoming `Used`, `Expired`, `Superseded` or `Cancelled`.
- Design 06: the manage token is issued when the `Booking` is created with manageTokenVersion 1, and is shown on the confirmation page and in the confirmation email — one link, two places.
- Design 06 (format): `base64url(purpose ‖ id ‖ version ‖ HMAC-SHA256(key, purpose ‖ id ‖ version))`, where purpose is book or manage.
- Design 06 (validation): recompute the HMAC and compare it in constant time before any database access, then load the row and require that its stored version equals the token's and that its state permits the operation.
- Design 06 (storage): only the version counter is stored. The raw token appears only in the URL and is never logged, audited or stored. The version exists so one link can be revoked by incrementing it.
- Design 06 (reuse): a token is reusable until it is invalidated; it is not consumed per request, and resending an email reuses the current link.
- Design 06 (key): the signing key must be at least 32 bytes, startup fails on a short or placeholder value, and rotating it invalidates every outstanding link.
- The ontology already defines tokenVersion on `Invite` and manageTokenVersion on `Booking`; this task only puts them in code.
- The master plan puts design 06's counter in Task 8. The user moved it here, so the fresh schema writes the column once instead of the inherited chain gaining a column it is about to lose.
- The predecessor's random-nonce token and stored hash are load-bearing in 62 files. This task is where they go.

- [ ] **Step 1: Write the failing tests**

Create these complete files before applying production changes.

tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs

```csharp
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Infrastructure.Tests;

public class HmacTokenServiceTests
{
    private const string SigningKey = "a-signing-key-that-is-long-enough-to-be-safe";

    /// <summary>purpose (1) + identifier (16) + version (4) + HMAC-SHA256 (32), base64url, unpadded.</summary>
    private const int TokenLength = 71;

    private static readonly TokenOptions Options =
        new(SigningKey);

    private readonly HmacTokenService _service = new(Options);

    [Theory]
    [InlineData(TokenPurpose.Book)]
    [InlineData(TokenPurpose.Manage)]
    public void AnIssuedTokenRoundTripsToItsPurposeIdentifierAndVersion(TokenPurpose purpose)
    {
        var id = Guid.NewGuid();

        var token = _service.Issue(purpose, id, 4);

        Assert.True(_service.TryRead(token, out var read));
        Assert.Equal(new TokenReference(purpose, id, 4), read);
    }

    /// <summary>
    /// Determinism is what lets the confirmation page and the confirmation email carry the same
    /// manage link without either of them storing it.
    /// </summary>
    [Fact]
    public void TheSameInputsAlwaysProduceTheSameToken()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            _service.Issue(TokenPurpose.Manage, id, 1),
            _service.Issue(TokenPurpose.Manage, id, 1));
    }

    [Fact]
    public void ABookTokenIsNotAManageTokenForTheSameIdentifier()
    {
        var id = Guid.NewGuid();

        var book = _service.Issue(TokenPurpose.Book, id, 1);
        var manage = _service.Issue(TokenPurpose.Manage, id, 1);

        Assert.NotEqual(book, manage);
        Assert.True(_service.TryRead(book, out var readBook));
        Assert.Equal(TokenPurpose.Book, readBook.Purpose);
        Assert.True(_service.TryRead(manage, out var readManage));
        Assert.Equal(TokenPurpose.Manage, readManage.Purpose);
    }

    [Fact]
    public void EachVersionOfOneIdentifierIsADifferentToken()
    {
        var id = Guid.NewGuid();

        var first = _service.Issue(TokenPurpose.Book, id, 1);
        var second = _service.Issue(TokenPurpose.Book, id, 2);

        Assert.NotEqual(first, second);
        Assert.True(_service.TryRead(first, out var readFirst));
        Assert.Equal(1, readFirst.Version);
        Assert.True(_service.TryRead(second, out var readSecond));
        Assert.Equal(2, readSecond.Version);
    }

    /// <summary>Nothing derived from the token is stored, so the service offers no hash of it.</summary>
    [Fact]
    public void TheServiceOffersNoWayToDeriveAStoredValueFromAToken()
    {
        Assert.DoesNotContain(
            typeof(ITokenService).GetMethods(),
            method => method.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ANonPositiveVersionIsRefusedAtIssue()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 0));

        Assert.Equal("version", ex.ParamName);
    }

    [Fact]
    public void AnUnknownPurposeIsRefusedAtIssue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Issue((TokenPurpose)7, Guid.NewGuid(), 1));
    }

    [Fact]
    public void ATokenCarryingANonPositiveVersionIsRejected()
    {
        var token = CreateKnownKeyToken((byte)TokenPurpose.Book, Guid.NewGuid(), 0);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATokenCarryingAnUnknownPurposeIsRejected()
    {
        var token = CreateKnownKeyToken(7, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATamperedTokenFailsVerification()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var replacement = Alphabet[(Alphabet.IndexOf(token[0], StringComparison.Ordinal) + 1) % Alphabet.Length];

        Assert.False(_service.TryRead($"{replacement}{token[1..]}", out var read));
        Assert.Equal(default, read);
    }

    [Fact]
    public void ATokenSignedWithAnotherKeyIsRejected()
    {
        var other = new HmacTokenService(new TokenOptions("a-completely-different-signing-key-value"));
        var token = other.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead(token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too-short")]
    [InlineData("a.b.c")]
    public void AMalformedTokenIsRejectedWithoutThrowing(string? token)
    {
        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(default, read);
    }

    /// <summary>
    /// The final base64url character carries only two significant bits; the other three spellings
    /// decode to the same bytes, and accepting them would make one link answer to four URLs.
    /// </summary>
    [Fact]
    public void ATokenWithANonCanonicalEncodingIsRejected()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);
        var index = Alphabet.IndexOf(token[^1], StringComparison.Ordinal);
        var alternate = Alphabet[(index & ~0b11) | ((index + 1) & 0b11)];

        Assert.False(_service.TryRead($"{token[..^1]}{alternate}", out _));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("=")]
    public void ATokenWithACharacterOutsideTheBase64UrlAlphabetIsRejected(string character)
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.False(_service.TryRead($"{character}{token[1..]}", out _));
    }

    [Fact]
    public void TokensOutsideTheCanonicalLengthAreRejected()
    {
        Assert.False(_service.TryRead(new string('A', 10_000), out _));
        Assert.False(_service.TryRead(new string('A', TokenLength - 1), out _));
        Assert.False(_service.TryRead(new string('A', TokenLength + 1), out _));
    }

    [Fact]
    public void TheTokenIsUrlSafeAndOfTheCanonicalLength()
    {
        var token = _service.Issue(TokenPurpose.Book, Guid.NewGuid(), 1);

        Assert.Equal(TokenLength, token.Length);
        Assert.Equal(token, Uri.EscapeDataString(token));
    }

    [Fact]
    public void AShortSigningKeyIsRejectedAtConstruction()
    {
        var ex = Assert.Throws<ArgumentException>(() => new HmacTokenService(new TokenOptions("too-short")));
        Assert.Contains("32", ex.Message);
    }

    [Fact]
    public void OptionsToStringDoesNotRevealTheSigningKey()
    {
        Assert.DoesNotContain(SigningKey, Options.ToString());
    }

    [Fact]
    public void NullOptionsAreRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(null!));

        Assert.Equal("options", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    [Fact]
    public void ARuntimeNullSigningKeyIsRejectedWithANonSecretMessage()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new HmacTokenService(new TokenOptions(null!)));

        Assert.Equal("SigningKey", ex.ParamName);
        Assert.DoesNotContain(SigningKey, ex.Message);
    }

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    private static string CreateKnownKeyToken(byte purpose, Guid id, int version)
    {
        var payload = new byte[21];
        payload[0] = purpose;
        id.TryWriteBytes(payload.AsSpan(1, 16), bigEndian: true, out _);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(17, 4), version);

        var signed = new byte[53];
        payload.CopyTo(signed, 0);
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(SigningKey), payload).CopyTo(signed, 21);

        return Convert.ToBase64String(signed).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
```

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~HmacTokenServiceTests
```

Expected: The suite does not compile: the purpose enum, the reference the reader hands back, the three-argument issue call, and the two version counters do not exist yet, and the four hash-keyed repository methods still do. A Docker startup failure is not the intended failure.

- [ ] **Step 3: Apply the exact implementation and regression edits**

The 32 phase-2a-edits-NNN.md files supply 79 complete before/after changes. The script validates every payload and current file before writing. It accepts an already-applied after state, refuses unrelated edits, and only deletes explicitly listed files whose before hash matches.

```bash
node --input-type=module <<'TASK_PAYLOAD'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root=fs.realpathSync('.'), plan='docs/detailed-implementations';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const names=fs.readdirSync(plan).filter(n=>n.startsWith('phase-2a-edits-')&&n.endsWith('.md')).sort();
if(names.length!==32)throw Error('Incomplete edit volumes.');
const entries=new Map();
for(const name of names){
 const text=fs.readFileSync(path.join(plan,name),'utf8');
 const pattern=/<!-- retirement-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
 for(const match of text.matchAll(pattern)){
  const m=JSON.parse(match[1]);
  if(path.isAbsolute(m.file)||m.file.split('/').includes('..'))throw Error('Unsafe path.');
  const e=entries.get(m.id)??{...m,before:new Map(),after:new Map(),counts:{}};
  if(e.file!==m.file||e.beforeSha!==m.beforeSha||e.afterSha!==m.afterSha||e[m.side].has(m.part))throw Error('Conflicting metadata.');
  e[m.side].set(m.part,match[2]+'\n');e.counts[m.side]=m.parts;entries.set(m.id,e);
 }
}
if(entries.size!==79)throw Error('Incomplete operation set.');
const actions=[];
for(const e of entries.values()){
 for(const side of ['before','after']){
  if(e[side+'Sha']===null)continue;
  if(e[side].size!==e.counts[side])throw Error('Missing parts.');
  const parts=Array.from({length:e.counts[side]},(_,i)=>e[side].get(i+1));
  if(parts.some(p=>p===undefined))throw Error('Missing part number.');
  e[side+'Text']=parts.join('');
  if(sha(e[side+'Text'])!==e[side+'Sha'])throw Error('Payload checksum mismatch.');
 }
 const target=path.join(root,e.file);
 let parent=path.dirname(target);while(!fs.existsSync(parent))parent=path.dirname(parent);
 const resolved=fs.realpathSync(parent);
 if(resolved!==root&&!resolved.startsWith(root+path.sep))throw Error('Parent escapes checkout.');
 if(fs.existsSync(target)&&fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target.');
 const actual=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
 if(actual!==e.beforeSha&&actual!==e.afterSha)throw Error('Unrelated edit: '+e.file);
 actions.push({target,body:e.afterText,remove:e.afterSha===null});
}
for(const action of actions){
 if(action.remove){if(fs.existsSync(action.target))fs.unlinkSync(action.target);}
 else{fs.mkdirSync(path.dirname(action.target),{recursive:true});fs.writeFileSync(action.target,action.body);}
}
console.log('Applied '+actions.length+' verified file changes.');
TASK_PAYLOAD
```

The included migration, designer and model snapshot were generated with this exact command, and are already represented in the supplied after files. Do not generate a duplicate migration:

```bash
dotnet ef migrations add DeterministicAttendeeTokens --project src/EventBooking.Infrastructure --startup-project src/EventBooking.Api
```

Review the complete migration in the edit volumes before running database-dependent tests.

- [ ] **Step 4: Verify the targeted tests pass**

```bash
dotnet test tests/EventBooking.Infrastructure.Tests --filter FullyQualifiedName~HmacTokenServiceTests
```

Expected: all targeted cases pass, with zero skipped tests.

- [ ] **Step 5: Build and validate the complete solution**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings, zero errors and zero failed or skipped tests. The verified checkpoint contains 1541 tests: Domain 360, Application 424, Infrastructure 174, API 232, MCP 35, Web 241 and SeedData 75.

- [ ] **Step 6: Commit and push**

No ontology change belongs to this task. `docs/ontology.ttl` already defines tokenVersion on `Invite` and manageTokenVersion on `Booking`; this task only puts them in code. If you find a concept that is genuinely missing, edit the source and regenerate before committing.

```bash
git add -- \
  'src/EventBooking.Application/Abstractions/IBookingRepository.cs' \
  'src/EventBooking.Application/Abstractions/IInviteRepository.cs' \
  'src/EventBooking.Application/Abstractions/ITokenService.cs' \
  'src/EventBooking.Application/Bookings/CancelBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/ViewBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/ViewInviteHandler.cs' \
  'src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs' \
  'src/EventBooking.Application/Invites/InviteIssuer.cs' \
  'src/EventBooking.Application/Notifications/RetryEmailHandler.cs' \
  'src/EventBooking.Domain/Bookings/Booking.cs' \
  'src/EventBooking.Domain/Invites/Invite.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260920102733_DeterministicAttendeeTokens.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs' \
  'src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs' \
  'src/EventBooking.SeedData/DemoSeeder.cs' \
  'tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AuditEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/BookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs' \
  'tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakeTokenService.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteLocationTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs' \
  'tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs' \
  'tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "feat(security): deterministic attendee tokens with a stored version counter" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Go to Task 9b, which deletes the inherited migration chain — including the one this task added — and writes the fresh schema that carries these two columns from the start.
