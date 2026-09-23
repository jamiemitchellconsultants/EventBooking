using System.Globalization;

namespace EventBooking.Api.Endpoints;

/// <summary>Shares invariant optional-timestamp parsing between HTTP and MCP surfaces.</summary>
public static class AuditInputParser
{
    /// <summary>Parses an optional invariant timestamp; blank is a successful null.</summary>
    /// <param name="value">The raw bound text, or null when the bound is absent.</param>
    /// <param name="bound">The parsed bound, or null when absent.</param>
    /// <returns>True when absent or a valid timestamp; false otherwise.</returns>
    public static bool TryParseBound(string? value, out DateTimeOffset? bound)
    {
        bound = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        bound = parsed;
        return true;
    }
}
