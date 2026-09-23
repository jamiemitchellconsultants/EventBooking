using System.Net;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

public class ApiCallTests
{
    private sealed record Payload(string Name);

    [Fact]
    public async Task ASuccessfulResponseCarriesItsBody()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new Payload("Amara Novak")),
        };

        var outcome = await ApiCall.ReadAsync<Payload>(response, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.Equal("Amara Novak", outcome.Value!.Name);
        Assert.Equal(200, outcome.StatusCode);
        Assert.Null(outcome.ErrorMessage);
    }

    /// <summary>Verifies problem details preserve both their stable title and safe detail.</summary>
    [Fact]
    public async Task AProblemDetailsBodyPreservesItsCodeAndUserFacingMessage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"title":"conflict","detail":"An open proposal already exists for that window.","status":409}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await ApiCall.ReadAsync<Payload>(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("conflict", outcome.ErrorCode);
        Assert.Equal("An open proposal already exists for that window.", outcome.ErrorMessage);
        Assert.Equal(409, outcome.StatusCode);
    }

    [Fact]
    public async Task AFailureWithNoUsableBodyStillProducesAMessage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("<html>gateway blew up</html>", Encoding.UTF8, "text/html"),
        };

        var outcome = await ApiCall.ReadAsync<Payload>(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("Something went wrong. Please try again.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task AForbiddenResponseSaysSoInPlainWords()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("", Encoding.UTF8, "text/plain"),
        };

        var outcome = await ApiCall.ReadAsync<Payload>(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("You do not have permission to do that.", outcome.ErrorMessage);
    }

    [Fact]
    public async Task ANoContentResponseIsASuccess()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        var outcome = await ApiCall.ReadNoContentAsync(response, CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.Value);
    }

    [Fact]
    public async Task ANoContentCallThatFailedCarriesTheMessage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"title":"validation","detail":"headcount must be greater than zero.","status":400}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await ApiCall.ReadNoContentAsync(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("headcount must be greater than zero.", outcome.ErrorMessage);
    }
}
