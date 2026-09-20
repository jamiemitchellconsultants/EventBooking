# 01c — Negotiation across any number of types, edits 9 (Task 6)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs — 1/1

<!-- retirement-file: {"id":25,"file":"tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs","beforeSha":"7c9718e4c6718bd0fd0679a9b91623ffc7fcdbcf041caa49f7e71c26f69366ef","afterSha":"7cfd25e4a72782c35cca6b31e1a1bb3b3414bcd2aba84ac0fea52e0f926943e3","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

[Collection("api")]
public class ManageBookingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task TheManageLinkShowsTheBookedTime()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingResponse>(
            $"/api/booking/manage/{booking.ManageToken}");

        Assert.NotNull(view);
        Assert.Equal("Amara Novak", view!.AttendeeName);
        Assert.Contains("-", view.Display);

        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync($"/api/booking/manage/{booking.ManageToken}"));
        var links = document.RootElement.GetProperty("_links");
        var cancel = links.GetProperty("cancel");
        Assert.Equal(
            $"/api/booking/manage/{Uri.EscapeDataString(booking.ManageToken)}/cancel",
            cancel.GetProperty("href").GetString());
        Assert.Equal("POST", cancel.GetProperty("method").GetString());
        Assert.Equal("cancelManagedBooking", cancel.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task CancellingWithoutRebookingReleasesTheBooking()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.False(outcome!.Reinvited);

        var afterwards = await client.GetAsync($"/api/booking/manage/{booking.ManageToken}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.NotYetInvited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        Assert.Empty(persisted.PendingInviteIds);
    }

    [Fact]
    public async Task CancelAndRebookIssuesAFreshInvite()
    {
        var booking = await GivenABooking();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/booking/manage/{booking.ManageToken}/cancel", new { Rebook = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var outcome = await response.Content.ReadFromJsonAsync<CancelResponse>();
        Assert.True(outcome!.Reinvited);

        var persisted = await ReadCancellationStateAsync(booking);
        Assert.Equal(BookingStatus.Cancelled, persisted.BookingStatus);
        Assert.Equal(AttendeeStatus.Invited, persisted.AttendeeStatus);
        Assert.Equal(persisted.TotalHeadcount, persisted.RemainingCapacity);
        var inviteId = Assert.Single(persisted.PendingInviteIds);
        Assert.NotEqual(booking.OriginalInviteId, inviteId);
    }

    [Fact]
    public async Task AnUnknownManageTokenIsNotFound()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/booking/manage/nonsense");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingFixture> GivenABooking()
    {
        var invite = await GivenAnInvitedAttendee();
        factory.SignedInAs = null;
        var client = factory.CreateClient();

        var view = await client.GetFromJsonAsync<BookingEndpointTests.InviteResponse>(
            $"/api/booking/{invite.Token}");

        var eventId = view!.Options[0].EventId;
        var confirmed = await client.PostAsJsonAsync(
            $"/api/booking/{invite.Token}/confirm",
            new { EventId = eventId });

        var outcome = await confirmed.Content.ReadFromJsonAsync<ConfirmResponse>();
        return new BookingFixture(
            outcome!.BookingId,
            outcome.ManageToken,
            invite.AttendeeId,
            invite.Id,
            eventId);
    }

    private async Task<AttendeeInviteFixture> GivenAnInvitedAttendee()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var eventIds = new List<Guid>();
        foreach (var (date, startTime) in new[]
                 {
                     (new DateOnly(2030, 1, 14), new TimeOnly(9, 0)),
                     (new DateOnly(2030, 1, 15), new TimeOnly(11, 0)),
                     (new DateOnly(2030, 1, 16), new TimeOnly(13, 0)),
                 })
        {
            var proposal = ProposalFixture.Create(
                Guid.NewGuid(), new EventWindow(date, startTime, 240), Guid.NewGuid());
            proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
            proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
            proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

            var eventId = Guid.NewGuid();
            context.EventProposals.Add(proposal);
            context.Events.Add(Event.CreateFrom(eventId, proposal));
            eventIds.Add(eventId);
        }

        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", $"{Guid.NewGuid():N}@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        attendee.MarkInvited();
        context.Attendees.Add(attendee);

        var inviteId = Guid.NewGuid();
        var issued = tokens.Issue(inviteId);
        context.Invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, issued.TokenHash, DateTimeOffset.UtcNow.AddDays(4),
            eventIds,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting], 0));
        await context.SaveChangesAsync();

        return new AttendeeInviteFixture(issued.Token, attendee.Id, inviteId);
    }

    private async Task<CancellationState> ReadCancellationStateAsync(BookingFixture booking)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();

        var persistedBooking = await context.Bookings.SingleAsync(b => b.Id == booking.BookingId);
        var attendee = await context.Attendees.SingleAsync(c => c.Id == booking.AttendeeId);
        var capacity = await context.EventCapacities.SingleAsync(c =>
            c.EventId == booking.EventId &&
            c.AppointmentTypeId == AppointmentTypeIds.DrugAndAlcoholTesting);
        var pendingInviteIds = await context.Invites
            .Where(i => i.AttendeeId == booking.AttendeeId && i.Status == InviteStatus.Pending)
            .Select(i => i.Id)
            .ToListAsync();

        return new CancellationState(
            persistedBooking.Status,
            attendee.Status,
            capacity.TotalHeadcount,
            capacity.RemainingCapacity,
            pendingInviteIds);
    }

    private sealed record ConfirmResponse(Guid BookingId, string ManageToken);

    private sealed record AttendeeInviteFixture(string Token, Guid AttendeeId, Guid Id);

    private sealed record BookingFixture(
        Guid BookingId,
        string ManageToken,
        Guid AttendeeId,
        Guid OriginalInviteId,
        Guid EventId);

    private sealed record CancellationState(
        BookingStatus BookingStatus,
        AttendeeStatus AttendeeStatus,
        int TotalHeadcount,
        int RemainingCapacity,
        IReadOnlyList<Guid> PendingInviteIds);

    private sealed record BookingResponse(DateOnly Date, string Display, string AttendeeName);

    private sealed record CancelResponse(bool Reinvited);
}
`````

## before — tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"336d6439b1c79a856bbf0bc88b87cf544de409ce0da3eb8cfccc27dea95f7579","afterSha":"60822ce2675d9cacb618415e5cd2affbead984dfbc2bf8d321a2ad47fb0e0896","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Attendees;

public class DeleteAttendeeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Attendee _attendee;

    private DeleteAttendeeHandler Handler => new(
        _attendees, _invites, _bookings, _events, _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        _audit,
        _unitOfWork);

    public DeleteAttendeeHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _attendees.Add(_attendee);
    }

    [Fact]
    public async Task AAttendeeWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfAAttendeeWithABookingIsRefusedWithAWarning()
    {
        GiveTheAttendeeABooking();

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this attendee will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var eventItem = GiveTheAttendeeABooking();
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var eventItem = GiveTheAttendeeABooking();
        var recoveryEvent = GiveTheAttendeeARecoveryBooking();
        Assert.Equal(9, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.AttendeeDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Event GiveTheAttendeeARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoveryEvent = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(recoveryEvent);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, original.Id, "recovery-hash", Now.AddDays(4),
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoveryEvent;
    }

    private Event GiveTheAttendeeABooking()
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(), _attendee.Id, "hash", Now.AddDays(4),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], _attendee.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            eventItem.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs — 1/1

<!-- retirement-file: {"id":26,"file":"tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs","beforeSha":"336d6439b1c79a856bbf0bc88b87cf544de409ce0da3eb8cfccc27dea95f7579","afterSha":"60822ce2675d9cacb618415e5cd2affbead984dfbc2bf8d321a2ad47fb0e0896","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Attendees;

public class DeleteAttendeeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryInviteRepository _invites = new();
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryEventRepository _events = new();
    private readonly InMemoryBookingAppointmentRepository _appointments;

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly Attendee _attendee;

    private DeleteAttendeeHandler Handler => new(
        _attendees, _invites, _bookings, _events, _roles,
        new BookingCanceller(_appointments, new InMemoryEventCapacityRepository(_events), _audit),
        _audit,
        _unitOfWork);

    public DeleteAttendeeHandlerTests()
    {
        _appointments = new InMemoryBookingAppointmentRepository(_bookings);
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));
        _attendees.Add(_attendee);
    }

    [Fact]
    public async Task AAttendeeWithNothingOutstandingIsDeletedOutright()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnUnconfirmedDeleteOfAAttendeeWithABookingIsRefusedWithAWarning()
    {
        GiveTheAttendeeABooking();

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(
            "Deleting this attendee will cancel 1 booking and 1 pending invite, and free the capacity they hold. Confirm to proceed.",
            result.Error.Message);
        Assert.Single(_attendees.Items);
    }

    [Fact]
    public async Task AConfirmedDeleteVoidsTheBookingAndGivesTheCapacityBack()
    {
        var eventItem = GiveTheAttendeeABooking();
        Assert.Equal(9, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, eventItem.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(6, eventItem.CapacityFor(AppointmentTypeIds.MedicalCheckUp).RemainingCapacity);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(InviteStatus.Superseded, _invites.Items.Single().Status);
        Assert.True(_audit.Contains(AuditAction.BookingCancelled));
        Assert.True(_audit.Contains(AuditAction.CapacityIncremented));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AConfirmedDeleteCascadesToTheActiveRecoveryBookingAndAuditsTheDeletion()
    {
        var eventItem = GiveTheAttendeeABooking();
        var recoveryEvent = GiveTheAttendeeARecoveryBooking();
        Assert.Equal(9, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);

        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_attendees.Items);
        Assert.Equal(2, _bookings.Items.Count);
        Assert.All(_bookings.Items, booking => Assert.Equal(BookingStatus.Cancelled, booking.Status));
        Assert.Equal(10, recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, eventItem.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.True(_audit.Contains(AuditAction.AttendeeDeleted));
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnUnknownAttendeeIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Coordinator, Guid.NewGuid(), true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new DeleteAttendeeCommand(Guid.NewGuid(), _attendee.Id, true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }

    private Event GiveTheAttendeeARecoveryBooking()
    {
        var original = _bookings.Items.Single();
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 11), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var recoveryEvent = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(recoveryEvent);

        var recoveryInvite = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, original.Id, "recovery-hash", Now.AddDays(4),
            [recoveryEvent.Id, Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recovery = Booking.CreateRecovery(
            Guid.NewGuid(), recoveryInvite, original, recoveryEvent.Id, "recovery-manage-hash", Now);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        recoveryEvent.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recoveryEvent;
    }

    private Event GiveTheAttendeeABooking()
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);

        var invite = Invite.CreateInitial(
            Guid.NewGuid(), _attendee.Id, "hash", Now.AddDays(4),
            [eventItem.Id, Guid.NewGuid(), Guid.NewGuid()], _attendee.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);

        var booking = Booking.Create(Guid.NewGuid(), invite, eventItem.Id, "manage-hash", Now);
        _bookings.Add(booking);

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            eventItem.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), booking.Id, typeId));
        }

        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"fadb8cee255319013dc28517bd96323ea687766c558c95633392ba39feef35b6","afterSha":"b0f4c04647ad80fea330180ecc88413b8df33893daf75475e0c1059cc93c7144","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, token.TokenHash, clock.UtcNow.AddDays(4),
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot, 0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
`````

## after — tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs — 1/1

<!-- retirement-file: {"id":27,"file":"tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs","beforeSha":"fadb8cee255319013dc28517bd96323ea687766c558c95633392ba39feef35b6","afterSha":"b0f4c04647ad80fea330180ecc88413b8df33893daf75475e0c1059cc93c7144","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Bookings;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies booking confirmation snapshots required operational appointments atomically.</summary>
public sealed class BookingAppointmentSnapshotTests
{
    /// <summary>Verifies one Expected appointment is created for each current attendee requirement.</summary>
    [Fact]
    public async Task ConfirmationCreatesOneAppointmentPerRequirementBeforeTheSingleSave()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        var attendee = Attendee.Create(
            Guid.NewGuid(),
            "Amara Novak",
            "amara@example.com",
            pilots);
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var second = AddEvent(events, new DateOnly(2026, 9, 9));
        var third = AddEvent(events, new DateOnly(2026, 9, 10));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId,
            attendee.Id,
            token.TokenHash,
            clock.UtcNow.AddDays(4),
            [selected.Id, second.Id, third.Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting],
            0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var deliveries = EmailDeliveryTestFactory.Create(
            new InMemoryEmailDeliveryRepository(),
            new RecordingEmailSender(),
            new FakeUnitOfWork(),
            clock);
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            deliveries,
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, appointments.Items.Count);
        Assert.All(appointments.Items, value =>
        {
            Assert.Equal(result.Value.BookingId, value.BookingId);
            Assert.Equal(BookingAppointmentStatus.Expected, value.Status);
            Assert.Equal(1, value.Version);
        });
        Assert.Equal(
            attendee.RequiredAppointmentTypeIds.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    /// <summary>Verifies one-, two-, and three-type snapshots each produce their exact set.</summary>
    [Theory]
    [InlineData("MED")]
    [InlineData("DAT,UNI")]
    [InlineData("DAT,MED,UNI")]
    public async Task ConfirmationCreatesTheExactSnapshotSet(string codes)
    {
        var byCode = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["DAT"] = AppointmentTypeIds.DrugAndAlcoholTesting,
            ["MED"] = AppointmentTypeIds.MedicalCheckUp,
            ["UNI"] = AppointmentTypeIds.UniformFitting,
        };
        var snapshot = codes.Split(',').Select(code => byCode[code]).ToList();

        var clock = new FakeClock(new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero));
        var tokens = new FakeTokenService();
        var attendees = new InMemoryAttendeeRepository();
        var attendee = Attendee.Create(
            Guid.NewGuid(), "Amara Novak", "amara@example.com",
            AttendeeGroup.Define(
                Guid.NewGuid(), "DAT_ONLY", "DAT only", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting]));
        attendees.Add(attendee);
        attendee.MarkInvited();

        var events = new InMemoryEventRepository();
        var selected = AddEvent(events, new DateOnly(2026, 9, 8));
        var inviteId = Guid.NewGuid();
        var token = tokens.Issue(inviteId);
        var invites = new InMemoryInviteRepository();
        invites.Add(Invite.CreateInitial(
            inviteId, attendee.Id, token.TokenHash, clock.UtcNow.AddDays(4),
            [selected.Id, AddEvent(events, new DateOnly(2026, 9, 9)).Id,
                AddEvent(events, new DateOnly(2026, 9, 10)).Id],
            snapshot, 0));

        var bookings = new InMemoryBookingRepository();
        var appointments = new InMemoryBookingAppointmentRepository(bookings);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ConfirmBookingHandler(
            invites,
            attendees,
            events,
            bookings,
            appointments,
            new InMemoryEventCapacityRepository(events),
            new EligibleEventFinder(events, clock),
            tokens,
            EmailDeliveryTestFactory.Create(
                new InMemoryEmailDeliveryRepository(),
                new RecordingEmailSender(),
                new FakeUnitOfWork(),
                clock),
            new RecordingAuditLogger(),
            unitOfWork,
            clock,
            new AttendeePortalOptions(
                "https://booking.example.com", "help@example.com"));

        // Each snapshot size needs its matching group so confirmation proceeds.
        var group = snapshot.Count switch
        {
            1 => AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            2 => AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            _ => AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                    AppointmentTypeIds.UniformFitting]),
        };
        attendee.AssignAttendeeGroup(group);

        var result = await handler.HandleAsync(
            new ConfirmBookingCommand(token.Token, selected.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            snapshot.OrderBy(value => value),
            appointments.Items.Select(value => value.AppointmentTypeId).OrderBy(value => value));
    }

    private static Event AddEvent(
        InMemoryEventRepository events,
        DateOnly date)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(date, new TimeOnly(9, 0), 240), Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 10);
        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        events.Add(eventItem);
        return eventItem;
    }
}
`````

## before — tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs — 1/1

<!-- retirement-file: {"id":28,"file":"tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs","beforeSha":"b6a9ac931a11e96af83147a521c3251b4bb999a7cd0124cffe316305438b13fc","afterSha":"af5114f536a465b1c913f523b763c9fc710699bb642889f1e2b4e617f8b91172","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Bookings;

/// <summary>Verifies a coordinator cancelling one attendee booking reuses the attendee path exactly.</summary>
public class CancelAttendeeBookingHandlerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private readonly TransactionOperationLog _operations = new();
    private readonly InMemoryBookingRepository _bookings;
    private readonly InMemoryAttendeeRepository _attendees;
    private readonly InMemoryEventRepository _events;
    private readonly InMemoryEventCapacityRepository _capacities;
    private readonly InMemoryBookingAppointmentRepository _appointments;
    private readonly InMemoryInviteRepository _invites;
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemorySystemSettingsRepository _settings = new();
    private readonly RecordingEmailSender _email = new();
    private readonly InMemoryEmailDeliveryRepository _deliveries = new();
    private readonly RecordingAuditLogger _audit = new();
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FakeTokenService _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Attendee _attendee;
    private readonly Event _booked;
    private readonly Guid _bookingId;
    private readonly Guid _staffUserId = Guid.NewGuid();

    /// <summary>Grants or denies exactly the capability the handler is expected to demand.</summary>
    private sealed class FakeAuthorizer(bool granted) : IStaffAccessAuthorizer
    {
        public StaffCapability? Seen { get; private set; }

        public Task<Result<StaffAccessContext>> AuthorizeAsync(
            Guid staffUserId,
            StaffCapability capability,
            Guid? requiredAppointmentTypeId,
            CancellationToken cancellationToken)
        {
            Seen = capability;
            return Task.FromResult(granted
                ? Result<StaffAccessContext>.Success(
                    new StaffAccessContext(staffUserId, new HashSet<Role> { Role.Coordinator }, null))
                : Result<StaffAccessContext>.Failure(
                    Error.Forbidden("This staff profile cannot perform this operation.")));
        }
    }

    private CancelAttendeeBookingHandler HandlerFor(IStaffAccessAuthorizer access) => new(
        access,
        _bookings,
        _events,
        _attendees,
        _invites,
        new BookingCanceller(_appointments, _capacities, _audit),
        new InviteIssuer(
            _invites, _groups, new EligibleEventFinder(_events, _clock), _settings,
            _tokens, EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
            _audit, _clock, Portal),
        EmailDeliveryTestFactory.Create(_deliveries, _email, _unitOfWork, _clock),
        _clock,
        _unitOfWork);

    private CancelAttendeeBookingHandler Handler => HandlerFor(new FakeAuthorizer(true));

    public CancelAttendeeBookingHandlerTests()
    {
        _invites = new InMemoryInviteRepository(_operations);
        _bookings = new InMemoryBookingRepository(_operations);
        _attendees = new InMemoryAttendeeRepository(_operations);
        _events = new InMemoryEventRepository(_operations);
        _capacities = new InMemoryEventCapacityRepository(_events, _operations);
        _appointments = new InMemoryBookingAppointmentRepository(_bookings, _operations);
        _unitOfWork = new FakeUnitOfWork(_operations);

        var pilots = AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]);
        _groups.Items.Add(pilots);
        _attendee = Attendee.Create(Guid.NewGuid(), "Amara Novak", "a.novak@mail.com", pilots);
        _attendees.Add(_attendee);

        _booked = AddEvent(10);
        AddEvent(12);
        AddEvent(14);
        AddEvent(16);

        var inviteId = Guid.NewGuid();
        var issued = _tokens.Issue(inviteId);
        var invite = Invite.CreateInitial(
            inviteId, _attendee.Id, issued.TokenHash, _clock.UtcNow.AddDays(4),
            [_booked.Id, _events.Items[1].Id, _events.Items[2].Id],
            _attendee.RequiredAppointmentTypeIds, 0);
        _invites.Add(invite);
        _attendee.MarkInvited();

        _bookingId = Guid.NewGuid();
        var manage = _tokens.Issue(_bookingId);
        _bookings.Add(Booking.Create(_bookingId, invite, _booked.Id, manage.TokenHash, _clock.UtcNow));
        invite.MarkUsed();
        _attendee.MarkBooked();

        foreach (var typeId in _attendee.RequiredAppointmentTypeIds)
        {
            _booked.CapacityFor(typeId).Decrement();
            _appointments.Add(BookingAppointment.Create(Guid.NewGuid(), _bookingId, typeId));
        }
    }

    private CancelAttendeeBookingCommand Command(Guid bookingId, bool rebook = false) =>
        new(_staffUserId, _attendee.Id, bookingId, rebook);

    [Fact]
    public async Task CancellingAnOriginalWithNoActiveRecoveryVoidsItResetsTheAttendeeAndAuditsStaffAttribution()
    {
        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single().Status);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(8, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);
        Assert.Equal(1, _unitOfWork.CommitCount);
        Assert.Empty(_email.Sent);

        var cancelled = Assert.Single(_audit.Entries, e => e.Action == AuditAction.BookingCancelled);
        Assert.Equal(ActorType.Staff, cancelled.ActorType);
        Assert.Equal(_staffUserId.ToString(), cancelled.ActorId);
    }

    [Fact]
    public async Task CancellingAnOriginalWithAnActiveRecoveryCascadesOntoTheRecoveryFirst()
    {
        var recovery = AddActiveRecovery();

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Cancelled, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.NotYetInvited, _attendee.Status);

        var cancellations = _audit.Entries.Where(e => e.Action == AuditAction.BookingCancelled).ToList();
        Assert.Equal(2, cancellations.Count);
        Assert.All(cancellations, e => Assert.Equal(ActorType.Staff, e.ActorType));
        Assert.All(cancellations, e => Assert.Equal(_staffUserId.ToString(), e.ActorId));
    }

    [Fact]
    public async Task CancellingAnOriginalSupersedesItsPendingRecoveryInvite()
    {
        var pendingRecovery = Invite.CreateRecovery(
            Guid.NewGuid(), _attendee.Id, _bookingId, "hash-recovery-pending",
            _clock.UtcNow.AddDays(4),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(pendingRecovery);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InviteStatus.Superseded, pendingRecovery.Status);
    }

    [Fact]
    public async Task CancellingARecoveryBookingAloneLeavesTheOriginalAndItsCapacityUntouched()
    {
        var recovery = AddActiveRecovery();
        var originalDat = _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity;
        var originalUni = _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity;

        var result = await Handler.HandleAsync(Command(recovery.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Reinvited);
        Assert.Equal(BookingStatus.Cancelled, recovery.Status);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(
            10, _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalDat, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
        Assert.Equal(originalUni, _booked.CapacityFor(AppointmentTypeIds.UniformFitting).RemainingCapacity);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RebookTrueOnAnOriginalIssuesAReplacementInviteAndReportsReinvited()
    {
        var result = await Handler.HandleAsync(Command(_bookingId, rebook: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Reinvited);
        Assert.True(result.Value.InviteCreated);
        Assert.Equal(AttendeeStatus.Invited, _attendee.Status);
        Assert.Single(_email.Sent);
        Assert.Equal(2, _invites.Items.Count);
        Assert.Equal(InviteStatus.Pending, _invites.Items[1].Status);
        Assert.Contains(_booked.Id, _invites.Items[1].OfferedEventIds);
    }

    [Fact]
    public async Task RebookTrueOnARecoveryBookingIsAConflictAndMutatesNothing()
    {
        var recovery = AddActiveRecovery();

        var result = await Handler.HandleAsync(
            Command(recovery.Id, rebook: true), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, recovery.Status);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single(b => b.IsOriginal).Status);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task UnknownBookingIdIsNotFound()
    {
        var result = await Handler.HandleAsync(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CancellingAfterTheEventDateIsRefused()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task AnotherAttendeesBookingIsNotFound()
    {
        var result = await Handler.HandleAsync(
            new CancelAttendeeBookingCommand(_staffUserId, Guid.NewGuid(), _bookingId, false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task ANonActiveBookingIsNotFound()
    {
        await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        var result = await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal(10, _booked.CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).RemainingCapacity);
    }

    [Fact]
    public async Task CallerWithoutManageAttendeesIsForbiddenAndMutatesNothing()
    {
        var authorizer = new FakeAuthorizer(false);

        var result = await HandlerFor(authorizer).HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(StaffCapability.ManageAttendees, authorizer.Seen);
        Assert.Equal(BookingStatus.Active, _bookings.Items.Single().Status);
        Assert.Equal(AttendeeStatus.Booked, _attendee.Status);
        Assert.Equal(0, _unitOfWork.CommitCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task CancellationTakesTheAttendeeLifecycleLockOrder()
    {
        await Handler.HandleAsync(Command(_bookingId), CancellationToken.None);

        Assert.Equal(
            [
                "transaction-begun",
                "attendee-locked",
                "pending-invites-locked",
                "booking-locked",
                "active-recovery-locked",
                "event-guard-locked",
                "capacity-locked"
            ],
            _operations.Events);
    }

    private Booking AddActiveRecovery()
    {
        var original = _bookings.Items.Single();
        var recoveryInviteId = Guid.NewGuid();
        var issued = _tokens.Issue(recoveryInviteId);
        var recoveryInvite = Invite.CreateRecovery(
            recoveryInviteId, _attendee.Id, original.Id, issued.TokenHash,
            _clock.UtcNow.AddDays(4),
            [_events.Items[1].Id, _events.Items[2].Id, _events.Items[3].Id],
            [AppointmentTypeIds.DrugAndAlcoholTesting]);
        _invites.Add(recoveryInvite);

        var recoveryId = Guid.NewGuid();
        var manage = _tokens.Issue(recoveryId);
        var recovery = Booking.CreateRecovery(
            recoveryId, recoveryInvite, original, _events.Items[1].Id,
            manage.TokenHash, _clock.UtcNow);
        _bookings.Add(recovery);
        recoveryInvite.MarkUsed();

        _events.Items[1].CapacityFor(AppointmentTypeIds.DrugAndAlcoholTesting).Decrement();
        _appointments.Add(BookingAppointment.Create(
            Guid.NewGuid(), recovery.Id, AppointmentTypeIds.DrugAndAlcoholTesting));

        return recovery;
    }

    private Event AddEvent(int day)
    {
        var proposal = EventProposal.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(9, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        var eventItem = Event.CreateFrom(Guid.NewGuid(), proposal);
        _events.Add(eventItem);
        return eventItem;
    }
}
`````
