# 00a — Port source 9 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs","encoding":"utf8","sha256":"96dea0d4ff0618963e31f094d4865637c1e1c12b7ab97132a5011a2c25312a72","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Dashboards;

/// <summary>Cross-cutting audit search request; allowed entity types are derived from capabilities.</summary>
/// <param name="StaffUserId">The staff identity performing the search.</param>
/// <param name="From">Inclusive lower bound on the recorded timestamp, or null.</param>
/// <param name="To">Inclusive upper bound on the recorded timestamp, or null.</param>
/// <param name="ActorType">Actor type name to match, or null for any.</param>
/// <param name="Action">Audit action name to match, or null for any.</param>
/// <param name="Identifier">Free-text identifier matched exactly against entity id or actor id.</param>
/// <param name="EntityType">Optional single entity type within the caller's allowed bucket.</param>
/// <param name="Cursor">Opaque keyset cursor, or null for the newest page.</param>
/// <param name="PageSize">Rows per page; clamped to 200.</param>
public sealed record GetAuditSearchQuery(
    Guid StaffUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? ActorType,
    string? Action,
    string? Identifier,
    string? EntityType,
    string? Cursor,
    int PageSize);

/// <summary>Searches the audit log within the entity-type bucket the caller's capabilities allow.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetAuditSearchHandler(
    IAuditQueries queries,
    IStaffAccessAuthorizer access)
{
    private static readonly IReadOnlyList<string> CandidateBucket =
    [
        AuditEntityTypes.Candidate,
        AuditEntityTypes.Invite,
        AuditEntityTypes.Booking,
        AuditEntityTypes.BookingAppointment
    ];

    private static readonly IReadOnlyList<string> OperationalBucket =
    [
        AuditEntityTypes.SlotProposal,
        AuditEntityTypes.ConfirmedSlot,
        AuditEntityTypes.StaffAccessProfile
    ];

    /// <summary>Computes the allowed bucket and runs the search, refusing out-of-bucket requests.</summary>
    /// <param name="query">The search request.</param>
    /// <param name="cancellationToken">Cancels the authorization and query.</param>
    /// <returns>The result page, or a forbidden failure when the caller may not search.</returns>
    public async Task<Result<AuditSearchPage>> HandleAsync(
        GetAuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var maySeeCandidates = (await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewCandidateAudit, null, cancellationToken)).IsSuccess;
        var maySeeOperations = (await access.AuthorizeAsync(
            query.StaffUserId, StaffCapability.ViewSlotAudit, null, cancellationToken)).IsSuccess;

        if (!maySeeCandidates && !maySeeOperations)
        {
            return Result<AuditSearchPage>.Failure(Error.Forbidden("Search requires audit access."));
        }

        IReadOnlyList<string> allowed = (maySeeCandidates, maySeeOperations) switch
        {
            (true, true) => AuditEntityTypes.All,
            (true, false) => CandidateBucket,
            _ => OperationalBucket,
        };

        if (query.EntityType is not null && !allowed.Contains(query.EntityType))
        {
            return Result<AuditSearchPage>.Failure(
                Error.Forbidden($"{query.EntityType} is outside the caller's audit access."));
        }

        var page = await queries.SearchAsync(
            new AuditSearchFilter(
                query.From, query.To, query.ActorType, query.Action, query.Identifier,
                allowed, query.EntityType, query.Cursor,
                query.PageSize <= 0 ? 50 : Math.Min(query.PageSize, 200)),
            cancellationToken);

        return Result<AuditSearchPage>.Success(page);
    }
}
`````

## src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs","encoding":"utf8","sha256":"f8b56e20cc90ffa84c1e15e10533b6c88969c3b0d5bded6b295f3c642e22e1d3","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Dashboards;

/// <summary>Defines dashboards view for the current use case.</summary>
/// <param name="AwaitingAvailability">The awaiting availability.</param>
/// <param name="NoResponse">The no response.</param>
/// <param name="Slots">The slots.</param>
/// <param name="EmailStatuses">The email statuses.</param>
public sealed record DashboardsView(
    IReadOnlyList<AwaitingAvailabilityRow> AwaitingAvailability,
    IReadOnlyList<NoResponseRow> NoResponse,
    IReadOnlyList<SlotOverviewRow> Slots,
    IReadOnlyList<CandidateEmailStatusView> EmailStatuses);

/// <summary>Staff-facing delivery state for one candidate.</summary>
/// <param name="CandidateId">The candidate whose latest delivery is shown.</param>
/// <param name="TemplateDisplay">Human-readable template name.</param>
/// <param name="SentAt">The latest attempt or pending timestamp.</param>
/// <param name="Status">The durable delivery status.</param>
/// <param name="CanRetry">Whether the current domain state still permits retry.</param>
public sealed record CandidateEmailStatusView(
    Guid CandidateId,
    string TemplateDisplay,
    DateTimeOffset SentAt,
    string Status,
    bool CanRetry);

/// <summary>Defines get dashboards query for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
public sealed record GetDashboardsQuery(Guid StaffUserId);

/// <summary>Defines get dashboards handler for the current use case.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetDashboardsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<DashboardsView>> HandleAsync(
        GetDashboardsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewCandidateDashboards,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<DashboardsView>.Failure(authorized.Error);
        }

        var emailStatuses = await queries.LatestEmailStatusAsync(cancellationToken);

        return Result<DashboardsView>.Success(new DashboardsView(
            await queries.AwaitingAvailabilityAsync(cancellationToken),
            await queries.NoResponseAsync(cancellationToken),
            await queries.SlotsOverviewAsync(cancellationToken),
            emailStatuses
                .Select(e => new CandidateEmailStatusView(
                    e.CandidateId,
                    TemplateDisplayOf(e.TemplateName),
                    e.SentAt,
                    e.Status.ToString(),
                    e.CanRetry))
                .ToList()));
    }

    /// <summary>Area H's template names, in the wording a coordinator reads on /candidates and /dashboards.</summary>
    /// <param name="template">The template.</param>
    public static string TemplateDisplayOf(EmailTemplate template) => template switch
    {
        EmailTemplate.CandidateInvite => "Invite",
        EmailTemplate.BookingConfirmation => "Booking confirmation",
        EmailTemplate.SlotCancelledRebookingNeeded => "Slot cancelled - rebooking needed",
        EmailTemplate.CandidateReinvite => "Re-invite",
        _ => template.ToString(),
    };
}
`````

## src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs","encoding":"utf8","sha256":"f8d497386b7d85d6daf16de1697558c30bf834ca491989f965491269015e256e","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Dashboards;

/// <summary>Slot-only view of every cancellable confirmed window, free of candidate data.</summary>
/// <param name="Slots">One row per confirmed slot with per-appointment-type capacity and aggregate booking count.</param>
public sealed record SlotOperationsView(IReadOnlyList<SlotOverviewRow> Slots);

/// <summary>Query carrying the caller's staff identity for the slot-only operations view.</summary>
/// <param name="StaffUserId">The authenticated staff identity making the request.</param>
public sealed record GetSlotOperationsQuery(Guid StaffUserId);

/// <summary>Returns the slot-only operations view after a view-slot-operations check.</summary>
/// <param name="queries">The queries.</param>
/// <param name="access">The access.</param>
public sealed class GetSlotOperationsHandler(
    IDashboardQueries queries,
    IStaffAccessAuthorizer access)
{
    /// <summary>Authorizes the caller then returns every cancellable slot-overview row.</summary>
    /// <param name="query">The query carrying the caller's staff identity.</param>
    /// <param name="cancellationToken">Propagated to the authorizer and queries.</param>
    /// <returns>The slot-only view, or the authorizer's forbidden failure.</returns>
    public async Task<Result<SlotOperationsView>> HandleAsync(
        GetSlotOperationsQuery query,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            query.StaffUserId,
            StaffCapability.ViewSlotOperations,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<SlotOperationsView>.Failure(authorized.Error);
        }

        var slots = await queries.SlotsOverviewAsync(cancellationToken);
        return Result<SlotOperationsView>.Success(new SlotOperationsView(slots));
    }
}
`````

## src/EventBooking.Application/DependencyInjection.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/DependencyInjection.cs","encoding":"utf8","sha256":"ba995637fc954d038d4ab49f975099d11a7458b9a42ceeea1bd6b10fa0450da7","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Candidates;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Slots;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        CandidatePortalOptions portal)
    {
        services.AddSingleton(portal);

        // Shared services.
        services.AddScoped<EligibleSlotFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<InviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Slot negotiation.
        services.AddScoped<ProposeSlotHandler>();
        services.AddScoped<AcceptProposalHandler>();
        services.AddScoped<ImportConfirmedSlotsHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<GetManagerSlotBoardHandler>();
        services.AddScoped<CancelConfirmedSlotHandler>();
        services.AddScoped<AdjustConfirmedSlotCapacityHandler>();

        // Candidates.
        services.AddScoped<ImportCandidatesHandler>();
        services.AddScoped<SaveCandidateHandler>();
        services.AddScoped<DeleteCandidateHandler>();
        services.AddScoped<ListCandidatesHandler>();
        services.AddScoped<ListEmployeeGroupsHandler>();
        services.AddScoped<CandidateReadinessCalculator>();
        services.AddScoped<GetCandidateReadinessHandler>();
        services.AddScoped<GetCandidateBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetSlotOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();

        // Invites and bookings.
        services.AddScoped<TriggerInviteHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ExpireInvitesHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        services.AddScoped<CancelCandidateBookingHandler>();

        return services;
    }
}
`````

## src/EventBooking.Application/EventBooking.Application.csproj — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/EventBooking.Application.csproj","encoding":"utf8","sha256":"311e4c1a0579494b125214e53fa06ef8a72b91a5d32c0457a748f9e53c590aed","parts":1,"part":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Domain\EventBooking.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>

</Project>
`````

## src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs","encoding":"utf8","sha256":"9733af18cec087533b1c8960d65ecce810f990710df081fcaf856bf1da707947","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;

namespace EventBooking.Application.Invites;

/// <summary>Cancels one pending recovery Invite without touching capacity or appointments.</summary>
/// <param name="StaffUserId">The Coordinator cancelling the recovery Invite.</param>
/// <param name="CandidateId">The candidate route the Invite must belong to.</param>
/// <param name="InviteId">The pending recovery Invite to cancel.</param>
public sealed record CancelRecoveryInviteCommand(Guid StaffUserId, Guid CandidateId, Guid InviteId);

/// <summary>Cancels one Pending recovery Invite without changing capacity or Candidate status.</summary>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class CancelRecoveryInviteHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IAuditLogger audit,
    IUnitOfWork unitOfWork)
{
    /// <summary>Cancels one Pending recovery Invite without changing capacity or Candidate status.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> HandleAsync(
        CancelRecoveryInviteCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such candidate."));
        }

        var invite = await invites.LockForUpdateAsync(command.InviteId, cancellationToken);
        if (invite is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("No such invite."));
        }

        if (invite.RecoveryOfBookingId is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("Only a recovery invite can be cancelled."));
        }

        if (invite.CandidateId != command.CandidateId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this candidate."));
        }

        var root = await bookings.LockForUpdateAsync(invite.RecoveryOfBookingId.Value, cancellationToken);
        if (root is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.NotFound("The original booking no longer exists."));
        }

        if (root.CandidateId != command.CandidateId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This invite does not belong to this candidate."));
        }

        if (invite.Status != InviteStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Conflict("This recovery invite is no longer pending."));
        }

        try
        {
            invite.RotateTokenHash(Guid.NewGuid().ToString("N"));
            invite.CancelRecovery();
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(Error.Validation(ex.Message));
        }

        audit.Record(
            AuditEntityTypes.Invite,
            invite.Id,
            AuditAction.RecoveryInviteCancelled,
            ActorType.Staff,
            command.StaffUserId.ToString(),
            $"root {root.Id}");

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result.Success();
    }
}
`````

## src/EventBooking.Application/Invites/EligibleSlotFinder.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Invites/EligibleSlotFinder.cs","encoding":"utf8","sha256":"c8aef4dccab2e3b09e48079867e06ea2ca312c7dc042e9a11ceb7d9e6791540e","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Invites;

/// <summary>
/// The one place the "which slots may a candidate be offered" rule lives. Task 36 uses it to build
/// an invite; Task 40 uses it to find a single replacement when an option fills up.
/// </summary>
/// <param name="slots">The slots.</param>
/// <param name="clock">The clock.</param>
public sealed class EligibleSlotFinder(IConfirmedSlotRepository slots, IClock clock)
{
    /// <summary>Defines find async for the current use case.</summary>
    /// <param name="requiredAppointmentTypeIds">The required appointment type ids.</param>
    /// <param name="take">The take.</param>
    /// <param name="excludeSlotIds">The exclude slot ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<ConfirmedSlot>> FindAsync(
        IReadOnlyCollection<Guid> requiredAppointmentTypeIds,
        int take,
        IReadOnlyCollection<Guid> excludeSlotIds,
        CancellationToken cancellationToken)
    {
        var today = clock.TodayAtHeadOffice;

        var candidates = await slots.ListActiveAsync(today, cancellationToken);

        return candidates
            .Where(s => !excludeSlotIds.Contains(s.Id))
            .Where(s => s.Window.StartsAfter(today))
            .Where(s => s.HasSpareCapacityForAll(requiredAppointmentTypeIds))
            .OrderBy(s => s.Window)
            .Take(take)
            .ToList();
    }
}
`````

## src/EventBooking.Application/Invites/ExpireInvitesHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Invites/ExpireInvitesHandler.cs","encoding":"utf8","sha256":"087520679fc8b186814729a62072cd491d5cca94517467e10a1d1e7d82e7d08a","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;

namespace EventBooking.Application.Invites;

/// <summary>Defines invite sweep summary for the current use case.</summary>
/// <param name="Expired">The expired.</param>
/// <param name="ReIssued">The re issued.</param>
/// <param name="FlaggedForFollowUp">The flagged for follow up.</param>
public sealed record InviteSweepSummary(int Expired, int ReIssued, int FlaggedForFollowUp);

/// <summary>
/// The scheduled half of the invite engine. Expiry is never triggered by a candidate opening a
/// link — an invite nobody ever opens has to expire too.
/// </summary>
/// <param name="deliveries">Dispatches staged re-invites after each commit.</param>
/// <param name="invites">The invites.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="settings">The settings.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="audit">The audit.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="clock">The clock.</param>
public sealed class ExpireInvitesHandler(
    IInviteRepository invites,
    ICandidateRepository candidates,
    ISystemSettingsRepository settings,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Expires and optionally replaces each due invite under its candidate lifecycle lock.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<InviteSweepSummary> HandleAsync(CancellationToken cancellationToken)
    {
        var configuration = await settings.GetAsync(cancellationToken);
        var due = await invites.ListPendingExpiredAsync(clock.UtcNow, cancellationToken);

        var expired = 0;
        var reIssued = 0;
        var flagged = 0;

        foreach (var dueInvite in due)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

            // The candidate is the lifecycle root. The due-list row is only a work hint and is
            // re-read under the candidate and invite locks before any transition is made.
            var candidate = await candidates.LockForUpdateAsync(dueInvite.CandidateId, cancellationToken);
            if (candidate is null)
            {
                continue;
            }

            var invite = await invites.LockForUpdateAsync(dueInvite.Id, cancellationToken);
            if (invite is null || !invite.IsUsableAt(clock.UtcNow) && invite.Status != Domain.Invites.InviteStatus.Pending)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            if (invite.RecoveryOfBookingId.HasValue)
            {
                if (invite.ExpiresAt > clock.UtcNow)
                {
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                invite.MarkExpired();
                expired++;

                audit.Record(
                    AuditEntityTypes.Invite,
                    invite.Id,
                    AuditAction.InviteExpired,
                    ActorType.System,
                    null,
                    $"retry {invite.RetryCount}");

                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }

                continue;
            }

            if (invite.ExpiresAt > clock.UtcNow || candidate.Status == Domain.Candidates.CandidateStatus.Booked)
            {
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            invite.MarkExpired();
            expired++;

            audit.Record(
                AuditEntityTypes.Invite,
                invite.Id,
                AuditAction.InviteExpired,
                ActorType.System,
                null,
                $"retry {invite.RetryCount}");

            if (invite.RetryCount >= configuration.MaxAutoRetryCount)
            {
                candidate.MarkNoResponse();
                flagged++;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var issueResult = await issuer.IssueInitialAsync(
                candidate,
                invite.RetryCount + 1,
                ActorType.System,
                null,
                isReinvite: true,
                cancellationToken);

            if (issueResult.IsFailure)
            {
                if (candidate.Status == Domain.Candidates.CandidateStatus.Invited)
                {
                    candidate.MarkNoResponse();
                    flagged++;
                    audit.Record(
                        AuditEntityTypes.Invite,
                        invite.Id,
                        AuditAction.InviteExpired,
                        ActorType.System,
                        null,
                        "re-issue failed");
                }

                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }

                continue;
            }

            var issued = issueResult.Value;
            if (issued.Invited)
            {
                reIssued++;
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            if (issued.DispatchPlan is { } plan)
            {
                await deliveries.DispatchClaimedAsync(
                    plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            }
        }

        return new InviteSweepSummary(expired, reIssued, flagged);
    }
}
`````

## src/EventBooking.Application/Invites/InviteIssuer.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Invites/InviteIssuer.cs","encoding":"utf8","sha256":"0767d03c0c368fbd8e385490df63eadd160d178e6b1e5a27d13bf43f66071a86","parts":1,"part":1} -->

`````csharp
using System.Text.Json;
using EventBooking.Application.Abstractions;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Invites;

/// <summary>Reports invite creation and the durable delivery state visible to callers.</summary>
/// <param name="Invited">Whether a new pending invite was created.</param>
/// <param name="InviteId">The new invite identifier, when one was created.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
/// <param name="DeliveryStatus">The durable delivery status, or <c>Unavailable</c> when no invite exists.</param>
/// <param name="DeliveryId">The durable delivery identifier, when one was staged.</param>
public sealed record InviteIssueResult(
    bool Invited,
    Guid? InviteId,
    bool EmailSent,
    string DeliveryStatus = "Unavailable",
    Guid? DeliveryId = null)
{
    internal EmailDispatchPlan? DispatchPlan { get; init; }
}

internal sealed record EmailDispatchPlan(Guid DeliveryId, EmailMessage Message, Action? OnSent = null);

/// <summary>
/// Creates one invite and stages its delivery, or records that there is nothing to offer. Shared by the
/// coordinator trigger (Task 37), the expiry sweep (Task 38) and slot cancellation (Task 42).
/// Never saves or calls a provider — the caller owns the unit of work and dispatches only after commit.
/// </summary>
/// <param name="invites">The invites.</param>
/// <param name="groups">The groups.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="settings">The settings.</param>
/// <param name="tokens">The tokens.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
/// <param name="portal">The portal.</param>
public sealed class InviteIssuer(
    IInviteRepository invites,
    IEmployeeGroupRepository groups,
    EligibleSlotFinder slotFinder,
    ISystemSettingsRepository settings,
    ITokenService tokens,
    EmailDeliveryService deliveries,
    IAuditLogger audit,
    IClock clock,
    CandidatePortalOptions portal)
{
    /// <summary>Issues an initial Invite from a locked, validated Candidate requirement set.</summary>
    /// <param name="candidate">The candidate whose lifecycle is already locked by the caller.</param>
    /// <param name="retryCount">The automated retry number to persist on the new invite.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="isReinvite">Whether the reminder template should be used.</param>
    /// <param name="cancellationToken">Cancels repository and slot reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueInitialAsync(
        Candidate candidate,
        int retryCount,
        ActorType actorType,
        string? actorId,
        bool isReinvite,
        CancellationToken cancellationToken)
    {
        if (!candidate.EmployeeGroupId.HasValue)
        {
            return Result<InviteIssueResult>.Failure(Error.CandidateReconciliationRequired(
                "Assign an employee group before issuing an invite."));
        }

        var group = await groups.GetAsync(candidate.EmployeeGroupId.Value, cancellationToken);
        var mapping = group?.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        var current = candidate.RequiredAppointmentTypeIds
            .Order()
            .ToList();
        if (group is null || !group.IsActive || mapping!.Count == 0 || !mapping.SequenceEqual(current))
        {
            return Result<InviteIssueResult>.Failure(Error.CandidateRequirementSnapshotMismatch(
                "The candidate requirements do not match their employee group."));
        }

        var pending = await invites.GetPendingForCandidateAsync(candidate.Id, cancellationToken);
        if (pending?.Status == Domain.Invites.InviteStatus.Pending)
        {
            pending.MarkSuperseded();
        }

        InviteIssueResult issued;
        try
        {
            var options = await slotFinder.FindAsync(
                mapping,
                Invite.RequiredOptionCount,
                [],
                cancellationToken);

            if (options.Count < Invite.RequiredOptionCount)
            {
                candidate.MarkAwaitingAvailability();
                return Result<InviteIssueResult>.Success(new InviteIssueResult(false, null, false));
            }

            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateInitial(
                inviteId,
                candidate.Id,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                options.Select(o => o.Id),
                mapping,
                retryCount);

            invites.Add(invite);
            candidate.MarkInvited();

            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.InviteCreated,
                actorType,
                actorId,
                $"retry {retryCount}");

            var message = CandidateEmailComposer.Invite(
                candidate,
                mapping,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite,
                isRecovery: false);

            var delivery = deliveries.StagePending(candidate.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    /// <summary>Issues a recovery Invite for already-selected no-show types from locked journey state.</summary>
    /// <param name="candidate">The candidate whose lifecycle is already locked by the caller.</param>
    /// <param name="rootBookingId">The original journey-root Booking the recovery belongs to.</param>
    /// <param name="selectedTypeIds">The recoverable snapshot, already revalidated under lock.</param>
    /// <param name="options">Exactly three future slots with capacity for every selected type.</param>
    /// <param name="actorType">The actor recorded for invite creation.</param>
    /// <param name="actorId">The actor identifier, when a staff identity caused the change.</param>
    /// <param name="cancellationToken">Cancels repository reads.</param>
    /// <returns>A pending delivery plan the caller dispatches after commit.</returns>
    public async Task<Result<InviteIssueResult>> IssueRecoveryAsync(
        Candidate candidate,
        Guid rootBookingId,
        IReadOnlyList<Guid> selectedTypeIds,
        IReadOnlyList<ConfirmedSlot> options,
        ActorType actorType,
        string? actorId,
        CancellationToken cancellationToken)
    {
        if (options.Count != Invite.RequiredOptionCount)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(
                "A recovery invite must offer exactly three slot options."));
        }

        InviteIssueResult issued;
        try
        {
            var configuration = await settings.GetAsync(cancellationToken);

            var inviteId = Guid.NewGuid();
            var token = tokens.Issue(inviteId);

            var invite = Invite.CreateRecovery(
                inviteId,
                candidate.Id,
                rootBookingId,
                token.TokenHash,
                clock.UtcNow.AddDays(configuration.InviteExpiryDays),
                options.Select(o => o.Id),
                selectedTypeIds);

            invites.Add(invite);

            var codes = selectedTypeIds
                .Select(AppointmentTypeIds.CodeOf)
                .Order(StringComparer.Ordinal)
                .ToList();
            audit.Record(
                AuditEntityTypes.Invite,
                inviteId,
                AuditAction.RecoveryInviteCreated,
                actorType,
                actorId,
                JsonSerializer.Serialize(
                    new RecoveryInviteAudit(rootBookingId, codes),
                    RecoveryAuditJson));

            var message = CandidateEmailComposer.Invite(
                candidate,
                selectedTypeIds,
                options,
                $"{portal.BaseUrl}/book/{token.Token}",
                isReinvite: false,
                isRecovery: true);

            var delivery = deliveries.StagePending(candidate.Id, message.Template, inviteId: inviteId);
            deliveries.ClaimForDispatch(delivery);
            var plan = new EmailDispatchPlan(
                delivery.Id,
                message,
                () => audit.Record(
                    AuditEntityTypes.Invite,
                    inviteId,
                    AuditAction.InviteSent,
                    actorType,
                    actorId,
                    $"invite {inviteId}"));

            issued = new InviteIssueResult(true, inviteId, false, EmailStatus.Pending.ToString(), delivery.Id)
            {
                DispatchPlan = plan,
            };
        }
        catch (DomainException ex)
        {
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        return Result<InviteIssueResult>.Success(issued);
    }

    private static readonly JsonSerializerOptions RecoveryAuditJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record RecoveryInviteAudit(Guid RootBookingId, IReadOnlyList<string> RequirementCodes);
}
`````

## src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs","encoding":"utf8","sha256":"8bbbf60061fb46b6d287f126bb5d536cc3f120b23b4920e837a50ee9e816941e","parts":1,"part":1} -->

`````csharp
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Invites;

/// <summary>One non-cancelled Booking Appointment considered for recovery eligibility.</summary>
/// <param name="BookingAppointmentId">The stable appointment-record identifier.</param>
/// <param name="AppointmentTypeId">The required appointment type delivered by the attempt.</param>
/// <param name="Status">The attempt's independent operational status.</param>
/// <param name="BookingCreatedAt">When the parent Booking was created, ordering repeat attempts.</param>
public sealed record RecoveryAttempt(
    Guid BookingAppointmentId,
    Guid AppointmentTypeId,
    BookingAppointmentStatus Status,
    DateTimeOffset BookingCreatedAt);

/// <summary>Selects the current requirement types whose latest attempt is an unsatisfied no-show.</summary>
public sealed class RecoveryRequirementSelector
{
    /// <summary>Returns every current type whose latest attempt is NoShow and none is Completed.</summary>
    /// <param name="currentRequirementTypeIds">The candidate's current derived requirement set.</param>
    /// <param name="attempts">Non-cancelled attempts across the journey, in any order.</param>
    /// <param name="typesAlreadyPendingRecovery">Types a pending recovery already covers.</param>
    /// <returns>The recoverable type identifiers in stable order.</returns>
    public IReadOnlyList<Guid> Select(
        IReadOnlyCollection<Guid> currentRequirementTypeIds,
        IReadOnlyCollection<RecoveryAttempt> attempts,
        IReadOnlyCollection<Guid> typesAlreadyPendingRecovery)
    {
        var current = currentRequirementTypeIds.ToHashSet();
        var pending = typesAlreadyPendingRecovery.ToHashSet();

        return attempts
            .Where(attempt => current.Contains(attempt.AppointmentTypeId))
            .Where(attempt => !pending.Contains(attempt.AppointmentTypeId))
            .GroupBy(attempt => attempt.AppointmentTypeId)
            .Where(group => !group.Any(attempt => attempt.Status == BookingAppointmentStatus.Completed))
            .Where(group => Latest(group).Status == BookingAppointmentStatus.NoShow)
            .Select(group => group.Key)
            .Order()
            .ToList();
    }

    private static RecoveryAttempt Latest(IEnumerable<RecoveryAttempt> attempts) =>
        attempts
            .OrderBy(attempt => attempt.BookingCreatedAt)
            .ThenBy(attempt => attempt.BookingAppointmentId)
            .Last();
}
`````

## src/EventBooking.Application/Invites/StartRecoveryHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Invites/StartRecoveryHandler.cs","encoding":"utf8","sha256":"4acb26446c4d20db04d6b667292d9537803a99edcfbe157ad7e7f95cca2929e6","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Bookings;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Bookings;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Invites;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>Starts one recovery Invite for a candidate with missed appointments.</summary>
/// <param name="StaffUserId">The Coordinator starting the recovery.</param>
/// <param name="CandidateId">The booked candidate whose no-shows are recovered.</param>
public sealed record StartRecoveryCommand(Guid StaffUserId, Guid CandidateId);

/// <summary>Reports recovery Invite creation, or the awaiting-availability outcome.</summary>
/// <param name="InviteId">The new recovery Invite identifier, or empty when no slots exist.</param>
/// <param name="AppointmentTypeIds">The recoverable snapshot offered, or awaiting availability.</param>
/// <param name="EmailSent">Whether the post-commit provider attempt completed successfully.</param>
public sealed record StartRecoveryResult(
    Guid InviteId,
    IReadOnlyList<Guid> AppointmentTypeIds,
    bool EmailSent);

/// <summary>
/// Gathers every currently recoverable no-show type into one recovery Invite under the
/// Candidate-first lifecycle lock order, revalidating eligibility after the locks because
/// preflight reads are never authoritative.
/// </summary>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="invites">The invites.</param>
/// <param name="bookings">The bookings.</param>
/// <param name="appointments">The appointments.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="slotFinder">The slot finder.</param>
/// <param name="deliveries">The deliveries.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class StartRecoveryHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    IInviteRepository invites,
    IBookingRepository bookings,
    IBookingAppointmentRepository appointments,
    InviteIssuer issuer,
    EligibleSlotFinder slotFinder,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Creates one recovery Invite after revalidating recoverability under lifecycle locks.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<StartRecoveryResult>> HandleAsync(
        StartRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageCandidates,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<StartRecoveryResult>.Failure(authorized.Error);
        }

        var located = await candidates.GetAsync(command.CandidateId, cancellationToken);
        if (located is null)
        {
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such candidate."));
        }

        var preflight = await SelectRecoverableAsync(located, null, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.NotFound("No such candidate."));
        }

        var pending = await invites.LockPendingListForCandidateAsync(candidate.Id, cancellationToken);
        var pendingRecoveries = pending
            .Where(invite => invite.RecoveryOfBookingId.HasValue)
            .ToList();

        var original = await bookings.LockActiveOriginalForCandidateAsync(candidate.Id, cancellationToken);
        if (original is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No active booking has a recoverable missed appointment."));
        }

        var activeRecovery = await bookings.LockActiveRecoveryAsync(original.Id, cancellationToken);
        if (pendingRecoveries.Count > 0 || activeRecovery is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this candidate."));
        }

        var journey = await bookings.ListJourneyAsync(original.Id, cancellationToken);
        var selected = await SelectRecoverableAsync(candidate, journey, pendingRecoveries, cancellationToken);
        if (!selected.SequenceEqual(preflight))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryStateChanged(
                "Recovery eligibility changed while starting the recovery."));
        }

        if (selected.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryNotAvailable(
                "No current requirement has a recoverable missed appointment."));
        }

        var options = await slotFinder.FindAsync(
            selected,
            Invite.RequiredOptionCount,
            [],
            cancellationToken);
        if (options.Count < Invite.RequiredOptionCount)
        {
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return Result<StartRecoveryResult>.Success(new StartRecoveryResult(Guid.Empty, selected, false));
        }

        InviteIssueResult issued;
        try
        {
            var issueResult = await issuer.IssueRecoveryAsync(
                candidate,
                original.Id,
                selected,
                options,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<StartRecoveryResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<StartRecoveryResult>.Failure(Error.RecoveryAlreadyPending(
                "A recovery is already pending for this candidate."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var emailSent = false;
        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);
            emailSent = status == EmailStatus.Sent;
        }

        return Result<StartRecoveryResult>.Success(
            new StartRecoveryResult(issued.InviteId!.Value, selected, emailSent));
    }

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Candidate candidate,
        IReadOnlyList<Booking>? journey,
        CancellationToken cancellationToken) =>
        await SelectRecoverableAsync(candidate, journey, [], cancellationToken);

    private async Task<IReadOnlyList<Guid>> SelectRecoverableAsync(
        Candidate candidate,
        IReadOnlyList<Booking>? journey,
        IReadOnlyList<Invite> pendingRecoveries,
        CancellationToken cancellationToken)
    {
        if (journey is null or { Count: 0 })
        {
            var rootId = await LocateRootBookingIdAsync(candidate.Id, cancellationToken);
            if (rootId is null)
            {
                return [];
            }

            journey = await bookings.ListJourneyAsync(rootId.Value, cancellationToken);
        }

        var ids = journey.Select(booking => booking.Id).ToList();
        var rows = ids.Count == 0
            ? []
            : await appointments.ListForBookingsAsync(ids, cancellationToken);

        var attempts = RecoveryConfirmationValidator.BuildAttempts(journey, rows);

        var covered = pendingRecoveries
            .SelectMany(invite => invite.RequiredAppointmentTypeIds)
            .Distinct()
            .ToList();

        return new RecoveryRequirementSelector().Select(
            candidate.RequiredAppointmentTypeIds, attempts, covered);
    }

    private async Task<Guid?> LocateRootBookingIdAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        var active = await bookings.GetActiveForCandidateAsync(candidateId, cancellationToken);
        return active is null ? null : active.RecoveryOfBookingId ?? active.Id;
    }
}
`````

## src/EventBooking.Application/Invites/TriggerInviteHandler.cs — 1/1

<!-- port-file: {"path":"src/EventBooking.Application/Invites/TriggerInviteHandler.cs","encoding":"utf8","sha256":"10c563fa4ed90355c038e1fc67eb9bb9aab1514bb69c78d71d793168679646d3","parts":1,"part":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Application.Notifications;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.Common;
using EventBooking.Domain.Notifications;

namespace EventBooking.Application.Invites;

/// <summary>A null staff identity means the system triggered this, not a person.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="CandidateId">The candidate id.</param>
public sealed record TriggerInviteCommand(Guid? StaffUserId, Guid CandidateId);

/// <summary>Issues an invite while serializing all transitions for the candidate lifecycle.</summary>
/// <param name="deliveries">Dispatches the staged invite after commit.</param>
/// <param name="candidates">The candidates.</param>
/// <param name="access">The access.</param>
/// <param name="issuer">The issuer.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class TriggerInviteHandler(
    ICandidateRepository candidates,
    IStaffAccessAuthorizer access,
    InviteIssuer issuer,
    EmailDeliveryService deliveries,
    IUnitOfWork unitOfWork)
{
    /// <summary>Issues or replaces a candidate invite under the candidate row lock.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<InviteIssueResult>> HandleAsync(
        TriggerInviteCommand command,
        CancellationToken cancellationToken)
    {
        var actorType = ActorType.System;
        string? actorId = null;

        if (command.StaffUserId is not null)
        {
            var authorized = await access.AuthorizeAsync(
                command.StaffUserId.Value,
                StaffCapability.ManageCandidates,
                null,
                cancellationToken);
            if (authorized.IsFailure)
            {
                return Result<InviteIssueResult>.Failure(authorized.Error);
            }

            actorType = ActorType.Staff;
            actorId = command.StaffUserId.Value.ToString();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var candidate = await candidates.LockForUpdateAsync(command.CandidateId, cancellationToken);
        if (candidate is null)
        {
            return Result<InviteIssueResult>.Failure(Error.NotFound("No such candidate."));
        }

        if (candidate.Status == CandidateStatus.Booked)
        {
            return Result<InviteIssueResult>.Failure(Error.Conflict("This candidate is already booked."));
        }

        InviteIssueResult issued;
        try
        {
            // A manually triggered invite starts the retry count again: a coordinator pressing
            // "Re-invite Now" means start over, not continue chasing.
            var issueResult = await issuer.IssueInitialAsync(
                candidate, 0, actorType, actorId, isReinvite: false, cancellationToken);
            if (issueResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<InviteIssueResult>.Failure(issueResult.Error);
            }

            issued = issueResult.Value;
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(Error.Validation(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<InviteIssueResult>.Failure(
                Error.Conflict("This candidate already has a pending invite."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (issued.DispatchPlan is { } plan)
        {
            var status = await deliveries.DispatchClaimedAsync(
                plan.DeliveryId, plan.Message, cancellationToken, plan.OnSent);

            issued = issued with
            {
                EmailSent = status == EmailStatus.Sent,
                DeliveryStatus = status.ToString(),
            };
        }

        return Result<InviteIssueResult>.Success(issued);
    }
}
`````
