using EventBooking.Application.Common;
using ModelContextProtocol;

namespace EventBooking.Mcp.Tools;

/// <summary>Turns application results into MCP tool outcomes.</summary>
internal static class McpErrors
{
    /// <summary>Returns the value, or throws an MCP error carrying the failure message.</summary>
    /// <typeparam name="T">The success value type.</typeparam>
    /// <param name="result">The handler result.</param>
    /// <returns>The success value.</returns>
    internal static T ValueOrThrow<T>(this Result<T> result) =>
        result.IsSuccess
            ? result.Value
            : throw new McpException(result.Error.Message);

    /// <summary>Throws an MCP error when the result failed.</summary>
    /// <param name="result">The handler result.</param>
    internal static void ThrowIfFailure(this Result result)
    {
        if (result.IsFailure)
        {
            throw new McpException(result.Error.Message);
        }
    }
}
