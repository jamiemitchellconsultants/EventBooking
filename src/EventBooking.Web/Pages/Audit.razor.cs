using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Pages;

public partial class Audit
{
    [Inject] private IAuditClient Audits { get; set; } = default!;
    [Inject] private IMeClient Me { get; set; } = default!;

    // The web app deliberately does not reference the domain assembly, so the server's enum names
    // are repeated here as plain strings; the API rejects anything it does not recognise.
    private static readonly string[] ActorTypes = ["Staff", "AttendeeToken", "System"];

    private static readonly string[] Actions =
    [
        "ProposalCreated", "ProposalWithdrawn", "AcceptanceRecorded", "AcceptanceWithdrawn",
        "EventConfirmed", "EventCancelled", "CapacityDecremented", "CapacityIncremented",
        "InviteCreated", "InviteSent", "InviteExpired", "InviteOptionReplaced",
        "BookingCreated", "BookingCancelled", "CapacityAdjusted",
        "StaffAccessChanged", "AppointmentCheckedIn", "AppointmentCompleted",
        "AppointmentMarkedNoShow", "AppointmentStatusCorrected", "AttendeeGroupAssigned",
        "AttendeeGroupReassigned", "RecoveryInviteCreated", "RecoveryInviteCancelled",
        "RecoveryBookingCreated", "RecoveryBookingConcluded", "StaffRolesSynced",
    ];

    private static readonly string[] EntityTypes =
    [
        "EventProposal", "Event", "Invite", "Booking",
        "StaffAccessProfile", "BookingAppointment", "Attendee",
    ];

    private readonly List<AuditRowDto> _rows = [];
    private DateTime? _from;
    private DateTime? _to;
    private string? _actorType;
    private string? _action;
    private string? _entityId;
    private string? _actorId;
    private string? _entityType;
    private string? _nextCursor;
    private string? _error;
    private bool _busy;
    private bool _showEntityType;

    protected override async Task OnInitializedAsync()
    {
        var me = await Me.GetAsync(CancellationToken.None);
        _showEntityType = me.Value?.Roles.Contains("Coordinator") == true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _rows.Clear();
        _nextCursor = null;
        await LoadMoreAsync();
    }

    private async Task LoadMoreAsync()
    {
        _busy = true;
        _error = null;
        try
        {
            var outcome = await Audits.SearchAsync(
                new AuditFilters(
                    ToOffset(_from),
                    ToOffset(_to),
                    EmptyToNull(_actorType),
                    EmptyToNull(_action),
                    EmptyToNull(_entityType),
                    Guid.TryParse(EmptyToNull(_entityId), out var entityId) ? entityId : null,
                    EmptyToNull(_actorId)),
                _nextCursor,
                CancellationToken.None);

            if (outcome is { IsSuccess: true, Value: not null })
            {
                _rows.AddRange(outcome.Value.Items);
                _nextCursor = outcome.Value.NextCursor;
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
        finally
        {
            _busy = false;
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
