using Microsoft.AspNetCore.Components;
using EventBooking.Web.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages.Admin;

public partial class AttendeeGroups
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private IMeClient CurrentStaff { get; set; } = default!;

    private List<AttendeeGroupDto> Rows { get; } = [];
    private AttendeeGroupForm? _edit;
    private bool _confirm, _creating, _canCreate, _loading = true, _busy;
    private string? _error;
    private long? _conflictVersion;
    private IdempotencySubmission? _submission;
    private IReadOnlyList<TypeOption> TypeOptions = [];
    private IReadOnlyList<TableColumn<AttendeeGroupDto>> Columns =>
    [
        new("Code", x => b => b.AddContent(0, x.Code)),
        new("Name", x => b => b.AddContent(0, x.Name)),
        new("Members", x => b => b.AddContent(0, x.MemberCount)),
        new("Actions", x => b =>
        {
            if (!x.Links.Allows("update")) return;
            b.OpenElement(0, "button");
            b.AddAttribute(1, "data-action", "edit");
            b.AddAttribute(2, "class", "button");
            b.AddAttribute(3, "onclick", EventCallback.Factory.Create(this, () =>
            {
                _creating = false;
                _confirm = false;
                _edit = AttendeeGroupForm.From(x);
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
            var groups = await Api.ListAttendeeGroupsAsync(true, CancellationToken.None);
            var types = await Api.ListAppointmentTypesAsync(true, CancellationToken.None);
            _canCreate = context.IsSuccess && context.Value!.Links.Allows("createAttendeeGroup");
            if (groups.IsSuccess)
            {
                Rows.Clear();
                Rows.AddRange(groups.Value!.Items);
            }
            else _error = groups.ErrorMessage;
            if (types.IsSuccess) TypeOptions = types.Value!.Items.Select(x => new TypeOption(x.Id, x.Code, x.Name, x.IsActive, x.HasManager)).ToArray();
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
        _confirm = false;
        _submission = IdempotencySubmission.Start();
        _edit = new()
        {
            IsActive = true
        };
    }
    private void Cancel()
    {
        _edit = null;
        _confirm = false;
        _creating = false;
        _conflictVersion = null;
    }
    private async Task RequestSaveAsync()
    {
        if (_edit is null) return;
        if (!_creating && _edit.MemberCount > 0)
        {
            _confirm = true;
            return;
        }
        await SaveConfirmedAsync();
    }
    private async Task SaveConfirmedAsync()
    {
        if (_edit is null) return;
        _busy = true;
        _error = null;
        try
        {
            var r = _creating ? await Api.CreateAttendeeGroupAsync(_edit.Code, _edit.Name, _edit.Description, _edit.RequirementTypeIds, _submission!, CancellationToken.None) : await Api.UpdateAttendeeGroupAsync(_edit.ToDto(), CancellationToken.None);
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
    private sealed class AttendeeGroupForm
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
        public string Description
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
        public IReadOnlyList<Guid> RequirementTypeIds
        {
            get;
            set;
        }
        = [];
        public int MemberCount
        {
            get;
            init;
        }
        public static AttendeeGroupForm From(AttendeeGroupDto x) => new()
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            Description = x.Description ?? "",
            IsActive = x.IsActive,
            Version = x.Version,
            RequirementTypeIds = x.RequirementTypeIds,
            MemberCount = x.MemberCount
        };
        public AttendeeGroupDto ToDto() => new(Id, Code, Name, Description, IsActive, Version, RequirementTypeIds, MemberCount, new Dictionary<string, ApiLink>());
    }
}
