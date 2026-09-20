# 00a — Port source 73 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs","encoding":"utf8","sha256":"3b27fc5b3bd92a69af2dfe6f1add39eaad4d4e6a441eaf5c531ff382de0aea85","parts":1,"part":1} -->

`````csharp
using System.Security.Cryptography;
using System.Text;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Infrastructure.Tests;

public class HmacTokenServiceTests
{
    private const string SigningKey = "a-signing-key-that-is-long-enough-to-be-safe";

    private static readonly TokenOptions Options =
        new(SigningKey);

    private readonly HmacTokenService _service = new(Options);

    [Fact]
    public void AnIssuedTokenRoundTripsToItsIdentifier()
    {
        var id = Guid.NewGuid();

        var issued = _service.Issue(id);

        Assert.True(_service.TryRead(issued.Token, out var read));
        Assert.Equal(id, read);
    }

    [Fact]
    public void TheStoredValueIsAHashNotTheToken()
    {
        var issued = _service.Issue(Guid.NewGuid());

        Assert.NotEqual(issued.Token, issued.TokenHash);
        Assert.Equal(issued.TokenHash, _service.Hash(issued.Token));
        Assert.Equal(64, issued.TokenHash.Length);
        Assert.DoesNotContain(issued.TokenHash, issued.Token);
    }

    [Fact]
    public void TwoTokensForTheSameIdentifierAreDifferent()
    {
        var id = Guid.NewGuid();

        var first = _service.Issue(id);
        var second = _service.Issue(id);

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.TokenHash, second.TokenHash);
        Assert.True(_service.TryRead(first.Token, out var a));
        Assert.True(_service.TryRead(second.Token, out var b));
        Assert.Equal(a, b);
    }

    [Fact]
    public void ATamperedIdentifierFailsVerification()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');
        var forged = $"{Guid.NewGuid():N}.{parts[1]}.{parts[2]}";

        Assert.False(_service.TryRead(forged, out _));
    }

    [Fact]
    public void ATamperedSignatureFailsVerification()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');

        Assert.False(_service.TryRead($"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}", out _));
    }

    [Fact]
    public void ATokenSignedWithAnotherKeyIsRejected()
    {
        var other = new HmacTokenService(new TokenOptions("a-completely-different-signing-key-value"));
        var issued = other.Issue(Guid.NewGuid());

        Assert.False(_service.TryRead(issued.Token, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("one-part")]
    [InlineData("two.parts")]
    [InlineData("a.b.c.d")]
    [InlineData("not-a-guid.nonce.signature")]
    public void AMalformedTokenIsRejectedWithoutThrowing(string? token)
    {
        Assert.False(_service.TryRead(token, out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void TheTokenIsUrlSafe()
    {
        var token = _service.Issue(Guid.NewGuid()).Token;

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

    [Fact]
    public void ATokenWithAnUppercaseIdentifierIsRejectedEvenWhenSignedWithTheKnownKey()
    {
        var id = Guid.NewGuid();
        var nonce = "AAAAAAAAAAAAAAAAAAAAAA";
        var token = CreateKnownKeyToken(id.ToString("N").ToUpperInvariant(), nonce);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Theory]
    [InlineData("!AAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAB")]
    public void ATokenWithANonCanonicalNonceIsRejectedEvenWhenSignedWithTheKnownKey(string nonce)
    {
        var token = CreateKnownKeyToken(Guid.NewGuid().ToString("N"), nonce);

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Theory]
    [InlineData("invalid-character")]
    [InlineData("padding")]
    [InlineData("wrong-length")]
    public void ATokenWithAMalformedSignatureEncodingIsRejected(string kind)
    {
        var issued = _service.Issue(Guid.NewGuid());
        var parts = issued.Token.Split('.');
        var malformedSignature = kind switch
        {
            "invalid-character" => $"!{parts[2][1..]}",
            "padding" => $"{parts[2][..^1]}=",
            "wrong-length" => parts[2][..^1],
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.False(_service.TryRead($"{parts[0]}.{parts[1]}.{malformedSignature}", out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Fact]
    public void ATokenWithANonCanonicalSignatureIsRejected()
    {
        var issued = _service.Issue(Guid.NewGuid());
        var finalCharacter = issued.Token[^1];
        const string base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        var alternateCharacter = base64UrlAlphabet[base64UrlAlphabet.IndexOf(finalCharacter) + 1];
        var token = $"{issued.Token[..^1]}{alternateCharacter}";

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    [Fact]
    public void TokensOutsideTheCanonicalLengthAreRejected()
    {
        var token = CreateKnownKeyToken(Guid.NewGuid().ToString("N"), new string('A', 10_000));

        Assert.False(_service.TryRead(token, out var read));
        Assert.Equal(Guid.Empty, read);
    }

    private static string CreateKnownKeyToken(string identifier, string nonce)
    {
        var payload = $"{identifier}.{nonce}";
        var signature = ToBase64Url(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(SigningKey),
            Encoding.UTF8.GetBytes(payload)));

        return $"{payload}.{signature}";
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
`````

## tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs","encoding":"utf8","sha256":"d35b7e65a4bd14839e2927e5e7c0fc9abac9f4ebeb30a3bc228759310c03afcb","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies Invite requirement snapshots round-trip with restrictive constraints.</summary>
[Collection("postgres")]
public sealed class InviteRequirementPersistenceTests(PostgresFixture fixture)
{
    /// <summary>An Invite reloads options, requirements, and recovery linkage together.</summary>
    [Fact]
    public async Task SnapshotRoundTrips()
    {
        await fixture.ResetAsync();
        var group = EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var candidate = Candidate.Create(Guid.NewGuid(), "Amara", "amara@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, "hash", DateTimeOffset.UtcNow.AddDays(1),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], candidate.RequiredAppointmentTypeIds, 0);

        await using (var write = fixture.NewContext())
        {
            write.Candidates.Add(candidate);
            write.Invites.Add(invite);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var actual = await read.Invites
            .Include(value => value.Options)
            .Include(value => value.Requirements)
            .SingleAsync(value => value.Id == invite.Id);
        Assert.Equal(3, actual.Options.Count);
        Assert.Equal(candidate.RequiredAppointmentTypeIds, actual.RequiredAppointmentTypeIds);
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/LocalInfrastructureExtensionsTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/LocalInfrastructureExtensionsTests.cs","encoding":"utf8","sha256":"4d19e962b3676fcb60913aba2509268e9bba2761e3707af5247b0f8e49405b97","parts":1,"part":1} -->

`````csharp
using EventBooking.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure.Tests;

public class LocalInfrastructureExtensionsTests
{
    [Fact]
    public void AddLocalEmailTransportRegistersTheSmtpTransport()
    {
        var services = new ServiceCollection();

        services.AddLocalEmailTransport(
            new EmailOptions("recruitment@example.com", "Recruitment Team", EmailProvider.Smtp),
            new SmtpOptions("localhost", 1025));

        var provider = services.BuildServiceProvider();
        Assert.IsType<SmtpEmailTransport>(provider.GetRequiredService<IEmailTransport>());
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs","encoding":"utf8","sha256":"881b1305b7bc47f000501cc7c0182de94d45c9f0ddb3b7bf49b8a78b2931db4b","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public class LoggingEmailSenderTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("not-an-email", "EventBooking")]
    [InlineData("sender@example.com", " ")]
    public void SenderOptionsRejectIncompleteValues(string fromAddress, string fromName)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new EmailOptions(fromAddress, fromName, EmailProvider.Smtp));

        if (!string.IsNullOrWhiteSpace(fromAddress))
        {
            Assert.DoesNotContain(fromAddress, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SenderOptionsDoNotExposeTheirValuesWhenFormatted()
    {
        var options = new EmailOptions("sender@example.com", "EventBooking", EmailProvider.Smtp);

        var formatted = options.ToString();

        Assert.DoesNotContain(options.FromAddress, formatted, StringComparison.Ordinal);
        Assert.DoesNotContain(options.FromName, formatted, StringComparison.Ordinal);
    }

    private sealed class FakeTransport : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];

        public bool Throw { get; set; }

        public bool Cancel { get; set; }

        public Action? AfterSend { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Cancel)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (Throw)
            {
                throw new InvalidOperationException("the provider rejected the message");
            }

            Sent.Add(message);
            AfterSend?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        /// <summary>Gets the fixed instant; this clock treats UTC as head-office time.</summary>
        public DateTimeOffset NowAtHeadOffice => now;

        public DateOnly TodayAtHeadOffice => DateAtHeadOffice(now);

        public DateOnly DateAtHeadOffice(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

        public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant.ToUniversalTime();
    }

    [Fact]
    public async Task ASuccessfulSendIsLoggedAsSent()
    {
        var (sender, transport, candidateId) = await Given();

        var sent = await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        Assert.True(sent);
        Assert.Single(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(candidateId, log.CandidateId);
        Assert.Equal(EmailTemplate.CandidateInvite, log.TemplateName);
        Assert.Equal(EmailStatus.Sent, log.Status);
    }

    [Fact]
    public async Task AFailedSendReturnsFalseAndIsLoggedAsFailed()
    {
        var (sender, transport, candidateId) = await Given();
        transport.Throw = true;

        var sent = await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        Assert.False(sent);
        Assert.Empty(transport.Sent);

        await using var context = fixture.NewContext();
        var log = await context.EmailLogs.SingleAsync();
        Assert.Equal(EmailStatus.Failed, log.Status);
    }

    [Fact]
    public async Task ThePublishedTimeComesFromTheClock()
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var (sender, _, candidateId) = await Given(now);

        await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        await using var context = fixture.NewContext();
        Assert.Equal(now, (await context.EmailLogs.SingleAsync()).SentAt);
    }

    [Fact]
    public async Task ACancelledTransportPropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, candidateId) = await Given();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        transport.Cancel = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(candidateId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task ACancelledAuditSavePropagatesCancellationWithoutWritingAnEmailLog()
    {
        var (sender, transport, candidateId) = await Given();
        using var cancellation = new CancellationTokenSource();
        transport.AfterSend = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.SendAsync(MessageFor(candidateId), cancellation.Token));

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    [Fact]
    public async Task AnAuditContextFailureIsWarnedAndDoesNotFailTheSend()
    {
        var (_, _, candidateId) = await Given();
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(
            new FakeTransport(),
            new ThrowingContextFactory(),
            new FixedClock(DateTimeOffset.UtcNow),
            logger);

        var sent = await sender.SendAsync(MessageFor(candidateId), CancellationToken.None);

        Assert.True(sent);
        Assert.Contains(LogLevel.Warning, logger.LogLevels);

        await using var context = fixture.NewContext();
        Assert.Empty(await context.EmailLogs.ToListAsync());
    }

    private async Task<(LoggingEmailSender Sender, FakeTransport Transport, Guid CandidateId)> Given(
        DateTimeOffset? now = null)
    {
        await fixture.ResetAsync();

        var candidateId = Guid.NewGuid();
        await using (var write = fixture.NewContext())
        {
            var pilots = write.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == EmployeeGroupIds.Pilots);
            var candidate = Candidate.Create(
                candidateId, "Amara Novak", "a.novak@mail.com", pilots);
            write.Candidates.Add(candidate);
            await write.SaveChangesAsync();
        }

        var transport = new FakeTransport();
        var sender = new LoggingEmailSender(
            transport,
            new TestContextFactory(fixture),
            new FixedClock(now ?? DateTimeOffset.UtcNow),
            NullLogger<LoggingEmailSender>.Instance);

        return (sender, transport, candidateId);
    }

    private static EmailMessage MessageFor(Guid candidateId) =>
        new(candidateId, "a.novak@mail.com", "Amara Novak", EmailTemplate.CandidateInvite,
            "Choose a time", "text", "<html></html>");

    private sealed class TestContextFactory(PostgresFixture fixture)
        : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() => fixture.NewContext();
    }

    private sealed class ThrowingContextFactory : IDbContextFactory<EventBookingDbContext>
    {
        public EventBookingDbContext CreateDbContext() =>
            throw new InvalidOperationException("the audit database is unavailable");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> LogLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevels.Add(logLevel);
        }
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs","encoding":"utf8","sha256":"95442f57cd3f6efad95b2bf15696344e9654dae57d2ae71fff0569587580551c","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// The claim: two candidates can never both take the last place. These tests run the real confirm
/// handler over the real database from many threads, each on its own connection.
/// </summary>
[Collection("postgres")]
public class NoOverbookingTests(PostgresFixture fixture)
{
    private static readonly Guid DrugAndAlcohol = AppointmentTypeIds.DrugAndAlcoholTesting;
    private static readonly Guid Uniform = AppointmentTypeIds.UniformFitting;
    private static readonly Guid Medical = AppointmentTypeIds.MedicalCheckUp;

    [Fact]
    public async Task TenCandidatesRacingForOnePlaceProduceExactlyOneBooking()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 1, medical: 50, uniform: 50);

        var tokens = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, DrugAndAlcohol));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(9, results.Count(r => r.IsFailure));
        Assert.All(results.Where(r => r.IsFailure), r => Assert.Equal("conflict", r.Error.Code));

        Assert.Equal(0, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
        Assert.Equal(1, await harness.ActiveBookingCountAsync(slotId));
    }

    [Fact]
    public async Task ThirtyCandidatesRacingForTenPlacesProduceExactlyTenBookings()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 10, medical: 50, uniform: 50);

        var tokens = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, DrugAndAlcohol));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(10, results.Count(r => r.IsSuccess));
        Assert.Equal(0, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
        Assert.Equal(10, await harness.ActiveBookingCountAsync(slotId));
    }

    [Fact]
    public async Task ACandidateNeedingTwoTypesIsStoppedByWhicheverRunsOutFirst()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        // Plenty of room for drug and alcohol, exactly one uniform place.
        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 50, medical: 50, uniform: 1);

        var tokens = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, DrugAndAlcohol, Uniform));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        Assert.Equal(0, await harness.RemainingCapacityAsync(slotId, Uniform));

        // The plentiful counter must have moved exactly once, not once per attempt.
        Assert.Equal(49, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
    }

    [Fact]
    public async Task CandidatesNeedingDifferentTypeCombinationsDoNotDeadlock()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var slotId = await harness.GivenSlotAsync(drugAndAlcohol: 20, medical: 20, uniform: 20);

        var tokens = new List<string>();
        for (var i = 0; i < 18; i++)
        {
            // Three different combinations, deliberately listed in different orders. The shared
            // ConfirmedSlot guard serializes same-slot mutations; capacity-row ordering is proven
            // directly by the existing repository and transaction-lock tests.
            Guid[] required = (i % 3) switch
            {
                0 => [Uniform, DrugAndAlcohol],
                1 => [Medical, Uniform],
                _ => [DrugAndAlcohol, Medical],
            };

            tokens.Add(await harness.GivenInvitedCandidateAsync(slotId, required));
        }

        var batch = await harness.ConfirmBatchAsync(tokens, slotId);
        var results = batch.Results;

        Assert.Equal(tokens.Count, batch.BlockedAttemptCount);

        // Capacity is ample, so every attempt should succeed. A deadlock would show up as a
        // failure here, not as a hang: PostgreSQL kills one side of a deadlock.
        Assert.Equal(18, results.Count(r => r.IsSuccess));
        Assert.Equal(18, await harness.ActiveBookingCountAsync(slotId));

        Assert.Equal(8, await harness.RemainingCapacityAsync(slotId, DrugAndAlcohol));
        Assert.Equal(8, await harness.RemainingCapacityAsync(slotId, Medical));
        Assert.Equal(8, await harness.RemainingCapacityAsync(slotId, Uniform));
    }

    [Fact]
    public async Task ARaceLosersInviteKeepsThreeLiveOptionsWhenAReplacementExists()
    {
        await using var harness = await ConcurrencyHarness.CreateAsync(fixture);

        var contested = await harness.GivenSlotAsync(drugAndAlcohol: 1, medical: 50, uniform: 50);
        var replacement = await harness.GivenSlotAsync(drugAndAlcohol: 50, medical: 50, uniform: 50);

        var winner = await harness.GivenInvitedCandidateAsync(contested, DrugAndAlcohol);
        var loser = await harness.GivenInvitedCandidateAsync(contested, DrugAndAlcohol);

        var tokens = new[] { winner, loser };
        var batch = await harness.ConfirmBatchAsync(tokens, contested);
        var results = batch.Results;

        Assert.Equal(tokens.Length, batch.BlockedAttemptCount);

        Assert.Equal(1, results.Count(r => r.IsSuccess));

        var failure = results.Single(r => r.IsFailure);
        Assert.Equal(
            "That time filled up while you were choosing. Please pick from the updated options.",
            failure.Error.Message);

        Assert.Equal(0, await harness.RemainingCapacityAsync(contested, DrugAndAlcohol));
        Assert.Equal(1, await harness.ActiveBookingCountAsync(contested));

        var loserToken = tokens[Enumerable.Range(0, results.Count)
            .Single(index => results[index].IsFailure)];
        var liveOptions = await harness.LiveOptionSlotIdsAsync(loserToken);

        Assert.Equal(3, liveOptions.Count);
        Assert.Equal(3, liveOptions.Distinct().Count());
        Assert.DoesNotContain(contested, liveOptions);
        Assert.Contains(replacement, liveOptions);
        Assert.All(harness.FallbackSlotIds, fallback => Assert.Contains(fallback, liveOptions));
    }
}
`````

## tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs","encoding":"utf8","sha256":"5d8f77671859f31d8c8d60cd5976d8813c8d2426b4fc0542677eb8f75b552ebe","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventBooking.Infrastructure.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("eventbooking")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private NpgsqlDataSource? _dataSource;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(ConnectionString);

        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public EventBookingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EventBookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new EventBookingDbContext(options);
    }

    /// <summary>
    /// Empties every table and re-seeds the fixed rows. Faster and far less flaky than dropping
    /// and re-creating the database between tests.
    /// </summary>
    public async Task ResetAsync()
    {
        var dataSource = _dataSource
            ?? throw new InvalidOperationException("The PostgreSQL fixture has not been initialized.");
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                DO $$
                DECLARE statements CURSOR FOR
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                    -- Change-controlled Employee Group rows stay seeded by the migration.
                    AND tablename NOT IN ('employee_group', 'employee_group_requirement');
                BEGIN
                    FOR statement IN statements LOOP
                        EXECUTE 'TRUNCATE TABLE ' || quote_ident(statement.tablename) || ' CASCADE;';
                    END LOOP;
                END $$;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await using var context = NewContext();
        context.AppointmentTypes.AddRange(
            Domain.AppointmentTypes.AppointmentType.CreateFixedSet());
        context.SystemSettings.Add(Domain.Settings.SystemSettings.CreateDefault());
        await EnsureEmployeeGroupsSeededAsync(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Repairs change-controlled Employee Group rows after truncation. Truncating
    /// appointment_type cascades into the mapping table, so migration-seeded mappings are
    /// re-inserted when missing. Production databases rely on the migration alone.
    /// </summary>
    private static async Task EnsureEmployeeGroupsSeededAsync(EventBookingDbContext context)
    {
        var fixedGroups = new[]
        {
            EmployeeGroup.Define(
                EmployeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
            EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            EmployeeGroup.Define(
                EmployeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT", "Ground Operations Agent", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            EmployeeGroup.Define(
                EmployeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            EmployeeGroup.Define(
                EmployeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES", "Ground Transport Services", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
        };

        foreach (var group in fixedGroups)
        {
            var existing = await context.EmployeeGroups
                .Include(persisted => persisted.Requirements)
                .SingleOrDefaultAsync(persisted => persisted.Id == group.Id);
            if (existing is null)
            {
                context.EmployeeGroups.Add(group);
                continue;
            }

            foreach (var requirement in group.Requirements)
            {
                if (existing.Requirements.All(persisted => persisted.AppointmentTypeId != requirement.AppointmentTypeId))
                {
                    context.Add(requirement);
                }
            }
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
`````

## tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs — 1/1

<!-- port-file: {"path":"tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs","encoding":"utf8","sha256":"3542f54fb38b2c5c3c5c7362db5d6d093cc7de8ee3b49e7ae662d6af09e59494","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;
using EventBooking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Infrastructure.Tests;

/// <summary>Verifies recovery journey links, ordering, and active uniqueness backstops.</summary>
[Collection("postgres")]
public sealed class RecoveryBookingPersistenceTests(PostgresFixture fixture)
{
    /// <summary>Verifies an original and two concluded recoveries reload as one ordered journey.</summary>
    [Fact]
    public async Task JourneyLinksPersistAndReloadInCreationOrder()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var initial = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        var original = Booking.Create(
            Guid.NewGuid(), initial, slotId, "manage-original", DateTimeOffset.UtcNow);
        var first = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(2));
        first.Conclude();
        second.Conclude();

        await using (var write = fixture.NewContext())
        {
            write.Bookings.AddRange(original, first, second);
            await write.SaveChangesAsync();
        }

        await using var read = fixture.NewContext();
        var journey = await new BookingRepository(read)
            .ListJourneyAsync(original.Id, CancellationToken.None);

        Assert.Equal([original.Id, first.Id, second.Id], journey.Select(b => b.Id));
        Assert.Null(journey[0].RecoveryOfBookingId);
        Assert.Equal(BookingStatus.Active, journey[0].Status);
        Assert.Equal([original.Id, original.Id], journey.Skip(1).Select(b => b.RecoveryOfBookingId));
        Assert.All(journey.Skip(1), b => Assert.Equal(BookingStatus.Concluded, b.Status));
    }

    /// <summary>Verifies a second active original for one candidate violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveOriginalViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var first = OriginalFor(candidateId);
        var second = OriginalFor(candidateId);

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>Verifies a second active recovery for one root violates uniqueness.</summary>
    [Fact]
    public async Task SecondActiveRecoveryViolatesUniqueness()
    {
        await fixture.ResetAsync();
        var candidateId = Guid.NewGuid();
        var original = OriginalFor(candidateId);
        var first = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(1));
        var second = RecoveryFor(candidateId, original, DateTimeOffset.UtcNow.AddHours(2));

        await using var context = fixture.NewContext();
        context.Bookings.AddRange(original, first, second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static Booking OriginalFor(Guid candidateId)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidateId, "initial", DateTimeOffset.UtcNow.AddDays(1),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp], 0);
        return Booking.Create(Guid.NewGuid(), invite, slotId, "manage", DateTimeOffset.UtcNow);
    }

    private static Booking RecoveryFor(Guid candidateId, Booking original, DateTimeOffset createdAt)
    {
        var slotId = Guid.NewGuid();
        var invite = Invite.CreateRecovery(
            Guid.NewGuid(), candidateId, original.Id, "recovery", DateTimeOffset.UtcNow.AddDays(2),
            [slotId, Guid.NewGuid(), Guid.NewGuid()], [AppointmentTypeIds.MedicalCheckUp]);
        return Booking.CreateRecovery(
            Guid.NewGuid(), invite, original, slotId, $"manage-recovery-{Guid.NewGuid():N}", createdAt);
    }
}
`````
