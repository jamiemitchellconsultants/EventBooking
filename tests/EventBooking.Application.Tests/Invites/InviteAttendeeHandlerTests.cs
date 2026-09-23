using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Tests.Invites;

public sealed class InviteAttendeeHandlerTests
{
    private static InviteAttendeeHandler Handler(InviteFixture f) => new(
        f.Attendees, f.Locations, f.Profiles, f.UnitOfWork, f.Clock,
        new InviteIssuer(f.Invites, f.Settings, f.Emails, f.Audit, f.Clock, f.Eligibility));

    [Fact]
    public async Task Invite_with_full_options_creates_invite_and_stages_email()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal("Invited", result.Value.Status);
        var invite = fixture.Invites.Items.Single(i => i.Id == result.Value.InviteId);
        Assert.Equal(fixture.Eligibility.EligibleInOrder, invite.Options.Select(o => o.EventId).ToList());
        Assert.Equal(fixture.LocationIds.Order().ToList(), invite.LocationIds.Order().ToList());
        Assert.Equal(InviteFixture.Now.AddDays(7), invite.ExpiresAt);
        Assert.Equal(3, invite.InviteOptionCount);
        Assert.Equal("Invited", fixture.Attendees.Items.Single().Status.ToString());
        var delivery = Assert.Single(fixture.Emails.Items);
        Assert.Equal(EmailTemplate.AttendeeInvite, delivery.TemplateName);
        Assert.Equal(EmailStatus.Pending, delivery.Status);
        Assert.Equal(invite.Id, delivery.InviteId);
        var entry = Assert.Single(fixture.Audit.Entries);
        Assert.Equal(AuditAction.InviteCreated, entry.Action);
    }

    [Fact]
    public async Task Invite_short_of_options_parks_attendee_with_no_side_effects()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(2);

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("insufficient-events", result.Error.Code);
        Assert.Equal("AwaitingAvailability", fixture.Attendees.Items.Single().Status.ToString());
        Assert.Empty(fixture.Invites.Items);
        Assert.Empty(fixture.Emails.Items);
        Assert.Empty(fixture.Audit.Entries);
    }

    [Fact]
    public async Task Inactive_location_refuses_with_nothing_created()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3).WithInactiveLocation();

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
        Assert.Empty(fixture.Invites.Items);
        Assert.Empty(fixture.Emails.Items);
    }

    [Fact]
    public async Task Reinvite_supersedes_the_pending_invite()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
        var first = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);
        fixture.Attendees.Items.Single().MarkNoResponse(InviteFixture.Now);

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, [fixture.LocationIds[1]]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal(Domain.Invites.InviteStatus.Superseded,
            fixture.Invites.Items.Single(i => i.Id == first.Value.InviteId).Status);
        var second = fixture.Invites.Items.Single(i => i.Id == result.Value.InviteId);
        Assert.Equal([fixture.LocationIds[1]], second.LocationIds.ToList());
        Assert.Equal(0, second.RetryCount);
    }

    [Fact]
    public async Task Replacement_of_usable_pending_invite_supersedes_it()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
        var first = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);
        Assert.True(first.IsSuccess, $"first failed: {first.Error?.Code} {first.Error?.Message}");

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, [fixture.LocationIds[0]]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, $"result failed: {result.Error?.Code} {result.Error?.Message}");
        Assert.Equal(Domain.Invites.InviteStatus.Superseded,
            fixture.Invites.Items.Single(i => i.Id == first.Value.InviteId).Status);
        Assert.Equal("Invited", fixture.Attendees.Items.Single().Status.ToString());
    }

    [Fact]
    public async Task Manager_cannot_invite()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
        var manager = Guid.NewGuid();
        fixture.Profiles.Add(StaffAccessProfile.Create(manager, Role.Manager, fixture.MedId));

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(manager, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Empty(fixture.Invites.Items);
    }

    [Fact]
    public async Task Booked_attendee_cannot_be_reinvited()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);
        var attendee = fixture.Attendees.Items.Single();
        attendee.MarkInvited(InviteFixture.Now);
        attendee.MarkBooked(InviteFixture.Now);

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, fixture.AttendeeId, fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Empty(fixture.Invites.Items);
    }

    [Fact]
    public async Task Unknown_attendee_is_not_found()
    {
        var fixture = InviteFixture.Create(optionCount: 3).WithEligibleEvents(3);

        var result = await Handler(fixture).HandleAsync(
            new InviteAttendeeCommand(fixture.Coordinator, Guid.NewGuid(), fixture.LocationIds),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }
}
