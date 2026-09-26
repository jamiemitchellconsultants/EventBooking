using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class StaffAccess
{
    [Inject] private StaffAccessClient Api { get; set; } = default!;
    [Inject] private AdminClient Types { get; set; } = default!;

    private readonly List<StaffAccessProfileDto> _rows = [];
    private readonly Dictionary<Guid, Guid?> _scope = [];
    private IReadOnlyList<AppointmentTypeDto> _options = [];
    private bool _loading = true, _busy;
    private string? _error;
    private string? _confirmation;
    protected override Task OnInitializedAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            var profiles = await Api.ListAsync(CancellationToken.None);
            var types = await Types.ListAppointmentTypesAsync(false, CancellationToken.None);
            if (profiles.IsSuccess)
            {
                _rows.Clear();
                _rows.AddRange(profiles.Value!.Items);
                _scope.Clear();
                foreach (var p in _rows) _scope[p.StaffUserId] = p.AppointmentTypeId;
            }
            else _error = profiles.ErrorMessage;
            if (types.IsSuccess) _options = types.Value!.Items.Where(x => x.IsActive).ToArray();
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _loading = false;
    }
    private string ScopeName(Guid? id) => id is null ? "No scope" : _options.FirstOrDefault(x => x.Id == id)?.Name ?? "Unknown type";
    private async Task SaveAsync(StaffAccessProfileDto profile)
    {
        _busy = true;
        _error = null;
        _confirmation = null;
        try
        {
            var chosen = _scope.TryGetValue(profile.StaffUserId, out var selected) ? selected : profile.AppointmentTypeId;
            var r = await Api.SetScopeAsync(profile.StaffUserId, chosen, profile.Version, CancellationToken.None);
            if (r.IsSuccess)
            {
                var outcome = r.Value!;
                var index = _rows.IndexOf(profile);
                if (index >= 0) _rows[index] = profile with
                {
                    AppointmentTypeId = outcome.AppointmentTypeId
                };
                _scope[profile.StaffUserId] = outcome.AppointmentTypeId;
                _confirmation = outcome.DisplacedManagerDisplayName is null ? $"Scope saved for {profile.DisplayName ?? profile.StaffId}.$" : $"{outcome.DisplacedManagerDisplayName} is no longer the Manager for {ScopeName(outcome.AppointmentTypeId)}.";
            }
            else _error = r.ErrorMessage;
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _busy = false;
    }
}
