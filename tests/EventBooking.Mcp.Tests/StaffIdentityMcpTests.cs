using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EventBooking.Mcp.Tests;

/// <summary>Covers identity recording at the authenticated MCP boundary.</summary>
[Collection("mcp")]
public sealed class StaffIdentityMcpTests(McpFactory factory)
{
    /// <summary>A valid first sighting is recorded before an unassigned caller is forbidden.</summary>
    [Fact]
    public async Task ValidFirstSighting_IsRecordedBeforeAuthorization()
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var originalRolesClaim = factory.RolesClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = "u234567";
            factory.RolesClaim = [];

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var identity = await factory.FindIdentityAsync(staffUserId);
            Assert.Equal("U234567", identity?.StaffId.Value);
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
            factory.RolesClaim = originalRolesClaim;
        }
    }

    /// <summary>An absent or malformed claim is never recorded.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-staff-id")]
    public async Task InvalidFirstSighting_IsNotRecorded(string? staffIdClaim)
    {
        var originalStaffUserId = factory.SignedInAs;
        var originalStaffIdClaim = factory.StaffIdClaim;
        var staffUserId = Guid.NewGuid();
        try
        {
            factory.SignedInAs = staffUserId;
            factory.StaffIdClaim = staffIdClaim;

            var response = await PostToolsListAsync();

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Null(await factory.FindIdentityAsync(staffUserId));
        }
        finally
        {
            factory.SignedInAs = originalStaffUserId;
            factory.StaffIdClaim = originalStaffIdClaim;
        }
    }

    private async Task<HttpResponseMessage> PostToolsListAsync()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = "1",
                    method = "tools/list",
                }),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await client.SendAsync(request);
    }
}
