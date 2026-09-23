using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.Invites;
using EventBooking.Application.Notifications;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using EventBooking.Application.Negotiation;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Application;

/// <summary>Defines application service collection extensions for the current use case.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Defines add event booking application for the current use case.</summary>
    /// <param name="services">The services.</param>
    /// <param name="portal">The portal.</param>
    /// <param name="staffIdPolicy">The deployment staff-number validation policy.</param>
    public static IServiceCollection AddEventBookingApplication(
        this IServiceCollection services,
        AttendeePortalOptions portal,
        StaffIdPolicy? staffIdPolicy = null)
    {
        services.AddSingleton(portal);
        services.AddSingleton(staffIdPolicy ?? new StaffIdPolicy());

        // Shared services.
        services.AddScoped<EligibleEventFinder>();
        services.AddScoped<EmailDeliveryService>();
        services.AddScoped<IInviteIssuer, InviteIssuer>();
        services.AddScoped<LegacyInviteIssuer>();
        services.AddScoped<BookingCanceller>();
        services.AddScoped<IStaffAccessAuthorizer, StaffAccessAuthorizer>();
        services.AddScoped<StaffAccessHandler>();

        // Event negotiation.
        services.AddScoped<ProposeEventHandler>();
        services.AddScoped<RecordAcceptanceHandler>();
        services.AddScoped<WithdrawAcceptanceHandler>();
        services.AddScoped<WithdrawProposalHandler>();
        services.AddScoped<NegotiationBoardHandler>();
        services.AddScoped<CancelEventHandler>();
        services.AddScoped<AdjustEventCapacityHandler>();

        // Attendees.
        services.AddScoped<ImportAttendeesHandler>();
        services.AddScoped<SaveAttendeeHandler>();
        services.AddScoped<DeleteAttendeeHandler>();
        services.AddScoped<ListAttendeesHandler>();
        services.AddScoped<ListAttendeeGroupsHandler>();
        services.AddScoped<AttendeeReadinessCalculator>();
        services.AddScoped<GetAttendeeReadinessHandler>();
        services.AddScoped<GetAttendeeBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetEventOperationsHandler>();
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
        services.AddScoped<InviteAttendeeHandler>();
        services.AddScoped<ExpireInviteHandler>();
        services.AddScoped<TopUpInviteOptionsHandler>();
        services.AddScoped<CountEligibleEventsHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingByAttendeeHandler>();
        services.AddScoped<CancelBookingByCoordinatorHandler>();

        return services;
    }
}
