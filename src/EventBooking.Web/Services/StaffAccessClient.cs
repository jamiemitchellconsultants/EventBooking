using System.Net.Http.Json;

namespace EventBooking.Web.Services;

/// <summary>Describes a staff access profile for administration. Roles are read-only here.</summary>
public sealed record StaffAccessProfileDto(
    Guid StaffUserId,
    IReadOnlyList<string> Roles,
    Guid? AppointmentTypeId,
    string? AppointmentTypeName,
    long Version,
    string? StaffId = null,
    /// <summary>
    /// The name mirrored from the identity provider, or null when the identity carries none.
    /// </summary>
    string? DisplayName = null);

/// <summary>Describes a scope mutation and any displaced manager.</summary>
public sealed record StaffAccessMutationDto(
    StaffAccessProfileDto Profile,
    Guid? FormerManagerStaffUserId);

/// <summary>Calls the staff-access administration API for appointment-type scope only.</summary>
public sealed class StaffAccessClient(HttpClient http)
{
    /// <summary>Lists every current staff access profile.</summary>
    public async Task<ApiOutcome<IReadOnlyList<StaffAccessProfileDto>>> ListAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/admin/staff-access", cancellationToken);
        return await ApiCall.ReadAsync<IReadOnlyList<StaffAccessProfileDto>>(
            response, cancellationToken);
    }

    /// <summary>Replaces an existing profile's appointment-type scope.</summary>
    public async Task<ApiOutcome<StaffAccessMutationDto>> ReplaceScopeAsync(
        Guid staffUserId,
        Guid? appointmentTypeId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/admin/staff-access/{staffUserId}",
            new { AppointmentTypeId = appointmentTypeId, ExpectedVersion = expectedVersion },
            cancellationToken);
        return await ApiCall.ReadAsync<StaffAccessMutationDto>(response, cancellationToken);
    }

    /// <summary>Clears an existing profile's appointment-type scope.</summary>
    public async Task<ApiOutcome<bool>> ClearScopeAsync(
        Guid staffUserId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/admin/staff-access/{staffUserId}?expectedVersion={expectedVersion}",
            cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
