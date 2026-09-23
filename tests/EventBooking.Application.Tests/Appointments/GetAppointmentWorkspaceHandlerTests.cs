using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies workspace reads authorize before using minimum-data query ports.</summary>
public sealed class GetAppointmentWorkspaceHandlerTests
{
    /// <summary>Verifies both scoped roles pass the profile's trusted type to the query.</summary>
    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.AppointmentStaff)]
    public async Task ScopedRoleUsesItsTrustedAppointmentType(Role role)
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [role], AppointmentTypeIds.MedicalCheckUp));
        var queries = new RecordingQueries();
        var handler = new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock());

        var result = await handler.ListSlotsAsync(staff, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, queries.ListCallCount);
        Assert.Equal(AppointmentTypeIds.MedicalCheckUp, queries.LastAppointmentTypeId);
    }

    /// <summary>Verifies a combined Coordinator profile receives its scoped-role capability.</summary>
    [Fact]
    public async Task CombinedCoordinatorAppointmentStaffCanReadTheWorkspace()
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff,
            [Role.Coordinator, Role.AppointmentStaff],
            AppointmentTypeIds.UniformFitting));
        var queries = new RecordingQueries();

        var result = await new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock())
            .GetSlotAsync(staff, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppointmentTypeIds.UniformFitting, queries.LastAppointmentTypeId);
    }

    /// <summary>Verifies Admin denial occurs before the candidate-data query is invoked.</summary>
    [Fact]
    public async Task AdminIsDeniedBeforeAnyWorkspaceQuery()
    {
        var admin = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(admin, [Role.Admin], null));
        var queries = new RecordingQueries();

        var result = await new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock())
            .ListSlotsAsync(admin, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, queries.ListCallCount);
        Assert.Equal(0, queries.DetailCallCount);
    }

    /// <summary>Verifies absent and cross-scope slots share the same not-found application result.</summary>
    [Fact]
    public async Task QueryNullBecomesTheStableNotFoundResult()
    {
        var staff = Guid.NewGuid();
        var profiles = new InMemoryStaffAccessProfileRepository();
        profiles.Add(StaffAccessProfile.Create(
            staff, [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting));
        var queries = new RecordingQueries { ReturnDetail = null };

        var result = await new GetAppointmentWorkspaceHandler(
            new StaffAccessAuthorizer(profiles), queries, new FakeClock())
            .GetSlotAsync(staff, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
        Assert.Equal("No such appointment workspace slot.", result.Error.Message);
    }

    private sealed class RecordingQueries : IAppointmentWorkspaceQueries
    {
        public int ListCallCount { get; private set; }
        public int DetailCallCount { get; private set; }
        public Guid? LastAppointmentTypeId { get; private set; }
        public AppointmentSlotDetail? ReturnDetail { get; set; } = new()
        {
            AppointmentTypeName = "Drug & Alcohol Testing",
            ConfirmedSlotId = Guid.NewGuid(),
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        };

        public Task<AppointmentWorkspaceSlotList> ListSlotsAsync(
            Guid appointmentTypeId,
            DateOnly onOrAfter,
            CancellationToken cancellationToken)
        {
            ListCallCount++;
            LastAppointmentTypeId = appointmentTypeId;
            return Task.FromResult(new AppointmentWorkspaceSlotList
            {
                AppointmentTypeName = "Medical Check-up",
                Slots = [],
            });
        }

        public Task<AppointmentSlotDetail?> GetSlotAsync(
            Guid appointmentTypeId,
            Guid confirmedSlotId,
            CancellationToken cancellationToken)
        {
            DetailCallCount++;
            LastAppointmentTypeId = appointmentTypeId;
            return Task.FromResult(ReturnDetail);
        }
    }
}
