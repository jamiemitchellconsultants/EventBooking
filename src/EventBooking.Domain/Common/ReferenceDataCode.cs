using System.Text.RegularExpressions;

namespace EventBooking.Domain.Common;

/// <summary>
/// The canonical form of a reference-data code. Codes are accepted case-insensitively at input
/// boundaries and stored uppercase, because they are the stable identifiers in CSV files, MCP
/// tools and email ordering.
/// </summary>
public static partial class ReferenceDataCode
{
    /// <summary>Normalises and validates a code, or refuses it.</summary>
    /// <param name="value">The supplied code, in any case, with or without surrounding space.</param>
    /// <param name="maximumLength">The longest code this kind of reference data allows.</param>
    /// <param name="field">The field name to use in a refusal message.</param>
    public static string Parse(string? value, int maximumLength, string field)
    {
        var canonical = (value ?? string.Empty).Trim().ToUpperInvariant();

        Guard.Against(
            !CanonicalExpression().IsMatch(canonical),
            $"{field} must be canonical uppercase snake case.");
        Guard.Against(
            canonical.Length > maximumLength,
            $"{field} must be at most {maximumLength} characters.");

        return canonical;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalExpression();
}
