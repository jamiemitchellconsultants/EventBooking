using EventBooking.SeedData;

namespace EventBooking.SeedData.Tests;

public sealed class SeedUsageTests
{
    [Theory]
    [InlineData("--demo")]
    [InlineData("--reanchor [yyyy-MM-dd]")]
    [InlineData("--reseed")]
    [InlineData("--load-fixture")]
    [InlineData("--verbose")]
    [InlineData("--help")]
    [InlineData("ConnectionStrings__EventBooking")]
    [InlineData("EVENTBOOKING_ALLOW_RESEED=true")]
    [InlineData("EVENTBOOKING_ENABLE_LOAD_FIXTURE=true")]
    [InlineData("EVENTBOOKING_LOAD_FIXTURE_PATH")]
    [InlineData("Keycloak__RealmExportPath")]
    [InlineData("Tokens__SigningKey")]
    [InlineData("Portal__BaseUrl")]
    public void UsageTextDocumentsEverySwitchAndItsEnvironment(string expected)
    {
        Assert.Contains(expected, SeedUsage.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageTextStatesThatMigrateOnlyIsTheDefault()
    {
        Assert.Contains("Without --demo", SeedUsage.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help", true)]
    [InlineData("-h", true)]
    [InlineData("--demo", false)]
    [InlineData("--helpful", false)]
    public void HelpRequestDetectionMatchesOnlyTheTwoSpellings(string argument, bool expected)
    {
        Assert.Equal(expected, SeedUsage.IsHelpRequest(["Host=x", argument]));
    }

    [Fact]
    public void FailureReportIsMessageOnlyWithoutVerbose()
    {
        var text = SeedFailureReport.Format(Thrown("boom"), ["Host=x", "--demo"]);

        Assert.Equal("Seed failed: boom", text);
    }

    [Fact]
    public void FailureReportIsTheFullExceptionWithVerbose()
    {
        var text = SeedFailureReport.Format(Thrown("boom"), ["Host=x", "--demo", "--verbose"]);

        Assert.StartsWith("Seed failed: System.InvalidOperationException: boom", text, StringComparison.Ordinal);
        Assert.Contains("   at ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseFailuresUnderVerboseAlsoReportTheFullException()
    {
        string[] args = ["Host=x", "--reanchor", "--verbose"];
        var failure = Assert.Throws<SeedException>(() => SeedCliOptions.Parse(args, _ => null));

        var text = SeedFailureReport.Format(failure, args);

        Assert.Contains("--reanchor requires --demo.", text, StringComparison.Ordinal);
        Assert.Contains("SeedException", text, StringComparison.Ordinal);
    }

    private static InvalidOperationException Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
