using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Application.Common;
using EventBooking.Application.Tests.Recovery;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Appointments;

public sealed class WorkspaceHandlerTests
{
    private static SetAppointmentStatusHandler StatusHandler(RecoveryFixture f) =>
        new(f.Appointments, f.Bookings, f.Invites, f.Events, f.Locations, f.Profiles,
            f.UnitOfWork, f.Audit, f.Clock, RecoveryZones.Instance);

    private static GetWorkspaceRosterHandler RosterHandler(RecoveryFixture f) =>
        new(f.Appointments, f.Bookings, f.Attendees, f.Events, f.Types, f.Profiles);

    [Fact]
    public async Task Dublin_checkin_before_utc_midnight_is_allowed_on_local_date()
    {
        var fixture = RecoveryFixture.Create();
        var appointmentId = fixture.WithDublinBooking();
        fixture.Clock.UtcNow = new DateTimeOffset(2026, 6, 14, 23, 30, 0, TimeSpan.Zero);
        var appointment = fixture.Appointments.Items.Single(a => a.Id == appointmentId);
        var status = StatusHandler(fixture);

        var result = await status.HandleAsync(new SetAppointmentStatusCommand(
            fixture.MedStaff, appointment.Id, "CheckedIn", appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingAppointmentStatus.CheckedIn, appointment.Status);
    }

    [Fact]
    public async Task Noshow_before_end_is_refused_and_roster_hides_ids()
    {
        var fixture = RecoveryFixture.Create();
        var appointment = RecoveryFixture.Apps(fixture).Single(a => a.AppointmentTypeId == fixture.MedId);
        // Back to Expected first: the refusal under test is the window rule, and a
        // same-status request would return without reaching it.
        appointment.TransitionTo(BookingAppointmentStatus.Expected, fixture.Coordinator,
            RecoveryFixture.Now, false, false);
        var status = StatusHandler(fixture);

        var result = await status.HandleAsync(new SetAppointmentStatusCommand(
            fixture.MedStaff, appointment.Id, "NoShow", appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);

        var roster = await RosterHandler(fixture)
            .HandleAsync(new GetWorkspaceRosterQuery(fixture.MedStaff, fixture.Events.Items.Single().Id),
                CancellationToken.None);
        Assert.True(roster.IsSuccess);
        var json = JsonSerializer.Serialize(roster.Value);
        Assert.DoesNotContain(fixture.AttendeeId.ToString(), json);
        Assert.DoesNotContain(fixture.BookingId.ToString(), json);
    }

    [Fact]
    public async Task Med_profile_never_sees_fit_rows_and_csv_neutralises_formulas()
    {
        var fixture = RecoveryFixture.Create();
        var roster = await RosterHandler(fixture)
            .HandleAsync(new GetWorkspaceRosterQuery(fixture.MedStaff, fixture.Events.Items.Single().Id),
                CancellationToken.None);

        Assert.True(roster.IsSuccess);
        Assert.All(roster.Value, row => Assert.Equal("MED", row.ScopeTypeCode));

        var csv = RosterCsv.Render(
            [new WorkspaceRosterRow("=cmd", "+x", "MED", "Expected", null, 1)],
            ["Name", "Email", "Status", "CheckedInAt", "Version"]);
        Assert.Contains("'=cmd", csv);
        Assert.Contains("'+x", csv);
    }

    [Fact]
    public async Task Stale_appointment_version_returns_current_state()
    {
        var fixture = RecoveryFixture.Create();
        var appointment = RecoveryFixture.Apps(fixture).First();
        var status = StatusHandler(fixture);

        var result = await status.HandleAsync(new SetAppointmentStatusCommand(
            fixture.MedStaff, appointment.Id, "CheckedIn", 99), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("appointment_version_conflict", result.Error.Code);
        Assert.Equal(appointment.Version, result.Error.Data!["currentVersion"]);
    }

    [Fact]
    public async Task Noshow_correction_is_refused_while_recovery_invite_pending()
    {
        var fixture = RecoveryFixture.Create();
        fixture.Eligibility.EligibleInOrder = [fixture.Events.Items.Single().Id];
        var starter = new Application.Recovery.StartRecoveryHandler(
            fixture.Attendees, fixture.Bookings, fixture.Appointments, fixture.Invites,
            fixture.Locations, fixture.Settings, fixture.Profiles,
            fixture.UnitOfWork, fixture.Clock,
            fixture.Eligibility, new Application.Invites.InviteIssuer(fixture.Invites,
                fixture.Settings, fixture.Emails, fixture.Audit, fixture.Clock, fixture.Eligibility));
        var started = await starter.HandleAsync(
            new Application.Recovery.StartRecoveryCommand(
                fixture.Coordinator, fixture.AttendeeId, []),
            CancellationToken.None);
        Assert.True(started.IsSuccess);

        var appointment = RecoveryFixture.Apps(fixture).Single(a => a.AppointmentTypeId == fixture.MedId);
        var result = await StatusHandler(fixture).HandleAsync(new SetAppointmentStatusCommand(
            fixture.MedStaff, appointment.Id, "Expected", appointment.Version),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery-active", result.Error.Code);
    }

    [Fact]
    public async Task Workspace_event_list_is_bounded_scoped_and_ordered()
    {
        var fixture = RecoveryFixture.Create();
        var inside = GivenWorkspaceEvent(fixture, new DateOnly(2026, 10, 10), new TimeOnly(9, 30));
        GivenWorkspaceEvent(fixture, new DateOnly(2026, 9, 20), new TimeOnly(9, 30));
        GivenWorkspaceEvent(fixture, new DateOnly(2026, 10, 30), new TimeOnly(9, 30));
        var workspace = new StubWorkspaceQueries(fixture.Events.Items.Select(e => e.Id).ToList());
        var handler = new ListWorkspaceEventsHandler(
            workspace, fixture.Events, fixture.Locations, fixture.Profiles,
            fixture.Clock, RecoveryZones.Instance);

        var result = await handler.HandleAsync(
            new ListWorkspaceEventsQuery(fixture.MedStaff, fixture.LondonId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var view = Assert.Single(result.Value);
        Assert.Equal(inside, view.EventId);
        Assert.Equal(fixture.LondonId, view.LocationId);
        Assert.Equal("London HQ", view.LocationName);
        Assert.Equal(new DateOnly(2026, 10, 10), view.Date);
        Assert.Equal("T", view.ZoneAbbreviation);
        Assert.Equal(fixture.LondonId, workspace.ReceivedLocationId);
    }

    [Fact]
    public async Task Workspace_event_list_needs_an_assigned_type()
    {
        var fixture = RecoveryFixture.Create();
        var handler = new ListWorkspaceEventsHandler(
            new StubWorkspaceQueries([]), fixture.Events, fixture.Locations, fixture.Profiles,
            fixture.Clock, RecoveryZones.Instance);

        var result = await handler.HandleAsync(
            new ListWorkspaceEventsQuery(fixture.Coordinator, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Roster_download_renders_neutralised_csv()
    {
        var fixture = RecoveryFixture.Create();
        var result = await new DownloadRosterHandler(RosterHandler(fixture)).HandleAsync(
            new GetWorkspaceRosterQuery(fixture.MedStaff, fixture.Events.Items.Single().Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("Amy", result.Value);
        Assert.DoesNotContain(fixture.AttendeeId.ToString(), result.Value);
    }

    private static Guid GivenWorkspaceEvent(RecoveryFixture fixture, DateOnly date, TimeOnly start)
    {
        var proposal = EventProposal.Propose(Guid.NewGuid(), fixture.LondonId, true, "Europe/London",
            new EventWindow(date, start, 90),
            RecoveryZones.Instance, new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
            [new ProposableAppointmentType(fixture.MedId, "MED", true, true),
             new ProposableAppointmentType(fixture.FitId, "FIT", true, true)],
            fixture.MedId, fixture.Coordinator, 10);
        proposal.Accept(fixture.FitId, fixture.Coordinator, 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        fixture.Events.Items.Add(eventItem);
        return eventItem.Id;
    }

    private sealed class StubWorkspaceQueries(IReadOnlyList<Guid> ids) : IWorkspaceQueries
    {
        public Guid? ReceivedLocationId { get; private set; }

        public Task<IReadOnlyList<Guid>> ListWorkspaceEventIdsAsync(
            Guid appointmentTypeId, Guid? locationId,
            DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            ReceivedLocationId = locationId;
            return Task.FromResult(ids);
        }
    }
}
