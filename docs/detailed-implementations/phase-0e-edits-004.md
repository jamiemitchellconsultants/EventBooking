# 00e — Require an attendee group, edits 4 (Task 3c)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Mcp/Tools/AttendeeTools.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Mcp/Tools/AttendeeTools.cs","beforeSha":"8489f4e8506d6d99ee2121cc35d300a56cbcb877fb213a338f8442b325142f2d","afterSha":"b4a7a9d68537a8a4aa0c540f43f0946e6007f12541079f586ade75d26557b47d","side":"after","part":1,"parts":1} -->

`````csharp
using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Attendees;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

public sealed record AttendeeToolView
{
    /// <summary>Gets the attendee identifier for follow-up tool calls.</summary>
    public required Guid AttendeeId { get; init; }
    /// <summary>Gets the attendee display name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the attendee email address.</summary>
    public required string Email { get; init; }
    /// <summary>Gets the attendee lifecycle status name.</summary>
    public required string Status { get; init; }
    /// <summary>Gets the assigned Attendee Group name.</summary>
    public required string AttendeeGroupName { get; init; }
    /// <summary>Gets the canonical Attendee Group code.</summary>
    public required string AttendeeGroupCode { get; init; }
    /// <summary>Gets read-only derived Appointment Type summaries.</summary>
    public required IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes { get; init; }
    /// <summary>Gets internal readiness without exposing recovery mutation.</summary>
    public required AttendeeReadiness? Readiness { get; init; }
}

/// <summary>Tool-safe readiness including Coordinator display wording.</summary>
/// <param name="AttendeeId">The attendee the readiness was calculated for.</param>
/// <param name="Code">The readiness code name.</param>
/// <param name="Display">The Coordinator-facing display wording for the code.</param>
/// <param name="OutstandingAppointmentTypes">The appointment types still outstanding.</param>
public sealed record AttendeeReadinessToolView(
    Guid AttendeeId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);

/// <summary>Attendee management, invites, and delivery retry for coordinators.</summary>
[McpServerToolType]
public sealed class AttendeeTools
{
    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 200;

    /// <summary>Lists attendees, optionally filtered by status or search text.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="readiness">Resolves internal readiness per listed attendee.</param>
    /// <param name="status">The attendee status name, or null for all.</param>
    /// <param name="search">Free-text filter, or null.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">Results per page, clamped to the tool maximum.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded page of attendee views.</returns>
    [McpServerTool(Name = "list_attendees", Title = "List attendees", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List attendees, optionally filtered by status name and search text. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<AttendeeToolView>> ListAttendeesAsync(
        ICallerAccessor caller,
        ListAttendeesHandler handler,
        GetAttendeeReadinessHandler readiness,
        [Description("Attendee status name (e.g. Invited) or null for all.")] string? status = null,
        [Description("Free-text name or email filter or null.")] string? search = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Results per page, at most 200.")] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        AttendeeStatus? parsed = null;
        if (status is not null)
        {
            if (!Enum.TryParse<AttendeeStatus>(status, ignoreCase: false, out var value) ||
                !Enum.IsDefined(value))
            {
                throw new ModelContextProtocol.McpException(
                    "Status must be a recognised AttendeeStatus name.");
            }

            parsed = value;
        }

        var staffUserId = caller.RequireStaffUserId();
        var result = await handler.HandleAsync(
            new ListAttendeesQuery(staffUserId, parsed, search),
            cancellationToken);
        var bounded = Math.Clamp(pageSize, 1, MaxPageSize);
        var skipped = Math.Max(page - 1, 0) * bounded;

        var views = new List<AttendeeToolView>();
        foreach (var item in result.ValueOrThrow().Skip(skipped).Take(bounded))
        {
            var readinessResult = await readiness.HandleAsync(
                new GetAttendeeReadinessQuery(staffUserId, item.AttendeeId),
                cancellationToken);
            views.Add(new AttendeeToolView
            {
                AttendeeId = item.AttendeeId,
                Name = item.Name,
                Email = item.Email,
                Status = item.Status.ToString(),
                AttendeeGroupName = item.AttendeeGroupName,
                AttendeeGroupCode = item.AttendeeGroupCode,
                RequiredAppointmentTypes = item.RequiredAppointmentTypes,
                Readiness = readinessResult.IsSuccess ? readinessResult.Value : null,
            });
        }

        return views;
    }

    /// <summary>Lists the Attendee Groups available for attendee assignment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    [McpServerTool(Name = "list_attendee_groups", Title = "List attendee groups", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List the attendee groups that determine attendee requirements. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<AttendeeGroupListItem>> ListAttendeeGroupsAsync(
        ICallerAccessor caller,
        ListAttendeeGroupsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListAttendeeGroupsQuery(caller.RequireStaffUserId()),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates one attendee in an Attendee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="name">The attendee name.</param>
    /// <param name="email">The attendee email.</param>
    /// <param name="attendeeGroupCode">The canonical Attendee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new attendee identifier.</returns>
    [McpServerTool(Name = "create_attendee", Title = "Create attendee", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Create a attendee in one attendee group; requirements derive from the group. Caller must be a coordinator or admin; creates a new attendee record.")]
    public async Task<Guid> CreateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        IAttendeeGroupRepository groups,
        [Description("Attendee full name.")] string name,
        [Description("Attendee email address.")] string email,
        [Description("Canonical attendee group code (e.g. PILOTS).")] string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, attendeeGroupCode, cancellationToken);
        var result = await handler.CreateAsync(
            new CreateAttendeeCommand(caller.RequireStaffUserId(), name, email, groupId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates a attendee's name, email, or Attendee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Attendee Group.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="name">The corrected name.</param>
    /// <param name="email">The corrected email.</param>
    /// <param name="attendeeGroupCode">The canonical Attendee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "update_attendee", Title = "Update attendee", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Update a attendee. Caller must be a coordinator or admin; requirements are frozen while an active booking exists.")]
    public async Task<string> UpdateAttendeeAsync(
        ICallerAccessor caller,
        SaveAttendeeHandler handler,
        IAttendeeGroupRepository groups,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Corrected full name.")] string name,
        [Description("Corrected email address.")] string email,
        [Description("Canonical attendee group code (e.g. PILOTS).")] string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, attendeeGroupCode, cancellationToken);
        var result = await handler.UpdateAsync(
            new UpdateAttendeeCommand(
                caller.RequireStaffUserId(), attendeeId, name, email, groupId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee updated.";
    }

    /// <summary>Deletes a attendee, optionally cascading.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The delete handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="confirm">Whether dependent data may be removed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "delete_attendee", Title = "Delete attendee", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Delete a attendee. Caller must be a coordinator or admin; set confirm to true to also remove dependent data.")]
    public async Task<string> DeleteAttendeeAsync(
        ICallerAccessor caller,
        DeleteAttendeeHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("Whether dependent data may be removed.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new DeleteAttendeeCommand(caller.RequireStaffUserId(), attendeeId, confirm),
            cancellationToken);
        result.ThrowIfFailure();
        return "Attendee deleted.";
    }

    /// <summary>Imports attendees from CSV content.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_attendees", Title = "Import attendees", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Import attendees from CSV content with name, email, and attendee group code. Caller must be a coordinator or admin; creates new attendee records.")]
    public async Task<AttendeeImportOutcome> ImportAttendeesAsync(
        ICallerAccessor caller,
        ImportAttendeesHandler handler,
        [Description("CSV content with one attendee per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportAttendeesCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Resolves a canonical Attendee Group code to its stable identifier.</summary>
    private static async Task<Guid> ResolveGroupIdAsync(
        IAttendeeGroupRepository groups,
        string attendeeGroupCode,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetByCodeAsync(attendeeGroupCode, cancellationToken);
        if (group is null)
        {
            throw new ModelContextProtocol.McpException(
                $"Employee group '{attendeeGroupCode}' is not known. Use list_attendee_groups.");
        }

        return group.Id;
    }

    /// <summary>Triggers a fresh invite for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The invite handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invite issue result.</returns>
    [McpServerTool(Name = "trigger_invite", Title = "Trigger invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Issue a fresh three-option invite to a attendee. Caller must be a coordinator or admin; sends an invite email.")]
    public async Task<InviteIssueResult> TriggerInviteAsync(
        ICallerAccessor caller,
        TriggerInviteHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TriggerInviteCommand(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Retries the latest unresolved email for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The retry handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retry outcome.</returns>
    [McpServerTool(Name = "retry_attendee_email", Title = "Retry attendee email", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Retry the latest unresolved attendee email delivery. Caller must be a coordinator or admin; resends the latest unresolved email.")]
    public async Task<RetryEmailOutcome> RetryAttendeeEmailAsync(
        ICallerAccessor caller,
        RetryEmailHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RetryEmailCommand(caller.RequireStaffUserId(), attendeeId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Starts a recovery invite for a attendee with a missed appointment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery invite issue result.</returns>
    [McpServerTool(Name = "start_recovery_invite", Title = "Start recovery invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Start a recovery invite for a attendee with a missed appointment. Caller must have ManageAttendees; sends a recovery invite email.")]
    public async Task<StartRecoveryResult> StartRecoveryInviteAsync(
        ICallerAccessor caller,
        StartRecoveryHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new StartRecoveryCommand(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a pending recovery invite for a attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery cancellation handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="inviteId">The recovery invite identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "cancel_recovery_invite", Title = "Cancel recovery invite", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a pending recovery invite for a attendee. Caller must have ManageAttendees; cancels the pending recovery invite.")]
    public async Task<string> CancelRecoveryInviteAsync(
        ICallerAccessor caller,
        CancelRecoveryInviteHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The recovery invite identifier.")] Guid inviteId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), attendeeId, inviteId), cancellationToken);
        result.ThrowIfFailure();
        return "Recovery invite cancelled.";
    }

    /// <summary>Lists the active bookings a coordinator may cancel for one attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The bookings handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The attendee's active booking summaries.</returns>
    [McpServerTool(Name = "list_attendee_bookings", Title = "List attendee bookings", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List a attendee's active bookings. Caller must have ManageAttendees; returns cancellable bookings without management tokens.")]
    public async Task<IReadOnlyList<AttendeeBookingSummary>> ListAttendeeBookingsAsync(
        ICallerAccessor caller,
        GetAttendeeBookingsHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeBookingsQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels one attendee booking, optionally rebooking the attendee.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_attendee_booking", Title = "Cancel attendee booking", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel one attendee booking, optionally rebooking. Caller must have ManageAttendees; cancels the booking and optionally sends a replacement invite.")]
    public async Task<CancelBookingOutcome> CancelAttendeeBookingAsync(
        ICallerAccessor caller,
        CancelAttendeeBookingHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        [Description("The booking identifier.")] Guid bookingId,
        [Description("Whether to issue a replacement invite.")] bool rebook,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelAttendeeBookingCommand(caller.RequireStaffUserId(), attendeeId, bookingId, rebook), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Gets tool-safe readiness including Coordinator display wording.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The readiness handler.</param>
    /// <param name="attendeeId">The attendee identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool-safe readiness view.</returns>
    [McpServerTool(Name = "get_attendee_readiness", Title = "Get attendee readiness", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Get a attendee's readiness with Coordinator display wording. Caller must have ManageAttendees; returns internal readiness without recovery mutation.")]
    public async Task<AttendeeReadinessToolView> GetAttendeeReadinessAsync(
        ICallerAccessor caller,
        GetAttendeeReadinessHandler handler,
        [Description("The attendee identifier.")] Guid attendeeId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAttendeeReadinessQuery(caller.RequireStaffUserId(), attendeeId), cancellationToken);
        var value = result.ValueOrThrow();
        return new AttendeeReadinessToolView(
            value.AttendeeId,
            value.Code.ToString(),
            AttendeeEndpoints.DisplayForTool(value.Code),
            value.OutstandingAppointmentTypes);
    }
}
`````

## before — src/EventBooking.Web/Pages/Attendees.razor — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Web/Pages/Attendees.razor","beforeSha":"1c194da4c4afa4869e218c33d58934df7535776a54441904740d6662d24ca4fb","afterSha":"0d3a171628204a410527dfe7872dad20d56d7d4a285185c474de8e7489f88344","side":"before","part":1,"parts":1} -->

`````text
@page "/attendees"
@attribute [Microsoft.AspNetCore.Authorization.Authorize]
@using EventBooking.Web.Services
@inject AttendeesClient AttendeesApi
@inject DashboardsClient Dashboards
@inject TransitionalLocationTimePresentation TimePresentation

<PageTitle>Attendees</PageTitle>

<section class="page attendees-page" aria-labelledby="attendees-heading">
    <div class="page-header">
        <div>
            <span class="eyebrow">Coordination</span>
            <h1 id="attendees-heading">Attendees</h1>
            <p>Manage people and the attendee groups that set their appointments.</p>
        </div>
        <div class="attendees-toolbar" aria-label="Attendee list controls">
            <label class="field-label">
                Status
                <select @bind="_statusFilter" @bind:after="ReloadAsync"
                        title="Filters the list to one point in the invitation journey.">
                    <option value="">Any status</option>
                    @foreach (var (value, label) in StatusChoices)
                    {
                        <option value="@value">@label</option>
                    }
                </select>
            </label>
            <label class="search-field" for="attendee-search">
                <span class="visually-hidden">Search attendees</span>
                <input id="attendee-search" @bind="_search" @bind:eventItem="oninput"
                       placeholder="Search by name or email…" />
            </label>
            <button class="button button-quiet button-icon-search" @onclick="ReloadAsync" disabled="@(_busy || _isLoading)"
                    title="Applies the status filter and search term.">
                Search
            </button>
            <label class="button button-quiet upload-button" for="attendee-csv"
                   title="The header line must read exactly: name,email,attendee_group. The whole file is accepted or rejected.">
                Upload CSV
            </label>
            <InputFile id="attendee-csv" class="visually-hidden" OnChange="OnFileChosenAsync" accept=".csv,text/csv" disabled="@_busy" />
        </div>
    </div>

    @if (_error is not null)
    {
        <div class="banner @(_deleteAwaitingConfirmation.HasValue ? "warning" : "error")" role="alert">
            @_error
        </div>
    }

    @if (_importErrors.Count > 0)
    {
        <div class="banner error" role="alert">
            <strong>Nothing was imported.</strong> Fix the file and try again.
            <ul class="import-errors">
                @foreach (var error in _importErrors)
                {
                    <li><strong>Line @error.LineNumber:</strong> @error.Message</li>
                }
            </ul>
        </div>
    }

    @if (_isLoading)
    {
        <div class="card loading-block" role="status">
            <span class="loading-line loading-line-short"></span>
            <span class="loading-line"></span>
            <span class="loading-line loading-line-medium"></span>
            <span class="visually-hidden">Loading attendees…</span>
        </div>
    }
    else if (_attendees is null)
    {
        <div class="card">
            <div class="empty-state">
                <strong>Couldn’t load attendees.</strong>
                <p>The attendee list could not be fetched. Try again to reload it.</p>
                <button class="button" @onclick="ReloadAsync">Try again</button>
            </div>
        </div>
    }
    else
    {
        <div class="card">
            <div class="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th scope="col">Name</th>
                            <th scope="col">Email</th>
                            <th scope="col" title="The appointment types the attendee's attendee group requires.">Required types</th>
                            <th scope="col" title="Where the attendee has reached in the invitation journey.">Status</th>
                            <th scope="col" title="Whether the attendee still has appointments outstanding before they can start.">Readiness</th>
                            <th scope="col" title="The last invitation or confirmation email sent, and whether it was delivered.">Delivery</th>
                            <th scope="col" title="The attendee's active bookings, and the actions that cancel them.">Booking</th>
                            <th scope="col" title="Every recorded change for this attendee, with who made it.">History</th>
                            <th scope="col" class="actions-column">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr class="attendee-add-row">
                            <td data-label="Name">
                                <label class="visually-hidden" for="new-attendee-name">Full name</label>
                                <input id="new-attendee-name" @bind="_newName" placeholder="Full name" />
                            </td>
                            <td data-label="Email">
                                <label class="visually-hidden" for="new-attendee-email">Email address</label>
                                <input id="new-attendee-email" type="email" @bind="_newEmail" placeholder="name@example.com" />
                            </td>
                            <td data-label="Required types">
                                <label class="visually-hidden" for="new-attendee-group">Employee group</label>
                                <select id="new-attendee-group" class="group-select" @bind="_newAttendeeGroupId" disabled="@_busy" required
                                        title="The attendee group decides which appointment types this attendee must attend.">
                                    <option value="">Select an attendee group</option>
                                    @foreach (var group in _groups)
                                    {
                                        <option value="@group.AttendeeGroupId">@group.Name</option>
                                    }
                                </select>
                                <div class="chip-row group-preview" aria-live="polite">
                                    @foreach (var type in SelectedGroupTypes(_newAttendeeGroupId))
                                    {
                                        <span class="chip">@type.Code</span>
                                    }
                                </div>
                            </td>
                            <td data-label="Status"><span class="attendee-status status-new">New attendee</span></td>
                            <td data-label="Readiness"><span>—</span></td>
                            <td data-label="Delivery"><span>—</span></td>
                            <td data-label="Booking"><span>—</span></td>
                            <td data-label="History"><span>—</span></td>
                            <td data-label="Actions">
                                <button class="button button-primary button-small" @onclick="CreateAsync" disabled="@_busy"
                                        title="Adds the attendee without inviting them yet.">
                                    Save attendee
                                </button>
                            </td>
                        </tr>

                        @foreach (var attendee in _attendees)
                        {
                            var editing = _editingId == attendee.AttendeeId;
                            var awaitingDeleteConfirmation = _deleteAwaitingConfirmation == attendee.AttendeeId;
                            <tr @key="attendee.AttendeeId" class="@(editing ? "attendee-edit-row" : awaitingDeleteConfirmation ? "awaiting-confirmation" : null)">
                                @if (editing)
                                {
                                    <td data-label="Name">
                                        <label class="visually-hidden" for="edit-attendee-name">Full name</label>
                                        <input id="edit-attendee-name" @bind="_editName" />
                                    </td>
                                    <td data-label="Email">
                                        <label class="visually-hidden" for="edit-attendee-email">Email address</label>
                                        <input id="edit-attendee-email" type="email" @bind="_editEmail" />
                                    </td>
                                    <td data-label="Required types">
                                        <label class="visually-hidden" for="edit-attendee-group">Employee group</label>
                                        <select id="edit-attendee-group" class="group-select" @bind="_editAttendeeGroupId" disabled="@_busy" required
                                            title="Changing the group changes the appointment types this attendee must attend.">
                                            <option value="">Select an attendee group</option>
                                            @foreach (var group in _groups)
                                            {
                                                <option value="@group.AttendeeGroupId">@group.Name</option>
                                            }
                                        </select>
                                        <div class="chip-row group-preview" aria-live="polite">
                                            @foreach (var type in SelectedGroupTypes(_editAttendeeGroupId))
                                            {
                                                <span class="chip">@type.Code</span>
                                            }
                                        </div>
                                    </td>
                                    <td data-label="Status"><span class="attendee-status @AttendeePresentation.StatusCssClass(attendee.Status)">@attendee.StatusDisplay</span></td>
                                    <td data-label="Readiness"><span>—</span></td>
                                    <td data-label="Delivery">
                                        @if (_emailStatus.TryGetValue(attendee.AttendeeId, out var email))
                                        {
                                            <span class="@(email.Status == "Failed" ? "error" : email.Status == "Pending" ? "warning" : "")">
                                                @email.TemplateDisplay @TimePresentation.Format(email.SentAt)
                                            </span>
                                            @if (email.Status is "Failed" or "Pending" && email.CanRetry)
                                            {
                                                <button class="button button-small" @onclick="() => RetryEmailAsync(attendee.AttendeeId)" disabled="@_busy"
                                                        title="Sends the same email again to the same address.">Resend</button>
                                            }
                                        }
                                        else
                                        {
                                            <span>—</span>
                                        }
                                    </td>
                                    <td data-label="Booking"><span>—</span></td>
                                    <td data-label="History"><AuditHistory AttendeeId="@attendee.AttendeeId" /></td>
                                    <td data-label="Actions">
                                        <div class="row-actions">
                                            <button class="button button-primary button-small" @onclick="SaveEditAsync" disabled="@_busy"
                                                    title="Saves the edited details. Existing invitations are not resent.">Save</button>
                                            <button class="button button-quiet button-small" @onclick="CancelEdit" disabled="@_busy">Cancel</button>
                                        </div>
                                    </td>
                                }
                                else
                                {
                                    <td data-label="Name">@attendee.Name</td>
                                    <td data-label="Email"><a href="mailto:@attendee.Email">@attendee.Email</a></td>
                                    <td data-label="Required types">
                                        @if (attendee.RequiresAttendeeGroupReconciliation)
                                        {
                                            <span class="attendee-status status-warning">Attendee Group required</span>
                                        }
                                        else
                                        {
                                            <div class="chip-row">
                                                @foreach (var requiredType in attendee.RequiredAppointmentTypes)
                                                {
                                                    <span class="chip">@requiredType.Code</span>
                                                }
                                            </div>
                                        }
                                    </td>
                                    <td data-label="Status"><span class="attendee-status @AttendeePresentation.StatusCssClass(attendee.Status)">@attendee.StatusDisplay</span></td>
                                    <td data-label="Readiness">
                                        @if (_readiness.TryGetValue(attendee.AttendeeId, out var readiness))
                                        {
                                            var expanded = _expandedReadiness.Contains(attendee.AttendeeId);
                                            <button class="button button-small readiness-badge @AttendeePresentation.ReadinessCssClass(readiness.Code)"
                                                    @onclick="() => ToggleReadinessAsync(attendee.AttendeeId)"
                                                    aria-expanded="@(expanded ? "true" : "false")"
                                                    aria-controls="@(expanded ? $"readiness-{attendee.AttendeeId}" : null)">
                                                <span aria-hidden="true">@AttendeePresentation.ReadinessIcon(readiness.Code)</span>
                                                @readiness.Display
                                            </button>
                                            @if (expanded)
                                            {
                                                <div class="readiness-detail" id="readiness-@attendee.AttendeeId">
                                                    @if (readiness.OutstandingAppointmentTypes.Count == 0)
                                                    {
                                                        <span>@readiness.Display</span>
                                                    }
                                                    else
                                                    {
                                                        <span>@readiness.Display:</span>
                                                        <ul class="readiness-types">
                                                            @foreach (var type in readiness.OutstandingAppointmentTypes)
                                                            {
                                                                <li>@type.Name@(type.IsRecoverable ? " (recoverable)" : null)</li>
                                                            }
                                                        </ul>
                                                    }
                                                    @if (readiness.OutstandingAppointmentTypes.Any(type => type.IsRecoverable)
                                                        && !_pendingRecoveryInvites.ContainsKey(attendee.AttendeeId))
                                                    {
                                                        <button class="button button-small" @onclick="() => StartRecoveryAsync(attendee.AttendeeId, RecoverableTypeNames(readiness))" disabled="@_busy"
                                                                title="Emails a fresh booking link covering only the appointments the attendee still owes.">Arrange missed appointments</button>
                                                    }
                                                    @if (_pendingRecoveryInvites.ContainsKey(attendee.AttendeeId))
                                                    {
                                                        <button class="button button-small button-danger" @onclick="() => CancelRecoveryAsync(attendee.AttendeeId)" disabled="@_busy">
                                                            @(_recoveryCancelAwaitingConfirmation == attendee.AttendeeId ? "Confirm cancel" : "Cancel recovery")
                                                        </button>
                                                    }
                                                    @if (_recoveryOutcomes.TryGetValue(attendee.AttendeeId, out var recoveryOutcome))
                                                    {
                                                        <span class="recovery-outcome" role="status">@recoveryOutcome</span>
                                                    }
                                                    @if (_recoveryErrors.TryGetValue(attendee.AttendeeId, out var recoveryError))
                                                    {
                                                        <span class="recovery-error" role="alert">@recoveryError</span>
                                                    }
                                                </div>
                                            }
                                        }
                                        else if (_readinessLoading.Contains(attendee.AttendeeId))
                                        {
                                            <span class="readiness-loading" role="status" aria-live="polite">Loading readiness…</span>
                                        }
                                        else if (_readinessErrors.TryGetValue(attendee.AttendeeId, out var readinessError))
                                        {
                                            <span class="readiness-error" role="alert">@readinessError</span>
                                            <button class="button button-small" @onclick="() => LoadReadinessAsync(attendee.AttendeeId)" disabled="@_busy">Try again</button>
                                        }
                                        else
                                        {
                                            <button class="button button-quiet button-small readiness-badge status-neutral"
                                                    @onclick="() => ToggleReadinessAsync(attendee.AttendeeId)"
                                                    title="Checks which appointments this attendee still owes before they can start."
                                                    aria-expanded="false">
                                                <span aria-hidden="true">?</span> Check readiness
                                            </button>
                                        }
                                    </td>
                                    <td data-label="Delivery">
                                        @if (_emailStatus.TryGetValue(attendee.AttendeeId, out var email))
                                        {
                                            <span class="@(email.Status == "Failed" ? "error" : email.Status == "Pending" ? "warning" : "")">
                                                @email.TemplateDisplay @TimePresentation.Format(email.SentAt)
                                            </span>
                                            @if (email.Status is "Failed" or "Pending" && email.CanRetry)
                                            {
                                                <button class="button button-small" @onclick="() => RetryEmailAsync(attendee.AttendeeId)" disabled="@_busy"
                                                        title="Sends the same email again to the same address.">Resend</button>
                                            }
                                        }
                                        else
                                        {
                                            <span>—</span>
                                        }
                                    </td>
                                    <td data-label="Booking">
                                        @{
                                            var bookingsExpanded = _expandedBookings.Contains(attendee.AttendeeId);
                                            var attendeeBookings = _bookings.TryGetValue(attendee.AttendeeId, out var loaded) ? loaded : null;
                                        }
                                        <button class="button button-quiet button-small booking-badge"
                                                @onclick="() => ToggleBookingsAsync(attendee.AttendeeId)"
                                                aria-expanded="@(bookingsExpanded ? "true" : "false")"
                                                aria-controls="@(bookingsExpanded ? $"bookings-{attendee.AttendeeId}" : null)"
                                                title="Shows the attendee's active bookings and the actions that cancel them.">
                                            @BookingSummary(attendeeBookings)
                                        </button>
                                        @if (bookingsExpanded)
                                        {
                                            <div class="booking-detail" id="bookings-@attendee.AttendeeId">
                                                @if (_bookingsLoading.Contains(attendee.AttendeeId))
                                                {
                                                    <span class="booking-loading" role="status" aria-live="polite">Loading bookings…</span>
                                                }
                                                else if (attendeeBookings is { Count: 0 })
                                                {
                                                    <span>No active bookings.</span>
                                                }
                                                else if (attendeeBookings is not null)
                                                {
                                                    <ul class="booking-list">
                                                        @foreach (var booking in attendeeBookings)
                                                        {
                                                            <li>
                                                                <span class="booking-window">
                                                                    @booking.EventDate.ToString("dd MMM yyyy")
                                                                    @booking.EventStartTime.ToString("HH\\:mm")–@booking.EventEndTime.ToString("HH\\:mm")
                                                                    @(booking.IsOriginal ? null : " (recovery)")
                                                                </span>
                                                                <button class="button button-danger button-small"
                                                                        @onclick="() => CancelBookingAsync(attendee.AttendeeId, booking.BookingId, false)"
                                                                        disabled="@_busy"
                                                                        title="Cancels this booking and releases its places.">
                                                                    @(_bookingCancelAwaitingConfirmation == booking.BookingId ? "Confirm cancel" : "Cancel booking")
                                                                </button>
                                                                @if (booking.IsOriginal)
                                                                {
                                                                    <button class="button button-small"
                                                                            @onclick="() => CancelBookingAsync(attendee.AttendeeId, booking.BookingId, true)"
                                                                            disabled="@_busy"
                                                                            title="Cancels this booking and emails a fresh booking link.">
                                                                        @(_bookingRebookAwaitingConfirmation == booking.BookingId ? "Confirm cancel" : "Cancel & rebook")
                                                                    </button>
                                                                }
                                                            </li>
                                                        }
                                                    </ul>
                                                }

                                                @if (_bookingOutcomes.TryGetValue(attendee.AttendeeId, out var bookingOutcome))
                                                {
                                                    <span class="booking-outcome" role="status">@bookingOutcome</span>
                                                }
                                                @if (_bookingErrors.TryGetValue(attendee.AttendeeId, out var bookingError))
                                                {
                                                    <span class="booking-error" role="alert">@bookingError</span>
                                                }
                                            </div>
                                        }
                                    </td>
                                    <td data-label="History"><AuditHistory AttendeeId="@attendee.AttendeeId" /></td>
                                    <td data-label="Actions">
                                        <div class="row-actions">
                                            <button class="button button-small button-icon-edit" @onclick="() => StartEdit(attendee)" disabled="@(_busy || _editingId.HasValue)"
                                                    title="Change the name, email address or attendee group.">Edit</button>
                                            <button class="button button-primary button-small" @onclick="() => InviteAsync(attendee)" disabled="@(_busy || _editingId.HasValue)"
                                                    aria-disabled="@(attendee.RequiresAttendeeGroupReconciliation ? "true" : null)"
                                                    title="@(attendee.RequiresAttendeeGroupReconciliation ? "Assign an attendee group before inviting" : "Emails a single-use booking link showing only the windows this attendee can take.")">Invite now</button>
                                            <button class="button button-danger button-small button-icon-delete" @onclick="() => DeleteAsync(attendee.AttendeeId)" disabled="@(_busy || _editingId.HasValue)"
                                                    title="Removes the attendee. Press a second time to confirm.">
                                                @(awaitingDeleteConfirmation ? "Confirm delete" : "Delete")
                                            </button>
                                        </div>
                                        @if (attendee.RequiresAttendeeGroupReconciliation)
                                        {
                                            <span class="invite-help">Needs an attendee group first.</span>
                                        }
                                    </td>
                                }
                            </tr>
                        }
                    </tbody>
                </table>
            </div>

            @if (_attendees.Count == 0)
            {
                <div class="empty-state">
                    <strong>No attendees yet</strong>
                    <p>Add one above, or upload a CSV to bring in several at once.</p>
                </div>
            }
        </div>
    }
</section>

@code {
    private const int MaximumUploadBytes = 1024 * 1024;
    private const string UnexpectedError = "Something went wrong. Please try again.";

    private List<AttendeeDto>? _attendees;
    private List<AttendeeGroupOptionDto> _groups = [];
    private List<ImportErrorDto> _importErrors = [];
    private string? _error;
    private bool _busy;
    private bool _isLoading = true;

    private string _search = string.Empty;
    private int? _statusFilter;
    private IReadOnlyDictionary<Guid, AttendeeEmailStatusDto> _emailStatus =
        new Dictionary<Guid, AttendeeEmailStatusDto>();
    private readonly Dictionary<Guid, AttendeeReadinessDto> _readiness = new();
    private readonly HashSet<Guid> _expandedReadiness = new();
    private readonly HashSet<Guid> _readinessLoading = new();
    private readonly Dictionary<Guid, string> _readinessErrors = new();
    private readonly Dictionary<Guid, Guid> _pendingRecoveryInvites = new();
    private readonly Dictionary<Guid, string> _recoveryOutcomes = new();
    private readonly Dictionary<Guid, string> _recoveryErrors = new();
    private Guid? _recoveryCancelAwaitingConfirmation;
    private readonly Dictionary<Guid, List<AttendeeBookingDto>> _bookings = new();
    private readonly HashSet<Guid> _expandedBookings = new();
    private readonly HashSet<Guid> _bookingsLoading = new();
    private readonly Dictionary<Guid, string> _bookingOutcomes = new();
    private readonly Dictionary<Guid, string> _bookingErrors = new();
    private Guid? _bookingCancelAwaitingConfirmation;
    private Guid? _bookingRebookAwaitingConfirmation;

    private static readonly (int Value, string Label)[] StatusChoices =
    [
        (1, "Not yet invited"),
        (2, "Awaiting availability"),
        (3, "Invited (pending response)"),
        (4, "Booked"),
        (5, "No response - needs follow-up"),
    ];
    private string _newName = string.Empty;
    private string _newEmail = string.Empty;
    private string _newAttendeeGroupId = string.Empty;
    private Guid? _deleteAwaitingConfirmation;
    private Guid? _editingId;
    private string _editName = string.Empty;
    private string _editEmail = string.Empty;
    private string _editAttendeeGroupId = string.Empty;

    // Visible to the component's test assembly so its busy-event guard can be exercised directly.
    internal bool IsBusyForTesting
    {
        get => _busy;
        set => _busy = value;
    }

    internal string? ErrorForTesting => _error;

    internal bool IsReadinessExpandedForTesting(Guid attendeeId) =>
        _expandedReadiness.Contains(attendeeId);

    internal AttendeeReadinessDto? ReadinessForTesting(Guid attendeeId) =>
        _readiness.TryGetValue(attendeeId, out var readiness) ? readiness : null;

    internal string? ReadinessErrorForTesting(Guid attendeeId) =>
        _readinessErrors.TryGetValue(attendeeId, out var error) ? error : null;

    internal Task ToggleReadinessForTestingAsync(Guid attendeeId) =>
        ToggleReadinessAsync(attendeeId);

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task ToggleReadinessAsync(Guid attendeeId)
    {
        if (_busy)
        {
            return;
        }

        if (_expandedReadiness.Contains(attendeeId))
        {
            _expandedReadiness.Remove(attendeeId);
            return;
        }

        _expandedReadiness.Add(attendeeId);
        if (!_readiness.ContainsKey(attendeeId))
        {
            await LoadReadinessAsync(attendeeId);
        }
    }

    private async Task LoadReadinessAsync(Guid attendeeId)
    {
        if (_busy || _readinessLoading.Contains(attendeeId))
        {
            return;
        }

        _readinessLoading.Add(attendeeId);
        _readinessErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.GetReadinessAsync(attendeeId, CancellationToken.None);
            if (outcome.IsSuccess && outcome.Value is not null)
            {
                _readiness[attendeeId] = outcome.Value;
            }
            else
            {
                _readinessErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
            }
        }
        catch (Exception)
        {
            _readinessErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _readinessLoading.Remove(attendeeId);
        }
    }

    private async Task ReloadAsync()
    {
        _isLoading = true;

        try
        {
            var attendeesTask = AttendeesApi.ListAsync(_statusFilter, _search, CancellationToken.None);
            var dashboardsTask = Dashboards.GetAsync(CancellationToken.None);
            var groupsTask = AttendeesApi.ListGroupsAsync(CancellationToken.None);
            await Task.WhenAll(attendeesTask, dashboardsTask, groupsTask);

            var outcome = attendeesTask.Result;
            if (outcome.IsSuccess)
            {
                _attendees = outcome.Value;
                _error = null;
            }
            else
            {
                _error = outcome.ErrorMessage ?? UnexpectedError;
            }

            var groupsOutcome = groupsTask.Result;
            if (groupsOutcome.IsSuccess)
            {
                _groups = groupsOutcome.Value ?? [];
            }
            else if (_error is null)
            {
                _error = groupsOutcome.ErrorMessage ?? UnexpectedError;
            }

            _emailStatus = dashboardsTask.Result.Value?.EmailStatuses
                .ToDictionary(e => e.AttendeeId)
                ?? new Dictionary<Guid, AttendeeEmailStatusDto>();
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private IReadOnlyList<AppointmentTypeSummaryDto> SelectedGroupTypes(string groupIdValue) =>
        Guid.TryParse(groupIdValue, out var groupId)
            ? _groups.FirstOrDefault(group => group.AttendeeGroupId == groupId)?.RequiredAppointmentTypes
                ?? []
            : [];

    private static Guid? ParseAttendeeGroupId(string value) =>
        Guid.TryParse(value, out var groupId) ? groupId : null;

    private async Task CreateAsync()
    {
        await RunAsync(
            () => AttendeesApi.CreateAsync(
                _newName, _newEmail, ParseAttendeeGroupId(_newAttendeeGroupId), CancellationToken.None),
            () =>
            {
                _newName = string.Empty;
                _newEmail = string.Empty;
                _newAttendeeGroupId = string.Empty;
                return Task.CompletedTask;
            });
    }

    private void StartEdit(AttendeeDto attendee)
    {
        _editingId = attendee.AttendeeId;
        _editName = attendee.Name;
        _editEmail = attendee.Email;
        _editAttendeeGroupId = attendee.AttendeeGroupId?.ToString() ?? string.Empty;
    }

    private void CancelEdit() => _editingId = null;

    private Task SaveEditAsync()
    {
        var id = _editingId!.Value;
        return RunAsync(
            () => AttendeesApi.UpdateAsync(
                id, _editName, _editEmail, ParseAttendeeGroupId(_editAttendeeGroupId), CancellationToken.None),
            () =>
            {
                _editingId = null;
                return Task.CompletedTask;
            });
    }

    private async Task DeleteAsync(Guid id)
    {
        var confirm = _deleteAwaitingConfirmation == id;
        _busy = true;

        try
        {
            var outcome = await AttendeesApi.DeleteAsync(id, confirm, CancellationToken.None);
            if (!outcome.IsSuccess && outcome.StatusCode == 409 && !confirm)
            {
                _deleteAwaitingConfirmation = id;
                _error = $"{outcome.ErrorMessage ?? UnexpectedError} Click Delete again to confirm.";
                return;
            }

            _deleteAwaitingConfirmation = null;
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;
            if (outcome.IsSuccess)
            {
                await ReloadAsync();
            }
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }
    }

    private Task InviteAsync(AttendeeDto attendee)
    {
        if (attendee.RequiresAttendeeGroupReconciliation)
        {
            _error = "Assign an attendee group before inviting.";
            return Task.CompletedTask;
        }

        return RunAsync(() => AttendeesApi.TriggerInviteAsync(attendee.AttendeeId, CancellationToken.None));
    }

    private Task RetryEmailAsync(Guid id) =>
        RunAsync(() => AttendeesApi.RetryEmailAsync(id, CancellationToken.None));

    private static List<string> RecoverableTypeNames(AttendeeReadinessDto readiness) =>
        readiness.OutstandingAppointmentTypes
            .Where(type => type.IsRecoverable)
            .Select(type => type.Name)
            .ToList();

    private static string DescribeRecoveryScope(IReadOnlyList<string> names, int selectedCount)
    {
        var count = Math.Max(selectedCount, names.Count);
        var noun = count == 1 ? "missed appointment" : "missed appointments";
        return names.Count == 0
            ? $"{count} {noun}"
            : $"{count} {noun}: {string.Join(", ", names)}";
    }

    private async Task StartRecoveryAsync(Guid attendeeId, List<string> recoverableNames)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _recoveryErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.StartRecoveryAsync(attendeeId, CancellationToken.None);
            if (!outcome.IsSuccess || outcome.Value is null)
            {
                _recoveryErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            var result = outcome.Value;
            var scope = DescribeRecoveryScope(recoverableNames, result.AppointmentTypeIds.Count);
            if (result.InviteId == Guid.Empty)
            {
                _recoveryOutcomes[attendeeId] = $"No appointments are available yet for {scope}.";
                return;
            }

            _pendingRecoveryInvites[attendeeId] = result.InviteId;
            _recoveryOutcomes[attendeeId] = result.EmailSent
                ? $"Recovery started for {scope}. Email sent."
                : $"Recovery started for {scope}, but the email could not be sent.";
        }
        catch (Exception)
        {
            _recoveryErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }

        await LoadReadinessAsync(attendeeId);
    }

    /// <summary>Collapsed label: nothing to act on reads as a dash, otherwise the active count.</summary>
    private static string BookingSummary(List<AttendeeBookingDto>? bookings) => bookings switch
    {
        null => "Bookings",
        { Count: 0 } => "No active booking",
        { Count: 1 } => "1 active booking",
        _ => $"{bookings.Count} active bookings",
    };

    private async Task ToggleBookingsAsync(Guid attendeeId)
    {
        if (_busy)
        {
            return;
        }

        if (_expandedBookings.Contains(attendeeId))
        {
            _expandedBookings.Remove(attendeeId);
            return;
        }

        _expandedBookings.Add(attendeeId);
        if (!_bookings.ContainsKey(attendeeId))
        {
            await LoadBookingsAsync(attendeeId);
        }
    }

    private async Task LoadBookingsAsync(Guid attendeeId)
    {
        if (_bookingsLoading.Contains(attendeeId))
        {
            return;
        }

        _bookingsLoading.Add(attendeeId);
        _bookingErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.GetBookingsAsync(attendeeId, CancellationToken.None);
            if (outcome is { IsSuccess: true, Value: not null })
            {
                _bookings[attendeeId] = outcome.Value;
            }
            else
            {
                _bookingErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
            }
        }
        catch (Exception)
        {
            _bookingErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _bookingsLoading.Remove(attendeeId);
            StateHasChanged();
        }
    }

    // Each action confirms on its own second click, and the two actions never share a pending
    // confirmation: arming one disarms the other.
    private async Task CancelBookingAsync(Guid attendeeId, Guid bookingId, bool rebook)
    {
        if (_busy)
        {
            return;
        }

        var armed = rebook
            ? _bookingRebookAwaitingConfirmation == bookingId
            : _bookingCancelAwaitingConfirmation == bookingId;
        if (!armed)
        {
            _bookingCancelAwaitingConfirmation = rebook ? null : bookingId;
            _bookingRebookAwaitingConfirmation = rebook ? bookingId : null;
            return;
        }

        _bookingCancelAwaitingConfirmation = null;
        _bookingRebookAwaitingConfirmation = null;
        _busy = true;
        _bookingErrors.Remove(attendeeId);
        _bookingOutcomes.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.CancelBookingAsync(
                attendeeId, bookingId, rebook, CancellationToken.None);
            if (outcome is not { IsSuccess: true, Value: not null })
            {
                _bookingErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _bookingOutcomes[attendeeId] = CancellationMessage(outcome.Value);
        }
        catch (Exception)
        {
            _bookingErrors[attendeeId] = UnexpectedError;
            return;
        }
        finally
        {
            _busy = false;
        }

        _bookings.Remove(attendeeId);
        await LoadBookingsAsync(attendeeId);
        await ReloadAsync();
    }

    /// <summary>Distinguishes a plain cancellation from a delivered and an undelivered replacement.</summary>
    private static string CancellationMessage(CancelAttendeeBookingDto outcome)
    {
        if (!outcome.Reinvited)
        {
            return "Booking cancelled.";
        }

        return outcome.DeliveryStatus == "Sent"
            ? "Booking cancelled; replacement invite sent."
            : "Booking cancelled; replacement invite could not be delivered.";
    }

    private async Task CancelRecoveryAsync(Guid attendeeId)
    {
        if (_busy)
        {
            return;
        }

        if (_recoveryCancelAwaitingConfirmation != attendeeId)
        {
            _recoveryCancelAwaitingConfirmation = attendeeId;
            return;
        }

        _recoveryCancelAwaitingConfirmation = null;
        if (!_pendingRecoveryInvites.TryGetValue(attendeeId, out var inviteId))
        {
            return;
        }

        _busy = true;
        _recoveryErrors.Remove(attendeeId);

        try
        {
            var outcome = await AttendeesApi.CancelRecoveryAsync(
                attendeeId, inviteId, CancellationToken.None);
            if (!outcome.IsSuccess)
            {
                _recoveryErrors[attendeeId] = outcome.ErrorMessage ?? UnexpectedError;
                return;
            }

            _pendingRecoveryInvites.Remove(attendeeId);
            _recoveryOutcomes[attendeeId] = "Recovery cancelled.";
        }
        catch (Exception)
        {
            _recoveryErrors[attendeeId] = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }

        await LoadReadinessAsync(attendeeId);
    }

    internal async Task OnFileChosenAsync(InputFileChangeEventArgs args)
    {
        if (_busy)
        {
            return;
        }

        _importErrors = [];
        _error = null;
        _busy = true;

        try
        {
            if (!args.File.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                _error = "Choose a CSV file to upload.";
                return;
            }

            if (args.File.Size > MaximumUploadBytes)
            {
                _error = "The CSV must be 1 MiB or smaller.";
                return;
            }

            using var stream = args.File.OpenReadStream(MaximumUploadBytes);
            using var reader = new StreamReader(stream);
            var csv = await reader.ReadToEndAsync();
            var outcome = await AttendeesApi.ImportAsync(csv, CancellationToken.None);
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;

            if (outcome.IsSuccess && outcome.Value is { Accepted: false } import)
            {
                _importErrors = import.Errors.ToList();
            }
            else if (outcome.IsSuccess)
            {
                await ReloadAsync();
            }
        }
        catch (Exception)
        {
            _error = "The CSV could not be read or uploaded. Please try again.";
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunAsync<T>(Func<Task<ApiOutcome<T>>> action, Func<Task>? onSuccess = null)
    {
        _busy = true;

        try
        {
            var outcome = await action();
            _error = outcome.IsSuccess ? null : outcome.ErrorMessage ?? UnexpectedError;
            if (outcome.IsSuccess)
            {
                if (onSuccess is not null)
                {
                    await onSuccess();
                }

                await ReloadAsync();
            }
        }
        catch (Exception)
        {
            _error = UnexpectedError;
        }
        finally
        {
            _busy = false;
        }
    }
}
`````
