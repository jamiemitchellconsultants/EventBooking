using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Events;
using EventBooking.Domain.Time;

namespace EventBooking.TestSupport;

/// <summary>
/// Builds proposals in the shape Task 6 introduced while the inherited suites still exercise the
/// rules that did not change. The three predecessor types are listed, with drug and alcohol
/// testing as the proposing type; negotiation itself is covered by the N-type suite.
/// </summary>
public static class ProposalFixture
{
    /// <summary>The proposing type used by every proposal this fixture builds.</summary>
    public static Guid ProposerType => AppointmentTypeIds.DrugAndAlcoholTesting;

    /// <summary>The zone abstraction: every local time names exactly one instant.</summary>
    public static IEventWindowZones Zones { get; } = new UniqueZones();

    /// <summary>The zone every fixture proposal is read in.</summary>
    public const string TimeZoneId = "Europe/London";

    /// <summary>An instant well before any fixture window.</summary>
    public static DateTimeOffset Now => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Proposes with the inherited three types listed, keeping the old call shape.</summary>
    /// <param name="id">The proposal identifier.</param>
    /// <param name="window">The proposed window.</param>
    /// <param name="createdByManagerUserId">The proposing Manager.</param>
    /// <param name="proposerAppointmentTypeId">The proposing type; drug and alcohol testing by default.</param>
    public static EventProposal Create(
        Guid id,
        EventWindow window,
        Guid createdByManagerUserId,
        Guid? proposerAppointmentTypeId = null) =>
        EventProposal.Propose(
            id,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            locationIsActive: true,
            TimeZoneId,
            window,
            Zones,
            Now,
            [
                new(AppointmentTypeIds.DrugAndAlcoholTesting, "DAT", true, true),
                new(AppointmentTypeIds.MedicalCheckUp, "MED", true, true),
                new(AppointmentTypeIds.UniformFitting, "UNI", true, true),
            ],
            proposerAppointmentTypeId ?? ProposerType,
            createdByManagerUserId,
            headcount: 1);

    private sealed class UniqueZones : IEventWindowZones
    {
        public bool IsKnownZone(string timeZoneId) => true;

        public LocalTimeValidity ValidityOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            LocalTimeValidity.Unique;

        public DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
            new(date.ToDateTime(time), TimeSpan.Zero);

        public DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) =>
            DateOnly.FromDateTime(instant.UtcDateTime);

        public string AbbreviationOf(DateTimeOffset instant, string timeZoneId) => "GMT";
    }
}
