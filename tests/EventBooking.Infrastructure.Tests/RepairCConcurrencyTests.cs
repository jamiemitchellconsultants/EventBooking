using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Slots;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventBooking.Infrastructure.Tests;

/// <summary>
/// Exercises the proposal and candidate lifecycle races against PostgreSQL. Each operation gets
/// an independent dependency-injection scope and therefore an independent DbContext connection.
/// </summary>
[Collection("postgres")]
public sealed class RepairCConcurrencyTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedToday = DateOnly.FromDateTime(FixedNow.DateTime);

    /// <summary>
    /// Two final acceptances of one open proposal create one confirmed slot and preserve all three
    /// acceptance-derived capacity rows.
    /// </summary>
    [Fact]
    public async Task FinalProposalAcceptancesProduceOneConfirmedSlot()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var proposal = await harness.GivenOpenProposalWithOneAcceptanceAsync();

        var results = await Task.WhenAll(
            harness.AcceptAsync(harness.MedicalManagerId, proposal.Id, 6),
            harness.AcceptAsync(harness.UniformManagerId, proposal.Id, 8));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(SlotProposalStatus.Confirmed, await harness.ProposalStatusAsync(proposal.Id));
        Assert.Equal(3, await harness.AcceptanceCountAsync(proposal.Id));
        Assert.Equal(1, await harness.ConfirmedSlotCountAsync(proposal.Id));
        Assert.Equal(new[] { 6, 8, 10 }, await harness.CapacitiesForProposalAsync(proposal.Id));
    }

    /// <summary>
    /// Two managers proposing the same future window leave one open proposal and expose a stable
    /// conflict to the losing request rather than a database-provider exception.
    /// </summary>
    [Fact]
    public async Task SameWindowProposalsProduceOneOpenProposalAndOneConflict()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var date = FixedToday.AddDays(30);

        Assert.True(await harness.HasIndexAsync("ux_slot_proposal_open_window"));
        Assert.True(await harness.HasIndexAsync("ux_invite_pending_candidate"));
        Assert.True(await harness.HasIndexAsync("ux_booking_active_original_candidate"));
        Assert.True(await harness.HasIndexAsync("ux_booking_active_recovery"));

        var results = await Task.WhenAll(
            harness.ProposeAsync(harness.DrugAndAlcoholManagerId, date, new TimeOnly(9, 0)),
            harness.ProposeAsync(harness.DrugAndAlcoholManagerId, date, new TimeOnly(9, 0)));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.Equal(1, results.Count(result => result.IsFailure));
        Assert.Equal("conflict", results.Single(result => result.IsFailure).Error.Code);
        Assert.Equal(1, await harness.OpenProposalCountAsync(date, new TimeOnly(9, 0)));
    }

    /// <summary>
    /// Simultaneous manual triggers for a candidate result in one pending invite, not two offers.
    /// </summary>
    [Fact]
    public async Task ManualInviteTriggersProduceOnePendingInvite()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var candidateId = await harness.GivenCandidateWithThreeEligibleSlotsAsync();

        await Task.WhenAll(
            harness.TriggerAsync(harness.CoordinatorId, candidateId),
            harness.TriggerAsync(harness.CoordinatorId, candidateId));

        Assert.Equal(1, await harness.PendingInviteCountAsync(candidateId));
    }

    /// <summary>
    /// Legacy duplicate pending tokens offering disjoint slots cannot create two active bookings
    /// or consume capacity twice once the candidate lifecycle is serialized.
    /// </summary>
    [Fact]
    public async Task DisjointInviteTokensProduceOneActiveBooking()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var scenario = await harness.GivenLegacyDuplicatePendingInvitesAsync();

        try
        {
            var results = await Task.WhenAll(
                harness.ConfirmAsync(scenario.FirstToken, scenario.FirstSlotId),
                harness.ConfirmAsync(scenario.SecondToken, scenario.SecondSlotId))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(1, results.Count(result => result.IsSuccess));
            Assert.Equal(1, await harness.ActiveBookingCountForCandidateAsync(scenario.CandidateId));
            Assert.Equal(1, await harness.RemainingCapacityAsync(
                scenario.FirstSlotId,
                AppointmentTypeIds.DrugAndAlcoholTesting)
                + await harness.RemainingCapacityAsync(
                    scenario.SecondSlotId,
                    AppointmentTypeIds.DrugAndAlcoholTesting));
        }
        finally
        {
            await harness.RestorePendingInviteIndexAsync();
        }
    }

    /// <summary>
    /// Candidate deletion and token confirmation cannot leave a deleted candidate's booking or
    /// its consumed capacity behind after their overlapping lifecycle transitions settle.
    /// </summary>
    [Fact]
    public async Task CandidateDeletionAndConfirmationLeaveNoOrphanBookingOrCapacity()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var scenario = await harness.GivenSingleInviteAsync();

        await Task.WhenAll(
            harness.DeleteAsync(harness.CoordinatorId, scenario.CandidateId),
            harness.ConfirmAsync(scenario.FirstToken, scenario.FirstSlotId))
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await harness.ActiveBookingCountForCandidateAsync(scenario.CandidateId));
        Assert.Equal(1, await harness.RemainingCapacityAsync(
            scenario.FirstSlotId,
                AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    /// <summary>
    /// Slot cancellation waits behind candidate deletion at the candidate lifecycle root, then
    /// completes without a PostgreSQL deadlock or provider exception after deletion commits.
    /// </summary>
    [Fact]
    public async Task SlotCancellationAndCandidateDeletionCompleteWithoutDeadlock()
    {
        var gate = new CandidateLifecycleRaceGate();
        await using var harness = await RepairCHarness.CreateAsync(fixture, gate);
        var scenario = await harness.GivenBookedCandidateAsync();

        var deletion = harness.DeleteAsync(harness.CoordinatorId, scenario.CandidateId);
        await gate.WaitUntilFirstCapacityLockAsync();

        var cancellation = harness.CancelSlotAsync(harness.CoordinatorId, scenario.SlotId);
        await gate.WaitUntilSecondCandidateLockAsync();
        gate.ReleaseFirstCapacityLock();

        await Task.WhenAll((Task)deletion, cancellation).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True((await deletion).IsSuccess);
        Assert.True((await cancellation).IsSuccess);
        Assert.Equal(0, await harness.ActiveBookingCountForCandidateAsync(scenario.CandidateId));
        Assert.Equal(0, await harness.CandidateCountAsync(scenario.CandidateId));
        Assert.Equal(1, await harness.RemainingCapacityAsync(
            scenario.SlotId,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Equal(ConfirmedSlotStatus.Cancelled, await harness.ConfirmedSlotStatusAsync(scenario.SlotId));
    }

    /// <summary>
    /// Uses the real application registrations over the shared PostgreSQL testcontainer.
    /// </summary>
    private sealed class RepairCHarness : IAsyncDisposable
    {
        private readonly PostgresFixture _fixture;
        private readonly ServiceProvider _services;
        private int _nextSlotOffset;

        private RepairCHarness(PostgresFixture fixture, ServiceProvider services)
        {
            _fixture = fixture;
            _services = services;
        }

        /// <summary>Identifies the coordinator that issues and deletes test candidates.</summary>
        public Guid CoordinatorId { get; } = Guid.Parse("c0000009-0000-0000-0000-000000000009");

        /// <summary>Identifies the drug-and-alcohol manager used to create proposals.</summary>
        public Guid DrugAndAlcoholManagerId { get; } = Guid.Parse("c0000001-0000-0000-0000-000000000001");

        /// <summary>Identifies the medical manager used to accept proposals.</summary>
        public Guid MedicalManagerId { get; } = Guid.Parse("c0000002-0000-0000-0000-000000000002");

        /// <summary>Identifies the uniform manager used to accept proposals.</summary>
        public Guid UniformManagerId { get; } = Guid.Parse("c0000003-0000-0000-0000-000000000003");

        /// <summary>Suppresses delivery while preserving the application's email side effects.</summary>
        private sealed class SilentTransport : IEmailTransport
        {
            /// <inheritdoc />
            public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }

        /// <summary>Creates a reset service provider with the production application wiring.</summary>
        public static async Task<RepairCHarness> CreateAsync(
            PostgresFixture fixture,
            CandidateLifecycleRaceGate? raceGate = null)
        {
            await fixture.ResetAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBookingInfrastructure(
                fixture.ConnectionString,
                new HeadOfficeOptions("Europe/London"),
                new TokenOptions("a-repair-c-concurrency-signing-key-long-enough"));
            services.AddEventBookingApplication(
                new CandidatePortalOptions("https://booking.example.com", "HQ", "recruitment@example.com"));
            services.AddScoped<IEmailTransport, SilentTransport>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new FixedClock(FixedNow, FixedToday));

            if (raceGate is not null)
            {
                services.RemoveAll<ICandidateRepository>();
                services.AddScoped<ICandidateRepository>(serviceProvider =>
                    new ObservingCandidateRepository(
                        serviceProvider.GetRequiredService<EventBookingDbContext>(),
                        raceGate));
                services.RemoveAll<ISlotCapacityRepository>();
                services.AddScoped<ISlotCapacityRepository>(serviceProvider =>
                    new GatedSlotCapacityRepository(
                        serviceProvider.GetRequiredService<EventBookingDbContext>(),
                        raceGate));
            }

            var harness = new RepairCHarness(fixture, services.BuildServiceProvider());
            await harness.GivenRolesAsync();
            return harness;
        }

        /// <summary>Seeds the three managers and coordinator used by all lifecycle races.</summary>
        private async Task GivenRolesAsync()
        {
            await using var context = _fixture.NewContext();
            context.StaffAccessProfiles.AddRange(
                StaffAccessProfile.Create(DrugAndAlcoholManagerId, Role.Manager, AppointmentTypeIds.DrugAndAlcoholTesting),
                StaffAccessProfile.Create(MedicalManagerId, Role.Manager, AppointmentTypeIds.MedicalCheckUp),
                StaffAccessProfile.Create(UniformManagerId, Role.Manager, AppointmentTypeIds.UniformFitting),
                StaffAccessProfile.Create(CoordinatorId, Role.Coordinator, null));
            await context.SaveChangesAsync();
        }

        /// <summary>Seeds one proposal with the drug-and-alcohol acceptance already recorded.</summary>
        public async Task<SlotProposal> GivenOpenProposalWithOneAcceptanceAsync()
        {
            var proposal = SlotProposal.Create(
                Guid.NewGuid(),
                new SlotWindow(FixedToday.AddDays(30), new TimeOnly(9, 0)),
                DrugAndAlcoholManagerId);
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManagerId, 10);
            await using var context = _fixture.NewContext();
            context.SlotProposals.Add(proposal);
            await context.SaveChangesAsync();
            return proposal;
        }

        /// <summary>Creates a candidate and three active slots that meet their required types.</summary>
        public async Task<Guid> GivenCandidateWithThreeEligibleSlotsAsync()
        {
            var candidate = Candidate.Create(
                Guid.NewGuid(), "Concurrent Candidate", $"{Guid.NewGuid():N}@mail.com",
                EmployeeGroup.Define(
                    EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                    [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
            await using var context = _fixture.NewContext();
            context.Candidates.Add(candidate);
            context.ConfirmedSlots.AddRange(
                CreateSlot(),
                CreateSlot(),
                CreateSlot());
            await context.SaveChangesAsync();
            return candidate.Id;
        }

        /// <summary>Seeds two pending token rows for legacy-data confirmation coverage.</summary>
        public async Task<LifecycleScenario> GivenLegacyDuplicatePendingInvitesAsync()
        {
            await DropPendingInviteIndexAsync();
            var scenario = await GivenInviteScenarioAsync(includeSecondInvite: true);
            return scenario;
        }

        /// <summary>Seeds one pending token for deletion-versus-confirmation coverage.</summary>
        public Task<LifecycleScenario> GivenSingleInviteAsync() =>
            GivenInviteScenarioAsync(includeSecondInvite: false);

        /// <summary>Seeds one active booking whose candidate deletion will release the held capacity.</summary>
        public async Task<SlotCancellationScenario> GivenBookedCandidateAsync()
        {
            var bookedSlot = CreateSlot();
            var fallbackOne = CreateSlot();
            var fallbackTwo = CreateSlot();
            var pilots = EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
            var candidate = Candidate.Create(
                Guid.NewGuid(),
                "Concurrent Candidate",
                $"{Guid.NewGuid():N}@mail.com",
                pilots);
            candidate.MarkInvited();
            var tokens = _services.GetRequiredService<ITokenService>();
            var inviteId = Guid.NewGuid();
            var inviteToken = tokens.Issue(inviteId);
            var invite = Invite.CreateInitial(
                inviteId,
                candidate.Id,
                inviteToken.TokenHash,
                FixedNow.AddDays(4),
                [bookedSlot.Id, fallbackOne.Id, fallbackTwo.Id],
                candidate.RequiredAppointmentTypeIds,
                0);
            var bookingId = Guid.NewGuid();
            var manageToken = tokens.Issue(bookingId);
            var booking = Booking.Create(
                bookingId,
                invite,
                bookedSlot.Id,
                manageToken.TokenHash,
                FixedNow);
            bookedSlot.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            invite.MarkUsed();
            candidate.MarkBooked();

            await using var context = _fixture.NewContext();
            context.Candidates.Add(candidate);
            context.ConfirmedSlots.AddRange(bookedSlot, fallbackOne, fallbackTwo);
            context.Invites.Add(invite);
            context.Bookings.Add(booking);
            context.BookingAppointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
            await context.SaveChangesAsync();

            return new SlotCancellationScenario(candidate.Id, bookedSlot.Id);
        }

        /// <summary>Runs the real proposal acceptance handler in an isolated scope.</summary>
        public async Task<Result<AcceptProposalOutcome>> AcceptAsync(Guid managerId, Guid proposalId, int headcount)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AcceptProposalHandler>().HandleAsync(
                new AcceptProposalCommand(managerId, proposalId, headcount), CancellationToken.None);
        }

        /// <summary>Runs the real proposal creation handler in an isolated scope.</summary>
        public async Task<Result<Guid>> ProposeAsync(Guid managerId, DateOnly date, TimeOnly startTime)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ProposeSlotHandler>().HandleAsync(
                new ProposeSlotCommand(managerId, date, startTime), CancellationToken.None);
        }

        /// <summary>Runs the real coordinator invite trigger in an isolated scope.</summary>
        public async Task<Result<InviteIssueResult>> TriggerAsync(Guid coordinatorId, Guid candidateId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<TriggerInviteHandler>().HandleAsync(
                new TriggerInviteCommand(coordinatorId, candidateId), CancellationToken.None);
        }

        /// <summary>Runs the real candidate-token booking confirmation in an isolated scope.</summary>
        public async Task<Result<ConfirmBookingOutcome>> ConfirmAsync(string token, Guid slotId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>().HandleAsync(
                new ConfirmBookingCommand(token, slotId), CancellationToken.None);
        }

        /// <summary>Runs the real confirmed candidate deletion in an isolated scope.</summary>
        public async Task<Result> DeleteAsync(Guid coordinatorId, Guid candidateId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<DeleteCandidateHandler>().HandleAsync(
                new DeleteCandidateCommand(coordinatorId, candidateId, true), CancellationToken.None);
        }

        /// <summary>Runs the real confirmed-slot cancellation handler in an isolated scope.</summary>
        public async Task<Result<CancelSlotOutcome>> CancelSlotAsync(Guid coordinatorId, Guid slotId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<CancelConfirmedSlotHandler>().HandleAsync(
                new CancelConfirmedSlotCommand(coordinatorId, slotId, true), CancellationToken.None);
        }

        /// <summary>Reads the durable proposal status after concurrent acceptances settle.</summary>
        public async Task<SlotProposalStatus> ProposalStatusAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.SlotProposals.Where(proposal => proposal.Id == proposalId)
                .Select(proposal => proposal.Status).SingleAsync();
        }

        /// <summary>Counts durable proposal acceptances after the accepting requests settle.</summary>
        public async Task<int> AcceptanceCountAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.SlotProposals.Where(proposal => proposal.Id == proposalId)
                .SelectMany(proposal => proposal.Acceptances).CountAsync();
        }

        /// <summary>Counts confirmed slots derived from one proposal.</summary>
        public async Task<int> ConfirmedSlotCountAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.ConfirmedSlots.CountAsync(slot => slot.ProposalId == proposalId);
        }

        /// <summary>Reads the ordered capacity totals for a proposal's one derived slot.</summary>
        public async Task<int[]> CapacitiesForProposalAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.ConfirmedSlots.Where(slot => slot.ProposalId == proposalId)
                .SelectMany(slot => slot.Capacities).OrderBy(capacity => capacity.TotalHeadcount)
                .Select(capacity => capacity.TotalHeadcount).ToArrayAsync();
        }

        /// <summary>Counts open proposals for one candidate-facing window.</summary>
        public async Task<int> OpenProposalCountAsync(DateOnly date, TimeOnly startTime)
        {
            await using var context = _fixture.NewContext();
            return await context.SlotProposals.CountAsync(proposal => proposal.Status == SlotProposalStatus.Open
                && proposal.Window.Date == date && proposal.Window.StartTime == startTime);
        }

        /// <summary>Counts the candidate's durable pending invite rows.</summary>
        public async Task<int> PendingInviteCountAsync(Guid candidateId)
        {
            await using var context = _fixture.NewContext();
            return await context.Invites.CountAsync(invite => invite.CandidateId == candidateId
                && invite.Status == InviteStatus.Pending);
        }

        /// <summary>Counts active bookings held by the candidate after racing token use.</summary>
        public async Task<int> ActiveBookingCountForCandidateAsync(Guid candidateId)
        {
            await using var context = _fixture.NewContext();
            return await context.Bookings.CountAsync(booking => booking.CandidateId == candidateId
                && booking.Status == BookingStatus.Active);
        }

        /// <summary>Counts durable candidate rows after a lifecycle race settles.</summary>
        public async Task<int> CandidateCountAsync(Guid candidateId)
        {
            await using var context = _fixture.NewContext();
            return await context.Candidates.CountAsync(candidate => candidate.Id == candidateId);
        }

        /// <summary>Reads the durable status of a confirmed slot after a lifecycle race settles.</summary>
        public async Task<ConfirmedSlotStatus> ConfirmedSlotStatusAsync(Guid slotId)
        {
            await using var context = _fixture.NewContext();
            return await context.ConfirmedSlots.Where(slot => slot.Id == slotId)
                .Select(slot => slot.Status).SingleAsync();
        }

        /// <summary>Reads the remaining capacity for one appointment type on one slot.</summary>
        public async Task<int> RemainingCapacityAsync(Guid slotId, Guid appointmentTypeId)
        {
            await using var context = _fixture.NewContext();
            return await context.SlotCapacities.Where(capacity => capacity.ConfirmedSlotId == slotId
                && capacity.AppointmentTypeId == appointmentTypeId)
                .Select(capacity => capacity.RemainingCapacity).SingleAsync();
        }

        /// <summary>Checks the migration-created database backstop by its PostgreSQL index name.</summary>
        public async Task<bool> HasIndexAsync(string indexName)
        {
            await using var context = _fixture.NewContext();
            return await context.Database.SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname = {indexName}) AS "Value"
                """).SingleAsync();
        }

        /// <summary>Disposes the production service provider used by this test harness.</summary>
        public ValueTask DisposeAsync() => _services.DisposeAsync();

        private async Task<LifecycleScenario> GivenInviteScenarioAsync(bool includeSecondInvite)
        {
            var firstSlot = CreateSlot();
            var secondSlot = CreateSlot();
            var fallbackOne = CreateSlot();
            var fallbackTwo = CreateSlot();
            var pilots = EmployeeGroup.Define(
                EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
            var candidate = Candidate.Create(
                Guid.NewGuid(), "Concurrent Candidate", $"{Guid.NewGuid():N}@mail.com", pilots);
            candidate.MarkInvited();
            var tokens = _services.GetRequiredService<ITokenService>();
            var firstInviteId = Guid.NewGuid();
            var firstToken = tokens.Issue(firstInviteId);
            var firstInvite = Invite.CreateInitial(firstInviteId, candidate.Id, firstToken.TokenHash,
                FixedNow.AddDays(4), [firstSlot.Id, fallbackOne.Id, fallbackTwo.Id],
                candidate.RequiredAppointmentTypeIds, 0);

            await using var context = _fixture.NewContext();
            context.Candidates.Add(candidate);
            context.ConfirmedSlots.AddRange(firstSlot, secondSlot, fallbackOne, fallbackTwo);
            context.Invites.Add(firstInvite);

            var secondToken = string.Empty;
            if (includeSecondInvite)
            {
                var secondInviteId = Guid.NewGuid();
                var issued = tokens.Issue(secondInviteId);
                secondToken = issued.Token;
                context.Invites.Add(Invite.CreateInitial(secondInviteId, candidate.Id, issued.TokenHash,
                    FixedNow.AddDays(4), [secondSlot.Id, fallbackOne.Id, fallbackTwo.Id],
                    candidate.RequiredAppointmentTypeIds, 0));
            }

            await context.SaveChangesAsync();
            return new LifecycleScenario(candidate.Id, firstToken.Token, secondToken, firstSlot.Id, secondSlot.Id);
        }

        private async Task DropPendingInviteIndexAsync()
        {
            await using var context = _fixture.NewContext();
            await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS ux_invite_pending_candidate;");
        }

        /// <summary>Restores the migration backstop after deliberately seeding legacy duplicate data.</summary>
        public async Task RestorePendingInviteIndexAsync()
        {
            await using var context = _fixture.NewContext();
            await context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_invite_pending_candidate ON invite (candidate_id) WHERE status = 1;");
        }

        private ConfirmedSlot CreateSlot() => ConfirmedSlot.CreateImported(
            Guid.NewGuid(),
            new SlotWindow(FixedToday.AddDays(30 + Interlocked.Increment(ref _nextSlotOffset)), new TimeOnly(9, 0)),
            new Dictionary<Guid, int>
            {
                [AppointmentTypeIds.DrugAndAlcoholTesting] = 1,
                [AppointmentTypeIds.MedicalCheckUp] = 1,
                [AppointmentTypeIds.UniformFitting] = 1,
            });

        private sealed class FixedClock(DateTimeOffset utcNow, DateOnly today) : IClock
        {
            /// <summary>Gets the deterministic instant supplied to this test clock.</summary>
            public DateTimeOffset UtcNow => utcNow;

            /// <summary>Gets the deterministic head-office instant supplied to this test clock.</summary>
            public DateTimeOffset NowAtHeadOffice => utcNow;

            /// <summary>Gets the deterministic head-office date supplied to this test clock.</summary>
            public DateOnly TodayAtHeadOffice => today;

            /// <summary>Returns the deterministic head-office date for every instant.</summary>
            public DateOnly DateAtHeadOffice(DateTimeOffset instant) => today;

            public DateTimeOffset InstantAtHeadOffice(DateTimeOffset instant) => instant;
        }

        private sealed class ObservingCandidateRepository(
            EventBookingDbContext context,
            CandidateLifecycleRaceGate gate) : ICandidateRepository
        {
            private readonly CandidateRepository _inner = new(context);

            /// <inheritdoc />
            public Task<Candidate?> GetAsync(Guid id, CancellationToken cancellationToken) =>
                _inner.GetAsync(id, cancellationToken);

            /// <inheritdoc />
            public async Task<Candidate?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
            {
                gate.RecordCandidateLockAttempt();
                return await _inner.LockForUpdateAsync(id, cancellationToken);
            }

            /// <inheritdoc />
            public Task<Candidate?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
                _inner.GetByEmailAsync(email, cancellationToken);

            /// <inheritdoc />
            public Task<IReadOnlyList<Candidate>> ListAsync(
                CandidateStatus? status,
                CancellationToken cancellationToken) =>
                _inner.ListAsync(status, cancellationToken);

            /// <inheritdoc />
            public void Add(Candidate candidate) => _inner.Add(candidate);

            /// <inheritdoc />
            public void Remove(Candidate candidate) => _inner.Remove(candidate);
        }

        private sealed class GatedSlotCapacityRepository(
            EventBookingDbContext context,
            CandidateLifecycleRaceGate gate) : ISlotCapacityRepository
        {
            private readonly SlotCapacityRepository _inner = new(context);

            /// <inheritdoc />
            public async Task<IReadOnlyList<SlotCapacity>> LockForUpdateAsync(
                Guid confirmedSlotId,
                IReadOnlyCollection<Guid> appointmentTypeIds,
                CancellationToken cancellationToken)
            {
                await gate.WaitBeforeFirstCapacityLockAsync();
                return await _inner.LockForUpdateAsync(
                    confirmedSlotId,
                    appointmentTypeIds,
                    cancellationToken);
            }
        }
    }

    /// <summary>Captures the candidate, tokens, and disjoint slots used by one lifecycle race.</summary>
    private sealed record LifecycleScenario(
        Guid CandidateId,
        string FirstToken,
        string SecondToken,
        Guid FirstSlotId,
        Guid SecondSlotId);

    /// <summary>Captures the candidate and slot whose locked rows drive the deadlock regression.</summary>
    private sealed record SlotCancellationScenario(Guid CandidateId, Guid SlotId);

    private sealed class CandidateLifecycleRaceGate
    {
        private readonly TaskCompletionSource _firstCapacityLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstCapacityLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondCandidateLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _candidateLockAttempts;
        private int _capacityLockAttempts;

        /// <summary>Signals when cancellation has attempted its candidate lifecycle lock.</summary>
        public void RecordCandidateLockAttempt()
        {
            if (Interlocked.Increment(ref _candidateLockAttempts) == 2)
            {
                _secondCandidateLock.TrySetResult();
            }
        }

        /// <summary>Blocks the first capacity lock until the test releases the deletion path.</summary>
        public async Task WaitBeforeFirstCapacityLockAsync()
        {
            if (Interlocked.Increment(ref _capacityLockAttempts) == 1)
            {
                _firstCapacityLock.TrySetResult();
                await _releaseFirstCapacityLock.Task;
            }
        }

        /// <summary>Waits until deletion has reached its capacity lock.</summary>
        public Task WaitUntilFirstCapacityLockAsync() =>
            _firstCapacityLock.Task.WaitAsync(TimeSpan.FromSeconds(10));

        /// <summary>Waits until cancellation has attempted to lock the candidate.</summary>
        public Task WaitUntilSecondCandidateLockAsync() => _secondCandidateLock.Task.WaitAsync(TimeSpan.FromSeconds(2));

        /// <summary>Allows deletion to finish its capacity update and release candidate locks.</summary>
        public void ReleaseFirstCapacityLock() => _releaseFirstCapacityLock.TrySetResult();
    }
}
