using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the appointment page's controls, data boundary, and recoverable conflicts.</summary>
public sealed class AppointmentsComponentTests : BunitContext
{
    private static readonly DateTimeOffset OperationalNow =
        new(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid FirstSlotId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondSlotId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ThirdSlotId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Verifies the page shows scoped rows and no candidate-administration surface.</summary>
    [Fact]
    public void CurrentSlotShowsOnlyLifecycleControls()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail("Expected")));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Uniform Fitting appointments", cut.Markup);
            Assert.Contains("Alex Morgan", cut.Markup);
            Assert.Contains("alex@example.com", cut.Markup);
            Assert.Contains("Check in", cut.Markup);
            Assert.DoesNotContain("Edit candidate", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Invite", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Requirements", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Capacity", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(cut.FindAll("select[name=appointment-type]"));
        });
    }

    /// <summary>Verifies destructive no-show waits for explicit accessible confirmation.</summary>
    [Fact]
    public async Task NoShowRequiresConfirmationBeforeCallingTheApi()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail("Expected")));
        handler.Enqueue(Ok(Update("NoShow", 2)));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("No-show", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=no-show]").Click());
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("alertdialog", cut.Find("[role=alertdialog]").GetAttribute("role"));

        await cut.InvokeAsync(() => cut.Find("button[data-confirm=yes]").Click());
        cut.WaitForAssertion(() => Assert.Contains("No-show recorded", cut.Markup));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("NoShow", handler.Requests[2].Body);
    }

    /// <summary>Verifies a stale-version conflict refreshes state without replaying the stale request.</summary>
    [Fact]
    public async Task StaleVersionConflictRefreshesTheSelectedSlotOnce()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail("CheckedIn")));
        handler.Enqueue(Problem(
            HttpStatusCode.Conflict,
            "This appointment changed. Refresh and try again.",
            "appointment_version_conflict"));
        handler.Enqueue(Ok(Detail("Completed")));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Complete", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=complete]").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("another staff member changed", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Completed", cut.Markup);
        });
        Assert.Equal(4, handler.Requests.Count);
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Put);
    }

    /// <summary>Verifies a failed stale-version refresh keeps the refresh error without claiming success.</summary>
    [Fact]
    public async Task StaleVersionRefreshFailureKeepsTheRefreshError()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail("CheckedIn")));
        handler.Enqueue(Problem(
            HttpStatusCode.Conflict,
            "This appointment changed. Refresh and try again.",
            "appointment_version_conflict"));
        handler.Enqueue(Problem(
            HttpStatusCode.NotFound,
            "No such appointment workspace slot.",
            "not_found"));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Complete", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=complete]").Click());

        cut.WaitForAssertion(() =>
            Assert.Contains("No such appointment workspace slot.", cut.Markup));
        Assert.DoesNotContain("row has been refreshed", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("another staff member changed", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, handler.Requests.Count);
    }

    /// <summary>Verifies timing and cancelled-parent conflicts keep their safe message without refreshing.</summary>
    [Theory]
    [InlineData(
        "Expected",
        "no-show",
        true,
        "No-show can only be recorded after the confirmed-slot window has ended.")]
    [InlineData(
        "CheckedIn",
        "complete",
        false,
        "Cancelled bookings and slots cannot be updated.")]
    public async Task BusinessRuleConflictKeepsItsMessageWithoutRefreshing(
        string initialStatus,
        string action,
        bool requiresConfirmation,
        string message)
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail(initialStatus)));
        handler.Enqueue(Problem(HttpStatusCode.Conflict, message));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll($"button[data-action={action}]")));

        await cut.InvokeAsync(() => cut.Find($"button[data-action={action}]").Click());
        if (requiresConfirmation)
        {
            await cut.InvokeAsync(() => cut.Find("button[data-confirm=yes]").Click());
        }

        cut.WaitForAssertion(() => Assert.Contains(message, cut.Markup));
        Assert.DoesNotContain("another staff member changed", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, handler.Requests.Count);
    }

    /// <summary>Verifies future slots explain unavailable check-in without enabling it.</summary>
    [Fact]
    public void FutureSlotDisablesCheckInWithExplanation()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(FutureSlotList()));
        handler.Enqueue(Ok(FutureDetail("Expected")));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Check-in opens on the confirmed-slot date.", cut.Markup);
            Assert.True(cut.Find("button[data-action=check-in]").HasAttribute("disabled"));
        });
    }

    /// <summary>Verifies each non-initial status offers exactly its permitted next actions.</summary>
    [Theory]
    [InlineData("CheckedIn", "Complete")]
    [InlineData("CheckedIn", "Correct to expected")]
    [InlineData("Completed", "Correct to checked in")]
    [InlineData("NoShow", "Correct to expected")]
    public void SettledRowsOfferOnlyTheirPermittedActions(string status, string expectedLabel)
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail(status)));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains(expectedLabel, cut.Markup));
    }

    /// <summary>Verifies an empty workspace explains that no slots are available.</summary>
    [Fact]
    public void EmptySlotListShowsNoSlotsMessage()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(new AppointmentWorkspaceSlotListDto
        {
            AppointmentTypeName = "Uniform Fitting",
            Slots = [],
        }));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
            Assert.Contains("No current or upcoming appointment slots.", cut.Markup));
    }

    /// <summary>Verifies a slot without scoped rows explains the empty selection.</summary>
    [Fact]
    public void EmptySelectedSlotShowsNoAppointmentsMessage()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(new AppointmentSlotDetailDto
        {
            AppointmentTypeName = "Uniform Fitting",
            ConfirmedSlotId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        }));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "No candidates require this appointment in the selected slot.", cut.Markup));
    }

    /// <summary>Verifies a forbidden workspace keeps its safe denial message.</summary>
    [Fact]
    public void ForbiddenSlotListShowsTheDenialMessage()
    {
        var handler = GivenClient();
        handler.Enqueue(Problem(HttpStatusCode.Forbidden, "You do not have permission to do that."));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
            Assert.Contains("You do not have permission to do that.", cut.Markup));
    }

    /// <summary>Verifies an unexpected failure keeps its safe retry message.</summary>
    [Fact]
    public void UnexpectedFailureShowsTheGenericMessage()
    {
        var handler = GivenClient();
        handler.Enqueue(Problem(
            HttpStatusCode.InternalServerError, "Something went wrong. Please try again."));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Something went wrong. Please try again.", cut.Markup));
    }

    /// <summary>Verifies changing slots immediately removes actions belonging to the old slot.</summary>
    [Fact]
    public async Task SlotSwitchHidesPreviousRowsWhileNewDetailLoads()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotListFor(FirstSlotId, SecondSlotId)));
        handler.Enqueue(Ok(DetailFor(FirstSlotId, "Alex Morgan")));
        var pendingDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));
        await cut.InvokeAsync(() => cut.Find("button[data-action=no-show]").Click());
        Assert.NotEmpty(cut.FindAll("[role=alertdialog]"));

        var switchTask = cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(SecondSlotId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(3, handler.Requests.Count));

        var markupWhileLoading = cut.Markup;
        var actionCountWhileLoading = cut.FindAll("button[data-action]").Count;
        var confirmationCountWhileLoading = cut.FindAll("[role=alertdialog]").Count;
        pendingDetail.SetResult(Ok(DetailFor(SecondSlotId, "Blair Scott")));
        await switchTask;

        Assert.DoesNotContain("Alex Morgan", markupWhileLoading);
        Assert.Contains("Loading appointments", markupWhileLoading);
        Assert.Equal(0, actionCountWhileLoading);
        Assert.Equal(0, confirmationCountWhileLoading);
        cut.WaitForAssertion(() => Assert.Contains("Blair Scott", cut.Markup));
    }

    /// <summary>Verifies a failed slot load cannot leave the previous slot actionable.</summary>
    [Fact]
    public async Task FailedSlotSwitchDoesNotRestorePreviousRows()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotListFor(FirstSlotId, SecondSlotId)));
        handler.Enqueue(Ok(DetailFor(FirstSlotId, "Alex Morgan")));
        handler.Enqueue(Problem(
            HttpStatusCode.InternalServerError,
            "The selected slot could not be loaded."));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(SecondSlotId.ToString()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("The selected slot could not be loaded.", cut.Markup);
            Assert.DoesNotContain("Alex Morgan", cut.Markup);
            Assert.Empty(cut.FindAll("button[data-action]"));
        });
    }

    /// <summary>Verifies an obsolete response cannot replace the latest selected slot detail.</summary>
    [Fact]
    public async Task RapidSlotSwitchIgnoresOutOfOrderResponses()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotListFor(FirstSlotId, SecondSlotId, ThirdSlotId)));
        handler.Enqueue(Ok(DetailFor(FirstSlotId, "Alex Morgan")));
        var secondDetail = handler.EnqueuePending();
        var thirdDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        var secondSwitch = cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(SecondSlotId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(3, handler.Requests.Count));

        var thirdSwitch = cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(ThirdSlotId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));

        thirdDetail.SetResult(Ok(DetailFor(ThirdSlotId, "Casey Patel")));
        await thirdSwitch;
        cut.WaitForAssertion(() => Assert.Contains("Casey Patel", cut.Markup));

        var renderCountBeforeObsoleteResponse = cut.RenderCount;
        secondDetail.SetResult(Ok(DetailFor(SecondSlotId, "Blair Scott")));
        await secondSwitch;
        cut.WaitForAssertion(() =>
            Assert.True(cut.RenderCount > renderCountBeforeObsoleteResponse));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Casey Patel", cut.Markup);
            Assert.DoesNotContain("Blair Scott", cut.Markup);
        });
    }

    /// <summary>Verifies a previous slot's status response cannot change the current slot UI.</summary>
    [Fact]
    public async Task StatusResponseFromPreviousSlotDoesNotMutateCurrentSlot()
    {
        var handler = GivenClient();
        var firstDetail = DetailFor(FirstSlotId, "Alex Morgan");
        handler.Enqueue(Ok(SlotListFor(FirstSlotId, SecondSlotId)));
        handler.Enqueue(Ok(firstDetail));
        var pendingUpdate = handler.EnqueuePending();
        var pendingDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=check-in]").Click());
        cut.WaitForAssertion(() => Assert.Equal(HttpMethod.Put, handler.Requests[2].Method));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(SecondSlotId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));
        pendingDetail.SetResult(Ok(DetailFor(SecondSlotId, "Blair Scott")));
        cut.WaitForAssertion(() => Assert.Contains("Blair Scott", cut.Markup));

        var renderCountBeforePreviousUpdate = cut.RenderCount;
        pendingUpdate.SetResult(Ok(Update(
            firstDetail.Appointments[0].BookingAppointmentId,
            "CheckedIn",
            2)));
        cut.WaitForAssertion(() =>
            Assert.True(cut.RenderCount > renderCountBeforePreviousUpdate));

        Assert.Contains("Blair Scott", cut.Markup);
        Assert.Contains("Expected 1", cut.Markup);
        Assert.Contains("Checked in 0", cut.Markup);
        Assert.DoesNotContain("Check-in recorded for Alex Morgan", cut.Markup);
    }

    /// <summary>Verifies an obsolete response cannot end a newer slot's loading state.</summary>
    [Fact]
    public async Task ObsoleteSlotResponseDoesNotEndLatestLoadingState()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotListFor(FirstSlotId, SecondSlotId, ThirdSlotId)));
        handler.Enqueue(Ok(DetailFor(FirstSlotId, "Alex Morgan")));
        var secondDetail = handler.EnqueuePending();
        var thirdDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(SecondSlotId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(3, handler.Requests.Count));
        await cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(ThirdSlotId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));

        var renderCountBeforeObsoleteResponse = cut.RenderCount;
        secondDetail.SetResult(Ok(DetailFor(SecondSlotId, "Blair Scott")));
        cut.WaitForAssertion(() =>
            Assert.True(cut.RenderCount > renderCountBeforeObsoleteResponse));

        Assert.Contains("Loading appointments", cut.Markup);
        Assert.DoesNotContain("Blair Scott", cut.Markup);
        Assert.Empty(cut.FindAll("button[data-action]"));

        thirdDetail.SetResult(Ok(DetailFor(ThirdSlotId, "Casey Patel")));
        cut.WaitForAssertion(() => Assert.Contains("Casey Patel", cut.Markup));
    }

    /// <summary>Verifies an obsolete conflict refresh cannot report success under a newer slot.</summary>
    [Fact]
    public async Task ConflictRefreshFromPreviousSlotDoesNotReportSuccessInCurrentSlot()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotListFor(FirstSlotId, SecondSlotId)));
        handler.Enqueue(Ok(Detail("CheckedIn")));
        handler.Enqueue(Problem(
            HttpStatusCode.Conflict,
            "This appointment changed. Refresh and try again.",
            "appointment_version_conflict"));
        var pendingConflictRefresh = handler.EnqueuePending();
        handler.Enqueue(Ok(DetailFor(SecondSlotId, "Blair Scott")));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Complete", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=complete]").Click());
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=confirmed-slot]").Change(SecondSlotId.ToString()));
        cut.WaitForAssertion(() => Assert.Contains("Blair Scott", cut.Markup));

        var renderCountBeforeObsoleteRefresh = cut.RenderCount;
        pendingConflictRefresh.SetResult(Ok(Detail("Completed")));
        cut.WaitForAssertion(() =>
            Assert.True(cut.RenderCount > renderCountBeforeObsoleteRefresh));

        Assert.Contains("Blair Scott", cut.Markup);
        Assert.DoesNotContain("row has been refreshed", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("another staff member changed", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private RoutedHandler GivenClient()
    {
        var handler = new RoutedHandler();
        Services.AddSingleton(new AppointmentsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
        Services.AddSingleton(new HeadOfficePageClock(
            "Europe/London", () => OperationalNow));
        return handler;
    }

    private static AppointmentWorkspaceSlotListDto SlotList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Slots =
        [
            new AppointmentSlotSummaryDto
            {
                ConfirmedSlotId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Date = new DateOnly(2026, 9, 7),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(13, 0),
                Counts = new AppointmentStatusCountsDto
                {
                    Expected = 1, CheckedIn = 0, Completed = 0, NoShow = 0,
                },
            },
        ],
    };

    private static AppointmentWorkspaceSlotListDto SlotListFor(params Guid[] slotIds) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Slots = slotIds.Select((slotId, index) => new AppointmentSlotSummaryDto
        {
            ConfirmedSlotId = slotId,
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9 + index, 0),
            EndTime = new TimeOnly(13 + index, 0),
            Counts = new AppointmentStatusCountsDto
            {
                Expected = 1,
                CheckedIn = 0,
                Completed = 0,
                NoShow = 0,
            },
        }).ToList(),
    };

    private static AppointmentSlotDetailDto DetailFor(Guid slotId, string candidateName) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        ConfirmedSlotId = slotId,
        Date = new DateOnly(2026, 9, 7),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.NewGuid(),
                CandidateName = candidateName,
                CandidateEmail = $"{candidateName.Replace(" ", ".").ToLowerInvariant()}@example.com",
                Status = "Expected",
                CheckedInAt = null,
                OutcomeAt = null,
                Version = 1,
            },
        ],
    };

    private static AppointmentSlotDetailDto Detail(string status) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        ConfirmedSlotId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Date = new DateOnly(2026, 9, 7),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                CandidateName = "Alex Morgan", CandidateEmail = "alex@example.com",
                Status = status, CheckedInAt = status == "Expected" ? null : DateTimeOffset.UtcNow,
                OutcomeAt = status == "Completed" ? DateTimeOffset.UtcNow : null, Version = 1,
            },
        ],
    };

    private static AppointmentWorkspaceSlotListDto FutureSlotList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Slots =
        [
            new AppointmentSlotSummaryDto
            {
                ConfirmedSlotId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Date = new DateOnly(2026, 9, 8),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(13, 0),
                Counts = new AppointmentStatusCountsDto
                {
                    Expected = 1, CheckedIn = 0, Completed = 0, NoShow = 0,
                },
            },
        ],
    };

    private static AppointmentSlotDetailDto FutureDetail(string status) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        ConfirmedSlotId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Date = new DateOnly(2026, 9, 8),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                CandidateName = "Alex Morgan", CandidateEmail = "alex@example.com",
                Status = status, CheckedInAt = null,
                OutcomeAt = null, Version = 1,
            },
        ],
    };

    private static BookingAppointmentUpdateDto Update(string status, long version) => new()
    {
        BookingAppointmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Status = status, CheckedInAt = null, OutcomeAt = DateTimeOffset.UtcNow, Version = version,
    };

    private static BookingAppointmentUpdateDto Update(
        Guid bookingAppointmentId,
        string status,
        long version) => new()
    {
        BookingAppointmentId = bookingAppointmentId,
        Status = status,
        CheckedInAt = DateTimeOffset.UtcNow,
        OutcomeAt = null,
        Version = version,
    };


    /// <summary>Verifies a successful download hands the interop helper the filename and body.</summary>
    [Fact]
    public async Task DownloadRosterSuccessInvokesInteropWithFilenameAndContent()
    {
        const string CsvBody = "Candidate Name,Candidate Email,Appointment Type,Status,Checked In At,Outcome At\n";
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail("Expected")));
        handler.Enqueue(Csv(CsvBody, "roster-uniform-fitting-2026-09-07-0900.csv"));
        var save = JSInterop.SetupVoid("saveTextFile", _ => true);
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button[data-testid=download-roster]")));

        await cut.InvokeAsync(() => cut.Find("button[data-testid=download-roster]").Click());

        cut.WaitForAssertion(() => Assert.Single(save.Invocations));
        var invocation = save.Invocations.Single();
        Assert.Equal("roster-uniform-fitting-2026-09-07-0900.csv", invocation.Arguments[0]);
        Assert.Equal(CsvBody, invocation.Arguments[1]);
        Assert.Contains(
            handler.Requests,
            request => request.Path.EndsWith("/roster", StringComparison.Ordinal));
    }

    /// <summary>Verifies the button is disabled while the roster request is still in flight.</summary>
    [Fact]
    public async Task DownloadRosterButtonIsDisabledWhileTheRequestIsInFlight()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail("Expected")));
        var pending = handler.EnqueuePending();
        var save = JSInterop.SetupVoid("saveTextFile", _ => true);
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button[data-testid=download-roster]")));

        // The disabled render happens before the request is awaited, so no further render follows
        // while it is in flight; the state is read directly rather than waited for.
        var click = cut.InvokeAsync(() => cut.Find("button[data-testid=download-roster]").Click());

        var disabledWhileInFlight =
            cut.Find("button[data-testid=download-roster]").HasAttribute("disabled");
        Assert.Contains(handler.Requests, request => request.Path.EndsWith("/roster", StringComparison.Ordinal));

        pending.SetResult(Csv("Candidate Name\n", "roster-uniform-fitting-2026-09-07-0900.csv"));
        await click;
        // The interop call only completes once the test releases it, so the button stays disabled
        // until then; releasing it is what lets the handler run to completion.
        save.SetVoidResult();

        Assert.True(disabledWhileInFlight);
        cut.WaitForAssertion(() =>
            Assert.False(cut.Find("button[data-testid=download-roster]").HasAttribute("disabled")));
    }

    /// <summary>Verifies a refused download reuses the page's existing error banner.</summary>
    [Fact]
    public async Task DownloadRosterFailureShowsTheExistingErrorState()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(SlotList()));
        handler.Enqueue(Ok(Detail("Expected")));
        handler.Enqueue(Problem(HttpStatusCode.Forbidden, "Denied.", "forbidden"));
        var save = JSInterop.SetupVoid("saveTextFile", _ => true);
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("button[data-testid=download-roster]")));

        await cut.InvokeAsync(() => cut.Find("button[data-testid=download-roster]").Click());

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "You do not have permission to do that.",
                cut.Find("p.banner.error[role=alert]").TextContent));
        Assert.Empty(save.Invocations);
        Assert.Single(cut.FindAll("p.banner.error[role=alert]"));
    }

    /// <summary>Verifies the download button only appears once a slot's roster is on screen.</summary>
    [Fact]
    public void DownloadRosterButtonIsAbsentBeforeASlotIsSelected()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(new AppointmentWorkspaceSlotListDto
        {
            AppointmentTypeName = "Uniform Fitting",
            Slots = [],
        }));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains("Uniform Fitting", cut.Markup));
        Assert.Empty(cut.FindAll("button[data-testid=download-roster]"));
    }

    private static HttpResponseMessage Csv(string body, string fileName)
    {
        var content = new StringContent(body, Encoding.UTF8, "text/csv");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = fileName,
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage Ok<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(body, options: CamelCase),
    };

    private static HttpResponseMessage Problem(
        HttpStatusCode status,
        string detail,
        string title = "conflict") => new(status)
    {
        Content = JsonContent.Create(new { title, detail, status = (int)status },
            options: CamelCase),
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();
        /// <summary>Gets the requests captured in sending order.</summary>
        public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = [];
        /// <summary>Queues one response for the next request.</summary>
        public void Enqueue(HttpResponseMessage response) =>
            _responses.Enqueue(_ => Task.FromResult(response));

        /// <summary>Queues a response controlled by the test and returns its completion source.</summary>
        public TaskCompletionSource<HttpResponseMessage> EnqueuePending()
        {
            var pending = new TaskCompletionSource<HttpResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _responses.Enqueue(cancellationToken => pending.Task.WaitAsync(cancellationToken));
            return pending;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return await _responses.Dequeue()(cancellationToken);
        }
    }
}
