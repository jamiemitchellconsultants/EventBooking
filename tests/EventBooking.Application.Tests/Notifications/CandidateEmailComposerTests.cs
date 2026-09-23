using EventBooking.Application.Notifications;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Candidates;
using EventBooking.Domain.EmployeeGroups;
using EventBooking.Domain.Notifications;
using EventBooking.Domain.Slots;

namespace EventBooking.Application.Tests.Notifications;

public class CandidateEmailComposerTests
{
    private static readonly CandidatePortalOptions Portal = new(
        "https://booking.example.com", "Corporate HQ, 1 Example Street", "recruitment@corp.com");

    private static readonly Candidate Amara = Candidate.Create(
        Guid.NewGuid(), "Amara Novak", "a.novak@mail.com",
        EmployeeGroup.Define(
            EmployeeGroupIds.Pilots, "PILOTS", "Pilots", true,
            [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]));

    [Fact]
    public void AWindowIsFormattedForAHumanReader()
    {
        var window = new SlotWindow(new DateOnly(2026, 9, 10), new TimeOnly(9, 0));

        Assert.Equal("Thursday 10 Sep 2026, 09:00-13:00", CandidateEmailComposer.FormatWindow(window));
    }

    [Fact]
    public void TheInviteNamesTheCandidateTheirTypesAndAllThreeOptions()
    {
        var options = new[] { SlotOn(10, 9), SlotOn(11, 13), SlotOn(13, 9) };

        var message = CandidateEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, options, "https://booking.example.com/book/abc",
            isReinvite: false, isRecovery: false);

        Assert.Equal(Amara.Id, message.CandidateId);
        Assert.Equal("a.novak@mail.com", message.ToAddress);
        Assert.Equal("Amara Novak", message.ToName);
        Assert.Equal(EmailTemplate.CandidateInvite, message.Template);
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
        var slot = SlotOn(10, 9);

        var message = CandidateEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [slot, SlotOn(11, 13), SlotOn(13, 9)],
            "https://x/book/abc", isReinvite: false, isRecovery: false);

        Assert.DoesNotContain("remaining", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capacity", message.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10", message.Subject);
    }

    [Fact]
    public void AReInviteUsesItsOwnTemplateAndSubject()
    {
        var message = CandidateEmailComposer.Invite(
            Amara, Amara.RequiredAppointmentTypeIds, [SlotOn(10, 9), SlotOn(11, 13), SlotOn(13, 9)],
            "https://x/book/abc", isReinvite: true, isRecovery: false);

        Assert.Equal(EmailTemplate.CandidateReinvite, message.Template);
        Assert.Equal("Reminder: choose a time for your appointments", message.Subject);
        Assert.Contains("We have not heard back", message.TextBody);
    }

    [Fact]
    public void TheConfirmationCarriesTheChosenTimeTheAddressAndTheManageLink()
    {
        var message = CandidateEmailComposer.BookingConfirmation(
            Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13),
            "https://booking.example.com/manage/xyz", Portal);

        Assert.Equal(EmailTemplate.BookingConfirmation, message.Template);
        Assert.Equal("Your appointment is confirmed", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("Corporate HQ, 1 Example Street", message.TextBody);
        Assert.Contains("https://booking.example.com/manage/xyz", message.TextBody);
        Assert.Contains("recruitment@corp.com", message.TextBody);
    }

    [Fact]
    public void TheCancellationApologisesAndPromisesANewInvite()
    {
        var message = CandidateEmailComposer.SlotCancelled(Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13), replacementInviteSent: true);

        Assert.Equal(EmailTemplate.SlotCancelledRebookingNeeded, message.Template);
        Assert.Equal("Your appointment time has been cancelled", message.Subject);
        Assert.Contains("Friday 11 Sep 2026, 13:00-17:00", message.TextBody);
        Assert.Contains("new invitation", message.TextBody);
    }

    /// <summary>Cancellation wording stays neutral until replacement delivery succeeds.</summary>
    [Fact]
    public void TheCancellationDoesNotPromiseAUnsentReplacement()
    {
        var message = CandidateEmailComposer.SlotCancelled(Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13));

        Assert.Contains("recruitment team will contact you", message.TextBody);
        Assert.DoesNotContain("on its way", message.TextBody);
    }

    [Fact]
    public void EveryMessageHasBothATextAndAnHtmlBody()
    {
        var messages = new[]
        {
            CandidateEmailComposer.Invite(
                Amara, Amara.RequiredAppointmentTypeIds, [SlotOn(10, 9), SlotOn(11, 13), SlotOn(13, 9)],
                "https://x/b", false, false),
            CandidateEmailComposer.BookingConfirmation(
                Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13), "https://x/m", Portal),
            CandidateEmailComposer.SlotCancelled(Amara, Amara.RequiredAppointmentTypeIds, SlotOn(11, 13)),
        };

        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m.TextBody)));
        Assert.All(messages, m => Assert.Contains("<html", m.HtmlBody, StringComparison.OrdinalIgnoreCase));
    }

    private static ConfirmedSlot SlotOn(int day, int hour)
    {
        var proposal = SlotProposal.Create(
            Guid.NewGuid(), new SlotWindow(new DateOnly(2026, 9, day), new TimeOnly(hour, 0)),
            Guid.NewGuid());
        proposal.Accept(AppointmentTypeIds.DrugAndAlcoholTesting, Guid.NewGuid(), 10);
        proposal.Accept(AppointmentTypeIds.MedicalCheckUp, Guid.NewGuid(), 6);
        proposal.Accept(AppointmentTypeIds.UniformFitting, Guid.NewGuid(), 8);

        return ConfirmedSlot.CreateFrom(Guid.NewGuid(), proposal);
    }
}
