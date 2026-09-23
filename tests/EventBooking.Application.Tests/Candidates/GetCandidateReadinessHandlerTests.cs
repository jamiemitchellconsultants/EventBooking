using EventBooking.Application.Access;
using EventBooking.Application.Candidates;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Candidates;

/// <summary>Verifies readiness authorization, not-found handling, and coordinator success.</summary>
public sealed class GetCandidateReadinessHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");
    private static readonly Guid Admin = Guid.Parse("c0000008-0000-0000-0000-000000000008");

    private readonly InMemoryStaffAccessProfileRepository _roles = new();
    private readonly InMemoryQueries _queries = new();

    private GetCandidateReadinessHandler Handler => new(
        new StaffAccessAuthorizer(_roles), _queries, new CandidateReadinessCalculator());

    public GetCandidateReadinessHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _roles.Add(StaffAccessProfile.Create(Admin, Role.Admin, null));
    }

    /// <summary>An unknown candidate fails before any readiness is calculated.</summary>
    [Fact]
    public async Task UnknownCandidateIsNotFound()
    {
        _queries.Snapshot = null;

        var result = await Handler.HandleAsync(
            new GetCandidateReadinessQuery(Coordinator, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    /// <summary>A coordinator receives the calculated readiness.</summary>
    [Fact]
    public async Task CoordinatorReceivesCalculatedReadiness()
    {
        var candidateId = Guid.NewGuid();
        _queries.Snapshot = new CandidateReadinessSnapshot(
            candidateId,
            Guid.NewGuid(),
            [AppointmentTypeIds.MedicalCheckUp],
            null,
            []);

        var result = await Handler.HandleAsync(
            new GetCandidateReadinessQuery(Coordinator, candidateId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(candidateId, result.Value.CandidateId);
        Assert.Equal(CandidateReadinessCode.NoActiveBooking, result.Value.Code);
    }

    /// <summary>An admin is forbidden before the readiness query runs.</summary>
    [Fact]
    public async Task AdminRunsNoReadinessQuery()
    {
        var result = await Handler.HandleAsync(
            new GetCandidateReadinessQuery(Admin, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
        Assert.Equal(0, _queries.QueryCount);
    }
}
