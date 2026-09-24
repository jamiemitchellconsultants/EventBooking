using EventBooking.Application.Attendees;

namespace EventBooking.Api.Tests;

public sealed class RequiredAttendeeGroupContractTests
{
    [Fact]
    public void Readiness_and_list_contracts_have_no_reconciliation_state()
    {
        Assert.DoesNotContain("AttendeeGroupUnassigned", Enum.GetNames<AttendeeReadinessCode>());
        Assert.DoesNotContain(typeof(AttendeeListItem).GetProperties(),
            p => p.Name == "RequiresAttendeeGroupReconciliation");
        // List rows carry the group code, never another aggregate's identifier; the
        // readiness snapshot keeps the id it resolves journeys by.
        Assert.Equal(typeof(string), typeof(AttendeeListItem).GetProperty("GroupCode")!.PropertyType);
        Assert.Equal(typeof(Guid), typeof(AttendeeReadinessSnapshot).GetProperty("AttendeeGroupId")!.PropertyType);
    }
}
