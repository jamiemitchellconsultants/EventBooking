using EventBooking.Application.Appointments;
using EventBooking.Application.Tests.Fakes;
using EventBooking.Domain.Bookings;

namespace EventBooking.Application.Tests.Appointments;

/// <summary>Verifies the roster CSV projection: columns, ordering, escaping, time, and filename.</summary>
public sealed class AppointmentRosterCsvFormatterTests
{
    private const string Header =
        "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At";

    private static AppointmentRosterCsvFormatter Formatter() => new(new FakeClock());

    private static string[] LinesOf(string csv) =>
        csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void FormatEmitsExactHeaderAndColumnOrder()
    {
        var result = Formatter().Format(EventWithRows());

        Assert.Equal(Header, LinesOf(result.CsvText)[0]);
    }

    [Fact]
    public void FormatPreservesRowOrderAndRepeatsAppointmentType()
    {
        var result = Formatter().Format(EventWithRows());

        var lines = LinesOf(result.CsvText);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Amina Yusuf,", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("Bruno Costa,", lines[2], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",Drug & Alcohol Testing,", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void FormatEmptyEventYieldsHeaderOnly()
    {
        var result = Formatter().Format(EventWith([]));

        Assert.Equal(Header + "\n", result.CsvText);
    }

    [Fact]
    public void FormatNullTimestampsRenderAsEmpty()
    {
        var result = Formatter().Format(EventWith([Row("Amina Yusuf", BookingAppointmentStatus.Expected)]));

        var dataLine = LinesOf(result.CsvText)[1];
        Assert.EndsWith(",,", dataLine, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNonNullTimestampsRenderAsTransitionalLocationIso8601()
    {
        var checkedInAt = new DateTimeOffset(2026, 9, 15, 9, 35, 0, TimeSpan.Zero);
        var outcomeAt = new DateTimeOffset(2026, 9, 15, 10, 5, 0, TimeSpan.Zero);
        var result = Formatter().Format(EventWith(
        [
            Row("Amina Yusuf", BookingAppointmentStatus.Completed, checkedInAt, outcomeAt),
        ]));

        var fields = LinesOf(result.CsvText)[1].Split(',');
        Assert.Equal(checkedInAt.ToString("o"), fields[4]);
        Assert.Equal(outcomeAt.ToString("o"), fields[5]);
    }

    [Fact]
    public void FormatUsesTheStatusEnumNameNotADisplayLabel()
    {
        var result = Formatter().Format(EventWith(
        [
            Row("Amina Yusuf", BookingAppointmentStatus.CheckedIn),
            Row("Bruno Costa", BookingAppointmentStatus.NoShow),
        ]));

        var lines = LinesOf(result.CsvText);
        Assert.Contains(",CheckedIn,", lines[1], StringComparison.Ordinal);
        Assert.Contains(",NoShow,", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNameWithCommaAndQuoteIsRfc4180Escaped()
    {
        var result = Formatter().Format(EventWith(
        [
            Row("Okafor, Ada \"Bisi\"", BookingAppointmentStatus.Expected),
        ]));

        Assert.Contains("\"Okafor, Ada \"\"Bisi\"\"\",", result.CsvText, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatNeverEmitsTheCommandTargetOrConcurrencyToken()
    {
        var detail = EventWithRows();

        var result = Formatter().Format(detail);

        foreach (var row in detail.Appointments)
        {
            Assert.DoesNotContain(row.BookingAppointmentId.ToString(), result.CsvText, StringComparison.Ordinal);
        }

        Assert.Equal(3, LinesOf(result.CsvText).Length);
        Assert.All(LinesOf(result.CsvText), line => Assert.Equal(5, line.Count(c => c == ',')));
    }

    [Fact]
    public void FormatBuildsSlugifiedFilename()
    {
        var result = Formatter().Format(EventWithRows());

        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0930.csv", result.FileName);
    }

    [Theory]
    [InlineData("=2+2")]
    [InlineData("+441234567")]
    [InlineData("-Robert")]
    [InlineData("@malicious")]
    [InlineData("\tindented")]
    [InlineData("\rreturn")]
    public void FormatNeutralisesFormulaTriggersWithASingleQuote(string name)
    {
        var result = Formatter().Format(EventWith(
        [
            Row(name, BookingAppointmentStatus.Expected),
        ]));

        var dataLine = LinesOf(result.CsvText)[1];
        Assert.Contains("'" + name, dataLine, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatFilenameKeepsAMidnightStartTimeFourDigits()
    {
        var result = Formatter().Format(EventWith([], startTime: new TimeOnly(0, 5)));

        Assert.Equal("roster-drug-&-alcohol-testing-2026-09-15-0005.csv", result.FileName);
    }

    private static BookingAppointmentRow Row(
        string attendeeName,
        BookingAppointmentStatus status,
        DateTimeOffset? checkedInAt = null,
        DateTimeOffset? outcomeAt = null) => new()
    {
        BookingAppointmentId = Guid.NewGuid(),
        AttendeeName = attendeeName,
        AttendeeEmail = $"{attendeeName.Split(' ')[0].ToLowerInvariant()}@mail.com",
        Status = status,
        CheckedInAt = checkedInAt,
        OutcomeAt = outcomeAt,
        Version = 1,
    };

    private static AppointmentEventDetail EventWith(
        IReadOnlyList<BookingAppointmentRow> rows,
        TimeOnly? startTime = null) => new()
    {
        AppointmentTypeName = "Drug & Alcohol Testing",
        EventId = Guid.NewGuid(),
        Date = new DateOnly(2026, 9, 15),
        StartTime = startTime ?? new TimeOnly(9, 30),
        EndTime = (startTime ?? new TimeOnly(9, 30)).AddHours(4),
        Appointments = rows,
    };

    private static AppointmentEventDetail EventWithRows() => EventWith(
    [
        Row("Amina Yusuf", BookingAppointmentStatus.Expected),
        Row("Bruno Costa", BookingAppointmentStatus.CheckedIn,
            new DateTimeOffset(2026, 9, 15, 9, 35, 0, TimeSpan.Zero)),
    ]);
}
