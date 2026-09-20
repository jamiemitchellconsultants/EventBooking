# 01b — Admin-managed reference data, edits 1 (Task 5)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files. A file with only a before side is deleted; a file with only an after side is created. Numbered parts concatenate without omitted code.

## before — src/EventBooking.Application/Settings/AdminSettingsHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Settings/AdminSettingsHandler.cs","beforeSha":"094b23d12e690ad265089e94b6e78688437e8b86b561b636a56d95883dfec19d","afterSha":"e2cb8917fdf9bd907db831b66974ceda7a6cb3d41d64a5f71528d0fdff2ca26b","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Settings;

/// <summary>Projects one appointment type with the manager assigned to it, when there is one.</summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="ManagerUserId">The assigned manager's provider identity, or null when unassigned.</param>
/// <param name="ManagerStaffId">The manager's enterprise staff number; null until an identity is recorded.</param>
/// <param name="ManagerDisplayName">
/// The manager's human-readable name mirrored from the identity provider; null when the identity
/// carries none. Presentation data only.
/// </param>
public sealed record AppointmentTypeView(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerUserId,
    string? ManagerStaffId = null,
    string? ManagerDisplayName = null);

/// <summary>Defines settings view for the current use case.</summary>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
/// <param name="AppointmentTypes">The appointment types.</param>
public sealed record SettingsView(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    IReadOnlyList<AppointmentTypeView> AppointmentTypes);

/// <summary>Defines update settings command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
public sealed record UpdateSettingsCommand(Guid StaffUserId, int InviteExpiryDays, int MaxAutoRetryCount);

/// <summary>Defines admin settings handler for the current use case.</summary>
/// <param name="settings">The settings.</param>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class AdminSettingsHandler(
    ISystemSettingsRepository settings,
    IAppointmentTypeRepository appointmentTypes,
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork)
{
    /// <summary>Defines get async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<SettingsView>> GetAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<SettingsView>.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);
        var types = await appointmentTypes.ListAsync(cancellationToken);

        var managerByType = (await profiles.ListAsync(cancellationToken))
            .Where(profile => profile.IsManager && profile.AppointmentTypeId is not null)
            .ToDictionary(
                profile => profile.AppointmentTypeId!.Value,
                profile => profile.StaffUserId);

        // One listing serves both projections; the name never costs an extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);

        return Result<SettingsView>.Success(new SettingsView(
            current.InviteExpiryDays,
            current.MaxAutoRetryCount,
            types.Select(type =>
            {
                Guid? managerUserId = managerByType.TryGetValue(type.Id, out var found) ? found : null;
                var identity = managerUserId is null
                    ? null
                    : identityByUserId.GetValueOrDefault(managerUserId.Value);
                return new AppointmentTypeView(
                    type.Id,
                    type.Code,
                    type.Name,
                    managerUserId,
                    identity?.StaffId.Value,
                    identity?.DisplayName);
            }).ToList()));
    }

    /// <summary>Defines update async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> UpdateAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);

        try
        {
            current.Update(command.InviteExpiryDays, command.MaxAutoRetryCount);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
`````

## after — src/EventBooking.Application/Settings/AdminSettingsHandler.cs — 1/1

<!-- retirement-file: {"id":0,"file":"src/EventBooking.Application/Settings/AdminSettingsHandler.cs","beforeSha":"094b23d12e690ad265089e94b6e78688437e8b86b561b636a56d95883dfec19d","afterSha":"e2cb8917fdf9bd907db831b66974ceda7a6cb3d41d64a5f71528d0fdff2ca26b","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Application.Abstractions;
using EventBooking.Application.Access;
using EventBooking.Application.Common;
using EventBooking.Domain.Common;

namespace EventBooking.Application.Settings;

/// <summary>Projects one appointment type with the manager assigned to it, when there is one.</summary>
/// <param name="Id">The appointment type identifier.</param>
/// <param name="Code">The canonical appointment-type code.</param>
/// <param name="Name">The canonical appointment-type name.</param>
/// <param name="ManagerUserId">The assigned manager's provider identity, or null when unassigned.</param>
/// <param name="ManagerStaffId">The manager's enterprise staff number; null until an identity is recorded.</param>
/// <param name="ManagerDisplayName">
/// The manager's human-readable name mirrored from the identity provider; null when the identity
/// carries none. Presentation data only.
/// </param>
public sealed record AppointmentTypeView(
    Guid Id,
    string Code,
    string Name,
    Guid? ManagerUserId,
    string? ManagerStaffId = null,
    string? ManagerDisplayName = null);

/// <summary>Defines settings view for the current use case.</summary>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
/// <param name="AppointmentTypes">The appointment types.</param>
public sealed record SettingsView(
    int InviteExpiryDays,
    int MaxAutoRetryCount,
    IReadOnlyList<AppointmentTypeView> AppointmentTypes);

/// <summary>Defines update settings command for the current use case.</summary>
/// <param name="StaffUserId">The staff user id.</param>
/// <param name="InviteExpiryDays">The invite expiry days.</param>
/// <param name="MaxAutoRetryCount">The max auto retry count.</param>
public sealed record UpdateSettingsCommand(Guid StaffUserId, int InviteExpiryDays, int MaxAutoRetryCount);

/// <summary>Defines admin settings handler for the current use case.</summary>
/// <param name="settings">The settings.</param>
/// <param name="appointmentTypes">The appointment types.</param>
/// <param name="profiles">The profiles.</param>
/// <param name="identities">The identities.</param>
/// <param name="access">The access.</param>
/// <param name="unitOfWork">The unit of work.</param>
public sealed class AdminSettingsHandler(
    ISystemSettingsRepository settings,
    IAppointmentTypeRepository appointmentTypes,
    IStaffAccessProfileRepository profiles,
    IStaffIdentityRepository identities,
    IStaffAccessAuthorizer access,
    IUnitOfWork unitOfWork)
{
    /// <summary>Defines get async for the current use case.</summary>
    /// <param name="staffUserId">The staff user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result<SettingsView>> GetAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            staffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result<SettingsView>.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);
        var types = await appointmentTypes.ListAsync(cancellationToken);

        var managerByType = (await profiles.ListAsync(cancellationToken))
            .Where(profile => profile.IsManager && profile.AppointmentTypeId is not null)
            .ToDictionary(
                profile => profile.AppointmentTypeId!.Value,
                profile => profile.StaffUserId);

        // One listing serves both projections; the name never costs an extra query.
        var identityByUserId = (await identities.ListAsync(cancellationToken))
            .ToDictionary(identity => identity.StaffUserId);

        return Result<SettingsView>.Success(new SettingsView(
            current.InviteExpiryDays,
            current.MaxAutoRetryCount,
            types.Select(type =>
            {
                Guid? managerUserId = managerByType.TryGetValue(type.Id, out var found) ? found : null;
                var identity = managerUserId is null
                    ? null
                    : identityByUserId.GetValueOrDefault(managerUserId.Value);
                return new AppointmentTypeView(
                    type.Id,
                    type.Code,
                    type.Name,
                    managerUserId,
                    identity?.StaffId.Value,
                    identity?.DisplayName);
            }).ToList()));
    }

    /// <summary>Defines update async for the current use case.</summary>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<Result> UpdateAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var authorized = await access.AuthorizeAsync(
            command.StaffUserId,
            StaffCapability.ManageSettings,
            null,
            cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure(authorized.Error);
        }

        var current = await settings.GetAsync(cancellationToken);

        try
        {
            // The option count is domain state from Task 5 but not yet editable: Task 12 adds it to
            // this command, the API and MCP together. Passing the current value keeps it unchanged.
            current.Update(command.InviteExpiryDays, command.MaxAutoRetryCount, current.InviteOptionCount);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation(ex.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
`````

## before — src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs","beforeSha":"b3e1865ce66194e7380817546d41b9c03c40d2b5c4e489d1d736504351f32d23","afterSha":"1952b93d2e6397d5d1f54f015cef4546ed0a5335cbd0e75ffc87c15a632bbfcd","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.AppointmentTypes;

/// <summary>Defines appointment type for the current use case.</summary>
public sealed class AppointmentType
{
    private AppointmentType()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    private AppointmentType(Guid id, string code, string name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

    /// <summary>Defines id for the current use case.</summary>
    public Guid Id { get; private set; }
    /// <summary>Defines code for the current use case.</summary>
    public string Code { get; private set; }
    /// <summary>Defines name for the current use case.</summary>
    public string Name { get; private set; }

    /// <summary>Defines create fixed set for the current use case.</summary>
    public static IReadOnlyList<AppointmentType> CreateFixedSet() =>
        AppointmentTypeIds.All
            .Select(id => new AppointmentType(
                id,
                AppointmentTypeIds.CodeOf(id),
                AppointmentTypeIds.NameOf(id)))
            .ToList();
}
`````

## after — src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs — 1/1

<!-- retirement-file: {"id":1,"file":"src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs","beforeSha":"b3e1865ce66194e7380817546d41b9c03c40d2b5c4e489d1d736504351f32d23","afterSha":"1952b93d2e6397d5d1f54f015cef4546ed0a5335cbd0e75ffc87c15a632bbfcd","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AppointmentTypes;

/// <summary>
/// One kind of appointment, run by at most one current Manager across every location. Admin-managed
/// reference data: created, renamed and deactivated, never deleted (FR-1.3, FR-1.6, FR-1.7).
/// </summary>
public sealed class AppointmentType
{
    /// <summary>The longest code an appointment type may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name an appointment type may have (design 08).</summary>
    public const int MaximumNameLength = 100;

    private AppointmentType()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>The stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>The staff-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>Whether new proposals and groups may name this type.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Creates an active appointment type.</summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    public static AppointmentType Create(Guid id, string? code, string? name)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = BoundedName(name);

        return new AppointmentType
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            IsActive = true,
            Version = 1,
        };
    }

    /// <summary>Renames the type. Always allowed; the code never changes.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = BoundedName(name);
        Version++;
    }

    /// <summary>Deactivates the type, refusing while it is in live use (FR-1.6).</summary>
    /// <param name="usage">What the type currently carries.</param>
    public void Deactivate(AppointmentTypeUsage usage)
    {
        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The appointment type cannot be deactivated while it is listed or mapped.",
                usage.AsBlocking());
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the type to use.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    /// <summary>
    /// The predecessor's three seeded types, retained only until Phase 3 rewires seeding and the
    /// handlers onto Admin-managed types.
    /// </summary>
    public static IReadOnlyList<AppointmentType> CreateFixedSet() =>
        AppointmentTypeIds.All
            .Select(id => new AppointmentType
            {
                Id = id,
                Code = AppointmentTypeIds.CodeOf(id),
                Name = AppointmentTypeIds.NameOf(id),
                IsActive = true,
                Version = 1,
            })
            .ToList();

    private static string BoundedName(string? name)
    {
        var displayName = Guard.NotBlank(name, "name");
        Guard.Against(
            displayName.Length > MaximumNameLength,
            $"name must be at most {MaximumNameLength} characters.");
        return displayName;
    }
}
`````

## after — src/EventBooking.Domain/AppointmentTypes/AppointmentTypeUsage.cs — 1/1

<!-- retirement-file: {"id":2,"file":"src/EventBooking.Domain/AppointmentTypes/AppointmentTypeUsage.cs","beforeSha":null,"afterSha":"728278d422405496c9a68bf67003ea24bec98ea75ae2fe6bebf995e65d082e75","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.AppointmentTypes;

/// <summary>
/// How much live use an appointment type carries. The caller counts; the aggregate decides.
/// </summary>
/// <param name="OpenProposals">Open proposals listing the type.</param>
/// <param name="FutureEvents">Active events listing the type whose window has not passed.</param>
/// <param name="ActiveGroups">Active attendee groups mapping the type.</param>
public readonly record struct AppointmentTypeUsage(int OpenProposals, int FutureEvents, int ActiveGroups)
{
    /// <summary>A type nothing depends on.</summary>
    public static AppointmentTypeUsage None => new(0, 0, 0);

    /// <summary>Whether anything live depends on this type.</summary>
    public bool Any => OpenProposals > 0 || FutureEvents > 0 || ActiveGroups > 0;

    /// <summary>The blocking counts, for a refusal a screen can render.</summary>
    public IReadOnlyDictionary<string, int> AsBlocking() =>
        new Dictionary<string, int>
        {
            ["openProposals"] = OpenProposals,
            ["futureEvents"] = FutureEvents,
            ["activeGroups"] = ActiveGroups,
        };
}
`````

## before — src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs","beforeSha":"91a43af2ae168824ba0dcda534934654c88fe50632cdde5f97fba4420c2a048a","afterSha":"ae8a5a5eaebc726b708caf0820c26deb260fbba4c0f763486f143889eadf85c4","side":"before","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AttendeeGroups;

/// <summary>One change-controlled employment category that determines attendee requirements.</summary>
public sealed class AttendeeGroup
{
    private static readonly Regex CanonicalCodeExpression =
        new("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant);

    private readonly List<AttendeeGroupRequirement> _requirements = [];

    private AttendeeGroup()
    {
        // Materializes persisted rows, including inactive ones that Define would reject.
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Gets the stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>Gets the canonical Coordinator-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets whether new and changed Attendees may be assigned this group.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the fixed Appointment Type mappings owned by this group.</summary>
    public IReadOnlyList<AttendeeGroupRequirement> Requirements => _requirements;

    /// <summary>Gets the mapped Appointment Type identifiers in stable identifier order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(requirement => requirement.AppointmentTypeId).Order().ToList();

    /// <summary>Defines one validated reference-data row and its complete mapping.</summary>
    /// <param name="id">The id.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The name.</param>
    /// <param name="isActive">The is active.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static AttendeeGroup Define(
        Guid id,
        string? code,
        string? name,
        bool isActive,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(
            code is null || !CanonicalCodeExpression.IsMatch(code),
            "code must be canonical uppercase snake case.");
        Guard.Against(!isActive, "An inactive attendee group cannot be assignment authority.");

        var displayName = Guard.NotBlank(name, "name");
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An attendee group cannot map the same appointment type twice.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        var group = new AttendeeGroup
        {
            Id = id,
            Code = code!,
            Name = displayName,
            IsActive = isActive,
        };

        foreach (var appointmentTypeId in mapping.Order())
        {
            group._requirements.Add(AttendeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }
}
`````

## after — src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs — 1/1

<!-- retirement-file: {"id":3,"file":"src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs","beforeSha":"91a43af2ae168824ba0dcda534934654c88fe50632cdde5f97fba4420c2a048a","afterSha":"ae8a5a5eaebc726b708caf0820c26deb260fbba4c0f763486f143889eadf85c4","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;
using EventBooking.Domain.AppointmentTypes;
using EventBooking.Domain.Common;

namespace EventBooking.Domain.AttendeeGroups;

/// <summary>One change-controlled employment category that determines attendee requirements.</summary>
public sealed class AttendeeGroup
{
    private static readonly Regex CanonicalCodeExpression =
        new("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant);

    private readonly List<AttendeeGroupRequirement> _requirements = [];

    private AttendeeGroup()
    {
        // Materializes persisted rows, including inactive ones that Define would reject.
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Gets the stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>Gets the canonical Coordinator-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets whether new and changed Attendees may be assigned this group.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Gets the fixed Appointment Type mappings owned by this group.</summary>
    public IReadOnlyList<AttendeeGroupRequirement> Requirements => _requirements;

    /// <summary>Gets the mapped Appointment Type identifiers in stable identifier order.</summary>
    public IReadOnlyList<Guid> RequiredAppointmentTypeIds =>
        _requirements.Select(requirement => requirement.AppointmentTypeId).Order().ToList();

    /// <summary>Defines one validated reference-data row and its complete mapping.</summary>
    /// <param name="id">The id.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The name.</param>
    /// <param name="isActive">The is active.</param>
    /// <param name="appointmentTypeIds">The appointment type ids.</param>
    public static AttendeeGroup Define(
        Guid id,
        string? code,
        string? name,
        bool isActive,
        IEnumerable<Guid> appointmentTypeIds)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        Guard.Against(
            code is null || !CanonicalCodeExpression.IsMatch(code),
            "code must be canonical uppercase snake case.");
        Guard.Against(!isActive, "An inactive attendee group cannot be assignment authority.");

        var displayName = Guard.NotBlank(name, "name");
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An attendee group cannot map the same appointment type twice.");

        foreach (var appointmentTypeId in mapping)
        {
            AppointmentTypeIds.EnsureKnown(appointmentTypeId);
        }

        var group = new AttendeeGroup
        {
            Id = id,
            Code = code!,
            Name = displayName,
            IsActive = isActive,
        };

        foreach (var appointmentTypeId in mapping.Order())
        {
            group._requirements.Add(AttendeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }

    /// <summary>The longest code an attendee group may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name an attendee group may have (design 08).</summary>
    public const int MaximumNameLength = 200;

    /// <summary>
    /// Creates an active Admin-managed group whose mapping names only active appointment types
    /// (FR-1.4).
    /// </summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    /// <param name="appointmentTypeIds">The appointment types every member requires.</param>
    /// <param name="activeAppointmentTypeIds">Every appointment type currently active.</param>
    public static AttendeeGroup Create(
        Guid id,
        string? code,
        string? name,
        IEnumerable<Guid> appointmentTypeIds,
        IReadOnlyCollection<Guid> activeAppointmentTypeIds)
    {
        Guard.Against(id == Guid.Empty, "id must not be empty.");
        ArgumentNullException.ThrowIfNull(activeAppointmentTypeIds);

        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = BoundedName(name);
        var mapping = ValidatedMapping(appointmentTypeIds, activeAppointmentTypeIds);

        var group = new AttendeeGroup
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            IsActive = true,
            Version = 1,
        };

        foreach (var appointmentTypeId in mapping)
        {
            group._requirements.Add(AttendeeGroupRequirement.For(id, appointmentTypeId));
        }

        return group;
    }

    /// <summary>Renames the group. Always allowed; the code never changes.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = BoundedName(name);
        Version++;
    }

    /// <summary>
    /// Replaces the mapping set. A change that leaves the set identical is always allowed and
    /// changes nothing; a real change is refused while any member holds an active original booking,
    /// because their booking appointments were created from the old snapshot (FR-1.5).
    /// </summary>
    /// <param name="appointmentTypeIds">The appointment types every member should require.</param>
    /// <param name="activeAppointmentTypeIds">Every appointment type currently active.</param>
    /// <param name="blockingMembers">Members holding an active original booking.</param>
    /// <returns>Whether the mapping actually changed.</returns>
    public bool ReplaceRequirements(
        IEnumerable<Guid> appointmentTypeIds,
        IReadOnlyCollection<Guid> activeAppointmentTypeIds,
        int blockingMembers)
    {
        ArgumentNullException.ThrowIfNull(activeAppointmentTypeIds);
        Guard.Against(blockingMembers < 0, "blockingMembers must not be negative.");

        var mapping = ValidatedMapping(appointmentTypeIds, activeAppointmentTypeIds);
        if (mapping.SequenceEqual(RequiredAppointmentTypeIds))
        {
            return false;
        }

        if (blockingMembers > 0)
        {
            throw new ReferenceDataInUseException(
                "The requirement set cannot change while members hold an active booking.",
                new Dictionary<string, int> { ["blockingMembers"] = blockingMembers });
        }

        _requirements.Clear();
        foreach (var appointmentTypeId in mapping)
        {
            _requirements.Add(AttendeeGroupRequirement.For(Id, appointmentTypeId));
        }

        Version++;
        return true;
    }

    /// <summary>Deactivates the group, refusing while it still has members (FR-1.6).</summary>
    /// <param name="memberCount">Attendees currently assigned to the group.</param>
    public void Deactivate(int memberCount)
    {
        Guard.Against(memberCount < 0, "memberCount must not be negative.");

        if (memberCount > 0)
        {
            throw new ReferenceDataInUseException(
                "The attendee group cannot be deactivated while it has members.",
                new Dictionary<string, int> { ["members"] = memberCount });
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the group to use as assignment authority.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    private static string BoundedName(string? name)
    {
        var displayName = Guard.NotBlank(name, "name");
        Guard.Against(
            displayName.Length > MaximumNameLength,
            $"name must be at most {MaximumNameLength} characters.");
        return displayName;
    }

    private static List<Guid> ValidatedMapping(
        IEnumerable<Guid> appointmentTypeIds, IReadOnlyCollection<Guid> activeAppointmentTypeIds)
    {
        var mapping = appointmentTypeIds.ToList();

        Guard.Against(mapping.Count == 0, "An attendee group must map at least one appointment type.");
        Guard.Against(
            mapping.Distinct().Count() != mapping.Count,
            "An attendee group cannot map the same appointment type twice.");
        Guard.Against(
            mapping.Any(id => !activeAppointmentTypeIds.Contains(id)),
            "An attendee group can only map active appointment types.");

        return [.. mapping.Order()];
    }
}
`````

## before — src/EventBooking.Domain/Common/DomainException.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Domain/Common/DomainException.cs","beforeSha":"439588f11a0efea10ce68d240d76f21b8c1473e71648182e7c9a688709e818db","afterSha":"806bf736df171a78d16246ab19051f84c4896b0a213f6a6be70e74da264f6af3","side":"before","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Common;

/// <summary>The single failure mode of the domain layer: an invariant was violated.</summary>
public sealed class DomainException : Exception
{
    /// <summary>Defines domain exception for the current use case.</summary>
    /// <param name="message">The message.</param>
    public DomainException(string message) : base(message)
    {
    }
}
`````

## after — src/EventBooking.Domain/Common/DomainException.cs — 1/1

<!-- retirement-file: {"id":4,"file":"src/EventBooking.Domain/Common/DomainException.cs","beforeSha":"439588f11a0efea10ce68d240d76f21b8c1473e71648182e7c9a688709e818db","afterSha":"806bf736df171a78d16246ab19051f84c4896b0a213f6a6be70e74da264f6af3","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Common;

/// <summary>
/// The failure mode of the domain layer: an invariant was violated. Reference-data refusals that
/// must name what is blocking them derive from this rather than inventing a second failure mode.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Creates a refusal carrying the rule that was broken.</summary>
    /// <param name="message">The message.</param>
    public DomainException(string message) : base(message)
    {
    }
}
`````

## after — src/EventBooking.Domain/Common/ReferenceDataCode.cs — 1/1

<!-- retirement-file: {"id":5,"file":"src/EventBooking.Domain/Common/ReferenceDataCode.cs","beforeSha":null,"afterSha":"4c3f3000698682ad349701a6922e44478bb33851ec1a74cc8cd482b61aece56f","side":"after","part":1,"parts":1} -->

`````csharp
using System.Text.RegularExpressions;

namespace EventBooking.Domain.Common;

/// <summary>
/// The canonical form of a reference-data code. Codes are accepted case-insensitively at input
/// boundaries and stored uppercase, because they are the stable identifiers in CSV files, MCP
/// tools and email ordering.
/// </summary>
public static partial class ReferenceDataCode
{
    /// <summary>Normalises and validates a code, or refuses it.</summary>
    /// <param name="value">The supplied code, in any case, with or without surrounding space.</param>
    /// <param name="maximumLength">The longest code this kind of reference data allows.</param>
    /// <param name="field">The field name to use in a refusal message.</param>
    public static string Parse(string? value, int maximumLength, string field)
    {
        var canonical = (value ?? string.Empty).Trim().ToUpperInvariant();

        Guard.Against(
            !CanonicalExpression().IsMatch(canonical),
            $"{field} must be canonical uppercase snake case.");
        Guard.Against(
            canonical.Length > maximumLength,
            $"{field} must be at most {maximumLength} characters.");

        return canonical;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalExpression();
}
`````

## after — src/EventBooking.Domain/Common/ReferenceDataInUseException.cs — 1/1

<!-- retirement-file: {"id":6,"file":"src/EventBooking.Domain/Common/ReferenceDataInUseException.cs","beforeSha":null,"afterSha":"709e21806e684bec35c8ecda942bbbe91ac3a49e2d722deee1cfbdd88562fac6","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Common;

/// <summary>
/// Reference data is never hard-deleted, and never changed out from under live use. This refusal
/// names what is blocking, and how much of it, so a screen can say "3 open proposals" rather than
/// "not allowed" (FR-1.2, FR-1.5, FR-1.6).
/// </summary>
public sealed class ReferenceDataInUseException : DomainException
{
    /// <summary>Creates a refusal carrying its blocking counts.</summary>
    /// <param name="message">What was refused.</param>
    /// <param name="blocking">Each kind of live use, with its count.</param>
    public ReferenceDataInUseException(string message, IReadOnlyDictionary<string, int> blocking)
        : base(message) => Blocking = blocking;

    /// <summary>The live uses that blocked the change, keyed by kind.</summary>
    public IReadOnlyDictionary<string, int> Blocking { get; }
}
`````

## after — src/EventBooking.Domain/Locations/Location.cs — 1/1

<!-- retirement-file: {"id":7,"file":"src/EventBooking.Domain/Locations/Location.cs","beforeSha":null,"afterSha":"1016124f95f2fa3a6c6d77de149fd0c915a54cd701ac991ebb90371b561d3e0a","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;
using EventBooking.Domain.Time;

namespace EventBooking.Domain.Locations;

/// <summary>
/// An Admin-managed site where events happen, with its own address and IANA time zone. Every
/// window at the site is read in that zone, so the zone cannot move while anything is scheduled.
/// </summary>
public sealed class Location
{
    /// <summary>The longest code a location may have (design 08).</summary>
    public const int MaximumCodeLength = 50;

    /// <summary>The longest name a location may have (design 08).</summary>
    public const int MaximumNameLength = 100;

    /// <summary>The longest address a location may have (design 08).</summary>
    public const int MaximumAddressLength = 500;

    private Location()
    {
        Code = string.Empty;
        Name = string.Empty;
        Address = string.Empty;
        TimeZoneId = string.Empty;
    }

    /// <summary>The stable reference-data identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The immutable canonical uppercase snake-case code.</summary>
    public string Code { get; private set; }

    /// <summary>The staff-facing display name.</summary>
    public string Name { get; private set; }

    /// <summary>The postal address shown to attendees.</summary>
    public string Address { get; private set; }

    /// <summary>The IANA zone every window at this location is read in.</summary>
    public string TimeZoneId { get; private set; }

    /// <summary>Whether new proposals may name this location.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Creates an active location.</summary>
    /// <param name="id">The new identifier.</param>
    /// <param name="code">The code, in any case.</param>
    /// <param name="name">The display name.</param>
    /// <param name="address">The postal address.</param>
    /// <param name="timeZoneId">The IANA zone identifier.</param>
    /// <param name="zones">The zone abstraction that says whether the zone exists.</param>
    public static Location Create(
        Guid id, string? code, string? name, string? address, string? timeZoneId, IEventWindowZones zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        Guard.Against(id == Guid.Empty, "id must not be empty.");

        var canonicalCode = ReferenceDataCode.Parse(code, MaximumCodeLength, "code");
        var displayName = Bounded(name, MaximumNameLength, "name");
        var postalAddress = Bounded(address, MaximumAddressLength, "address");
        var zone = KnownZone(timeZoneId, zones);

        return new Location
        {
            Id = id,
            Code = canonicalCode,
            Name = displayName,
            Address = postalAddress,
            TimeZoneId = zone,
            IsActive = true,
            Version = 1,
        };
    }

    /// <summary>Renames the location. Always allowed.</summary>
    /// <param name="name">The new display name.</param>
    public void Rename(string? name)
    {
        Name = Bounded(name, MaximumNameLength, "name");
        Version++;
    }

    /// <summary>Changes the postal address. Always allowed.</summary>
    /// <param name="address">The new address.</param>
    public void ChangeAddress(string? address)
    {
        Address = Bounded(address, MaximumAddressLength, "address");
        Version++;
    }

    /// <summary>
    /// Moves the location to another zone. Refused while anything is scheduled, because it would
    /// silently move the wall-clock time of every window already agreed (FR-1.2).
    /// </summary>
    /// <param name="timeZoneId">The new IANA zone identifier.</param>
    /// <param name="zones">The zone abstraction that says whether the zone exists.</param>
    /// <param name="usage">What the location currently carries.</param>
    public void ChangeTimeZone(string? timeZoneId, IEventWindowZones zones, LocationUsage usage)
    {
        ArgumentNullException.ThrowIfNull(zones);
        var zone = KnownZone(timeZoneId, zones);

        if (zone == TimeZoneId)
        {
            return;
        }

        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The time zone cannot change while the location has open proposals or future events.",
                usage.AsBlocking());
        }

        TimeZoneId = zone;
        Version++;
    }

    /// <summary>Deactivates the location, refusing while it is in live use (FR-1.6).</summary>
    /// <param name="usage">What the location currently carries.</param>
    public void Deactivate(LocationUsage usage)
    {
        if (usage.Any)
        {
            throw new ReferenceDataInUseException(
                "The location cannot be deactivated while it has open proposals or future events.",
                usage.AsBlocking());
        }

        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Version++;
    }

    /// <summary>Returns the location to use.</summary>
    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Version++;
    }

    private static string Bounded(string? value, int maximumLength, string field)
    {
        var trimmed = Guard.NotBlank(value, field);
        Guard.Against(trimmed.Length > maximumLength, $"{field} must be at most {maximumLength} characters.");
        return trimmed;
    }

    private static string KnownZone(string? timeZoneId, IEventWindowZones zones)
    {
        var zone = Guard.NotBlank(timeZoneId, "timeZoneId");
        Guard.Against(!zones.IsKnownZone(zone), $"timeZoneId '{zone}' is not a known IANA time zone.");
        return zone;
    }
}
`````

## after — src/EventBooking.Domain/Locations/LocationUsage.cs — 1/1

<!-- retirement-file: {"id":8,"file":"src/EventBooking.Domain/Locations/LocationUsage.cs","beforeSha":null,"afterSha":"ea7abc41f04c81262417ce9c88e852c6aa646b9de208119905f06454711edfa8","side":"after","part":1,"parts":1} -->

`````csharp
namespace EventBooking.Domain.Locations;

/// <summary>
/// How much live scheduling a location carries. The caller counts; the aggregate decides.
/// </summary>
/// <param name="OpenProposals">Open proposals hosted at the location.</param>
/// <param name="FutureEvents">Active events at the location whose window has not passed.</param>
public readonly record struct LocationUsage(int OpenProposals, int FutureEvents)
{
    /// <summary>A location nothing is scheduled against.</summary>
    public static LocationUsage None => new(0, 0);

    /// <summary>Whether anything live depends on this location.</summary>
    public bool Any => OpenProposals > 0 || FutureEvents > 0;

    /// <summary>The blocking counts, for a refusal a screen can render.</summary>
    public IReadOnlyDictionary<string, int> AsBlocking() =>
        new Dictionary<string, int>
        {
            ["openProposals"] = OpenProposals,
            ["futureEvents"] = FutureEvents,
        };
}
`````

## before — src/EventBooking.Domain/Settings/SystemSettings.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Domain/Settings/SystemSettings.cs","beforeSha":"6a541e191a55637d3d93e8d15d16c1ff4025e0a1efe8663591e04e246401c472","afterSha":"a8320c6be980f756d4f82b3559869c580a50c58594f2c1b7a5c8deda038d6e1d","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Settings;

/// <summary>Admin-configurable settings. Exactly one row exists, with a constant identifier.</summary>
public sealed class SystemSettings
{
    /// <summary>Defines singleton id for the current use case.</summary>
    public const int SingletonId = 1;

    private SystemSettings()
    {
    }

    /// <summary>Defines id for the current use case.</summary>
    public int Id { get; private set; } = SingletonId;

    /// <summary>Defines invite expiry days for the current use case.</summary>
    public int InviteExpiryDays { get; private set; }

    /// <summary>Defines max auto retry count for the current use case.</summary>
    public int MaxAutoRetryCount { get; private set; }

    /// <summary>Defines create default for the current use case.</summary>
    public static SystemSettings CreateDefault() =>
        new() { Id = SingletonId, InviteExpiryDays = 4, MaxAutoRetryCount = 2 };

    /// <summary>Defines update for the current use case.</summary>
    /// <param name="inviteExpiryDays">The invite expiry days.</param>
    /// <param name="maxAutoRetryCount">The max auto retry count.</param>
    public void Update(int inviteExpiryDays, int maxAutoRetryCount)
    {
        // Validate both before mutating either, so a rejected update changes nothing.
        var days = Guard.Positive(inviteExpiryDays, "inviteExpiryDays");
        var retries = Guard.NotNegative(maxAutoRetryCount, "maxAutoRetryCount");

        InviteExpiryDays = days;
        MaxAutoRetryCount = retries;
    }
}
`````

## after — src/EventBooking.Domain/Settings/SystemSettings.cs — 1/1

<!-- retirement-file: {"id":9,"file":"src/EventBooking.Domain/Settings/SystemSettings.cs","beforeSha":"6a541e191a55637d3d93e8d15d16c1ff4025e0a1efe8663591e04e246401c472","afterSha":"a8320c6be980f756d4f82b3559869c580a50c58594f2c1b7a5c8deda038d6e1d","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Common;

namespace EventBooking.Domain.Settings;

/// <summary>Admin-configurable settings. Exactly one row exists, with a constant identifier.</summary>
public sealed class SystemSettings
{
    /// <summary>Defines singleton id for the current use case.</summary>
    public const int SingletonId = 1;

    private SystemSettings()
    {
    }

    /// <summary>Defines id for the current use case.</summary>
    public int Id { get; private set; } = SingletonId;

    /// <summary>Defines invite expiry days for the current use case.</summary>
    public int InviteExpiryDays { get; private set; }

    /// <summary>Defines max auto retry count for the current use case.</summary>
    public int MaxAutoRetryCount { get; private set; }

    /// <summary>How many event options an invitation offers (design 07: 1 to 5, default 3).</summary>
    public int InviteOptionCount { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public long Version { get; private set; }

    /// <summary>Defines create default for the current use case.</summary>
    public static SystemSettings CreateDefault() =>
        new() { Id = SingletonId, InviteExpiryDays = 7, MaxAutoRetryCount = 2, InviteOptionCount = 3, Version = 1 };

    /// <summary>Applies new settings, or refuses them all (FR-1.9; design 08 bounds).</summary>
    /// <param name="inviteExpiryDays">How long an invitation stays usable: 1 to 60.</param>
    /// <param name="maxAutoRetryCount">How many automatic re-issues an invitation gets: 0 to 10.</param>
    /// <param name="inviteOptionCount">How many options an invitation offers: 1 to 5.</param>
    public void Update(int inviteExpiryDays, int maxAutoRetryCount, int inviteOptionCount)
    {
        // Validate everything before mutating anything, so a rejected update changes nothing.
        Guard.Against(
            inviteExpiryDays is < 1 or > 60, "inviteExpiryDays must be between 1 and 60.");
        Guard.Against(
            maxAutoRetryCount is < 0 or > 10, "maxAutoRetryCount must be between 0 and 10.");
        Guard.Against(
            inviteOptionCount is < 1 or > 5, "inviteOptionCount must be between 1 and 5.");

        InviteExpiryDays = inviteExpiryDays;
        MaxAutoRetryCount = maxAutoRetryCount;
        InviteOptionCount = inviteOptionCount;
        Version++;
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs","beforeSha":"5ddad5a3cac85b467e9aa59fe64ded6e3bc55a8845da10a95deae02e9e951b0a","afterSha":"793677bf0a08eec563845e04457b98a5817ed5a28c93907b8bcbbd5c06d5a383","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AppointmentTypeConfiguration : IEntityTypeConfiguration<AppointmentType>
{
    public void Configure(EntityTypeBuilder<AppointmentType> builder)
    {
        builder.ToTable("appointment_type");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(8).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();

        // The 3 types are fixed by the spec, so they are seeded rather than created at run time.
        builder.HasData(
            new { Id = AppointmentTypeIds.DrugAndAlcoholTesting, Code = "DAT", Name = "Drug & Alcohol Testing" },
            new { Id = AppointmentTypeIds.MedicalCheckUp, Code = "MED", Name = "Medical Check-up" },
            new { Id = AppointmentTypeIds.UniformFitting, Code = "UNI", Name = "Uniform Fitting" });
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs — 1/1

<!-- retirement-file: {"id":10,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs","beforeSha":"5ddad5a3cac85b467e9aa59fe64ded6e3bc55a8845da10a95deae02e9e951b0a","afterSha":"793677bf0a08eec563845e04457b98a5817ed5a28c93907b8bcbbd5c06d5a383","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AppointmentTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class AppointmentTypeConfiguration : IEntityTypeConfiguration<AppointmentType>
{
    public void Configure(EntityTypeBuilder<AppointmentType> builder)
    {
        builder.ToTable("appointment_type");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(8).IsRequired();
        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active");
        builder.Property(t => t.Version).HasColumnName("version").IsConcurrencyToken();

        builder.HasIndex(t => t.Code).IsUnique();

        // Types are Admin-managed from Task 5. These three rows are the predecessor's seeded set,
        // kept until Phase 3 moves seeding onto the managed create path.
        builder.HasData(
            new { Id = AppointmentTypeIds.DrugAndAlcoholTesting, Code = "DAT", Name = "Drug & Alcohol Testing", IsActive = true, Version = 1L },
            new { Id = AppointmentTypeIds.MedicalCheckUp, Code = "MED", Name = "Medical Check-up", IsActive = true, Version = 1L },
            new { Id = AppointmentTypeIds.UniformFitting, Code = "UNI", Name = "Uniform Fitting", IsActive = true, Version = 1L });
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs","beforeSha":"2b176f65240554ca3b3557d18ccb9537b5528f4d2d2e01fe8bb25eb0837f53c2","afterSha":"1176d7a3e6a57146ebebf15ce3260d866547e32368afb7c13dda19c287537a96","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the change-controlled Attendee Group reference table.</summary>
public sealed class AttendeeGroupConfiguration : IEntityTypeConfiguration<AttendeeGroup>
{
    /// <summary>Configures the attendee group table, constraints, and mapping collection.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroup> builder)
    {
        builder.ToTable("attendee_group", table =>
        {
            table.HasCheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");
            table.HasCheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(group => group.IsActive).HasColumnName("is_active");

        builder.Ignore(group => group.RequiredAppointmentTypeIds);

        builder
            .HasMany(group => group.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.Code).IsUnique();
    }
}
`````

## after — src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs — 1/1

<!-- retirement-file: {"id":11,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs","beforeSha":"2b176f65240554ca3b3557d18ccb9537b5528f4d2d2e01fe8bb25eb0837f53c2","afterSha":"1176d7a3e6a57146ebebf15ce3260d866547e32368afb7c13dda19c287537a96","side":"after","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.AttendeeGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

/// <summary>Maps the change-controlled Attendee Group reference table.</summary>
public sealed class AttendeeGroupConfiguration : IEntityTypeConfiguration<AttendeeGroup>
{
    /// <summary>Configures the attendee group table, constraints, and mapping collection.</summary>
    public void Configure(EntityTypeBuilder<AttendeeGroup> builder)
    {
        builder.ToTable("attendee_group", table =>
        {
            table.HasCheckConstraint("ck_attendee_group_code_nonblank", "code <> ''");
            table.HasCheckConstraint("ck_attendee_group_name_nonblank", "name <> ''");
        });
        builder.HasKey(group => group.Id);

        builder.Property(group => group.Id).HasColumnName("id");
        builder.Property(group => group.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(group => group.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(group => group.IsActive).HasColumnName("is_active");
        builder.Property(group => group.Version).HasColumnName("version").IsConcurrencyToken();

        builder.Ignore(group => group.RequiredAppointmentTypeIds);

        builder
            .HasMany(group => group.Requirements)
            .WithOne()
            .HasForeignKey(requirement => requirement.AttendeeGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(group => group.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(group => group.Code).IsUnique();
    }
}
`````

## before — src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs — 1/1

<!-- retirement-file: {"id":12,"file":"src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs","beforeSha":"438722e621190d431d430512c823ee9d72a28ba7e36542ddb6424123184e2cbf","afterSha":"a83e87860aa2ae911dab768c65070096f1aa4fb655afb15ca74c659f40218794","side":"before","part":1,"parts":1} -->

`````csharp
using EventBooking.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventBooking.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.InviteExpiryDays).HasColumnName("invite_expiry_days");
        builder.Property(s => s.MaxAutoRetryCount).HasColumnName("max_auto_retry_count");

        builder.HasData(
            new { Id = Domain.Settings.SystemSettings.SingletonId, InviteExpiryDays = 4, MaxAutoRetryCount = 2 });
    }
}
`````
