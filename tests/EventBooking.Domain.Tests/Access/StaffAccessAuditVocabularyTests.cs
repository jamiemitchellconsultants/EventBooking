using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessAuditVocabularyTests
{
    [Fact]
    public void StaffAccessActionsAppendWithoutRenumberingExistingActions()
    {
        Assert.Equal(17, (int)AuditAction.StaffAccessChanged);
    }

    [Fact]
    public void StaffAccessProfileIsAnAuditedEntityType()
    {
        Assert.Equal("StaffAccessProfile", AuditEntityTypes.StaffAccessProfile);
        Assert.Contains(AuditEntityTypes.StaffAccessProfile, AuditEntityTypes.All);
    }
}
