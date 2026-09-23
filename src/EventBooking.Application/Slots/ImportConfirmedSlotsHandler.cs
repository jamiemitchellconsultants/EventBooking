using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Access;
using EventBooking.Domain.Audit;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <param name="StaffUserId">The staff identity performing the import.</param>
/// <param name="CsvContent">The raw CSV file content.</param>
/// <param name="AllowPastDates">Whether historical slot dates are accepted. Only the demo
/// seeder sets this: its agreed slots are deliberately historical, while the user-facing
/// import requires future dates like slot proposals do.</param>
public sealed record ImportConfirmedSlotsCommand(Guid StaffUserId, string? CsvContent, bool AllowPastDates = false);

/// <summary>Defines confirmed slot import outcome for the current use case.</summary>
/// <param name="Accepted">The accepted.</param>
/// <param name="ImportedCount">The imported count.</param>
/// <param name="Errors">The errors.</param>
public sealed record ConfirmedSlotImportOutcome(
    bool Accepted, int ImportedCount, IReadOnlyList<ConfirmedSlotImportError> Errors);

/// <summary>Defines import confirmed slots handler for the current use case.</summary>
/// <param name="confirmedSlots">The confirmed slots.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
/// <param name="audit">The audit.</param>
/// <param name="clock">The clock.</param>
public sealed class ImportConfirmedSlotsHandler(
    IConfirmedSlotRepository confirmedSlots,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock)
{
    /// <summary>Defines handle async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<ConfirmedSlotImportOutcome>> HandleAsync(
        ImportConfirmedSlotsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ImportConfirmedSlots,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<ConfirmedSlotImportOutcome>.Failure(authorized.Error);
        }

        var parsed = ConfirmedSlotImportParser.Parse(
            command.CsvContent,
            command.AllowPastDates ? null : clock.TodayAtHeadOffice);
        if (parsed.Errors.Count > 0)
        {
            return Result<ConfirmedSlotImportOutcome>.Success(
                new ConfirmedSlotImportOutcome(false, 0, parsed.Errors));
        }

        foreach (var row in parsed.Rows)
        {
            var slot = ConfirmedSlot.CreateImported(Guid.NewGuid(), row.Window, row.HeadcountsByAppointmentType);
            confirmedSlots.Add(slot);

            audit.Record(
                AuditEntityTypes.ConfirmedSlot,
                slot.Id,
                AuditAction.SlotImported,
                ActorType.Staff,
                command.StaffUserId.ToString(),
                $"Imported from CSV line {row.LineNumber}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ConfirmedSlotImportOutcome>.Success(
            new ConfirmedSlotImportOutcome(true, parsed.Rows.Count, []));
    }
}
