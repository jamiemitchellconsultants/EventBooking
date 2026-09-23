using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Events;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
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
/// Exercises the proposal and attendee lifecycle races against PostgreSQL. Each operation gets
/// an independent dependency-injection scope and therefore an independent DbContext connection.
/// </summary>
[Collection("postgres")]
public sealed class RepairCConcurrencyTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedToday = DateOnly.FromDateTime(FixedNow.DateTime);

    /// <summary>
    /// Two final acceptances of one open proposal create one event and preserve all three
    /// acceptance-derived capacity rows.
    /// </summary>
    [Fact]
    public async Task FinalProposalAcceptancesProduceOneEvent()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var proposal = await harness.GivenOpenProposalWithOneAcceptanceAsync();

        var results = await Task.WhenAll(
            harness.AcceptAsync(harness.MedicalManagerId, proposal.Id, 6),
            harness.AcceptAsync(harness.UniformManagerId, proposal.Id, 8));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(EventProposalStatus.Confirmed, await harness.ProposalStatusAsync(proposal.Id));
        Assert.Equal(3, await harness.AcceptanceCountAsync(proposal.Id));
        Assert.Equal(1, await harness.EventCountAsync(proposal.Id));
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

        Assert.True(await harness.HasIndexAsync("ux_event_proposal_open_window"));
        Assert.True(await harness.HasIndexAsync("ux_invite_pending_attendee"));
        Assert.True(await harness.HasIndexAsync("ux_booking_active_original_attendee"));
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
    /// Simultaneous manual triggers for a attendee result in one pending invite, not two offers.
    /// </summary>
    [Fact]
    public async Task ManualInviteTriggersProduceOnePendingInvite()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var attendeeId = await harness.GivenAttendeeWithThreeEligibleEventsAsync();

        await Task.WhenAll(
            harness.TriggerAsync(harness.CoordinatorId, attendeeId),
            harness.TriggerAsync(harness.CoordinatorId, attendeeId));

        Assert.Equal(1, await harness.PendingInviteCountAsync(attendeeId));
    }

    /// <summary>
    /// Legacy duplicate pending tokens offering disjoint events cannot create two active bookings
    /// or consume capacity twice once the attendee lifecycle is serialized.
    /// </summary>
    [Fact]
    public async Task DisjointInviteTokensProduceOneActiveBooking()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var scenario = await harness.GivenLegacyDuplicatePendingInvitesAsync();

        try
        {
            var results = await Task.WhenAll(
                harness.ConfirmAsync(scenario.FirstToken, scenario.FirstEventId),
                harness.ConfirmAsync(scenario.SecondToken, scenario.SecondEventId))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(1, results.Count(result => result.IsSuccess));
            Assert.Equal(1, await harness.ActiveBookingCountForAttendeeAsync(scenario.AttendeeId));
            Assert.Equal(1, await harness.RemainingCapacityAsync(
                scenario.FirstEventId,
                AppointmentTypeIds.DrugAndAlcoholTesting)
                + await harness.RemainingCapacityAsync(
                    scenario.SecondEventId,
                    AppointmentTypeIds.DrugAndAlcoholTesting));
        }
        finally
        {
            await harness.RestorePendingInviteIndexAsync();
        }
    }

    /// <summary>
    /// Attendee deletion and token confirmation cannot leave a deleted attendee's booking or
    /// its consumed capacity behind after their overlapping lifecycle transitions settle.
    /// </summary>
    [Fact]
    public async Task AttendeeDeletionAndConfirmationLeaveNoOrphanBookingOrCapacity()
    {
        await using var harness = await RepairCHarness.CreateAsync(fixture);
        var scenario = await harness.GivenSingleInviteAsync();

        await Task.WhenAll(
            harness.DeleteAsync(harness.CoordinatorId, scenario.AttendeeId),
            harness.ConfirmAsync(scenario.FirstToken, scenario.FirstEventId))
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, await harness.ActiveBookingCountForAttendeeAsync(scenario.AttendeeId));
        Assert.Equal(1, await harness.RemainingCapacityAsync(
            scenario.FirstEventId,
                AppointmentTypeIds.DrugAndAlcoholTesting));
    }

    /// <summary>
    /// Event cancellation waits behind attendee deletion at the attendee lifecycle root, then
    /// completes without a PostgreSQL deadlock or provider exception after deletion commits.
    /// </summary>
    [Fact]
    public async Task EventCancellationAndAttendeeDeletionCompleteWithoutDeadlock()
    {
        var gate = new AttendeeLifecycleRaceGate();
        await using var harness = await RepairCHarness.CreateAsync(fixture, gate);
        var scenario = await harness.GivenBookedAttendeeAsync();

        var deletion = harness.DeleteAsync(harness.CoordinatorId, scenario.AttendeeId);
        await gate.WaitUntilFirstCapacityLockAsync();

        var cancellation = harness.CancelEventAsync(harness.CoordinatorId, scenario.EventId);
        await gate.WaitUntilSecondAttendeeLockAsync();
        gate.ReleaseFirstCapacityLock();

        await Task.WhenAll((Task)deletion, cancellation).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True((await deletion).IsSuccess);
        Assert.True((await cancellation).IsSuccess);
        Assert.Equal(0, await harness.ActiveBookingCountForAttendeeAsync(scenario.AttendeeId));
        Assert.Equal(0, await harness.AttendeeCountAsync(scenario.AttendeeId));
        Assert.Equal(1, await harness.RemainingCapacityAsync(
            scenario.EventId,
            AppointmentTypeIds.DrugAndAlcoholTesting));
        Assert.Equal(EventStatus.Cancelled, await harness.EventStatusAsync(scenario.EventId));
    }

    /// <summary>
    /// Uses the real application registrations over the shared PostgreSQL testcontainer.
    /// </summary>
    private sealed class RepairCHarness : IAsyncDisposable
    {
        private readonly PostgresFixture _fixture;
        private readonly ServiceProvider _services;
        private int _nextEventOffset;

        private RepairCHarness(PostgresFixture fixture, ServiceProvider services)
        {
            _fixture = fixture;
            _services = services;
        }

        /// <summary>Identifies the coordinator that issues and deletes test attendees.</summary>
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
            AttendeeLifecycleRaceGate? raceGate = null)
        {
            await fixture.ResetAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBookingInfrastructure(
                fixture.ConnectionString,
                new TransitionalLocationOptions("Europe/London"),
                new TokenOptions("a-repair-c-concurrency-signing-key-long-enough"));
            services.AddEventBookingApplication(
                new AttendeePortalOptions("https://booking.example.com", "HQ", "recruitment@example.com"));
            services.AddScoped<IEmailTransport, SilentTransport>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new FixedClock(FixedNow, FixedToday));

            if (raceGate is not null)
            {
                services.RemoveAll<IAttendeeRepository>();
                services.AddScoped<IAttendeeRepository>(serviceProvider =>
                    new ObservingAttendeeRepository(
                        serviceProvider.GetRequiredService<EventBookingDbContext>(),
                        raceGate));
                services.RemoveAll<IEventCapacityRepository>();
                services.AddScoped<IEventCapacityRepository>(serviceProvider =>
                    new GatedEventCapacityRepository(
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
        public async Task<EventProposal> GivenOpenProposalWithOneAcceptanceAsync()
        {
            var proposal = EventProposal.Create(
                Guid.NewGuid(),
                new EventWindow(FixedToday.AddDays(30), new TimeOnly(9, 0)),
                DrugAndAlcoholManagerId);
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, DrugAndAlcoholManagerId, 10);
            await using var context = _fixture.NewContext();
            context.EventProposals.Add(proposal);
            await context.SaveChangesAsync();
            return proposal;
        }

        /// <summary>Creates a attendee and three active events that meet their required types.</summary>
        public async Task<Guid> GivenAttendeeWithThreeEligibleEventsAsync()
        {
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Concurrent Attendee", $"{Guid.NewGuid():N}@mail.com",
                AttendeeGroup.Define(
                    AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                    [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
            await using var context = _fixture.NewContext();
            context.Attendees.Add(attendee);
            context.Events.AddRange(
                CreateEvent(),
                CreateEvent(),
                CreateEvent());
            await context.SaveChangesAsync();
            return attendee.Id;
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

        /// <summary>Seeds one active booking whose attendee deletion will release the held capacity.</summary>
        public async Task<EventCancellationScenario> GivenBookedAttendeeAsync()
        {
            var bookedEvent = CreateEvent();
            var fallbackOne = CreateEvent();
            var fallbackTwo = CreateEvent();
            var pilots = AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
            var attendee = Attendee.Create(
                Guid.NewGuid(),
                "Concurrent Attendee",
                $"{Guid.NewGuid():N}@mail.com",
                pilots);
            attendee.MarkInvited();
            var tokens = _services.GetRequiredService<ITokenService>();
            var inviteId = Guid.NewGuid();
            var inviteToken = tokens.Issue(inviteId);
            var invite = Invite.CreateInitial(
                inviteId,
                attendee.Id,
                inviteToken.TokenHash,
                FixedNow.AddDays(4),
                [bookedEvent.Id, fallbackOne.Id, fallbackTwo.Id],
                attendee.RequiredAppointmentTypeIds,
                0);
            var bookingId = Guid.NewGuid();
            var manageToken = tokens.Issue(bookingId);
            var booking = Booking.Create(
                bookingId,
                invite,
                bookedEvent.Id,
                manageToken.TokenHash,
                FixedNow);
            bookedEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
            invite.MarkUsed();
            attendee.MarkBooked();

            await using var context = _fixture.NewContext();
            context.Attendees.Add(attendee);
            context.Events.AddRange(bookedEvent, fallbackOne, fallbackTwo);
            context.Invites.Add(invite);
            context.Bookings.Add(booking);
            context.BookingAppointments.Add(BookingAppointment.Create(
                Guid.NewGuid(), booking.Id, AppointmentTypeIds.DrugAndAlcoholTesting));
            await context.SaveChangesAsync();

            return new EventCancellationScenario(attendee.Id, bookedEvent.Id);
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
            return await scope.ServiceProvider.GetRequiredService<ProposeEventHandler>().HandleAsync(
                new ProposeEventCommand(managerId, date, startTime), CancellationToken.None);
        }

        /// <summary>Runs the real coordinator invite trigger in an isolated scope.</summary>
        public async Task<Result<InviteIssueResult>> TriggerAsync(Guid coordinatorId, Guid attendeeId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<TriggerInviteHandler>().HandleAsync(
                new TriggerInviteCommand(coordinatorId, attendeeId), CancellationToken.None);
        }

        /// <summary>Runs the real attendee-token booking confirmation in an isolated scope.</summary>
        public async Task<Result<ConfirmBookingOutcome>> ConfirmAsync(string token, Guid eventId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ConfirmBookingHandler>().HandleAsync(
                new ConfirmBookingCommand(token, eventId), CancellationToken.None);
        }

        /// <summary>Runs the real confirmed attendee deletion in an isolated scope.</summary>
        public async Task<Result> DeleteAsync(Guid coordinatorId, Guid attendeeId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<DeleteAttendeeHandler>().HandleAsync(
                new DeleteAttendeeCommand(coordinatorId, attendeeId, true), CancellationToken.None);
        }

        /// <summary>Runs the real event cancellation handler in an isolated scope.</summary>
        public async Task<Result<CancelEventOutcome>> CancelEventAsync(Guid coordinatorId, Guid eventId)
        {
            await using var scope = _services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<CancelEventHandler>().HandleAsync(
                new CancelEventCommand(coordinatorId, eventId, true), CancellationToken.None);
        }

        /// <summary>Reads the durable proposal status after concurrent acceptances settle.</summary>
        public async Task<EventProposalStatus> ProposalStatusAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.EventProposals.Where(proposal => proposal.Id == proposalId)
                .Select(proposal => proposal.Status).SingleAsync();
        }

        /// <summary>Counts durable proposal acceptances after the accepting requests settle.</summary>
        public async Task<int> AcceptanceCountAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.EventProposals.Where(proposal => proposal.Id == proposalId)
                .SelectMany(proposal => proposal.Acceptances).CountAsync();
        }

        /// <summary>Counts events derived from one proposal.</summary>
        public async Task<int> EventCountAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.Events.CountAsync(eventItem => eventItem.ProposalId == proposalId);
        }

        /// <summary>Reads the ordered capacity totals for a proposal's one derived eventItem.</summary>
        public async Task<int[]> CapacitiesForProposalAsync(Guid proposalId)
        {
            await using var context = _fixture.NewContext();
            return await context.Events.Where(eventItem => eventItem.ProposalId == proposalId)
                .SelectMany(eventItem => eventItem.Capacities).OrderBy(capacity => capacity.TotalHeadcount)
                .Select(capacity => capacity.TotalHeadcount).ToArrayAsync();
        }

        /// <summary>Counts open proposals for one attendee-facing window.</summary>
        public async Task<int> OpenProposalCountAsync(DateOnly date, TimeOnly startTime)
        {
            await using var context = _fixture.NewContext();
            return await context.EventProposals.CountAsync(proposal => proposal.Status == EventProposalStatus.Open
                && proposal.Window.Date == date && proposal.Window.StartTime == startTime);
        }

        /// <summary>Counts the attendee's durable pending invite rows.</summary>
        public async Task<int> PendingInviteCountAsync(Guid attendeeId)
        {
            await using var context = _fixture.NewContext();
            return await context.Invites.CountAsync(invite => invite.AttendeeId == attendeeId
                && invite.Status == InviteStatus.Pending);
        }

        /// <summary>Counts active bookings held by the attendee after racing token use.</summary>
        public async Task<int> ActiveBookingCountForAttendeeAsync(Guid attendeeId)
        {
            await using var context = _fixture.NewContext();
            return await context.Bookings.CountAsync(booking => booking.AttendeeId == attendeeId
                && booking.Status == BookingStatus.Active);
        }

        /// <summary>Counts durable attendee rows after a lifecycle race settles.</summary>
        public async Task<int> AttendeeCountAsync(Guid attendeeId)
        {
            await using var context = _fixture.NewContext();
            return await context.Attendees.CountAsync(attendee => attendee.Id == attendeeId);
        }

        /// <summary>Reads the durable status of a event after a lifecycle race settles.</summary>
        public async Task<EventStatus> EventStatusAsync(Guid eventId)
        {
            await using var context = _fixture.NewContext();
            return await context.Events.Where(eventItem => eventItem.Id == eventId)
                .Select(eventItem => eventItem.Status).SingleAsync();
        }

        /// <summary>Reads the remaining capacity for one appointment type on one eventItem.</summary>
        public async Task<int> RemainingCapacityAsync(Guid eventId, Guid appointmentTypeId)
        {
            await using var context = _fixture.NewContext();
            return await context.EventCapacities.Where(capacity => capacity.EventId == eventId
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
            var firstEvent = CreateEvent();
            var secondEvent = CreateEvent();
            var fallbackOne = CreateEvent();
            var fallbackTwo = CreateEvent();
            var pilots = AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
            var attendee = Attendee.Create(
                Guid.NewGuid(), "Concurrent Attendee", $"{Guid.NewGuid():N}@mail.com", pilots);
            attendee.MarkInvited();
            var tokens = _services.GetRequiredService<ITokenService>();
            var firstInviteId = Guid.NewGuid();
            var firstToken = tokens.Issue(firstInviteId);
            var firstInvite = Invite.CreateInitial(firstInviteId, attendee.Id, firstToken.TokenHash,
                FixedNow.AddDays(4), [firstEvent.Id, fallbackOne.Id, fallbackTwo.Id],
                attendee.RequiredAppointmentTypeIds, 0);

            await using var context = _fixture.NewContext();
            context.Attendees.Add(attendee);
            context.Events.AddRange(firstEvent, secondEvent, fallbackOne, fallbackTwo);
            context.Invites.Add(firstInvite);

            var secondToken = string.Empty;
            if (includeSecondInvite)
            {
                var secondInviteId = Guid.NewGuid();
                var issued = tokens.Issue(secondInviteId);
                secondToken = issued.Token;
                context.Invites.Add(Invite.CreateInitial(secondInviteId, attendee.Id, issued.TokenHash,
                    FixedNow.AddDays(4), [secondEvent.Id, fallbackOne.Id, fallbackTwo.Id],
                    attendee.RequiredAppointmentTypeIds, 0));
            }

            await context.SaveChangesAsync();
            return new LifecycleScenario(attendee.Id, firstToken.Token, secondToken, firstEvent.Id, secondEvent.Id);
        }

        private async Task DropPendingInviteIndexAsync()
        {
            await using var context = _fixture.NewContext();
            await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS ux_invite_pending_attendee;");
        }

        /// <summary>Restores the migration backstop after deliberately seeding legacy duplicate data.</summary>
        public async Task RestorePendingInviteIndexAsync()
        {
            await using var context = _fixture.NewContext();
            await context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_invite_pending_attendee ON invite (attendee_id) WHERE status = 1;");
        }

        private Event CreateEvent() => Event.CreateImported(
            Guid.NewGuid(),
            new EventWindow(FixedToday.AddDays(30 + Interlocked.Increment(ref _nextEventOffset)), new TimeOnly(9, 0)),
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

            /// <summary>Gets the deterministic transitional-location instant supplied to this test clock.</summary>
            public DateTimeOffset NowAtTransitionalLocation => utcNow;

            /// <summary>Gets the deterministic transitional-location date supplied to this test clock.</summary>
            public DateOnly TodayAtTransitionalLocation => today;

            /// <summary>Returns the deterministic transitional-location date for every instant.</summary>
            public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => today;

            public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) => instant;
        }

        private sealed class ObservingAttendeeRepository(
            EventBookingDbContext context,
            AttendeeLifecycleRaceGate gate) : IAttendeeRepository
        {
            private readonly AttendeeRepository _inner = new(context);

            /// <inheritdoc />
            public Task<Attendee?> GetAsync(Guid id, CancellationToken cancellationToken) =>
                _inner.GetAsync(id, cancellationToken);

            /// <inheritdoc />
            public async Task<Attendee?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken)
            {
                gate.RecordAttendeeLockAttempt();
                return await _inner.LockForUpdateAsync(id, cancellationToken);
            }

            /// <inheritdoc />
            public Task<Attendee?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
                _inner.GetByEmailAsync(email, cancellationToken);

            /// <inheritdoc />
            public Task<IReadOnlyList<Attendee>> ListAsync(
                AttendeeStatus? status,
                CancellationToken cancellationToken) =>
                _inner.ListAsync(status, cancellationToken);

            /// <inheritdoc />
            public void Add(Attendee attendee) => _inner.Add(attendee);

            /// <inheritdoc />
            public void Remove(Attendee attendee) => _inner.Remove(attendee);
        }

        private sealed class GatedEventCapacityRepository(
            EventBookingDbContext context,
            AttendeeLifecycleRaceGate gate) : IEventCapacityRepository
        {
            private readonly EventCapacityRepository _inner = new(context);

            /// <inheritdoc />
            public async Task<IReadOnlyList<EventCapacity>> LockForUpdateAsync(
                Guid eventId,
                IReadOnlyCollection<Guid> appointmentTypeIds,
                CancellationToken cancellationToken)
            {
                await gate.WaitBeforeFirstCapacityLockAsync();
                return await _inner.LockForUpdateAsync(
                    eventId,
                    appointmentTypeIds,
                    cancellationToken);
            }
        }
    }

    /// <summary>Captures the attendee, tokens, and disjoint events used by one lifecycle race.</summary>
    private sealed record LifecycleScenario(
        Guid AttendeeId,
        string FirstToken,
        string SecondToken,
        Guid FirstEventId,
        Guid SecondEventId);

    /// <summary>Captures the attendee and event whose locked rows drive the deadlock regression.</summary>
    private sealed record EventCancellationScenario(Guid AttendeeId, Guid EventId);

    private sealed class AttendeeLifecycleRaceGate
    {
        private readonly TaskCompletionSource _firstCapacityLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstCapacityLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondAttendeeLock = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attendeeLockAttempts;
        private int _capacityLockAttempts;

        /// <summary>Signals when cancellation has attempted its attendee lifecycle lock.</summary>
        public void RecordAttendeeLockAttempt()
        {
            if (Interlocked.Increment(ref _attendeeLockAttempts) == 2)
            {
                _secondAttendeeLock.TrySetResult();
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

        /// <summary>Waits until cancellation has attempted to lock the attendee.</summary>
        public Task WaitUntilSecondAttendeeLockAsync() => _secondAttendeeLock.Task.WaitAsync(TimeSpan.FromSeconds(2));

        /// <summary>Allows deletion to finish its capacity update and release attendee locks.</summary>
        public void ReleaseFirstCapacityLock() => _releaseFirstCapacityLock.TrySetResult();
    }
}
