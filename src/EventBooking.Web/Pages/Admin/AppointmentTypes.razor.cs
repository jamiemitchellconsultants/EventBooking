using Microsoft.AspNetCore.Components;
using EventBooking.Web.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages.Admin;

public partial class AppointmentTypes
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private IMeClient CurrentStaff { get; set; } = default!;

    private readonly List<AppointmentTypeDto> _rows = [];
    private AppointmentTypeForm? _edit;
    private bool _loading = true, _creating, _canCreate, _busy;
    private string? _error;
    private long? _conflictVersion;
    private IdempotencySubmission? _submission;
    private IReadOnlyList<TableColumn<AppointmentTypeDto>> Columns =>
    [
        new("Code", x => b => b.AddContent(0, x.Code)),
        new("Name", x => b =>
        {
            b.OpenComponent<TypeChip>(0);
            b.AddAttribute(1, "Code", x.Code);
            b.AddAttribute(2, "Name", x.Name);
            b.CloseComponent();
        }),
        new("Manager", x => b => b.AddContent(0,
            x.HasManager ? x.ManagerDisplayName ?? "Manager assigned" : "No Manager assigned")),
        new("Status", x => b => b.AddContent(0, x.IsActive ? "Active" : "Inactive")),
        new("Actions", x => b =>
        {
            if (!x.Links.Allows("update")) return;
            b.OpenElement(0, "button");
            b.AddAttribute(1, "data-action", "edit");
            b.AddAttribute(2, "class", "button");
            b.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () =>
            {
                _creating = false;
                _edit = AppointmentTypeForm.From(x);
            }));
            b.AddContent(4, "Edit");
            b.CloseElement();
        })
    ];

    protected override Task OnInitializedAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        try
        {
            var context = await CurrentStaff.GetAsync(CancellationToken.None);
            var r = await Api.ListAppointmentTypesAsync(true, CancellationToken.None);
            _canCreate = context.IsSuccess && context.Value!.Links.Allows("createAppointmentType");
            if (r.IsSuccess)
            {
                _rows.Clear();
                _rows.AddRange(r.Value!.Items);
            }
            else _error = r.ErrorMessage;
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _loading = false;
    }
    private void New()
    {
        _creating = true;
        _submission = IdempotencySubmission.Start();
        _edit = new()
        {
            IsActive = true
        };
    }
    private void Cancel()
    {
        _edit = null;
        _creating = false;
        _conflictVersion = null;
    }
    private async Task SaveAsync()
    {
        if (_edit is null) return;
        _busy = true;
        _error = null;
        try
        {
            var r = _creating ? await Api.CreateAppointmentTypeAsync(_edit.Code, _edit.Name, _submission!, CancellationToken.None) : await Api.UpdateAppointmentTypeAsync(_edit.ToDto(), CancellationToken.None);
            if (r.IsSuccess)
            {
                Cancel();
                await LoadAsync();
            }
            else
            {
                _error = r.ErrorMessage;
                if (r.ErrorCode == "version-conflict" && r.Problem?.Current is { } current && current.TryGetProperty("currentVersion", out var version) && version.TryGetInt64(out var number))
                {
                    _conflictVersion = number;
                    _edit.Version = number;
                }
            }
        }
        catch (Exception)
        {
            _error = "Something went wrong. Please try again.";
        }
        _busy = false;
    }
    private sealed class AppointmentTypeForm
    {
        public Guid Id
        {
            get;
            init;
        }
        public string Code
        {
            get;
            set;
        }
        = "";
        public string Name
        {
            get;
            set;
        }
        = "";
        public bool IsActive
        {
            get;
            set;
        }
        public long Version
        {
            get;
            set;
        }
        public bool HasManager
        {
            get;
            init;
        }
        public string? ManagerDisplayName
        {
            get;
            init;
        }
        public static AppointmentTypeForm From(AppointmentTypeDto x) => new()
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            IsActive = x.IsActive,
            Version = x.Version,
            HasManager = x.HasManager,
            ManagerDisplayName = x.ManagerDisplayName
        };
        public AppointmentTypeDto ToDto() => new(Id, Code, Name, IsActive, Version, HasManager, ManagerDisplayName, new Dictionary<string, ApiLink>());
    }
}
