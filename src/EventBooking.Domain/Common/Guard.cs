namespace EventBooking.Domain.Common;

/// <summary>Defines guard for the current use case.</summary>
public static class Guard
{
    /// <summary>Defines against for the current use case.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="message">The message.</param>
    public static void Against(bool condition, string message)
    {
        if (condition)
        {
            throw new DomainException(message);
        }
    }

    /// <summary>Defines not blank for the current use case.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The field.</param>
    public static string NotBlank(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} must not be blank.");
        }

        return value.Trim();
    }

    /// <summary>Defines positive for the current use case.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The field.</param>
    public static int Positive(int value, string field)
    {
        if (value <= 0)
        {
            throw new DomainException($"{field} must be greater than zero.");
        }

        return value;
    }

    /// <summary>Defines not negative for the current use case.</summary>
    /// <param name="value">The value.</param>
    /// <param name="field">The field.</param>
    public static int NotNegative(int value, string field)
    {
        if (value < 0)
        {
            throw new DomainException($"{field} must not be negative.");
        }

        return value;
    }
}
