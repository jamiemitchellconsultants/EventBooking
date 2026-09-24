namespace EventBooking.Application.Appointments;

/// <summary>Renders workspace roster rows as neutralised CSV.</summary>
public static class RosterCsv
{
    /// <summary>
    /// Renders the header and one line per row. Neutralise formula cells: any cell starting
    /// with =, +, -, @, tab or carriage return is prefixed with a single quote. CRLF endings.
    /// </summary>
    /// <param name="rows">The roster rows.</param>
    /// <param name="headers">The header cells.</param>
    public static string Render(IReadOnlyList<WorkspaceRosterRow> rows, IReadOnlyList<string> headers)
    {
        static string Cell(string value) =>
            value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
        var lines = new List<string> { string.Join(",", headers.Select(Cell)) };
        lines.AddRange(rows.Select(r => string.Join(",", new[]
        {
            Cell(r.Name), Cell(r.Email), Cell(r.AppointmentStatus),
            Cell(r.CheckedInAt?.ToString("o") ?? string.Empty), Cell(r.Version.ToString()),
        })));
        return string.Join("\r\n", lines) + "\r\n";
    }
}
