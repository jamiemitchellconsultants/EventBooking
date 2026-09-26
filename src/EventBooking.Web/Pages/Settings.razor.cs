using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class Settings
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private SettingsForm? _form;
    private bool _loading = true, _busy;
    private string? _error;
    private long? _conflictVersion;
    private IReadOnlyDictionary<string, ApiLink> _links = new Dictionary<string, ApiLink>();
    protected override Task OnInitializedAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            var r = await Api.GetSettingsAsync(CancellationToken.None);
            if (r.IsSuccess)
            {
                _form = SettingsForm.From(r.Value!);
                _links = r.Value!.Links;
            }
            else _error = r.ErrorMessage;
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _loading = false;
    }
    private async Task SaveAsync()
    {
        if (_form is null) return;
        _busy = true;
        _error = null;
        try
        {
            var r = await Api.UpdateSettingsAsync(_form.ToDto(_links), CancellationToken.None);
            if (r.IsSuccess)
            {
                _form = SettingsForm.From(r.Value!);
                _links = r.Value!.Links;
                _conflictVersion = null;
            }
            else
            {
                _error = r.ErrorMessage;
                if (r.ErrorCode == "version-conflict" && r.Problem?.Current is { } current && current.TryGetProperty("currentVersion", out var version) && version.TryGetInt64(out var number))
                {
                    _conflictVersion = number;
                    _form.Version = number;
                }
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _busy = false;
    }
    private sealed class SettingsForm
    {
        public int InviteExpiryDays
        {
            get;
            set;
        }
        public int MaxAutoRetryCount
        {
            get;
            set;
        }
        public int InviteOptionCount
        {
            get;
            set;
        }
        public int PendingRegistrationExpiryHours
        {
            get;
            set;
        }
        public long Version
        {
            get;
            set;
        }
        public static SettingsForm From(SettingsDto x) => new()
        {
            InviteExpiryDays = x.InviteExpiryDays,
            MaxAutoRetryCount = x.MaxAutoRetryCount,
            InviteOptionCount = x.InviteOptionCount,
            PendingRegistrationExpiryHours = x.PendingRegistrationExpiryHours,
            Version = x.Version
        };
        public SettingsDto ToDto(IReadOnlyDictionary<string, ApiLink> links) => new(InviteExpiryDays, MaxAutoRetryCount, InviteOptionCount, PendingRegistrationExpiryHours, Version, links);
    }
}
