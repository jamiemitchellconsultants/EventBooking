namespace EventBooking.Application.Appointments;

/// <summary>Renders workspace roster rows as neutralised CSV.</summary>
public static class RosterCsv
{
    /// <summary>
    /// Renders the header and one line per row: the on-screen columns without the command
    /// target id or the version (FR-8.7). Cells are RFC 4180 quoted, and neutralised first:
    /// any cell starting with =, +, -, @, tab or carriage return is prefixed with a single
    /// quote (FR-8.8). CRLF endings.
    /// </summary>
    /// <param name="rows">The roster rows.</param>
    /// <param name="headers">The header cells.</param>
    public static string Render(IReadOnlyList<WorkspaceRosterRow> rows, IReadOnlyList<string> headers)
    {
        static string Cell(string value)
        {
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
            {
                value = "'" + value;
            }

            return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }

        var lines = new List<string> { string.Join(",", headers.Select(Cell)) };
        lines.AddRange(rows.Select(r => string.Join(",", new[]
        {
            Cell(r.Name), Cell(r.Email), Cell(r.AppointmentStatus),
            Cell(r.CheckedInAt?.ToString("o") ?? string.Empty),
        })));
        return string.Join("\r\n", lines) + "\r\n";
    }
}
