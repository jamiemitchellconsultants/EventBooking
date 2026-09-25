using EventBooking.Application.Access;
using EventBooking.Application.Appointments;
using EventBooking.Application.Audit;
using EventBooking.Application.Bookings;
using EventBooking.Application.Attendees;
using EventBooking.Application.Dashboards;
using EventBooking.Application.EventGroups;
using EventBooking.Application.Invites;
using EventBooking.Application.Jobs;
using EventBooking.Application.Notifications;
using EventBooking.Application.Recovery;
using EventBooking.Application.Settings;
using EventBooking.Application.Events;
using EventBooking.Application.Negotiation;
using EventBooking.Application.ReferenceData;
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
        services.AddScoped<IInviteIssuer, InviteIssuer>();
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
        services.AddScoped<ListEventsHandler>();
        services.AddScoped<ListCancellableEventsHandler>();
        services.AddScoped<GetEventHandler>();
        services.AddScoped<ListEventProposalsHandler>();

        // Attendees.
        services.AddScoped<ImportAttendeesHandler>();
        services.AddScoped<SaveAttendeeHandler>();
        services.AddScoped<DeleteAttendeeHandler>();
        services.AddScoped<ListAttendeesHandler>();
        services.AddScoped<ListAssignableAttendeeGroupsHandler>();
        services.AddScoped<AttendeeReadinessCalculator>();
        services.AddScoped<GetAttendeeReadinessHandler>();
        services.AddScoped<GetAttendeeBookingsHandler>();
        services.AddScoped<GetDashboardsHandler>();
        services.AddScoped<GetEventOperationsHandler>();
        services.AddScoped<GetAuditHistoryHandler>();
        services.AddScoped<GetAuditSearchHandler>();
        services.AddScoped<SearchAuditHandler>();
        services.AddScoped<AttendeeHistoryHandler>();
        services.AddScoped<EventHistoryHandler>();

        // Reference data.
        services.AddScoped<CreateLocationHandler>();
        services.AddScoped<UpdateLocationHandler>();
        services.AddScoped<ListLocationsHandler>();
        services.AddScoped<CreateAppointmentTypeHandler>();
        services.AddScoped<UpdateAppointmentTypeHandler>();
        services.AddScoped<ListAppointmentTypesHandler>();
        services.AddScoped<CreateAttendeeGroupHandler>();
        services.AddScoped<UpdateAttendeeGroupHandler>();
        services.AddScoped<ListAttendeeGroupsHandler>();

        // Event groups.
        services.AddScoped<ManageEventGroupHandler>();
        services.AddScoped<ListEventGroupsHandler>();

        // Self-registrations.
        services.AddScoped<SelfRegistrations.SubmitSelfRegistrationHandler>();
        services.AddScoped<SelfRegistrations.ConfirmSelfRegistrationHandler>();

        // Administration.
        services.AddScoped<AdminSettingsHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<StaffScopeHandler>();
        services.AddScoped<SyncStaffAccessProfileRolesHandler>();

        // Appointments.
        services.AddScoped<GetAppointmentWorkspaceHandler>();
        services.AddScoped<AppointmentRosterCsvFormatter>();
        services.AddScoped<RecoveryBookingOutcomeCoordinator>();
        services.AddScoped<UpdateBookingAppointmentStatusHandler>();
        services.AddScoped<ListWorkspaceEventsHandler>();
        services.AddScoped<GetWorkspaceRosterHandler>();
        services.AddScoped<DownloadRosterHandler>();

        // Invites and bookings.
        services.AddScoped<InviteAttendeeHandler>();
        services.AddScoped<ListInviteLocationsHandler>();
        services.AddScoped<ExpireInviteHandler>();
        services.AddScoped<TopUpInviteOptionsHandler>();
        services.AddScoped<CountEligibleEventsHandler>();
        services.AddScoped<StartRecoveryHandler>();
        services.AddScoped<CancelRecoveryInviteHandler>();
        services.AddScoped<ConcludeRecoveryHandler>();
        services.AddScoped<RetryEmailHandler>();
        services.AddScoped<ViewInviteHandler>();
        services.AddScoped<ViewBookingHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<CancelBookingByAttendeeHandler>();
        services.AddScoped<CancelBookingByCoordinatorHandler>();

        // Background jobs.
        services.AddScoped<ISweepSteps, SweepSteps>();
        services.AddScoped<SweepRunner>();

        return services;
    }
}
