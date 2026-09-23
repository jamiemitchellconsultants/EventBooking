namespace EventBooking.Infrastructure.Tokens;

/// <summary>
/// The secret behind every candidate link. Supplied from configuration; never checked in.
/// Rotating it invalidates every outstanding invite and cancel link, which is the intended
/// emergency behaviour.
/// </summary>
public sealed record TokenOptions(string SigningKey)
{
    public override string ToString() => "TokenOptions { SigningKey = [REDACTED] }";
}
