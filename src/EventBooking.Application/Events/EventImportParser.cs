using System.Globalization;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Events;

/// <summary>Defines event import row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Window">The window.</param>
/// <param name="HeadcountsByAppointmentType">The headcounts by appointment type.</param>
public sealed record EventImportRow(
    int LineNumber, EventWindow Window, IReadOnlyDictionary<Guid, int> HeadcountsByAppointmentType);

/// <summary>Defines event import error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record EventImportError(int LineNumber, string Message);

/// <summary>Defines event import parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record EventImportParseResult(
    IReadOnlyList<EventImportRow> Rows,
    IReadOnlyList<EventImportError> Errors);

/// <summary>
/// Structural validation only, mirroring AttendeeCsvParser (Task 29): header, field count,
/// parseable date/time, a positive integer per fixed AppointmentType column, duplicate windows
/// within the file, and a row-count ceiling. Nothing here reads the database.
/// </summary>
public static class EventImportParser
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
    public static EventImportParseResult Parse(string? content, DateOnly? today = null)
    {
        var rows = new List<EventImportRow>();
        var errors = new List<EventImportError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new EventImportError(0, "The file is empty."));
            return new EventImportParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new EventImportError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new EventImportParseResult(rows, errors);
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
            errors.Add(new EventImportError(0, $"A file may contain at most 200 rows."));
            return new EventImportParseResult(rows, errors);
        }

        var seenWindows = new Dictionary<(DateOnly Date, TimeOnly StartTime), int>();

        foreach (var index in dataLineNumbers)
        {
            var lineNumber = index + 1;
            var fields = lines[index].Split(',');

            if (fields.Length != 5)
            {
                errors.Add(new EventImportError(
                    lineNumber, "Expected 5 comma-separated fields: date,startTime,DAT,MED,UNI."));
                continue;
            }

            if (!DateOnly.TryParseExact(
                    fields[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                errors.Add(new EventImportError(lineNumber, $"{fields[0].Trim()} is not a valid date (expected yyyy-MM-dd)."));
                continue;
            }

            if (!TimeOnly.TryParseExact(
                    fields[1].Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
            {
                errors.Add(new EventImportError(lineNumber, $"{fields[1].Trim()} is not a valid startTime (expected HH:mm)."));
                continue;
            }

            if (!TryReadHeadcounts(fields, lineNumber, errors, out var headcounts))
            {
                continue;
            }

            var window = (date, startTime);

            EventWindow eventWindow;
            try
            {
                eventWindow = new EventWindow(date, startTime);
            }
            catch (DomainException ex)
            {
                errors.Add(new EventImportError(lineNumber, ex.Message));
                continue;
            }

            // Imported events follow the same future-date rule as event proposals.
            if (today.HasValue && !eventWindow.StartsAfter(today.Value))
            {
                errors.Add(new EventImportError(
                    lineNumber, "The event date must be in the future."));
                continue;
            }

            if (seenWindows.TryGetValue(window, out var firstLine))
            {
                errors.Add(new EventImportError(
                    lineNumber, $"Duplicate event window — already used on line {firstLine}."));
                continue;
            }

            seenWindows.Add(window, lineNumber);
            rows.Add(new EventImportRow(lineNumber, eventWindow, headcounts));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new EventImportError(1, "The file contains no event rows."));
        }

        return new EventImportParseResult(rows, errors);
    }

    private static bool TryReadHeadcounts(
        string[] fields,
        int lineNumber,
        List<EventImportError> errors,
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
                errors.Add(new EventImportError(
                    lineNumber, $"{code} headcount must be a positive whole number."));
                return false;
            }

            result[id] = headcount;
        }

        return true;
    }
}
