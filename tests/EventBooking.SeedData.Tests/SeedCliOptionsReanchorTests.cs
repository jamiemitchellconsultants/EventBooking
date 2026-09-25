using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

public sealed class SeedCliOptionsReanchorTests
{
    private const string Connection = "Host=localhost;Database=eventbooking";

    [Fact]
    public void DateAfterReanchorIsCapturedAndIsNotAConnectionString()
    {
        var options = SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", "2026-10-01"], _ => null);

        Assert.True(options.Reanchor);
        Assert.Equal(new DateOnly(2026, 10, 1), options.ReanchorDate);
        Assert.Equal(Connection, options.ConnectionString);
    }

    [Fact]
    public void DateWorksWhenTheConnectionStringComesAfterIt()
    {
        var options = SeedCliOptions.Parse(
            ["--demo", "--reanchor", "2026-10-01", Connection], _ => null);

        Assert.Equal(new DateOnly(2026, 10, 1), options.ReanchorDate);
        Assert.Equal(Connection, options.ConnectionString);
    }

    [Fact]
    public void DateWorksWithTheConnectionStringTakenFromTheEnvironment()
    {
        var options = SeedCliOptions.Parse(
            ["--demo", "--reanchor", "2026-10-01"],
            key => key == "ConnectionStrings__EventBooking" ? Connection : null);

        Assert.Equal(new DateOnly(2026, 10, 1), options.ReanchorDate);
        Assert.Equal(Connection, options.ConnectionString);
    }

    [Fact]
    public void BareReanchorLeavesTheDateNull()
    {
        var options = SeedCliOptions.Parse([Connection, "--demo", "--reanchor"], _ => null);

        Assert.True(options.Reanchor);
        Assert.Null(options.ReanchorDate);
    }

    [Fact]
    public void NoReanchorLeavesTheDateNull()
    {
        var options = SeedCliOptions.Parse([Connection, "--demo"], _ => null);

        Assert.False(options.Reanchor);
        Assert.Null(options.ReanchorDate);
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01")]
    [InlineData("2026-00-10")]
    public void DateShapedButImpossibleDatesAreRejectedWithTheDocumentedMessage(string token)
    {
        var error = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", token], _ => null));

        Assert.Contains("--reanchor date must be yyyy-MM-dd", error.Message, StringComparison.Ordinal);
        Assert.Contains(token, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATokenOfTheWrongShapeIsAStrayPositionalNotADate()
    {
        var error = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [Connection, "--demo", "--reanchor", "25/09/2026"], _ => null));

        Assert.Contains("exactly one", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReanchorDateWithoutDemoIsRejected()
    {
        var error = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(
            [Connection, "--reanchor", "2026-10-01"], _ => null));

        Assert.Contains("--reanchor requires --demo", error.Message, StringComparison.Ordinal);
    }
}
