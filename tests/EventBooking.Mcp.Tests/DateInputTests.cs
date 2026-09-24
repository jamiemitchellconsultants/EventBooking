using System.Text.Json;
using EventBooking.Domain.Access;
using EventBooking.Domain.AppointmentTypes;

namespace EventBooking.Mcp.Tests;

/// <summary>
/// The tools take the formats their descriptions name and REST binds — yyyy-MM-dd and HH:mm —
/// and nothing culture-dependent. "03/12/2026" is 12 March on one host and 3 December on
/// another; accepting it would let the same call propose an event on different days.
/// </summary>
[Collection("mcp")]
public sealed class DateInputTests(McpFactory factory)
{
    public static TheoryData<string, string> AmbiguousDates => new()
    {
        { "list_events", "from" },
        { "list_events", "to" },
        { "list_cancellable_events", "from" },
        { "list_cancellable_events", "to" },
    };

    [Theory]
    [MemberData(nameof(AmbiguousDates))]
    public async Task AListFilterDateOutsideTheIsoFormIsRefused(string tool, string field)
    {
        var client = await SignedInClientAsync();

        var envelope = await client.CallRawAsync(
            tool, new Dictionary<string, object> { ["limit"] = 1, [field] = "03/12/2026" });

        AssertValidationRefusal(envelope);
    }

    [Theory]
    [InlineData("03/12/2026", "09:30")]
    [InlineData("2026-12-03", "9:30 PM")]
    public async Task AProposalWindowOutsideTheIsoFormIsRefused(string date, string startTime)
    {
        var client = await SignedInClientAsync();

        var envelope = await client.CallRawAsync("propose_event", new
        {
            locationId = Guid.NewGuid(),
            date,
            startTime,
            durationMinutes = 60,
            appointmentTypeIds = new[] { AppointmentTypeIds.DrugAndAlcoholTesting },
            headcount = 1,
        });

        AssertValidationRefusal(envelope);
    }

    private async Task<McpClient> SignedInClientAsync()
    {
        factory.SignedInAs = await factory.GivenStaffAsync(
            [Role.Manager], AppointmentTypeIds.DrugAndAlcoholTesting);
        return new McpClient(factory.CreateClient());
    }

    /// <summary>
    /// Refused with the application's validation code, which REST renders as validation-failed:
    /// a bare message would be a refusal the other surface could not be compared with.
    /// </summary>
    private static void AssertValidationRefusal(JsonElement envelope)
    {
        string text;
        if (envelope.TryGetProperty("error", out var error))
        {
            text = error.GetProperty("message").GetString() ?? string.Empty;
        }
        else
        {
            var result = envelope.GetProperty("result");
            Assert.True(
                result.TryGetProperty("isError", out var isError) && isError.GetBoolean(),
                $"The call was accepted: {result}");
            text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        }

        var failure = System.Text.RegularExpressions.Regex.Replace(
            text, "^An error occurred invoking '[^']*': ", string.Empty);
        var separator = failure.IndexOf(": ", StringComparison.Ordinal);
        Assert.True(separator > 0, $"The refusal carried no application error code: {failure}");
        Assert.Equal(
            "validation-failed",
            EventBooking.Api.Endpoints.ProblemCatalogue.For(failure[..separator]).Type);
    }
}
