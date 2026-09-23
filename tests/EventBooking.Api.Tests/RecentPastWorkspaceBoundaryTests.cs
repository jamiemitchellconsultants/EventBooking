using System.Net;
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Slots;
using EventBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Api.Tests;

/// <summary>Verifies recently past slots keep the scoped, minimum-data workspace boundary.</summary>
[Collection("api")]
public sealed class RecentPastWorkspaceBoundaryTests(ApiFactory factory)
{
    /// <summary>Verifies Manager and AppointmentStaff both reach a recently past slot.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRolesReachRecentlyPastSlots(Role role)
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [role], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var list = await client.GetAsync("/api/appointment-workspace/slots");
        using var detail = await client.GetAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains(data.SlotId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a recently past slot outside trusted scope is indistinguishable from missing.</summary>
    [Fact]
    public async Task CrossTypeRecentlyPastSlotIsNotFound()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.UniformFitting);

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Verifies unscoped callers are denied recently past slots without data.</summary>
    [Fact]
    public async Task UnassignedCallerIsForbiddenRecentlyPastSlot()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = Guid.NewGuid();

        using var response = await factory.CreateClient().GetAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Verifies a recently past slot keeps the exact approved minimum-data shape.</summary>
    [Fact]
    public async Task RecentlyPastResponsesKeepTheApprovedPropertySets()
    {
        var data = await GivenWorkspaceAsync(
            AppointmentTypeIds.DrugAndAlcoholTesting, daysBeforeToday: 1);
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.AppointmentStaff], AppointmentTypeIds.DrugAndAlcoholTesting);
        var client = factory.CreateClient();

        using var detail = JsonDocument.Parse(await client.GetStringAsync(
            $"/api/appointment-workspace/slots/{data.SlotId}"));
        AssertKeys(detail.RootElement,
            "appointmentTypeName", "confirmedSlotId", "date", "startTime", "endTime", "appointments", "_links");
        var row = Assert.Single(detail.RootElement.GetProperty("appointments").EnumerateArray());
        AssertKeys(row, "bookingAppointmentId", "candidateName", "candidateEmail",
            "status", "checkedInAt", "outcomeAt", "version", "_links");
        Assert.DoesNotContain("candidateId", detail.RootElement.GetRawText());
        Assert.DoesNotContain("bookingId", detail.RootElement.GetRawText());
    }

    private async Task<(Guid SlotId, Guid AppointmentId)> GivenWorkspaceAsync(
        Guid appointmentTypeId,
        int daysBeforeToday)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayAtHeadOffice;
        var slot = ConfirmedSlot.CreateImported(
            Guid.NewGuid(), new SlotWindow(today.AddDays(-daysBeforeToday), new TimeOnly(9, 0)),
            AppointmentTypeIds.All.ToDictionary(id => id, _ => 20));
        var groupId = appointmentTypeId == AppointmentTypeIds.MedicalCheckUp
            ? EmployeeGroupIds.GroundOperationsAgent
            : EmployeeGroupIds.Pilots;
        var group = context.EmployeeGroups.Include(g => g.Requirements).Single(g => g.Id == groupId);
        var candidate = Candidate.Create(
            Guid.NewGuid(), "Alex Morgan", $"alex-{Guid.NewGuid():N}@example.com", group);
        var invite = Invite.CreateInitial(
            Guid.NewGuid(), candidate.Id, $"invite-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow.AddDays(1), [slot.Id, Guid.NewGuid(), Guid.NewGuid()],
            candidate.RequiredAppointmentTypeIds, 0);
        var booking = Booking.Create(
            Guid.NewGuid(), invite, slot.Id, $"manage-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var appointment = BookingAppointment.Create(
            Guid.NewGuid(), booking.Id, appointmentTypeId);
        context.AddRange(slot, candidate, booking, appointment);
        await context.SaveChangesAsync();
        return (slot.Id, appointment.Id);
    }

    /// <summary>Asserts a JSON object carries exactly the approved property names.</summary>
    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
