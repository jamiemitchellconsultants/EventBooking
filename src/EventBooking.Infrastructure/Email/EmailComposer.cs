using EventBooking.Application.Abstractions;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Time;

namespace EventBooking.Infrastructure.Email;

/// <summary>Pure rendering context for one attendee email.</summary>
/// <param name="TypeCodes">The appointment type codes.</param>
/// <param name="LocationName">The location name.</param>
/// <param name="LocationAddress">The location address.</param>
/// <param name="WindowText">The window text.</param>
/// <param name="BookUrl">The booking URL.</param>
/// <param name="CoordinatorContact">The coordinator contact.</param>
/// <param name="ReplacementCreated">Whether a replacement was created.</param>
/// <param name="IsRecovery">Whether this recovers a missed appointment.</param>
/// <param name="OutstandingCount">How many appointments remain.</param>
public sealed record EmailContext(
    IReadOnlyList<string> TypeCodes,
    string LocationName,
    string LocationAddress,
    string WindowText,
    string BookUrl,
    string CoordinatorContact,
    bool ReplacementCreated,
    bool IsRecovery,
    int OutstandingCount);

/// <summary>Pure rendering: (template, context) to (subject, text, HTML).</summary>
public static class EmailComposer
{
    /// <summary>Composes the template against the context.</summary>
    /// <param name="template">The template name.</param>
    /// <param name="context">The context.</param>
    public static EmailMessage Compose(string template, EmailContext context) =>
        template switch
        {
            "AttendeeInvite" => InviteMessage(context),
            "AttendeeReinvite" => InviteMessage(context),
            "EventCancelledRebookingNeeded" => CancellationMessage(context),
            "BookingConfirmation" => ConfirmationMessage(context),
            "SelfRegistrationConfirmation" => SelfRegistrationConfirmationMessage(context),
            _ => throw new ArgumentException($"Unknown email template '{template}'.", nameof(template)),
        };

    /// <summary>Formats a window in the location's zone with its abbreviation.</summary>
    /// <param name="date">The date.</param>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="locationName">The location name.</param>
    /// <param name="abbreviation">The zone abbreviation.</param>
    public static string FormatWindow(
        DateOnly date, TimeOnly start, TimeOnly end, string locationName, string abbreviation) =>
        WindowText.Format(date, start, end, locationName, abbreviation);

    private static EmailMessage InviteMessage(EmailContext context)
    {
        var types = string.Join(", ", context.TypeCodes.OrderBy(code => code, StringComparer.Ordinal));
        var lines = new List<string>
        {
            $"You are invited to {types}.",
            context.LocationName,
            context.LocationAddress,
            context.WindowText,
            RecoveryLine(context),
            $"Book here: {context.BookUrl}",
            $"Contact: {context.CoordinatorContact}",
        };
        return new EmailMessage(Guid.Empty, string.Empty, string.Empty,
            context.IsRecovery ? EmailTemplate.AttendeeReinvite : EmailTemplate.AttendeeInvite,
            $"Invitation: {types}", string.Join("\n", lines), string.Join("\n", lines));
    }

    private static string RecoveryLine(EmailContext context) =>
        !context.IsRecovery ? "Please respond before the invitation expires."
        : context.OutstandingCount == 1 ? "1 appointment remains to be rebooked."
        : $"{context.OutstandingCount} appointments remain to be rebooked.";

    private static EmailMessage CancellationMessage(EmailContext context)
    {
        var types = string.Join(", ", context.TypeCodes.OrderBy(code => code, StringComparer.Ordinal));
        var ending = context.ReplacementCreated
            ? "a replacement has been created for you."
            : "no replacement could be created; please contact us for help.";
        var lines = new List<string>
        {
            $"Your booking for {types} was cancelled.",
            context.LocationName,
            context.LocationAddress,
            context.WindowText,
            $"Good news or bad news first: {ending}",
            $"Contact: {context.CoordinatorContact}",
        };
        return new EmailMessage(Guid.Empty, string.Empty, string.Empty,
            EmailTemplate.EventCancelledRebookingNeeded,
            $"Cancelled: {types}", string.Join("\n", lines), string.Join("\n", lines));
    }

    private static EmailMessage ConfirmationMessage(EmailContext context)
    {
        var types = string.Join(", ", context.TypeCodes.OrderBy(code => code, StringComparer.Ordinal));
        var lines = new List<string>
        {
            $"Your booking for {types} is confirmed.",
            context.LocationName,
            context.LocationAddress,
            context.WindowText,
            $"Manage here: {context.BookUrl}",
            $"Contact: {context.CoordinatorContact}",
        };
        return new EmailMessage(Guid.Empty, string.Empty, string.Empty,
            EmailTemplate.BookingConfirmation,
            $"Confirmed: {types}", string.Join("\n", lines), string.Join("\n", lines));
    }

    private static EmailMessage SelfRegistrationConfirmationMessage(EmailContext context)
    {
        var types = string.Join(", ", context.TypeCodes.OrderBy(code => code, StringComparer.Ordinal));
        var lines = new List<string>
        {
            $"Your self-registration for {types} is confirmed.",
            context.LocationName,
            context.LocationAddress,
            context.WindowText,
            $"Manage here: {context.BookUrl}",
            $"Contact: {context.CoordinatorContact}",
        };
        return new EmailMessage(Guid.Empty, string.Empty, string.Empty,
            EmailTemplate.SelfRegistrationConfirmation,
            $"Confirmed: {types}", string.Join("\n", lines), string.Join("\n", lines));
    }
}
