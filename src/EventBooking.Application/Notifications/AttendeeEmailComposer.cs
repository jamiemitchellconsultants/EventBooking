using System.Globalization;
using System.Text;
using EventBooking.Application.Abstractions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Notifications;

/// <summary>
/// Pure text composition for the 4 attendee emails. No clock, no repository, no mail service —
/// every output is a function of the arguments, so the wording can be asserted in a unit test.
/// </summary>
public static class AttendeeEmailComposer
{
    /// <summary>Formats a four-hour event window using invariant, human-readable wording.</summary>
    /// <param name="window">The window.</param>
    public static string FormatWindow(EventWindow window) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0:dddd d MMM yyyy}, {1:HH\\:mm}-{2:HH\\:mm}",
            window.Date,
            window.StartTime,
            window.EndTime);

    /// <summary>Composes an initial, reminder, or recovery Invite from persisted type IDs.</summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="options">The options.</param>
    /// <param name="bookingUrl">The booking url.</param>
    /// <param name="isReinvite">The is reinvite.</param>
    /// <param name="isRecovery">The is recovery.</param>
    public static EmailMessage Invite(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        IReadOnlyList<Event> options,
        string bookingUrl,
        bool isReinvite,
        bool isRecovery)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();

        if (isRecovery)
        {
            text.AppendLine(
                appointmentTypeIds.Count == 1
                    ? "You have a missed appointment, so here are new times to complete it."
                    : "You have missed appointments, so here are new times to complete them.");
        }
        else if (isReinvite)
        {
            text.AppendLine(
                "We have not heard back about your appointments, so here are the latest available times.");
        }
        else
        {
            text.AppendLine("Please choose one of the following times for your appointments.");
        }

        text.AppendLine();
        text.AppendLine($"Appointments: {types}");
        text.AppendLine();

        foreach (var option in options)
        {
            text.AppendLine($"  - {FormatWindow(option.Window)}");
        }

        text.AppendLine();
        text.AppendLine("Choose your time here:");
        text.AppendLine(bookingUrl);

        return new EmailMessage(
            attendee.Id,
            attendee.Email,
            attendee.Name,
            isReinvite ? EmailTemplate.AttendeeReinvite : EmailTemplate.AttendeeInvite,
            isReinvite
                ? "Reminder: choose a time for your appointments"
                : "Choose a time for your appointments",
            text.ToString(),
            AsHtml(text.ToString(), bookingUrl, "Choose your time"));
    }

    /// <summary>Composes confirmation and names every booked snapshot type.</summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="manageUrl">The manage url.</param>
    /// <param name="portal">The portal.</param>
    public static EmailMessage BookingConfirmation(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        Event eventItem,
        string manageUrl,
        AttendeePortalOptions portal)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();
        text.AppendLine("Your appointments are confirmed for:");
        text.AppendLine($"  {FormatWindow(eventItem.Window)}");
        text.AppendLine($"  {portal.TransitionalLocationAddress}");
        text.AppendLine();
        text.AppendLine($"Appointments: {types}");
        text.AppendLine();
        text.AppendLine("Need to change or cancel? Use this link:");
        text.AppendLine(manageUrl);
        text.AppendLine();
        text.AppendLine($"Any questions, contact {portal.CoordinatorContact}.");

        return new EmailMessage(
            attendee.Id,
            attendee.Email,
            attendee.Name,
            EmailTemplate.BookingConfirmation,
            "Your appointment is confirmed",
            text.ToString(),
            AsHtml(text.ToString(), manageUrl, "Cancel or reschedule"));
    }

    /// <summary>
    /// Composes a cancellation notice whose recovery wording reflects whether a replacement invite
    /// was actually delivered. Pending or failed replacement delivery receives neutral wording.
    /// </summary>
    /// <param name="attendee">The attendee.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    /// <param name="eventItem">The eventItem.</param>
    /// <param name="replacementInviteSent">The replacement invite sent.</param>
    public static EmailMessage EventCancelled(
        Attendee attendee,
        IReadOnlyCollection<Guid> appointmentTypeIds,
        Event eventItem,
        bool replacementInviteSent = false)
    {
        var types = FormatTypes(appointmentTypeIds);

        var text = new StringBuilder();
        text.AppendLine($"Hi {attendee.Name},");
        text.AppendLine();
        text.AppendLine(
            $"We are sorry — your {AppointmentNoun(appointmentTypeIds.Count)} on {FormatWindow(eventItem.Window)} has had to be cancelled.");
        text.AppendLine();
        text.AppendLine($"Affected appointments: {types}");
        text.AppendLine();
        if (replacementInviteSent)
        {
            text.AppendLine("A new invitation with fresh times is on its way to you.");
        }
        else
        {
            text.AppendLine("The recruitment team will contact you with the next available times.");
        }

        return new EmailMessage(
            attendee.Id,
            attendee.Email,
            attendee.Name,
            EmailTemplate.EventCancelledRebookingNeeded,
            "Your appointment time has been cancelled",
            text.ToString(),
            AsHtml(text.ToString(), null, null));
    }

    /// <summary>Names snapshot types in deterministic code order for every template.</summary>
    private static string FormatTypes(IReadOnlyCollection<Guid> appointmentTypeIds) =>
        string.Join(
            ", ",
            appointmentTypeIds
                .Select(id => (Code: AppointmentTypeIds.CodeOf(id), Name: AppointmentTypeIds.NameOf(id)))
                .OrderBy(entry => entry.Code, StringComparer.Ordinal)
                .Select(entry => entry.Name));

    /// <summary>Uses one/appointments grammar shared by every template.</summary>
    private static string AppointmentNoun(int count) =>
        count == 1 ? "appointment" : "appointments";

    private static string AsHtml(string text, string? actionUrl, string? actionLabel)
    {
        var body = new StringBuilder();
        body.AppendLine("<html><body style=\"font-family:sans-serif;font-size:15px\">");

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            body.AppendLine(line.Length == 0 ? "<br />" : $"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>");
        }

        if (actionUrl is not null && actionLabel is not null)
        {
            body.AppendLine($"<p><a href=\"{actionUrl}\">{actionLabel}</a></p>");
        }

        body.AppendLine("</body></html>");
        return body.ToString();
    }
}
