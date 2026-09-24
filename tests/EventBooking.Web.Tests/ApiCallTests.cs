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

    [Fact]
    public async Task ASuccessWithANullBodyIsAFailure()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        };

        var outcome = await ApiCall.ReadAsync<Payload>(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("unexpected", outcome.ErrorCode);
        Assert.Equal("Something went wrong. Please try again.", outcome.ErrorMessage);
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
                """{"type":"validation-failed","title":"Validation failed","detail":"headcount must be greater than zero.","status":400}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var outcome = await ApiCall.ReadNoContentAsync(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("validation-failed", outcome.ErrorCode);
        Assert.Equal("headcount must be greater than zero.", outcome.ErrorMessage);
    }

    [Theory]
    [InlineData("\"Bad gateway\"")]
    [InlineData("[1,2]")]
    [InlineData("""{"type":42}""")]
    [InlineData("""{"type":"validation-failed","errors":{"name":["Required"]}}""")]
    [InlineData("""{"type":"validation-failed","errors":[{"line":99999999999}]}""")]
    public async Task AnUnexpectedlyShapedProblemBodyIsAGenericFailureNotAnException(string body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        var outcome = await ApiCall.ReadAsync<Payload>(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal(502, outcome.StatusCode);
        Assert.NotNull(outcome.ErrorCode);
    }

    [Fact]
    public async Task AMalformedSuccessBodyIsAGenericFailureNotAnException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not json", Encoding.UTF8, "application/json"),
        };

        var outcome = await ApiCall.ReadAsync<Payload>(response, CancellationToken.None);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("unexpected", outcome.ErrorCode);
        Assert.Equal(200, outcome.StatusCode);
    }
}
