using EventBooking.Application.Abstractions;
using EventBooking.Application.Events;
using EventBooking.Application.Negotiation;
using EventBooking.Application.ReferenceData;
using EventBooking.Domain.Time;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Locking;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventBookingPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered as a factory, with a scoped context created from it. Task 51's email sender
        // needs a context of its own that is not tied to the request's unit of work, and this is
        // the pattern that gives it one without a second registration of the context type.
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(ConnectionPooling.WithDefaultMaxPoolSize(connectionString)));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        // Scoped: one tracker per DbContext, because the order is a property of one connection's
        // transaction, not of the process.
        services.AddScoped<TransactionLocks>();
        services.AddScoped<RowLocks>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IReferenceDataBlockingQueries, ReferenceDataBlockingQueries>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<IEventProposalRepository, EventProposalRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventCapacityRepository, EventCapacityRepository>();
        services.AddScoped<IAttendeeRepository, AttendeeRepository>();
        services.AddScoped<IAttendeeGroupRepository, AttendeeGroupRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingAppointmentRepository, BookingAppointmentRepository>();
        services.AddScoped<IStaffAccessProfileRepository, StaffAccessProfileRepository>();
        services.AddScoped<IStaffIdentityRepository, StaffIdentityRepository>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IAttendeeListQueries, AttendeeListQueries>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAuditSearchQueries, AuditSearchQueries>();
        services.AddScoped<IEventReadQueries, Persistence.Queries.EventReadQueries>();
        services.AddScoped<IEventProposalListQueries, Persistence.Queries.EventProposalListQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<IAttendeeReadinessQueries, AttendeeReadinessQueries>();
        services.AddScoped<IAttendeeBookingQueries, AttendeeBookingQueries>();
        services.AddScoped<IEventEligibilityQuery, EventEligibilityQuery>();
        services.AddScoped<IWorkspaceQueries, WorkspaceQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEventWindowZones, NodaTimeEventWindowZones>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
