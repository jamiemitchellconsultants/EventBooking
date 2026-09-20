# 01a — Variable-length windows in the location's zone, edits 1 (Task 4)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — Directory.Packages.props — 1/1

<!-- retirement-file: {"id":0,"file":"Directory.Packages.props","beforeSha":"0fddcbdb929a48c2cb6c8754fb5e2efb54d8fd45e1181dfe5c85559d6ee4b61b","afterSha":"32d444d9367ba5460fa6eb7a9b53f08d1e96061580d14c951b3e22416c5845f6","side":"before","part":1,"parts":1} -->

`````text
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MailKit" Version="4.17.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.11" />
    <PackageVersion Include="Swashbuckle.AspNetCore.SwaggerUI" Version="10.2.3" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.11" />
    <PackageVersion Include="bunit" Version="2.9.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageVersion>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Markdig" Version="0.41.3" />
    <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.10" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.14.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
`````

## after — Directory.Packages.props — 1/1

<!-- retirement-file: {"id":0,"file":"Directory.Packages.props","beforeSha":"0fddcbdb929a48c2cb6c8754fb5e2efb54d8fd45e1181dfe5c85559d6ee4b61b","afterSha":"32d444d9367ba5460fa6eb7a9b53f08d1e96061580d14c951b3e22416c5845f6","side":"after","part":1,"parts":1} -->

`````text
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MailKit" Version="4.17.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.11" />
    <PackageVersion Include="Swashbuckle.AspNetCore.SwaggerUI" Version="10.2.3" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.11" />
    <PackageVersion Include="bunit" Version="2.9.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.Authentication" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageVersion>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Http" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Markdig" Version="0.41.3" />
    <PackageVersion Include="NodaTime" Version="3.2.2" />
    <PackageVersion Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.10" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.10" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.14.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
`````

## before — src/EventBooking.Application/Events/ProposeEventHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Events/ProposeEventHandler.cs","beforeSha":"e9a8f03cde4775f50395e3e7766157d53255e0370b9c3847d7f7124b6010e31e","afterSha":"05566bb17f0a18d98096e6e9a354fbb44dcc33359e4dadc2dfe34f6a560b216d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines propose event command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
public sealed record ProposeEventCommand(Guid ManagerUserId, DateOnly Date, TimeOnly StartTime);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ProposeEventHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeEventCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        EventWindow window;
        try
        {
            window = new EventWindow(command.Date, command.StartTime);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtTransitionalLocation))
        {
            return Result<Guid>.Failure(Error.Validation("A event must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        EventProposal proposal;
        try
        {
            proposal = EventProposal.Create(id, window, command.ManagerUserId);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.EventProposal,
                id,
                AuditAction.ProposalCreated,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                window.ToString());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        return Result<Guid>.Success(id);
    }
}
`````

## after — src/EventBooking.Application/Events/ProposeEventHandler.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Application/Events/ProposeEventHandler.cs","beforeSha":"e9a8f03cde4775f50395e3e7766157d53255e0370b9c3847d7f7124b6010e31e","afterSha":"05566bb17f0a18d98096e6e9a354fbb44dcc33359e4dadc2dfe34f6a560b216d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines propose event command for the current use case.</summary>
/// <param name="ManagerUserId">The manager user id.</param>
/// <param name="Date">The date.</param>
/// <param name="StartTime">The start time.</param>
public sealed record ProposeEventCommand(Guid ManagerUserId, DateOnly Date, TimeOnly StartTime);

/// <summary>Creates one future open proposal inside a transaction protected by a database backstop.</summary>
/// <param name="proposals">The proposals.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ProposeEventHandler(
    IEventProposalRepository proposals,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Creates the requested proposal or returns a stable conflict for its open window.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<Guid>> HandleAsync(
        ProposeEventCommand command,
        CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.ManagerUserId,
            StaffCapability.ManageEventNegotiation,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<Guid>.Failure(authorized.Error);
        }

        EventWindow window;
        try
        {
            window = new EventWindow(command.Date, command.StartTime, 240);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }

        if (!window.StartsAfter(clock.TodayAtTransitionalLocation))
        {
            return Result<Guid>.Failure(Error.Validation("A event must be proposed for a future date."));
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var open = await proposals.ListOpenAsync(cancellationToken);
        if (open.Any(p => p.Window == window))
        {
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        var id = Guid.NewGuid();

        EventProposal proposal;
        try
        {
            proposal = EventProposal.Create(id, window, command.ManagerUserId);
            proposals.Add(proposal);

            audit.Record(
                AuditEntityTypes.EventProposal,
                id,
                AuditAction.ProposalCreated,
                ActorType.Staff,
                command.ManagerUserId.ToString(),
                window.ToString());

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Validation(ex.Message));
        }
        catch (UniqueConstraintViolationException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<Guid>.Failure(Error.Conflict("An open proposal already exists for that window."));
        }

        return Result<Guid>.Success(id);
    }
}
`````

## before — src/EventBooking.Domain/Events/EventWindow.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Domain/Events/EventWindow.cs","beforeSha":"0070a97b34ee2c8050a3c39f6f4e31c638b2520f552f8e4617a1d04d57008912","afterSha":"d758fd959f27da74db4f3c922481d073ea2e46e911b47daaeb6e1896d30d002c","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Events;

/// <summary>
/// The 4-hour attendee-facing window. Duration is fixed by the domain, so only the date and the
/// start time are ever stored; the end time is always derived.
/// </summary>
public sealed record EventWindow : IComparable<EventWindow>
{
    /// <summary>Defines duration for the current use case.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromHours(4);

    /// <summary>Defines event window for the current use case.</summary>
    /// <param name="date">The date.</param>
    /// <param name="startTime">The start time.</param>
    public EventWindow(DateOnly date, TimeOnly startTime)
    {
        Guard.Against(
            startTime.ToTimeSpan() + Duration > TimeSpan.FromHours(24),
            "startTime must leave room for the full 4-hour window on the same day.");

        Date = date;
        StartTime = startTime;
    }

    /// <summary>Defines date for the current use case.</summary>
    public DateOnly Date { get; }

    /// <summary>Defines start time for the current use case.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Defines end time for the current use case.</summary>
    public TimeOnly EndTime => StartTime.Add(Duration);

    /// <summary>Defines starts after for the current use case.</summary>
    /// <param name="today">The today.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>Defines compare to for the current use case.</summary>
    /// <param name="other">The other.</param>
    public int CompareTo(EventWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Defines to string for the current use case.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
`````

## after — src/EventBooking.Domain/Events/EventWindow.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Domain/Events/EventWindow.cs","beforeSha":"0070a97b34ee2c8050a3c39f6f4e31c638b2520f552f8e4617a1d04d57008912","afterSha":"d758fd959f27da74db4f3c922481d073ea2e46e911b47daaeb6e1896d30d002c","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Events;

/// <summary>
/// The attendee-facing window: a local date, a local start time and an explicit duration, all read
/// in the <c>Location</c>'s zone. The end time is always derived, never stored.
/// </summary>
public sealed record EventWindow : IComparable<EventWindow>
{
    /// <summary>The smallest step a duration may take, and the granularity of every duration.</summary>
    public const int DurationStepMinutes = 15;

    /// <summary>The longest window the design allows (design 08 — boundary values).</summary>
    public const int MaximumDurationMinutes = 720;

    /// <summary>Creates a window, refusing any duration the design does not allow.</summary>
    /// <param name="date">The local date the window starts and ends on.</param>
    /// <param name="startTime">The local start time.</param>
    /// <param name="durationMinutes">The length in minutes: a multiple of 15, from 15 to 720.</param>
    public EventWindow(DateOnly date, TimeOnly startTime, int durationMinutes)
    {
        Guard.Against(
            durationMinutes < DurationStepMinutes || durationMinutes > MaximumDurationMinutes,
            $"durationMinutes must be between {DurationStepMinutes} and {MaximumDurationMinutes}.");
        Guard.Against(
            durationMinutes % DurationStepMinutes != 0,
            $"durationMinutes must be a multiple of {DurationStepMinutes}.");
        Guard.Against(
            startTime.ToTimeSpan() + TimeSpan.FromMinutes(durationMinutes) >= TimeSpan.FromHours(24),
            "The window must end on the local date it starts.");

        Date = date;
        StartTime = startTime;
        DurationMinutes = durationMinutes;
    }

    /// <summary>The local date the window starts and ends on.</summary>
    public DateOnly Date { get; }

    /// <summary>The local start time.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>The length of the window in minutes.</summary>
    public int DurationMinutes { get; }

    /// <summary>The derived local end time, always on the same date.</summary>
    public TimeOnly EndTime => StartTime.Add(TimeSpan.FromMinutes(DurationMinutes));

    /// <summary>Whether the window's date falls after the given local date.</summary>
    /// <param name="today">The local date to compare against.</param>
    public bool StartsAfter(DateOnly today) => Date > today;

    /// <summary>The instant the window starts, in the given zone.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public DateTimeOffset StartInstant(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.InstantOf(Date, StartTime, timeZoneId);
    }

    /// <summary>The instant the window ends, in the given zone.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public DateTimeOffset EndInstant(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.InstantOf(Date, EndTime, timeZoneId);
    }

    /// <summary>Whether the window has started at the given instant.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool HasStarted(IEventWindowZones zones, string timeZoneId, DateTimeOffset now) =>
        now >= StartInstant(zones, timeZoneId);

    /// <summary>Whether the window has ended at the given instant.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool HasEnded(IEventWindowZones zones, string timeZoneId, DateTimeOffset now) =>
        now >= EndInstant(zones, timeZoneId);

    /// <summary>Whether the given instant falls on the window's own local date.</summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    /// <param name="now">The current instant.</param>
    public bool IsOnEventDate(IEventWindowZones zones, string timeZoneId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(zones);
        return zones.LocalDateOf(now, timeZoneId) == Date;
    }

    /// <summary>
    /// Why this window cannot be interpreted in the zone, or None. A window whose start or end
    /// falls in a daylight-saving gap or overlap does not identify a unique instant, so it is
    /// refused when a proposal is made rather than resolved arbitrarily.
    /// </summary>
    /// <param name="zones">The zone abstraction.</param>
    /// <param name="timeZoneId">The location's IANA zone.</param>
    public EventWindowZoneProblem ProblemIn(IEventWindowZones zones, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(zones);

        if (!zones.IsKnownZone(timeZoneId))
        {
            return EventWindowZoneProblem.UnknownZone;
        }

        if (zones.ValidityOf(Date, StartTime, timeZoneId) != LocalTimeValidity.Unique)
        {
            return EventWindowZoneProblem.StartHasNoUniqueInstant;
        }

        return zones.ValidityOf(Date, EndTime, timeZoneId) != LocalTimeValidity.Unique
            ? EventWindowZoneProblem.EndHasNoUniqueInstant
            : EventWindowZoneProblem.None;
    }

    /// <summary>Orders by local date, then local start time.</summary>
    /// <param name="other">The window to compare against.</param>
    public int CompareTo(EventWindow? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byDate = Date.CompareTo(other.Date);
        return byDate != 0 ? byDate : StartTime.CompareTo(other.StartTime);
    }

    /// <summary>Renders the local window for logs and diagnostics.</summary>
    public override string ToString() =>
        $"{Date:yyyy-MM-dd} {StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
}
`````

## after — src/EventBooking.Domain/Time/IEventWindowZones.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Domain/Time/IEventWindowZones.cs","beforeSha":null,"afterSha":"2aab209674f584ff029c1904f105351a40c821ee4a426d24f32f9dc33935eb6d","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Time;

/// <summary>
/// Whether one local wall-clock time names exactly one instant in a zone. A daylight-saving jump
/// forward leaves a gap where it names none; a jump back leaves an overlap where it names two.
/// </summary>
public enum LocalTimeValidity
{
    /// <summary>The local time names exactly one instant.</summary>
    Unique,

    /// <summary>The local time falls in a daylight-saving gap and names no instant.</summary>
    Gap,

    /// <summary>The local time falls in a daylight-saving overlap and names two instants.</summary>
    Ambiguous,
}

/// <summary>Why an <c>EventWindow</c> cannot be interpreted in a given zone.</summary>
public enum EventWindowZoneProblem
{
    /// <summary>The window names one unambiguous span of time in that zone.</summary>
    None,

    /// <summary>The zone identifier is not one the host recognises.</summary>
    UnknownZone,

    /// <summary>The start falls in a daylight-saving gap or overlap.</summary>
    StartHasNoUniqueInstant,

    /// <summary>The end falls in a daylight-saving gap or overlap.</summary>
    EndHasNoUniqueInstant,
}

/// <summary>
/// The time-zone questions the domain has to ask to interpret an <c>EventWindow</c> at its
/// <c>Location</c>. The domain owns the questions because its rules depend on the answers; the
/// IANA database that answers them lives in infrastructure.
/// </summary>
public interface IEventWindowZones
{
    /// <summary>Whether the identifier names a zone this host can resolve.</summary>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    bool IsKnownZone(string timeZoneId);

    /// <summary>Whether a local date and time names exactly one instant in the zone.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="time">The local time of day.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId);

    /// <summary>Converts a local date and time in the zone to the instant it names.</summary>
    /// <param name="date">The local date.</param>
    /// <param name="time">The local time of day.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId);

    /// <summary>The calendar date an instant falls on, in the zone.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId);

    /// <summary>The zone abbreviation in force at an instant, for display.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="timeZoneId">The IANA time-zone identifier.</param>
    string AbbreviationOf(DateTimeOffset instant, string timeZoneId);
}
`````

## before — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"d75101dcd6f83e10f40e686d59c854f32fb59095b7cd07de7bb144ae1baa3eb2","afterSha":"892820087fc053b3ebaeb0b582958d3493bb07adb54366dc61e036b12decc092","side":"before","part":1,"parts":1} -->

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

## after — src/EventBooking.Infrastructure/DependencyInjection.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Infrastructure/DependencyInjection.cs","beforeSha":"d75101dcd6f83e10f40e686d59c854f32fb59095b7cd07de7bb144ae1baa3eb2","afterSha":"892820087fc053b3ebaeb0b582958d3493bb07adb54366dc61e036b12decc092","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Domain.Time;
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
        services.AddSingleton<IEventWindowZones, NodaTimeEventWindowZones>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
`````

## before — src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj","beforeSha":"8ad942183f3244a8a66e0ea0bac2a8ef10940a683ccf22dba8eb4cefaa181d84","afterSha":"64ee70bf7ccd21edc796e6042a5b348c36636a6035434a33dae379d9b2c50ec9","side":"before","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="MailKit" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

</Project>
`````

## after — src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj","beforeSha":"8ad942183f3244a8a66e0ea0bac2a8ef10940a683ccf22dba8eb4cefaa181d84","afterSha":"64ee70bf7ccd21edc796e6042a5b348c36636a6035434a33dae379d9b2c50ec9","side":"after","part":1,"parts":1} -->

`````text
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\EventBooking.Application\EventBooking.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="NodaTime" />
    <PackageReference Include="MailKit" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

</Project>
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"776dd446794cd0f3882139eac9961f46b62111f8a4e95cf3a0c837ad8e43972e","afterSha":"4da235933993c514b2d4258a2d594207d8acec084a573dc36d18ddba412606a2","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs","beforeSha":"776dd446794cd0f3882139eac9961f46b62111f8a4e95cf3a0c837ad8e43972e","afterSha":"4da235933993c514b2d4258a2d594207d8acec084a573dc36d18ddba412606a2","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("event");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.ProposalId).HasColumnName("proposal_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<int>();

        builder.OwnsOne(s => s.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
        });
        builder.Navigation(s => s.Window).IsRequired();

        builder
            .HasMany(s => s.Capacities)
            .WithOne()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Capacities).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ProposalId).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs","beforeSha":"1dc59b9c4b48c91c238a20bb095708ab2f2564b6e38c1e8e3ad3d709a58d34ad","afterSha":"d538bc93030fc20009c56bebc73302b5851a077a807c170d39bb843483b8b0d5","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps proposal persistence and its one-open-window uniqueness backstop.</summary>
public sealed class EventProposalConfiguration : IEntityTypeConfiguration<EventProposal>
{
    /// <summary>Configures proposal columns, acceptances, and the filtered window index.</summary>
    public void Configure(EntityTypeBuilder<EventProposal> builder)
    {
        builder.ToTable("event_proposal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(p => p.CreatedByManagerUserId).HasColumnName("created_by_manager_user_id");

        // The 4-hour window lives in this table's own date and start_time columns.
        builder.OwnsOne(p => p.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Ignore(w => w.EndTime);
            window.HasIndex(item => new { item.Date, item.StartTime })
                .HasDatabaseName("ux_event_proposal_open_window")
                .HasFilter("status = 1")
                .IsUnique();
        });
        builder.Navigation(p => p.Window).IsRequired();

        builder
            .HasMany(p => p.Acceptances)
            .WithOne()
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Acceptances).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.Status);

    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs","beforeSha":"1dc59b9c4b48c91c238a20bb095708ab2f2564b6e38c1e8e3ad3d709a58d34ad","afterSha":"d538bc93030fc20009c56bebc73302b5851a077a807c170d39bb843483b8b0d5","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps proposal persistence and its one-open-window uniqueness backstop.</summary>
public sealed class EventProposalConfiguration : IEntityTypeConfiguration<EventProposal>
{
    /// <summary>Configures proposal columns, acceptances, and the filtered window index.</summary>
    public void Configure(EntityTypeBuilder<EventProposal> builder)
    {
        builder.ToTable("event_proposal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<int>();
        builder.Property(p => p.CreatedByManagerUserId).HasColumnName("created_by_manager_user_id");

        // The 4-hour window lives in this table's own date and start_time columns.
        builder.OwnsOne(p => p.Window, window =>
        {
            window.Property(w => w.Date).HasColumnName("date");
            window.Property(w => w.StartTime).HasColumnName("start_time");
            window.Property(w => w.DurationMinutes).HasColumnName("duration_minutes");
            window.Ignore(w => w.EndTime);
            window.HasIndex(item => new { item.Date, item.StartTime })
                .HasDatabaseName("ux_event_proposal_open_window")
                .HasFilter("status = 1")
                .IsUnique();
        });
        builder.Navigation(p => p.Window).IsRequired();

        builder
            .HasMany(p => p.Acceptances)
            .WithOne()
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Acceptances).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.Status);

    }
}
`````
