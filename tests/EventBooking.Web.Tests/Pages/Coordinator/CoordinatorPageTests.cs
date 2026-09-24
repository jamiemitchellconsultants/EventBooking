using Bunit;
using EventBooking.Web.Pages;
using Microsoft.AspNetCore.Components.Forms;
using EventBooking.Web.Services;
using EventBooking.Web.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Pages.Coordinator;

public sealed class CoordinatorPageTests : BunitContext
{
    [Fact]
    public void EmptyListStillOffersCreateActionsFromCollectionLinks()
    {
        Services.AddSingleton<IAttendeesClient>(new FakeAttendeesClient());
        Services.AddSingleton<IAuditClient>(new FakeAuditClient());
        var cut = Render<Attendees>();
        cut.WaitForAssertion(() => Assert.Contains("New attendee", cut.Markup));
        Assert.Single(cut.FindAll("[data-action='new-attendee']"));
        Assert.Single(cut.FindAll("[data-action='open-import']"));
    }

    [Fact]
    public void InviteDialogShowsLiveCountAndMinimum()
    {
        var attendeeId = Guid.Parse("50000000-0000-0000-0000-000000000005");
        var london = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var dublin = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var api = new FakeAttendeesClient();
        api.Rows.Add(new AttendeeDto(attendeeId, "T. Okafor", "t@example.org", "AwaitingAvailability", "Awaiting availability", "OFFICE", "AppointmentsOutstanding", ["IND"], "Failed", "cursor", Guid.NewGuid(), new Dictionary<string, ApiLink>
        {
            ["invite"] = new($"/api/attendees/{attendeeId}/invites", "POST", "inviteAttendee"),
        }));
        Services.AddSingleton<IAttendeesClient>(api);
        Services.AddSingleton<IAuditClient>(new FakeAuditClient());
        var cut = Render<Attendees>();
        cut.WaitForElement("[data-action='invite']").Click();
        cut.Find($"input[value='{london}']").Change(true);
        cut.Find($"input[value='{dublin}']").Change(true);

        cut.WaitForAssertion(() => Assert.Contains("only 2 eligible events", cut.Markup, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("need 3", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, api.LastLocations?.Count);
    }

    [Fact]
    public async Task RetryingAnInviteAfterALostResponseReusesTheIdempotencyKey()
    {
        var attendeeId = Guid.Parse("50000000-0000-0000-0000-000000000005");
        var london = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var api = new FakeAttendeesClient { InviteThrows = 1 };
        api.Rows.Add(new AttendeeDto(attendeeId, "T. Okafor", "t@example.org", "AwaitingAvailability", "Awaiting availability", "OFFICE", "AppointmentsOutstanding", ["IND"], "Failed", "cursor", Guid.NewGuid(), new Dictionary<string, ApiLink>
        {
            ["invite"] = new($"/api/attendees/{attendeeId}/invites", "POST", "inviteAttendee"),
        }));
        Services.AddSingleton<IAttendeesClient>(api);
        Services.AddSingleton<IAuditClient>(new FakeAuditClient());
        var cut = Render<Attendees>();
        cut.WaitForElement("[data-action='invite']").Click();
        cut.Find($"input[value='{london}']").Change(true);

        await cut.WaitForElement("[data-action='send-invite']").ClickAsync(new());
        await cut.WaitForElement("[data-action='send-invite']").ClickAsync(new());

        Assert.Equal(2, api.InviteKeys.Count);
        Assert.Equal(api.InviteKeys[0], api.InviteKeys[1]);
    }

    [Fact]
    public async Task FailedImportListsEveryLineWithoutImporting()
    {
        Services.AddSingleton<IAttendeesClient>(new FakeAttendeesClient
        {
            ImportResult = ApiOutcome<ImportOutcomeDto>.Success(new(false, 0, [
                new(3, "Email is invalid."),
                new(7, "Attendee group is unknown."),
            ])),
        });
        Services.AddSingleton<IAuditClient>(new FakeAuditClient());
        var cut = Render<Attendees>();
        cut.WaitForElement("[data-action='open-import']").Click();
        // InputFile exposes no DOM-triggerable change handler to bUnit, so the file
        // is offered through the component's own callback instead.
        var inputFile = cut.FindComponent<InputFile>();
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(
            InputFileChange("name,email,attendee_group\nBad,bad,UNKNOWN\nBad,bad,UNKNOWN")));

        cut.WaitForElement("[data-action='upload-csv']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Line 3", cut.Markup));
        Assert.Contains("Line 7", cut.Markup);
        Assert.Contains("Nothing was imported.", cut.Markup);
    }

    [Fact]
    public async Task AuditHistoryLoadsOnceAcrossRepeatedToggles()
    {
        var api = new FakeAuditClient();
        Services.AddSingleton<IAuditClient>(api);
        var cut = Render<AuditHistory>(parameters => parameters
            .Add(x => x.EntityKind, AuditEntityKind.Event)
            .Add(x => x.EntityId, Guid.NewGuid()));

        for (var attempt = 0; attempt < 3; attempt++)
            await cut.Find("button").ClickAsync(new());

        Assert.Equal("EventConfirmed", cut.Find("[data-role='audit-action']").TextContent.Trim());
        Assert.Equal(1, api.Reads);
    }

    [Fact]
    public async Task AuditHistoryErrorOffersRetry()
    {
        var api = new FakeAuditClient { Fail = true };
        Services.AddSingleton<IAuditClient>(api);
        var cut = Render<AuditHistory>(parameters => parameters
            .Add(x => x.EntityKind, AuditEntityKind.Attendee)
            .Add(x => x.EntityId, Guid.NewGuid()));

        await cut.Find("button").ClickAsync(new());

        Assert.Contains("Something went wrong", cut.Markup);
        Assert.Contains("Try again", cut.Markup);
    }

    private static InputFileChangeEventArgs InputFileChange(string text)
    {
        var content = new InputFileContent("attendees.csv", System.Text.Encoding.UTF8.GetBytes(text));
        return new InputFileChangeEventArgs([content]);
    }

    private sealed class InputFileContent(string name, byte[] bytes) : IBrowserFile
    {
        public string Name => name;
        public string ContentType => "text/csv";
        public long Size => bytes.Length;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken ct = default) => new MemoryStream(bytes);
    }

    private sealed class FakeAttendeesClient : IAttendeesClient
    {
        private static readonly Guid London = Guid.Parse("10000000-0000-0000-0000-000000000001");
        private static readonly Guid Dublin = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public List<AttendeeDto> Rows { get; } = [];
        public IReadOnlyList<Guid>? LastLocations { get; private set; }
        public ApiOutcome<ImportOutcomeDto> ImportResult { get; init; } =
            ApiOutcome<ImportOutcomeDto>.Success(new(true, 1, []));
        public Task<ApiOutcome<CoordinatorReferenceData>> GetReferenceDataAsync(CancellationToken ct) =>
            Task.FromResult(ApiOutcome<CoordinatorReferenceData>.Success(new(
                [new(London, "London HQ", "BST"), new(Dublin, "Dublin Centre", "IST")],
                [new(Guid.NewGuid(), "OFFICE", "Office staff", [])],
                [],
                new Dictionary<string, ApiLink>
                {
                    ["createAttendee"] = new("/api/attendees", "POST", "createAttendee"),
                    ["importAttendees"] = new("/api/attendees/import", "POST", "importAttendees"),
                })));
        public Task<ApiOutcome<PageDto<AttendeeDto>>> ListAsync(string? status, Guid? groupId, string? readiness, string? search, string? cursor, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PageDto<AttendeeDto>>.Success(new(Rows, null)));
        public Task<ApiOutcome<Guid>> CreateAsync(string name, string email, Guid? groupId, IdempotencySubmission submission, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<Guid>.Success(Guid.NewGuid()));
        public Task<ApiOutcome<bool>> UpdateAsync(Guid id, string name, string email, Guid? groupId, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<bool>.Success(true));
        public Task<ApiOutcome<bool>> DeleteAsync(Guid id, bool confirm, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<bool>.Success(true));
        public Task<ApiOutcome<ImportOutcomeDto>> ImportAsync(Stream csv, string fileName, IdempotencySubmission submission, CancellationToken ct) => Task.FromResult(ImportResult);
        public Task<ApiOutcome<EligibleEventCountDto>> CountEligibleAsync(Guid id, IReadOnlyList<Guid> locationIds, CancellationToken ct)
        {
            LastLocations = locationIds;
            return Task.FromResult(ApiOutcome<EligibleEventCountDto>.Success(new(locationIds.Count, 3)));
        }
        public List<string> InviteKeys { get; } = [];
        public int InviteThrows { get; set; }
        public Task<ApiOutcome<InviteOutcomeDto>> InviteAsync(Guid id, IReadOnlyList<Guid> locationIds, IdempotencySubmission submission, CancellationToken ct)
        {
            InviteKeys.Add(submission.Key);
            if (InviteThrows-- > 0) throw new HttpRequestException("response lost");
            return Task.FromResult(ApiOutcome<InviteOutcomeDto>.Success(new(Guid.NewGuid(), "Invited")));
        }
        public Task<ApiOutcome<EmailRetryDto>> RetryEmailAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<EmailRetryDto>.Success(new(Guid.NewGuid())));
        public Task<ApiOutcome<PageDto<AttendeeBookingDto>>> GetBookingsAsync(Guid attendeeId, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PageDto<AttendeeBookingDto>>.Success(new([], null)));
        public Task<ApiOutcome<CancelAttendeeBookingDto>> CancelBookingAsync(Guid attendeeId, Guid bookingId, bool confirm, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<CancelAttendeeBookingDto>.Success(new(false, 0, null, new Dictionary<string, ApiLink>())));
        public Task<ApiOutcome<RecoveryInviteOutcomeDto>> StartRecoveryAsync(Guid attendeeId, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<RecoveryInviteOutcomeDto>.Success(new(Guid.NewGuid(), [], [], new Dictionary<string, ApiLink>())));
        public Task<ApiOutcome<bool>> CancelRecoveryAsync(Guid attendeeId, Guid inviteId, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<bool>.Success(true));
        public Task<ApiOutcome<AttendeeReadinessDto>> GetReadinessAsync(Guid attendeeId, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<AttendeeReadinessDto>.Success(new(attendeeId, "Ready", "Ready to book", [], new Dictionary<string, ApiLink>())));
    }

    private sealed class FakeAuditClient : IAuditClient
    {
        public int Reads { get; private set; }
        public bool Fail { get; init; }
        public Task<ApiOutcome<PageDto<AuditRowDto>>> SearchAsync(AuditFilters filter, string? cursor, CancellationToken ct) =>
            Task.FromResult(ApiOutcome<PageDto<AuditRowDto>>.Success(new([], null)));
        public Task<ApiOutcome<PageDto<AuditRowDto>>> ForAttendeeAsync(Guid attendeeId, string? cursor, CancellationToken ct) => History();
        public Task<ApiOutcome<PageDto<AuditRowDto>>> ForEventAsync(Guid eventId, string? cursor, CancellationToken ct) => History();
        private Task<ApiOutcome<PageDto<AuditRowDto>>> History()
        {
            Reads++;
            return Task.FromResult(Fail
                ? ApiOutcome<PageDto<AuditRowDto>>.Failure("Something went wrong. Please try again.")
                : ApiOutcome<PageDto<AuditRowDto>>.Success(new([new(Guid.NewGuid(), DateTimeOffset.UtcNow, "Event", Guid.NewGuid(), "EventConfirmed", "Staff", null, null, null, new Dictionary<string, ApiLink>())], null)));
        }
    }
}
