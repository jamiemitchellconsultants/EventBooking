using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies readiness authorization, not-found handling, and coordinator success.</summary>
public sealed class GetAttendeeReadinessHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly InMemoryQueries _queries = new();

    private GetAttendeeReadinessHandler Handler => new(
        new StaffAccessAuthorizer(_roles), _queries, new AttendeeReadinessCalculator());

    private static IReadOnlyDictionary<Guid, AttendeeReadinessType> Map(params Guid[] ids) =>
        ids.ToDictionary(
            id => id,
            id => new AttendeeReadinessType(
                id, AppointmentTypeIds.CodeOf(id), AppointmentTypeIds.NameOf(id)));

    public GetAttendeeReadinessHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
    }

    /// <summary>An unknown attendee fails before any readiness is calculated.</summary>
    [Fact]
    public async Task UnknownAttendeeIsNotFound()
    {
        _queries.Snapshot = null;

        var result = await Handler.HandleAsync(
            new GetAttendeeReadinessQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    /// <summary>A coordinator receives the calculated readiness.</summary>
    [Fact]
    public async Task CoordinatorReceivesCalculatedReadiness()
    {
        var attendeeId = Guid.NewGuid();
        _queries.Snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [AppointmentTypeIds.MedicalCheckUp],
            null,
            [],
            Map(AppointmentTypeIds.MedicalCheckUp));

        var result = await Handler.HandleAsync(
            new GetAttendeeReadinessQuery(Coordinator, attendeeId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(attendeeId, result.Value.AttendeeId);
        Assert.Equal(AttendeeReadinessCode.NoActiveBooking, result.Value.Code);
    }

    /// <summary>An admin is forbidden before the readiness query runs.</summary>
    [Fact]
    public async Task AdminRunsNoReadinessQuery()
    {
        var result = await Handler.HandleAsync(
            new GetAttendeeReadinessQuery(Admin, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _queries.QueryCount);
    }
}
