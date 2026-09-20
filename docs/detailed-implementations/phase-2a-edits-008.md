# 02a — Deterministic attendee links and the token version counter, edits 8 (Task 9a)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs — 1/1

<!-- retirement-file: {"id":17,"file":"src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs","beforeSha":"d3652adb16434dbee2c5d14c25536ab70aa4d24515f28b434f18db8623d3f0d1","afterSha":"a88d948dd35e76f38cdb2c526816bfd74417b7e79fa6c2a6d9b7d5d2ab6c4847","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Persistence.Repositories;

public sealed class AppointmentTypeRepository(EventBookingDbContext context) : IAppointmentTypeRepository
{
    public async Task<IReadOnlyList<AppointmentType>> ListAsync(CancellationToken cancellationToken) =>
        await context.AppointmentTypes.OrderBy(t => t.Code).ToListAsync(cancellationToken);

    public Task<AppointmentType?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.AppointmentTypes.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
}

public sealed class SystemSettingsRepository(EventBookingDbContext context) : ISystemSettingsRepository
{
    public async Task<SystemSettings> GetAsync(CancellationToken cancellationToken) =>
        await context.SystemSettings.SingleAsync(cancellationToken);
}

/// <summary>Persists proposals and exposes their PostgreSQL lifecycle row lock.</summary>
public sealed class EventProposalRepository(EventBookingDbContext context) : IEventProposalRepository
{
    public Task<EventProposal?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.EventProposals
            .Include(p => p.Acceptances)
            .Include(p => p.ListedTypes)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <summary>Locks the proposal row and then loads its current acceptance collection.</summary>
    public async Task<EventProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var proposal = (await context.EventProposals
            .FromSqlInterpolated($"SELECT * FROM event_proposal WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (proposal is not null)
        {
            await context.Entry(proposal).Collection(item => item.Acceptances).LoadAsync(cancellationToken);
            await context.Entry(proposal).Collection(item => item.ListedTypes).LoadAsync(cancellationToken);
        }

        return proposal;
    }

    public async Task<IReadOnlyList<EventProposal>> ListOpenAsync(CancellationToken cancellationToken) =>
        await context.EventProposals
            .Include(p => p.Acceptances)
            .Include(p => p.ListedTypes)
            .Where(p => p.Status == EventProposalStatus.Open)
            .ToListAsync(cancellationToken);

    public void Add(EventProposal proposal) => context.EventProposals.Add(proposal);
}

public sealed class EventRepository(EventBookingDbContext context) : IEventRepository
{
    public Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Events
            .Include(s => s.Capacities)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<Event?> LockForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await context.Events
            .FromSqlInterpolated(
                $"SELECT * FROM event WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var eventItem = rows.SingleOrDefault();
        if (eventItem is not null)
        {
            await context.Entry(eventItem).Collection(item => item.Capacities).LoadAsync(cancellationToken);
        }

        return eventItem;
    }

    public async Task<IReadOnlyList<Event>> ListActiveAsync(
        DateOnly onOrAfter,
        CancellationToken cancellationToken) =>
        await context.Events
            .Include(s => s.Capacities)
            .Where(s => s.Status == EventStatus.Active && s.Window.Date >= onOrAfter)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Event>> ListAllAsync(
        CancellationToken cancellationToken) =>
        await context.Events
            .Include(s => s.Capacities)
            .ToListAsync(cancellationToken);

    public void Add(Event eventItem) => context.Events.Add(eventItem);
}

/// <summary>Persists attendees and exposes the lifecycle root row lock.</summary>
public sealed class AttendeeRepository(EventBookingDbContext context) : IAttendeeRepository
{
    public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Attendees
            .Include(c => c.Requirements)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <summary>Locks the attendee row and then loads the requirements needed by lifecycle handlers.</summary>
    public async Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var attendee = (await context.Attendees
            .FromSqlInterpolated($"SELECT * FROM attendee WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken))
            .SingleOrDefault();

        if (attendee is not null)
        {
            await context.Entry(attendee).Collection(item => item.Requirements).LoadAsync(cancellationToken);
        }

        return attendee;
    }

    public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        context.Attendees
            .Include(c => c.Requirements)
            .SingleOrDefaultAsync(c => c.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Attendee>> ListAsync(
        AttendeeStatus? status,
        CancellationToken cancellationToken) =>
        await context.Attendees
            .Include(c => c.Requirements)
            .Where(c => status == null || c.Status == status)
            .ToListAsync(cancellationToken);

    public void Add(Attendee attendee) => context.Attendees.Add(attendee);

    public void Remove(Attendee attendee) => context.Attendees.Remove(attendee);
}

/// <summary>Persists invite rows and their option collections, including lifecycle locks.</summary>
public sealed class InviteRepository(EventBookingDbContext context) : IInviteRepository
{
    public Task<Invite?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <summary>Locks the identified invite and loads its offered event IDs.</summary>
    public async Task<Invite?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated($"SELECT * FROM invite WHERE id = {id} FOR UPDATE"),
            cancellationToken);

    /// <summary>Locks the attendee's current pending invite and loads its offered event IDs.</summary>
    public async Task<Invite?> LockPendingForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE attendee_id = {attendeeId} AND status = {(int)InviteStatus.Pending} FOR UPDATE"),
            cancellationToken);

    public Task<Invite?> GetPendingForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .SingleOrDefaultAsync(
                i => i.AttendeeId == attendeeId && i.Status == InviteStatus.Pending,
                cancellationToken);

    /// <summary>Locks the attendee's pending initial invite and loads its offered event IDs.</summary>
    public async Task<Invite?> LockPendingInitialForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        await LockAndLoadOptionsAsync(
            context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE attendee_id = {attendeeId} AND status = {(int)InviteStatus.Pending} AND recovery_of_booking_id IS NULL FOR UPDATE"),
            cancellationToken);

    /// <summary>Locks every pending invite for the attendee in ID order with events loaded.</summary>
    public async Task<IReadOnlyList<Invite>> LockPendingListForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var pending = await context.Invites.FromSqlInterpolated(
                $"SELECT * FROM invite WHERE attendee_id = {attendeeId} AND status = {(int)InviteStatus.Pending} ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);

        foreach (var invite in pending)
        {
            await context.Entry(invite).Collection(item => item.Options).LoadAsync(cancellationToken);
            await context.Entry(invite).Collection(item => item.Requirements).LoadAsync(cancellationToken);
        }

        return pending;
    }

    public async Task<IReadOnlyList<Invite>> ListPendingExpiredAsync(
        DateTimeOffset asAt,
        CancellationToken cancellationToken) =>
        await context.Invites
            .Include(i => i.Options)
            .Include(i => i.Requirements)
            .Where(i => i.Status == InviteStatus.Pending && i.ExpiresAt <= asAt)
            .ToListAsync(cancellationToken);

    public void Add(Invite invite) => context.Invites.Add(invite);

    private async Task<Invite?> LockAndLoadOptionsAsync(
        IQueryable<Invite> query,
        CancellationToken cancellationToken)
    {
        var invite = (await query.ToListAsync(cancellationToken)).SingleOrDefault();
        if (invite is not null)
        {
            await context.Entry(invite).Collection(item => item.Options).LoadAsync(cancellationToken);
            await context.Entry(invite).Collection(item => item.Requirements).LoadAsync(cancellationToken);
        }

        return invite;
    }
}

/// <summary>Persists hash-only attendee email delivery attempts and their safe retry context.</summary>
public sealed class EmailDeliveryRepository(EventBookingDbContext context) : IEmailDeliveryRepository
{
    /// <summary>Loads one delivery without taking a row lock.</summary>
    public Task<EmailLog?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.EmailLogs.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <summary>Locks one delivery row for the claim or outcome transition.</summary>
    public async Task<EmailLog?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await context.EmailLogs
            .FromSqlInterpolated($"SELECT * FROM email_log WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Locks the newest unresolved delivery, or the newest terminal row when none remain.</summary>
    public async Task<EmailLog?> LockLatestForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var rows = await context.EmailLogs
            .FromSqlInterpolated($"""
                SELECT * FROM email_log
                WHERE attendee_id = {attendeeId}
                ORDER BY CASE
                    WHEN status IN ({(int)EmailStatus.Failed}, {(int)EmailStatus.Pending}) THEN 0
                    ELSE 1
                END, sent_at DESC, id DESC
                LIMIT 1
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Reads the latest row for one attendee and template for cancellation recovery.</summary>
    public Task<EmailLog?> GetLatestForAttendeeAsync(
        Guid attendeeId,
        EmailTemplate template,
        CancellationToken cancellationToken) =>
        context.EmailLogs
            .AsNoTracking()
            .Where(e => e.AttendeeId == attendeeId && e.TemplateName == template)
            .OrderBy(e => e.Status == EmailStatus.Failed || e.Status == EmailStatus.Pending ? 0 : 1)
            .ThenByDescending(e => e.SentAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Stages a delivery row on the current context transaction.</summary>
    public void Add(EmailLog delivery) => context.EmailLogs.Add(delivery);
}

/// <summary>Persists booking rows and exposes token and attendee lifecycle locks.</summary>
public sealed class BookingRepository(EventBookingDbContext context) : IBookingRepository
{
    public Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Bookings.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <summary>Locks one booking row before rotating its management-token hash.</summary>
    public async Task<Booking?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated($"SELECT * FROM booking WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    public Task<Guid?> GetEventIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => (Guid?)b.EventId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Reads only the attendee ID used to establish cancellation lock order.</summary>
    public Task<Guid?> GetAttendeeIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => (Guid?)b.AttendeeId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<Booking?> LockByIdForAttendeeAsync(
        Guid bookingId,
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE id = {bookingId} AND attendee_id = {attendeeId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Locks the attendee's active booking, if one remains after the prior locks.</summary>
    public async Task<Booking?> LockActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        await LockActiveOriginalForAttendeeAsync(attendeeId, cancellationToken);

    /// <summary>Locks the attendee's active original booking for lifecycle serialization.</summary>
    public async Task<Booking?> LockActiveOriginalForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE attendee_id = {attendeeId} AND status = {(int)BookingStatus.Active} AND recovery_of_booking_id IS NULL FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>Lists the original and all direct recovery bookings in creation and ID order.</summary>
    public async Task<IReadOnlyList<Booking>> ListJourneyAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == originalBookingId || b.RecoveryOfBookingId == originalBookingId)
            .OrderBy(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Locks the root's active recovery booking for lifecycle serialization.</summary>
    public async Task<Booking?> LockActiveRecoveryAsync(
        Guid originalBookingId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Bookings
            .FromSqlInterpolated(
                $"SELECT * FROM booking WHERE recovery_of_booking_id = {originalBookingId} AND status = {(int)BookingStatus.Active} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    public Task<Booking?> GetActiveForAttendeeAsync(
        Guid attendeeId,
        CancellationToken cancellationToken) =>
        context.Bookings.SingleOrDefaultAsync(
            b => b.AttendeeId == attendeeId && b.Status == BookingStatus.Active,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListActiveAttendeeIdsForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.EventId == eventId && b.Status == BookingStatus.Active)
            .Select(b => b.AttendeeId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> ListActiveForEventAsync(
        Guid eventId,
        CancellationToken cancellationToken) =>
        await context.Bookings
            .Where(b =>
                b.EventId == eventId && b.Status == BookingStatus.Active)
            .ToListAsync(cancellationToken);

    public void Add(Booking booking) => context.Bookings.Add(booking);
}
`````

## before — src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs — 1/1

<!-- retirement-file: {"id":18,"file":"src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs","beforeSha":"eab11b15fe7428bd6fb255926c3018ee4db54857233f517ca53dade0ffe99eaf","afterSha":"2f218d1a7f48bc60a40fed1d6ae5e25463706aa6e886e5160902559e5f819535","side":"before","part":1,"parts":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Tokens;

public sealed class HmacTokenService : ITokenService
{
    private const int MinimumKeyLength = 32;

    /// <summary>
    /// The placeholder signing key shipped in appsettings.json. It passes the length check,
    /// so it is rejected by value: anyone who can read this repository could forge tokens with it.
    /// </summary>
    private const string PlaceholderSigningKey = "replace-this-with-a-real-secret-of-at-least-32-characters";
    private const int NonceBytes = 16;
    private const int GuidLength = 32;
    private const int NonceLength = 22;
    private const int SignatureBytes = 32;
    private const int SignatureLength = 43;
    private const int PayloadLength = GuidLength + 1 + NonceLength;
    private const int TokenLength = PayloadLength + 1 + SignatureLength;

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

    public IssuedToken Issue(Guid entityId)
    {
        var nonce = ToBase64Url(RandomNumberGenerator.GetBytes(NonceBytes));
        var payload = $"{entityId:N}.{nonce}";
        var token = $"{payload}.{Sign(payload)}";

        return new IssuedToken(token, Hash(token));
    }

    public bool TryRead(string? token, out Guid entityId)
    {
        entityId = Guid.Empty;

        if (token is null || token.Length != TokenLength)
        {
            return false;
        }

        if (token[GuidLength] != '.' || token[PayloadLength] != '.')
        {
            return false;
        }

        var identifier = token[..GuidLength];
        if (!Guid.TryParseExact(identifier, "N", out var id) ||
            !string.Equals(identifier, id.ToString("N"), StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryDecodeBase64Url(token.AsSpan(GuidLength + 1, NonceLength), NonceBytes, out _) ||
            !TryDecodeBase64Url(token.AsSpan(PayloadLength + 1, SignatureLength), SignatureBytes, out var supplied))
        {
            return false;
        }

        var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(token[..PayloadLength]));

        // Constant time: a timing difference here would leak how much of a guess was right.
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            return false;
        }

        entityId = id;
        return true;
    }

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private string Sign(string payload) =>
        ToBase64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));

    /// <summary>Base64 with the two characters that are unsafe in a URL replaced, and no padding.</summary>
    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeBase64Url(ReadOnlySpan<char> value, int expectedByteLength, out byte[] decoded)
    {
        decoded = Array.Empty<byte>();

        if (!IsBase64Url(value))
        {
            return false;
        }

        try
        {
            var base64 = value.ToString().Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            decoded = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return false;
        }

        return decoded.Length == expectedByteLength &&
            value.SequenceEqual(ToBase64Url(decoded).AsSpan());
    }

    private static bool IsBase64UrlCharacter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';

    private static bool IsBase64Url(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!IsBase64UrlCharacter(character))
            {
                return false;
            }
        }

        return true;
    }
}
`````

## after — src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs — 1/1

<!-- retirement-file: {"id":18,"file":"src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs","beforeSha":"eab11b15fe7428bd6fb255926c3018ee4db54857233f517ca53dade0ffe99eaf","afterSha":"2f218d1a7f48bc60a40fed1d6ae5e25463706aa6e886e5160902559e5f819535","side":"after","part":1,"parts":1} -->

`````csharp
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
`````

## before — src/EventBooking.SeedData/DemoSeeder.cs — 1/1

<!-- retirement-file: {"id":19,"file":"src/EventBooking.SeedData/DemoSeeder.cs","beforeSha":"dcbb6972415174ecb7bf951700073bedb11fbd7f6955eef5326626f613b454f0","afterSha":"4d68d6e2b3a595e755f3ec53d313c4a74936e442887936195e101a04356c6ec0","side":"before","part":1,"parts":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Attendees;
using EventBooking.Application.Events;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Settings;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.SeedData;

/// <summary>Counts the deterministic rows created by one idempotent seed operation.</summary>
/// <param name="IdentitiesEnsured">The number of missing identity mirror rows inserted.</param>
/// <param name="ProfilesEnsured">The number of missing staff access profiles inserted.</param>
/// <param name="AgreedEventsImported">The number of agreed appointment events imported.</param>
/// <param name="ProposalsEnsured">The number of missing event proposals inserted.</param>
/// <param name="AcceptancesApplied">The number of proposal acceptances applied.</param>
/// <param name="AttendeesCreated">The number of attendees created.</param>
public sealed record SeedSummary(
    int IdentitiesEnsured,
    int ProfilesEnsured,
    int AgreedEventsImported,
    int ProposalsEnsured,
    int AcceptancesApplied,
    int AttendeesCreated)
{
    /// <summary>Gets the number of missing identity mirror rows inserted.</summary>
    public int IdentitiesEnsured { get; init; } = IdentitiesEnsured;

    /// <summary>Gets the number of missing staff access profiles inserted.</summary>
    public int ProfilesEnsured { get; init; } = ProfilesEnsured;

    /// <summary>Gets the number of agreed appointment events imported.</summary>
    public int AgreedEventsImported { get; init; } = AgreedEventsImported;

    /// <summary>Gets the number of missing event proposals inserted.</summary>
    public int ProposalsEnsured { get; init; } = ProposalsEnsured;

    /// <summary>Gets the number of proposal acceptances applied.</summary>
    public int AcceptancesApplied { get; init; } = AcceptancesApplied;

    /// <summary>Gets the number of attendees created.</summary>
    public int AttendeesCreated { get; init; } = AttendeesCreated;
}

public sealed class SeedException(string message) : Exception(message);

/// <summary>
/// Applies <see cref="DemoSeedSpec"/> through the application handlers so every domain rule
/// is enforced exactly as if staff had entered the data by hand. Re-runnable: existing rows
/// are matched by natural key (role id, event window, attendee email) and skipped, which is
/// also how filled-in headcount placeholders get applied on a later run.
/// Reseeding instead wipes every domain table first, restoring the fixed reference rows,
/// so a database mutated by a demo comes back to exactly the seed state.
/// </summary>
/// <param name="profiles">Provides persistence for application access profiles.</param>
/// <param name="identities">Provides persistence for the identity mirror.</param>
/// <param name="events">Provides persistence for confirmed appointment events.</param>
/// <param name="proposals">Provides persistence for event proposals.</param>
/// <param name="attendees">Provides persistence for attendees.</param>
/// <param name="groups">Resolves demo requirement sets to their Attendee Group.</param>
/// <param name="proposeEvent">Creates proposed appointment events.</param>
/// <param name="acceptProposal">Accepts proposed appointment events.</param>
/// <param name="saveAttendee">Creates attendees through the application workflow.</param>
/// <param name="unitOfWork">Commits tracked seed changes.</param>
/// <param name="clock">Supplies the observation time for identity mirror rows.</param>
/// <param name="database">Provides destructive reseed access to the application database.</param>
public sealed class DemoSeeder(
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IEventRepository events,
    IEventProposalRepository proposals,
    IAttendeeRepository attendees,
    IAttendeeGroupRepository groups,
    ProposeEventHandler proposeEvent,
    AcceptProposalHandler acceptProposal,
    SaveAttendeeHandler saveAttendee,
    IUnitOfWork unitOfWork,
    IClock clock,
    EventBookingDbContext database)
{
    /// <summary>
    /// Gets or sets the verbose progress sink. Defaults to <see cref="TextWriter.Null" />;
    /// the console host assigns <see cref="Console.Out" /> when --verbose is passed.
    /// </summary>
    public TextWriter Progress { get; set; } = TextWriter.Null;

    private void Report(string message) => Progress.WriteLine($"[seed] {message}");

    private static IReadOnlyList<StaffProfileSpec> Staff() => DemoSeedSpec.Staff();

    private static IReadOnlyDictionary<Guid, Guid> ManagerForType() => DemoSeedSpec.ManagerForType();

    private static Guid AdminUserId() => DemoSeedSpec.AdminUserId();

    private static Guid CoordinatorUserId() => DemoSeedSpec.CoordinatorUserId();

    public async Task<SeedSummary> RunAsync(CancellationToken cancellationToken)
    {
        Report(
            $"Starting seed: {Staff().Count} staff, " +
            $"{DemoSeedSpec.AgreedEvents().Count} agreed events, " +
            $"{DemoSeedSpec.OpenProposals().Count} proposals, " +
            $"{DemoSeedSpec.Attendees().Count} attendees " +
            $"(anchor {DemoSeedSpec.AnchorDate():yyyy-MM-dd}" +
            $"{(DemoSeedSpec.AnchorOverridden ? ", reanchored" : "")}, " +
            $"today {clock.TodayAtTransitionalLocation:yyyy-MM-dd}).");
        var identitiesEnsured = await EnsureIdentitiesAsync(cancellationToken);
        var profilesEnsured = await EnsureProfilesAsync(cancellationToken);
        var agreedImported = await SeedAgreedEventsAsync(cancellationToken);
        var (proposalsEnsured, acceptancesApplied) = await SeedProposalsAsync(cancellationToken);
        var attendeesCreated = await SeedAttendeesAsync(cancellationToken);
        Report(
            $"Seed run finished: {identitiesEnsured} identities, {profilesEnsured} profiles, " +
            $"{agreedImported} agreed events, {proposalsEnsured} proposals, " +
            $"{acceptancesApplied} acceptances, {attendeesCreated} attendees.");

        return new SeedSummary(
            identitiesEnsured,
            profilesEnsured,
            agreedImported,
            proposalsEnsured,
            acceptancesApplied,
            attendeesCreated);
    }

    public async Task<SeedSummary> ReseedAsync(CancellationToken cancellationToken)
    {
        await WipeAsync(cancellationToken);
        return await RunAsync(cancellationToken);
    }

    private async Task WipeAsync(CancellationToken cancellationToken)
    {
        Report("Wiping domain tables (reseed)...");
        await database.Database.ExecuteSqlRawAsync(
            """
            DO $$
            DECLARE statements CURSOR FOR
                SELECT tablename FROM pg_tables
                WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory';
            BEGIN
                FOR statement IN statements LOOP
                    EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                END LOOP;
            END $$;
            """,
            cancellationToken);

        // The truncate above bypasses the change tracker, so drop every stale
        // tracked entity before seeding into the emptied tables.
        database.ChangeTracker.Clear();

        database.AppointmentTypes.AddRange(AppointmentType.CreateFixedSet());
        database.SystemSettings.Add(SystemSettings.CreateDefault());
        database.AttendeeGroups.AddRange(FixedAttendeeGroups());
        await database.SaveChangesAsync(cancellationToken);
        Report("Wipe complete; reference rows restored.");
    }

    /// <summary>
    /// The five change-controlled groups, matching the reference migration exactly. Task 17
    /// replaces demo attendee assignment with explicit groups and journeys.
    /// </summary>
    private static IReadOnlyList<AttendeeGroup> FixedAttendeeGroups() =>
    [
        AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]),
        AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
        AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]),
        AttendeeGroup.Define(
            AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
            [AppointmentTypeIds.MedicalCheckUp]),
        AttendeeGroup.Define(
            AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
            "Ground Transport Services", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]),
    ];

    private async Task<int> EnsureProfilesAsync(CancellationToken cancellationToken)
    {
        var added = 0;
        var skipped = 0;
        foreach (var spec in Staff())
        {
            if (await profiles.GetAsync(spec.UserId, cancellationToken) is not null)
            {
                skipped++;
                continue;
            }

            profiles.Add(StaffAccessProfile.Create(
                spec.UserId, spec.Roles, spec.AppointmentTypeId));
            added++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        Report($"Staff profiles: {added} created, {skipped} already present.");
        return added;
    }

    private async Task<int> EnsureIdentitiesAsync(CancellationToken cancellationToken)
    {
        var existing = (await identities.ListAsync(cancellationToken))
            .Select(identity => identity.StaffUserId)
            .ToHashSet();
        var added = 0;
        foreach (var spec in Staff())
        {
            if (existing.Contains(spec.UserId))
            {
                continue;
            }

            await identities.UpsertAsync(
                spec.UserId, spec.StaffId, null, clock.UtcNow, cancellationToken);
            added++;
            Report($"Identity ensured: {spec.UserId}.");
        }

        Report($"Staff identities: {added} ensured, {existing.Count} already present.");
        return added;
    }

    private async Task<int> SeedAgreedEventsAsync(CancellationToken cancellationToken)
    {
        var existing = (await events.ListAllAsync(cancellationToken))
            .Select(s => (s.Window.Date, s.Window.StartTime))
            .ToHashSet();

        var missing = DemoSeedSpec.AgreedEvents()
            .Where(s => !existing.Contains((s.Date, s.StartTime)))
            .ToList();

        if (missing.Count == 0)
        {
            Report("Agreed events: none missing.");
            return 0;
        }

        foreach (var eventItem in missing)
        {
            Report($"Agreed event missing, will create with an accepted proposal: {eventItem.Date:yyyy-MM-dd} {eventItem.StartTime:HH\\:mm}.");
        }

        foreach (var item in missing)
        {
            DemoEventFactory.Create(database, Guid.NewGuid(), new EventWindow(item.Date, item.StartTime, 240),
                new Dictionary<Guid, int>
                {
                    [AppointmentTypeIds.DrugAndAlcoholTesting] = item.DatHeadcount,
                    [AppointmentTypeIds.MedicalCheckUp] = item.MedHeadcount,
                    [AppointmentTypeIds.UniformFitting] = item.UniHeadcount,
                });
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Report($"Agreed events: created {missing.Count} with accepted proposals.");
        return missing.Count;
    }

    private async Task<(int ProposalsEnsured, int AcceptancesApplied)> SeedProposalsAsync(
        CancellationToken cancellationToken)
    {
        var ensured = 0;
        var acceptances = 0;

        foreach (var spec in DemoSeedSpec.OpenProposals())
        {
            var open = await proposals.ListOpenAsync(cancellationToken);
            var match = open.FirstOrDefault(p =>
                p.Window.Date == spec.Date && p.Window.StartTime == spec.StartTime);

            Guid proposalId;
            if (match is not null)
            {
                proposalId = match.Id;
                Report($"Proposal already open: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
            }
            else
            {
                var confirmedWindows = (await events.ListAllAsync(cancellationToken))
                    .Select(s => (s.Window.Date, s.Window.StartTime))
                    .ToHashSet();
                if (confirmedWindows.Contains((spec.Date, spec.StartTime)))
                {
                    Report($"Proposal already confirmed, skipping: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
                    continue;
                }

                Report($"Proposing event: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} (today is {clock.TodayAtTransitionalLocation:yyyy-MM-dd}).");
                var proposed = await proposeEvent.HandleAsync(
                    new ProposeEventCommand(spec.CreatedByManagerUserId, spec.Date, spec.StartTime),
                    cancellationToken);
                if (proposed.IsFailure)
                {
                    throw new SeedException(
                        $"Proposing {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} failed: {proposed.Error}.");
                }

                proposalId = proposed.Value;
                ensured++;
                Report($"Proposed event: {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
            }

            var headcounts = new Dictionary<Guid, int?>();
            if (spec.DatHeadcount is not null)
            {
                headcounts[AppointmentTypeIds.DrugAndAlcoholTesting] = spec.DatHeadcount;
            }

            if (spec.MedHeadcount is not null)
            {
                headcounts[AppointmentTypeIds.MedicalCheckUp] = spec.MedHeadcount;
            }

            if (spec.UniHeadcount is not null)
            {
                headcounts[AppointmentTypeIds.UniformFitting] = spec.UniHeadcount;
            }

            foreach (var (typeId, headcount) in headcounts)
            {
                var accepted = await acceptProposal.HandleAsync(
                    new AcceptProposalCommand(ManagerForType()[typeId], proposalId, headcount!.Value),
                    cancellationToken);
                if (accepted.IsFailure)
                {
                    throw new SeedException(
                        $"Accepting {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm} failed: {accepted.Error}.");
                }

                acceptances++;
                Report($"Accepted headcount for {spec.Date:yyyy-MM-dd} {spec.StartTime:HH\\:mm}.");
            }
        }

        Report($"Proposals: {ensured} newly proposed, {acceptances} headcounts accepted.");
        return (ensured, acceptances);
    }

    /// <summary>Gets the stable journey-event identifier for a deterministic demo window.</summary>
    private static Guid JourneyEventId(string name) => Guid.Parse($"d0000000-0000-0000-0000-{name}");

    private async Task EnsureJourneyEventsAsync(CancellationToken cancellationToken)
    {
        var anchor = DemoSeedSpec.AnchorDate();
        var windows = new (string Suffix, DateOnly Date, TimeOnly Start)[]
        {
            ("000000000001", anchor, new TimeOnly(9, 0)),
            ("000000000002", anchor.AddDays(7), new TimeOnly(9, 0)),
            ("000000000003", anchor, new TimeOnly(13, 0)),
            ("000000000004", anchor.AddDays(-7), new TimeOnly(9, 0)),
            ("000000000005", anchor.AddDays(-14), new TimeOnly(9, 0)),
            ("000000000006", anchor, new TimeOnly(15, 0)),
        };

        var added = 0;
        foreach (var (suffix, date, start) in windows)
        {
            var id = JourneyEventId(suffix);
            if (await events.GetAsync(id, cancellationToken) is not null)
            {
                continue;
            }

            DemoEventFactory.Create(database,
                id,
                new EventWindow(date, start, 240),
                AppointmentTypeIds.All.ToDictionary(typeId => typeId, _ => 20));
            added++;
            Report($"Journey event created: {date:yyyy-MM-dd} {start:HH\\:mm}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        Report($"Journey events: {added} created, {windows.Length - added} already present.");
    }

    /// <summary>Derives one stable identifier from a attendee email and a seed role name.</summary>
    private static Guid SeedId(string email, string role) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{email}:{role}")));

    private async Task<int> SeedAttendeesAsync(CancellationToken cancellationToken)
    {
        await EnsureJourneyEventsAsync(cancellationToken);

        var created = 0;
        var skipped = 0;
        var position = 0;
        foreach (var spec in DemoSeedSpec.Attendees())
        {
            if (await attendees.GetByEmailAsync(spec.Email, cancellationToken) is not null)
            {
                skipped++;
                position++;
                continue;
            }

            var group = await groups.GetByCodeAsync(spec.AttendeeGroupCode, cancellationToken)
                ?? throw new SeedException(
                    $"Employee group '{spec.AttendeeGroupCode}' for {spec.Email} is not seeded.");

            var result = await saveAttendee.CreateAsync(
                new CreateAttendeeCommand(
                    CoordinatorUserId(), spec.Name, spec.Email, group.Id),
                cancellationToken);
            if (result.IsFailure)
            {
                throw new SeedException($"Creating attendee {spec.Email} failed: {result.Error}.");
            }

            var attendee = await attendees.GetAsync(result.Value, cancellationToken)
                ?? throw new SeedException($"Seeded attendee {spec.Email} is missing.");
            await BuildJourneyAsync(attendee, group, spec.Journey, position, cancellationToken);
            Report($"Attendee created: {spec.Email} ({spec.AttendeeGroupCode}, {spec.Journey}).");

            created++;
            position++;
        }

        Report($"Attendees: {created} created, {skipped} already present.");
        return created;
    }

    /// <summary>Constructs the requested deterministic lifecycle journey through domain factories.</summary>
    private async Task BuildJourneyAsync(
        Attendee attendee,
        AttendeeGroup group,
        DemoAttendeeJourney journey,
        int position,
        CancellationToken cancellationToken)
    {
        var types = group.RequiredAppointmentTypeIds.Order().ToList();
        var coordinator = CoordinatorUserId();
        var now = clock.UtcNow;
        var today = clock.TodayAtTransitionalLocation;

        switch (journey)
        {
            case DemoAttendeeJourney.Unbooked:
                return;
            case DemoAttendeeJourney.Ready:
                {
                    var eventDate = EventDate(JourneyEventId("000000000001"));
                    var appointments = await SeedBookingAsync(
                        attendee, types, JourneyEventId("000000000001"), "initial", now, cancellationToken);
                    foreach (var appointment in appointments)
                    {
                        appointment.TransitionTo(
                            BookingAppointmentStatus.CheckedIn, coordinator, now,
                            checkInAllowed: eventDate == today, noShowAllowed: false);
                        appointment.TransitionTo(
                            BookingAppointmentStatus.Completed, coordinator, now,
                            checkInAllowed: true, noShowAllowed: false);
                    }

                    break;
                }

            case DemoAttendeeJourney.Outstanding:
                {
                    if (position % 2 == 0)
                    {
                        var appointments = await SeedBookingAsync(
                            attendee, types, JourneyEventId("000000000003"), "initial", now, cancellationToken);
                        appointments[0].TransitionTo(
                            BookingAppointmentStatus.CheckedIn, coordinator, now,
                            checkInAllowed: EventDate(JourneyEventId("000000000003")) == today,
                            noShowAllowed: false);
                    }
                    else
                    {
                        await SeedBookingAsync(
                            attendee, types, JourneyEventId("000000000002"), "initial", now, cancellationToken);
                    }

                    break;
                }

            case DemoAttendeeJourney.NoShow:
                {
                    var appointments = await SeedBookingAsync(
                        attendee, types, JourneyEventId("000000000004"), "initial", now, cancellationToken);
                    appointments[0].TransitionTo(
                        BookingAppointmentStatus.NoShow, coordinator, now,
                        checkInAllowed: false,
                        noShowAllowed: EventDate(JourneyEventId("000000000004")) < today);
                    break;
                }

            case DemoAttendeeJourney.RecoveryCompleted:
                {
                    var original = await SeedBookingAsync(
                        attendee, types, JourneyEventId("000000000005"), "initial", now, cancellationToken);
                    var originalBooking = database.Bookings.Single(b =>
                        b.AttendeeId == attendee.Id && b.RecoveryOfBookingId == null);
                    original[0].TransitionTo(
                        BookingAppointmentStatus.NoShow, coordinator, now,
                        checkInAllowed: false,
                        noShowAllowed: EventDate(JourneyEventId("000000000005")) < today);

                    var recoveryEventId = JourneyEventId("000000000006");
                    var recoveryInvite = Invite.CreateRecovery(
                        SeedId(attendee.Email, "recovery:invite"),
                        attendee.Id,
                        originalBooking.Id,
                        $"seed-recovery-{position}",
                        now.AddDays(7),
                        TransitionalLocation.Id,
                        null,
                        [recoveryEventId, SeedId(attendee.Email, "recovery:spare1"),
                        SeedId(attendee.Email, "recovery:spare2")],
                        [types[0]]);
                    database.Invites.Add(recoveryInvite);
                    var recovery = Booking.CreateRecovery(
                        SeedId(attendee.Email, "recovery:booking"),
                        recoveryInvite,
                        originalBooking,
                        recoveryEventId,
                        $"seed-recovery-manage-{position}",
                        now.AddMinutes(5));
                    recoveryInvite.MarkUsed();
                    database.Bookings.Add(recovery);
                    var recoveryAppointment = BookingAppointment.Create(
                        SeedId(attendee.Email, "recovery:appointment"),
                        recovery.Id,
                        types[0]);
                    recoveryAppointment.TransitionTo(
                        BookingAppointmentStatus.CheckedIn, coordinator, now,
                        checkInAllowed: EventDate(recoveryEventId) == today, noShowAllowed: false);
                    recoveryAppointment.TransitionTo(
                        BookingAppointmentStatus.Completed, coordinator, now,
                        checkInAllowed: true, noShowAllowed: false);
                    database.BookingAppointments.Add(recoveryAppointment);
                    break;
                }

            default:
                throw new SeedException($"Demo journey {journey} is not supported.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private DateOnly EventDate(Guid eventId) =>
        database.Events.Single(eventItem => eventItem.Id == eventId).Window.Date;

    /// <summary>Seeds one used initial invite, booking, and appointment row per required type.</summary>
    /// <returns>The created appointments in requirement order.</returns>
    private async Task<IReadOnlyList<BookingAppointment>> SeedBookingAsync(
        Attendee attendee,
        IReadOnlyList<Guid> types,
        Guid eventId,
        string tag,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        var invite = Invite.CreateInitial(
            SeedId(attendee.Email, $"{tag}:invite"),
            attendee.Id,
            $"seed-{tag}-{attendee.Email}",
            createdAt.AddDays(7),
            [TransitionalLocation.Id],
            [eventId, SeedId(attendee.Email, $"{tag}:spare1"), SeedId(attendee.Email, $"{tag}:spare2")],
            types,
            0);
        database.Invites.Add(invite);
        var booking = Booking.Create(
            SeedId(attendee.Email, $"{tag}:booking"),
            invite,
            eventId,
            $"seed-{tag}-manage-{attendee.Email}",
            createdAt);
        invite.MarkUsed();
        database.Bookings.Add(booking);

        var appointments = types
            .Select(typeId => BookingAppointment.Create(
                SeedId(attendee.Email, $"{tag}:appointment:{typeId}"),
                booking.Id,
                typeId))
            .ToList();
        database.BookingAppointments.AddRange(appointments);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return appointments;
    }
}
`````
