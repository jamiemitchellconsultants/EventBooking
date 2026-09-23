using EventBooking.Domain.Audit;

namespace EventBooking.Domain.Tests.Access;

public class StaffAccessAuditVocabularyTests
{
    [Fact]
    public void StaffAccessActionsAppendWithoutRenumberingExistingActions()
    {
        Assert.Equal(16, (int)AuditAction.EventImported);
        Assert.Equal(17, (int)AuditAction.StaffAccessChanged);
        Assert.Equal(18, (int)AuditAction.StaffAccessRemoved);
    }

    [Fact]
    public void StaffAccessProfileIsAnAuditedEntityType()
    {
        Assert.Equal("StaffAccessProfile", AuditEntityTypes.StaffAccessProfile);
        Assert.Contains(AuditEntityTypes.StaffAccessProfile, AuditEntityTypes.All);
    }
}
