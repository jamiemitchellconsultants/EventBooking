using EventBooking.Api.Endpoints;
using EventBooking.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace EventBooking.Api.Tests;

public class ResultResponsesTests
{
    [Theory]
    [InlineData("validation", 422)]
    [InlineData("forbidden", 403)]
    [InlineData("not_found", 404)]
    [InlineData("conflict", 409)]
    [InlineData("appointment_version_conflict", 409)]
    [InlineData("already-confirmed", 409)]
    [InlineData("capacity-exhausted", 409)]
    [InlineData("confirmation-required", 409)]
    [InlineData("window-started", 409)]
    [InlineData("token-invalid", 404)]
    [InlineData("token-expired", 410)]
    [InlineData("proposal-not-open", 409)]
    public void EachErrorCodeMapsToItsStatus(string code, int expected)
    {
        Assert.Equal(expected, ProblemCatalogue.For(code).Status);
    }

    [Fact]
    public void AnUnmappedCodeThrowsRatherThanBecomingAServerError()
    {
        Assert.Throws<InvalidOperationException>(() => ProblemCatalogue.For("something-new"));
    }

    [Fact]
    public void ASuccessfulResultWithNoValueIsNoContent()
    {
        var response = Result.Success().ToResponse();

        Assert.Equal("NoContent", response.GetType().Name.Replace("Result", "NoContent"));
    }

    [Fact]
    public void AFailedResultCarriesItsMessage()
    {
        var response = Result.Failure(Error.Conflict("No remaining capacity.")).ToResponse();

        Assert.NotNull(response);
        Assert.Contains("ProblemHttpResult", response.GetType().Name);
    }

    [Fact]
    public async Task AFailedResultWritesProblemDetails()
    {
        var context = await Execute(Result.Failure(Error.Conflict("No remaining capacity.")).ToResponse());

        Assert.Equal(409, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        var problem = await ReadJson(context);
        Assert.Equal("conflict", problem.RootElement.GetProperty("type").GetString());
        Assert.Equal("That is not possible right now.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("No remaining capacity.", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(409, problem.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ASuccessfulGenericResultWritesItsValueWithOk()
    {
        var context = await Execute(Result<string>.Success("confirmed").ToResponse());

        Assert.Equal(200, context.Response.StatusCode);
        var body = await ReadBody(context);
        Assert.Equal("\"confirmed\"", body);
    }

    [Fact]
    public async Task ASuccessfulCreatedResultWritesLocationAndValue()
    {
        var context = await Execute(Result<string>.Success("confirmed").ToCreated(value => $"/bookings/{value}"));

        Assert.Equal(201, context.Response.StatusCode);
        Assert.Equal("/bookings/confirmed", context.Response.Headers.Location.ToString());
        var body = await ReadBody(context);
        Assert.Equal("\"confirmed\"", body);
    }

    [Fact]
    public async Task FailedGenericAndCreatedResultsWriteProblemDetailsWithoutReadingValue()
    {
        var failed = Result<string>.Failure(Error.NotFound("Booking was not found."));

        var genericContext = await Execute(failed.ToResponse());
        var createdContext = await Execute(failed.ToCreated(_ => throw new InvalidOperationException("location must not be evaluated")));

        Assert.Equal(404, genericContext.Response.StatusCode);
        Assert.Equal(404, createdContext.Response.StatusCode);
        Assert.Equal("Booking was not found.", (await ReadJson(genericContext)).RootElement.GetProperty("detail").GetString());
        Assert.Equal("Booking was not found.", (await ReadJson(createdContext)).RootElement.GetProperty("detail").GetString());
    }

    private static async Task<DefaultHttpContext> Execute(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection().AddOptions().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        return context;
    }

    private static async Task<string> ReadBody(DefaultHttpContext context)
    {
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static async Task<JsonDocument> ReadJson(DefaultHttpContext context) =>
        await JsonDocument.ParseAsync(context.Response.Body);
}
