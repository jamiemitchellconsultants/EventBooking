using EventBooking.Application.Attendees;

namespace EventBooking.Application.Tests.Attendees;

public class AttendeeCsvParserTests
{
    private const string Header = "name,email,attendee_group";

    [Fact]
    public void AWellFormedFileParsesEveryRow()
    {
        var result = AttendeeCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,CABIN_CREW
             B. Chen,b.chen@mail.com,ENGINEERING
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        Assert.Equal(2, result.Rows[0].LineNumber);
        Assert.Equal("Amara Novak", result.Rows[0].Name);
        Assert.Equal("a.novak@mail.com", result.Rows[0].Email);
        Assert.Equal("CABIN_CREW", result.Rows[0].AttendeeGroupCode);

        Assert.Equal(3, result.Rows[1].LineNumber);
        Assert.Equal("ENGINEERING", result.Rows[1].AttendeeGroupCode);
    }

    [Fact]
    public void GroupCodesAreCaseInsensitiveAndWhitespaceIsTrimmed()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n  Amara Novak , a.novak@mail.com , pilots ");

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Amara Novak", row.Name);
        Assert.Equal("a.novak@mail.com", row.Email);
        Assert.Equal("PILOTS", row.AttendeeGroupCode);
    }

    [Fact]
    public void BlankLinesAreSkippedWithoutDisturbingLineNumbers()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n\nAmara Novak,a.novak@mail.com,PILOTS\n\n");

        Assert.Empty(result.Errors);
        Assert.Equal(3, Assert.Single(result.Rows).LineNumber);
    }

    [Fact]
    public void AnEmptyFileIsAnError()
    {
        var result = AttendeeCsvParser.Parse("");

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(0, error.LineNumber);
        Assert.Equal("The file is empty.", error.Message);
    }

    [Fact]
    public void TheWrongHeaderIsAnError()
    {
        var result = AttendeeCsvParser.Parse("name,email,types\nAmara Novak,a.novak@mail.com,PILOTS");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
        Assert.Equal(
            "The header line must read exactly: name,email,attendee_group",
            error.Message);
    }

    [Fact]
    public void TheOldRequirementHeaderIsAnError()
    {
        var result = AttendeeCsvParser.Parse(
            "name,email,appointment_types\nAmara Novak,a.novak@mail.com,DAT;UNI");

        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void AFileWithOnlyAHeaderIsAnError()
    {
        var result = AttendeeCsvParser.Parse(Header);

        var error = Assert.Single(result.Errors);
        Assert.Equal("The file contains no attendee rows.", error.Message);
    }

    [Theory]
    [InlineData("Amara Novak,a.novak@mail.com")]
    [InlineData("Amara Novak,a.novak@mail.com,PILOTS,extra")]
    public void TheWrongNumberOfFieldsIsARowError(string line)
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n{line}");

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("Expected 3 comma-separated fields: name, email, attendee_group.", error.Message);
    }

    [Fact]
    public void ABlankNameIsARowError()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\n ,a.novak@mail.com,PILOTS");

        Assert.Equal("Name is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ABlankEmailIsARowError()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\nAmara Novak, ,PILOTS");

        Assert.Equal("Email is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void NoAttendeeGroupIsARowError()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com, ");

        Assert.Equal("Employee group is required.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnknownCodePassesStructuralValidationForTheImportHandler()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\nAmara Novak,a.novak@mail.com,UNKNOWN_GROUP");

        Assert.Empty(result.Errors);
        Assert.Equal("UNKNOWN_GROUP", Assert.Single(result.Rows).AttendeeGroupCode);
    }

    [Fact]
    public void ARepeatedEmailInTheFileIsARowErrorOnTheSecondOccurrence()
    {
        var result = AttendeeCsvParser.Parse(
            $"""
             {Header}
             Amara Novak,a.novak@mail.com,PILOTS
             Someone Else,A.NOVAK@mail.com,ENGINEERING
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Equal("a.novak@mail.com appears more than once in this file.", error.Message);
    }

    [Fact]
    public void EveryBadRowIsReportedNotJustTheFirst()
    {
        var result = AttendeeCsvParser.Parse(
            $"""
             {Header}
             ,a.novak@mail.com,PILOTS
             B. Chen,b.chen@mail.com,
             C. Diallo,c.diallo@mail.com,ENGINEERING
             """);

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal([2, 3], result.Errors.Select(e => e.LineNumber));
        Assert.Single(result.Rows);
    }

    [Fact]
    public void WindowsLineEndingsAreHandled()
    {
        var result = AttendeeCsvParser.Parse($"{Header}\r\nAmara Novak,a.novak@mail.com,PILOTS\r\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
