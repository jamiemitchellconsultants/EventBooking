# 00b — Vocabulary edits 108 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## after — tests/EventBooking.Web.Tests/AppointmentsComponentTests.cs — 1/1

<!-- vocabulary-file: {"id":369,"oldPath":"tests/EventBooking.Web.Tests/AppointmentsComponentTests.cs","newPath":"tests/EventBooking.Web.Tests/AppointmentsComponentTests.cs","beforeSha":"c910f6f8af620d5badd1a9f39dd0dead6591a2c6a47e2cace4871b2f2cff10dc","afterSha":"6eaff158227dd12ec57a9bb4507aca5cd73c9e00d6e332903a4b981e72070ffd","side":"after","part":1,"parts":1} -->

`````csharp
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
    private static readonly Guid FirstEventId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondEventId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ThirdEventId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Verifies the page shows scoped rows and no attendee-administration surface.</summary>
    [Fact]
    public void CurrentEventShowsOnlyLifecycleControls()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventList()));
        handler.Enqueue(Ok(Detail("Expected")));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Uniform Fitting appointments", cut.Markup);
            Assert.Contains("Alex Morgan", cut.Markup);
            Assert.Contains("alex@example.com", cut.Markup);
            Assert.Contains("Check in", cut.Markup);
            Assert.DoesNotContain("Edit attendee", cut.Markup, StringComparison.OrdinalIgnoreCase);
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
        handler.Enqueue(Ok(EventList()));
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
    public async Task StaleVersionConflictRefreshesTheSelectedEventOnce()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventList()));
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
        handler.Enqueue(Ok(EventList()));
        handler.Enqueue(Ok(Detail("CheckedIn")));
        handler.Enqueue(Problem(
            HttpStatusCode.Conflict,
            "This appointment changed. Refresh and try again.",
            "appointment_version_conflict"));
        handler.Enqueue(Problem(
            HttpStatusCode.NotFound,
            "No such appointment workspace eventItem.",
            "not_found"));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Complete", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=complete]").Click());

        cut.WaitForAssertion(() =>
            Assert.Contains("No such appointment workspace eventItem.", cut.Markup));
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
        "No-show can only be recorded after the event window has ended.")]
    [InlineData(
        "CheckedIn",
        "complete",
        false,
        "Cancelled bookings and events cannot be updated.")]
    public async Task BusinessRuleConflictKeepsItsMessageWithoutRefreshing(
        string initialStatus,
        string action,
        bool requiresConfirmation,
        string message)
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventList()));
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

    /// <summary>Verifies future events explain unavailable check-in without enabling it.</summary>
    [Fact]
    public void FutureEventDisablesCheckInWithExplanation()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(FutureEventList()));
        handler.Enqueue(Ok(FutureDetail("Expected")));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Check-in opens on the event date.", cut.Markup);
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
        handler.Enqueue(Ok(EventList()));
        handler.Enqueue(Ok(Detail(status)));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains(expectedLabel, cut.Markup));
    }

    /// <summary>Verifies an empty workspace explains that no events are available.</summary>
    [Fact]
    public void EmptyEventListShowsNoEventsMessage()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(new AppointmentWorkspaceEventListDto
        {
            AppointmentTypeName = "Uniform Fitting",
            Events = [],
        }));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
            Assert.Contains("No current or upcoming appointment events.", cut.Markup));
    }

    /// <summary>Verifies a event without scoped rows explains the empty selection.</summary>
    [Fact]
    public void EmptySelectedEventShowsNoAppointmentsMessage()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventList()));
        handler.Enqueue(Ok(new AppointmentEventDetailDto
        {
            AppointmentTypeName = "Uniform Fitting",
            EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Date = new DateOnly(2026, 9, 7),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(13, 0),
            Appointments = [],
        }));

        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
            Assert.Contains(
                "No attendees require this appointment in the selected eventItem.", cut.Markup));
    }

    /// <summary>Verifies a forbidden workspace keeps its safe denial message.</summary>
    [Fact]
    public void ForbiddenEventListShowsTheDenialMessage()
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

    /// <summary>Verifies changing events immediately removes actions belonging to the old eventItem.</summary>
    [Fact]
    public async Task EventSwitchHidesPreviousRowsWhileNewDetailLoads()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventListFor(FirstEventId, SecondEventId)));
        handler.Enqueue(Ok(DetailFor(FirstEventId, "Alex Morgan")));
        var pendingDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));
        await cut.InvokeAsync(() => cut.Find("button[data-action=no-show]").Click());
        Assert.NotEmpty(cut.FindAll("[role=alertdialog]"));

        var switchTask = cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(SecondEventId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(3, handler.Requests.Count));

        var markupWhileLoading = cut.Markup;
        var actionCountWhileLoading = cut.FindAll("button[data-action]").Count;
        var confirmationCountWhileLoading = cut.FindAll("[role=alertdialog]").Count;
        pendingDetail.SetResult(Ok(DetailFor(SecondEventId, "Blair Scott")));
        await switchTask;

        Assert.DoesNotContain("Alex Morgan", markupWhileLoading);
        Assert.Contains("Loading appointments", markupWhileLoading);
        Assert.Equal(0, actionCountWhileLoading);
        Assert.Equal(0, confirmationCountWhileLoading);
        cut.WaitForAssertion(() => Assert.Contains("Blair Scott", cut.Markup));
    }

    /// <summary>Verifies a failed event load cannot leave the previous event actionable.</summary>
    [Fact]
    public async Task FailedEventSwitchDoesNotRestorePreviousRows()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventListFor(FirstEventId, SecondEventId)));
        handler.Enqueue(Ok(DetailFor(FirstEventId, "Alex Morgan")));
        handler.Enqueue(Problem(
            HttpStatusCode.InternalServerError,
            "The selected event could not be loaded."));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(SecondEventId.ToString()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("The selected event could not be loaded.", cut.Markup);
            Assert.DoesNotContain("Alex Morgan", cut.Markup);
            Assert.Empty(cut.FindAll("button[data-action]"));
        });
    }

    /// <summary>Verifies an obsolete response cannot replace the latest selected event detail.</summary>
    [Fact]
    public async Task RapidEventSwitchIgnoresOutOfOrderResponses()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventListFor(FirstEventId, SecondEventId, ThirdEventId)));
        handler.Enqueue(Ok(DetailFor(FirstEventId, "Alex Morgan")));
        var secondDetail = handler.EnqueuePending();
        var thirdDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        var secondSwitch = cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(SecondEventId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(3, handler.Requests.Count));

        var thirdSwitch = cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(ThirdEventId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));

        thirdDetail.SetResult(Ok(DetailFor(ThirdEventId, "Casey Patel")));
        await thirdSwitch;
        cut.WaitForAssertion(() => Assert.Contains("Casey Patel", cut.Markup));

        var renderCountBeforeObsoleteResponse = cut.RenderCount;
        secondDetail.SetResult(Ok(DetailFor(SecondEventId, "Blair Scott")));
        await secondSwitch;
        cut.WaitForAssertion(() =>
            Assert.True(cut.RenderCount > renderCountBeforeObsoleteResponse));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Casey Patel", cut.Markup);
            Assert.DoesNotContain("Blair Scott", cut.Markup);
        });
    }

    /// <summary>Verifies a previous event's status response cannot change the current event UI.</summary>
    [Fact]
    public async Task StatusResponseFromPreviousEventDoesNotMutateCurrentEvent()
    {
        var handler = GivenClient();
        var firstDetail = DetailFor(FirstEventId, "Alex Morgan");
        handler.Enqueue(Ok(EventListFor(FirstEventId, SecondEventId)));
        handler.Enqueue(Ok(firstDetail));
        var pendingUpdate = handler.EnqueuePending();
        var pendingDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=check-in]").Click());
        cut.WaitForAssertion(() => Assert.Equal(HttpMethod.Put, handler.Requests[2].Method));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(SecondEventId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));
        pendingDetail.SetResult(Ok(DetailFor(SecondEventId, "Blair Scott")));
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

    /// <summary>Verifies an obsolete response cannot end a newer event's loading state.</summary>
    [Fact]
    public async Task ObsoleteEventResponseDoesNotEndLatestLoadingState()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventListFor(FirstEventId, SecondEventId, ThirdEventId)));
        handler.Enqueue(Ok(DetailFor(FirstEventId, "Alex Morgan")));
        var secondDetail = handler.EnqueuePending();
        var thirdDetail = handler.EnqueuePending();
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(SecondEventId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(3, handler.Requests.Count));
        await cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(ThirdEventId.ToString()));
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));

        var renderCountBeforeObsoleteResponse = cut.RenderCount;
        secondDetail.SetResult(Ok(DetailFor(SecondEventId, "Blair Scott")));
        cut.WaitForAssertion(() =>
            Assert.True(cut.RenderCount > renderCountBeforeObsoleteResponse));

        Assert.Contains("Loading appointments", cut.Markup);
        Assert.DoesNotContain("Blair Scott", cut.Markup);
        Assert.Empty(cut.FindAll("button[data-action]"));

        thirdDetail.SetResult(Ok(DetailFor(ThirdEventId, "Casey Patel")));
        cut.WaitForAssertion(() => Assert.Contains("Casey Patel", cut.Markup));
    }

    /// <summary>Verifies an obsolete conflict refresh cannot report success under a newer eventItem.</summary>
    [Fact]
    public async Task ConflictRefreshFromPreviousEventDoesNotReportSuccessInCurrentEvent()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(EventListFor(FirstEventId, SecondEventId)));
        handler.Enqueue(Ok(Detail("CheckedIn")));
        handler.Enqueue(Problem(
            HttpStatusCode.Conflict,
            "This appointment changed. Refresh and try again.",
            "appointment_version_conflict"));
        var pendingConflictRefresh = handler.EnqueuePending();
        handler.Enqueue(Ok(DetailFor(SecondEventId, "Blair Scott")));
        var cut = Render<Appointments>();
        cut.WaitForAssertion(() => Assert.Contains("Complete", cut.Markup));

        await cut.InvokeAsync(() => cut.Find("button[data-action=complete]").Click());
        cut.WaitForAssertion(() => Assert.Equal(4, handler.Requests.Count));

        await cut.InvokeAsync(() =>
            cut.Find("select[name=event]").Change(SecondEventId.ToString()));
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
        Services.AddSingleton(new TransitionalLocationPageClock(
            "Europe/London", () => OperationalNow));
        return handler;
    }

    private static AppointmentWorkspaceEventListDto EventList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Events =
        [
            new AppointmentEventSummaryDto
            {
                EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
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

    private static AppointmentWorkspaceEventListDto EventListFor(params Guid[] eventIds) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Events = eventIds.Select((eventId, index) => new AppointmentEventSummaryDto
        {
            EventId = eventId,
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

    private static AppointmentEventDetailDto DetailFor(Guid eventId, string attendeeName) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        EventId = eventId,
        Date = new DateOnly(2026, 9, 7),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.NewGuid(),
                AttendeeName = attendeeName,
                AttendeeEmail = $"{attendeeName.Replace(" ", ".").ToLowerInvariant()}@example.com",
                Status = "Expected",
                CheckedInAt = null,
                OutcomeAt = null,
                Version = 1,
            },
        ],
    };

    private static AppointmentEventDetailDto Detail(string status) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Date = new DateOnly(2026, 9, 7),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                AttendeeName = "Alex Morgan", AttendeeEmail = "alex@example.com",
                Status = status, CheckedInAt = status == "Expected" ? null : DateTimeOffset.UtcNow,
                OutcomeAt = status == "Completed" ? DateTimeOffset.UtcNow : null, Version = 1,
            },
        ],
    };

    private static AppointmentWorkspaceEventListDto FutureEventList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Events =
        [
            new AppointmentEventSummaryDto
            {
                EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
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

    private static AppointmentEventDetailDto FutureDetail(string status) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        EventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Date = new DateOnly(2026, 9, 8),
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                AttendeeName = "Alex Morgan", AttendeeEmail = "alex@example.com",
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
        const string CsvBody = "Attendee Name,Attendee Email,Appointment Type,Status,Checked In At,Outcome At\n";
        var handler = GivenClient();
        handler.Enqueue(Ok(EventList()));
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
        handler.Enqueue(Ok(EventList()));
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

        pending.SetResult(Csv("Attendee Name\n", "roster-uniform-fitting-2026-09-07-0900.csv"));
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
        handler.Enqueue(Ok(EventList()));
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

    /// <summary>Verifies the download button only appears once a event's roster is on screen.</summary>
    [Fact]
    public void DownloadRosterButtonIsAbsentBeforeAEventIsSelected()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(new AppointmentWorkspaceEventListDto
        {
            AppointmentTypeName = "Uniform Fitting",
            Events = [],
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
`````

## before — tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs — 1/1

<!-- vocabulary-file: {"id":370,"oldPath":"tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs","newPath":"tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs","beforeSha":"acd706eb30ca5a8bc3265d2a5f8fd655eaf90b4a535fa97ec4f95b0ecf254e71","afterSha":"8251bd2c9110c3b5f09709e9181e4e309a029ebcb21785916802fb17736d8c9c","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the appointment page groups recently past slots without changing row behavior.</summary>
public sealed class AppointmentsRecentPastTests : BunitContext
{
    private static readonly DateTimeOffset OperationalNow =
        new(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid PastSlotId =
        Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid CurrentSlotId =
        Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Verifies the selector separates recently past slots from current ones.</summary>
    [Fact]
    public void SlotSelectorGroupsRecentPastSeparately()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(MixedSlotList()));
        handler.Enqueue(Ok(CurrentDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            var groups = cut.FindAll("select[name=confirmed-slot] optgroup");
            Assert.Equal(2, groups.Count);
            Assert.Equal("Recent past", groups[0].GetAttribute("label"));
            Assert.Equal("Current and upcoming", groups[1].GetAttribute("label"));
            Assert.Contains("2026-09-06", groups[0].TextContent);
            Assert.Contains("2026-09-07", groups[1].TextContent);
        });
    }

    /// <summary>Verifies the page opens on the nearest current slot when past slots come first.</summary>
    [Fact]
    public void DefaultsToNearestCurrentSlot()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(MixedSlotList()));
        handler.Enqueue(Ok(CurrentDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith(
            $"/api/appointment-workspace/slots/{CurrentSlotId}",
            handler.Requests[1].Path,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Monday, 07 Sep 2026", cut.Markup);
    }

    /// <summary>Verifies the page falls back to the most recent past slot when nothing is current.</summary>
    [Fact]
    public void FallsBackToMostRecentPastSlot()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(PastOnlySlotList()));
        handler.Enqueue(Ok(PastDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains("Past Candidate", cut.Markup));
        Assert.Contains("Sunday, 06 Sep 2026", cut.Markup);
    }

    /// <summary>Verifies a past Expected row offers no-show but no check-in.</summary>
    [Fact]
    public void PastSlotDisablesCheckInAndEnablesNoShow()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(PastOnlySlotList()));
        handler.Enqueue(Ok(PastDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("button[data-action=check-in]").HasAttribute("disabled"));
            Assert.False(cut.Find("button[data-action=no-show]").HasAttribute("disabled"));
            Assert.Contains("Check-in opens on the confirmed-slot date.", cut.Markup);
        });
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

    private static AppointmentWorkspaceSlotListDto MixedSlotList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Slots =
        [
            Summary(PastSlotId, new DateOnly(2026, 9, 6)),
            Summary(CurrentSlotId, new DateOnly(2026, 9, 7)),
        ],
    };

    private static AppointmentWorkspaceSlotListDto PastOnlySlotList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Slots = [Summary(PastSlotId, new DateOnly(2026, 9, 6))],
    };

    private static AppointmentSlotSummaryDto Summary(Guid slotId, DateOnly date) => new()
    {
        ConfirmedSlotId = slotId,
        Date = date,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Counts = new AppointmentStatusCountsDto
        {
            Expected = 1, CheckedIn = 0, Completed = 0, NoShow = 0,
        },
    };

    private static AppointmentSlotDetailDto CurrentDetail(string status) =>
        Detail(CurrentSlotId, new DateOnly(2026, 9, 7), "Alex Morgan", "alex@example.com", status);

    private static AppointmentSlotDetailDto PastDetail(string status) =>
        Detail(PastSlotId, new DateOnly(2026, 9, 6), "Past Candidate", "past@example.com", status);

    private static AppointmentSlotDetailDto Detail(
        Guid slotId, DateOnly date, string name, string email, string status) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        ConfirmedSlotId = slotId,
        Date = date,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.NewGuid(),
                CandidateName = name, CandidateEmail = email,
                Status = status, CheckedInAt = null,
                OutcomeAt = null, Version = 1,
            },
        ],
    };

    private static HttpResponseMessage Ok<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(body, options: CamelCase),
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();
        /// <summary>Gets the requests captured in sending order.</summary>
        public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = [];
        /// <summary>Queues one response for the next request.</summary>
        public void Enqueue(HttpResponseMessage response) =>
            _responses.Enqueue(_ => Task.FromResult(response));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return await _responses.Dequeue()(cancellationToken);
        }
    }
}
`````

## after — tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs — 1/1

<!-- vocabulary-file: {"id":370,"oldPath":"tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs","newPath":"tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs","beforeSha":"acd706eb30ca5a8bc3265d2a5f8fd655eaf90b4a535fa97ec4f95b0ecf254e71","afterSha":"8251bd2c9110c3b5f09709e9181e4e309a029ebcb21785916802fb17736d8c9c","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using EventBooking.Web.Pages;
using EventBooking.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the appointment page groups recently past events without changing row behavior.</summary>
public sealed class AppointmentsRecentPastTests : BunitContext
{
    private static readonly DateTimeOffset OperationalNow =
        new(2026, 9, 7, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid PastEventId =
        Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid CurrentEventId =
        Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Verifies the selector separates recently past events from current ones.</summary>
    [Fact]
    public void EventSelectorGroupsRecentPastSeparately()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(MixedEventList()));
        handler.Enqueue(Ok(CurrentDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            var groups = cut.FindAll("select[name=event] optgroup");
            Assert.Equal(2, groups.Count);
            Assert.Equal("Recent past", groups[0].GetAttribute("label"));
            Assert.Equal("Current and upcoming", groups[1].GetAttribute("label"));
            Assert.Contains("2026-09-06", groups[0].TextContent);
            Assert.Contains("2026-09-07", groups[1].TextContent);
        });
    }

    /// <summary>Verifies the page opens on the nearest current event when past events come first.</summary>
    [Fact]
    public void DefaultsToNearestCurrentEvent()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(MixedEventList()));
        handler.Enqueue(Ok(CurrentDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains("Alex Morgan", cut.Markup));
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith(
            $"/api/appointment-workspace/events/{CurrentEventId}",
            handler.Requests[1].Path,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Monday, 07 Sep 2026", cut.Markup);
    }

    /// <summary>Verifies the page falls back to the most recent past event when nothing is current.</summary>
    [Fact]
    public void FallsBackToMostRecentPastEvent()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(PastOnlyEventList()));
        handler.Enqueue(Ok(PastDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() => Assert.Contains("Past Attendee", cut.Markup));
        Assert.Contains("Sunday, 06 Sep 2026", cut.Markup);
    }

    /// <summary>Verifies a past Expected row offers no-show but no check-in.</summary>
    [Fact]
    public void PastEventDisablesCheckInAndEnablesNoShow()
    {
        var handler = GivenClient();
        handler.Enqueue(Ok(PastOnlyEventList()));
        handler.Enqueue(Ok(PastDetail("Expected")));
        var cut = Render<Appointments>();

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("button[data-action=check-in]").HasAttribute("disabled"));
            Assert.False(cut.Find("button[data-action=no-show]").HasAttribute("disabled"));
            Assert.Contains("Check-in opens on the event date.", cut.Markup);
        });
    }

    private RoutedHandler GivenClient()
    {
        var handler = new RoutedHandler();
        Services.AddSingleton(new AppointmentsClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com"),
        }));
        Services.AddSingleton(new TransitionalLocationPageClock(
            "Europe/London", () => OperationalNow));
        return handler;
    }

    private static AppointmentWorkspaceEventListDto MixedEventList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Events =
        [
            Summary(PastEventId, new DateOnly(2026, 9, 6)),
            Summary(CurrentEventId, new DateOnly(2026, 9, 7)),
        ],
    };

    private static AppointmentWorkspaceEventListDto PastOnlyEventList() => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        Events = [Summary(PastEventId, new DateOnly(2026, 9, 6))],
    };

    private static AppointmentEventSummaryDto Summary(Guid eventId, DateOnly date) => new()
    {
        EventId = eventId,
        Date = date,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Counts = new AppointmentStatusCountsDto
        {
            Expected = 1, CheckedIn = 0, Completed = 0, NoShow = 0,
        },
    };

    private static AppointmentEventDetailDto CurrentDetail(string status) =>
        Detail(CurrentEventId, new DateOnly(2026, 9, 7), "Alex Morgan", "alex@example.com", status);

    private static AppointmentEventDetailDto PastDetail(string status) =>
        Detail(PastEventId, new DateOnly(2026, 9, 6), "Past Attendee", "past@example.com", status);

    private static AppointmentEventDetailDto Detail(
        Guid eventId, DateOnly date, string name, string email, string status) => new()
    {
        AppointmentTypeName = "Uniform Fitting",
        EventId = eventId,
        Date = date,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(13, 0),
        Appointments =
        [
            new BookingAppointmentRowDto
            {
                BookingAppointmentId = Guid.NewGuid(),
                AttendeeName = name, AttendeeEmail = email,
                Status = status, CheckedInAt = null,
                OutcomeAt = null, Version = 1,
            },
        ],
    };

    private static HttpResponseMessage Ok<T>(T body) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(body, options: CamelCase),
    };

    private sealed class RoutedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();
        /// <summary>Gets the requests captured in sending order.</summary>
        public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = [];
        /// <summary>Queues one response for the next request.</summary>
        public void Enqueue(HttpResponseMessage response) =>
            _responses.Enqueue(_ => Task.FromResult(response));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return await _responses.Dequeue()(cancellationToken);
        }
    }
}
`````

## before — tests/EventBooking.Web.Tests/AuditClientTests.cs — 1/1

<!-- vocabulary-file: {"id":371,"oldPath":"tests/EventBooking.Web.Tests/AuditClientTests.cs","newPath":"tests/EventBooking.Web.Tests/AuditClientTests.cs","beforeSha":"d99143ae6d29a3974bb2db35ae2e31e8c8f30839f8380254eae728eb6bbad3a0","afterSha":"d43a5079da74068aec16078f545971c1afc5d98dab7d07a2e12cd0083a5f5e1d","side":"before","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the audit search client builds its query string and reads the page back.</summary>
public class AuditClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }

    private static (AuditClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AuditClient(http), handler);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task SearchBuildsQueryStringAndReadsPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        var outcome = await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, "SlotConfirmed", null, null, null, 50),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Value);
        Assert.Null(outcome.Value!.NextCursor);
        Assert.Equal("/api/audit/search", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Contains("action=SlotConfirmed", handler.Request.RequestUri.Query);
        Assert.Contains("pageSize=50", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task SearchSendsCursorForNextPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, "abc123", 50),
            CancellationToken.None);

        Assert.Contains("cursor=abc123", handler.Request!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchOmitsAbsentCriteria()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, null, 50),
            CancellationToken.None);

        var query = handler.Request!.RequestUri!.Query;
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
        Assert.DoesNotContain("actorType=", query);
        Assert.DoesNotContain("entityType=", query);
        Assert.DoesNotContain("cursor=", query);
    }

    [Fact]
    public async Task SearchSendsBothTimestampBoundsWhenGiven()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

        await client.SearchAsync(
            new AuditSearchFilterDto(from, to, "Staff", null, "abc", "ConfirmedSlot", null, 25),
            CancellationToken.None);

        var query = Uri.UnescapeDataString(handler.Request!.RequestUri!.Query);
        Assert.Contains(from.ToString("O"), query);
        Assert.Contains(to.ToString("O"), query);
        Assert.Contains("actorType=Staff", query);
        Assert.Contains("identifier=abc", query);
        Assert.Contains("entityType=ConfirmedSlot", query);
    }

    [Fact]
    public async Task SearchReadsRowsFromThePage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse(
            """
            {"rows":[{"timestamp":"2026-09-03T12:00:00+00:00","entityType":"ConfirmedSlot",
            "entityId":"11111111-1111-1111-1111-111111111111","action":"SlotConfirmed",
            "actorType":"Staff","actorId":"staff-1","details":"6 headcount"}],"nextCursor":"next"}
            """);

        var outcome = await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, null, 50),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var row = Assert.Single(outcome.Value!.Rows);
        Assert.Equal("SlotConfirmed", row.Action);
        Assert.Equal("6 headcount", row.Details);
        Assert.Equal("next", outcome.Value.NextCursor);
    }
}
`````

## after — tests/EventBooking.Web.Tests/AuditClientTests.cs — 1/1

<!-- vocabulary-file: {"id":371,"oldPath":"tests/EventBooking.Web.Tests/AuditClientTests.cs","newPath":"tests/EventBooking.Web.Tests/AuditClientTests.cs","beforeSha":"d99143ae6d29a3974bb2db35ae2e31e8c8f30839f8380254eae728eb6bbad3a0","afterSha":"d43a5079da74068aec16078f545971c1afc5d98dab7d07a2e12cd0083a5f5e1d","side":"after","part":1,"parts":1} -->

`````csharp
using System.Net;
using System.Text;
using EventBooking.Web.Services;

namespace EventBooking.Web.Tests;

/// <summary>Verifies the audit search client builds its query string and reads the page back.</summary>
public class AuditClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(Response);
        }
    }

    private static (AuditClient Client, StubHandler Handler) Given()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return (new AuditClient(http), handler);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task SearchBuildsQueryStringAndReadsPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        var outcome = await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, "EventConfirmed", null, null, null, 50),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        Assert.NotNull(outcome.Value);
        Assert.Null(outcome.Value!.NextCursor);
        Assert.Equal("/api/audit/search", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Contains("action=EventConfirmed", handler.Request.RequestUri.Query);
        Assert.Contains("pageSize=50", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task SearchSendsCursorForNextPage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, "abc123", 50),
            CancellationToken.None);

        Assert.Contains("cursor=abc123", handler.Request!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchOmitsAbsentCriteria()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");

        await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, null, 50),
            CancellationToken.None);

        var query = handler.Request!.RequestUri!.Query;
        Assert.DoesNotContain("from=", query);
        Assert.DoesNotContain("to=", query);
        Assert.DoesNotContain("actorType=", query);
        Assert.DoesNotContain("entityType=", query);
        Assert.DoesNotContain("cursor=", query);
    }

    [Fact]
    public async Task SearchSendsBothTimestampBoundsWhenGiven()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse("""{"rows":[],"nextCursor":null}""");
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);

        await client.SearchAsync(
            new AuditSearchFilterDto(from, to, "Staff", null, "abc", "Event", null, 25),
            CancellationToken.None);

        var query = Uri.UnescapeDataString(handler.Request!.RequestUri!.Query);
        Assert.Contains(from.ToString("O"), query);
        Assert.Contains(to.ToString("O"), query);
        Assert.Contains("actorType=Staff", query);
        Assert.Contains("identifier=abc", query);
        Assert.Contains("entityType=Event", query);
    }

    [Fact]
    public async Task SearchReadsRowsFromThePage()
    {
        var (client, handler) = Given();
        handler.Response = JsonResponse(
            """
            {"rows":[{"timestamp":"2026-09-03T12:00:00+00:00","entityType":"Event",
            "entityId":"11111111-1111-1111-1111-111111111111","action":"EventConfirmed",
            "actorType":"Staff","actorId":"staff-1","details":"6 headcount"}],"nextCursor":"next"}
            """);

        var outcome = await client.SearchAsync(
            new AuditSearchFilterDto(null, null, null, null, null, null, null, 50),
            CancellationToken.None);

        Assert.True(outcome.IsSuccess);
        var row = Assert.Single(outcome.Value!.Rows);
        Assert.Equal("EventConfirmed", row.Action);
        Assert.Equal("6 headcount", row.Details);
        Assert.Equal("next", outcome.Value.NextCursor);
    }
}
`````
