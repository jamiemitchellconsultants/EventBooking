using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests.Contracts;

public sealed class ApiCallContractTests
{
    [Fact]
    public async Task Rfc9457TypeAndExtensionsSurviveParsing()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                """{"type":"capacity-below-bookings","title":"Capacity conflict","status":409,"detail":"Keep the value.","minimum":4,"current":{"totalHeadcount":6},"errors":[{"field":"totalHeadcount","code":"too-low","message":"Minimum is 4."}]}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var result = await ApiCall.ReadAsync<object>(response, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("capacity-below-bookings", result.ErrorCode);
        Assert.Equal(4, result.Problem!.Minimum);
        Assert.Equal("too-low", Assert.Single(result.Problem.Errors).Code);
        Assert.Equal(6, result.Problem.Current!.Value.GetProperty("totalHeadcount").GetInt32());
    }

    [Fact]
    public async Task ForbiddenIsNotRewrittenAsUnauthenticated()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                """{"type":"forbidden","title":"Forbidden","status":403,"detail":"Scope is required."}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

        var result = await ApiCall.ReadAsync<object>(response, CancellationToken.None);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public void SubmissionKeyIsStableForOneAttemptAndDifferentForTheNext()
    {
        var first = IdempotencySubmission.Start();
        var retryKey = first.Key;
        var second = IdempotencySubmission.Start();

        Assert.Equal(retryKey, first.Key);
        Assert.NotEqual(first.Key, second.Key);
    }

    [Fact]
    public void PendingSubmissionReusesItsKeyUntilThePayloadChangesOrItCompletes()
    {
        var pending = new PendingSubmission();

        var first = pending.For(("a", 1));
        var retry = pending.For(("a", 1));
        var edited = pending.For(("a", 2));
        pending.Complete();
        var next = pending.For(("a", 2));

        Assert.Equal(first.Key, retry.Key);
        Assert.False(first.IsRetry);
        Assert.True(retry.IsRetry);
        Assert.NotEqual(first.Key, edited.Key);
        Assert.NotEqual(edited.Key, next.Key);
    }
}
