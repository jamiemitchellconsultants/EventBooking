using System.Net;
using System.Net.Http.Json;
using System.Text;
using EventBooking.Domain.Invites;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.Api.Tests.Catalogue;

/// <summary>The thirteen attendee routes, including the two-step delete and the CSV import.</summary>
[Collection("api")]
public sealed class AttendeeEndpointTests(ApiFactory factory)
    : CatalogueSuite(factory)
{
    [Fact]
    public async Task AttendeesAreCreatedEditedAndListed()
    {
        var type = await GivenAppointmentTypeAsync("AC1");
        var group = await GivenAttendeeGroupAsync("ATT_CRUD", type);
        var client = await CoordinatorAsync("U700301");

        var created = await PostAsync(client, "/api/attendees", new
        {
            name = "Ada Lovelace", email = "ada-crud@example.com", attendeeGroupId = group,
        });
        var id = (await BodyAsync(created)).GetGuid();
        var edited = await client.PutAsJsonAsync($"/api/attendees/{id}", new
        {
            name = "Ada King", email = "ada-crud@example.com", attendeeGroupId = group,
        });
        var listed = await BodyAsync(
            await client.GetAsync($"/api/attendees?groupId={group}&limit=50"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        Assert.Single(listed.GetProperty("items").EnumerateArray());
    }

    /// <summary>The forbidden case: attendee data is a Coordinator capability, never Admin's.</summary>
    [Fact]
    public async Task AnAdminCannotReadAttendees()
    {
        var client = await AdminAsync("U700302");

        var response = await client.GetAsync("/api/attendees?limit=50");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeletionIsTwoStepAndTheFirstCallChangesNothing()
    {
        var type = await GivenAppointmentTypeAsync("AD1");
        var group = await GivenAttendeeGroupAsync("ATT_DEL", type);
        var attendee = await GivenAttendeeAsync(group, "ada-del@example.com");
        await GivenPendingInviteAsync(attendee, type);
        var client = await CoordinatorAsync("U700303");

        var first = await client.DeleteAsync($"/api/attendees/{attendee}");
        var stillThere = await ExistsAsync(attendee);
        var second = await client.DeleteAsync($"/api/attendees/{attendee}?confirm=true");
        var gone = !await ExistsAsync(attendee);

        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        Assert.Equal(
            "confirmation-required", (await BodyAsync(first)).GetProperty("type").GetString());
        Assert.True(stillThere);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.True(gone);
    }

    /// <summary>
    /// Design 05 says multipart, and the whole file is one transaction: a bad row anywhere
    /// means nothing is written, with the line number in the body (FR-4.3).
    /// </summary>
    [Fact]
    public async Task AnImportIsAllOrNothingAndNamesTheFailingLine()
    {
        var type = await GivenAppointmentTypeAsync("AS1");
        var group = await GivenAttendeeGroupAsync("ATT_CSV", type);
        var client = await CoordinatorAsync("U700304");
        var csv = string.Join('\n',
            "name,email,attendee_group",
            "Good Row,good@example.com,ATT_CSV",
            "Bad Row,not-an-email,ATT_CSV");

        var response = await ImportAsync(client, csv);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await BodyAsync(response);
        Assert.Equal("validation-failed", problem.GetProperty("type").GetString());
        Assert.Equal(3, problem.GetProperty("errors")[0].GetProperty("line").GetInt32());
        Assert.False(await AnyInGroupAsync(group));
    }

    [Fact]
    public async Task AnImportOverTheRowLimitIsRefused()
    {
        var type = await GivenAppointmentTypeAsync("AB1");
        await GivenAttendeeGroupAsync("ATT_BIG", type);
        var client = await CoordinatorAsync("U700305");
        var rows = new StringBuilder("name,email,attendee_group\n");
        for (var index = 0; index < 1001; index++)
        {
            rows.Append($"Row {index},row{index}@example.com,ATT_BIG\n");
        }

        var response = await ImportAsync(client, rows.ToString());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task TheEligibleCountAndTheInviteShareTheirLocationFilter()
    {
        var type = await GivenAppointmentTypeAsync("AI1");
        var group = await GivenAttendeeGroupAsync("ATT_INV", type);
        var attendee = await GivenAttendeeAsync(group, "ada-inv@example.com");
        var location = await GivenLocationAsync("ATT_INV_LOC");
        var client = await CoordinatorAsync("U700306");

        var counted = await BodyAsync(await client.GetAsync(
            $"/api/attendees/{attendee}/eligible-event-count?locationIds={location}"));
        var invited = await PostAsync(
            client, $"/api/attendees/{attendee}/invites", new { locationIds = new[] { location } });

        Assert.Equal(0, counted.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        Assert.Equal(
            "AwaitingAvailability", (await BodyAsync(invited)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheEligibleCountNamesTheRequiredOptionCount()
    {
        var type = await GivenAppointmentTypeAsync("AE1");
        var group = await GivenAttendeeGroupAsync("ATT_CNT", type);
        var attendee = await GivenAttendeeAsync(group, "ada-cnt@example.com");
        var location = await GivenLocationAsync("ATT_CNT_LOC");
        var admin = await AdminAsync("U700309");
        var required = (await BodyAsync(await admin.GetAsync("/api/settings")))
            .GetProperty("inviteOptionCount").GetInt32();
        var client = await CoordinatorAsync("U700310");

        var counted = await BodyAsync(await client.GetAsync(
            $"/api/attendees/{attendee}/eligible-event-count?locationIds={location}"));

        Assert.True(counted.TryGetProperty("count", out _));
        Assert.Equal(required, counted.GetProperty("requiredOptionCount").GetInt32());
    }

    /// <summary>Settlement #13: readiness is a dashboard read, not an attendee-management one.</summary>
    [Fact]
    public async Task ReadinessAnswersUnderTheDashboardCapability()
    {
        var type = await GivenAppointmentTypeAsync("AR1");
        var group = await GivenAttendeeGroupAsync("ATT_RDY", type);
        var attendee = await GivenAttendeeAsync(group, "ada-rdy@example.com");
        var client = await CoordinatorAsync("U700307");

        var allowed = await client.GetAsync($"/api/attendees/{attendee}/readiness");
        await AdminAsync("U700308");
        var refused = await client.GetAsync($"/api/attendees/{attendee}/readiness");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(
            "NoActiveBooking", (await BodyAsync(allowed)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    private static async Task<HttpResponseMessage> ImportAsync(HttpClient client, string csv)
    {
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "attendees.csv");
        return await client.PostAsync("/api/attendees/import", content);
    }

    private async Task<bool> ExistsAsync(Guid attendeeId)
    {
        await using var scoped = NewScope();
        return await scoped.Context.Attendees.AnyAsync(a => a.Id == attendeeId);
    }

    private async Task<bool> AnyInGroupAsync(Guid groupId)
    {
        await using var scoped = NewScope();
        return await scoped.Context.Attendees.AnyAsync(a => a.AttendeeGroupId == groupId);
    }

    /// <summary>
    /// Seeds one pending invite, which is what makes the delete a two-step: an attendee with
    /// nothing outstanding is deleted outright. The invite offers nothing because the delete
    /// never reads its options, only its pending state.
    /// </summary>
    private async Task GivenPendingInviteAsync(Guid attendeeId, Guid typeId)
    {
        var location = await GivenLocationAsync("ATT_DEL_LOC");
        await using var scoped = NewScope();
        scoped.Context.Invites.Add(Invite.CreateInitial(
            Guid.NewGuid(), attendeeId, Seeded.AddDays(7), [location], [], [typeId],
            retryCount: 0, inviteOptionCount: 0));
        await scoped.Context.SaveChangesAsync();
    }
}
