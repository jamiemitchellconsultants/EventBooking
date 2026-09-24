using System.Globalization;
using EventBooking.Application.Common;

namespace EventBooking.Mcp.Tools;

/// <summary>
/// The date and time forms the tool descriptions name and REST binds, parsed exactly and
/// invariantly. A culture-aware parse would read "03/12/2026" as a different day on
/// different hosts, so anything outside the named form is refused rather than guessed at.
/// </summary>
internal static class IsoInput
{
    /// <summary>Parses a required yyyy-MM-dd day, or refuses with the validation code.</summary>
    /// <param name="value">The caller's value.</param>
    /// <param name="field">The argument name, for the refusal message.</param>
    /// <returns>The day.</returns>
    internal static DateOnly Date(string? value, string field) =>
        DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw Refusal($"{field} must be a yyyy-MM-dd date.");

    /// <summary>Parses an optional yyyy-MM-dd day, or refuses with the validation code.</summary>
    /// <param name="value">The caller's value, or null.</param>
    /// <param name="field">The argument name, for the refusal message.</param>
    /// <returns>The day, or null.</returns>
    internal static DateOnly? OptionalDate(string? value, string field) =>
        value is null ? null : Date(value, field);

    /// <summary>Parses a required HH:mm time, or refuses with the validation code.</summary>
    /// <param name="value">The caller's value.</param>
    /// <param name="field">The argument name, for the refusal message.</param>
    /// <returns>The time.</returns>
    internal static TimeOnly Time(string? value, string field) =>
        TimeOnly.TryParseExact(
            value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw Refusal($"{field} must be an HH:mm time.");

    private static ModelContextProtocol.McpException Refusal(string message) =>
        McpErrors.ToMcpException(Error.Validation(message));
}
