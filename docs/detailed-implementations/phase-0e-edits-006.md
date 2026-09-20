# 00e — Require an attendee group, edits 6 (Task 3c)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Web/Services/AttendeesClient.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.Web/Services/AttendeesClient.cs","beforeSha":"232ed7e56281f88f80ce96b6427d449e7ac3e0d26d8f1d72976960f825fefd1d","afterSha":"9efae87e950589686fda7d641064fc0c4f2840473e67aec1c2d2471afef39659","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Json;
using System.Text;

namespace EventBooking.Web.Services;

public sealed record AppointmentTypeSummaryDto(string Code, string Name);

public sealed record AttendeeGroupOptionDto(
    Guid AttendeeGroupId,
    string Code,
    string Name,
    IReadOnlyList<AppointmentTypeSummaryDto> RequiredAppointmentTypes);

public sealed record AttendeeDto(
    Guid AttendeeId,
    string Name,
    string Email,
    Guid AttendeeGroupId,
    string AttendeeGroupCode,
    string AttendeeGroupName,
    IReadOnlyList<AppointmentTypeSummaryDto> RequiredAppointmentTypes,
    int Status,
    string StatusDisplay);

public sealed record ImportErrorDto(int LineNumber, string Message);

public sealed record ImportOutcomeDto(
    bool Accepted,
    int ImportedCount,
    IReadOnlyList<ImportErrorDto> Errors);

/// <summary>Reports the durable result of a template-aware email retry.</summary>
/// <param name="DeliveryStatus">The provider outcome of the replacement attempt.</param>
/// <param name="DeliveryId">The new durable delivery identifier.</param>
public sealed record EmailRetryDto(string DeliveryStatus, Guid DeliveryId);

/// <summary>Minimum canonical detail for one incomplete appointment type.</summary>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="IsRecoverable">Whether recovery can currently be started for this type.</param>
public sealed record OutstandingAppointmentTypeDto(string Code, string Name, bool IsRecoverable);

/// <summary>Durable delivery outcome for one started recovery invite.</summary>
/// <param name="InviteId">The new recovery Invite identifier, or empty when awaiting availability.</param>
/// <param name="AppointmentTypeIds">The recoverable snapshot offered, or awaiting availability.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
public sealed record RecoveryInviteOutcomeDto(
    Guid InviteId,
    IReadOnlyList<Guid> AppointmentTypeIds,
    bool EmailSent);

/// <summary>One active booking a coordinator may cancel; carries no management token.</summary>
/// <param name="BookingId">The booking identifier used to target a cancellation.</param>
/// <param name="IsOriginal">True for the original booking; false for an active recovery booking.</param>
/// <param name="EventDate">The date of the confirmed window the booking holds.</param>
/// <param name="EventStartTime">The start of the confirmed window the booking holds.</param>
/// <param name="EventEndTime">The end of the confirmed window the booking holds.</param>
public sealed record AttendeeBookingDto(
    Guid BookingId,
    bool IsOriginal,
    DateOnly EventDate,
    TimeOnly EventStartTime,
    TimeOnly EventEndTime);

/// <summary>Coordinator-facing outcome of cancelling one attendee booking.</summary>
/// <param name="Reinvited">Whether a replacement invite was created for the attendee.</param>
/// <param name="InviteCreated">The explicit replacement-invite creation state.</param>
/// <param name="DeliveryStatus">The provider outcome, or Unavailable when no replacement invite exists.</param>
/// <param name="DeliveryId">The durable replacement delivery identifier, when one was staged.</param>
public sealed record CancelAttendeeBookingDto(
    bool Reinvited,
    bool InviteCreated,
    string? DeliveryStatus,
    Guid? DeliveryId);

/// <summary>Coordinator-facing readiness for one attendee.</summary>
/// <param name="AttendeeId">The stable attendee identifier.</param>
/// <param name="Code">The stable machine-readable readiness reason.</param>
/// <param name="Display">The Coordinator-facing explanation.</param>
/// <param name="OutstandingAppointmentTypes">Incomplete current appointment types.</param>
public sealed record AttendeeReadinessDto(
    Guid AttendeeId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentTypeDto> OutstandingAppointmentTypes);

public sealed class AttendeesClient(HttpClient http)
{
    public async Task<ApiOutcome<List<AttendeeDto>>> ListAsync(
        int? status, string? search, CancellationToken cancellationToken)
    {
        var parameters = new List<string>();
        if (status is not null)
        {
            parameters.Add($"status={status.Value}");
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            parameters.Add($"search={Uri.EscapeDataString(search)}");
        }

        var route = parameters.Count == 0
            ? "/api/attendees"
            : $"/api/attendees?{string.Join("&", parameters)}";

        using var response = await http.GetAsync(route, cancellationToken);
        return await ApiCall.ReadAsync<List<AttendeeDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<List<AttendeeGroupOptionDto>>> ListGroupsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("/api/attendee-groups", cancellationToken);
        return await ApiCall.ReadAsync<List<AttendeeGroupOptionDto>>(response, cancellationToken);
    }

    public async Task<ApiOutcome<Guid>> CreateAsync(
        string name, string email, Guid? attendeeGroupId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            "/api/attendees",
            new { Name = name, Email = email, AttendeeGroupId = attendeeGroupId },
            cancellationToken);

        return await ApiCall.ReadAsync<Guid>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> UpdateAsync(
        Guid id, string name, string email, Guid? attendeeGroupId, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(
            $"/api/attendees/{id}",
            new { Name = name, Email = email, AttendeeGroupId = attendeeGroupId },
            cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> DeleteAsync(
        Guid id, bool confirm, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/attendees/{id}?confirm={(confirm ? "true" : "false")}", cancellationToken);

        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    public async Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(
        string csv, CancellationToken cancellationToken)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        using var response = await http.PostAsync("/api/attendees/import", content, cancellationToken);

        return await ApiCall.ReadAsync<ImportOutcomeDto>(response, cancellationToken);
    }

    public async Task<ApiOutcome<bool>> TriggerInviteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"/api/attendees/{id}/invite", null, cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    /// <summary>Retries the latest failed or pending delivery using its server-side template.</summary>
    public async Task<ApiOutcome<EmailRetryDto>> RetryEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"/api/attendees/{id}/email-retry", null, cancellationToken);
        return await ApiCall.ReadAsync<EmailRetryDto>(response, cancellationToken);
    }

    /// <summary>Starts one recovery invite for the attendee's missed appointments.</summary>
    public async Task<ApiOutcome<RecoveryInviteOutcomeDto>> StartRecoveryAsync(
        Guid attendeeId,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(
            $"/api/attendees/{attendeeId}/recovery-invites", null, cancellationToken);
        return await ApiCall.ReadAsync<RecoveryInviteOutcomeDto>(response, cancellationToken);
    }

    /// <summary>Cancels one pending recovery invite without touching bookings.</summary>
    public async Task<ApiOutcome<bool>> CancelRecoveryAsync(
        Guid attendeeId,
        Guid inviteId,
        CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(
            $"/api/attendees/{attendeeId}/recovery-invites/{inviteId}", cancellationToken);
        return await ApiCall.ReadNoContentAsync(response, cancellationToken);
    }

    /// <summary>Lists the attendee's active bookings for the cancellation workflow.</summary>
    /// <param name="attendeeId">The attendee whose bookings are listed.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The active bookings, or the failure the API reported.</returns>
    public async Task<ApiOutcome<List<AttendeeBookingDto>>> GetBookingsAsync(
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"/api/attendees/{attendeeId}/bookings", cancellationToken);
        return await ApiCall.ReadAsync<List<AttendeeBookingDto>>(response, cancellationToken);
    }

    /// <summary>
    /// Cancels one of the attendee's active bookings. Requesting a replacement invite is valid
    /// only for the original booking; the API refuses it for a recovery booking.
    /// </summary>
    /// <param name="attendeeId">The attendee the booking belongs to.</param>
    /// <param name="bookingId">The booking to cancel.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The cancellation outcome, or the failure the API reported.</returns>
    public async Task<ApiOutcome<CancelAttendeeBookingDto>> CancelBookingAsync(
        Guid attendeeId,
        Guid bookingId,
        bool rebook,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"/api/attendees/{attendeeId}/bookings/{bookingId}/cancel",
            new { Rebook = rebook },
            cancellationToken);
        return await ApiCall.ReadAsync<CancelAttendeeBookingDto>(response, cancellationToken);
    }

    /// <summary>Gets internal readiness for a visible Attendee.</summary>
    public async Task<ApiOutcome<AttendeeReadinessDto>> GetReadinessAsync(
        Guid attendeeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"/api/attendees/{attendeeId}/readiness", cancellationToken);
        return await ApiCall.ReadAsync<AttendeeReadinessDto>(response, cancellationToken);
    }
}
`````

## after — tests/EventBooking.Api.Tests/RequiredAttendeeGroupContractTests.cs — 1/1

<!-- retirement-file: {"id":14,"file":"tests/EventBooking.Api.Tests/RequiredAttendeeGroupContractTests.cs","beforeSha":null,"afterSha":"23e445fb47326dd3f6297927e0f3b70338eb80d35679d932b8d8b8a1a90f8d70","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Attendees;

namespace EventBooking.Api.Tests;

public sealed class RequiredAttendeeGroupContractTests
{
    [Fact]
    public void Readiness_and_list_contracts_have_no_reconciliation_state()
    {
        Assert.DoesNotContain("AttendeeGroupUnassigned", Enum.GetNames<AttendeeReadinessCode>());
        Assert.DoesNotContain(typeof(AttendeeListItem).GetProperties(),
            p => p.Name == "RequiresAttendeeGroupReconciliation");
        Assert.Equal(typeof(Guid), typeof(AttendeeListItem).GetProperty("AttendeeGroupId")!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(AttendeeReadinessSnapshot).GetProperty("AttendeeGroupId")!.PropertyType);
    }
}
`````

## before — tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs — 1/1

<!-- retirement-file: {"id":15,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs","beforeSha":"9eeefb71008758e62fe664b243b999b9805484bfc06dd334a05a9d1d1166d110","afterSha":"564d0e9d7d45a3d431422b0dd1f38494c22205a6c3edb97947b1da55caa7ba7e","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Attendees;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies readiness precedence and latest-attempt semantics.</summary>
public sealed class AttendeeReadinessCalculatorTests
{
    private readonly AttendeeReadinessCalculator _calculator = new();

    /// <summary>A later Completed recovery attempt supersedes the original NoShow.</summary>
    [Fact]
    public void RecoveryCompletionMakesAttendeeReady()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.Completed, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>Any non-cancelled Completed attempt satisfies the type despite a later NoShow.</summary>
    [Fact]
    public void AnyCompletedAttemptControlsOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.UniformFitting;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.Completed, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.NoShow, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>A latest NoShow with no completion is outstanding and recoverable.</summary>
    [Fact]
    public void LatestNoShowIsRecoverable()
    {
        var original = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [type], original,
            [Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1)]));

        Assert.Equal(AttendeeReadinessCode.AppointmentsOutstanding, actual.Code);
        var outstanding = Assert.Single(actual.OutstandingAppointmentTypes);
        Assert.Equal("MED", outstanding.Code);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>Unassigned reconciliation state wins over missing Booking state.</summary>
    [Fact]
    public void UnassignedGroupHasHighestFailurePrecedence()
    {
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), null, [], null, []));

        Assert.Equal(AttendeeReadinessCode.AttendeeGroupUnassigned, actual.Code);
    }

    private static AttendeeReadinessAttempt Attempt(
        Guid typeId,
        Guid bookingId,
        BookingStatus bookingStatus,
        BookingAppointmentStatus status,
        int day) => new(
            Guid.NewGuid(), typeId, status, bookingId, bookingStatus,
            DateTimeOffset.Parse($"2026-09-{day:00}T09:00:00Z"));
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs — 1/1

<!-- retirement-file: {"id":15,"file":"tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs","beforeSha":"9eeefb71008758e62fe664b243b999b9805484bfc06dd334a05a9d1d1166d110","afterSha":"564d0e9d7d45a3d431422b0dd1f38494c22205a6c3edb97947b1da55caa7ba7e","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Attendees;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Attendees;

/// <summary>Verifies readiness precedence and latest-attempt semantics.</summary>
public sealed class AttendeeReadinessCalculatorTests
{
    private readonly AttendeeReadinessCalculator _calculator = new();

    /// <summary>A later Completed recovery attempt supersedes the original NoShow.</summary>
    [Fact]
    public void RecoveryCompletionMakesAttendeeReady()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.Completed, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>Any non-cancelled Completed attempt satisfies the type despite a later NoShow.</summary>
    [Fact]
    public void AnyCompletedAttemptControlsOutcome()
    {
        var attendeeId = Guid.NewGuid();
        var original = Guid.NewGuid();
        var recovery = Guid.NewGuid();
        var type = AppointmentTypeIds.UniformFitting;
        var snapshot = new AttendeeReadinessSnapshot(
            attendeeId,
            Guid.NewGuid(),
            [type],
            original,
            [
                Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.Completed, 1),
                Attempt(type, recovery, BookingStatus.Concluded, BookingAppointmentStatus.NoShow, 2),
            ]);

        var actual = _calculator.Calculate(snapshot);

        Assert.Equal(AttendeeReadinessCode.Ready, actual.Code);
        Assert.Empty(actual.OutstandingAppointmentTypes);
    }

    /// <summary>A latest NoShow with no completion is outstanding and recoverable.</summary>
    [Fact]
    public void LatestNoShowIsRecoverable()
    {
        var original = Guid.NewGuid();
        var type = AppointmentTypeIds.MedicalCheckUp;
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [type], original,
            [Attempt(type, original, BookingStatus.Active, BookingAppointmentStatus.NoShow, 1)]));

        Assert.Equal(AttendeeReadinessCode.AppointmentsOutstanding, actual.Code);
        var outstanding = Assert.Single(actual.OutstandingAppointmentTypes);
        Assert.Equal("MED", outstanding.Code);
        Assert.True(outstanding.IsRecoverable);
    }

    /// <summary>A required group without an active original booking is not ready.</summary>
    [Fact]
    public void AssignedGroupWithoutBookingIsNotReady()
    {
        var actual = _calculator.Calculate(new AttendeeReadinessSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), [], null, []));

        Assert.Equal(AttendeeReadinessCode.NoActiveBooking, actual.Code);
    }

    private static AttendeeReadinessAttempt Attempt(
        Guid typeId,
        Guid bookingId,
        BookingStatus bookingStatus,
        BookingAppointmentStatus status,
        int day) => new(
            Guid.NewGuid(), typeId, status, bookingId, bookingStatus,
            DateTimeOffset.Parse($"2026-09-{day:00}T09:00:00Z"));
}
`````

## before — tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":16,"file":"tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs","beforeSha":"dbc262fe1b172ed395df550746ab36bf51d5ad8e133b63db49fbcc642357c8cd","afterSha":"6b80452bae44ef392a3464a64c109a2331576c1a407fe3efb57cae375a6f696f","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class ListAttendeesHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();

    private ListAttendeesHandler Handler => new(
        _attendees, _groups, new StaffAccessAuthorizer(_roles));

    public ListAttendeesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]));

        _attendees.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.CabinCrew)));

        var chen = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        chen.MarkInvited();
        _attendees.Add(chen);

        var diallo = Attendee.Create(
            Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        diallo.MarkAwaitingAvailability();
        _attendees.Add(diallo);
    }

    [Fact]
    public async Task EveryAttendeeIsListedWithGroupAndDisplayStatus()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var novak = result.Value.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal(AttendeeGroupIds.CabinCrew, novak.AttendeeGroupId);
        Assert.Equal("CABIN_CREW", novak.AttendeeGroupCode);
        Assert.Equal("Cabin Crew", novak.AttendeeGroupName);
        Assert.False(novak.RequiresAttendeeGroupReconciliation);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            novak.RequiredAppointmentTypes.Select(summary => summary.Code));
        Assert.Equal(AttendeeStatus.NotYetInvited, novak.Status);
        Assert.Equal("Not yet invited", novak.StatusDisplay);

        var diallo = result.Value.Single(c => c.Email == "c.diallo@mail.com");
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, diallo.AttendeeGroupId);
        Assert.Equal("GROUND_OPERATIONS_AGENT", diallo.AttendeeGroupCode);
        Assert.Equal("Ground Operations Agent", diallo.AttendeeGroupName);
        Assert.False(diallo.RequiresAttendeeGroupReconciliation);

        Assert.Equal(
            "Invited (pending response)",
            result.Value.Single(c => c.Email == "b.chen@mail.com").StatusDisplay);
        Assert.Equal(
            "Awaiting availability",
            result.Value.Single(c => c.Email == "c.diallo@mail.com").StatusDisplay);
    }

    [Fact]
    public async Task TheListIsOrderedByName()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.Equal(new[] { "A. Novak", "B. Chen", "C. Diallo" }, result.Value.Select(c => c.Name));
    }

    [Fact]
    public async Task FilteringByStatusNarrowsTheList()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, AttendeeStatus.Invited, null), CancellationToken.None);

        Assert.Equal("b.chen@mail.com", Assert.Single(result.Value).Email);
    }

    [Theory]
    [InlineData("diallo")]
    [InlineData("DIALLO")]
    [InlineData("c.diallo@mail.com")]
    public async Task SearchMatchesNameOrEmailCaseInsensitively(string search)
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, search), CancellationToken.None);

        Assert.Equal("C. Diallo", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task AnEmptySearchIsIgnored()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, "   "), CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## after — tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs — 1/1

<!-- retirement-file: {"id":16,"file":"tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs","beforeSha":"dbc262fe1b172ed395df550746ab36bf51d5ad8e133b63db49fbcc642357c8cd","afterSha":"6b80452bae44ef392a3464a64c109a2331576c1a407fe3efb57cae375a6f696f","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Attendees;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;

namespace EventBooking.Application.Tests.Attendees;

public class ListAttendeesHandlerTests
{
    private static readonly Guid Coordinator = Guid.Parse("c0000009-0000-0000-0000-000000000009");

    private readonly InMemoryAttendeeRepository _attendees = new();
    private readonly InMemoryAttendeeGroupRepository _groups = new();
    private readonly InMemoryStaffAccessProfileRepository _roles = new();

    private ListAttendeesHandler Handler => new(
        _attendees, _groups, new StaffAccessAuthorizer(_roles));

    public ListAttendeesHandlerTests()
    {
        _roles.Add(StaffAccessProfile.Create(Coordinator, Role.Coordinator, null));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp,
                AppointmentTypeIds.UniformFitting]));
        _groups.Items.Add(AttendeeGroup.Define(
            AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
            "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]));

        _attendees.Add(Attendee.Create(
            Guid.NewGuid(), "A. Novak", "a.novak@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.CabinCrew)));

        var chen = Attendee.Create(
            Guid.NewGuid(), "B. Chen", "b.chen@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        chen.MarkInvited();
        _attendees.Add(chen);

        var diallo = Attendee.Create(
            Guid.NewGuid(), "C. Diallo", "c.diallo@mail.com",
            _groups.Items.Single(group => group.Id == AttendeeGroupIds.GroundOperationsAgent));
        diallo.MarkAwaitingAvailability();
        _attendees.Add(diallo);
    }

    [Fact]
    public async Task EveryAttendeeIsListedWithGroupAndDisplayStatus()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        var novak = result.Value.Single(c => c.Email == "a.novak@mail.com");
        Assert.Equal(AttendeeGroupIds.CabinCrew, novak.AttendeeGroupId);
        Assert.Equal("CABIN_CREW", novak.AttendeeGroupCode);
        Assert.Equal("Cabin Crew", novak.AttendeeGroupName);
        Assert.Equal(
            new[] { "DAT", "MED", "UNI" },
            novak.RequiredAppointmentTypes.Select(summary => summary.Code));
        Assert.Equal(AttendeeStatus.NotYetInvited, novak.Status);
        Assert.Equal("Not yet invited", novak.StatusDisplay);

        var diallo = result.Value.Single(c => c.Email == "c.diallo@mail.com");
        Assert.Equal(AttendeeGroupIds.GroundOperationsAgent, diallo.AttendeeGroupId);
        Assert.Equal("GROUND_OPERATIONS_AGENT", diallo.AttendeeGroupCode);
        Assert.Equal("Ground Operations Agent", diallo.AttendeeGroupName);

        Assert.Equal(
            "Invited (pending response)",
            result.Value.Single(c => c.Email == "b.chen@mail.com").StatusDisplay);
        Assert.Equal(
            "Awaiting availability",
            result.Value.Single(c => c.Email == "c.diallo@mail.com").StatusDisplay);
    }

    [Fact]
    public async Task TheListIsOrderedByName()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, null), CancellationToken.None);

        Assert.Equal(new[] { "A. Novak", "B. Chen", "C. Diallo" }, result.Value.Select(c => c.Name));
    }

    [Fact]
    public async Task FilteringByStatusNarrowsTheList()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, AttendeeStatus.Invited, null), CancellationToken.None);

        Assert.Equal("b.chen@mail.com", Assert.Single(result.Value).Email);
    }

    [Theory]
    [InlineData("diallo")]
    [InlineData("DIALLO")]
    [InlineData("c.diallo@mail.com")]
    public async Task SearchMatchesNameOrEmailCaseInsensitively(string search)
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, search), CancellationToken.None);

        Assert.Equal("C. Diallo", Assert.Single(result.Value).Name);
    }

    [Fact]
    public async Task AnEmptySearchIsIgnored()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Coordinator, null, "   "), CancellationToken.None);

        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task ANonCoordinatorIsForbidden()
    {
        var result = await Handler.HandleAsync(
            new ListAttendeesQuery(Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error.Code);
    }
}
`````

## after — tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs — 1/1

<!-- retirement-file: {"id":17,"file":"tests/EventBooking.Domain.Tests/Attendees/RequiredAttendeeGroupTests.cs","beforeSha":null,"afterSha":"bf29699d2d6fdf72a3f189d825de0594a813d6aa1d66e65a74b84b25c17b76e0","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Attendees;

namespace EventBooking.Domain.Tests.Attendees;

public sealed class RequiredAttendeeGroupTests
{
    [Fact]
    public void Creation_without_a_group_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Attendee.Create(Guid.NewGuid(), "Demo attendee", "demo@example.test", null!));
    }

    [Fact]
    public void Aggregate_does_not_expose_a_nullable_group_identifier()
    {
        Assert.Equal(typeof(Guid), typeof(Attendee).GetProperty(nameof(Attendee.AttendeeGroupId))!.PropertyType);
    }
}
`````

## before — tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs","beforeSha":"806c53f6a7f1ab8043a43724493c4a70af28318b1ac8abd7ec7f80f9af06dd8f","afterSha":"f5ab349cd6078c7169220f80150a76dd937078648a3b1ca153b190f2cfa99d09","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the attendee read-parity contract exposed by MCP.</summary>
[Collection("mcp")]
public sealed class AttendeeMcpTests(McpFactory factory)
{
    /// <summary>Listing exposes group identity, derived requirements, and readiness.</summary>
    [Fact]
    public async Task ListAttendees_ReturnsGroupRequirementsAndReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var email = $"mcp-{Guid.NewGuid():N}@example.com";
        await CallToolResultAsync(
            "create_attendee",
            new { name = "Mcp Pilot", email, attendeeGroupCode = "PILOTS" });

        var items = await CallToolResultAsync(
            "list_attendees", new { search = email });
        var item = items.EnumerateArray().Single();

        Assert.Equal("PILOTS", item.GetProperty("attendeeGroupCode").GetString());
        Assert.False(item.GetProperty("requiresAttendeeGroupReconciliation").GetBoolean());
        Assert.Equal(2, item.GetProperty("requiredAppointmentTypes").GetArrayLength());
        Assert.Equal(
            "NoActiveBooking",
            item.GetProperty("readiness").GetProperty("code").GetString());
    }

    /// <summary>Pages slice the list without overlap.</summary>
    [Fact]
    public async Task ListAttendees_PagesWithoutOverlap()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var prefix = $"paged-{Guid.NewGuid():N}";
        for (var index = 0; index < 3; index++)
        {
            await CallToolResultAsync(
                "create_attendee",
                new
                {
                    name = $"Paged {index}",
                    email = $"{prefix}-{index}@example.com",
                    attendeeGroupCode = "PILOTS",
                });
        }

        var first = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 1, pageSize = 2 });
        var second = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 2, pageSize = 2 });

        Assert.Equal(2, first.GetArrayLength());
        Assert.Single(second.EnumerateArray());
        Assert.Empty(
            first.EnumerateArray().Select(item => item.GetRawText())
                .Intersect(second.EnumerateArray().Select(item => item.GetRawText())));
    }

    /// <summary>An unknown status surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ListAttendees_RejectsUnknownStatus()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("list_attendees", new { status = "Bogus" });

        Assert.True(IsToolError(payload));
    }

    /// <summary>Create input accepts a group code and no appointment-type codes.</summary>
    [Fact]
    public async Task CreateAttendeeSchema_HasGroupCodeWithoutAppointmentTypes()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var create = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "create_attendee");
        var properties = create
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("attendeeGroupCode", properties);
        Assert.DoesNotContain("appointmentTypeIds", properties);
        Assert.DoesNotContain("appointmentTypes", properties);
        Assert.DoesNotContain("requiredTypes", properties);
    }

    /// <summary>Recovery tool inputs carry attendee and invite identifiers and no token.</summary>
    [Fact]
    public async Task RecoveryToolSchemas_HaveIdentifiersWithoutTokens()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var tools = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Where(tool => tool.GetProperty("name").GetString() is "start_recovery_invite" or "cancel_recovery_invite")
            .ToList();

        Assert.Equal(2, tools.Count);
        foreach (var tool in tools)
        {
            var properties = tool
                .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("attendeeId", properties);
            Assert.DoesNotContain("token", properties);
        }

        var cancel = tools.Single(tool => tool.GetProperty("name").GetString() == "cancel_recovery_invite");
        var cancelProperties = cancel
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("inviteId", cancelProperties);
    }

    private async Task<JsonElement> CallToolResultAsync(string name, object arguments)
    {
        var payload = await CallToolAsync(name, arguments);
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }

        var data = body.Split('\n').Select(line => line.Trim())
            .Last(line => line.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## after — tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs — 1/1

<!-- retirement-file: {"id":18,"file":"tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs","beforeSha":"806c53f6a7f1ab8043a43724493c4a70af28318b1ac8abd7ec7f80f9af06dd8f","afterSha":"f5ab349cd6078c7169220f80150a76dd937078648a3b1ca153b190f2cfa99d09","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers the attendee read-parity contract exposed by MCP.</summary>
[Collection("mcp")]
public sealed class AttendeeMcpTests(McpFactory factory)
{
    /// <summary>Listing exposes group identity, derived requirements, and readiness.</summary>
    [Fact]
    public async Task ListAttendees_ReturnsGroupRequirementsAndReadiness()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var email = $"mcp-{Guid.NewGuid():N}@example.com";
        await CallToolResultAsync(
            "create_attendee",
            new { name = "Mcp Pilot", email, attendeeGroupCode = "PILOTS" });

        var items = await CallToolResultAsync(
            "list_attendees", new { search = email });
        var item = items.EnumerateArray().Single();

        Assert.Equal("PILOTS", item.GetProperty("attendeeGroupCode").GetString());
        Assert.False(item.TryGetProperty("requiresAttendeeGroupReconciliation", out _));
        Assert.Equal(2, item.GetProperty("requiredAppointmentTypes").GetArrayLength());
        Assert.Equal(
            "NoActiveBooking",
            item.GetProperty("readiness").GetProperty("code").GetString());
    }

    /// <summary>Pages slice the list without overlap.</summary>
    [Fact]
    public async Task ListAttendees_PagesWithoutOverlap()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);
        var prefix = $"paged-{Guid.NewGuid():N}";
        for (var index = 0; index < 3; index++)
        {
            await CallToolResultAsync(
                "create_attendee",
                new
                {
                    name = $"Paged {index}",
                    email = $"{prefix}-{index}@example.com",
                    attendeeGroupCode = "PILOTS",
                });
        }

        var first = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 1, pageSize = 2 });
        var second = await CallToolResultAsync(
            "list_attendees", new { search = prefix, page = 2, pageSize = 2 });

        Assert.Equal(2, first.GetArrayLength());
        Assert.Single(second.EnumerateArray());
        Assert.Empty(
            first.EnumerateArray().Select(item => item.GetRawText())
                .Intersect(second.EnumerateArray().Select(item => item.GetRawText())));
    }

    /// <summary>An unknown status surfaces as a tool error, not a transport failure.</summary>
    [Fact]
    public async Task ListAttendees_RejectsUnknownStatus()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await CallToolAsync("list_attendees", new { status = "Bogus" });

        Assert.True(IsToolError(payload));
    }

    /// <summary>Create input accepts a group code and no appointment-type codes.</summary>
    [Fact]
    public async Task CreateAttendeeSchema_HasGroupCodeWithoutAppointmentTypes()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var create = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("name").GetString() == "create_attendee");
        var properties = create
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("attendeeGroupCode", properties);
        Assert.DoesNotContain("appointmentTypeIds", properties);
        Assert.DoesNotContain("appointmentTypes", properties);
        Assert.DoesNotContain("requiredTypes", properties);
    }

    /// <summary>Recovery tool inputs carry attendee and invite identifiers and no token.</summary>
    [Fact]
    public async Task RecoveryToolSchemas_HaveIdentifiersWithoutTokens()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Coordinator], null);

        var payload = await PostRpcJsonAsync(new { jsonrpc = "2.0", id = "1", method = "tools/list" });
        var tools = payload
            .GetProperty("result").GetProperty("tools").EnumerateArray()
            .Where(tool => tool.GetProperty("name").GetString() is "start_recovery_invite" or "cancel_recovery_invite")
            .ToList();

        Assert.Equal(2, tools.Count);
        foreach (var tool in tools)
        {
            var properties = tool
                .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("attendeeId", properties);
            Assert.DoesNotContain("token", properties);
        }

        var cancel = tools.Single(tool => tool.GetProperty("name").GetString() == "cancel_recovery_invite");
        var cancelProperties = cancel
            .GetProperty("inputSchema").GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("inviteId", cancelProperties);
    }

    private async Task<JsonElement> CallToolResultAsync(string name, object arguments)
    {
        var payload = await CallToolAsync(name, arguments);
        Assert.False(IsToolError(payload), payload.GetRawText());
        var text = payload.GetProperty("result").GetProperty("content")[0]
            .GetProperty("text").GetString();
        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }

    private Task<JsonElement> CallToolAsync(string name, object arguments) =>
        PostRpcJsonAsync(new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "tools/call",
            @params = new { name, arguments },
        });

    private async Task<JsonElement> PostRpcJsonAsync(object body)
    {
        var response = await PostRpcAsync(body);
        response.EnsureSuccessStatusCode();
        return ParseRpcPayload(await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> PostRpcAsync(object body)
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }

    private static JsonElement ParseRpcPayload(string body)
    {
        if (body.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(body).RootElement.Clone();
        }

        var data = body.Split('\n').Select(line => line.Trim())
            .Last(line => line.StartsWith("data: "))["data: ".Length..];
        return JsonDocument.Parse(data).RootElement.Clone();
    }

    private static bool IsToolError(JsonElement payload) =>
        payload.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True;
}
`````

## before — tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs — 1/1

<!-- retirement-file: {"id":19,"file":"tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs","beforeSha":"53ac047434a11e50743e2124a7e37110bb66e507fd3ec2ebe3f130a0ed70062a","afterSha":"5b5777156e9fe807ce2eef1119981b4aa7cbc0532aa0d5c20869b473a3c74024","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the coordinator booking cell: its summary, confirmations, and outcome copy.</summary>
public class AttendeeBookingCancellationComponentTests : BunitContext
{
    private static readonly Guid AttendeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(object? value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8,
            "application/json"),
    };

    private static AttendeeBookingDto Booking(Guid id, bool isOriginal, int day) => new(
        id, isOriginal, new DateOnly(2026, 9, day), new TimeOnly(9, 0), new TimeOnly(13, 0));

    private static AngleSharp.Dom.IElement FindButton(IRenderedComponent<Attendees> cut, string text) =>
        cut.FindAll("button").First(button => button.TextContent.Trim() == text);

    /// <summary>Renders the attendees page with a stubbed bookings list and cancel response.</summary>
    private (IRenderedComponent<Attendees> Cut, StubHandler Handler) RenderWith(
        Queue<IReadOnlyList<AttendeeBookingDto>> bookingPages,
        Func<HttpRequestMessage, HttpResponseMessage>? cancel = null)
    {
        this.AddAuthorization().SetAuthorized("Coordinator");
        StubHandler? handler = null;
        handler = new StubHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post
                && path.StartsWith($"/api/attendees/{AttendeeId}/bookings/", StringComparison.Ordinal))
            {
                return cancel is not null
                    ? cancel(request)
                    : Json(new CancelAttendeeBookingDto(false, false, "Unavailable", null));
            }

            if (path == $"/api/attendees/{AttendeeId}/bookings")
            {
                return Json(bookingPages.Count > 0
                    ? bookingPages.Dequeue()
                    : Array.Empty<AttendeeBookingDto>());
            }

            if (path == "/api/attendees")
            {
                return Json(new[]
                {
                    new AttendeeDto(
                        AttendeeId, "Amara Novak", "a.novak@mail.com", null, null, null, false,
                        [], 4, "Booked"),
                });
            }

            if (path == "/api/attendee-groups")
            {
                return Json(Array.Empty<object>());
            }

            return Json(new DashboardsDto([], [], [], []));
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton(new AttendeesClient(http));
        Services.AddSingleton(new DashboardsClient(http));
        Services.AddSingleton(new AuditClient(http));
        Services.AddSingleton(new TransitionalLocationTimePresentation("Europe/London"));

        return (Render<Attendees>(), handler);
    }

    private static Queue<IReadOnlyList<AttendeeBookingDto>> Pages(
        params IReadOnlyList<AttendeeBookingDto>[] pages) => new(pages);

    [Fact]
    public void BookingCellReportsNoActiveBookings()
    {
        var (cut, _) = RenderWith(Pages([]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Contains("No active bookings", cut.Markup));
        Assert.Empty(cut.FindAll("ul.booking-list li"));
    }

    [Fact]
    public void BookingCellSummarizesOneActiveBooking()
    {
        var (cut, _) = RenderWith(Pages([Booking(Guid.NewGuid(), true, 10)]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));
        Assert.Contains("1 active booking", cut.Markup);
        Assert.Contains("10 Sep 2026", cut.Markup);
    }

    [Fact]
    public void BookingCellSummarizesTwoActiveBookings()
    {
        var (cut, _) = RenderWith(Pages(
        [
            Booking(Guid.NewGuid(), true, 10),
            Booking(Guid.NewGuid(), false, 12),
        ]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.booking-list li").Count));
        Assert.Contains("2 active bookings", cut.Markup);
    }

    [Fact]
    public void RecoveryRowOmitsCancelAndRebook()
    {
        var (cut, _) = RenderWith(Pages(
        [
            Booking(Guid.NewGuid(), true, 10),
            Booking(Guid.NewGuid(), false, 12),
        ]));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));

        cut.Find("button.booking-badge").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("ul.booking-list li").Count));
        var rows = cut.FindAll("ul.booking-list li");
        Assert.Contains(rows[0].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel & rebook");
        Assert.DoesNotContain(rows[1].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel & rebook");
        Assert.Contains(rows[1].QuerySelectorAll("button"), b => b.TextContent.Trim() == "Cancel booking");
        Assert.Contains("(recovery)", rows[1].TextContent);
    }

    [Fact]
    public void CancelBookingRequiresAConfirmingSecondClick()
    {
        var bookingId = Guid.NewGuid();
        var (cut, handler) = RenderWith(Pages([Booking(bookingId, true, 10)], []));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel booking").Click();

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);

        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() => Assert.Contains("Booking cancelled.", cut.Markup));
        var post = Assert.Single(handler.Requests, r => r.Method == HttpMethod.Post);
        Assert.Equal(
            $"/api/attendees/{AttendeeId}/bookings/{bookingId}/cancel",
            post.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void CancelAndRebookRequiresAConfirmingSecondClick()
    {
        var bookingId = Guid.NewGuid();
        var (cut, handler) = RenderWith(
            Pages([Booking(bookingId, true, 10)], []),
            cancel: _ => Json(new CancelAttendeeBookingDto(true, true, "Sent", Guid.NewGuid())));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel & rebook").Click();

        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
        // Arming rebook must not arm the plain cancel on the same row.
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Cancel booking");

        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Booking cancelled; replacement invite sent.", cut.Markup));
    }

    [Fact]
    public void SuccessMessageReportsAnUndeliveredReplacementInvite()
    {
        var bookingId = Guid.NewGuid();
        var (cut, _) = RenderWith(
            Pages([Booking(bookingId, true, 10)], []),
            cancel: _ => Json(new CancelAttendeeBookingDto(true, true, "Failed", Guid.NewGuid())));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel & rebook").Click();
        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("replacement invite could not be delivered", cut.Markup));
    }

    [Fact]
    public void AFailedCancellationShowsTheSafeMessage()
    {
        var bookingId = Guid.NewGuid();
        var (cut, _) = RenderWith(
            Pages([Booking(bookingId, false, 12)]),
            cancel: _ => new HttpResponseMessage(HttpStatusCode.Conflict));
        cut.WaitForAssertion(() => Assert.Contains("Amara Novak", cut.Markup));
        cut.Find("button.booking-badge").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("ul.booking-list li")));

        FindButton(cut, "Cancel booking").Click();
        cut.WaitForAssertion(() => Assert.Contains("Confirm cancel", cut.Markup));
        FindButton(cut, "Confirm cancel").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("span.booking-error[role=alert]")));
    }
}
`````
