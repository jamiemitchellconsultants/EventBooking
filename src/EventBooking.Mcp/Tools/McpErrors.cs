using EventBooking.Application.Common;
using ModelContextProtocol;

namespace EventBooking.Mcp.Tools;

/// <summary>Turns application failures into code-bearing MCP errors.</summary>
internal static class McpErrors
{
    /// <summary>Returns the value, or throws carrying the application error's code and message.</summary>
    internal static T ValueOrThrow<T>(this Result<T> result) =>
        result.IsSuccess ? result.Value : throw ToMcpException(result.Error);

    /// <summary>Awaits the result, then returns its value or throws carrying the error.</summary>
    internal static async Task<T> ValueOrThrowAsync<T>(this Task<Result<T>> pending) =>
        (await pending).ValueOrThrow();

    /// <summary>Throws carrying the application error's code and message when failed.</summary>
    internal static void ThrowIfFailure(this Result result)
    {
        if (result.IsFailure) throw ToMcpException(result.Error);
    }

    private static McpException ToMcpException(Error error) =>
        new($"{error.Code}: {error.Message}");
}
