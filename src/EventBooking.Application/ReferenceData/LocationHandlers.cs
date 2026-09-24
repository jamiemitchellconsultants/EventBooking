using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Locations;
using EventBooking.Domain.Time;

namespace EventBooking.Application.ReferenceData;

/// <summary>Creates a location.</summary>
/// <param name="locations">The locations.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
/// <param name="zones">The zones.</param>
public sealed class CreateLocationHandler(
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IEventWindowZones zones)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<LocationResult>> HandleAsync(CreateLocationCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure) return Result<LocationResult>.Failure(authorized.Error);

        Location location;
        try
        {
            location = Location.Create(Guid.NewGuid(), command.Code, command.Name, command.Address, command.TimeZoneId, zones);
        }
        catch (DomainException ex)
        {
            return Result<LocationResult>.Failure(Error.Validation(ex.Message));
        }

        if (await locations.GetByCodeAsync(location.Code, ct) is not null)
            return Result<LocationResult>.Failure(Error.Conflict($"A location with code '{location.Code}' already exists."));

        locations.Add(location);
        audit.Record(AuditEntityTypes.Location, location.Id, AuditAction.LocationCreated,
            ActorType.Staff, command.StaffUserId.ToString(), $"code {location.Code}; zone {location.TimeZoneId}");
        await unitOfWork.SaveChangesAsync(ct);
        return Result<LocationResult>.Success(new LocationResult(
            location.Id, location.Code, location.Name, location.Address, location.TimeZoneId, location.IsActive, location.Version));
    }
}

/// <summary>Updates a location's name, address or time zone.</summary>
/// <param name="locations">The locations.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unitOfWork.</param>
/// <param name="audit">The audit.</param>
/// <param name="zones">The zones.</param>
/// <param name="blocking">The blocking.</param>
public sealed class UpdateLocationHandler(
    ILocationRepository locations,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IEventWindowZones zones,
    IReferenceDataBlockingQueries blocking)
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<LocationResult>> HandleAsync(UpdateLocationCommand command, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(command.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure) return Result<LocationResult>.Failure(authorized.Error);

        var location = await locations.GetAsync(command.LocationId, ct);
        if (location is null) return Result<LocationResult>.Failure(Error.NotFound("No such location."));
        if (location.Version != command.ExpectedVersion)
            return Result<LocationResult>.Failure(Error.VersionConflict("The location changed under you.", location.Version));

        var changes = new List<string>();
        try
        {
            // Activation runs first so that a refusal throws before the name, address or
            // zone is touched. The refusal covers the whole command: nothing is saved
            // when it throws, because the save is after the whole block, and the entity
            // is untouched for callers that hold the same reference. Activation is only
            // asked about when it changes: a rename of a site with live scheduling is
            // always allowed, and asking the blocking query on every update would turn
            // one into a refusal.
            if (command.IsActive != location.IsActive)
            {
                if (command.IsActive) location.Reactivate();
                else location.Deactivate(await blocking.LocationUsageAsync(location.Id, ct));
                changes.Add($"isActive -> {location.IsActive}");
            }

            if (command.Name is not null) { location.Rename(command.Name); changes.Add("name"); }
            if (command.Address is not null) { location.ChangeAddress(command.Address); changes.Add("address"); }
            if (command.TimeZoneId is not null && command.TimeZoneId != location.TimeZoneId)
            {
                var before = location.TimeZoneId;
                var usage = await blocking.LocationUsageAsync(location.Id, ct);
                location.ChangeTimeZone(command.TimeZoneId, zones, usage);
                if (location.TimeZoneId != before) changes.Add($"timeZone {before} -> {location.TimeZoneId}");
            }
        }
        catch (ReferenceDataInUseException ex)
        {
            return Result<LocationResult>.Failure(Error.ReferenceDataInUse(ex.Message, ex.Blocking));
        }
        catch (DomainException ex)
        {
            return Result<LocationResult>.Failure(Error.Validation(ex.Message));
        }

        if (changes.Count == 0)
            return Result<LocationResult>.Success(ToResult(location));

        audit.Record(AuditEntityTypes.Location, location.Id, AuditAction.LocationUpdated,
            ActorType.Staff, command.StaffUserId.ToString(), string.Join("; ", changes));
        await unitOfWork.SaveChangesAsync(ct);
        return Result<LocationResult>.Success(ToResult(location));
    }

    private static LocationResult ToResult(Location location) => new(
        location.Id, location.Code, location.Name, location.Address, location.TimeZoneId, location.IsActive, location.Version);
}

/// <summary>Lists locations in code order, hiding inactive rows unless asked.</summary>
/// <param name="locations">The locations.</param>
/// <param name="access">The access.</param>
public sealed class ListLocationsHandler(
    ILocationRepository locations,
    IStaffAccessAuthorizer access)
{
    /// <summary>Handles the query.</summary>
    /// <param name="query">Whether to include inactive rows.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task<Result<IReadOnlyList<LocationListItem>>> HandleAsync(
        ListLocationsQuery query, CancellationToken ct)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ManageReferenceData, null, ct);
        if (authorized.IsFailure)
            return Result<IReadOnlyList<LocationListItem>>.Failure(authorized.Error);

        var rows = await locations.ListAsync(ct);
        return Result<IReadOnlyList<LocationListItem>>.Success(
            rows.Where(l => query.IncludeInactive || l.IsActive)
                .OrderBy(l => l.Code, StringComparer.Ordinal)
                .Select(l => new LocationListItem(l.Id, l.Code, l.Name, l.IsActive))
                .ToList());
    }
}
