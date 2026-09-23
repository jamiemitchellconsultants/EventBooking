namespace EventBooking.Application.Attendees;

/// <summary>Defines attendee csv row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="AttendeeGroupCode">The attendee group code.</param>
public sealed record AttendeeCsvRow(
    int LineNumber,
    string Name,
    string Email,
    string AttendeeGroupCode);

/// <summary>Defines attendee csv error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record AttendeeCsvError(int LineNumber, string Message);

/// <summary>Defines attendee csv parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record AttendeeCsvParseResult(
    IReadOnlyList<AttendeeCsvRow> Rows,
    IReadOnlyList<AttendeeCsvError> Errors);

/// <summary>
/// Structural validation only — field count, blank fields, one canonicalized Attendee Group code,
/// and duplicate emails within the file. Group existence is the import handler's rule.
/// </summary>
public static class AttendeeCsvParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "name,email,attendee_group";

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    public static AttendeeCsvParseResult Parse(string? content)
    {
        var rows = new List<AttendeeCsvRow>();
        var errors = new List<AttendeeCsvError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new AttendeeCsvError(0, "The file is empty."));
            return new AttendeeCsvParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new AttendeeCsvError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new AttendeeCsvParseResult(rows, errors);
        }

        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(',');
            if (fields.Length != 3)
            {
                errors.Add(new AttendeeCsvError(
                    lineNumber, "Expected 3 comma-separated fields: name, email, attendee_group."));
                continue;
            }

            var name = fields[0].Trim();
            var email = fields[1].Trim();
            var groupCode = fields[2].Trim();

            if (name.Length == 0)
            {
                errors.Add(new AttendeeCsvError(lineNumber, "Name is required."));
                continue;
            }

            if (email.Length == 0)
            {
                errors.Add(new AttendeeCsvError(lineNumber, "Email is required."));
                continue;
            }

            if (groupCode.Length == 0)
            {
                errors.Add(new AttendeeCsvError(lineNumber, "Attendee group is required."));
                continue;
            }

            if (!seenEmails.Add(email))
            {
                errors.Add(new AttendeeCsvError(
                    lineNumber, $"{email.ToLowerInvariant()} appears more than once in this file."));
                continue;
            }

            rows.Add(new AttendeeCsvRow(
                lineNumber, name, email.ToLowerInvariant(), groupCode.ToUpperInvariant()));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new AttendeeCsvError(1, "The file contains no attendee rows."));
        }

        return new AttendeeCsvParseResult(rows, errors);
    }
}
