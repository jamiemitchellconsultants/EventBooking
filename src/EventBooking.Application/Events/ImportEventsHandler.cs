using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <param name="StaffUserId">The staff identity performing the import.</param>
/// <param name="CsvContent">The raw CSV file content.</param>
/// <param name="AllowPastDates">Whether historical event dates are accepted. Only the demo
/// seeder sets this: its agreed events are deliberately historical, while the user-facing
/// import requires future dates like event proposals do.</param>
public sealed record ImportEventsCommand(Guid StaffUserId, string? CsvContent, bool AllowPastDates = false);

/// <summary>Defines event import outcome for the current use case.</summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record EventImportOutcome(
    bool Accepted, int ImportedCount, IReadOnlyList<EventImportError> Errors);

/// <summary>Defines import events handler for the current use case.</summary>
/// <param name="events">The events.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ImportEventsHandler(
    IEventRepository events,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<EventImportOutcome>> HandleAsync(
        ImportEventsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ImportEvents,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<EventImportOutcome>.Failure(authorized.Error);
        }

        var parsed = EventImportParser.Parse(
            command.CsvContent,
            command.AllowPastDates ? null : clock.TodayAtTransitionalLocation);
        if (parsed.Errors.Count > 0)
        {
            return Result<EventImportOutcome>.Success(
                new EventImportOutcome(false, 0, parsed.Errors));
        }

        foreach (var row in parsed.Rows)
        {
            var eventItem = Event.CreateImported(Guid.NewGuid(), row.Window, row.HeadcountsByAppointmentType);
            events.Add(eventItem);

            audit.Record(
                AuditEntityTypes.Event,
                eventItem.Id,
                AuditAction.EventImported,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"Imported from CSV line {row.LineNumber}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EventImportOutcome>.Success(
            new EventImportOutcome(true, parsed.Rows.Count, []));
    }
}
