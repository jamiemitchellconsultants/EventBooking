using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Shared;

public partial class AuditHistory
{
    [Inject] private IAuditClient Audit { get; set; } = default!;

    [Parameter, EditorRequired]
    public AuditEntityKind EntityKind { get; set; }

    [Parameter, EditorRequired]
    public Guid EntityId { get; set; }

    private readonly List<AuditRowDto> _rows = [];
    private string? _cursor;
    private string? _error;
    private bool _expanded;
    private bool _requested;
    private bool _busy;

    // Loaded on first expand rather than on render: a table of many rows must not
    // fire one request per row. The result is cached while collapsed.
    private async Task ToggleAsync()
    {
        _expanded = !_expanded;
        if (_expanded && !_requested)
            await LoadFirstPageAsync();
    }

    private Task ReloadAsync() => LoadFirstPageAsync();

    private async Task LoadFirstPageAsync()
    {
        _busy = true; _error = null;
        try
        {
            var outcome = await ReadAsync(null);
            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.Clear();
                _rows.AddRange(outcome.Value.Items);
                _cursor = outcome.Value.NextCursor;
                _requested = true;
            }
            else
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _busy = false;
    }

    private async Task LoadMoreAsync()
    {
        _busy = true;
        try
        {
            var outcome = await ReadAsync(_cursor);
            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.AddRange(outcome.Value.Items);
                _cursor = outcome.Value.NextCursor;
            }
            else
            {
                _error = outcome.ErrorMessage ?? "Something went wrong. Please try again.";
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _busy = false;
    }

    private Task<ApiOutcome<PageDto<AuditRowDto>>> ReadAsync(string? cursor) =>
        EntityKind == AuditEntityKind.Event
            ? Audit.ForEventAsync(EntityId, cursor, CancellationToken.None)
            : Audit.ForAttendeeAsync(EntityId, cursor, CancellationToken.None);
}
