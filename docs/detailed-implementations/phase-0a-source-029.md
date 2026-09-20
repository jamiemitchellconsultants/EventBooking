# 00a — Port source 29 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Mcp/Program.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Program.cs","encoding":"utf8","sha256":"2aa6fc371014e561b87920d73f493887ba8c7a36faf6685dd8a0f1be7a2c8fe0","parts":1,"part":1} -->

`````csharp
using EventBooking.Api;
using EventBooking.Api.Auth;
using EventBooking.Application;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);


var (connectionString, headOffice, tokens, email, portal) =
    EventBookingConfiguration.Read(builder.Configuration);

builder.Services.AddEventBookingInfrastructure(connectionString, headOffice, tokens);

var smtpHost = builder.Configuration["Email:Smtp:Host"]
    ?? throw new InvalidOperationException(
        "Email:Smtp:Host is required when Email:Provider is Smtp.");
var smtpPort = int.TryParse(builder.Configuration["Email:Smtp:Port"], out var port)
    ? port
    : throw new InvalidOperationException(
        "Email:Smtp:Port must be a valid integer when Email:Provider is Smtp.");

builder.Services.AddLocalEmailTransport(email, new SmtpOptions(smtpHost, smtpPort));

builder.Services.AddEventBookingApplication(portal);
builder.Services.AddEventBookingAuthentication(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<SlotTools>()
    .WithTools<CandidateTools>()
    .WithTools<AdminTools>()
    .WithTools<OperationsTools>();

var app = builder.Build();

app.UseAuthentication();
app.UseMiddleware<StaffIdentityRecorder>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Same bearer tokens and the same staff policy as the API: every tool call runs
// as the signed-in staff identity, and each handler re-checks its capability.
app.MapMcp("/mcp").RequireAuthorization(AuthenticationExtensions.StaffPolicy);

app.Run();

/// <summary>Named so the integration test factory can start this host.</summary>
public partial class Program;
`````

## src/EventBooking.Mcp/Properties/launchSettings.json — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Properties/launchSettings.json","encoding":"utf8","sha256":"9310f3405f6ae8dde6540c63fabab856dfc99fa8583b0b649e1b671fe46d092a","parts":1,"part":1} -->

`````text
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5003",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
`````

## src/EventBooking.Mcp/Tools/AdminTools.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Tools/AdminTools.cs","encoding":"utf8","sha256":"7fceca484cdfa9159998eb4c9324e50a8c66d0d58d924d19c33cec6703e3b2e8","parts":1,"part":1} -->

`````csharp
using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Access;
using EventBooking.Application.Settings;
using EventBooking.Domain.Access;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>System settings, staff access, and caller identity for admins.</summary>
[McpServerToolType]
public sealed class AdminTools
{
    /// <summary>Reads the current system settings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The settings handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The system settings.</returns>
    [McpServerTool(Name = "get_settings", Title = "Get settings", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Read the admin-configurable system settings. Caller must be an admin.")]
    public async Task<SettingsView> GetSettingsAsync(
        ICallerAccessor caller,
        AdminSettingsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetAsync(caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates the system settings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The settings handler.</param>
    /// <param name="inviteExpiryDays">The invite expiry window in days.</param>
    /// <param name="maxAutoRetryCount">The maximum number of times an unanswered invite is automatically re-issued.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "update_settings", Title = "Update settings", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Update the invite expiry window and the invite re-issue limit. Caller must be an admin; overwrites both settings.")]
    public async Task<string> UpdateSettingsAsync(
        ICallerAccessor caller,
        AdminSettingsHandler handler,
        [Description("Invite expiry window in days.")] int inviteExpiryDays,
        [Description("Maximum number of times an unanswered invite is automatically re-issued.")] int maxAutoRetryCount,
        CancellationToken cancellationToken)
    {
        var result = await handler.UpdateAsync(
            new UpdateSettingsCommand(
                caller.RequireStaffUserId(), inviteExpiryDays, maxAutoRetryCount),
            cancellationToken);
        result.ThrowIfFailure();
        return "Settings updated.";
    }

    /// <summary>Lists every staff access profile.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The staff access handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The staff access profiles with scalar staff numbers.</returns>
    [McpServerTool(Name = "list_staff_access", Title = "List staff access", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List every staff access profile with staff number, roles, scope, provider key, and version. Caller must be an admin.")]
    public async Task<IReadOnlyList<StaffAccessToolProfile>> ListStaffAccessAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ListAsync(caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow().Select(profile => profile.ToToolView()).ToList();
    }

    /// <summary>Replaces one existing staff profile's appointment-type scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The staff access handler.</param>
    /// <param name="staffId">The target staff number, or null for historic fallback.</param>
    /// <param name="historicStaffUserId">A historic provider key whose listed staff number is null.</param>
    /// <param name="appointmentTypeId">The appointment-type scope to set, or null to clear it.</param>
    /// <param name="expectedVersion">The version last observed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mutation view.</returns>
    [McpServerTool(Name = "replace_staff_access_scope", Title = "Replace staff access scope", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Atomically replace one existing staff member's appointment-type scope. Caller must be an admin; overwrites the scope. Select by staffId; use historicStaffUserId only for a listed profile whose staffId is null.")]
    public async Task<StaffAccessToolMutation> ReplaceStaffAccessScopeAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        [Description("The target staff number, or null when using historicStaffUserId.")] string? staffId,
        [Description("A listed provider key whose staffId is null, or null when using staffId.")] Guid? historicStaffUserId,
        [Description("The appointment-type scope to set, or null to clear it. This is a real write, not a no-op check.")] Guid? appointmentTypeId,
        [Description("Version last observed.")] long expectedVersion,
        CancellationToken cancellationToken)
    {
        var actorStaffUserId = caller.RequireStaffUserId();
        var target = await ResolveTargetAsync(
            actorStaffUserId, handler, staffId, historicStaffUserId, cancellationToken);
        var result = await handler.ReplaceScopeAsync(
            new ReplaceStaffAccessProfileScopeCommand(
                actorStaffUserId, target.StaffUserId, appointmentTypeId, expectedVersion),
            cancellationToken);
        var mutation = result.ValueOrThrow();
        return new StaffAccessToolMutation
        {
            Profile = mutation.Profile.ToToolView(target.StaffId),
            FormerManagerStaffUserId = mutation.FormerManagerStaffUserId,
        };
    }

    /// <summary>Clears one existing staff profile's appointment-type scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The staff access handler.</param>
    /// <param name="staffId">The target staff number, or null for historic fallback.</param>
    /// <param name="historicStaffUserId">A historic provider key whose listed staff number is null.</param>
    /// <param name="expectedVersion">The version last observed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "clear_staff_access_scope", Title = "Clear staff access scope", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Clear one staff member's appointment-type scope. Select by staffId; use historicStaffUserId only for a listed profile whose staffId is null.")]
    public async Task<string> ClearStaffAccessScopeAsync(
        ICallerAccessor caller,
        StaffAccessHandler handler,
        [Description("The target staff number, or null when using historicStaffUserId.")] string? staffId,
        [Description("A listed provider key whose staffId is null, or null when using staffId.")] Guid? historicStaffUserId,
        [Description("Version last observed.")] long expectedVersion,
        CancellationToken cancellationToken)
    {
        var actorStaffUserId = caller.RequireStaffUserId();
        var target = await ResolveTargetAsync(
            actorStaffUserId, handler, staffId, historicStaffUserId, cancellationToken);
        var result = await handler.ClearScopeAsync(
            new ClearStaffAccessProfileScopeCommand(
                actorStaffUserId, target.StaffUserId, expectedVersion),
            cancellationToken);
        result.ThrowIfFailure();
        return "Staff access scope cleared.";
    }

    /// <summary>Returns the caller's validated staff number, roles, and scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The identity handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The caller's access view, including the validated staff number.</returns>
    [McpServerTool(Name = "get_my_access", Title = "Get my access", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return your validated staff number, roles, and appointment type scope. Available to any signed-in staff member.")]
    public async Task<MyAccessToolView> GetMyAccessAsync(
        ICallerAccessor caller,
        MeHandler handler,
        CancellationToken cancellationToken)
    {
        var view = await handler.GetAsync(
            caller.RequireStaffUserId(), caller.RequireStaffId(), caller.Roles, cancellationToken);
        return view.ToToolView();
    }

    private static async Task<ResolvedStaffTarget> ResolveTargetAsync(
        Guid actorStaffUserId,
        StaffAccessHandler handler,
        string? staffId,
        Guid? historicStaffUserId,
        CancellationToken cancellationToken)
    {
        var hasStaffId = staffId is not null;
        var hasHistoricStaffUserId = historicStaffUserId is not null;
        if (hasStaffId == hasHistoricStaffUserId)
        {
            throw new McpException(
                "Provide exactly one of staffId or historicStaffUserId.");
        }

        if (hasStaffId)
        {
            if (!StaffId.TryParse(staffId, out var parsedStaffId))
            {
                throw new McpException("Enter a valid staff number.");
            }

            var resolved = await handler.ResolveIdentityAsync(
                actorStaffUserId, staffId!, cancellationToken);
            return new ResolvedStaffTarget(
                resolved.ValueOrThrow(), parsedStaffId!.Value);
        }

        var profiles = (await handler.ListAsync(actorStaffUserId, cancellationToken)).ValueOrThrow();
        var historic = profiles.SingleOrDefault(
            profile => profile.StaffUserId == historicStaffUserId);
        if (historic is null)
        {
            throw new McpException("No such historic staff profile.");
        }

        if (historic.StaffId is not null)
        {
            throw new McpException(
                "Use staffId for a profile with an observed staff number.");
        }

        return new ResolvedStaffTarget(historic.StaffUserId, null);
    }

    private sealed record ResolvedStaffTarget(Guid StaffUserId, string? StaffId);
}
`````

## src/EventBooking.Mcp/Tools/AdminToolViews.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Tools/AdminToolViews.cs","encoding":"utf8","sha256":"22269cbbd9665b1b088e3cbbda10fc1365aa0f210dd4202c42524734034f8b9e","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;

namespace EventBooking.Mcp.Tools;

/// <summary>Describes the caller's access using scalar values suitable for MCP clients.</summary>
public sealed record MyAccessToolView
{
    /// <summary>Gets the caller's validated enterprise staff number.</summary>
    public required string StaffId { get; init; }

    /// <summary>Gets the caller's complete role names.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Gets the shared appointment type scope, when one applies.</summary>
    public Guid? AppointmentTypeId { get; init; }

    /// <summary>Gets the display name for the shared appointment type scope.</summary>
    public string? AppointmentTypeName { get; init; }
}

/// <summary>Describes a staff access profile using scalar values suitable for MCP clients.</summary>
public sealed record StaffAccessToolProfile
{
    /// <summary>Gets the provider key retained for historic-profile fallback.</summary>
    public Guid StaffUserId { get; init; }

    /// <summary>Gets the observed enterprise staff number, or null for an historic profile.</summary>
    public string? StaffId { get; init; }

    /// <summary>Gets the profile's complete role names.</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Gets the shared appointment type scope, when one applies.</summary>
    public Guid? AppointmentTypeId { get; init; }

    /// <summary>Gets the display name for the shared appointment type scope.</summary>
    public string? AppointmentTypeName { get; init; }

    /// <summary>Gets the optimistic concurrency version.</summary>
    public long Version { get; init; }
}

/// <summary>Describes the result of replacing a staff access profile through MCP.</summary>
public sealed record StaffAccessToolMutation
{
    /// <summary>Gets the created or replaced profile.</summary>
    public required StaffAccessToolProfile Profile { get; init; }

    /// <summary>Gets the displaced manager's provider key, when a manager changed.</summary>
    public Guid? FormerManagerStaffUserId { get; init; }
}

/// <summary>Maps application access views to stable MCP response contracts.</summary>
internal static class AdminToolViewMapper
{
    /// <summary>Maps the caller view to a scalar MCP contract.</summary>
    /// <param name="view">The application caller view.</param>
    /// <returns>The MCP caller view.</returns>
    internal static MyAccessToolView ToToolView(this MeView view) => new()
    {
        StaffId = view.StaffId?.Value
            ?? throw new InvalidOperationException("The MCP staff policy requires a staff number."),
        Roles = view.Roles.Select(role => role.ToString()).ToList(),
        AppointmentTypeId = view.AppointmentTypeId,
        AppointmentTypeName = view.AppointmentTypeName,
    };

    /// <summary>Maps a profile to a scalar MCP contract.</summary>
    /// <param name="view">The application profile view.</param>
    /// <param name="staffId">An optional resolved staff number override.</param>
    /// <returns>The MCP profile view.</returns>
    internal static StaffAccessToolProfile ToToolView(
        this StaffAccessProfileView view,
        string? staffId = null) => new()
    {
        StaffUserId = view.StaffUserId,
        StaffId = staffId ?? view.StaffId?.Value,
        Roles = view.Roles.Select(role => role.ToString()).ToList(),
        AppointmentTypeId = view.AppointmentTypeId,
        AppointmentTypeName = view.AppointmentTypeName,
        Version = view.Version,
    };
}
`````

## src/EventBooking.Mcp/Tools/CandidateTools.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Tools/CandidateTools.cs","encoding":"utf8","sha256":"9e2c33521a2caab8888945c2cb286e9ad93172d4cf0f1f6c1eb84804f485eede","parts":1,"part":1} -->

`````csharp
using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Candidates;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

public sealed record CandidateToolView
{
    /// <summary>Gets the candidate identifier for follow-up tool calls.</summary>
    public required Guid CandidateId { get; init; }
    /// <summary>Gets the candidate display name.</summary>
    public required string Name { get; init; }
    /// <summary>Gets the candidate email address.</summary>
    public required string Email { get; init; }
    /// <summary>Gets the candidate lifecycle status name.</summary>
    public required string Status { get; init; }
    /// <summary>Gets the assigned Employee Group name, or null during reconciliation.</summary>
    public required string? EmployeeGroupName { get; init; }
    /// <summary>Gets the canonical Employee Group code, or null during Release 1 reconciliation.</summary>
    public required string? EmployeeGroupCode { get; init; }
    /// <summary>Gets whether explicit Coordinator assignment is still required.</summary>
    public required bool RequiresEmployeeGroupReconciliation { get; init; }
    /// <summary>Gets read-only derived Appointment Type summaries.</summary>
    public required IReadOnlyList<AppointmentTypeSummary> RequiredAppointmentTypes { get; init; }
    /// <summary>Gets internal readiness without exposing recovery mutation.</summary>
    public required CandidateReadiness? Readiness { get; init; }
}

/// <summary>Tool-safe readiness including Coordinator display wording.</summary>
/// <param name="CandidateId">The candidate the readiness was calculated for.</param>
/// <param name="Code">The readiness code name.</param>
/// <param name="Display">The Coordinator-facing display wording for the code.</param>
/// <param name="OutstandingAppointmentTypes">The appointment types still outstanding.</param>
public sealed record CandidateReadinessToolView(
    Guid CandidateId,
    string Code,
    string Display,
    IReadOnlyList<OutstandingAppointmentType> OutstandingAppointmentTypes);

/// <summary>Candidate management, invites, and delivery retry for coordinators.</summary>
[McpServerToolType]
public sealed class CandidateTools
{
    private const int DefaultPageSize = 50;

    private const int MaxPageSize = 200;

    /// <summary>Lists candidates, optionally filtered by status or search text.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="readiness">Resolves internal readiness per listed candidate.</param>
    /// <param name="status">The candidate status name, or null for all.</param>
    /// <param name="search">Free-text filter, or null.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">Results per page, clamped to the tool maximum.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded page of candidate views.</returns>
    [McpServerTool(Name = "list_candidates", Title = "List candidates", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List candidates, optionally filtered by status name and search text. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<CandidateToolView>> ListCandidatesAsync(
        ICallerAccessor caller,
        ListCandidatesHandler handler,
        GetCandidateReadinessHandler readiness,
        [Description("Candidate status name (e.g. Invited) or null for all.")] string? status = null,
        [Description("Free-text name or email filter or null.")] string? search = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Results per page, at most 200.")] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        CandidateStatus? parsed = null;
        if (status is not null)
        {
            if (!Enum.TryParse<CandidateStatus>(status, ignoreCase: false, out var value) ||
                !Enum.IsDefined(value))
            {
                throw new ModelContextProtocol.McpException(
                    "Status must be a recognised CandidateStatus name.");
            }

            parsed = value;
        }

        var staffUserId = caller.RequireStaffUserId();
        var result = await handler.HandleAsync(
            new ListCandidatesQuery(staffUserId, parsed, search),
            cancellationToken);
        var bounded = Math.Clamp(pageSize, 1, MaxPageSize);
        var skipped = Math.Max(page - 1, 0) * bounded;

        var views = new List<CandidateToolView>();
        foreach (var item in result.ValueOrThrow().Skip(skipped).Take(bounded))
        {
            var readinessResult = await readiness.HandleAsync(
                new GetCandidateReadinessQuery(staffUserId, item.CandidateId),
                cancellationToken);
            views.Add(new CandidateToolView
            {
                CandidateId = item.CandidateId,
                Name = item.Name,
                Email = item.Email,
                Status = item.Status.ToString(),
                EmployeeGroupName = item.EmployeeGroupName,
                EmployeeGroupCode = item.EmployeeGroupCode,
                RequiresEmployeeGroupReconciliation = item.RequiresEmployeeGroupReconciliation,
                RequiredAppointmentTypes = item.RequiredAppointmentTypes,
                Readiness = readinessResult.IsSuccess ? readinessResult.Value : null,
            });
        }

        return views;
    }

    /// <summary>Lists the Employee Groups available for candidate assignment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The list handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The assignable groups with their required appointment types.</returns>
    [McpServerTool(Name = "list_employee_groups", Title = "List employee groups", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List the employee groups that determine candidate requirements. Caller must be a coordinator or admin.")]
    public async Task<IReadOnlyList<EmployeeGroupListItem>> ListEmployeeGroupsAsync(
        ICallerAccessor caller,
        ListEmployeeGroupsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ListEmployeeGroupsQuery(caller.RequireStaffUserId()),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Creates one candidate in an Employee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Employee Group.</param>
    /// <param name="name">The candidate name.</param>
    /// <param name="email">The candidate email.</param>
    /// <param name="employeeGroupCode">The canonical Employee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new candidate identifier.</returns>
    [McpServerTool(Name = "create_candidate", Title = "Create candidate", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Create a candidate in one employee group; requirements derive from the group. Caller must be a coordinator or admin; creates a new candidate record.")]
    public async Task<Guid> CreateCandidateAsync(
        ICallerAccessor caller,
        SaveCandidateHandler handler,
        IEmployeeGroupRepository groups,
        [Description("Candidate full name.")] string name,
        [Description("Candidate email address.")] string email,
        [Description("Canonical employee group code (e.g. PILOTS).")] string employeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, employeeGroupCode, cancellationToken);
        var result = await handler.CreateAsync(
            new CreateCandidateCommand(caller.RequireStaffUserId(), name, email, groupId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Updates a candidate's name, email, or Employee Group.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The save handler.</param>
    /// <param name="groups">Resolves the assigned Employee Group.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="name">The corrected name.</param>
    /// <param name="email">The corrected email.</param>
    /// <param name="employeeGroupCode">The canonical Employee Group code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "update_candidate", Title = "Update candidate", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Update a candidate. Caller must be a coordinator or admin; requirements are frozen while an active booking exists.")]
    public async Task<string> UpdateCandidateAsync(
        ICallerAccessor caller,
        SaveCandidateHandler handler,
        IEmployeeGroupRepository groups,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("Corrected full name.")] string name,
        [Description("Corrected email address.")] string email,
        [Description("Canonical employee group code (e.g. PILOTS).")] string employeeGroupCode,
        CancellationToken cancellationToken)
    {
        var groupId = await ResolveGroupIdAsync(groups, employeeGroupCode, cancellationToken);
        var result = await handler.UpdateAsync(
            new UpdateCandidateCommand(
                caller.RequireStaffUserId(), candidateId, name, email, groupId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Candidate updated.";
    }

    /// <summary>Deletes a candidate, optionally cascading.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The delete handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="confirm">Whether dependent data may be removed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "delete_candidate", Title = "Delete candidate", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Delete a candidate. Caller must be a coordinator or admin; set confirm to true to also remove dependent data.")]
    public async Task<string> DeleteCandidateAsync(
        ICallerAccessor caller,
        DeleteCandidateHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("Whether dependent data may be removed.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new DeleteCandidateCommand(caller.RequireStaffUserId(), candidateId, confirm),
            cancellationToken);
        result.ThrowIfFailure();
        return "Candidate deleted.";
    }

    /// <summary>Imports candidates from CSV content.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_candidates", Title = "Import candidates", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Import candidates from CSV content with name, email, and employee group code. Caller must be a coordinator or admin; creates new candidate records.")]
    public async Task<CandidateImportOutcome> ImportCandidatesAsync(
        ICallerAccessor caller,
        ImportCandidatesHandler handler,
        [Description("CSV content with one candidate per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportCandidatesCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Resolves a canonical Employee Group code to its stable identifier.</summary>
    private static async Task<Guid> ResolveGroupIdAsync(
        IEmployeeGroupRepository groups,
        string employeeGroupCode,
        CancellationToken cancellationToken)
    {
        var group = await groups.GetByCodeAsync(employeeGroupCode, cancellationToken);
        if (group is null)
        {
            throw new ModelContextProtocol.McpException(
                $"Employee group '{employeeGroupCode}' is not known. Use list_employee_groups.");
        }

        return group.Id;
    }

    /// <summary>Triggers a fresh invite for a candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The invite handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The invite issue result.</returns>
    [McpServerTool(Name = "trigger_invite", Title = "Trigger invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Issue a fresh three-option invite to a candidate. Caller must be a coordinator or admin; sends an invite email.")]
    public async Task<InviteIssueResult> TriggerInviteAsync(
        ICallerAccessor caller,
        TriggerInviteHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new TriggerInviteCommand(caller.RequireStaffUserId(), candidateId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Retries the latest unresolved email for a candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The retry handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retry outcome.</returns>
    [McpServerTool(Name = "retry_candidate_email", Title = "Retry candidate email", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Retry the latest unresolved candidate email delivery. Caller must be a coordinator or admin; resends the latest unresolved email.")]
    public async Task<RetryEmailOutcome> RetryCandidateEmailAsync(
        ICallerAccessor caller,
        RetryEmailHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RetryEmailCommand(caller.RequireStaffUserId(), candidateId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Starts a recovery invite for a candidate with a missed appointment.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery invite issue result.</returns>
    [McpServerTool(Name = "start_recovery_invite", Title = "Start recovery invite", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Start a recovery invite for a candidate with a missed appointment. Caller must have ManageCandidates; sends a recovery invite email.")]
    public async Task<StartRecoveryResult> StartRecoveryInviteAsync(
        ICallerAccessor caller,
        StartRecoveryHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new StartRecoveryCommand(caller.RequireStaffUserId(), candidateId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a pending recovery invite for a candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The recovery cancellation handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="inviteId">The recovery invite identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "cancel_recovery_invite", Title = "Cancel recovery invite", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a pending recovery invite for a candidate. Caller must have ManageCandidates; cancels the pending recovery invite.")]
    public async Task<string> CancelRecoveryInviteAsync(
        ICallerAccessor caller,
        CancelRecoveryInviteHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("The recovery invite identifier.")] Guid inviteId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelRecoveryInviteCommand(caller.RequireStaffUserId(), candidateId, inviteId), cancellationToken);
        result.ThrowIfFailure();
        return "Recovery invite cancelled.";
    }

    /// <summary>Lists the active bookings a coordinator may cancel for one candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The bookings handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The candidate's active booking summaries.</returns>
    [McpServerTool(Name = "list_candidate_bookings", Title = "List candidate bookings", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List a candidate's active bookings. Caller must have ManageCandidates; returns cancellable bookings without management tokens.")]
    public async Task<IReadOnlyList<CandidateBookingSummary>> ListCandidateBookingsAsync(
        ICallerAccessor caller,
        GetCandidateBookingsHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetCandidateBookingsQuery(caller.RequireStaffUserId(), candidateId), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels one candidate booking, optionally rebooking the candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="bookingId">The booking identifier.</param>
    /// <param name="rebook">Whether to issue a replacement invite.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_candidate_booking", Title = "Cancel candidate booking", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel one candidate booking, optionally rebooking. Caller must have ManageCandidates; cancels the booking and optionally sends a replacement invite.")]
    public async Task<CancelBookingOutcome> CancelCandidateBookingAsync(
        ICallerAccessor caller,
        CancelCandidateBookingHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        [Description("The booking identifier.")] Guid bookingId,
        [Description("Whether to issue a replacement invite.")] bool rebook,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelCandidateBookingCommand(caller.RequireStaffUserId(), candidateId, bookingId, rebook), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Gets tool-safe readiness including Coordinator display wording.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The readiness handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool-safe readiness view.</returns>
    [McpServerTool(Name = "get_candidate_readiness", Title = "Get candidate readiness", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Get a candidate's readiness with Coordinator display wording. Caller must have ManageCandidates; returns internal readiness without recovery mutation.")]
    public async Task<CandidateReadinessToolView> GetCandidateReadinessAsync(
        ICallerAccessor caller,
        GetCandidateReadinessHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetCandidateReadinessQuery(caller.RequireStaffUserId(), candidateId), cancellationToken);
        var value = result.ValueOrThrow();
        return new CandidateReadinessToolView(
            value.CandidateId,
            value.Code.ToString(),
            CandidateEndpoints.DisplayForTool(value.Code),
            value.OutstandingAppointmentTypes);
    }
}
`````

## src/EventBooking.Mcp/Tools/McpErrors.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Tools/McpErrors.cs","encoding":"utf8","sha256":"59397b8f033cbb03f0ebf4513c75c2614cab0b94fc38c1713c096f0d1f5acc16","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Common;
using ModelContextProtocol;

namespace EventBooking.Mcp.Tools;

/// <summary>Turns application results into MCP tool outcomes.</summary>
internal static class McpErrors
{
    /// <summary>Returns the value, or throws an MCP error carrying the failure message.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The handler result.</param>
    /// <returns>The success value.</returns>
    internal static T ValueOrThrow<T>(this Result<T> result) =>
        result.IsSuccess
            ? result.Value
            : throw new McpException(result.Error.Message);

    /// <summary>Throws an MCP error when the result failed.</summary>
    /// <param name="result">The handler result.</param>
    internal static void ThrowIfFailure(this Result result)
    {
        if (result.IsFailure)
        {
            throw new McpException(result.Error.Message);
        }
    }
}
`````

## src/EventBooking.Mcp/Tools/OperationsTools.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Tools/OperationsTools.cs","encoding":"utf8","sha256":"ada909195d6947b2a7aa37bf0e9d01910204e1384c4321f77834ca6252884e10","parts":1,"part":1} -->

`````csharp
using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Api.Endpoints;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Appointments;
using EventBooking.Application.Dashboards;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>Dashboards, audit history, and the appointment workspace.</summary>
[McpServerToolType]
public sealed class OperationsTools
{
    /// <summary>Returns the coordinator dashboards for the caller's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The dashboards handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The dashboards view.</returns>
    [McpServerTool(Name = "get_dashboards", Title = "Get dashboards", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the coordinator dashboards visible to the caller. Caller must be a coordinator or admin.")]
    public async Task<DashboardsView> GetDashboardsAsync(
        ICallerAccessor caller,
        GetDashboardsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetDashboardsQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the audit history of one confirmed slot.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit history handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The audit rows.</returns>
    [McpServerTool(Name = "slot_audit_history", Title = "Slot audit history", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the audit history of one confirmed slot. Caller must have audit visibility (Admin or Coordinator).")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetSlotAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(
                caller.RequireStaffUserId(), AuditEntityTypes.ConfirmedSlot, confirmedSlotId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the audit history of one candidate.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit history handler.</param>
    /// <param name="candidateId">The candidate identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The audit rows.</returns>
    [McpServerTool(Name = "candidate_audit_history", Title = "Candidate audit history", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return the audit history of one candidate. Caller must have audit visibility (Admin or Coordinator).")]
    public async Task<IReadOnlyList<AuditHistoryRow>> GetCandidateAuditHistoryAsync(
        ICallerAccessor caller,
        GetAuditHistoryHandler handler,
        [Description("The candidate identifier.")] Guid candidateId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetAuditHistoryQuery(caller.RequireStaffUserId(), null, candidateId),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Lists the appointment workspace slots for the caller's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workspace slot list.</returns>
    [McpServerTool(Name = "appointment_slots", Title = "Appointment slots", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List current and upcoming active slots with appointment counts for your scope. Caller must be a coordinator, admin, or manager within scope.")]
    public async Task<AppointmentWorkspaceSlotList> ListAppointmentSlotsAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ListSlotsAsync(
            caller.RequireStaffUserId(), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns one appointment workspace slot with its operational rows.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The slot detail.</returns>
    [McpServerTool(Name = "appointment_slot_detail", Title = "Appointment slot detail", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Return one active slot with its minimum-data appointment rows. Caller must be a coordinator, admin, or manager within scope.")]
    public async Task<AppointmentSlotDetail> GetAppointmentSlotAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        CancellationToken cancellationToken)
    {
        var result = await handler.GetSlotAsync(
            caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Searches audit events with optional filters and cursor paging.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The audit search handler.</param>
    /// <param name="from">Inclusive lower timestamp bound, or null for no lower bound.</param>
    /// <param name="to">Inclusive upper timestamp bound, or null for no upper bound.</param>
    /// <param name="actorType">Actor type name to match, or null for any.</param>
    /// <param name="action">Audit action name to match, or null for any.</param>
    /// <param name="identifier">Free-text identifier matched against entity or actor id.</param>
    /// <param name="entityType">Optional entity type within the caller's allowed bucket.</param>
    /// <param name="cursor">Opaque keyset cursor, or null for the newest page.</param>
    /// <param name="pageSize">Rows per page; defaults to 50 and is clamped to 200.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching audit page; an empty cursor means no further pages.</returns>
    [McpServerTool(Name = "search_audit", Title = "Search audit", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Search audit events with optional from/to timestamp bounds, actorType, action, identifier, and entityType filters plus an opaque cursor. Page size defaults to 50 and is clamped to 200. Entity visibility is scoped to the caller's audit capabilities.")]
    public async Task<AuditSearchPage> SearchAuditAsync(
        ICallerAccessor caller,
        GetAuditSearchHandler handler,
        [Description("Inclusive lower timestamp bound, or null for no lower bound.")] string? from = null,
        [Description("Inclusive upper timestamp bound, or null for no upper bound.")] string? to = null,
        [Description("Actor type name to match, or null for any.")] string? actorType = null,
        [Description("Audit action name to match, or null for any.")] string? action = null,
        [Description("Free-text identifier matched against entity or actor id.")] string? identifier = null,
        [Description("Optional entity type within the caller's allowed bucket.")] string? entityType = null,
        [Description("Opaque keyset cursor, or null for the newest page.")] string? cursor = null,
        [Description("Rows per page; defaults to 50 and is clamped to 200.")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!AuditInputParser.TryParseBound(from, out var fromBound) ||
            !AuditInputParser.TryParseBound(to, out var toBound))
        {
            throw new ModelContextProtocol.McpException("The from/to bound is not a valid timestamp.");
        }

        var result = await handler.HandleAsync(
            new GetAuditSearchQuery(
                caller.RequireStaffUserId(),
                fromBound,
                toBound,
                actorType,
                action,
                identifier,
                entityType,
                cursor,
                pageSize <= 0 ? 50 : pageSize),
            cancellationToken);
        var page = result.ValueOrThrow();
        return page.NextCursor is null ? page with { NextCursor = string.Empty } : page;
    }

    /// <summary>Exports one scoped slot roster as CSV text with its download filename.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The workspace handler.</param>
    /// <param name="formatter">The roster CSV formatter.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roster CSV text and download filename.</returns>
    [McpServerTool(Name = "export_appointment_roster", Title = "Export appointment roster", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Export one appointment roster as CSV text with its download filename. Requires the ConductAppointments scope; the CSV is returned as text, not a server-side file.")]
    public async Task<RosterCsvResult> ExportAppointmentRosterAsync(
        ICallerAccessor caller,
        GetAppointmentWorkspaceHandler handler,
        AppointmentRosterCsvFormatter formatter,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.GetSlotAsync(
            caller.RequireStaffUserId(), confirmedSlotId, cancellationToken);
        return formatter.Format(result.ValueOrThrow());
    }

    /// <summary>Records check-in, completion, no-show, or a bounded correction.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The status handler.</param>
    /// <param name="bookingAppointmentId">The booking appointment identifier.</param>
    /// <param name="status">The requested status name.</param>
    /// <param name="expectedVersion">The positive version last observed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated appointment row.</returns>
    [McpServerTool(Name = "update_appointment_status", Title = "Update appointment status", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Check in, complete, mark no-show, or correct one booked appointment. Caller must be a coordinator or admin; mutates the appointment status.")]
    public async Task<BookingAppointmentUpdateView> UpdateAppointmentStatusAsync(
        ICallerAccessor caller,
        UpdateBookingAppointmentStatusHandler handler,
        [Description("The booking appointment identifier.")] Guid bookingAppointmentId,
        [Description("Requested status name: Expected, CheckedIn, Completed, or NoShow.")] string status,
        [Description("Positive version last observed by the caller.")] long expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BookingAppointmentStatus>(status, ignoreCase: false, out var parsed) ||
            !Enum.IsDefined(parsed) ||
            expectedVersion <= 0)
        {
            throw new ModelContextProtocol.McpException(
                "A recognised status and positive expectedVersion are required.");
        }

        var result = await handler.HandleAsync(
            new UpdateBookingAppointmentStatusCommand
            {
                StaffUserId = caller.RequireStaffUserId(),
                BookingAppointmentId = bookingAppointmentId,
                Status = parsed,
                ExpectedVersion = expectedVersion,
            },
            cancellationToken);
        return result.ValueOrThrow();
    }
}
`````

## src/EventBooking.Mcp/Tools/SlotTools.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Mcp/Tools/SlotTools.cs","encoding":"utf8","sha256":"eb1d9526afe9028cb9e34604c941db51ae69d95fd06c00699548bc609da6379f","parts":1,"part":1} -->

`````csharp
using System.ComponentModel;
using EventBooking.Api.Auth;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Slots;
using ModelContextProtocol.Server;

namespace EventBooking.Mcp.Tools;

/// <summary>Slot negotiation and confirmed-slot operations for the calling manager.</summary>
[McpServerToolType]
public sealed class SlotTools
{
    /// <summary>Proposes a new four-hour candidate-facing slot window.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The slot proposal handler.</param>
    /// <param name="date">The head-office calendar date, yyyy-MM-dd.</param>
    /// <param name="startTime">The window start, HH:mm.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new proposal identifier.</returns>
    [McpServerTool(Name = "propose_slot", Title = "Propose slot", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Propose a new four-hour slot window. Caller must be a manager.")]
    public async Task<Guid> ProposeSlotAsync(
        ICallerAccessor caller,
        ProposeSlotHandler handler,
        [Description("Head-office calendar date, yyyy-MM-dd.")] string date,
        [Description("Window start time, HH:mm.")] string startTime,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(date, out var parsedDate) ||
            !TimeOnly.TryParse(startTime, out var parsedStart))
        {
            throw new ModelContextProtocol.McpException(
                "A yyyy-MM-dd date and HH:mm startTime are required.");
        }

        var result = await handler.HandleAsync(
            new ProposeSlotCommand(caller.RequireStaffUserId(), parsedDate, parsedStart),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Records or revises the calling manager's acceptance of a proposal.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The acceptance handler.</param>
    /// <param name="proposalId">The proposal identifier.</param>
    /// <param name="headcount">The manager's headcount for their appointment type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The acceptance outcome, including the confirmed slot once all three managers accept.</returns>
    [McpServerTool(Name = "accept_proposal", Title = "Accept proposal", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Accept a slot proposal with your headcount, or revise your headcount while it stays open. Caller must be a manager; may confirm the slot once every required type accepts.")]
    public async Task<AcceptProposalOutcome> AcceptProposalAsync(
        ICallerAccessor caller,
        AcceptProposalHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        [Description("Headcount for your appointment type.")] int headcount,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AcceptProposalCommand(caller.RequireStaffUserId(), proposalId, headcount),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Withdraws the calling manager's acceptance while the proposal is still open.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The withdrawal handler.</param>
    /// <param name="proposalId">The proposal identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "withdraw_acceptance", Title = "Withdraw acceptance", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Withdraw your acceptance of an open slot proposal. Caller must be a manager; removes only your acceptance.")]
    public async Task<string> WithdrawAcceptanceAsync(
        ICallerAccessor caller,
        WithdrawAcceptanceHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new WithdrawAcceptanceCommand(caller.RequireStaffUserId(), proposalId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Acceptance withdrawn.";
    }

    /// <summary>Withdraws a proposal created by the calling manager.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The withdrawal handler.</param>
    /// <param name="proposalId">The proposal identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A confirmation message.</returns>
    [McpServerTool(Name = "withdraw_proposal", Title = "Withdraw proposal", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Withdraw one of your own open slot proposals. Caller must be the proposing manager.")]
    public async Task<string> WithdrawProposalAsync(
        ICallerAccessor caller,
        WithdrawProposalHandler handler,
        [Description("The proposal identifier.")] Guid proposalId,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new WithdrawProposalCommand(caller.RequireStaffUserId(), proposalId),
            cancellationToken);
        result.ThrowIfFailure();
        return "Proposal withdrawn.";
    }

    /// <summary>Lists open proposals and confirmed slots for the calling manager's scope.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The slot board handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The manager slot board.</returns>
    [McpServerTool(Name = "slot_board", Title = "Slot board", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("List open slot proposals and your confirmed slots with remaining capacity. Caller must be a manager; scoped to your appointment type.")]
    public async Task<ManagerSlotBoard> GetSlotBoardAsync(
        ICallerAccessor caller,
        GetManagerSlotBoardHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetManagerSlotBoardQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Returns the candidate-free confirmed-slot operations view.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The slot operations handler.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Every confirmed slot visible to Admin or Coordinator, without candidate data.</returns>
    [McpServerTool(
        Name = "get_slot_operations",
        Title = "Get slot operations",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("List confirmed-slot dates, windows, capacities, status, and aggregate booking counts without candidate data. Caller must have ViewSlotOperations capability (Admin or Coordinator).")]
    public async Task<SlotOperationsView> GetSlotOperationsAsync(
        ICallerAccessor caller,
        GetSlotOperationsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new GetSlotOperationsQuery(caller.RequireStaffUserId()), cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Replaces the total headcount for the calling manager's type on a slot.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The capacity handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="totalHeadcount">The new positive total covering every active booking.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated capacity.</returns>
    [McpServerTool(Name = "adjust_slot_capacity", Title = "Adjust slot capacity", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Replace the total headcount for your appointment type on an active confirmed slot. Caller must be a manager; must cover every active booking.")]
    public async Task<AdjustConfirmedSlotCapacityOutcome> AdjustSlotCapacityAsync(
        ICallerAccessor caller,
        AdjustConfirmedSlotCapacityHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        [Description("New positive total headcount.")] int totalHeadcount,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AdjustConfirmedSlotCapacityCommand(
                caller.RequireStaffUserId(), confirmedSlotId, totalHeadcount),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Cancels a confirmed slot, optionally cascading to its active bookings.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The cancellation handler.</param>
    /// <param name="confirmedSlotId">The confirmed slot identifier.</param>
    /// <param name="confirm">Whether cancellation of active bookings is authorized.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cancellation outcome.</returns>
    [McpServerTool(Name = "cancel_confirmed_slot", Title = "Cancel confirmed slot", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Cancel a confirmed slot. Caller must be a manager; set confirm to true to also void its active bookings.")]
    public async Task<CancelSlotOutcome> CancelConfirmedSlotAsync(
        ICallerAccessor caller,
        CancelConfirmedSlotHandler handler,
        [Description("The confirmed slot identifier.")] Guid confirmedSlotId,
        [Description("Whether active bookings may be voided.")] bool confirm,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new CancelConfirmedSlotCommand(caller.RequireStaffUserId(), confirmedSlotId, confirm),
            cancellationToken);
        return result.ValueOrThrow();
    }

    /// <summary>Bulk-imports already-agreed confirmed slots from CSV.</summary>
    /// <param name="caller">The signed-in staff identity.</param>
    /// <param name="handler">The import handler.</param>
    /// <param name="csv">The CSV content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import outcome.</returns>
    [McpServerTool(Name = "import_confirmed_slots", Title = "Import confirmed slots", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Bulk-import already-agreed confirmed slots from CSV. Caller must be an admin or coordinator.")]
    public async Task<ConfirmedSlotImportOutcome> ImportConfirmedSlotsAsync(
        ICallerAccessor caller,
        ImportConfirmedSlotsHandler handler,
        [Description("CSV content with one already-agreed slot per row.")] string csv,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new ImportConfirmedSlotsCommand(caller.RequireStaffUserId(), csv),
            cancellationToken);
        return result.ValueOrThrow();
    }
}
`````
