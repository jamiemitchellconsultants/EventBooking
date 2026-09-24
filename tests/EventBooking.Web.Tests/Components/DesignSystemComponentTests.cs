using Bunit;
using EventBooking.Web.Components;
using EventBooking.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Web.Tests.Components;

public sealed class DesignSystemComponentTests : BunitContext
{
    [Fact]
    public void StatusBadgeAlwaysCarriesDisplayText()
    {
        var cut = Render<StatusBadge>(p => p
            .Add(x => x.Value, "NoShow")
            .Add(x => x.Display, "No-show"));

        Assert.Equal("No-show", cut.Find(".status-badge").TextContent.Trim());
        Assert.Contains("status-noshow", cut.Find(".status-badge").ClassList);
    }

    [Fact]
    public void TypeChipUsesCodeForAStablePaletteClassAndNameForText()
    {
        var first = Render<TypeChip>(p => p
            .Add(x => x.Code, "MED")
            .Add(x => x.Name, "Medical check"));
        var second = Render<TypeChip>(p => p
            .Add(x => x.Code, "MED")
            .Add(x => x.Name, "Renamed medical check"));

        Assert.Equal("Medical check", first.Find(".type-chip").TextContent.Trim());
        Assert.Equal("MED", first.Find(".type-chip").GetAttribute("title"));
        Assert.Equal(
            first.Find(".type-chip").ClassList.Single(x => x.StartsWith("type-palette-")),
            second.Find(".type-chip").ClassList.Single(x => x.StartsWith("type-palette-")));
    }

    [Theory]
    [InlineData(5, 5, false)]
    [InlineData(6, 0, true)]
    public void DataTableUsesDynamicColumnsUntilTheSixthType(
        int typeCount, int expectedTypeHeaders, bool expectedCapacityHeader)
    {
        var capacities = Enumerable.Range(0, typeCount)
            .Select(i => new TypeCapacity(Guid.NewGuid(), $"T{i:00}", $"Type {i}", i + 1, i + 2))
            .ToArray();
        var row = new Row(Guid.NewGuid(), capacities);
        RenderFragment<Row> name = item => builder => builder.AddContent(0, item.Id);

        var cut = Render<DataTable<Row>>(p => p
            .Add(x => x.Items, [row])
            .Add(x => x.RowKey, x => x.Id)
            .Add(x => x.Columns, [new TableColumn<Row>("Row", name)])
            .Add(x => x.Capacities, x => x.Capacities)
            .Add(x => x.EmptyTitle, "No rows")
            .Add(x => x.EmptyActionLabel, "Create one"));

        Assert.Equal(expectedTypeHeaders, cut.FindAll("th[data-type-code]").Count);
        Assert.Equal(expectedCapacityHeader, cut.Markup.Contains(">Capacity<"));
        Assert.Equal(
            expectedCapacityHeader ? typeCount : 0,
            cut.FindAll(".capacity-breakdown li").Count);
    }

    [Fact]
    public void TypePickerLocksCallerAndExplainsManagerlessType()
    {
        var caller = Guid.NewGuid();
        var managerless = Guid.NewGuid();
        var cut = Render<TypePicker>(p => p
            .Add(x => x.Options,
            [
                new(caller, "MED", "Medical", true, true),
                new(managerless, "ESC", "Escort briefing", true, false),
            ])
            .Add(x => x.CallerTypeId, caller)
            .Add(x => x.SelectedIds, [caller]));

        Assert.True(cut.Find($"input[value='{caller}']").HasAttribute("disabled"));
        Assert.True(cut.Find($"input[value='{managerless}']").HasAttribute("disabled"));
        Assert.Contains("No Manager assigned", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void LocationPickerShowsNameAddressAndZoneAndEmitsStableMultiSelection()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        IReadOnlyList<Guid>? changed = null;
        var cut = Render<LocationPicker>(p => p
            .Add(x => x.Mode, LocationPickerMode.Multi)
            .Add(x => x.Options,
            [
                new(first, "London HQ", "1 Example St", "BST", true),
                new(second, "Dublin", "2 Sample Rd", "IST", true),
            ])
            .Add(x => x.SelectedIds, [first])
            .Add(x => x.SelectedIdsChanged,
                EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, value => changed = value)));

        Assert.Contains("London HQ", cut.Markup);
        Assert.Contains("1 Example St", cut.Markup);
        Assert.Contains("BST", cut.Markup);
        cut.Find($"input[value='{second}']").Change(true);
        Assert.Equal([first, second], changed);
    }

    [Fact]
    public void BannerAnnouncesItsAppearanceAndOffersRetry()
    {
        var retried = false;
        var cut = Render<Banner>(p => p
            .Add(x => x.Variant, BannerVariant.Error)
            .Add(x => x.Message, "The list could not load.")
            .Add(x => x.Retry, EventCallback.Factory.Create(this, () => retried = true)));

        Assert.Equal("assertive", cut.Find("[aria-live]").GetAttribute("aria-live"));
        cut.Find("button").Click();
        Assert.True(retried);
    }

    [Fact]
    public void CsvRejectionListsLinesAndSaysNothingWasImported()
    {
        var cut = Render<CsvImportResult>(p => p
            .Add(x => x.Accepted, false)
            .Add(x => x.Errors,
            [
                new FieldProblem("email", 4, "duplicate", "Email is duplicated."),
                new FieldProblem("attendee_group", 7, "unknown", "Group is unknown."),
            ]));

        Assert.Contains("Nothing was imported.", cut.Markup);
        Assert.Contains("Line 4", cut.Markup);
        Assert.Contains("Line 7", cut.Markup);
    }

    [Fact]
    public void EventTimeRendersApiValuesWithoutDerivingAnEnd()
    {
        var time = new EventTimeDto(
            new DateOnly(2026, 10, 14), new TimeOnly(9, 30), 90,
            new DateTimeOffset(2026, 10, 14, 9, 30, 0, TimeSpan.FromHours(1)),
            new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.FromHours(1)),
            new DateTimeOffset(2026, 10, 14, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero),
            "Europe/London", "BST");

        var cut = Render<EventTime>(p => p
            .Add(x => x.Value, time)
            .Add(x => x.LocationName, "London HQ")
            .Add(x => x.ShowLocation, true));

        Assert.Contains("Wed 14 Oct 2026, 09:30–11:00 BST", cut.Markup);
        Assert.Contains("London HQ", cut.Markup);
    }

    [Fact]
    public void TwoStepButtonRequiresASecondActivation()
    {
        var confirmed = 0;
        var cut = Render<TwoStepButton>(parameters => parameters
            .Add(component => component.Label, "Cancel event")
            .Add(component => component.ConfirmLabel, "Confirm cancellation")
            .Add(component => component.Consequence, "Cancels 3 bookings")
            .Add(component => component.Confirmed,
                EventCallback.Factory.Create(this, () => confirmed++)));

        cut.Find("button").Click();

        Assert.Equal(0, confirmed);
        Assert.Equal("Confirm cancellation", cut.Find("button").TextContent.Trim());
        Assert.Contains("Cancels 3 bookings", cut.Find("[role='status']").TextContent);

        cut.Find("button").Click();

        Assert.Equal(1, confirmed);
        Assert.Equal("Cancel event", cut.Find("button").TextContent.Trim());
        Assert.Empty(cut.FindAll("[role='status']"));
    }

    [Fact]
    public void TwoStepButtonResetsAfterItsTenSecondWindow()
    {
        var time = new ManualTimeProvider();
        var cut = Render<TwoStepButton>(parameters => parameters
            .Add(component => component.Label, "Cancel event")
            .Add(component => component.ConfirmLabel, "Confirm cancellation")
            .Add(component => component.Consequence, "Cancels 3 bookings")
            .Add(component => component.TimeProvider, time));

        cut.Find("button").Click();
        Assert.Equal(TimeSpan.FromSeconds(10), time.LastDueTime);

        time.Elapse();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Cancel event", cut.Find("button").TextContent.Trim());
            Assert.Empty(cut.FindAll("[role='status']"));
        });
    }

    [Fact]
    public void TwoStepButtonResetsWhenNavigationChanges()
    {
        var cut = Render<TwoStepButton>(parameters => parameters
            .Add(component => component.Label, "Cancel event")
            .Add(component => component.ConfirmLabel, "Confirm cancellation")
            .Add(component => component.Consequence, "Cancels 3 bookings"));
        cut.Find("button").Click();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/another-page");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Cancel event", cut.Find("button").TextContent.Trim());
            Assert.Empty(cut.FindAll("[role='status']"));
        });
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private ManualTimer? _timer;

        public TimeSpan? LastDueTime { get; private set; }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            LastDueTime = dueTime;
            _timer = new ManualTimer(callback, state);
            return _timer;
        }

        public void Elapse() => _timer?.Fire();

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            private bool _active = true;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _active = true;
                return true;
            }

            public void Dispose() => _active = false;

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void Fire()
            {
                if (!_active)
                    return;

                _active = false;
                callback(state);
            }
        }
    }

    [Fact]
    public void DataTableAlignsEachCapacityCellUnderItsTypeHeader()
    {
        TypeCapacity Capacity(string code, int total) =>
            new(Guid.NewGuid(), code, $"Type {code}", total, total);
        var first = new Row(Guid.NewGuid(), [Capacity("OPS", 1), Capacity("CAB", 2)]);
        var second = new Row(Guid.NewGuid(), [Capacity("CAB", 7)]);
        RenderFragment<Row> name = item => builder => builder.AddContent(0, item.Id);

        var cut = Render<DataTable<Row>>(p => p
            .Add(x => x.Items, [first, second])
            .Add(x => x.RowKey, x => x.Id)
            .Add(x => x.Columns, [new TableColumn<Row>("Row", name)])
            .Add(x => x.Capacities, x => x.Capacities));

        var headers = cut.FindAll("th[data-type-code]").Select(x => x.GetAttribute("data-type-code")).ToArray();
        var rows = cut.FindAll("tbody tr");
        Assert.All(rows, row => Assert.Equal(1 + headers.Length, row.QuerySelectorAll("td").Length));
        var secondCells = rows[1].QuerySelectorAll("td").Skip(1).ToArray();
        var cabIndex = Array.IndexOf(headers, "CAB");
        var opsIndex = Array.IndexOf(headers, "OPS");
        Assert.Contains("7 total", secondCells[cabIndex].TextContent);
        Assert.Equal("CAB", secondCells[cabIndex].GetAttribute("data-label"));
        Assert.DoesNotContain("total", secondCells[opsIndex].TextContent);
    }

    [Fact]
    public void CollapsedDataTableRendersOnlyTheCapacityColumn()
    {
        var capacities = Enumerable.Range(0, 6)
            .Select(i => new TypeCapacity(Guid.NewGuid(), $"T{i:00}", $"Type {i}", i + 1, i + 2))
            .ToArray();
        var row = new Row(Guid.NewGuid(), capacities);
        RenderFragment<Row> name = item => builder => builder.AddContent(0, item.Id);

        var cut = Render<DataTable<Row>>(p => p
            .Add(x => x.Items, [row])
            .Add(x => x.RowKey, x => x.Id)
            .Add(x => x.Columns, [new TableColumn<Row>("Row", name)])
            .Add(x => x.Capacities, x => x.Capacities));

        Assert.Equal(2, cut.FindAll("thead th").Count);
        Assert.Equal(2, cut.FindAll("tbody tr td").Count);
    }

    private sealed record Row(Guid Id, IReadOnlyList<TypeCapacity> Capacities);
}
