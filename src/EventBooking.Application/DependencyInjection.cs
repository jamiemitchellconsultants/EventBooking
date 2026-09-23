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
