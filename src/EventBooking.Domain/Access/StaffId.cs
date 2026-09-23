using System.Text.RegularExpressions;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Access;

/// <summary>A canonical staff number validated against the deployment's configured format.</summary>
public sealed record StaffId
{
    /// <summary>The organisation-neutral format used when no deployment override is supplied.</summary>
    public const string DefaultPattern = "^[A-Z0-9]{1,32}$";

    /// <summary>Trims, uppercases and validates an identity-provider staff number.</summary>
    /// <param name="value">The untrusted input, including any surrounding whitespace.</param>
    /// <param name="pattern">The deployment's complete-value validation expression.</param>
    /// <exception cref="DomainException">The input violates the configured format or size bound.</exception>
    public StaffId(string? value, string pattern = DefaultPattern)
    {
        var canonical = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Matches(canonical, pattern))
            throw new DomainException("staffId does not match the configured format.");
        Value = canonical;
    }

    /// <summary>Gets the normalized identifier; equality compares this value.</summary>
    public string Value { get; }

    /// <summary>Parses input using the deployment's format.</summary>
    /// <param name="value">The untrusted staff number.</param>
    /// <param name="pattern">The deployment's complete-value expression.</param>
    /// <returns>The canonical staff number.</returns>
    public static StaffId Parse(string? value, string pattern = DefaultPattern) => new(value, pattern);

    /// <summary>Validates input without throwing for malformed staff numbers.</summary>
    /// <param name="value">The untrusted staff number.</param>
    /// <param name="staffId">The parsed value, or null on refusal.</param>
    /// <param name="pattern">The deployment's complete-value expression.</param>
    /// <returns>True when the input is valid under the supplied policy.</returns>
    public static bool TryParse(string? value, out StaffId? staffId, string pattern = DefaultPattern)
    {
        try { staffId = new StaffId(value, pattern); return true; }
        catch (DomainException) { staffId = null; return false; }
    }

    /// <summary>Rehydrates a stored identifier without applying a later deployment policy.</summary>
    /// <param name="value">The canonical identifier stored by an earlier authenticated request.</param>
    /// <returns>The same stored identifier without changing its representation.</returns>
    public static StaffId FromPersisted(string value)
    {
        if (value != value.Trim().ToUpperInvariant())
            throw new DomainException("Stored staffId is not canonical.");
        return new StaffId(value, "^.{1,32}$");
    }

    /// <summary>Returns the canonical identifier.</summary>
    /// <returns>The value, without a presentation prefix.</returns>
    public override string ToString() => Value;

    private static bool Matches(string canonical, string pattern)
    {
        if (canonical.Length is < 1 or > 32) return false;
        try
        {
            var match = Regex.Match(canonical, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return match.Success && match.Index == 0 && match.Length == canonical.Length;
        }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
