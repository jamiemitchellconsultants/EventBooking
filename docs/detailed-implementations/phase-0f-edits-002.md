# 00f — Retire the single-site configuration, edits 2 (Task 3d)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## after — src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs","beforeSha":"0c2d5c34cba4c8c83218eb7e00934f7377f75bf6bb7113ddb55332bbf63ef326","afterSha":"5e205b43eb7a35a17d71d9daa1f50097c7a98befe5518ae9bdac4224eb98828b","side":"after","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Notifications;

/// <summary>
/// Pure text composition for the 4 attendee emails. No clock, no repository, no mail service —
/// every output is a function of the arguments, so the wording can be asserted in a unit test.
/// </summary>
public static class AttendeeEmailComposer
{
    /// <summary>Formats a four-hour event window using invariant, human-readable wording.</summary>
    /// <param name="window">The window.</param>
    public static string FormatWindow(EventWindow window) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0:dddd d MMM yyyy}, {1:HH\\:mm}-{2:HH\\:mm}",
            window.Date,
            window.StartTime,
            window.EndTime);

    /// <summary>Composes an initial, reminder, or recovery Invite from persisted type IDs.</summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="options">The options.</param>
    /// <param name="bookingUrl">The booking url.</param>
    /// <param name="isReinvite">The is reinvite.</param>
    /// <param name="isRecovery">The is recovery.</param>
    public static EmailMessage Invite(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        IReadOnlyList<Event> options,
        string bookingUrl,
        bool isReinvite,
        bool isRecovery)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();

        if (isRecovery)
        {
            text.AppendLine(
                appointmentTypeIds.Count == 1
                    ? "You have a missed appointment, so here are new times to complete it."
                    : "You have missed appointments, so here are new times to complete them.");
        }
        else if (isReinvite)
        {
            text.AppendLine(
                "We have not heard back about your appointments, so here are the latest available times.");
        }
        else
        {
            text.AppendLine("Please choose one of the following times for your appointments.");
        }

        text.AppendLine();
        text.AppendLine($"Appointments: {types}");
        text.AppendLine();

        foreach (var option in options)
        {
            text.AppendLine($"  - {FormatWindow(option.Window)}");
        }

        text.AppendLine();
        text.AppendLine("Choose your time here:");
        text.AppendLine(bookingUrl);

        return new EmailMessage(
            attendee.Id,
            attendee.Email,
            attendee.Name,
            isReinvite ? EmailTemplate.AttendeeReinvite : EmailTemplate.AttendeeInvite,
            isReinvite
                ? "Reminder: choose a time for your appointments"
                : "Choose a time for your appointments",
            text.ToString(),
            AsHtml(text.ToString(), bookingUrl, "Choose your time"));
    }

    /// <summary>Composes confirmation and names every booked snapshot type.</summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="manageUrl">The manage url.</param>
    /// <param name="portal">The portal.</param>
    public static EmailMessage BookingConfirmation(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        Event eventItem,
        string manageUrl,
        AttendeePortalOptions portal)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();
        text.AppendLine("Your appointments are confirmed for:");
        text.AppendLine($"  {FormatWindow(eventItem.Window)}");
        text.AppendLine();
        text.AppendLine($"Appointments: {types}");
        text.AppendLine();
        text.AppendLine("Need to change or cancel? Use this link:");
        text.AppendLine(manageUrl);
        text.AppendLine();
        text.AppendLine($"Any questions, contact {portal.CoordinatorContact}.");

        return new EmailMessage(
            attendee.Id,
            attendee.Email,
            attendee.Name,
            EmailTemplate.BookingConfirmation,
            "Your appointment is confirmed",
            text.ToString(),
            AsHtml(text.ToString(), manageUrl, "Cancel or reschedule"));
    }

    /// <summary>
    /// Composes a cancellation notice whose recovery wording reflects whether a replacement invite
    /// was actually delivered. Pending or failed replacement delivery receives neutral wording.
    /// </summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="replacementInviteSent">The replacement invite sent.</param>
    public static EmailMessage EventCancelled(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        Event eventItem,
        bool replacementInviteSent = false)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();
        text.AppendLine(
            $"We are sorry — your {AppointmentNoun(appointmentTypeIds.Count)} on {FormatWindow(eventItem.Window)} has had to be cancelled.");
        text.AppendLine();
        text.AppendLine($"Affected appointments: {types}");
        text.AppendLine();
        if (replacementInviteSent)
        {
            text.AppendLine("A new invitation with fresh times is on its way to you.");
        }
        else
        {
            text.AppendLine("The recruitment team will contact you with the next available times.");
        }

        return new EmailMessage(
            attendee.Id,
            attendee.Email,
            attendee.Name,
            EmailTemplate.EventCancelledRebookingNeeded,
            "Your appointment time has been cancelled",
            text.ToString(),
            AsHtml(text.ToString(), null, null));
    }

    /// <summary>Names snapshot types in deterministic code order for every template.</summary>
    private static string FormatTypes(IReadOnlyCollection<Guid> appointmentTypeIds) =>
        string.Join(
            ", ",
            appointmentTypeIds
                .Select(id => (Code: AppointmentTypeIds.CodeOf(id), Name: AppointmentTypeIds.NameOf(id)))
                .OrderBy(entry => entry.Code, StringComparer.Ordinal)
                .Select(entry => entry.Name));

    /// <summary>Uses one/appointments grammar shared by every template.</summary>
    private static string AppointmentNoun(int count) =>
        count == 1 ? "appointment" : "appointments";

    private static string AsHtml(string text, string? actionUrl, string? actionLabel)
    {
        var body = new StringBuilder();
        body.AppendLine("<html><body style=\"font-family:sans-serif;font-size:15px\">");

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            body.AppendLine(line.Length == 0 ? "<br />" : $"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>");
        }

        if (actionUrl is not null && actionLabel is not null)
        {
            body.AppendLine($"<p><a href=\"{actionUrl}\">{actionLabel}</a></p>");
        }

        body.AppendLine("</body></html>");
        return body.ToString();
    }
}
`````

## before — src/EventBooking.Application/Notifications/AttendeePortalOptions.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Notifications/AttendeePortalOptions.cs","beforeSha":"974954bce97659c98e38f7f37e8ca31763d46b30ea1c9e756989cdd6f0a18696","afterSha":"cc8cece88e45b73ae5b1847173c9b65dc70382b75c5e12b5e81f31b78fef1ef8","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the attendee-facing emails and pages need. Bound
/// from configuration in Task 55 and injected as a singleton.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="TransitionalLocationAddress">The transitional location address.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record AttendeePortalOptions(
    string BaseUrl,
    string TransitionalLocationAddress,
    string CoordinatorContact);
`````

## after — src/EventBooking.Application/Notifications/AttendeePortalOptions.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Application/Notifications/AttendeePortalOptions.cs","beforeSha":"974954bce97659c98e38f7f37e8ca31763d46b30ea1c9e756989cdd6f0a18696","afterSha":"cc8cece88e45b73ae5b1847173c9b65dc70382b75c5e12b5e81f31b78fef1ef8","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Application.Notifications;

/// <summary>
/// The handful of deployment-specific strings the attendee-facing emails and pages need. Bound
/// from configuration at startup and injected as a singleton. Addresses belong to a
/// <c>Location</c>, not to the deployment.
/// </summary>
/// <param name="BaseUrl">The base url.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
public sealed record AttendeePortalOptions(
    string BaseUrl,
    string CoordinatorContact);
`````

## before — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"e8e1c7f56dd7943b0d7a41eb4e2c5c204e7e7a49d7ab026e54f599e6c0d9f4e4","afterSha":"d75101dcd6f83e10f40e686d59c854f32fb59095b7cd07de7bb144ae1baa3eb2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
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
        services.AddSingleton<StatusStampingInterceptor>();
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<StatusStampingInterceptor>()));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
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
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<IAttendeeReadinessQueries, AttendeeReadinessQueries>();
        services.AddScoped<IAttendeeBookingQueries, AttendeeBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        TransitionalLocationOptions transitionalLocation,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(transitionalLocation);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## after — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"e8e1c7f56dd7943b0d7a41eb4e2c5c204e7e7a49d7ab026e54f599e6c0d9f4e4","afterSha":"d75101dcd6f83e10f40e686d59c854f32fb59095b7cd07de7bb144ae1baa3eb2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
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
        services.AddSingleton<StatusStampingInterceptor>();
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<StatusStampingInterceptor>()));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
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
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<IAttendeeReadinessQueries, AttendeeReadinessQueries>();
        services.AddScoped<IAttendeeBookingQueries, AttendeeBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        ClockOptions clock,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(clock);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## after — src/EventBooking.Infrastructure/Time/ClockOptions.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Time/ClockOptions.cs","beforeSha":null,"afterSha":"dd0a180142298513f1bbea030f26e52d66c9c930cb47e8a0daaf488a5e471522","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Infrastructure.Time;

/// <summary>
/// The one time zone every date rule uses until Task 4 gives each <c>Location</c> its own zone.
/// An identifier the host operating system recognises.
/// </summary>
/// <param name="TimeZoneId">The IANA time-zone identifier.</param>
public sealed record ClockOptions(string TimeZoneId);
`````

## before — src/EventBooking.Infrastructure/Time/SystemClock.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Infrastructure/Time/SystemClock.cs","beforeSha":"9b0c6876b720fc8bb6b42618d7a12bfe5a025b1ba30a50fab63005aa908de176","afterSha":"baa3f95703d77cce1bf07cb25067d074131c5cef6c37f8ca620706f48711f006","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _transitionalLocation;

    public SystemClock(TransitionalLocationOptions options)
    {
        // Resolved once, at startup: a bad configuration value should stop the host coming up
        // rather than fail the first time somebody reads the date.
        _transitionalLocation = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, _transitionalLocation);

    public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(NowAtTransitionalLocation.DateTime);

    public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => LocalDateOf(instant, _transitionalLocation);

    /// <inheritdoc/>
    public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, _transitionalLocation);

    public static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
`````

## after — src/EventBooking.Infrastructure/Time/SystemClock.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Infrastructure/Time/SystemClock.cs","beforeSha":"9b0c6876b720fc8bb6b42618d7a12bfe5a025b1ba30a50fab63005aa908de176","afterSha":"baa3f95703d77cce1bf07cb25067d074131c5cef6c37f8ca620706f48711f006","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;

namespace EventBooking.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _transitionalLocation;

    public SystemClock(ClockOptions options)
    {
        // Resolved once, at startup: a bad configuration value should stop the host coming up
        // rather than fail the first time somebody reads the date.
        _transitionalLocation = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
    }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    public DateTimeOffset NowAtTransitionalLocation => TimeZoneInfo.ConvertTime(UtcNow, _transitionalLocation);

    public DateOnly TodayAtTransitionalLocation => DateOnly.FromDateTime(NowAtTransitionalLocation.DateTime);

    public DateOnly DateAtTransitionalLocation(DateTimeOffset instant) => LocalDateOf(instant, _transitionalLocation);

    /// <inheritdoc/>
    public DateTimeOffset InstantAtTransitionalLocation(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, _transitionalLocation);

    public static DateOnly LocalDateOf(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
`````

## before — src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs","beforeSha":"0044d6db9a5d1a5ae505e8f5ea1d9d1bd42ede7408e7d7cae369bf8284d4fb8f","afterSha":null,"side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Infrastructure.Time;

/// <summary>Single site, single zone. An identifier the host operating system recognises.</summary>
public sealed record TransitionalLocationOptions(string TimeZoneId);
`````

## before — src/EventBooking.Mcp/appsettings.Local.json — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Mcp/appsettings.Local.json","beforeSha":"19da1e0664eb87dfaae9b4953c8f16ca0ce42dc866e02982e37ebf4b38bf77a8","afterSha":"5ad019b2c7f86972d0addfc66a9fecac4cb9675541931daa088e60e7a363cefd","side":"before","part":1,"parts":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "TransitionalLocation": {
    "TimeZoneId": "Europe/London",
    "Address": "1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## after — src/EventBooking.Mcp/appsettings.Local.json — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Mcp/appsettings.Local.json","beforeSha":"19da1e0664eb87dfaae9b4953c8f16ca0ce42dc866e02982e37ebf4b38bf77a8","afterSha":"5ad019b2c7f86972d0addfc66a9fecac4cb9675541931daa088e60e7a363cefd","side":"after","part":1,"parts":1} -->

`````text
{
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=eventbooking_app;Password=eventbooking_local"
  },
  "Clock": {
    "TimeZoneId": "Europe/London"
  },
  "Tokens": {
    "SigningKey": "a-local-signing-key-that-is-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "http://localhost:5002",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## before — src/EventBooking.Mcp/appsettings.json — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Mcp/appsettings.json","beforeSha":"32647a1d6d775e2add9a96cd276f81b72f87f52db62b8ed051c686d506f589b6","afterSha":"7c1342b54993fe79822eeb51f11d42d5656a0dfecf8ae34ee3db8b2335c492a5","side":"before","part":1,"parts":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "TransitionalLocation": {
    "TimeZoneId": "Europe/London",
    "Address": "Corporate HQ, 1 Example Street, London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## after — src/EventBooking.Mcp/appsettings.json — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Mcp/appsettings.json","beforeSha":"32647a1d6d775e2add9a96cd276f81b72f87f52db62b8ed051c686d506f589b6","afterSha":"7c1342b54993fe79822eeb51f11d42d5656a0dfecf8ae34ee3db8b2335c492a5","side":"after","part":1,"parts":1} -->

`````text
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "EventBooking": "Host=localhost;Database=eventbooking;Username=postgres;Password=postgres"
  },
  "Clock": {
    "TimeZoneId": "Europe/London"
  },
  "Tokens": {
    "SigningKey": "replace-this-with-a-real-secret-of-at-least-32-characters"
  },
  "Auth": {
    "Provider": "Local",
    "Local": {
      "Authority": "http://localhost:8081/realms/eventbooking",
      "Audience": "eventbooking-web"
    }
  },
  "Email": {
    "Provider": "Smtp",
    "FromAddress": "recruitment@example.com",
    "FromName": "Recruitment Team",
    "Smtp": {
      "Host": "localhost",
      "Port": 1025
    }
  },
  "Portal": {
    "BaseUrl": "https://localhost:5001",
    "CoordinatorContact": "recruitment@example.com"
  }
}
`````

## before — src/EventBooking.SeedData/DemoEmailOptions.cs — 1/1

<!-- retirement-file: {"id":12,"file":"src/EventBooking.SeedData/DemoEmailOptions.cs","beforeSha":"975f96593fe4210bc1e69f02dab59cf2f466f1e4eaacd084facf9a370044ccc4","afterSha":"6ca2abd54e25164524abd8bf0a482b6cdb045eac369d48ab13d6bfcf28710259","side":"before","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Net.Mail;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.SeedData;

/// <summary>Validated demo SMTP settings and the API-compatible attendee link configuration.</summary>
public sealed class DemoEmailOptions
{
    private DemoEmailOptions(AttendeePortalOptions portal, TokenOptions tokens,
        EmailOptions sender, SmtpOptions smtp, TransitionalLocationOptions transitionalLocation)
    {
        Portal = portal;
        Tokens = tokens;
        Sender = sender;
        Smtp = smtp;
        TransitionalLocation = transitionalLocation;
    }

    /// <summary>Gets the public attendee portal URL, office address and coordinator contact.</summary>
    public AttendeePortalOptions Portal { get; }
    /// <summary>Gets the signing key that must match the API validating attendee tokens.</summary>
    public TokenOptions Tokens { get; }
    /// <summary>Gets the sender identity used for demo messages over SMTP.</summary>
    public EmailOptions Sender { get; }
    /// <summary>Gets the Mailpit SMTP host and port reachable from this process.</summary>
    public SmtpOptions Smtp { get; }
    /// <summary>Gets the timezone used to determine future transitional-location dates.</summary>
    public TransitionalLocationOptions TransitionalLocation { get; }

    /// <summary>Reads environment-style settings, with local defaults and explicit non-local keys.</summary>
    /// <param name="readSetting">Returns a setting value, or null when the key is absent.</param>
    /// <returns>Configuration validated before the host starts issuing demo invitations.</returns>
    /// <exception cref="SeedException">A required setting is missing, blank or invalid.</exception>
    public static DemoEmailOptions From(Func<string, string?> readSetting)
    {
        string Read(string key, string? fallback)
        {
            var value = readSetting(key) ?? fallback;
            if (string.IsNullOrWhiteSpace(value))
                throw Invalid(key);
            return value;
        }

        var baseUrl = Read("Portal__BaseUrl", "http://localhost:5002");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw Invalid("Portal__BaseUrl");

        var signingKey = Read("Tokens__SigningKey", uri.IsLoopback
            ? "a-local-signing-key-that-is-at-least-32-characters" : null);
        if (signingKey.Length < 32)
            throw Invalid("Tokens__SigningKey");
        var smtpHost = Read("Email__Smtp__Host", uri.IsLoopback ? "localhost" : null);
        var portText = Read("Email__Smtp__Port", "1025");
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1 || port > 65535)
            throw Invalid("Email__Smtp__Port");
        var address = Read("Email__FromAddress", "recruitment@example.com");
        if (!MailAddress.TryCreate(address, out var mailbox)
            || !string.Equals(mailbox.Address, address, StringComparison.Ordinal))
            throw Invalid("Email__FromAddress");
        var name = Read("Email__FromName", "Recruitment Team");
        var timezone = Read("TransitionalLocation__TimeZoneId", "Europe/London");
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw Invalid("TransitionalLocation__TimeZoneId");
        }
        catch (InvalidTimeZoneException)
        {
            throw Invalid("TransitionalLocation__TimeZoneId");
        }
        return new DemoEmailOptions(
            new AttendeePortalOptions(uri.AbsoluteUri.TrimEnd('/'),
                Read("TransitionalLocation__Address", "1 Example Street, London"),
                Read("Portal__CoordinatorContact", "recruitment@example.com")),
            new TokenOptions(signingKey),
            new EmailOptions(address, name, EmailProvider.Smtp),
            new SmtpOptions(smtpHost, port),
            new TransitionalLocationOptions(timezone));
    }

    private static SeedException Invalid(string key) =>
        new($"Demo email setting '{key}' is missing or invalid.");

    /// <summary>Describes the configuration without exposing credentials or deployment values.</summary>
    public override string ToString() => "Demo email configuration (values redacted)";
}
`````

## after — src/EventBooking.SeedData/DemoEmailOptions.cs — 1/1

<!-- retirement-file: {"id":12,"file":"src/EventBooking.SeedData/DemoEmailOptions.cs","beforeSha":"975f96593fe4210bc1e69f02dab59cf2f466f1e4eaacd084facf9a370044ccc4","afterSha":"6ca2abd54e25164524abd8bf0a482b6cdb045eac369d48ab13d6bfcf28710259","side":"after","part":1,"parts":1} -->

`````csharp
using System.Globalization;
using System.Net.Mail;
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.SeedData;

/// <summary>Validated demo SMTP settings and the API-compatible attendee link configuration.</summary>
public sealed class DemoEmailOptions
{
    private DemoEmailOptions(AttendeePortalOptions portal, TokenOptions tokens,
        EmailOptions sender, SmtpOptions smtp, ClockOptions clock)
    {
        Portal = portal;
        Tokens = tokens;
        Sender = sender;
        Smtp = smtp;
        Clock = clock;
    }

    /// <summary>Gets the public attendee portal URL and coordinator contact.</summary>
    public AttendeePortalOptions Portal { get; }
    /// <summary>Gets the signing key that must match the API validating attendee tokens.</summary>
    public TokenOptions Tokens { get; }
    /// <summary>Gets the sender identity used for demo messages over SMTP.</summary>
    public EmailOptions Sender { get; }
    /// <summary>Gets the Mailpit SMTP host and port reachable from this process.</summary>
    public SmtpOptions Smtp { get; }
    /// <summary>Gets the timezone used to determine future demo dates.</summary>
    public ClockOptions Clock { get; }

    /// <summary>Reads environment-style settings, with local defaults and explicit non-local keys.</summary>
    /// <param name="readSetting">Returns a setting value, or null when the key is absent.</param>
    /// <returns>Configuration validated before the host starts issuing demo invitations.</returns>
    /// <exception cref="SeedException">A required setting is missing, blank or invalid.</exception>
    public static DemoEmailOptions From(Func<string, string?> readSetting)
    {
        string Read(string key, string? fallback)
        {
            var value = readSetting(key) ?? fallback;
            if (string.IsNullOrWhiteSpace(value))
                throw Invalid(key);
            return value;
        }

        var baseUrl = Read("Portal__BaseUrl", "http://localhost:5002");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw Invalid("Portal__BaseUrl");

        var signingKey = Read("Tokens__SigningKey", uri.IsLoopback
            ? "a-local-signing-key-that-is-at-least-32-characters" : null);
        if (signingKey.Length < 32)
            throw Invalid("Tokens__SigningKey");
        var smtpHost = Read("Email__Smtp__Host", uri.IsLoopback ? "localhost" : null);
        var portText = Read("Email__Smtp__Port", "1025");
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port < 1 || port > 65535)
            throw Invalid("Email__Smtp__Port");
        var address = Read("Email__FromAddress", "recruitment@example.com");
        if (!MailAddress.TryCreate(address, out var mailbox)
            || !string.Equals(mailbox.Address, address, StringComparison.Ordinal))
            throw Invalid("Email__FromAddress");
        var name = Read("Email__FromName", "Recruitment Team");
        var timezone = Read("Clock__TimeZoneId", "Europe/London");
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw Invalid("Clock__TimeZoneId");
        }
        catch (InvalidTimeZoneException)
        {
            throw Invalid("Clock__TimeZoneId");
        }
        return new DemoEmailOptions(
            new AttendeePortalOptions(uri.AbsoluteUri.TrimEnd('/'),
                Read("Portal__CoordinatorContact", "recruitment@example.com")),
            new TokenOptions(signingKey),
            new EmailOptions(address, name, EmailProvider.Smtp),
            new SmtpOptions(smtpHost, port),
            new ClockOptions(timezone));
    }

    private static SeedException Invalid(string key) =>
        new($"Demo email setting '{key}' is missing or invalid.");

    /// <summary>Describes the configuration without exposing credentials or deployment values.</summary>
    public override string ToString() => "Demo email configuration (values redacted)";
}
`````

## before — src/EventBooking.SeedData/Program.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.SeedData/Program.cs","beforeSha":"21774106ccd66cd8e203887e3aa6eeebf92ec90881c8715fe0c6474b1abbdd79","afterSha":"cf99f5228d669b302a84eb5fce3861e0626e331190cefd5107fc5a4e320f7a6b","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var reseed = args.Contains("--reseed");
var skipSeed = args.Contains("--skip-seed");
var verbose = args.Contains("--verbose");
var reanchorIndex = args.ToList().IndexOf("--reanchor");
var reanchorRequested = reanchorIndex >= 0;
DateOnly? reanchorDate = null;
if (reanchorRequested
    && reanchorIndex + 1 < args.Length
    && DateOnly.TryParse(args[reanchorIndex + 1], out var parsedDate))
{
    reanchorDate = parsedDate;
}
var connectionString = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__EventBooking");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/EventBooking.SeedData -- \"<postgres connection string>\" [--reseed] [--skip-seed] [--verbose]");
    Console.Error.WriteLine(
        "   or set the ConnectionStrings__EventBooking environment variable.");
    Console.Error.WriteLine(
        "   Pending migrations are always applied first, whichever mode runs.");
    Console.Error.WriteLine(
        "   --reseed wipes every domain table first, then seeds fresh.");
    Console.Error.WriteLine(
        "   --skip-seed applies migrations only and seeds nothing.");
    Console.Error.WriteLine(
        "   --verbose reports per-step progress; failures print the full exception.");
    Console.Error.WriteLine(
        "   --reanchor [yyyy-MM-dd] resolves every day offset against the given date");
    Console.Error.WriteLine(
        "   (default: today at transitional location) instead of the file anchor, without editing");
    Console.Error.WriteLine(
        "   demo-seed.json. Use it when the file anchor has gone stale and proposals");
    Console.Error.WriteLine(
        "   land on today or earlier.");
    Console.Error.WriteLine(
        "   Keycloak demo users are converged when Keycloak__BaseUrl, Keycloak__Realm,");
    Console.Error.WriteLine(
        "   Keycloak__AdminRealm, Keycloak__AdminUsername, Keycloak__AdminPassword, and");
    Console.Error.WriteLine(
        "   Keycloak__DemoPassword are set; otherwise only the database is seeded.");
    Console.Error.WriteLine(
        "   --reseed also deletes and recreates the Keycloak realm from the file named by");
    Console.Error.WriteLine(
        "   Keycloak__RealmExportPath, when Keycloak settings are configured.");
    Console.Error.WriteLine("   Normal seed/reseed sends five demo invitations through Mailpit SMTP.");
    Console.Error.WriteLine("   Local defaults: Portal__BaseUrl=http://localhost:5002, Email__Smtp__Host=localhost,");
    Console.Error.WriteLine("   Email__Smtp__Port=1025 and the local API's development token key.");
    Console.Error.WriteLine("   Non-local portals require explicit Tokens__SigningKey and Email__Smtp__Host.");
    Console.Error.WriteLine("   Match Tokens__SigningKey and Portal__BaseUrl to the running API.");
    Console.Error.WriteLine("   --skip-seed does not read email settings or send any messages.");
    return 2;
}

try
{
    var services = new ServiceCollection();
    services.AddLogging();
    if (skipSeed)
    {
        // The persistence interceptor still needs IClock, even for migration-only context creation.
        services.AddEventBookingPersistence(connectionString);
        services.AddSingleton(new TransitionalLocationOptions("Europe/London"));
        services.AddSingleton<IClock, SystemClock>();
    }
    else
    {
        var email = DemoEmailOptions.From(Environment.GetEnvironmentVariable);
        services.AddEventBookingInfrastructure(connectionString, email.TransitionalLocation, email.Tokens);
        services.AddEventBookingApplication(email.Portal,
            new EventBooking.Application.Access.StaffIdPolicy(Environment.GetEnvironmentVariable("Identity__StaffIdPattern")));
        services.AddLocalEmailTransport(email.Sender, email.Smtp);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
    }
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
    if (verbose)
    {
        Console.WriteLine("[seed] Applying pending migrations...");
    }

    await database.Database.MigrateAsync();
    if (verbose)
    {
        Console.WriteLine("[seed] Migrations applied.");
    }

    if (skipSeed)
    {
        Console.WriteLine("Migrations applied. Skipping seed data (--skip-seed).");
        return 0;
    }

    var keycloakStep = new KeycloakSeedStep(
        Environment.GetEnvironmentVariable,
        static () => new HttpClient());
    if (reseed && verbose)
    {
        Console.WriteLine("[seed] Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.");
    }

    var keycloakSummary = await keycloakStep.RunAsync(
        skipSeed, reseed, DemoSeedSpec.Staff(), CancellationToken.None);
    if (keycloakSummary is not null)
    {
        if (verbose)
        {
            Console.WriteLine(reseed
                ? "[seed] Keycloak realm reset and convergence complete."
                : "[seed] Keycloak convergence complete.");
        }

        Console.WriteLine(
            $"Keycloak seed complete: {keycloakSummary.RolesCreated} roles created, " +
            $"{keycloakSummary.MapperWrites} mapper writes, " +
            $"{keycloakSummary.UsersCreated} users created, " +
            $"{keycloakSummary.RoleMappingWrites} role-mapping writes.");
    }
    else if (verbose)
    {
        Console.WriteLine("[seed] Keycloak provider seed skipped.");
    }

    if (reanchorRequested)
    {
        reanchorDate ??= scope.ServiceProvider
            .GetRequiredService<IClock>()
            .TodayAtTransitionalLocation;
        DemoSeedSpec.OverrideAnchor(reanchorDate.Value);
        Console.WriteLine($"[seed] Reanchored to {reanchorDate:yyyy-MM-dd}.");
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
    if (verbose)
    {
        seeder.Progress = Console.Out;
    }

    var summary = reseed
        ? await seeder.ReseedAsync(CancellationToken.None)
        : await seeder.RunAsync(CancellationToken.None);

    var invitations = scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>();
    if (verbose) invitations.Progress = Console.Out;
    var invitationEmailsSent = await invitations.RunAsync(CancellationToken.None);

    Console.WriteLine(
        $"Migrations applied. " +
        $"{(reseed ? "Reseed complete (database was cleared): " : "Seed complete: ")}" +
        $"{summary.IdentitiesEnsured} identities, " +
        $"{summary.ProfilesEnsured} profiles, " +
        $"{summary.AgreedEventsImported} agreed events, " +
        $"{summary.ProposalsEnsured} proposals, " +
        $"{summary.AcceptancesApplied} acceptances, " +
        $"{summary.AttendeesCreated} attendees; " +
        $"{invitationEmailsSent} invitation emails sent or retried.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(verbose ? $"Seed failed: {ex}" : $"Seed failed: {ex.Message}");
    return 2;
}
`````

## after — src/EventBooking.SeedData/Program.cs — 1/1

<!-- retirement-file: {"id":13,"file":"src/EventBooking.SeedData/Program.cs","beforeSha":"21774106ccd66cd8e203887e3aa6eeebf92ec90881c8715fe0c6474b1abbdd79","afterSha":"cf99f5228d669b302a84eb5fce3861e0626e331190cefd5107fc5a4e320f7a6b","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application;
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;
using EventBooking.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var reseed = args.Contains("--reseed");
var skipSeed = args.Contains("--skip-seed");
var verbose = args.Contains("--verbose");
var reanchorIndex = args.ToList().IndexOf("--reanchor");
var reanchorRequested = reanchorIndex >= 0;
DateOnly? reanchorDate = null;
if (reanchorRequested
    && reanchorIndex + 1 < args.Length
    && DateOnly.TryParse(args[reanchorIndex + 1], out var parsedDate))
{
    reanchorDate = parsedDate;
}
var connectionString = args.FirstOrDefault(a => !a.StartsWith('-'))
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__EventBooking");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/EventBooking.SeedData -- \"<postgres connection string>\" [--reseed] [--skip-seed] [--verbose]");
    Console.Error.WriteLine(
        "   or set the ConnectionStrings__EventBooking environment variable.");
    Console.Error.WriteLine(
        "   Pending migrations are always applied first, whichever mode runs.");
    Console.Error.WriteLine(
        "   --reseed wipes every domain table first, then seeds fresh.");
    Console.Error.WriteLine(
        "   --skip-seed applies migrations only and seeds nothing.");
    Console.Error.WriteLine(
        "   --verbose reports per-step progress; failures print the full exception.");
    Console.Error.WriteLine(
        "   --reanchor [yyyy-MM-dd] resolves every day offset against the given date");
    Console.Error.WriteLine(
        "   (default: today at transitional location) instead of the file anchor, without editing");
    Console.Error.WriteLine(
        "   demo-seed.json. Use it when the file anchor has gone stale and proposals");
    Console.Error.WriteLine(
        "   land on today or earlier.");
    Console.Error.WriteLine(
        "   Keycloak demo users are converged when Keycloak__BaseUrl, Keycloak__Realm,");
    Console.Error.WriteLine(
        "   Keycloak__AdminRealm, Keycloak__AdminUsername, Keycloak__AdminPassword, and");
    Console.Error.WriteLine(
        "   Keycloak__DemoPassword are set; otherwise only the database is seeded.");
    Console.Error.WriteLine(
        "   --reseed also deletes and recreates the Keycloak realm from the file named by");
    Console.Error.WriteLine(
        "   Keycloak__RealmExportPath, when Keycloak settings are configured.");
    Console.Error.WriteLine("   Normal seed/reseed sends five demo invitations through Mailpit SMTP.");
    Console.Error.WriteLine("   Local defaults: Portal__BaseUrl=http://localhost:5002, Email__Smtp__Host=localhost,");
    Console.Error.WriteLine("   Email__Smtp__Port=1025 and the local API's development token key.");
    Console.Error.WriteLine("   Non-local portals require explicit Tokens__SigningKey and Email__Smtp__Host.");
    Console.Error.WriteLine("   Match Tokens__SigningKey and Portal__BaseUrl to the running API.");
    Console.Error.WriteLine("   --skip-seed does not read email settings or send any messages.");
    return 2;
}

try
{
    var services = new ServiceCollection();
    services.AddLogging();
    if (skipSeed)
    {
        // The persistence interceptor still needs IClock, even for migration-only context creation.
        services.AddEventBookingPersistence(connectionString);
        services.AddSingleton(new ClockOptions("Europe/London"));
        services.AddSingleton<IClock, SystemClock>();
    }
    else
    {
        var email = DemoEmailOptions.From(Environment.GetEnvironmentVariable);
        services.AddEventBookingInfrastructure(connectionString, email.Clock, email.Tokens);
        services.AddEventBookingApplication(email.Portal,
            new EventBooking.Application.Access.StaffIdPolicy(Environment.GetEnvironmentVariable("Identity__StaffIdPattern")));
        services.AddLocalEmailTransport(email.Sender, email.Smtp);
        services.AddScoped<DemoSeeder>();
        services.AddScoped<DemoInvitationSeeder>();
    }
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<EventBookingDbContext>();
    if (verbose)
    {
        Console.WriteLine("[seed] Applying pending migrations...");
    }

    await database.Database.MigrateAsync();
    if (verbose)
    {
        Console.WriteLine("[seed] Migrations applied.");
    }

    if (skipSeed)
    {
        Console.WriteLine("Migrations applied. Skipping seed data (--skip-seed).");
        return 0;
    }

    var keycloakStep = new KeycloakSeedStep(
        Environment.GetEnvironmentVariable,
        static () => new HttpClient());
    if (reseed && verbose)
    {
        Console.WriteLine("[seed] Reseed requested: the Keycloak realm will be deleted and recreated first, if configured.");
    }

    var keycloakSummary = await keycloakStep.RunAsync(
        skipSeed, reseed, DemoSeedSpec.Staff(), CancellationToken.None);
    if (keycloakSummary is not null)
    {
        if (verbose)
        {
            Console.WriteLine(reseed
                ? "[seed] Keycloak realm reset and convergence complete."
                : "[seed] Keycloak convergence complete.");
        }

        Console.WriteLine(
            $"Keycloak seed complete: {keycloakSummary.RolesCreated} roles created, " +
            $"{keycloakSummary.MapperWrites} mapper writes, " +
            $"{keycloakSummary.UsersCreated} users created, " +
            $"{keycloakSummary.RoleMappingWrites} role-mapping writes.");
    }
    else if (verbose)
    {
        Console.WriteLine("[seed] Keycloak provider seed skipped.");
    }

    if (reanchorRequested)
    {
        reanchorDate ??= scope.ServiceProvider
            .GetRequiredService<IClock>()
            .TodayAtTransitionalLocation;
        DemoSeedSpec.OverrideAnchor(reanchorDate.Value);
        Console.WriteLine($"[seed] Reanchored to {reanchorDate:yyyy-MM-dd}.");
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DemoSeeder>();
    if (verbose)
    {
        seeder.Progress = Console.Out;
    }

    var summary = reseed
        ? await seeder.ReseedAsync(CancellationToken.None)
        : await seeder.RunAsync(CancellationToken.None);

    var invitations = scope.ServiceProvider.GetRequiredService<DemoInvitationSeeder>();
    if (verbose) invitations.Progress = Console.Out;
    var invitationEmailsSent = await invitations.RunAsync(CancellationToken.None);

    Console.WriteLine(
        $"Migrations applied. " +
        $"{(reseed ? "Reseed complete (database was cleared): " : "Seed complete: ")}" +
        $"{summary.IdentitiesEnsured} identities, " +
        $"{summary.ProfilesEnsured} profiles, " +
        $"{summary.AgreedEventsImported} agreed events, " +
        $"{summary.ProposalsEnsured} proposals, " +
        $"{summary.AcceptancesApplied} acceptances, " +
        $"{summary.AttendeesCreated} attendees; " +
        $"{invitationEmailsSent} invitation emails sent or retried.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(verbose ? $"Seed failed: {ex}" : $"Seed failed: {ex.Message}");
    return 2;
}
`````
