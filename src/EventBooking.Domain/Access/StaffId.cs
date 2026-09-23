using System.Text.RegularExpressions;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>
/// The enterprise staff number issued by HR: <c>U</c> or <c>N</c> followed by six digits.
/// Input is case-insensitive and the stored value is always uppercase.
/// </summary>
public sealed partial record StaffId
{
    /// <summary>Creates a canonical staff number from a valid seven-character value.</summary>
    /// <param name="value">The untrimmed value to validate without accepting surrounding whitespace.</param>
    /// <exception cref="DomainException">Thrown when the value is absent or malformed.</exception>
    public StaffId(string value)
    {
        Guard.Against(value is null || !StaffIdPattern().IsMatch(value),
            "staffId must be U or N followed by 6 digits.");
        Value = value!.ToUpperInvariant();
    }

    /// <summary>Gets the canonical uppercase seven-character staff number.</summary>
    public string Value { get; }

    /// <summary>Parses a staff number, throwing when the value is malformed.</summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>The canonical staff number.</returns>
    public static StaffId Parse(string value) => new(value);

    /// <summary>Attempts to parse untrusted input without throwing.</summary>
    /// <param name="value">The potentially absent or malformed value.</param>
    /// <param name="staffId">The canonical staff number when parsing succeeds; otherwise null.</param>
    /// <returns><see langword="true"/> only when the value has the required shape.</returns>
    public static bool TryParse(string? value, out StaffId? staffId)
    {
        if (value is not null && StaffIdPattern().IsMatch(value))
        {
            staffId = new StaffId(value);
            return true;
        }

        staffId = null;
        return false;
    }

    /// <summary>Returns the canonical uppercase staff number.</summary>
    /// <returns>The same value exposed by <see cref="Value"/>.</returns>
    public override string ToString() => Value;

    [GeneratedRegex("^[UuNn][0-9]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex StaffIdPattern();
}
