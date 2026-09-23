using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Attendees;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Events;

namespace EventBooking.Application.Tests.Notifications;

public class AttendeeEmailComposerTests
{
    private static readonly AttendeePortalOptions Portal = new(
        "https://booking.example.com", "recruitment@corp.com");

    private static readonly Attendee Amara = Attendee.Create(
        Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
        AttendeeGroup.Define(
            AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));

    [Fact]
    public void AWindowIsFormattedForAHumanReader()
    {
        var window = new EventWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0), 240);

        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", AttendeeEmailComposer.FormatWindow(window));
    }

    [Fact]
    public void TheInviteNamesTheAttendeeTheirTypesAndAllThreeOptions()
    {
        var options = new[] { EventOn(10, 9), EventOn(11, 13), EventOn(13, 9) };

        var message = AttendeeEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, options, "https://booking.example.com/book/abc",
            isReinvite: false, isRecovery: false);

        Assert.Equal(Amara.Id, message.AttendeeId);
        Assert.Equal("a.novak@mail.com", message.ToAddress);
        Assert.Equal("Amara Novak", message.ToName);
        Assert.Equal(EmailTemplate.AttendeeInvite, message.Template);
        Assert.Equal("Choose a time for your appointments", message.Subject);

        Assert.Contains("Hi Amara Novak", message.TextBody);
        Assert.Contains("Drug & Alcohol Testing", message.TextBody);
        Assert.Contains("Uniform Fitting", message.TextBody);
        Assert.DoesNotContain("Medical Check-up", message.TextBody);
        Assert.Contains("Thursday 10 Sep 2026, 09:00-13:00", message.TextBody);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("Sunday 13 Sep 2026, 09:00-13:00", message.TextBody);
        Assert.Contains("https://booking.example.com/book/abc", message.TextBody);
        Assert.Contains("https://booking.example.com/book/abc", message.HtmlBody);
    }

    [Fact]
    public void TheInviteNeverLeaksCapacityNumbers()
    {
        var eventItem = EventOn(10, 9);

        var message = AttendeeEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [eventItem, EventOn(11, 13), EventOn(13, 9)],
            "https://x/book/abc", isReinvite: false, isRecovery: false);

        Assert.DoesNotContain("remaining", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capacity", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10", message.Subject);
    }

    [Fact]
    public void AReInviteUsesItsOwnTemplateAndSubject()
    {
        var message = AttendeeEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [EventOn(10, 9), EventOn(11, 13), EventOn(13, 9)],
            "https://x/book/abc", isReinvite: true, isRecovery: false);

        Assert.Equal(EmailTemplate.AttendeeReinvite, message.Template);
        Assert.Equal("Reminder: choose a time for your appointments", message.Subject);
        Assert.Contains("We have not heard back", message.TextBody);
    }

    [Fact]
    public void TheConfirmationCarriesTheChosenTimeAndTheManageLinkButNoDeploymentAddress()
    {
        var message = AttendeeEmailComposer.BookingConfirmation(
            Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13),
            "https://booking.example.com/manage/xyz", Portal);

        Assert.Equal(EmailTemplate.BookingConfirmation, message.Template);
        Assert.Equal("Your appointment is confirmed", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.DoesNotContain("Corporate HQ", message.TextBody);
        Assert.Contains("https://booking.example.com/manage/xyz", message.TextBody);
        Assert.Contains("recruitment@corp.com", message.TextBody);
    }

    [Fact]
    public void TheCancellationApologisesAndPromisesANewInvite()
    {
        var message = AttendeeEmailComposer.EventCancelled(Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13), replacementInviteSent: true);

        Assert.Equal(EmailTemplate.EventCancelledRebookingNeeded, message.Template);
        Assert.Equal("Your appointment time has been cancelled", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("new invitation", message.TextBody);
    }

    /// <summary>Cancellation wording stays neutral until replacement delivery succeeds.</summary>
    [Fact]
    public void TheCancellationDoesNotPromiseAUnsentReplacement()
    {
        var message = AttendeeEmailComposer.EventCancelled(Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13));

        Assert.Contains("recruitment team will contact you", message.TextBody);
        Assert.DoesNotContain("on its way", message.TextBody);
    }

    [Fact]
    public void EveryMessageHasBothATextAndAnHtmlBody()
    {
        var messages = new[]
        {
            AttendeeEmailComposer.Invite(
                Amara, Amara.RequiredAppointmentTypeIds, [EventOn(10, 9), EventOn(11, 13), EventOn(13, 9)],
                "https://x/b", false, false),
            AttendeeEmailComposer.BookingConfirmation(
                Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13), "https://x/m", Portal),
            AttendeeEmailComposer.EventCancelled(Amara, Amara.RequiredAppointmentTypeIds, EventOn(11, 13)),
        };

        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m.TextBody)));
        Assert.All(messages, m => Assert.Contains("<html", m.HtmlBody, StringComparison.OrdinalIgnoreCase));
    }

    private static Event EventOn(int day, int hour)
    {
        var proposal = ProposalFixture.Create(
            Guid.NewGuid(), new EventWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0), 240),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return Event.CreateFrom(Guid.NewGuid(), proposal);
    }
}
