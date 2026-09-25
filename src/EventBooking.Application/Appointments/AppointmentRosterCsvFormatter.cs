using System.Globalization;
using System.Text;

namespace EventBooking.Application.Appointments;

/// <summary>The formatted roster: CSV body text and its filesystem-safe download filename.</summary>
public sealed record RosterCsvResult
{
    /// <summary>Gets the full CSV body including the header row and a trailing line break.</summary>
    public required string CsvText { get; init; }

    /// <summary>Gets the download filename shaped roster-slug-yyyy-MM-dd-HHmm.csv.</summary>
    public required string FileName { get; init; }
}

/// <summary>
/// Projects a scoped appointment-workspace event detail into roster CSV text plus a filename. It
/// consumes only the event detail the workspace handler already returns, so it cannot expose a
/// field the JSON event-detail route does not already expose.
/// </summary>
public sealed class AppointmentRosterCsvFormatter()
{
    private static readonly string[] HeaderFields =
    [
        "Attendee Name",
        "Attendee Email",
        "Appointment Type",
        "Status",
        "Checked In At",
        "Outcome At",
    ];

    /// <summary>Formats the given event detail as CSV text with its download filename.</summary>
    /// <param name="detail">The scoped event detail already returned by the workspace handler.</param>
    /// <returns>The CSV body text and the filesystem-safe download filename.</returns>
    public RosterCsvResult Format(AppointmentEventDetail detail)
    {
        // A literal line feed rather than AppendLine: the body must not vary with the host's
        // newline convention, and both CSV parsers in this codebase normalise either ending.
        var builder = new StringBuilder();
        builder.Append(string.Join(',', HeaderFields)).Append('\n');

        // The rows are emitted in the order the workspace returned them; never re-sorted or filtered.
        foreach (var row in detail.Appointments)
        {
            builder
                .Append(string.Join(
                    ',',
                    Escape(row.AttendeeName),
                    Escape(row.AttendeeEmail),
                    Escape(detail.AppointmentTypeName),
                    Escape(row.Status.ToString()),
                    Escape(FormatInstant(row.CheckedInAt)),
                    Escape(FormatInstant(row.OutcomeAt))))
                .Append('\n');
        }

        return new RosterCsvResult
        {
            CsvText = builder.ToString(),
            FileName = BuildFileName(detail),
        };
    }

    /// <summary>Formats a nullable instant as UTC ISO 8601, or empty when null.</summary>
    private static string FormatInstant(DateTimeOffset? instant) =>
        instant is null
            ? string.Empty
            : instant.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    /// <summary>Builds the download filename from the event's type slug, date, and start time.</summary>
    private static string BuildFileName(AppointmentEventDetail detail)
    {
        // Spaces to hyphens and lower-cased, nothing else altered or removed.
        var slug = detail.AppointmentTypeName.Replace(' ', '-').ToLowerInvariant();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"roster-{slug}-{detail.Date:yyyy-MM-dd}-{detail.StartTime:HHmm}.csv");
    }

    /// <summary>Escapes one CSV field per RFC 4180 and neutralises spreadsheet formula injection.</summary>
    private static string Escape(string value)
    {
        // OWASP: prefix cells starting with a formula trigger so Excel/Sheets treat them as text.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
