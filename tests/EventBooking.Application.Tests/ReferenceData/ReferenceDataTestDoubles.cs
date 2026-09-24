using EventBooking.Application.ReferenceData;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.Tests.ReferenceData;

public sealed class TestZones : IEventWindowZones
{
    public static readonly TestZones Instance = new();
    public bool IsKnownZone(string timeZoneId) =>
        timeZoneId is "Europe/London" or "Asia/Tokyo";
    public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        throw new NotImplementedException();
    public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        throw new NotImplementedException();
    public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
        throw new NotImplementedException();
    public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) =>
        throw new NotImplementedException();
}

public sealed class MemoryBlocking : IReferenceDataBlockingQueries
{
    public LocationUsage LocationUsageValue { get; set; } = LocationUsage.None;
    public AppointmentTypeUsage TypeUsageValue { get; set; } = AppointmentTypeUsage.None;
    public int MemberCount { get; set; }
    public int BlockingMembers { get; set; }

    public Task<LocationUsage> LocationUsageAsync(Guid locationId, CancellationToken ct) =>
        Task.FromResult(LocationUsageValue);
    public Task<AppointmentTypeUsage> AppointmentTypeUsageAsync(Guid typeId, CancellationToken ct) =>
        Task.FromResult(TypeUsageValue);
    public Task<int> AttendeeGroupMemberCountAsync(Guid groupId, CancellationToken ct) =>
        Task.FromResult(MemberCount);
    public Task<int> AttendeeGroupBlockingMemberCountAsync(Guid groupId, CancellationToken ct) =>
        Task.FromResult(BlockingMembers);
}
