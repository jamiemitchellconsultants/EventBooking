using EventBooking.Application.Common;

namespace EventBooking.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void ASuccessCarriesNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void AFailureCarriesTheError()
    {
        var error = Error.Conflict("No remaining capacity.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("conflict", result.Error.Code);
        Assert.Equal("No remaining capacity.", result.Error.Message);
    }

    [Fact]
    public void AValueResultExposesItsValueOnSuccess()
    {
        var id = Guid.NewGuid();

        var result = Result<Guid>.Success(id);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value);
    }

    [Fact]
    public void ReadingTheValueOfAFailureThrows()
    {
        var result = Result<Guid>.Failure(Error.NotFound("No such candidate."));

        var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal("A failed result has no value.", ex.Message);
    }

    [Fact]
    public void TheErrorFactoriesUseTheAgreedCodes()
    {
        Assert.Equal("validation", Error.Validation("x").Code);
        Assert.Equal("not_found", Error.NotFound("x").Code);
        Assert.Equal("conflict", Error.Conflict("x").Code);
        Assert.Equal("forbidden", Error.Forbidden("x").Code);
        Assert.Equal(string.Empty, Error.None.Code);
    }
}
