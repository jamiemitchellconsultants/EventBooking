using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.AttendeeGroups;
using EventBooking.Domain.Locations;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Time;

namespace EventBooking.TestSupport;

/// <summary>
/// Re-inserts the predecessor's fixed reference rows in code. The migration no longer seeds
/// them, but suites that address the well-known identifiers still need the rows to exist.
/// </summary>
public static class FixedReferenceData
{
    /// <summary>Adds the three fixed types, the transitional location and the five fixed groups.</summary>
    public static void Seed(EventBookingDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.AppointmentTypes.AddRange(AppointmentType.CreateFixedSet());
        context.Locations.Add(Location.Create(
            TransitionalLocation.Id,
            "TRANSITIONAL",
            "Transitional location",
            "Recorded against the transitional site until Phase 3.",
            TransitionalLocation.TimeZoneId,
            new NodaTimeEventWindowZones()));
        context.AttendeeGroups.AddRange(
            AttendeeGroup.Define(
                AttendeeGroupIds.CabinCrew, "CABIN_CREW", "Cabin Crew", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Pilots, "PILOTS", "Pilots", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.UniformFitting]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundOperationsAgent, "GROUND_OPERATIONS_AGENT",
                "Ground Operations Agent", true, [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.Engineering, "ENGINEERING", "Engineering", true,
                [AppointmentTypeIds.MedicalCheckUp]),
            AttendeeGroup.Define(
                AttendeeGroupIds.GroundTransportServices, "GROUND_TRANSPORT_SERVICES",
                "Ground Transport Services", true,
                [AppointmentTypeIds.DrugAndAlcoholTesting, AppointmentTypeIds.MedicalCheckUp, AppointmentTypeIds.UniformFitting]));
    }
}
