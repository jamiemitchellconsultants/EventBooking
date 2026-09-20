# 00a — Import the complete port (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

This cross-layer task establishes the buildable domain, application, infrastructure, REST, MCP, web and seed baseline. It precedes vocabulary normalization in Task 2. Source volumes are numbered continuation sections of this task, not separate tasks or commits.

> Use superpowers:executing-plans for this task. Do not run the later domain-generalisation tasks yet.

**Goal:** Reproduce the verified predecessor behaviour without cloud-provider projects.

**Architecture:** Eight production projects share the application layer; SMTP is registered inside Infrastructure. All source and tests are embedded here; no predecessor checkout is required.

**Tech Stack:** .NET 10, EF Core/Npgsql, xUnit, Testcontainers, bUnit, Blazor WebAssembly, generic OIDC and SMTP; versions are pinned in the supplied central package file.

**Spec:** [Decision record](../superpowers/specs/2026-09-19-eventbooking-design.md), D5/D8/D9; [master plan](../superpowers/plans/2026-09-19-eventbooking-implementation.md), Task 1.

## Global constraints

Target net10.0 with nullable enabled and warnings as errors. Public Domain/Application APIs generate XML documentation. Do not add cloud SDKs, skip Docker tests, change capacity behaviour, or push to main. Task 1's predecessor names are a user-approved intermediate exception; Task 2 must remove them before the Phase 0 PR. This step does not claim that the three-type predecessor already implements the generalised domain.

## Review focus

Provider removal must cover package references and the loaded application dependency graph: the port architecture tests check both. Missing browser assets must not be silently omitted: the extractor checks every embedded checksum. Existing user changes must survive: extraction rejects unexpected target contents. Documentation warnings must not be suppressed: the full build gate treats warnings as errors. Database tests must actually execute: all seven project result counts must be nonzero and skipped counts zero.

### Task 1: Import the solution under EventBooking names, without AWS

**Files:**

- Create: .dockerignore
- Create: deploy/home-lab/keycloak/eventbooking-realm.json
- Create: deploy/home-lab/README.md
- Create: deploy/keycloak/realm-export.json
- Create: Directory.Build.props
- Create: Directory.Packages.props
- Create: docs/demo-runbook.md
- Create: docs/user-guides/admin-guide.md
- Create: docs/user-guides/appointment-staff-guide.md
- Create: docs/user-guides/candidate-guide.md
- Create: docs/user-guides/coordinator-guide.md
- Create: docs/user-guides/manager-guide.md
- Create: docs/user-guides/README.md
- Create: EventBooking.sln
- Modify: README.md
- Create: src/EventBooking.Api.Auth/EventBooking.Api.Auth.csproj
- Create: src/EventBooking.Api.Auth/LocalAuthenticationExtensions.cs
- Create: src/EventBooking.Api/appsettings.json
- Create: src/EventBooking.Api/appsettings.Local.json
- Create: src/EventBooking.Api/Auth/AuthenticationExtensions.cs
- Create: src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs
- Create: src/EventBooking.Api/Auth/ICallerAccessor.cs
- Create: src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs
- Create: src/EventBooking.Api/Auth/StaffIdentityRecorder.cs
- Create: src/EventBooking.Api/Auth/StaffRequirement.cs
- Create: src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs
- Create: src/EventBooking.Api/Contracts/ApiLink.cs
- Create: src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs
- Create: src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs
- Create: src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs
- Create: src/EventBooking.Api/Dockerfile
- Create: src/EventBooking.Api/Endpoints/AdminEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/AuditEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/AuditInputParser.cs
- Create: src/EventBooking.Api/Endpoints/BookingEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/CandidateEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/DashboardEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/MeEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/ResultResponses.cs
- Create: src/EventBooking.Api/Endpoints/SlotEndpoints.cs
- Create: src/EventBooking.Api/Endpoints/StaffAccessEndpoints.cs
- Create: src/EventBooking.Api/EventBooking.Api.csproj
- Create: src/EventBooking.Api/EventBookingConfiguration.cs
- Create: src/EventBooking.Api/InviteSweepService.cs
- Create: src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs
- Create: src/EventBooking.Api/OpenApi/EndpointMetadataExtensions.cs
- Create: src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs
- Create: src/EventBooking.Api/Program.cs
- Create: src/EventBooking.Api/Properties/launchSettings.json
- Create: src/EventBooking.Application/Abstractions/IAppointmentTypeRepository.cs
- Create: src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs
- Create: src/EventBooking.Application/Abstractions/IAuditLogger.cs
- Create: src/EventBooking.Application/Abstractions/IAuditQueries.cs
- Create: src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs
- Create: src/EventBooking.Application/Abstractions/IBookingRepository.cs
- Create: src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs
- Create: src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs
- Create: src/EventBooking.Application/Abstractions/ICandidateRepository.cs
- Create: src/EventBooking.Application/Abstractions/IClock.cs
- Create: src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs
- Create: src/EventBooking.Application/Abstractions/IDashboardQueries.cs
- Create: src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs
- Create: src/EventBooking.Application/Abstractions/IEmailSender.cs
- Create: src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs
- Create: src/EventBooking.Application/Abstractions/IInviteRepository.cs
- Create: src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs
- Create: src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs
- Create: src/EventBooking.Application/Abstractions/IStaffAccessProfileRepository.cs
- Create: src/EventBooking.Application/Abstractions/IStaffIdentityRepository.cs
- Create: src/EventBooking.Application/Abstractions/ISystemSettingsRepository.cs
- Create: src/EventBooking.Application/Abstractions/ITokenService.cs
- Create: src/EventBooking.Application/Abstractions/IUnitOfWork.cs
- Create: src/EventBooking.Application/Access/MeHandler.cs
- Create: src/EventBooking.Application/Access/StaffAccessAuthorizer.cs
- Create: src/EventBooking.Application/Access/StaffAccessHandler.cs
- Create: src/EventBooking.Application/Access/StaffCapability.cs
- Create: src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs
- Create: src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs
- Create: src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs
- Create: src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs
- Create: src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs
- Create: src/EventBooking.Application/Appointments/RecoveryBookingOutcomeCoordinator.cs
- Create: src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs
- Create: src/EventBooking.Application/Bookings/BookingCanceller.cs
- Create: src/EventBooking.Application/Bookings/CancelBookingHandler.cs
- Create: src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs
- Create: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Create: src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs
- Create: src/EventBooking.Application/Bookings/ViewBookingHandler.cs
- Create: src/EventBooking.Application/Bookings/ViewInviteHandler.cs
- Create: src/EventBooking.Application/Candidates/CandidateCsvParser.cs
- Create: src/EventBooking.Application/Candidates/CandidateReadiness.cs
- Create: src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs
- Create: src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs
- Create: src/EventBooking.Application/Candidates/EmployeeGroupModels.cs
- Create: src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs
- Create: src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs
- Create: src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs
- Create: src/EventBooking.Application/Candidates/ListCandidatesHandler.cs
- Create: src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs
- Create: src/EventBooking.Application/Candidates/SaveCandidateHandler.cs
- Create: src/EventBooking.Application/Common/Error.cs
- Create: src/EventBooking.Application/Common/Result.cs
- Create: src/EventBooking.Application/Common/UniqueConstraintViolationException.cs
- Create: src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs
- Create: src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs
- Create: src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs
- Create: src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs
- Create: src/EventBooking.Application/DependencyInjection.cs
- Create: src/EventBooking.Application/EventBooking.Application.csproj
- Create: src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs
- Create: src/EventBooking.Application/Invites/EligibleSlotFinder.cs
- Create: src/EventBooking.Application/Invites/ExpireInvitesHandler.cs
- Create: src/EventBooking.Application/Invites/InviteIssuer.cs
- Create: src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs
- Create: src/EventBooking.Application/Invites/StartRecoveryHandler.cs
- Create: src/EventBooking.Application/Invites/TriggerInviteHandler.cs
- Create: src/EventBooking.Application/Notifications/CandidateEmailComposer.cs
- Create: src/EventBooking.Application/Notifications/CandidatePortalOptions.cs
- Create: src/EventBooking.Application/Notifications/EmailDeliveryService.cs
- Create: src/EventBooking.Application/Notifications/RetryEmailHandler.cs
- Create: src/EventBooking.Application/Properties/AssemblyInfo.cs
- Create: src/EventBooking.Application/Settings/AdminSettingsHandler.cs
- Create: src/EventBooking.Application/Slots/AcceptProposalHandler.cs
- Create: src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs
- Create: src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs
- Create: src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs
- Create: src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs
- Create: src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs
- Create: src/EventBooking.Application/Slots/ProposeSlotHandler.cs
- Create: src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs
- Create: src/EventBooking.Application/Slots/WithdrawProposalHandler.cs
- Create: src/EventBooking.Domain/Access/Role.cs
- Create: src/EventBooking.Domain/Access/StaffAccessProfile.cs
- Create: src/EventBooking.Domain/Access/StaffId.cs
- Create: src/EventBooking.Domain/Access/StaffIdentity.cs
- Create: src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs
- Create: src/EventBooking.Domain/AppointmentTypes/AppointmentTypeIds.cs
- Create: src/EventBooking.Domain/Audit/ActorType.cs
- Create: src/EventBooking.Domain/Audit/AuditAction.cs
- Create: src/EventBooking.Domain/Audit/AuditEntityTypes.cs
- Create: src/EventBooking.Domain/Audit/AuditLog.cs
- Create: src/EventBooking.Domain/Bookings/Booking.cs
- Create: src/EventBooking.Domain/Bookings/BookingAppointment.cs
- Create: src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs
- Create: src/EventBooking.Domain/Bookings/BookingStatus.cs
- Create: src/EventBooking.Domain/Candidates/Candidate.cs
- Create: src/EventBooking.Domain/Candidates/CandidateRequirement.cs
- Create: src/EventBooking.Domain/Candidates/CandidateStatus.cs
- Create: src/EventBooking.Domain/Common/DomainException.cs
- Create: src/EventBooking.Domain/Common/Guard.cs
- Create: src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs
- Create: src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs
- Create: src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs
- Create: src/EventBooking.Domain/EventBooking.Domain.csproj
- Create: src/EventBooking.Domain/Invites/Invite.cs
- Create: src/EventBooking.Domain/Invites/InviteOption.cs
- Create: src/EventBooking.Domain/Invites/InviteRequirement.cs
- Create: src/EventBooking.Domain/Invites/InviteStatus.cs
- Create: src/EventBooking.Domain/Notifications/EmailLog.cs
- Create: src/EventBooking.Domain/Notifications/EmailStatus.cs
- Create: src/EventBooking.Domain/Notifications/EmailTemplate.cs
- Create: src/EventBooking.Domain/Settings/SystemSettings.cs
- Create: src/EventBooking.Domain/Slots/ConfirmedSlot.cs
- Create: src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs
- Create: src/EventBooking.Domain/Slots/ProposalAcceptance.cs
- Create: src/EventBooking.Domain/Slots/SlotCapacity.cs
- Create: src/EventBooking.Domain/Slots/SlotProposal.cs
- Create: src/EventBooking.Domain/Slots/SlotProposalStatus.cs
- Create: src/EventBooking.Domain/Slots/SlotWindow.cs
- Create: src/EventBooking.Infrastructure/Audit/EfAuditLogger.cs
- Create: src/EventBooking.Infrastructure/DependencyInjection.cs
- Create: src/EventBooking.Infrastructure/Email/EmailOptions.cs
- Create: src/EventBooking.Infrastructure/Email/IEmailTransport.cs
- Create: src/EventBooking.Infrastructure/Email/LocalInfrastructureExtensions.cs
- Create: src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs
- Create: src/EventBooking.Infrastructure/Email/SmtpEmailTransport.cs
- Create: src/EventBooking.Infrastructure/Email/SmtpOptions.cs
- Create: src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/BookingAppointmentConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/InviteRequirementConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/StaffAccessProfileConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs
- Create: src/EventBooking.Infrastructure/Persistence/DesignTimeDbContextFactory.cs
- Create: src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.Designer.cs
- Create: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/AppointmentWorkspaceQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/AuditQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/CandidateBookingQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/CandidateReadinessQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs
- Create: src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs
- Create: src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs
- Create: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs
- Create: src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs
- Create: src/EventBooking.Infrastructure/Persistence/Repositories/StaffAccessProfileRepository.cs
- Create: src/EventBooking.Infrastructure/Persistence/Repositories/StaffIdentityRepository.cs
- Create: src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs
- Create: src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs
- Create: src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs
- Create: src/EventBooking.Infrastructure/Time/SystemClock.cs
- Create: src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs
- Create: src/EventBooking.Infrastructure/Tokens/TokenOptions.cs
- Create: src/EventBooking.Mcp/appsettings.json
- Create: src/EventBooking.Mcp/appsettings.Local.json
- Create: src/EventBooking.Mcp/Dockerfile
- Create: src/EventBooking.Mcp/EventBooking.Mcp.csproj
- Create: src/EventBooking.Mcp/Program.cs
- Create: src/EventBooking.Mcp/Properties/launchSettings.json
- Create: src/EventBooking.Mcp/Tools/AdminTools.cs
- Create: src/EventBooking.Mcp/Tools/AdminToolViews.cs
- Create: src/EventBooking.Mcp/Tools/CandidateTools.cs
- Create: src/EventBooking.Mcp/Tools/McpErrors.cs
- Create: src/EventBooking.Mcp/Tools/OperationsTools.cs
- Create: src/EventBooking.Mcp/Tools/SlotTools.cs
- Create: src/EventBooking.SeedData/demo-seed.json
- Create: src/EventBooking.SeedData/DemoEmailOptions.cs
- Create: src/EventBooking.SeedData/DemoInvitationSeeder.cs
- Create: src/EventBooking.SeedData/DemoSeeder.cs
- Create: src/EventBooking.SeedData/DemoSeedSpec.cs
- Create: src/EventBooking.SeedData/EventBooking.SeedData.csproj
- Create: src/EventBooking.SeedData/KeycloakSeeder.cs
- Create: src/EventBooking.SeedData/KeycloakSeedOptions.cs
- Create: src/EventBooking.SeedData/KeycloakSeedStep.cs
- Create: src/EventBooking.SeedData/Program.cs
- Create: src/EventBooking.Web/_Imports.razor
- Create: src/EventBooking.Web/.npmrc
- Create: src/EventBooking.Web/App.razor
- Create: src/EventBooking.Web/Dockerfile
- Create: src/EventBooking.Web/EventBooking.Web.csproj
- Create: src/EventBooking.Web/Layout/CandidateLayout.razor
- Create: src/EventBooking.Web/Layout/MainLayout.razor
- Create: src/EventBooking.Web/Layout/RedirectToLogin.razor
- Create: src/EventBooking.Web/nginx.conf
- Create: src/EventBooking.Web/Pages/Appointments.razor
- Create: src/EventBooking.Web/Pages/Appointments.razor.css
- Create: src/EventBooking.Web/Pages/Audit.razor
- Create: src/EventBooking.Web/Pages/Audit.razor.css
- Create: src/EventBooking.Web/Pages/Authentication.razor
- Create: src/EventBooking.Web/Pages/Book.razor
- Create: src/EventBooking.Web/Pages/Book.razor.css
- Create: src/EventBooking.Web/Pages/Candidates.razor
- Create: src/EventBooking.Web/Pages/Candidates.razor.css
- Create: src/EventBooking.Web/Pages/ConfirmedSlots.razor
- Create: src/EventBooking.Web/Pages/ConfirmedSlots.razor.css
- Create: src/EventBooking.Web/Pages/Dashboards.razor
- Create: src/EventBooking.Web/Pages/Dashboards.razor.css
- Create: src/EventBooking.Web/Pages/Help.razor
- Create: src/EventBooking.Web/Pages/Home.razor
- Create: src/EventBooking.Web/Pages/ManageBooking.razor
- Create: src/EventBooking.Web/Pages/ManageBooking.razor.css
- Create: src/EventBooking.Web/Pages/NotFound.razor
- Create: src/EventBooking.Web/Pages/Settings.razor
- Create: src/EventBooking.Web/Pages/Settings.razor.css
- Create: src/EventBooking.Web/Pages/Slots.razor
- Create: src/EventBooking.Web/Pages/Slots.razor.css
- Create: src/EventBooking.Web/Pages/StaffAccess.razor
- Create: src/EventBooking.Web/Pages/StaffAccess.razor.css
- Create: src/EventBooking.Web/Program.cs
- Create: src/EventBooking.Web/Properties/AssemblyInfo.cs
- Create: src/EventBooking.Web/Properties/launchSettings.json
- Create: src/EventBooking.Web/Services/AdminClient.cs
- Create: src/EventBooking.Web/Services/ApiCall.cs
- Create: src/EventBooking.Web/Services/ApiOutcome.cs
- Create: src/EventBooking.Web/Services/AppointmentsClient.cs
- Create: src/EventBooking.Web/Services/AuditClient.cs
- Create: src/EventBooking.Web/Services/BookingClient.cs
- Create: src/EventBooking.Web/Services/CandidatePresentation.cs
- Create: src/EventBooking.Web/Services/CandidatesClient.cs
- Create: src/EventBooking.Web/Services/ConfirmedSlotsClient.cs
- Create: src/EventBooking.Web/Services/DashboardsClient.cs
- Create: src/EventBooking.Web/Services/HeadOfficePageClock.cs
- Create: src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs
- Create: src/EventBooking.Web/Services/MeClient.cs
- Create: src/EventBooking.Web/Services/SlotsClient.cs
- Create: src/EventBooking.Web/Services/StaffAccessClient.cs
- Create: src/EventBooking.Web/Services/StaffNavigation.cs
- Create: src/EventBooking.Web/Services/UserGuideCatalog.cs
- Create: src/EventBooking.Web/Shared/AuditHistory.razor
- Create: src/EventBooking.Web/Shared/BrandMark.razor
- Create: src/EventBooking.Web/wwwroot/appsettings.Development.json
- Create: src/EventBooking.Web/wwwroot/appsettings.json
- Create: src/EventBooking.Web/wwwroot/css/app.css
- Create: src/EventBooking.Web/wwwroot/css/fonts.css
- Create: src/EventBooking.Web/wwwroot/favicon.png
- Create: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-bd.woff2
- Create: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-extlig.woff2
- Create: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-lt.woff2
- Create: src/EventBooking.Web/wwwroot/fonts/mylius-Modern-reg.woff2
- Create: src/EventBooking.Web/wwwroot/fonts/open-sans/open-sans-v15-latin-300.woff2
- Create: src/EventBooking.Web/wwwroot/fonts/open-sans/open-sans-v15-latin-700.woff2
- Create: src/EventBooking.Web/wwwroot/fonts/open-sans/open-sans-v15-latin-regular.woff2
- Create: src/EventBooking.Web/wwwroot/icon-192.png
- Create: src/EventBooking.Web/wwwroot/icons/alert.svg
- Create: src/EventBooking.Web/wwwroot/icons/delete.svg
- Create: src/EventBooking.Web/wwwroot/icons/edit.svg
- Create: src/EventBooking.Web/wwwroot/icons/search.svg
- Create: src/EventBooking.Web/wwwroot/icons/tick.svg
- Create: src/EventBooking.Web/wwwroot/index.html
- Create: src/EventBooking.Web/wwwroot/js/download.js
- Create: src/EventBooking.Web/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css
- Create: src/EventBooking.Web/wwwroot/speedmarque.png
- Test: tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs
- Test: tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs
- Test: tests/EventBooking.Api.Tests/ApiFactory.cs
- Test: tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/AuditEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs
- Test: tests/EventBooking.Api.Tests/BookingEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/CallerAccessorTests.cs
- Test: tests/EventBooking.Api.Tests/CallerIdentityTests.cs
- Test: tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/CandidateEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs
- Test: tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/CorsTests.cs
- Test: tests/EventBooking.Api.Tests/DashboardEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/DiscoveryDocumentationTests.cs
- Test: tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj
- Test: tests/EventBooking.Api.Tests/Fakes/RecordingEmailTransport.cs
- Test: tests/EventBooking.Api.Tests/HealthTests.cs
- Test: tests/EventBooking.Api.Tests/LocalAuthenticationExtensionsTests.cs
- Test: tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/MeEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/OpenApiContractTests.cs
- Test: tests/EventBooking.Api.Tests/OpenApiHostingTests.cs
- Test: tests/EventBooking.Api.Tests/PortArchitectureTests.cs
- Test: tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs
- Test: tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/ResultResponsesTests.cs
- Test: tests/EventBooking.Api.Tests/SlotEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/StaffAccessEndpointTests.cs
- Test: tests/EventBooking.Api.Tests/StaffHypermediaTests.cs
- Test: tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs
- Test: tests/EventBooking.Api.Tests/StaffRequirementHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs
- Test: tests/EventBooking.Application.Tests/Access/MeHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs
- Test: tests/EventBooking.Application.Tests/Access/StaffAccessHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Access/SyncStaffAccessProfileRolesHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs
- Test: tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/CancelCandidateBookingHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs
- Test: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Common/ResultTests.cs
- Test: tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs
- Test: tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/EventBooking.Application.Tests.csproj
- Test: tests/EventBooking.Application.Tests/Fakes/EmailDeliveryTestFactory.cs
- Test: tests/EventBooking.Application.Tests/Fakes/FakeClock.cs
- Test: tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs
- Test: tests/EventBooking.Application.Tests/Fakes/FakeTokenService.cs
- Test: tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs
- Test: tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs
- Test: tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs
- Test: tests/EventBooking.Application.Tests/Fakes/RecordingAuditLogger.cs
- Test: tests/EventBooking.Application.Tests/Fakes/RecordingEmailSender.cs
- Test: tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/RecoveryRequirementSelectorTests.cs
- Test: tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Notifications/CandidateEmailComposerTests.cs
- Test: tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs
- Test: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs
- Test: tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs
- Test: tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs
- Test: tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs
- Test: tests/EventBooking.Domain.Tests/Access/StaffAccessProfileTests.cs
- Test: tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs
- Test: tests/EventBooking.Domain.Tests/AppointmentTypes/AppointmentTypeTests.cs
- Test: tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs
- Test: tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs
- Test: tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentTests.cs
- Test: tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs
- Test: tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs
- Test: tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs
- Test: tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs
- Test: tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs
- Test: tests/EventBooking.Domain.Tests/Common/GuardTests.cs
- Test: tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs
- Test: tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj
- Test: tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs
- Test: tests/EventBooking.Domain.Tests/Invites/InviteTests.cs
- Test: tests/EventBooking.Domain.Tests/OntologyEnumTests.cs
- Test: tests/EventBooking.Domain.Tests/ScaffoldSmokeTests.cs
- Test: tests/EventBooking.Domain.Tests/Settings/SystemSettingsTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/SlotCapacityHeadcountAdjustmentTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/SlotCapacityTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/SlotProposalAcceptanceTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/SlotProposalConfirmationTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/SlotProposalTests.cs
- Test: tests/EventBooking.Domain.Tests/Slots/SlotWindowTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs
- Test: tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs
- Test: tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/EventBooking.Infrastructure.Tests.csproj
- Test: tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/LocalInfrastructureExtensionsTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs
- Test: tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/SchemaTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/StaffAccessProfilePersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs
- Test: tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs
- Test: tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs
- Test: tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs
- Test: tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs
- Test: tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj
- Test: tests/EventBooking.Mcp.Tests/Fakes/RecordingEmailTransport.cs
- Test: tests/EventBooking.Mcp.Tests/McpEndpointTests.cs
- Test: tests/EventBooking.Mcp.Tests/McpFactory.cs
- Test: tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs
- Test: tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs
- Test: tests/EventBooking.Mcp.Tests/SlotMcpTests.cs
- Test: tests/EventBooking.Mcp.Tests/StaffAccessMcpTests.cs
- Test: tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs
- Test: tests/EventBooking.SeedData.Tests/AppointmentStaffDemoSeedTests.cs
- Test: tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs
- Test: tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs
- Test: tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs
- Test: tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs
- Test: tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs
- Test: tests/EventBooking.SeedData.Tests/EventBooking.SeedData.Tests.csproj
- Test: tests/EventBooking.SeedData.Tests/IdentityProviderDocumentationTests.cs
- Test: tests/EventBooking.SeedData.Tests/IdentityProviderRoleBoundaryTests.cs
- Test: tests/EventBooking.SeedData.Tests/KeycloakSeedContractTests.cs
- Test: tests/EventBooking.SeedData.Tests/KeycloakSeederTests.cs
- Test: tests/EventBooking.SeedData.Tests/KeycloakSeedStepTests.cs
- Test: tests/EventBooking.SeedData.Tests/LoopbackSmtpReceiver.cs
- Test: tests/EventBooking.SeedData.Tests/ReanchorTests.cs
- Test: tests/EventBooking.SeedData.Tests/ReseedTests.cs
- Test: tests/EventBooking.Web.Tests/AdminClientTests.cs
- Test: tests/EventBooking.Web.Tests/ApiCallTests.cs
- Test: tests/EventBooking.Web.Tests/AppointmentsClientTests.cs
- Test: tests/EventBooking.Web.Tests/AppointmentsComponentTests.cs
- Test: tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs
- Test: tests/EventBooking.Web.Tests/AuditClientTests.cs
- Test: tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs
- Test: tests/EventBooking.Web.Tests/AuditPageTests.cs
- Test: tests/EventBooking.Web.Tests/BookingClientTests.cs
- Test: tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs
- Test: tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs
- Test: tests/EventBooking.Web.Tests/CandidateLayoutTests.cs
- Test: tests/EventBooking.Web.Tests/CandidatePresentationTests.cs
- Test: tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs
- Test: tests/EventBooking.Web.Tests/CandidatesClientTests.cs
- Test: tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs
- Test: tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs
- Test: tests/EventBooking.Web.Tests/DashboardsClientTests.cs
- Test: tests/EventBooking.Web.Tests/DashboardsComponentTests.cs
- Test: tests/EventBooking.Web.Tests/EventBooking.Web.Tests.csproj
- Test: tests/EventBooking.Web.Tests/HelpPageTests.cs
- Test: tests/EventBooking.Web.Tests/HomePageTests.cs
- Test: tests/EventBooking.Web.Tests/MainLayoutTests.cs
- Test: tests/EventBooking.Web.Tests/MeClientTests.cs
- Test: tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs
- Test: tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs
- Test: tests/EventBooking.Web.Tests/SettingsTests.cs
- Test: tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs
- Test: tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs
- Test: tests/EventBooking.Web.Tests/SlotsClientTests.cs
- Test: tests/EventBooking.Web.Tests/StaffAccessClientTests.cs
- Test: tests/EventBooking.Web.Tests/StaffAccessPageTests.cs
- Test: tests/EventBooking.Web.Tests/StaffNavigationTests.cs
- Test: tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs

**Interfaces:**

```csharp
using EventBooking.Application.Notifications;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;

namespace EventBooking.Api;

public static class EventBookingConfiguration
{
    /// <summary>
    /// Reads and validates the configuration required to start the EventBooking API.
    /// </summary>
    public static (
        string ConnectionString,
        HeadOfficeOptions HeadOffice,
        TokenOptions Tokens,
        EmailOptions Email,
        CandidatePortalOptions Portal) Read(IConfiguration configuration)
    {
        var missing = new List<string>();

        string Required(string key)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(key);
                return string.Empty;
            }

            return value;
        }

        var connectionString = Required("ConnectionStrings:EventBooking");
        var timeZone = Required("HeadOffice:TimeZoneId");
        var address = Required("HeadOffice:Address");
        var signingKey = Required("Tokens:SigningKey");
        var fromAddress = Required("Email:FromAddress");
        var fromName = Required("Email:FromName");
        var emailProviderRaw = Required("Email:Provider");
        var authProviderRaw = Required("Auth:Provider");
        var baseUrl = Required("Portal:BaseUrl");
        var coordinatorContact = Required("Portal:CoordinatorContact");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The following configuration values are missing: " + string.Join(", ", missing));
        }

        // Checked after the missing-key check, not folded into it: a key that is present but
        // holds an unrecognised value is a different failure from a key that was never set.
        if (emailProviderRaw != "Smtp")
        {
            throw new InvalidOperationException(
                $"Email:Provider must be 'Smtp', but was '{emailProviderRaw}'.");
        }

        var emailProvider = EmailProvider.Smtp;

        if (authProviderRaw != "Local")
        {
            throw new InvalidOperationException(
                $"Auth:Provider must be 'Local', but was '{authProviderRaw}'.");
        }

        return (
            connectionString,
            new HeadOfficeOptions(timeZone),
            new TokenOptions(signingKey),
            new EmailOptions(fromAddress, fromName, emailProvider),
            new CandidatePortalOptions(baseUrl, address, coordinatorContact));
    }
}
```

```csharp
using EventBooking.Application.Abstractions;
using EventBooking.Infrastructure.Audit;
using EventBooking.Infrastructure.Email;
using EventBooking.Infrastructure.Persistence;
using EventBooking.Infrastructure.Persistence.Queries;
using EventBooking.Infrastructure.Persistence.Repositories;
using EventBooking.Infrastructure.Time;
using EventBooking.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventBookingPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        // Registered as a factory, with a scoped context created from it. Task 51's email sender
        // needs a context of its own that is not tied to the request's unit of work, and this is
        // the pattern that gives it one without a second registration of the context type.
        services.AddSingleton<StatusStampingInterceptor>();
        services.AddDbContextFactory<EventBookingDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<StatusStampingInterceptor>()));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<EventBookingDbContext>>().CreateDbContext());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAppointmentTypeRepository, AppointmentTypeRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<ISlotProposalRepository, SlotProposalRepository>();
        services.AddScoped<IConfirmedSlotRepository, ConfirmedSlotRepository>();
        services.AddScoped<ISlotCapacityRepository, SlotCapacityRepository>();
        services.AddScoped<ICandidateRepository, CandidateRepository>();
        services.AddScoped<IEmployeeGroupRepository, EmployeeGroupRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingAppointmentRepository, BookingAppointmentRepository>();
        services.AddScoped<IStaffAccessProfileRepository, StaffAccessProfileRepository>();
        services.AddScoped<IStaffIdentityRepository, StaffIdentityRepository>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddScoped<IAppointmentWorkspaceQueries, AppointmentWorkspaceQueries>();
        services.AddScoped<ICandidateReadinessQueries, CandidateReadinessQueries>();
        services.AddScoped<ICandidateBookingQueries, CandidateBookingQueries>();

        return services;
    }

    public static IServiceCollection AddEventBookingInfrastructure(
        this IServiceCollection services,
        string connectionString,
        HeadOfficeOptions headOffice,
        TokenOptions tokens)
    {
        services.AddEventBookingPersistence(connectionString);

        services.AddSingleton(headOffice);
        services.AddSingleton(tokens);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, HmacTokenService>();

        services.AddScoped<IEmailSender, LoggingEmailSender>();

        services.AddScoped<IAuditLogger, EfAuditLogger>();

        return services;
    }
}
```

**Context you need**

- D5 removes AWS/MinIO; D8 uses provider-neutral OIDC; D9 uses SMTP.
- This is a parity port, not a redesign of negotiation or booking.
- The predecessor snapshot is 6957928a6c9054372dda915b359f63b73969ebee.
- Its verified tests were Domain 230, Application 451, Infrastructure 158, API 225, MCP 34, Web 251 and SeedData 76: total 1425.
- Three provider-specific tests are retired: one Infrastructure AWS registration test, one API Entra registration test and one SeedData Entra deployment-documentation test.
- Two architecture regression tests are added: expected total 1424.
- Ontology invariant: EventCapacity remainingCapacity never falls below zero or exceeds totalHeadcount.
- Ontology invariant: a Booking and its required BookingAppointments consume capacity in the same transaction.
- This task preserves predecessor transactions and their concurrency tests; N-type generalisation belongs to later phases.
- Every production and test file below is an exact port, not a request to obtain missing source elsewhere.

- [ ] **Step 1: Write the failing test**

Create tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj with this temporary standalone test bootstrap. Step 3 replaces it with the fully connected project definition after checking the exact bootstrap checksum.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
```

Create tests/EventBooking.Api.Tests/PortArchitectureTests.cs:

```csharp
using System.Reflection;
using Xunit;
using System.Xml.Linq;

namespace EventBooking.Api.Tests;

public sealed class PortArchitectureTests
{
    [Fact]
    public void Api_dependency_graph_has_no_retired_provider()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Visit(Assembly.Load("EventBooking.Api"), seen);
        Assert.DoesNotContain(seen, IsRetired);
    }

    [Fact]
    public void Source_projects_have_no_retired_provider_reference()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EventBooking.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var references = Directory.EnumerateFiles(Path.Combine(directory.FullName, "src"), "*.csproj", SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants())
            .Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);
        Assert.DoesNotContain(references, IsRetired);
    }

    private static bool IsRetired(string name) =>
        name.Contains("Aws", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Amazon.Lambda", StringComparison.OrdinalIgnoreCase)
        || name.Contains("EntraId", StringComparison.OrdinalIgnoreCase);

    private static void Visit(Assembly assembly, HashSet<string> seen)
    {
        if (!seen.Add(assembly.GetName().Name!)) return;
        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            if (IsRetired(reference.Name!)) seen.Add(reference.Name!);
            else if (reference.Name!.StartsWith("EventBooking", StringComparison.Ordinal))
                Visit(Assembly.Load(reference), seen);
        }
    }
}
```

- [ ] **Step 2: Run the failing tests**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~PortArchitectureTests
```

Expected: two failed tests. No EventBooking.Api assembly or solution exists yet. This is the missing implementation, not a failing package restore. If restore or the SDK fails, repair the environment and repeat this step.

- [ ] **Step 3: Apply the complete source sections**

The 83 phase-0a-source-NNN.md sections include all 584 files, including tests, central build configuration, local identity fixtures and browser assets. Read the section for a file before changing its port. Files longer than one section are continued with explicit part numbers. Do not omit a section or improvise a replacement.

Run this exact extractor from the repository root. It verifies the entire payload and all existing-file preconditions before writing any file. The root README is the only existing tracked file it replaces; that replacement documents discovery URLs without changing the project scope.

```bash
node --input-type=module <<'PORT_NODE'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const plan='docs/detailed-implementations';
const root=fs.realpathSync('.');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const entries=new Map();
const names=fs.readdirSync(plan).filter(n=>/^phase-0a-source-\d{3}\.md$/.test(n)).sort();
if(names.length!==83)throw Error('Expected 83 source sections.');
for(const name of names){
  const text=fs.readFileSync(path.join(plan,name),'utf8');
  const pattern=/<!-- port-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
  for(const match of text.matchAll(pattern)){
    const item=JSON.parse(match[1]);
    if(path.isAbsolute(item.path)||item.path.split('/').includes('..'))throw Error('Unsafe path.');
    const entry=entries.get(item.path)??{...item,chunks:new Map()};
    if(entry.chunks.has(item.part))throw Error('Duplicate part '+item.path);
    if(entry.sha256!==item.sha256||entry.parts!==item.parts)throw Error('Mismatched metadata.');
    entry.chunks.set(item.part,match[2]+'\n');entries.set(item.path,entry);
  }
}
if(entries.size!==584)throw Error('Incomplete port: '+entries.size+' files.');
const pending=[];
for(const entry of entries.values()){
  if(entry.chunks.size!==entry.parts)throw Error('Missing part: '+entry.path);
  const source=Array.from({length:entry.parts},(_,i)=>entry.chunks.get(i+1)).join('');
  const bytes=Buffer.from(source,entry.encoding);
  if(sha(bytes)!==entry.sha256)throw Error('Checksum mismatch: '+entry.path);
  const target=path.join(root,entry.path);
  let parent=path.dirname(target);
  while(!fs.existsSync(parent))parent=path.dirname(parent);
  if(!fs.realpathSync(parent).startsWith(root+path.sep)&&fs.realpathSync(parent)!==root)throw Error('Parent escapes checkout.');
  if(fs.existsSync(target)){
    if(fs.lstatSync(target).isSymbolicLink())throw Error('Symlink target: '+entry.path);
    const actual=sha(fs.readFileSync(target));
    const bootstrap=entry.path==='tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj'?'d045fba7ca213de6773fde335a46ae356a5cabcf0f3de0dd42761bce56cf9246':null;
    if(actual!==entry.sha256&&actual!==entry.before&&actual!==bootstrap)throw Error('Existing changes: '+entry.path);
  }
  pending.push([target,bytes]);
}
for(const [target,bytes] of pending){fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,bytes);}
console.log('Verified and applied '+pending.length+' embedded files.');
PORT_NODE
```

- [ ] **Step 4: Verify the port architecture tests pass**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~PortArchitectureTests
```

Expected: two passed, zero failed, zero skipped. The full suite below additionally exercises actual REST and MCP requests, PostgreSQL transactions, SMTP registration and rendered components.

STOP AND CHECK: run the architecture tests against the connected source project, not the standalone bootstrap. The final API test project contains project references to Api, Domain, Application, Infrastructure and Api.Auth. Restore must not bring in the retired provider projects.

- [ ] **Step 5: Build and run the full suite**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero build warnings/errors; Domain 230, Application 451, Infrastructure 157, API 226, MCP 34, Web 251, SeedData 75. All 1424 pass and none are skipped. This task introduces no new domain concept and does not edit the ontology.

- [ ] **Step 6: Commit and push**

```bash
git add -- \
  '.dockerignore' \
  'deploy/home-lab/keycloak/eventbooking-realm.json' \
  'deploy/home-lab/README.md' \
  'deploy/keycloak/realm-export.json' \
  'Directory.Build.props' \
  'Directory.Packages.props' \
  'docs/demo-runbook.md' \
  'docs/user-guides/admin-guide.md' \
  'docs/user-guides/appointment-staff-guide.md' \
  'docs/user-guides/candidate-guide.md' \
  'docs/user-guides/coordinator-guide.md' \
  'docs/user-guides/manager-guide.md' \
  'docs/user-guides/README.md' \
  'EventBooking.sln' \
  'README.md' \
  'src/EventBooking.Api.Auth/EventBooking.Api.Auth.csproj' \
  'src/EventBooking.Api.Auth/LocalAuthenticationExtensions.cs' \
  'src/EventBooking.Api/appsettings.json' \
  'src/EventBooking.Api/appsettings.Local.json' \
  'src/EventBooking.Api/Auth/AuthenticationExtensions.cs' \
  'src/EventBooking.Api/Auth/HttpContextCallerAccessor.cs' \
  'src/EventBooking.Api/Auth/ICallerAccessor.cs' \
  'src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs' \
  'src/EventBooking.Api/Auth/StaffIdentityRecorder.cs' \
  'src/EventBooking.Api/Auth/StaffRequirement.cs' \
  'src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/ApiLink.cs' \
  'src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs' \
  'src/EventBooking.Api/Dockerfile' \
  'src/EventBooking.Api/Endpoints/AdminEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/AuditEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/AuditInputParser.cs' \
  'src/EventBooking.Api/Endpoints/BookingEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/CandidateEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/DashboardEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/MeEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/ResultResponses.cs' \
  'src/EventBooking.Api/Endpoints/SlotEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/StaffAccessEndpoints.cs' \
  'src/EventBooking.Api/EventBooking.Api.csproj' \
  'src/EventBooking.Api/EventBookingConfiguration.cs' \
  'src/EventBooking.Api/InviteSweepService.cs' \
  'src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs' \
  'src/EventBooking.Api/OpenApi/EndpointMetadataExtensions.cs' \
  'src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs' \
  'src/EventBooking.Api/Program.cs' \
  'src/EventBooking.Api/Properties/launchSettings.json' \
  'src/EventBooking.Application/Abstractions/IAppointmentTypeRepository.cs' \
  'src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs' \
  'src/EventBooking.Application/Abstractions/IAuditLogger.cs' \
  'src/EventBooking.Application/Abstractions/IAuditQueries.cs' \
  'src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs' \
  'src/EventBooking.Application/Abstractions/IBookingRepository.cs' \
  'src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs' \
  'src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs' \
  'src/EventBooking.Application/Abstractions/ICandidateRepository.cs' \
  'src/EventBooking.Application/Abstractions/IClock.cs' \
  'src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs' \
  'src/EventBooking.Application/Abstractions/IDashboardQueries.cs' \
  'src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs' \
  'src/EventBooking.Application/Abstractions/IEmailSender.cs' \
  'src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs' \
  'src/EventBooking.Application/Abstractions/IInviteRepository.cs' \
  'src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs' \
  'src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs' \
  'src/EventBooking.Application/Abstractions/IStaffAccessProfileRepository.cs' \
  'src/EventBooking.Application/Abstractions/IStaffIdentityRepository.cs' \
  'src/EventBooking.Application/Abstractions/ISystemSettingsRepository.cs' \
  'src/EventBooking.Application/Abstractions/ITokenService.cs' \
  'src/EventBooking.Application/Abstractions/IUnitOfWork.cs' \
  'src/EventBooking.Application/Access/MeHandler.cs' \
  'src/EventBooking.Application/Access/StaffAccessAuthorizer.cs' \
  'src/EventBooking.Application/Access/StaffAccessHandler.cs' \
  'src/EventBooking.Application/Access/StaffCapability.cs' \
  'src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs' \
  'src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs' \
  'src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs' \
  'src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs' \
  'src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs' \
  'src/EventBooking.Application/Appointments/RecoveryBookingOutcomeCoordinator.cs' \
  'src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs' \
  'src/EventBooking.Application/Bookings/BookingCanceller.cs' \
  'src/EventBooking.Application/Bookings/CancelBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs' \
  'src/EventBooking.Application/Bookings/ViewBookingHandler.cs' \
  'src/EventBooking.Application/Bookings/ViewInviteHandler.cs' \
  'src/EventBooking.Application/Candidates/CandidateCsvParser.cs' \
  'src/EventBooking.Application/Candidates/CandidateReadiness.cs' \
  'src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs' \
  'src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs' \
  'src/EventBooking.Application/Candidates/EmployeeGroupModels.cs' \
  'src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs' \
  'src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs' \
  'src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs' \
  'src/EventBooking.Application/Candidates/ListCandidatesHandler.cs' \
  'src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs' \
  'src/EventBooking.Application/Candidates/SaveCandidateHandler.cs' \
  'src/EventBooking.Application/Common/Error.cs' \
  'src/EventBooking.Application/Common/Result.cs' \
  'src/EventBooking.Application/Common/UniqueConstraintViolationException.cs' \
  'src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs' \
  'src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs' \
  'src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs' \
  'src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs' \
  'src/EventBooking.Application/DependencyInjection.cs' \
  'src/EventBooking.Application/EventBooking.Application.csproj' \
  'src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs' \
  'src/EventBooking.Application/Invites/EligibleSlotFinder.cs' \
  'src/EventBooking.Application/Invites/ExpireInvitesHandler.cs' \
  'src/EventBooking.Application/Invites/InviteIssuer.cs' \
  'src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs' \
  'src/EventBooking.Application/Invites/StartRecoveryHandler.cs' \
  'src/EventBooking.Application/Invites/TriggerInviteHandler.cs' \
  'src/EventBooking.Application/Notifications/CandidateEmailComposer.cs' \
  'src/EventBooking.Application/Notifications/CandidatePortalOptions.cs' \
  'src/EventBooking.Application/Notifications/EmailDeliveryService.cs' \
  'src/EventBooking.Application/Notifications/RetryEmailHandler.cs' \
  'src/EventBooking.Application/Properties/AssemblyInfo.cs' \
  'src/EventBooking.Application/Settings/AdminSettingsHandler.cs' \
  'src/EventBooking.Application/Slots/AcceptProposalHandler.cs' \
  'src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs' \
  'src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs' \
  'src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs' \
  'src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs' \
  'src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs' \
  'src/EventBooking.Application/Slots/ProposeSlotHandler.cs' \
  'src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs' \
  'src/EventBooking.Application/Slots/WithdrawProposalHandler.cs' \
  'src/EventBooking.Domain/Access/Role.cs' \
  'src/EventBooking.Domain/Access/StaffAccessProfile.cs' \
  'src/EventBooking.Domain/Access/StaffId.cs' \
  'src/EventBooking.Domain/Access/StaffIdentity.cs' \
  'src/EventBooking.Domain/AppointmentTypes/AppointmentType.cs' \
  'src/EventBooking.Domain/AppointmentTypes/AppointmentTypeIds.cs' \
  'src/EventBooking.Domain/Audit/ActorType.cs' \
  'src/EventBooking.Domain/Audit/AuditAction.cs' \
  'src/EventBooking.Domain/Audit/AuditEntityTypes.cs' \
  'src/EventBooking.Domain/Audit/AuditLog.cs' \
  'src/EventBooking.Domain/Bookings/Booking.cs' \
  'src/EventBooking.Domain/Bookings/BookingAppointment.cs' \
  'src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs' \
  'src/EventBooking.Domain/Bookings/BookingStatus.cs' \
  'src/EventBooking.Domain/Candidates/Candidate.cs' \
  'src/EventBooking.Domain/Candidates/CandidateRequirement.cs' \
  'src/EventBooking.Domain/Candidates/CandidateStatus.cs' \
  'src/EventBooking.Domain/Common/DomainException.cs' \
  'src/EventBooking.Domain/Common/Guard.cs' \
  'src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs' \
  'src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs' \
  'src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs' \
  'src/EventBooking.Domain/EventBooking.Domain.csproj' \
  'src/EventBooking.Domain/Invites/Invite.cs' \
  'src/EventBooking.Domain/Invites/InviteOption.cs' \
  'src/EventBooking.Domain/Invites/InviteRequirement.cs' \
  'src/EventBooking.Domain/Invites/InviteStatus.cs' \
  'src/EventBooking.Domain/Notifications/EmailLog.cs' \
  'src/EventBooking.Domain/Notifications/EmailStatus.cs' \
  'src/EventBooking.Domain/Notifications/EmailTemplate.cs' \
  'src/EventBooking.Domain/Settings/SystemSettings.cs' \
  'src/EventBooking.Domain/Slots/ConfirmedSlot.cs' \
  'src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs' \
  'src/EventBooking.Domain/Slots/ProposalAcceptance.cs' \
  'src/EventBooking.Domain/Slots/SlotCapacity.cs' \
  'src/EventBooking.Domain/Slots/SlotProposal.cs' \
  'src/EventBooking.Domain/Slots/SlotProposalStatus.cs' \
  'src/EventBooking.Domain/Slots/SlotWindow.cs' \
  'src/EventBooking.Infrastructure/Audit/EfAuditLogger.cs' \
  'src/EventBooking.Infrastructure/DependencyInjection.cs' \
  'src/EventBooking.Infrastructure/Email/EmailOptions.cs' \
  'src/EventBooking.Infrastructure/Email/IEmailTransport.cs' \
  'src/EventBooking.Infrastructure/Email/LocalInfrastructureExtensions.cs' \
  'src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs' \
  'src/EventBooking.Infrastructure/Email/SmtpEmailTransport.cs' \
  'src/EventBooking.Infrastructure/Email/SmtpOptions.cs' \
  'src/EventBooking.Infrastructure/EventBooking.Infrastructure.csproj' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AppointmentTypeConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/BookingAppointmentConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/StaffAccessProfileConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/StaffIdentityConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/SystemSettingsConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/DesignTimeDbContextFactory.cs' \
  'src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/AppointmentWorkspaceQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/AuditQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/CandidateBookingQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/CandidateReadinessQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/StaffAccessProfileRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/StaffIdentityRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs' \
  'src/EventBooking.Infrastructure/Persistence/UnitOfWork.cs' \
  'src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs' \
  'src/EventBooking.Infrastructure/Time/SystemClock.cs' \
  'src/EventBooking.Infrastructure/Tokens/HmacTokenService.cs' \
  'src/EventBooking.Infrastructure/Tokens/TokenOptions.cs' \
  'src/EventBooking.Mcp/appsettings.json' \
  'src/EventBooking.Mcp/appsettings.Local.json' \
  'src/EventBooking.Mcp/Dockerfile' \
  'src/EventBooking.Mcp/EventBooking.Mcp.csproj' \
  'src/EventBooking.Mcp/Program.cs' \
  'src/EventBooking.Mcp/Properties/launchSettings.json' \
  'src/EventBooking.Mcp/Tools/AdminTools.cs' \
  'src/EventBooking.Mcp/Tools/AdminToolViews.cs' \
  'src/EventBooking.Mcp/Tools/CandidateTools.cs' \
  'src/EventBooking.Mcp/Tools/McpErrors.cs' \
  'src/EventBooking.Mcp/Tools/OperationsTools.cs' \
  'src/EventBooking.Mcp/Tools/SlotTools.cs' \
  'src/EventBooking.SeedData/demo-seed.json' \
  'src/EventBooking.SeedData/DemoEmailOptions.cs' \
  'src/EventBooking.SeedData/DemoInvitationSeeder.cs' \
  'src/EventBooking.SeedData/DemoSeeder.cs' \
  'src/EventBooking.SeedData/DemoSeedSpec.cs' \
  'src/EventBooking.SeedData/EventBooking.SeedData.csproj' \
  'src/EventBooking.SeedData/KeycloakSeeder.cs' \
  'src/EventBooking.SeedData/KeycloakSeedOptions.cs' \
  'src/EventBooking.SeedData/KeycloakSeedStep.cs' \
  'src/EventBooking.SeedData/Program.cs' \
  'src/EventBooking.Web/_Imports.razor' \
  'src/EventBooking.Web/.npmrc' \
  'src/EventBooking.Web/App.razor' \
  'src/EventBooking.Web/Dockerfile' \
  'src/EventBooking.Web/EventBooking.Web.csproj' \
  'src/EventBooking.Web/Layout/CandidateLayout.razor' \
  'src/EventBooking.Web/Layout/MainLayout.razor' \
  'src/EventBooking.Web/Layout/RedirectToLogin.razor' \
  'src/EventBooking.Web/nginx.conf' \
  'src/EventBooking.Web/Pages/Appointments.razor' \
  'src/EventBooking.Web/Pages/Appointments.razor.css' \
  'src/EventBooking.Web/Pages/Audit.razor' \
  'src/EventBooking.Web/Pages/Audit.razor.css' \
  'src/EventBooking.Web/Pages/Authentication.razor' \
  'src/EventBooking.Web/Pages/Book.razor' \
  'src/EventBooking.Web/Pages/Book.razor.css' \
  'src/EventBooking.Web/Pages/Candidates.razor' \
  'src/EventBooking.Web/Pages/Candidates.razor.css' \
  'src/EventBooking.Web/Pages/ConfirmedSlots.razor' \
  'src/EventBooking.Web/Pages/ConfirmedSlots.razor.css' \
  'src/EventBooking.Web/Pages/Dashboards.razor' \
  'src/EventBooking.Web/Pages/Dashboards.razor.css' \
  'src/EventBooking.Web/Pages/Help.razor' \
  'src/EventBooking.Web/Pages/Home.razor' \
  'src/EventBooking.Web/Pages/ManageBooking.razor' \
  'src/EventBooking.Web/Pages/ManageBooking.razor.css' \
  'src/EventBooking.Web/Pages/NotFound.razor' \
  'src/EventBooking.Web/Pages/Settings.razor' \
  'src/EventBooking.Web/Pages/Settings.razor.css' \
  'src/EventBooking.Web/Pages/Slots.razor' \
  'src/EventBooking.Web/Pages/Slots.razor.css' \
  'src/EventBooking.Web/Pages/StaffAccess.razor' \
  'src/EventBooking.Web/Pages/StaffAccess.razor.css' \
  'src/EventBooking.Web/Program.cs' \
  'src/EventBooking.Web/Properties/AssemblyInfo.cs' \
  'src/EventBooking.Web/Properties/launchSettings.json' \
  'src/EventBooking.Web/Services/AdminClient.cs' \
  'src/EventBooking.Web/Services/ApiCall.cs' \
  'src/EventBooking.Web/Services/ApiOutcome.cs' \
  'src/EventBooking.Web/Services/AppointmentsClient.cs' \
  'src/EventBooking.Web/Services/AuditClient.cs' \
  'src/EventBooking.Web/Services/BookingClient.cs' \
  'src/EventBooking.Web/Services/CandidatePresentation.cs' \
  'src/EventBooking.Web/Services/CandidatesClient.cs' \
  'src/EventBooking.Web/Services/ConfirmedSlotsClient.cs' \
  'src/EventBooking.Web/Services/DashboardsClient.cs' \
  'src/EventBooking.Web/Services/HeadOfficePageClock.cs' \
  'src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs' \
  'src/EventBooking.Web/Services/MeClient.cs' \
  'src/EventBooking.Web/Services/SlotsClient.cs' \
  'src/EventBooking.Web/Services/StaffAccessClient.cs' \
  'src/EventBooking.Web/Services/StaffNavigation.cs' \
  'src/EventBooking.Web/Services/UserGuideCatalog.cs' \
  'src/EventBooking.Web/Shared/AuditHistory.razor' \
  'src/EventBooking.Web/Shared/BrandMark.razor' \
  'src/EventBooking.Web/wwwroot/appsettings.Development.json' \
  'src/EventBooking.Web/wwwroot/appsettings.json' \
  'src/EventBooking.Web/wwwroot/css/app.css' \
  'src/EventBooking.Web/wwwroot/css/fonts.css' \
  'src/EventBooking.Web/wwwroot/favicon.png' \
  'src/EventBooking.Web/wwwroot/fonts/mylius-Modern-bd.woff2' \
  'src/EventBooking.Web/wwwroot/fonts/mylius-Modern-extlig.woff2' \
  'src/EventBooking.Web/wwwroot/fonts/mylius-Modern-lt.woff2' \
  'src/EventBooking.Web/wwwroot/fonts/mylius-Modern-reg.woff2' \
  'src/EventBooking.Web/wwwroot/fonts/open-sans/open-sans-v15-latin-300.woff2' \
  'src/EventBooking.Web/wwwroot/fonts/open-sans/open-sans-v15-latin-700.woff2' \
  'src/EventBooking.Web/wwwroot/fonts/open-sans/open-sans-v15-latin-regular.woff2' \
  'src/EventBooking.Web/wwwroot/icon-192.png' \
  'src/EventBooking.Web/wwwroot/icons/alert.svg' \
  'src/EventBooking.Web/wwwroot/icons/delete.svg' \
  'src/EventBooking.Web/wwwroot/icons/edit.svg' \
  'src/EventBooking.Web/wwwroot/icons/search.svg' \
  'src/EventBooking.Web/wwwroot/icons/tick.svg' \
  'src/EventBooking.Web/wwwroot/index.html' \
  'src/EventBooking.Web/wwwroot/js/download.js' \
  'src/EventBooking.Web/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css' \
  'src/EventBooking.Web/wwwroot/speedmarque.png' \
  'tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs' \
  'tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs' \
  'tests/EventBooking.Api.Tests/ApiFactory.cs' \
  'tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AuditEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs' \
  'tests/EventBooking.Api.Tests/BookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/CallerAccessorTests.cs' \
  'tests/EventBooking.Api.Tests/CallerIdentityTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/CorsTests.cs' \
  'tests/EventBooking.Api.Tests/DashboardEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/DiscoveryDocumentationTests.cs' \
  'tests/EventBooking.Api.Tests/EventBooking.Api.Tests.csproj' \
  'tests/EventBooking.Api.Tests/Fakes/RecordingEmailTransport.cs' \
  'tests/EventBooking.Api.Tests/HealthTests.cs' \
  'tests/EventBooking.Api.Tests/LocalAuthenticationExtensionsTests.cs' \
  'tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/MeEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/OpenApiContractTests.cs' \
  'tests/EventBooking.Api.Tests/OpenApiHostingTests.cs' \
  'tests/EventBooking.Api.Tests/PortArchitectureTests.cs' \
  'tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs' \
  'tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ResultResponsesTests.cs' \
  'tests/EventBooking.Api.Tests/SlotEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/StaffAccessEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/StaffHypermediaTests.cs' \
  'tests/EventBooking.Api.Tests/StaffIdentityRecorderTests.cs' \
  'tests/EventBooking.Api.Tests/StaffRequirementHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs' \
  'tests/EventBooking.Application.Tests/Access/MeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs' \
  'tests/EventBooking.Application.Tests/Access/StaffAccessHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Access/SyncStaffAccessProfileRolesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelCandidateBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Common/ResultTests.cs' \
  'tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs' \
  'tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/EventBooking.Application.Tests.csproj' \
  'tests/EventBooking.Application.Tests/Fakes/EmailDeliveryTestFactory.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakeClock.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakeTokenService.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs' \
  'tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs' \
  'tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs' \
  'tests/EventBooking.Application.Tests/Fakes/RecordingAuditLogger.cs' \
  'tests/EventBooking.Application.Tests/Fakes/RecordingEmailSender.cs' \
  'tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryRequirementSelectorTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/CandidateEmailComposerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs' \
  'tests/EventBooking.Application.Tests/Settings/AdminSettingsAccessProfileTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs' \
  'tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs' \
  'tests/EventBooking.Domain.Tests/Access/StaffAccessProfileTests.cs' \
  'tests/EventBooking.Domain.Tests/Access/StaffIdTests.cs' \
  'tests/EventBooking.Domain.Tests/AppointmentTypes/AppointmentTypeTests.cs' \
  'tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentAuditVocabularyTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/BookingAppointmentTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs' \
  'tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs' \
  'tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs' \
  'tests/EventBooking.Domain.Tests/Common/GuardTests.cs' \
  'tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs' \
  'tests/EventBooking.Domain.Tests/EventBooking.Domain.Tests.csproj' \
  'tests/EventBooking.Domain.Tests/Invites/InviteRequirementSnapshotTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteTests.cs' \
  'tests/EventBooking.Domain.Tests/OntologyEnumTests.cs' \
  'tests/EventBooking.Domain.Tests/ScaffoldSmokeTests.cs' \
  'tests/EventBooking.Domain.Tests/Settings/SystemSettingsTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/SlotCapacityHeadcountAdjustmentTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/SlotCapacityTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/SlotProposalAcceptanceTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/SlotProposalConfirmationTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/SlotProposalTests.cs' \
  'tests/EventBooking.Domain.Tests/Slots/SlotWindowTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs' \
  'tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EventBooking.Infrastructure.Tests.csproj' \
  'tests/EventBooking.Infrastructure.Tests/HmacTokenServiceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/LocalInfrastructureExtensionsTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs' \
  'tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SchemaTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffAccessProfileMigrationRegressionTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffAccessProfilePersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/StaffIdentityPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs' \
  'tests/EventBooking.Mcp.Tests/AgentSurfaceParityTests.cs' \
  'tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/EventBooking.Mcp.Tests.csproj' \
  'tests/EventBooking.Mcp.Tests/Fakes/RecordingEmailTransport.cs' \
  'tests/EventBooking.Mcp.Tests/McpEndpointTests.cs' \
  'tests/EventBooking.Mcp.Tests/McpFactory.cs' \
  'tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs' \
  'tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/SlotMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/StaffAccessMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs' \
  'tests/EventBooking.SeedData.Tests/AppointmentStaffDemoSeedTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs' \
  'tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs' \
  'tests/EventBooking.SeedData.Tests/EventBooking.SeedData.Tests.csproj' \
  'tests/EventBooking.SeedData.Tests/IdentityProviderDocumentationTests.cs' \
  'tests/EventBooking.SeedData.Tests/IdentityProviderRoleBoundaryTests.cs' \
  'tests/EventBooking.SeedData.Tests/KeycloakSeedContractTests.cs' \
  'tests/EventBooking.SeedData.Tests/KeycloakSeederTests.cs' \
  'tests/EventBooking.SeedData.Tests/KeycloakSeedStepTests.cs' \
  'tests/EventBooking.SeedData.Tests/LoopbackSmtpReceiver.cs' \
  'tests/EventBooking.SeedData.Tests/ReanchorTests.cs' \
  'tests/EventBooking.SeedData.Tests/ReseedTests.cs' \
  'tests/EventBooking.Web.Tests/AdminClientTests.cs' \
  'tests/EventBooking.Web.Tests/ApiCallTests.cs' \
  'tests/EventBooking.Web.Tests/AppointmentsClientTests.cs' \
  'tests/EventBooking.Web.Tests/AppointmentsComponentTests.cs' \
  'tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs' \
  'tests/EventBooking.Web.Tests/AuditClientTests.cs' \
  'tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs' \
  'tests/EventBooking.Web.Tests/AuditPageTests.cs' \
  'tests/EventBooking.Web.Tests/BookingClientTests.cs' \
  'tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs' \
  'tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs' \
  'tests/EventBooking.Web.Tests/CandidateLayoutTests.cs' \
  'tests/EventBooking.Web.Tests/CandidatePresentationTests.cs' \
  'tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs' \
  'tests/EventBooking.Web.Tests/CandidatesClientTests.cs' \
  'tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs' \
  'tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs' \
  'tests/EventBooking.Web.Tests/DashboardsClientTests.cs' \
  'tests/EventBooking.Web.Tests/DashboardsComponentTests.cs' \
  'tests/EventBooking.Web.Tests/EventBooking.Web.Tests.csproj' \
  'tests/EventBooking.Web.Tests/HelpPageTests.cs' \
  'tests/EventBooking.Web.Tests/HomePageTests.cs' \
  'tests/EventBooking.Web.Tests/MainLayoutTests.cs' \
  'tests/EventBooking.Web.Tests/MeClientTests.cs' \
  'tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs' \
  'tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs' \
  'tests/EventBooking.Web.Tests/SettingsTests.cs' \
  'tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs' \
  'tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs' \
  'tests/EventBooking.Web.Tests/SlotsClientTests.cs' \
  'tests/EventBooking.Web.Tests/StaffAccessClientTests.cs' \
  'tests/EventBooking.Web.Tests/StaffAccessPageTests.cs' \
  'tests/EventBooking.Web.Tests/StaffNavigationTests.cs' \
  'tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "build: port JointBooking solution as EventBooking without AWS" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Do not open the Phase 0 PR yet. Task 2 removes predecessor vocabulary; Task 3 retires event import and the remaining single-site configuration. The phase PR follows Task 3.
