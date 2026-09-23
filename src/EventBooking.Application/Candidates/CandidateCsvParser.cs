namespace EventBooking.Application.Candidates;

/// <summary>Defines candidate csv row for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Name">The name.</param>
/// <param name="Email">The email.</param>
/// <param name="EmployeeGroupCode">The employee group code.</param>
public sealed record CandidateCsvRow(
    int LineNumber,
    string Name,
    string Email,
    string EmployeeGroupCode);

/// <summary>Defines candidate csv error for the current use case.</summary>
/// <param name="LineNumber">The line number.</param>
/// <param name="Message">The message.</param>
public sealed record CandidateCsvError(int LineNumber, string Message);

/// <summary>Defines candidate csv parse result for the current use case.</summary>
/// <param name="Rows">The rows.</param>
/// <param name="Errors">The errors.</param>
public sealed record CandidateCsvParseResult(
    IReadOnlyList<CandidateCsvRow> Rows,
    IReadOnlyList<CandidateCsvError> Errors);

/// <summary>
/// Structural validation only — field count, blank fields, one canonicalized Employee Group code,
/// and duplicate emails within the file. Group existence is the import handler's rule.
/// </summary>
public static class CandidateCsvParser
{
    /// <summary>Defines required header for the current use case.</summary>
    public const string RequiredHeader = "name,email,employee_group";

    /// <summary>Defines parse for the current use case.</summary>
    /// <param name="content">The content.</param>
    public static CandidateCsvParseResult Parse(string? content)
    {
        var rows = new List<CandidateCsvRow>();
        var errors = new List<CandidateCsvError>();

        if (string.IsNullOrWhiteSpace(content))
        {
            errors.Add(new CandidateCsvError(0, "The file is empty."));
            return new CandidateCsvParseResult(rows, errors);
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        if (!string.Equals(lines[0].Trim(), RequiredHeader, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new CandidateCsvError(1, $"The header line must read exactly: {RequiredHeader}"));
            return new CandidateCsvParseResult(rows, errors);
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
                errors.Add(new CandidateCsvError(
                    lineNumber, "Expected 3 comma-separated fields: name, email, employee_group."));
                continue;
            }

            var name = fields[0].Trim();
            var email = fields[1].Trim();
            var groupCode = fields[2].Trim();

            if (name.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Name is required."));
                continue;
            }

            if (email.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Email is required."));
                continue;
            }

            if (groupCode.Length == 0)
            {
                errors.Add(new CandidateCsvError(lineNumber, "Employee group is required."));
                continue;
            }

            if (!seenEmails.Add(email))
            {
                errors.Add(new CandidateCsvError(
                    lineNumber, $"{email.ToLowerInvariant()} appears more than once in this file."));
                continue;
            }

            rows.Add(new CandidateCsvRow(
                lineNumber, name, email.ToLowerInvariant(), groupCode.ToUpperInvariant()));
        }

        if (errors.Count == 0 && rows.Count == 0)
        {
            errors.Add(new CandidateCsvError(1, "The file contains no candidate rows."));
        }

        return new CandidateCsvParseResult(rows, errors);
    }
}
