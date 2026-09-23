using EventBooking.Application.Events;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Application.Tests.Events;

public class EventImportParserTests
{
    private const string Header = "date,startTime,DAT,MED,UNI";

    [Fact]
    public void AGoodFileParsesEveryRow()
    {
        var result = EventImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-11,13:00,4,4,4
             """);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Rows.Count);

        var first = result.Rows[0];
        Assert.Equal(2, first.LineNumber);
        Assert.Equal(new DateOnly(2026, 9, 10), first.Window.Date);
        Assert.Equal(new TimeOnly(9, 0), first.Window.StartTime);
        Assert.Equal(10, first.HeadcountsByAppointmentType[AppointmentTypeIds.DrugAndAlcoholTesting]);
        Assert.Equal(6, first.HeadcountsByAppointmentType[AppointmentTypeIds.MedicalCheckUp]);
        Assert.Equal(8, first.HeadcountsByAppointmentType[AppointmentTypeIds.UniformFitting]);
    }

    [Fact]
    public void BlankLinesAreTolerated()
    {
        var result = EventImportParser.Parse($"{Header}\n\n2026-09-10,09:00,10,6,8\n\n");

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void AnEmptyFileIsRejected()
    {
        var result = EventImportParser.Parse("");

        Assert.Empty(result.Rows);
        Assert.Equal("The file is empty.", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AWrongHeaderIsRejected()
    {
        var result = EventImportParser.Parse("date,startTime,types\n2026-09-10,09:00,DAT");

        Assert.Contains("header line must read exactly", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AFileOfOnlyAHeaderIsRejected()
    {
        var result = EventImportParser.Parse(Header);

        Assert.Equal("The file contains no event rows.", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("2026-09-10,09:00,10,6")]
    [InlineData("2026-09-10,09:00,10,6,8,1")]
    public void AWrongFieldCountIsRejected(string line)
    {
        var result = EventImportParser.Parse($"{Header}\n{line}");

        Assert.Contains("Expected 5 comma-separated fields", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableDateIsRejected()
    {
        var result = EventImportParser.Parse($"{Header}\n10-09-2026,09:00,10,6,8");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("date", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUnparseableStartTimeIsRejected()
    {
        var result = EventImportParser.Parse($"{Header}\n2026-09-10,9am,10,6,8");

        Assert.Contains("startTime", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("0,6,8")]
    [InlineData("-1,6,8")]
    [InlineData("abc,6,8")]
    [InlineData(",6,8")]
    public void ANonPositiveOrUnparseableHeadcountIsRejected(string counts)
    {
        var result = EventImportParser.Parse($"{Header}\n2026-09-10,09:00,{counts}");

        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
        Assert.Contains("DAT", result.Errors[0].Message);
    }

    [Fact]
    public void ADuplicateWindowWithinTheFileIsRejected()
    {
        var result = EventImportParser.Parse(
            $"""
             {Header}
             2026-09-10,09:00,10,6,8
             2026-09-10,09:00,1,1,1
             """);

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
        Assert.Contains("line 2", error.Message);
    }

    [Fact]
    public void MoreThanTwoHundredRowsIsRejectedWithoutRowErrors()
    {
        var rows = Enumerable.Range(0, 201)
            .Select(i => $"2026-01-{(i % 27) + 1:D2},{(i % 20) + 1:D2}:00,1,1,1");
        var result = EventImportParser.Parse($"{Header}\n{string.Join('\n', rows)}");

        Assert.Empty(result.Rows);
        Assert.Contains("at most 200 rows", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ALateStartTimeIsARowErrorNotAnException()
    {
        var result = EventImportParser.Parse(
            $"{Header}\n2026-09-10,21:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("4-hour window", error.Message);
    }

    [Fact]
    public void APastDatedRowIsRejected()
    {
        var result = EventImportParser.Parse(
            $"{Header}\n2026-09-01,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("future", error.Message);
    }

    [Fact]
    public void AFutureDatedRowParsesWhenTodayIsSupplied()
    {
        var result = EventImportParser.Parse(
            $"{Header}\n2026-09-10,09:00,10,6,8",
            new DateOnly(2026, 9, 3));

        Assert.Empty(result.Errors);
        Assert.Single(result.Rows);
    }
}
