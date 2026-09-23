namespace EventBooking.Infrastructure.Email;

/// <summary>The local SMTP catcher's address — Mailpit in Docker Compose.</summary>
public sealed record SmtpOptions(string Host, int Port);
