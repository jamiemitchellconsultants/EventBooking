using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Recovery;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Tests.Recovery;

public sealed class RecoveryHandlerTests
{
    private static StartRecoveryHandler Starter(RecoveryFixture f) => new(
        f.Attendees, f.Bookings, f.Appointments, f.Invites, f.Locations, f.Settings,
        f.Profiles, f.UnitOfWork, f.Clock,
        f.Eligibility, new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility));

    [Fact]
    public async Task Start_recovery_defaults_to_booking_location_and_snapshots_recoverable()
    {
        var fixture = RecoveryFixture.Create();
        fixture.Eligibility.EligibleInOrder = [fixture.Events.Items.Single().Id];

        var result = await Starter(fixture).HandleAsync(
            new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, [fixture.TokyoId]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(fixture.LondonId, result.Value.LocationIds);
        Assert.Contains(fixture.TokyoId, result.Value.LocationIds);
        Assert.Equal([fixture.MedId], result.Value.RecoverableTypeIds);
    }

    [Fact]
    public async Task Second_recovery_while_active_is_refused()
    {
        var fixture = RecoveryFixture.Create();
        fixture.Eligibility.EligibleInOrder = [fixture.Events.Items.Single().Id];
        var first = await Starter(fixture).HandleAsync(
            new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
            CancellationToken.None);
        Assert.True(first.IsSuccess);

        var result = await Starter(fixture).HandleAsync(
            new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("recovery-active", result.Error.Code);
    }

    [Fact]
    public async Task Recovery_without_manage_attendees_is_forbidden()
    {
        var fixture = RecoveryFixture.Create();
        fixture.RemoveCoordinator();

        var result = await Starter(fixture).HandleAsync(
            new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Recovery_booking_concludes_when_all_appointments_terminal()
    {
        var fixture = RecoveryFixture.Create();
        fixture.Eligibility.EligibleInOrder = [fixture.Events.Items.Single().Id];
        var started = await Starter(fixture).HandleAsync(
            new StartRecoveryCommand(fixture.Coordinator, fixture.AttendeeId, []),
            CancellationToken.None);
        Assert.True(started.IsSuccess);

        var recoveryInvite = fixture.Invites.Items.Single(i => i.Id == started.Value.RecoveryInviteId);
        var token = fixture.Tokens.Issue(TokenPurpose.Book, recoveryInvite.Id, recoveryInvite.TokenVersion);
        var confirmer = new ConfirmBookingHandler(
            fixture.Invites, fixture.Attendees, fixture.Events, fixture.Capacities,
            fixture.Bookings, fixture.Appointments, fixture.Locations, fixture.Tokens,
            fixture.Emails, fixture.UnitOfWork, fixture.Audit, fixture.Clock,
            RecoveryZones.Instance);
        var confirmed = await confirmer.HandleAsync(
            new ConfirmBookingCommand(token, recoveryInvite.Options[0].EventId),
            CancellationToken.None);
        Assert.True(confirmed.IsSuccess);

        foreach (var appointment in fixture.Appointments.Items.Where(a => a.BookingId == confirmed.Value.BookingId))
        {
            appointment.TransitionTo(BookingAppointmentStatus.CheckedIn, fixture.Coordinator,
                RecoveryFixture.Now, true, false);
            appointment.TransitionTo(BookingAppointmentStatus.Completed, fixture.Coordinator,
                RecoveryFixture.Now, true, false);
        }

        var result = await new ConcludeRecoveryHandler(
            fixture.Bookings, fixture.Appointments, fixture.UnitOfWork, fixture.Audit)
            .HandleAsync(confirmed.Value.BookingId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Concluded,
            fixture.Bookings.Items.Single(b => b.Id == confirmed.Value.BookingId).Status);
        Assert.Contains(fixture.Audit.Entries, e => e.Action == AuditAction.RecoveryBookingConcluded);
    }
}
