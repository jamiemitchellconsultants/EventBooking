using Microsoft.AspNetCore.Components;
using EventBooking.Web.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages.Admin;

public partial class Locations
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private IMeClient CurrentStaff { get; set; } = default!;

    // The API accepts any IANA zone; this list is only the picker's choices. The zone being
    // edited is always offered, so a location stored in another zone keeps it on save.
    private static readonly IReadOnlyList<string> KnownZones = TimeZoneInfo.GetSystemTimeZones()
        .Select(zone => zone.HasIanaId ? zone.Id
            : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : null)
        .OfType<string>()
        .Concat(["Europe/London", "Europe/Dublin", "Asia/Tokyo", "Etc/UTC"])
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
    private const string DefaultZone = "Europe/London";
    private readonly List<LocationDto> _rows = []; private LocationForm? _edit; private LocationDto? _original; private string? _error; private long? _conflictVersion; private bool _creating; private bool _loading = true; private bool _busy; private bool _canCreate; private IdempotencySubmission? _submission;
    private (int Proposals, int Events)? _zoneBlocking; private (int Proposals, int Events)? _deactivationBlocking;
    private IEnumerable<string> ZoneOptions => _edit is null || KnownZones.Contains(_edit.TimeZoneId)
        ? KnownZones
        : KnownZones.Append(_edit.TimeZoneId).Order(StringComparer.Ordinal);
    private IReadOnlyList<TableColumn<LocationDto>> Columns => [new("Code", x => b => b.AddContent(0, x.Code)), new("Name", x => b => b.AddContent(0, x.Name)), new("Address", x => b => b.AddContent(0, x.Address)), new("Zone", x => b => b.AddContent(0, x.TimeZoneId)), new("Status", x => b => { b.OpenComponent<StatusBadge>(0); b.AddAttribute(1, "Value", x.IsActive ? "Active" : "Inactive"); b.AddAttribute(2, "Display", x.IsActive ? "Active" : "Inactive"); b.CloseComponent(); }), new("Actions", x => b => { if (x.Links.Allows("update")) { b.OpenElement(0, "button"); b.AddAttribute(1, "data-action", "edit"); b.AddAttribute(2, "class", "button"); b.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () => Edit(x))); b.AddContent(4, "Edit"); b.CloseElement(); } })];
    protected override Task OnInitializedAsync() => ReloadAsync();
    private async Task ReloadAsync() { _loading = true; _error = null; try { var context = await CurrentStaff.GetAsync(CancellationToken.None); var result = await Api.ListLocationsAsync(true, CancellationToken.None); _canCreate = context.IsSuccess && context.Value!.Links.Allows("createLocation"); if (result.IsSuccess) { _rows.Clear(); _rows.AddRange(result.Value!.Items); } else _error = result.ErrorMessage; } catch (Exception) { _error = "Something went wrong. Please try again."; } _loading = false; }
    private void New() { ClearEditorState(); _creating = true; _submission = IdempotencySubmission.Start(); _edit = new() { TimeZoneId = DefaultZone, IsActive = true }; }
    private void Edit(LocationDto value) { ClearEditorState(); _creating = false; _submission = null; _original = value; _edit = LocationForm.From(value); }
    private void Cancel() { ClearEditorState(); _edit = null; }
    private void ClearEditorState() { _original = null; _conflictVersion = null; _zoneBlocking = null; _deactivationBlocking = null; }
    private async Task SaveAsync()
    {
        if (_edit is null) return;
        _busy = true; _error = null;
        try
        {
            var result = _creating
                ? await Api.CreateLocationAsync(_edit.Code, _edit.Name, _edit.Address, _edit.TimeZoneId, _submission!, CancellationToken.None)
                : await Api.UpdateLocationAsync(_edit.ToDto(), CancellationToken.None);
            if (result.IsSuccess) { Cancel(); await ReloadAsync(); return; }
            _error = result.ErrorMessage;
            if (result.ErrorCode == "version-conflict" && result.Problem?.Current is { } current && current.TryGetProperty("currentVersion", out var version) && version.TryGetInt64(out var number)) { _conflictVersion = number; _edit.Version = number; }
            if (result.ErrorCode == "in-use" && _original is not null && result.Problem?.Blocking is { } blocking) RefuseInUseChange(Counts(blocking));
        }
        catch (Exception) { _error = "Something went wrong. Please try again."; }
        _busy = false;
    }
    // The server refuses both a zone change and a deactivation with `in-use`. Only the change
    // actually attempted is explained, and it is put back to the stored value so the next save
    // is not the same refused request again; the Admin's other edits are kept.
    private void RefuseInUseChange((int Proposals, int Events) counts)
    {
        if (_edit!.TimeZoneId != _original!.TimeZoneId) { _zoneBlocking = counts; _edit.TimeZoneId = _original.TimeZoneId; }
        if (!_edit.IsActive && _original.IsActive) { _deactivationBlocking = counts; _edit.IsActive = true; }
    }
    private static (int Proposals, int Events) Counts(System.Text.Json.JsonElement blocking) =>
        (Count(blocking, "openProposals"), Count(blocking, "futureEvents"));
    private static int Count(System.Text.Json.JsonElement blocking, string name) =>
        blocking.ValueKind == System.Text.Json.JsonValueKind.Object && blocking.TryGetProperty(name, out var value) && value.TryGetInt32(out var count) ? count : 0;
    private sealed class LocationForm
    {
        public Guid Id { get; init; }
        public string Code { get; set; } = ""; public string Name { get; set; } = "";
        public string Address { get; set; } = ""; public string TimeZoneId { get; set; } = "";
        public bool IsActive { get; set; }
        public long Version { get; set; }
        public static LocationForm From(LocationDto value) => new() { Id = value.Id, Code = value.Code, Name = value.Name, Address = value.Address, TimeZoneId = value.TimeZoneId, IsActive = value.IsActive, Version = value.Version };
        public LocationDto ToDto() => new(Id, Code, Name, Address, TimeZoneId, IsActive, Version, new Dictionary<string, ApiLink>());
    }
}
