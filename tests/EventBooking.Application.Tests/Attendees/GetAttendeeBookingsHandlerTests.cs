using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies staff authorization and shape of the attendee active-booking listing.</summary>
public sealed class GetAttendeeBookingsHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Manager = Guid.Parse("c0000007-0000-0000-0000-000000000007");

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly InMemoryAttendeeBookingQueries _queries = new();

    private GetAttendeeBookingsHandler Handler =>
        new(new StaffAccessAuthorizer(_roles), _queries);

    public GetAttendeeBookingsHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(
            Manager, Role.Manager, Domain.AppointmentTypes.AppointmentTypeIds.MedicalCheckUp));
    }

    [Fact]
    public async Task CoordinatorGetsNoRowsWhenTheAttendeeHasNoActiveBooking()
    {
        _queries.Rows = [];

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task CoordinatorGetsOneActiveBookingWithItsEventWindow()
    {
        var bookingId = Guid.NewGuid();
        _queries.Rows =
        [
            new AttendeeBookingSummary(
                bookingId, true, new DateOnly(2026, 9, 10), new TimeOnly(9, 0), new TimeOnly(13, 0)),
        ];

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(bookingId, row.BookingId);
        Assert.True(row.IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 10), row.EventDate);
        Assert.Equal(new TimeOnly(9, 0), row.EventStartTime);
        Assert.Equal(new TimeOnly(13, 0), row.EventEndTime);
    }

    [Fact]
    public async Task CoordinatorGetsBothAnOriginalAndAnActiveRecoveryOnDifferentEvents()
    {
        var originalId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        _queries.Rows =
        [
            new AttendeeBookingSummary(
                originalId, true, new DateOnly(2026, 9, 10), new TimeOnly(9, 0), new TimeOnly(13, 0)),
            new AttendeeBookingSummary(
                recoveryId, false, new DateOnly(2026, 9, 12), new TimeOnly(13, 0), new TimeOnly(17, 0)),
        ];

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.True(result.Value[0].IsOriginal);
        Assert.False(result.Value[1].IsOriginal);
        Assert.Equal(new DateOnly(2026, 9, 12), result.Value[1].EventDate);
        Assert.Equal(new TimeOnly(17, 0), result.Value[1].EventEndTime);
    }

    [Fact]
    public async Task UnknownAttendeeIsNotFound()
    {
        _queries.Rows = null;

        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    [Fact]
    public async Task CallerWithoutManageAttendeesIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new GetAttendeeBookingsQuery(Manager, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _queries.QueryCount);
    }
}
