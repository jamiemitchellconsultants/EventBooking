# 00b — Vocabulary edits 64 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — src/EventBooking.Web/Services/DashboardsClient.cs — 1/1

<!-- vocabulary-file: {"id":210,"oldPath":"src/EventBooking.Web/Services/DashboardsClient.cs","newPath":"src/EventBooking.Web/Services/DashboardsClient.cs","beforeSha":"5dd478529d42d9ef584a296524229aa4ce2524d3e31710fbee127b17c50486c8","afterSha":"93784643d93f190e15300099506719e2f065847e8c520aeb566ea4ac46a79758","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

public sealed record AwaitingRowDto(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly WaitingSince,
    int DaysWaiting);

public sealed record NoResponseRowDto(
    Guid AttendeeId,
    string Name,
    string Email,
    IReadOnlyList<string> RequiredCodes,
    DateOnly GaveUpOn);

public sealed record EventCapacityRowDto(string Code, int TotalHeadcount, int RemainingCapacity);

public sealed record EventRowDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<EventCapacityRowDto> Capacities,
    int ActiveBookings);

/// <summary>The latest attendee delivery status projected for staff pages.</summary>
/// <param name="AttendeeId">The attendee whose delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether current server-side state permits a retry.</param>
public sealed record AttendeeEmailStatusDto(
    Guid AttendeeId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

public sealed record DashboardsDto(
    IReadOnlyList<AwaitingRowDto> AwaitingAvailability,
    IReadOnlyList<NoResponseRowDto> NoResponse,
    IReadOnlyList<EventRowDto> Events,
    IReadOnlyList<AttendeeEmailStatusDto> EmailStatuses);

public sealed class DashboardsClient(HttpClient http)
{
    public async Task<ApiOutcome<DashboardsDto>> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/dashboards", cancellationToken);
        return await ApiCall.ReadAsync<DashboardsDto>(response, cancellationToken);
    }
}
`````

## before — src/EventBooking.Web/Services/HeadOfficePageClock.cs — 1/1

<!-- vocabulary-file: {"id":211,"oldPath":"src/EventBooking.Web/Services/HeadOfficePageClock.cs","newPath":"src/EventBooking.Web/Services/TransitionalLocationPageClock.cs","beforeSha":"930b85e1388102e6056813b0258e67ccfbe4b7c15fefd67edaa068aae5556921","afterSha":"87a3ebf1a2974fefb240cce9b59cb9944b004206d13e53ce10add659be818258","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Supplies browser action gating in the configured head-office time zone.</summary>
public sealed class HeadOfficePageClock
{
    private readonly TimeZoneInfo _zone;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a clock for one IANA or operating-system time-zone identifier.</summary>
    /// <param name="timeZoneId">The configured head-office time-zone identifier.</param>
    /// <param name="utcNow">An optional UTC source used by deterministic tests.</param>
    public HeadOfficePageClock(string timeZoneId, Func<DateTimeOffset>? utcNow = null)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Gets the current instant represented in the head-office time zone.</summary>
    public DateTimeOffset NowAtHeadOffice => TimeZoneInfo.ConvertTime(_utcNow(), _zone);
}
`````

## after — src/EventBooking.Web/Services/TransitionalLocationPageClock.cs — 1/1

<!-- vocabulary-file: {"id":211,"oldPath":"src/EventBooking.Web/Services/HeadOfficePageClock.cs","newPath":"src/EventBooking.Web/Services/TransitionalLocationPageClock.cs","beforeSha":"930b85e1388102e6056813b0258e67ccfbe4b7c15fefd67edaa068aae5556921","afterSha":"87a3ebf1a2974fefb240cce9b59cb9944b004206d13e53ce10add659be818258","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Supplies browser action gating in the configured transitional-location time zone.</summary>
public sealed class TransitionalLocationPageClock
{
    private readonly TimeZoneInfo _zone;
    private readonly Func<DateTimeOffset> _utcNow;

    /// <summary>Creates a clock for one IANA or operating-system time-zone identifier.</summary>
    /// <param name="timeZoneId">The configured transitional-location time-zone identifier.</param>
    /// <param name="utcNow">An optional UTC source used by deterministic tests.</param>
    public TransitionalLocationPageClock(string timeZoneId, Func<DateTimeOffset>? utcNow = null)
    {
        _zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Gets the current instant represented in the transitional-location time zone.</summary>
    public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(_utcNow(), _zone);
}
`````

## before — src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs — 1/1

<!-- vocabulary-file: {"id":212,"oldPath":"src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs","newPath":"src/EventBooking.Web/Services/TransitionalLocationTimePresentation.cs","beforeSha":"80fc84f3b64e7c0dc1fbcf7d4a58e10b4033b40cdb2caa9ac868ccc58b6b264f","afterSha":"a1764fd89e7a18abe9c1ee5a0d64f08d6d0257e82b86eba1d01ae7622b652389","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>
/// Formats UTC audit and delivery instants in the configured head-office local time zone.
/// </summary>
public sealed class HeadOfficeTimePresentation
{
    private readonly TimeZoneInfo _timeZone;
    private readonly string _timeZoneId;

    /// <summary>
    /// Creates presentation formatting for the configured IANA or operating-system time-zone identifier.
    /// </summary>
    /// <param name="timeZoneId">The configured head-office time-zone identifier.</param>
    public HeadOfficeTimePresentation(string timeZoneId)
    {
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _timeZoneId = timeZoneId;
    }

    /// <summary>
    /// Converts a stored instant to head-office local time and includes its offset and configured zone identifier.
    /// </summary>
    /// <param name="timestamp">The stored instant to present without changing its point in time.</param>
    /// <returns>A local date and time with an unambiguous offset and zone identifier.</returns>
    public string Format(DateTimeOffset timestamp) =>
        $"{TimeZoneInfo.ConvertTime(timestamp, _timeZone):yyyy-MM-dd HH:mm zzz} ({_timeZoneId})";
}
`````

## after — src/EventBooking.Web/Services/TransitionalLocationTimePresentation.cs — 1/1

<!-- vocabulary-file: {"id":212,"oldPath":"src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs","newPath":"src/EventBooking.Web/Services/TransitionalLocationTimePresentation.cs","beforeSha":"80fc84f3b64e7c0dc1fbcf7d4a58e10b4033b40cdb2caa9ac868ccc58b6b264f","afterSha":"a1764fd89e7a18abe9c1ee5a0d64f08d6d0257e82b86eba1d01ae7622b652389","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>
/// Formats UTC audit and delivery instants in the configured transitional-location local time zone.
/// </summary>
public sealed class TransitionalLocationTimePresentation
{
    private readonly TimeZoneInfo _timeZone;
    private readonly string _timeZoneId;

    /// <summary>
    /// Creates presentation formatting for the configured IANA or operating-system time-zone identifier.
    /// </summary>
    /// <param name="timeZoneId">The configured transitional-location time-zone identifier.</param>
    public TransitionalLocationTimePresentation(string timeZoneId)
    {
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        _timeZoneId = timeZoneId;
    }

    /// <summary>
    /// Converts a stored instant to transitional-location local time and includes its offset and configured zone identifier.
    /// </summary>
    /// <param name="timestamp">The stored instant to present without changing its point in time.</param>
    /// <returns>A local date and time with an unambiguous offset and zone identifier.</returns>
    public string Format(DateTimeOffset timestamp) =>
        $"{TimeZoneInfo.ConvertTime(timestamp, _timeZone):yyyy-MM-dd HH:mm zzz} ({_timeZoneId})";
}
`````

## before — src/EventBooking.Web/Services/SlotsClient.cs — 1/1

<!-- vocabulary-file: {"id":213,"oldPath":"src/EventBooking.Web/Services/SlotsClient.cs","newPath":"src/EventBooking.Web/Services/EventsClient.cs","beforeSha":"23af9a3e3b9ed2e83834b5eb9c50daf2a2b7039aaf86492d1ac7f44f17f10b5b","afterSha":"e480141f315d52c2131eb3b66476b7f7a3e7e718840cbfd256a947b558acd18c","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record OpenProposalDto(
    Guid ProposalId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

public sealed record ConfirmedSlotDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

public sealed record AdjustConfirmedCapacityDto(
    Guid ConfirmedSlotId,
    int TotalHeadcount,
    int RemainingCapacity);

public sealed record SlotBoardDto(
    IReadOnlyList<OpenProposalDto> OpenProposals,
    IReadOnlyList<ConfirmedSlotDto> ConfirmedSlots);

/// <summary>Remaining and accepted places for one appointment type within a confirmed slot.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="TotalHeadcount">The headcount the manager accepted for this appointment type.</param>
/// <param name="RemainingCapacity">The places still free for this appointment type.</param>
public sealed record SlotOperationCapacityDto(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>One confirmed slot in the slot-only operations view; carries no candidate data.</summary>
/// <param name="ConfirmedSlotId">The confirmed slot the row describes.</param>
/// <param name="Date">The date of the confirmed window.</param>
/// <param name="StartTime">The start of the confirmed window.</param>
/// <param name="EndTime">The end of the confirmed window.</param>
/// <param name="Capacities">Per-appointment-type capacity for this window.</param>
/// <param name="ActiveBookings">How many active bookings the window currently holds.</param>
public sealed record SlotOperationDto(
    Guid ConfirmedSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<SlotOperationCapacityDto> Capacities,
    int ActiveBookings);

/// <summary>The slot-only operations view returned to administrators and coordinators.</summary>
/// <param name="Slots">Every active confirmed slot, newest data as the server returned it.</param>
public sealed record SlotOperationsDto(IReadOnlyList<SlotOperationDto> Slots);

public sealed class SlotsClient(HttpClient http)
{
    public async Task<ApiOutcome<SlotBoardDto>> GetBoardAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/slots/board", cancellationToken);
        return await ApiCall.ReadAsync<SlotBoardDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> ProposeAsync(
        DateOnly date,
        TimeOnly startTime,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/slots/proposals",
            new { Date = date, StartTime = startTime },
            cancellationToken);

        return await ApiCall.ReadAsync<Guid>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> AcceptAsync(
        Guid proposalId,
        int headcount,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/slots/proposals/{proposalId}/acceptance",
            new { Headcount = headcount },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawAcceptanceAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/proposals/{proposalId}/acceptance",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/proposals/{proposalId}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<AdjustConfirmedCapacityDto>> AdjustCapacityAsync(
        Guid confirmedSlotId,
        int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/slots/confirmed/{confirmedSlotId}/capacity",
            new { TotalHeadcount = totalHeadcount },
            cancellationToken);

        return await ApiCall.ReadAsync<AdjustConfirmedCapacityDto>(
            response,
            cancellationToken);
    }

    /// <summary>Loads the slot-only operations view; never touches candidate data.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every active confirmed slot, or the failure the API reported.</returns>
    public async Task<ApiOutcome<SlotOperationsDto>> GetSlotOperationsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/slots/operations", cancellationToken);
        return await ApiCall.ReadAsync<SlotOperationsDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> CancelConfirmedSlotAsync(
        Guid slotId,
        bool confirm,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/slots/confirmed/{slotId}?confirm={(confirm ? "true" : "false")}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
`````

## after — src/EventBooking.Web/Services/EventsClient.cs — 1/1

<!-- vocabulary-file: {"id":213,"oldPath":"src/EventBooking.Web/Services/SlotsClient.cs","newPath":"src/EventBooking.Web/Services/EventsClient.cs","beforeSha":"23af9a3e3b9ed2e83834b5eb9c50daf2a2b7039aaf86492d1ac7f44f17f10b5b","afterSha":"e480141f315d52c2131eb3b66476b7f7a3e7e718840cbfd256a947b558acd18c","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;

namespace EventBooking.Web.Services;

public sealed record OpenProposalDto(
    Guid ProposalId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<string> AcceptedByAppointmentTypeNames,
    int? MyAcceptedHeadcount,
    bool AcceptedByMe,
    bool CreatedByMe);

public sealed record EventDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int MyHeadcount,
    int MyRemainingCapacity);

public sealed record AdjustConfirmedCapacityDto(
    Guid EventId,
    int TotalHeadcount,
    int RemainingCapacity);

public sealed record EventBoardDto(
    IReadOnlyList<OpenProposalDto> OpenProposals,
    IReadOnlyList<EventDto> Events);

/// <summary>Remaining and accepted places for one appointment type within a eventItem.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="TotalHeadcount">The headcount the manager accepted for this appointment type.</param>
/// <param name="RemainingCapacity">The places still free for this appointment type.</param>
public sealed record EventOperationCapacityDto(string Code, int TotalHeadcount, int RemainingCapacity);

/// <summary>One eventItem in the event-only operations view; carries no attendee data.</summary>
/// <param name="EventId">The event the row describes.</param>
/// <param name="Date">The date of the confirmed window.</param>
/// <param name="StartTime">The start of the confirmed window.</param>
/// <param name="EndTime">The end of the confirmed window.</param>
/// <param name="Capacities">Per-appointment-type capacity for this window.</param>
/// <param name="ActiveBookings">How many active bookings the window currently holds.</param>
public sealed record EventOperationDto(
    Guid EventId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    IReadOnlyList<EventOperationCapacityDto> Capacities,
    int ActiveBookings);

/// <summary>The event-only operations view returned to administrators and coordinators.</summary>
/// <param name="Events">Every active eventItem, newest data as the server returned it.</param>
public sealed record EventOperationsDto(IReadOnlyList<EventOperationDto> Events);

public sealed class EventsClient(HttpClient http)
{
    public async Task<ApiOutcome<EventBoardDto>> GetBoardAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/events/board", cancellationToken);
        return await ApiCall.ReadAsync<EventBoardDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> ProposeAsync(
        DateOnly date,
        TimeOnly startTime,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/event-proposals",
            new { Date = date, StartTime = startTime },
            cancellationToken);

        return await ApiCall.ReadAsync<Guid>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> AcceptAsync(
        Guid proposalId,
        int headcount,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            new { Headcount = headcount },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawAcceptanceAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/event-proposals/{proposalId}/acceptance",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> WithdrawProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/event-proposals/{proposalId}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<AdjustConfirmedCapacityDto>> AdjustCapacityAsync(
        Guid eventId,
        int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/events/{eventId}/capacity",
            new { TotalHeadcount = totalHeadcount },
            cancellationToken);

        return await ApiCall.ReadAsync<AdjustConfirmedCapacityDto>(
            response,
            cancellationToken);
    }

    /// <summary>Loads the event-only operations view; never touches attendee data.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every active eventItem, or the failure the API reported.</returns>
    public async Task<ApiOutcome<EventOperationsDto>> GetEventOperationsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/events/operations", cancellationToken);
        return await ApiCall.ReadAsync<EventOperationsDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> CancelEventAsync(
        Guid eventId,
        bool confirm,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/events/{eventId}?confirm={(confirm ? "true" : "false")}",
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }
}
`````

## before — src/EventBooking.Web/Services/StaffNavigation.cs — 1/1

<!-- vocabulary-file: {"id":214,"oldPath":"src/EventBooking.Web/Services/StaffNavigation.cs","newPath":"src/EventBooking.Web/Services/StaffNavigation.cs","beforeSha":"1ca7d0e8ef863fb8dd144c5c3caaa8269202f5c6a01b30baa2c4f1433ba1279d","afterSha":"209160387fc9ae8e479407926e51a903f380b1aea0e8a48acef160e112280cb8","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Describes one staff navigation destination shown for a role combination.</summary>
/// <param name="Href">The link target shown for the permitted role combination.</param>
/// <param name="Label">The visible link text naming the permitted workspace.</param>
/// <param name="Description">The accessible description of what the workspace offers.</param>
public sealed record StaffLink(string Href, string Label, string Description);

/// <summary>Builds the deterministic navigation union permitted by staff roles.</summary>
public static class StaffNavigation
{
    /// <summary>Builds the deterministic union of links permitted by the caller's roles.</summary>
    /// <param name="me">The authenticated staff identity with its assigned roles.</param>
    /// <returns>The ordered links the caller is permitted to open.</returns>
    public static IReadOnlyList<StaffLink> LinksFor(MeDto me)
    {
        var roles = me.Roles.ToHashSet(StringComparer.Ordinal);
        if (roles.Contains("Admin"))
        {
            return
            [
                new("/settings", "System settings", "Configure invitation timing"),
                new("/staff-access", "Staff access", "Set appointment-type scope"),
                new("/confirmed-slots", "Confirmed slots", "Import already agreed slots"),
                new("/audit", "Audit trail", "Search what changed"),
            ];
        }

        var links = new List<StaffLink>();
        if (roles.Contains("Manager"))
        {
            links.Add(new("/slots", "Slot proposals", "Negotiate and confirm shared windows"));
        }

        if (roles.Contains("Manager") || roles.Contains("AppointmentStaff"))
        {
            links.Add(new(
                "/appointments",
                "Appointments",
                "Check candidates in and record appointment outcomes"));
        }

        if (roles.Contains("Coordinator"))
        {
            links.Add(new("/candidates", "Candidates", "Invite and track candidates"));
            links.Add(new("/dashboards", "Dashboards", "Waiting lists and follow-ups"));
            links.Add(new("/confirmed-slots", "Confirmed slots", "Import already agreed slots"));
            links.Add(new("/audit", "Audit trail", "Search what changed"));
        }

        return links;
    }
}
`````

## after — src/EventBooking.Web/Services/StaffNavigation.cs — 1/1

<!-- vocabulary-file: {"id":214,"oldPath":"src/EventBooking.Web/Services/StaffNavigation.cs","newPath":"src/EventBooking.Web/Services/StaffNavigation.cs","beforeSha":"1ca7d0e8ef863fb8dd144c5c3caaa8269202f5c6a01b30baa2c4f1433ba1279d","afterSha":"209160387fc9ae8e479407926e51a903f380b1aea0e8a48acef160e112280cb8","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Web.Services;

/// <summary>Describes one staff navigation destination shown for a role combination.</summary>
/// <param name="Href">The link target shown for the permitted role combination.</param>
/// <param name="Label">The visible link text naming the permitted workspace.</param>
/// <param name="Description">The accessible description of what the workspace offers.</param>
public sealed record StaffLink(string Href, string Label, string Description);

/// <summary>Builds the deterministic navigation union permitted by staff roles.</summary>
public static class StaffNavigation
{
    /// <summary>Builds the deterministic union of links permitted by the caller's roles.</summary>
    /// <param name="me">The authenticated staff identity with its assigned roles.</param>
    /// <returns>The ordered links the caller is permitted to open.</returns>
    public static IReadOnlyList<StaffLink> LinksFor(MeDto me)
    {
        var roles = me.Roles.ToHashSet(StringComparer.Ordinal);
        if (roles.Contains("Admin"))
        {
            return
            [
                new("/settings", "System settings", "Configure invitation timing"),
                new("/staff-access", "Staff access", "Set appointment-type scope"),
                new("/events/operations", "Events", "Import already agreed events"),
                new("/audit", "Audit trail", "Search what changed"),
            ];
        }

        var links = new List<StaffLink>();
        if (roles.Contains("Manager"))
        {
            links.Add(new("/events/negotiate", "Event proposals", "Negotiate and confirm shared windows"));
        }

        if (roles.Contains("Manager") || roles.Contains("AppointmentStaff"))
        {
            links.Add(new(
                "/appointments",
                "Appointments",
                "Check attendees in and record appointment outcomes"));
        }

        if (roles.Contains("Coordinator"))
        {
            links.Add(new("/attendees", "Attendees", "Invite and track attendees"));
            links.Add(new("/dashboards", "Dashboards", "Waiting lists and follow-ups"));
            links.Add(new("/events/operations", "Events", "Import already agreed events"));
            links.Add(new("/audit", "Audit trail", "Search what changed"));
        }

        return links;
    }
}
`````

## before — src/EventBooking.Web/Services/UserGuideCatalog.cs — 1/1

<!-- vocabulary-file: {"id":215,"oldPath":"src/EventBooking.Web/Services/UserGuideCatalog.cs","newPath":"src/EventBooking.Web/Services/UserGuideCatalog.cs","beforeSha":"4d9370cba912f4abe828aab76593606b0b148e66e3ece1efb18b69fdefafffce","afterSha":"c46da55b05d1de5f55d5b686c0bf8a6f7ffb683aae21da39536818ac9e17fc15","side":"before","part":1,"parts":1} -->

`````csharp
using System.Reflection;

namespace EventBooking.Web.Services;

/// <summary>One role's rendered guide, ready to drop into the Help page.</summary>
/// <param name="RoleKey">The role this guide covers, or "Candidate" for the anonymous guide.</param>
/// <param name="AnchorId">The in-page section id other guides' cross-links target.</param>
/// <param name="Title">The guide's display heading.</param>
/// <param name="Html">The guide's markdown, already rendered to HTML.</param>
public sealed record UserGuide(string RoleKey, string AnchorId, string Title, string Html);

/// <summary>
/// Serves the docs/user-guides markdown files bundled into the assembly as embedded resources,
/// rendered to HTML for the in-app Help page (Pages/Help.razor). Bundling keeps the guides working
/// offline and versioned with the code that matches them, at the cost of needing a rebuild to
/// publish a wording change.
/// </summary>
public static class UserGuideCatalog
{
    private const string ResourcePrefix = "UserGuides/";

    // Ordered to match the workspace-link union StaffNavigation.LinksFor builds for a combined
    // profile, so a coordinator-manager sees Manager guidance before Coordinator guidance.
    private static readonly (string RoleKey, string ResourceName, string Title)[] RoleGuides =
    [
        ("Manager", "manager-guide.md", "Manager guide"),
        ("AppointmentStaff", "appointment-staff-guide.md", "Appointment staff guide"),
        ("Coordinator", "coordinator-guide.md", "Coordinator guide"),
    ];

    private static readonly (string RoleKey, string ResourceName, string Title) AdminGuide =
        ("Admin", "admin-guide.md", "Admin guide");

    private static readonly (string RoleKey, string ResourceName, string Title) CandidateGuideEntry =
        ("Candidate", "candidate-guide.md", "Candidate guide");

    // Every signed-in staff member can read every guide, including the candidate guide — a
    // coordinator fielding a candidate's question, or a manager covering another type, needs the
    // same reference the other roles see, not just their own. Only a signed-out visitor is
    // restricted to the candidate guide alone (the <NotAuthorized> branch in Help.razor).
    private static readonly (string RoleKey, string ResourceName, string Title)[] AllGuides =
        [AdminGuide, .. RoleGuides, CandidateGuideEntry];

    // GuidesFor unlocks the full catalog for anyone holding at least one of these — an account
    // whose only role isn't one EventBooking recognises still gets no guide, matching Home.razor's
    // "not assigned a role yet" message rather than dumping the whole catalog on a role typo.
    private static readonly HashSet<string> RecognisedStaffRoles = new(
        RoleGuides.Select(guide => guide.RoleKey).Append(AdminGuide.RoleKey),
        StringComparer.Ordinal);

    // The screenshots the guides embed as "screenshots/foo.png" (relative to docs/user-guides/) are
    // published to wwwroot/help-assets/screenshots by the .csproj. Blazor's <base href="/"> resolves
    // relative URLs against "/" regardless of the current route, not against "/help", so the bare
    // relative path 404s (rendering as a broken-image placeholder) unless rewritten to an absolute
    // one — and that path deliberately isn't under /help/, which would collide with the client-side
    // /help route (see the .csproj comment on the Content item for the failure mode).
    private const string ScreenshotMarkdownPrefix = "(screenshots/";
    private const string ScreenshotUrlPrefix = "(/help-assets/screenshots/";

    // Every guide opens with this exact line pointing back to the standalone docs/user-guides
    // index — useful when the file is read on its own (e.g. on GitHub), but redundant on the Help
    // page now that every guide sits on the one page under its own jump-link in the index above
    // them (see Help.razor). Dropped along with the blank line that follows it, so removing it
    // doesn't leave a gap between the heading and the guide's first paragraph.
    private const string BackToAllGuidesLine = "[← All user guides](README.md)\n\n";

    // manager-guide.md links on to "appointment-staff-guide.md". Rendered guides sit inline on one
    // page rather than as browsable files, so that bare filename is rewritten to an in-page anchor
    // to avoid producing a dead link.
    private static readonly Dictionary<string, string> AnchorsByResourceName = RoleGuides
        .Append(AdminGuide)
        .Append(CandidateGuideEntry)
        .ToDictionary(guide => guide.ResourceName, guide => guide.RoleKey.ToLowerInvariant());

    private static readonly Dictionary<string, string> RenderedHtmlByResourceName = new(StringComparer.Ordinal);
    private static readonly Lock RenderLock = new();

    /// <summary>
    /// Builds every guide for a caller holding at least one recognised staff role — the full
    /// catalog, not just the guides matching their own roles, since staff routinely need to
    /// understand what other roles (and candidates) see. A caller with no recognised role gets
    /// none, so Help.razor can show its "not assigned a role yet" message.
    /// </summary>
    public static IReadOnlyList<UserGuide> GuidesFor(IReadOnlyList<string> roles) =>
        roles.Any(RecognisedStaffRoles.Contains) ? AllGuides.Select(BuildGuide).ToList() : [];

    /// <summary>Builds the candidate guide shown to signed-out visitors of the Help page.</summary>
    public static UserGuide CandidateGuide() => BuildGuide(CandidateGuideEntry);

    private static UserGuide BuildGuide((string RoleKey, string ResourceName, string Title) guide) =>
        new(guide.RoleKey, AnchorsByResourceName[guide.ResourceName], guide.Title, RenderedHtmlFor(guide.ResourceName));

    private static string RenderedHtmlFor(string resourceName)
    {
        lock (RenderLock)
        {
            if (RenderedHtmlByResourceName.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var markdown = RewriteCrossGuideLinks(ReadEmbeddedMarkdown(resourceName));
            var html = Markdig.Markdown.ToHtml(markdown);
            RenderedHtmlByResourceName[resourceName] = html;
            return html;
        }
    }

    private static string ReadEmbeddedMarkdown(string resourceName)
    {
        var assembly = typeof(UserGuideCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded user guide '{resourceName}' is missing. Check the EmbeddedResource glob in EventBooking.Web.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string RewriteCrossGuideLinks(string markdown)
    {
        var rewritten = markdown
            .Replace(BackToAllGuidesLine, string.Empty, StringComparison.Ordinal)
            .Replace(ScreenshotMarkdownPrefix, ScreenshotUrlPrefix, StringComparison.Ordinal);
        foreach (var (resourceName, anchor) in AnchorsByResourceName)
        {
            // Blazor's <base href="/"> resolves a bare "#anchor" against "/", not against the
            // current route — the same failure mode as the screenshot paths above — so a fragment
            // link needs the /help prefix to land back on this page instead of Home.
            rewritten = rewritten.Replace($"({resourceName})", $"(/help#{anchor})", StringComparison.Ordinal);
        }

        return rewritten;
    }
}
`````

## after — src/EventBooking.Web/Services/UserGuideCatalog.cs — 1/1

<!-- vocabulary-file: {"id":215,"oldPath":"src/EventBooking.Web/Services/UserGuideCatalog.cs","newPath":"src/EventBooking.Web/Services/UserGuideCatalog.cs","beforeSha":"4d9370cba912f4abe828aab76593606b0b148e66e3ece1efb18b69fdefafffce","afterSha":"c46da55b05d1de5f55d5b686c0bf8a6f7ffb683aae21da39536818ac9e17fc15","side":"after","part":1,"parts":1} -->

`````csharp
using System.Reflection;

namespace EventBooking.Web.Services;

/// <summary>One role's rendered guide, ready to drop into the Help page.</summary>
/// <param name="RoleKey">The role this guide covers, or "Attendee" for the anonymous guide.</param>
/// <param name="AnchorId">The in-page section id other guides' cross-links target.</param>
/// <param name="Title">The guide's display heading.</param>
/// <param name="Html">The guide's markdown, already rendered to HTML.</param>
public sealed record UserGuide(string RoleKey, string AnchorId, string Title, string Html);

/// <summary>
/// Serves the docs/user-guides markdown files bundled into the assembly as embedded resources,
/// rendered to HTML for the in-app Help page (Pages/Help.razor). Bundling keeps the guides working
/// offline and versioned with the code that matches them, at the cost of needing a rebuild to
/// publish a wording change.
/// </summary>
public static class UserGuideCatalog
{
    private const string ResourcePrefix = "UserGuides/";

    // Ordered to match the workspace-link union StaffNavigation.LinksFor builds for a combined
    // profile, so a coordinator-manager sees Manager guidance before Coordinator guidance.
    private static readonly (string RoleKey, string ResourceName, string Title)[] RoleGuides =
    [
        ("Manager", "manager-guide.md", "Manager guide"),
        ("AppointmentStaff", "appointment-staff-guide.md", "Appointment staff guide"),
        ("Coordinator", "coordinator-guide.md", "Coordinator guide"),
    ];

    private static readonly (string RoleKey, string ResourceName, string Title) AdminGuide =
        ("Admin", "admin-guide.md", "Admin guide");

    private static readonly (string RoleKey, string ResourceName, string Title) AttendeeGuideEntry =
        ("Attendee", "attendee-guide.md", "Attendee guide");

    // Every signed-in staff member can read every guide, including the attendee guide — a
    // coordinator fielding a attendee's question, or a manager covering another type, needs the
    // same reference the other roles see, not just their own. Only a signed-out visitor is
    // restricted to the attendee guide alone (the <NotAuthorized> branch in Help.razor).
    private static readonly (string RoleKey, string ResourceName, string Title)[] AllGuides =
        [AdminGuide, .. RoleGuides, AttendeeGuideEntry];

    // GuidesFor unlocks the full catalog for anyone holding at least one of these — an account
    // whose only role isn't one EventBooking recognises still gets no guide, matching Home.razor's
    // "not assigned a role yet" message rather than dumping the whole catalog on a role typo.
    private static readonly HashSet<string> RecognisedStaffRoles = new(
        RoleGuides.Select(guide => guide.RoleKey).Append(AdminGuide.RoleKey),
        StringComparer.Ordinal);

    // The screenshots the guides embed as "screenshots/foo.png" (relative to docs/user-guides/) are
    // published to wwwroot/help-assets/screenshots by the .csproj. Blazor's <base href="/"> resolves
    // relative URLs against "/" regardless of the current route, not against "/help", so the bare
    // relative path 404s (rendering as a broken-image placeholder) unless rewritten to an absolute
    // one — and that path deliberately isn't under /help/, which would collide with the client-side
    // /help route (see the .csproj comment on the Content item for the failure mode).
    private const string ScreenshotMarkdownPrefix = "(screenshots/";
    private const string ScreenshotUrlPrefix = "(/help-assets/screenshots/";

    // Every guide opens with this exact line pointing back to the standalone docs/user-guides
    // index — useful when the file is read on its own (e.g. on GitHub), but redundant on the Help
    // page now that every guide sits on the one page under its own jump-link in the index above
    // them (see Help.razor). Dropped along with the blank line that follows it, so removing it
    // doesn't leave a gap between the heading and the guide's first paragraph.
    private const string BackToAllGuidesLine = "[← All user guides](README.md)\n\n";

    // manager-guide.md links on to "appointment-staff-guide.md". Rendered guides sit inline on one
    // page rather than as browsable files, so that bare filename is rewritten to an in-page anchor
    // to avoid producing a dead link.
    private static readonly Dictionary<string, string> AnchorsByResourceName = RoleGuides
        .Append(AdminGuide)
        .Append(AttendeeGuideEntry)
        .ToDictionary(guide => guide.ResourceName, guide => guide.RoleKey.ToLowerInvariant());

    private static readonly Dictionary<string, string> RenderedHtmlByResourceName = new(StringComparer.Ordinal);
    private static readonly Lock RenderLock = new();

    /// <summary>
    /// Builds every guide for a caller holding at least one recognised staff role — the full
    /// catalog, not just the guides matching their own roles, since staff routinely need to
    /// understand what other roles (and attendees) see. A caller with no recognised role gets
    /// none, so Help.razor can show its "not assigned a role yet" message.
    /// </summary>
    public static IReadOnlyList<UserGuide> GuidesFor(IReadOnlyList<string> roles) =>
        roles.Any(RecognisedStaffRoles.Contains) ? AllGuides.Select(BuildGuide).ToList() : [];

    /// <summary>Builds the attendee guide shown to signed-out visitors of the Help page.</summary>
    public static UserGuide AttendeeGuide() => BuildGuide(AttendeeGuideEntry);

    private static UserGuide BuildGuide((string RoleKey, string ResourceName, string Title) guide) =>
        new(guide.RoleKey, AnchorsByResourceName[guide.ResourceName], guide.Title, RenderedHtmlFor(guide.ResourceName));

    private static string RenderedHtmlFor(string resourceName)
    {
        lock (RenderLock)
        {
            if (RenderedHtmlByResourceName.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var markdown = RewriteCrossGuideLinks(ReadEmbeddedMarkdown(resourceName));
            var html = Markdig.Markdown.ToHtml(markdown);
            RenderedHtmlByResourceName[resourceName] = html;
            return html;
        }
    }

    private static string ReadEmbeddedMarkdown(string resourceName)
    {
        var assembly = typeof(UserGuideCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded user guide '{resourceName}' is missing. Check the EmbeddedResource glob in EventBooking.Web.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string RewriteCrossGuideLinks(string markdown)
    {
        var rewritten = markdown
            .Replace(BackToAllGuidesLine, string.Empty, StringComparison.Ordinal)
            .Replace(ScreenshotMarkdownPrefix, ScreenshotUrlPrefix, StringComparison.Ordinal);
        foreach (var (resourceName, anchor) in AnchorsByResourceName)
        {
            // Blazor's <base href="/"> resolves a bare "#anchor" against "/", not against the
            // current route — the same failure mode as the screenshot paths above — so a fragment
            // link needs the /help prefix to land back on this page instead of Home.
            rewritten = rewritten.Replace($"({resourceName})", $"(/help#{anchor})", StringComparison.Ordinal);
        }

        return rewritten;
    }
}
`````

## before — src/EventBooking.Web/Shared/AuditHistory.razor — 1/1

<!-- vocabulary-file: {"id":216,"oldPath":"src/EventBooking.Web/Shared/AuditHistory.razor","newPath":"src/EventBooking.Web/Shared/AuditHistory.razor","beforeSha":"a638e20addc6e766bc940f2911c70dfb115e88fcb813fa2a9d29e80ce60971d9","afterSha":"038a98ce1ad932227bc40350957de87050b155fea848c6a60a1e9e2453fcd842","side":"before","part":1,"parts":1} -->

`````razor
@using EventBooking.Web.Services
@inject AuditClient Audit
@inject HeadOfficeTimePresentation TimePresentation

<details class="audit-history" @ontoggle="OnToggleAsync">
    <summary title="Every recorded change, newest first, with who made it. Loaded when you expand it.">History</summary>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (!_requested)
    {
        <p>Loading…</p>
    }
    else if (_rows is { Count: 0 })
    {
        <p>Nothing recorded yet.</p>
    }
    else if (_rows is not null)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr><th scope="col">When</th><th scope="col">What</th><th scope="col">Who</th><th scope="col">Details</th></tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</details>

@code {
    /// <summary>Either a confirmed slot or a candidate. Set exactly one.</summary>
    [Parameter]
    public Guid? SlotId { get; set; }

    [Parameter]
    public Guid? CandidateId { get; set; }

    private List<AuditRowDto>? _rows;
    private string? _error;
    private bool _requested;

    // Loaded on first expand rather than on render: a table of 20 slots should not fire 20 requests.
    private async Task OnToggleAsync()
    {
        if (_requested)
        {
            return;
        }

        _requested = true;

        try
        {
            var outcome = SlotId is not null
                ? await Audit.ForSlotAsync(SlotId.Value, CancellationToken.None)
                : await Audit.ForCandidateAsync(CandidateId!.Value, CancellationToken.None);

            _rows = outcome.Value;
            _error = outcome.ErrorMessage;
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
    }
}
`````

## after — src/EventBooking.Web/Shared/AuditHistory.razor — 1/1

<!-- vocabulary-file: {"id":216,"oldPath":"src/EventBooking.Web/Shared/AuditHistory.razor","newPath":"src/EventBooking.Web/Shared/AuditHistory.razor","beforeSha":"a638e20addc6e766bc940f2911c70dfb115e88fcb813fa2a9d29e80ce60971d9","afterSha":"038a98ce1ad932227bc40350957de87050b155fea848c6a60a1e9e2453fcd842","side":"after","part":1,"parts":1} -->

`````razor
@using EventBooking.Web.Services
@inject AuditClient Audit
@inject TransitionalLocationTimePresentation TimePresentation

<details class="audit-history" @ontoggle="OnToggleAsync">
    <summary title="Every recorded change, newest first, with who made it. Loaded when you expand it.">History</summary>

    @if (_error is not null)
    {
        <p class="error" role="alert">@_error</p>
    }
    else if (!_requested)
    {
        <p>Loading…</p>
    }
    else if (_rows is { Count: 0 })
    {
        <p>Nothing recorded yet.</p>
    }
    else if (_rows is not null)
    {
        <div class="table-wrap">
            <table>
                <thead>
                    <tr><th scope="col">When</th><th scope="col">What</th><th scope="col">Who</th><th scope="col">Details</th></tr>
                </thead>
                <tbody>
                    @foreach (var row in _rows)
                    {
                        <tr>
                            <td data-label="When">@TimePresentation.Format(row.Timestamp)</td>
                            <td data-label="What">@row.Action</td>
                            <td data-label="Who">@row.ActorType @row.ActorId</td>
                            <td data-label="Details">@row.Details</td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</details>

@code {
    /// <summary>Either a event or a attendee. Set exactly one.</summary>
    [Parameter]
    public Guid? EventId { get; set; }

    [Parameter]
    public Guid? AttendeeId { get; set; }

    private List<AuditRowDto>? _rows;
    private string? _error;
    private bool _requested;

    // Loaded on first expand rather than on render: a table of 20 events should not fire 20 requests.
    private async Task OnToggleAsync()
    {
        if (_requested)
        {
            return;
        }

        _requested = true;

        try
        {
            var outcome = EventId is not null
                ? await Audit.ForEventAsync(EventId.Value, CancellationToken.None)
                : await Audit.ForAttendeeAsync(AttendeeId!.Value, CancellationToken.None);

            _rows = outcome.Value;
            _error = outcome.ErrorMessage;
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
    }
}
`````

## before — src/EventBooking.Web/wwwroot/appsettings.json — 1/1

<!-- vocabulary-file: {"id":217,"oldPath":"src/EventBooking.Web/wwwroot/appsettings.json","newPath":"src/EventBooking.Web/wwwroot/appsettings.json","beforeSha":"4f2f350c862a8ade3be4cc8da7b09571e3af9e712c6f3538d49b69d2e863485d","afterSha":"822dbb4222ac55b5db08f1d1fb794270a2aac20e7a108c08e1b214b425ef8bed","side":"before","part":1,"parts":1} -->

`````text
{
  "ApiBaseUrl": "https://localhost:5001",
  "CoordinatorContact": "recruitment@example.com",
  "HeadOfficeTimeZoneId": "Europe/London",
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "ClientId": "eventbooking-web"
    }
  }
}
`````

## after — src/EventBooking.Web/wwwroot/appsettings.json — 1/1

<!-- vocabulary-file: {"id":217,"oldPath":"src/EventBooking.Web/wwwroot/appsettings.json","newPath":"src/EventBooking.Web/wwwroot/appsettings.json","beforeSha":"4f2f350c862a8ade3be4cc8da7b09571e3af9e712c6f3538d49b69d2e863485d","afterSha":"822dbb4222ac55b5db08f1d1fb794270a2aac20e7a108c08e1b214b425ef8bed","side":"after","part":1,"parts":1} -->

`````text
{
  "ApiBaseUrl": "https://localhost:5001",
  "CoordinatorContact": "recruitment@example.com",
  "TransitionalLocationTimeZoneId": "Europe/London",
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "ClientId": "eventbooking-web"
    }
  }
}
`````
