using System.Globalization;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Slots;

/// <summary>Defines confirmed slot import row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Window">The window.</param>
/// <param name="HeadcountsByAppointmentType">The headcounts by appointment type.</param>
public sealed record ConfirmedSlotImportRow(
    int LineNumber, SlotWindow Window, IReadOnlyDictionary<Guid, int> HeadcountsByAppointmentType);

/// <summary>Defines confirmed slot import error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record ConfirmedSlotImportError(int LineNumber, string Message);

/// <summary>Defines confirmed slot import parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record ConfirmedSlotImportParseResult(
    IReadOnlyList<ConfirmedSlotImportRow> Rows,
    IReadOnlyList<ConfirmedSlotImportError> Errors);

/// <summary>
/// Structural validation only, mirroring CandidateCsvParser (Task 29): header, field count,
/// parseable date/time, a positive integer per fixed AppointmentType column, duplicate windows
/// within the file, and a row-count ceiling. Nothing here reads the database.
/// </summary>
public static class ConfirmedSlotImportParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "date,startTime,DAT,MED,UNI";
    /// <summary>Defines max rows for the current use case.</summary>
    public const int MaxRows = 200;

    private static readonly (string Code, Guid Id)[] TypeColumns =
    [
        ("DAT", AppointmentTypeIds.DrugAndAlcoholTesting),
        ("MED", AppointmentTypeIds.MedicalCheckUp),
        ("UNI", AppointmentTypeIds.UniformFitting),
    ];

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    /// <param name="today">The today.</param>
    public static ConfirmedSlotImportParseResult Parse(string? content, DateOnly? today = null)
    {
        var rows = new List<ConfirmedSlotImportRow>();
        var errors = new List<ConfirmedSlotImportError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new ConfirmedSlotImportError(0, "The file is empty."));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ConfirmedSlotImportError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var dataLineNumbers = new List<int>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                dataLineNumbers.Add(index);
            }
        }

        if (dataLineNumbers.Count > MaxRows)
        {
            errors.Add(new ConfirmedSlotImportError(0, $"A file may contain at most 200 rows."));
            return new ConfirmedSlotImportParseResult(rows, errors);
        }

        var seenWindows = new Dictionary<(DateOnly Date, TimeOnly StartTime), int>();

        foreach (var index in dataLineNumbers)
        {
            var lineNumber = index + 1;
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, "Expected 5 comma-separated fields: date,startTime,DAT,MED,UNI."));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    fields[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, $"{fields[0].Trim()} is not a valid date (expected yyyy-MM-dd)."));
                continue;
            }

            if (!TimeOnly.TryParseExact(
                    fields[1].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, $"{fields[1].Trim()} is not a valid startTime (expected HH:mm)."));
                continue;
            }

            if (!TryReadHeadcounts(fields, lineNumber, errors, out var headcounts))
            {
                continue;
            }

            var window = (date, startTime);

            SlotWindow slotWindow;
            try
            {
                slotWindow = new SlotWindow(date, startTime);
            }
            catch (DomainException ex)
            {
                errors.Add(new ConfirmedSlotImportError(lineNumber, ex.Message));
                continue;
            }

            // Imported slots follow the same future-date rule as slot proposals.
            if (today.HasValue && !slotWindow.StartsAfter(today.Value))
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, "The slot date must be in the future."));
                continue;
            }

            if (seenWindows.TryGetValue(window, out var firstLine))
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, $"Duplicate slot window — already used on line {firstLine}."));
                continue;
            }

            seenWindows.Add(window, lineNumber);
            rows.Add(new ConfirmedSlotImportRow(lineNumber, slotWindow, headcounts));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new ConfirmedSlotImportError(1, "The file contains no slot rows."));
        }

        return new ConfirmedSlotImportParseResult(rows, errors);
    }

    private static bool TryReadHeadcounts(
        string[] fields,
        int lineNumber,
        List<ConfirmedSlotImportError> errors,
        out IReadOnlyDictionary<Guid, int> headcounts)
    {
        var result = new Dictionary<Guid, int>();
        headcounts = result;

        for (var column = 0; column < TypeColumns.Length; column++)
        {
            var (code, id) = TypeColumns[column];
            var raw = fields[column + 2].Trim();

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var headcount)
                || headcount <= 0)
            {
                errors.Add(new ConfirmedSlotImportError(
                    lineNumber, $"{code} headcount must be a positive whole number."));
                return false;
            }

            result[id] = headcount;
        }

        return true;
    }
}
