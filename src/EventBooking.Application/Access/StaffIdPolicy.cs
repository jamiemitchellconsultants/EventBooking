using System.Text.RegularExpressions;
using EventBooking.Domain.Access;

namespace EventBooking.Application.Access;

/// <summary>Immutable deployment configuration shared by every staff-number input boundary.</summary>
public sealed class StaffIdPolicy
{
    /// <summary>Validates the deployment expression when the host starts.</summary>
    /// <param name="pattern">The configured regular expression, or the default when absent.</param>
    public StaffIdPolicy(string? pattern = null)
    {
        Pattern = pattern ?? StaffId.DefaultPattern;
        _ = new Regex(Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Gets the expression applied after trimming and uppercasing staff identifiers.</summary>
    public string Pattern { get; }
}
