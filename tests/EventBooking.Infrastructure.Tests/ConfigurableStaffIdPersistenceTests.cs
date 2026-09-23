using EventBooking.Domain.Access;
using EventBooking.Infrastructure.Persistence.Repositories;

namespace EventBooking.Infrastructure.Tests;

[Collection("postgres")]
public sealed class ConfigurableStaffIdPersistenceTests(PostgresFixture fixture)
{
    [Theory]
    [InlineData("A10023", "^[A-Z0-9]{1,32}$")]
    [InlineData("ORG-10023", "^ORG-[0-9]{5}$")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "^[A-Z0-9]{1,32}$")]
    public async Task Validated_staff_number_round_trips_without_the_retired_seven_character_constraint(string value, string pattern)
    {
        await fixture.ResetAsync();
        var id = Guid.NewGuid();
        var staffId = new StaffId(value, pattern);
        await using (var write = fixture.NewContext())
            await new StaffIdentityRepository(write).UpsertAsync(id, staffId, null,
                DateTimeOffset.Parse("2026-09-20T09:00:00Z"), CancellationToken.None);
        await using var read = fixture.NewContext();
        var identity = await new StaffIdentityRepository(read).GetByStaffIdAsync(staffId, CancellationToken.None);
        Assert.NotNull(identity);
        Assert.Equal(id, identity.StaffUserId);
        Assert.Equal(value, identity.StaffId.Value);
    }
}
