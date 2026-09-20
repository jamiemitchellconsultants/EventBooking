# 00b — Canonical vocabulary (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

This cross-layer task follows the complete port and preserves behaviour while changing domain, storage, REST, MCP and web vocabulary. It precedes removal of retired functionality in Task 3. All before and after files are supplied in this task's numbered edit sections.

> Use superpowers:executing-plans. Execute on the existing Phase 0 branch after Task 1's pushed commit.

**Goal:** Make every public contract use the ontology vocabulary before the Phase 0 PR.

**Architecture:** Keep the same domain/application boundaries and transaction algorithms; rename their contracts together so the host, persistence and clients agree.

**Tech Stack:** .NET 10, xUnit, Testcontainers and bUnit, with Task 1's exact central package versions.

**Spec:** [Decision record §3](../superpowers/specs/2026-09-19-eventbooking-design.md#3-vocabulary-mapping), [ontology](../ontology.md), and [master Task 2](../superpowers/plans/2026-09-19-eventbooking-implementation.md).

## Global constraints

No behavioural generalisation belongs in this task. Preserve ordered row locking and the capacity check constraint. Preserve the predecessor migration chain until Task 9. No old names remain in public types, enum members, JSON fields, routes or MCP tools. The explicitly labelled before sides of this plan retain the old names solely to match edits. Task 3 removes the temporary single-location configuration, the event-import operation and the unassigned-group compatibility path.

## Review focus

Do not rename a local variable to the C# keyword event: use eventItem. The two web pages and their clients need distinct canonical names, not a collision at Events. API group prefixes and relative client URLs must change together. Renamed XML parameter tags must match declarations. Database parameters and SQL placeholders must match exactly after normalization. The supplied compiled after files incorporate these checks.

### Task 2: Apply the vocabulary mapping

**Files:**

- Modify: docs/user-guides/README.md
- Modify: docs/user-guides/admin-guide.md
- Modify: docs/user-guides/appointment-staff-guide.md
- Modify: docs/user-guides/candidate-guide.md (rename to docs/user-guides/attendee-guide.md)
- Modify: docs/user-guides/coordinator-guide.md
- Modify: docs/user-guides/manager-guide.md
- Modify: src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs
- Modify: src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs
- Modify: src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs (rename to src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs)
- Modify: src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs
- Modify: src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs (rename to src/EventBooking.Api/Contracts/EventHypermediaResponses.cs)
- Modify: src/EventBooking.Api/Endpoints/AdminEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/AuditEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/BookingEndpoints.cs
- Modify: src/EventBooking.Api/Endpoints/CandidateEndpoints.cs (rename to src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs)
- Modify: src/EventBooking.Api/Endpoints/ResultResponses.cs
- Modify: src/EventBooking.Api/Endpoints/SlotEndpoints.cs (rename to src/EventBooking.Api/Endpoints/EventEndpoints.cs)
- Modify: src/EventBooking.Api/EventBookingConfiguration.cs
- Modify: src/EventBooking.Api/InviteSweepService.cs
- Modify: src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs
- Modify: src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs
- Modify: src/EventBooking.Api/Program.cs
- Modify: src/EventBooking.Api/appsettings.Local.json
- Modify: src/EventBooking.Api/appsettings.json
- Modify: src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs
- Modify: src/EventBooking.Application/Abstractions/IAuditQueries.cs
- Modify: src/EventBooking.Application/Abstractions/IBookingAppointmentRepository.cs
- Modify: src/EventBooking.Application/Abstractions/IBookingRepository.cs
- Modify: src/EventBooking.Application/Abstractions/ICandidateBookingQueries.cs (rename to src/EventBooking.Application/Abstractions/IAttendeeBookingQueries.cs)
- Modify: src/EventBooking.Application/Abstractions/ICandidateReadinessQueries.cs (rename to src/EventBooking.Application/Abstractions/IAttendeeReadinessQueries.cs)
- Modify: src/EventBooking.Application/Abstractions/ICandidateRepository.cs (rename to src/EventBooking.Application/Abstractions/IAttendeeRepository.cs)
- Modify: src/EventBooking.Application/Abstractions/IClock.cs
- Modify: src/EventBooking.Application/Abstractions/IConfirmedSlotRepository.cs (rename to src/EventBooking.Application/Abstractions/IEventRepository.cs)
- Modify: src/EventBooking.Application/Abstractions/IDashboardQueries.cs
- Modify: src/EventBooking.Application/Abstractions/IEmailDeliveryRepository.cs
- Modify: src/EventBooking.Application/Abstractions/IEmailSender.cs
- Modify: src/EventBooking.Application/Abstractions/IEmployeeGroupRepository.cs (rename to src/EventBooking.Application/Abstractions/IAttendeeGroupRepository.cs)
- Modify: src/EventBooking.Application/Abstractions/IInviteRepository.cs
- Modify: src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs (rename to src/EventBooking.Application/Abstractions/IEventCapacityRepository.cs)
- Modify: src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs (rename to src/EventBooking.Application/Abstractions/IEventProposalRepository.cs)
- Modify: src/EventBooking.Application/Access/StaffAccessAuthorizer.cs
- Modify: src/EventBooking.Application/Access/StaffCapability.cs
- Modify: src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs
- Modify: src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs
- Modify: src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs
- Modify: src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs
- Modify: src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs
- Modify: src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs
- Modify: src/EventBooking.Application/Bookings/BookingCanceller.cs
- Modify: src/EventBooking.Application/Bookings/CancelBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/CancelCandidateBookingHandler.cs (rename to src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs)
- Modify: src/EventBooking.Application/Bookings/ConfirmBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/RecoveryConfirmationValidator.cs
- Modify: src/EventBooking.Application/Bookings/ViewBookingHandler.cs
- Modify: src/EventBooking.Application/Bookings/ViewInviteHandler.cs
- Modify: src/EventBooking.Application/Candidates/CandidateCsvParser.cs (rename to src/EventBooking.Application/Attendees/AttendeeCsvParser.cs)
- Modify: src/EventBooking.Application/Candidates/CandidateReadiness.cs (rename to src/EventBooking.Application/Attendees/AttendeeReadiness.cs)
- Modify: src/EventBooking.Application/Candidates/CandidateReadinessCalculator.cs (rename to src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs)
- Modify: src/EventBooking.Application/Candidates/DeleteCandidateHandler.cs (rename to src/EventBooking.Application/Attendees/DeleteAttendeeHandler.cs)
- Modify: src/EventBooking.Application/Candidates/EmployeeGroupModels.cs (rename to src/EventBooking.Application/Attendees/AttendeeGroupModels.cs)
- Modify: src/EventBooking.Application/Candidates/GetCandidateBookingsHandler.cs (rename to src/EventBooking.Application/Attendees/GetAttendeeBookingsHandler.cs)
- Modify: src/EventBooking.Application/Candidates/GetCandidateReadinessHandler.cs (rename to src/EventBooking.Application/Attendees/GetAttendeeReadinessHandler.cs)
- Modify: src/EventBooking.Application/Candidates/ImportCandidatesHandler.cs (rename to src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs)
- Modify: src/EventBooking.Application/Candidates/ListCandidatesHandler.cs (rename to src/EventBooking.Application/Attendees/ListAttendeesHandler.cs)
- Modify: src/EventBooking.Application/Candidates/ListEmployeeGroupsHandler.cs (rename to src/EventBooking.Application/Attendees/ListAttendeeGroupsHandler.cs)
- Modify: src/EventBooking.Application/Candidates/SaveCandidateHandler.cs (rename to src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs)
- Modify: src/EventBooking.Application/Common/Error.cs
- Modify: src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs
- Modify: src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs
- Modify: src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs
- Modify: src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs (rename to src/EventBooking.Application/Dashboards/GetEventOperationsHandler.cs)
- Modify: src/EventBooking.Application/DependencyInjection.cs
- Modify: src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs
- Modify: src/EventBooking.Application/Invites/EligibleSlotFinder.cs (rename to src/EventBooking.Application/Invites/EligibleEventFinder.cs)
- Modify: src/EventBooking.Application/Invites/ExpireInvitesHandler.cs
- Modify: src/EventBooking.Application/Invites/InviteIssuer.cs
- Modify: src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs
- Modify: src/EventBooking.Application/Invites/StartRecoveryHandler.cs
- Modify: src/EventBooking.Application/Invites/TriggerInviteHandler.cs
- Modify: src/EventBooking.Application/Notifications/CandidateEmailComposer.cs (rename to src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs)
- Modify: src/EventBooking.Application/Notifications/CandidatePortalOptions.cs (rename to src/EventBooking.Application/Notifications/AttendeePortalOptions.cs)
- Modify: src/EventBooking.Application/Notifications/EmailDeliveryService.cs
- Modify: src/EventBooking.Application/Notifications/RetryEmailHandler.cs
- Modify: src/EventBooking.Application/Slots/AcceptProposalHandler.cs (rename to src/EventBooking.Application/Events/AcceptProposalHandler.cs)
- Modify: src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs (rename to src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs)
- Modify: src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs (rename to src/EventBooking.Application/Events/CancelEventHandler.cs)
- Modify: src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs (rename to src/EventBooking.Application/Events/EventImportParser.cs)
- Modify: src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs (rename to src/EventBooking.Application/Events/GetManagerEventBoardHandler.cs)
- Modify: src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs (rename to src/EventBooking.Application/Events/ImportEventsHandler.cs)
- Modify: src/EventBooking.Application/Slots/ProposeSlotHandler.cs (rename to src/EventBooking.Application/Events/ProposeEventHandler.cs)
- Modify: src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs (rename to src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs)
- Modify: src/EventBooking.Application/Slots/WithdrawProposalHandler.cs (rename to src/EventBooking.Application/Events/WithdrawProposalHandler.cs)
- Modify: src/EventBooking.Domain/Audit/ActorType.cs
- Modify: src/EventBooking.Domain/Audit/AuditAction.cs
- Modify: src/EventBooking.Domain/Audit/AuditEntityTypes.cs
- Modify: src/EventBooking.Domain/Audit/AuditLog.cs
- Modify: src/EventBooking.Domain/Bookings/Booking.cs
- Modify: src/EventBooking.Domain/Bookings/BookingAppointment.cs
- Modify: src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs
- Modify: src/EventBooking.Domain/Candidates/Candidate.cs (rename to src/EventBooking.Domain/Attendees/Attendee.cs)
- Modify: src/EventBooking.Domain/Candidates/CandidateRequirement.cs (rename to src/EventBooking.Domain/Attendees/AttendeeRequirement.cs)
- Modify: src/EventBooking.Domain/Candidates/CandidateStatus.cs (rename to src/EventBooking.Domain/Attendees/AttendeeStatus.cs)
- Modify: src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs (rename to src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs)
- Modify: src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs (rename to src/EventBooking.Domain/AttendeeGroups/AttendeeGroupIds.cs)
- Modify: src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs (rename to src/EventBooking.Domain/AttendeeGroups/AttendeeGroupRequirement.cs)
- Modify: src/EventBooking.Domain/Invites/Invite.cs
- Modify: src/EventBooking.Domain/Invites/InviteOption.cs
- Modify: src/EventBooking.Domain/Invites/InviteStatus.cs
- Modify: src/EventBooking.Domain/Notifications/EmailLog.cs
- Modify: src/EventBooking.Domain/Notifications/EmailStatus.cs
- Modify: src/EventBooking.Domain/Notifications/EmailTemplate.cs
- Modify: src/EventBooking.Domain/Slots/ConfirmedSlot.cs (rename to src/EventBooking.Domain/Events/Event.cs)
- Modify: src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs (rename to src/EventBooking.Domain/Events/EventStatus.cs)
- Modify: src/EventBooking.Domain/Slots/ProposalAcceptance.cs (rename to src/EventBooking.Domain/Events/ProposalAcceptance.cs)
- Modify: src/EventBooking.Domain/Slots/SlotCapacity.cs (rename to src/EventBooking.Domain/Events/EventCapacity.cs)
- Modify: src/EventBooking.Domain/Slots/SlotProposal.cs (rename to src/EventBooking.Domain/Events/EventProposal.cs)
- Modify: src/EventBooking.Domain/Slots/SlotProposalStatus.cs (rename to src/EventBooking.Domain/Events/EventProposalStatus.cs)
- Modify: src/EventBooking.Domain/Slots/SlotWindow.cs (rename to src/EventBooking.Domain/Events/EventWindow.cs)
- Modify: src/EventBooking.Infrastructure/DependencyInjection.cs
- Modify: src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs (rename to src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs (rename to src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeRequirementConfiguration.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs (rename to src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs (rename to src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs (rename to src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs (rename to src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs (rename to src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.Designer.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.Designer.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.Designer.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.Designer.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.Designer.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.Designer.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.Designer.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.Designer.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.cs (rename to src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.Designer.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/AppointmentWorkspaceQueries.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/AuditQueries.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/CandidateBookingQueries.cs (rename to src/EventBooking.Infrastructure/Persistence/Queries/AttendeeBookingQueries.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/CandidateReadinessQueries.cs (rename to src/EventBooking.Infrastructure/Persistence/Queries/AttendeeReadinessQueries.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs (rename to src/EventBooking.Infrastructure/Persistence/Repositories/AttendeeGroupRepository.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs
- Modify: src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs (rename to src/EventBooking.Infrastructure/Persistence/Repositories/EventCapacityRepository.cs)
- Modify: src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs
- Modify: src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs (rename to src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs)
- Modify: src/EventBooking.Infrastructure/Time/SystemClock.cs
- Modify: src/EventBooking.Infrastructure/Tokens/TokenOptions.cs
- Modify: src/EventBooking.Mcp/Program.cs
- Modify: src/EventBooking.Mcp/Tools/CandidateTools.cs (rename to src/EventBooking.Mcp/Tools/AttendeeTools.cs)
- Modify: src/EventBooking.Mcp/Tools/OperationsTools.cs
- Modify: src/EventBooking.Mcp/Tools/SlotTools.cs (rename to src/EventBooking.Mcp/Tools/EventTools.cs)
- Modify: src/EventBooking.Mcp/appsettings.Local.json
- Modify: src/EventBooking.Mcp/appsettings.json
- Modify: src/EventBooking.SeedData/DemoEmailOptions.cs
- Modify: src/EventBooking.SeedData/DemoInvitationSeeder.cs
- Modify: src/EventBooking.SeedData/DemoSeedSpec.cs
- Modify: src/EventBooking.SeedData/DemoSeeder.cs
- Modify: src/EventBooking.SeedData/Program.cs
- Modify: src/EventBooking.SeedData/demo-seed.json
- Modify: src/EventBooking.Web/Layout/CandidateLayout.razor (rename to src/EventBooking.Web/Layout/AttendeeLayout.razor)
- Modify: src/EventBooking.Web/Layout/MainLayout.razor
- Modify: src/EventBooking.Web/Pages/Appointments.razor
- Modify: src/EventBooking.Web/Pages/Appointments.razor.css
- Modify: src/EventBooking.Web/Pages/Audit.razor
- Modify: src/EventBooking.Web/Pages/Book.razor
- Modify: src/EventBooking.Web/Pages/Candidates.razor (rename to src/EventBooking.Web/Pages/Attendees.razor)
- Modify: src/EventBooking.Web/Pages/Candidates.razor.css (rename to src/EventBooking.Web/Pages/Attendees.razor.css)
- Modify: src/EventBooking.Web/Pages/ConfirmedSlots.razor (rename to src/EventBooking.Web/Pages/EventOperations.razor)
- Modify: src/EventBooking.Web/Pages/ConfirmedSlots.razor.css (rename to src/EventBooking.Web/Pages/EventOperations.razor.css)
- Modify: src/EventBooking.Web/Pages/Dashboards.razor
- Modify: src/EventBooking.Web/Pages/Help.razor
- Modify: src/EventBooking.Web/Pages/Home.razor
- Modify: src/EventBooking.Web/Pages/ManageBooking.razor
- Modify: src/EventBooking.Web/Pages/Settings.razor
- Modify: src/EventBooking.Web/Pages/Slots.razor (rename to src/EventBooking.Web/Pages/EventNegotiation.razor)
- Modify: src/EventBooking.Web/Pages/Slots.razor.css (rename to src/EventBooking.Web/Pages/EventNegotiation.razor.css)
- Modify: src/EventBooking.Web/Program.cs
- Modify: src/EventBooking.Web/Services/AppointmentsClient.cs
- Modify: src/EventBooking.Web/Services/AuditClient.cs
- Modify: src/EventBooking.Web/Services/BookingClient.cs
- Modify: src/EventBooking.Web/Services/CandidatePresentation.cs (rename to src/EventBooking.Web/Services/AttendeePresentation.cs)
- Modify: src/EventBooking.Web/Services/CandidatesClient.cs (rename to src/EventBooking.Web/Services/AttendeesClient.cs)
- Modify: src/EventBooking.Web/Services/ConfirmedSlotsClient.cs (rename to src/EventBooking.Web/Services/EventOperationsClient.cs)
- Modify: src/EventBooking.Web/Services/DashboardsClient.cs
- Modify: src/EventBooking.Web/Services/HeadOfficePageClock.cs (rename to src/EventBooking.Web/Services/TransitionalLocationPageClock.cs)
- Modify: src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs (rename to src/EventBooking.Web/Services/TransitionalLocationTimePresentation.cs)
- Modify: src/EventBooking.Web/Services/SlotsClient.cs (rename to src/EventBooking.Web/Services/EventsClient.cs)
- Modify: src/EventBooking.Web/Services/StaffNavigation.cs
- Modify: src/EventBooking.Web/Services/UserGuideCatalog.cs
- Modify: src/EventBooking.Web/Shared/AuditHistory.razor
- Modify: src/EventBooking.Web/wwwroot/appsettings.json
- Modify: src/EventBooking.Web/wwwroot/css/app.css
- Modify: src/EventBooking.Web/wwwroot/index.html
- Modify: tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs
- Modify: tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs
- Modify: tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AuditEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs
- Modify: tests/EventBooking.Api.Tests/BookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs (rename to tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs)
- Modify: tests/EventBooking.Api.Tests/CandidateEndpointTests.cs (rename to tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs)
- Modify: tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs (rename to tests/EventBooking.Api.Tests/AttendeeHypermediaTests.cs)
- Modify: tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs (rename to tests/EventBooking.Api.Tests/AttendeeReadinessEndpointTests.cs)
- Modify: tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs (rename to tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs)
- Modify: tests/EventBooking.Api.Tests/DashboardEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/HealthTests.cs
- Modify: tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/OpenApiContractTests.cs
- Modify: tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs
- Modify: tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs
- Modify: tests/EventBooking.Api.Tests/SlotEndpointTests.cs (rename to tests/EventBooking.Api.Tests/EventEndpointTests.cs)
- Modify: tests/EventBooking.Api.Tests/StaffHypermediaTests.cs
- Modify: tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs (rename to tests/EventBooking.Application.Tests/Access/AdminAttendeeDataIsolationTests.cs)
- Modify: tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs
- Modify: tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/CancelCandidateBookingHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Bookings/ConfirmBookingHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteOptionReplacementTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/InviteSnapshotAuthorityTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/RecoveryBookingLifecycleTests.cs
- Modify: tests/EventBooking.Application.Tests/Bookings/ViewInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Candidates/ActiveBookingRequirementTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/CandidateCsvParserTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/AttendeeCsvParserTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/CandidateEmployeeGroupFlowTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/CandidateReadinessCalculatorTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/DeleteCandidateHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/EmployeeGroupLifecycleTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/GetCandidateBookingsHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/GetAttendeeBookingsHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/GetCandidateReadinessHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/GetAttendeeReadinessHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/ImportCandidatesHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/ListCandidatesHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Candidates/SaveCandidateHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Common/ResultTests.cs
- Modify: tests/EventBooking.Application.Tests/Dashboards/AuditPortShapeTests.cs
- Modify: tests/EventBooking.Application.Tests/Dashboards/GetAuditSearchHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Dashboards/GetDashboardsHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Dashboards/GetEventOperationsHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Fakes/FakeClock.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs
- Modify: tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs
- Modify: tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs (rename to tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs)
- Modify: tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/CandidateEmailComposerTests.cs (rename to tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs
- Modify: tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs
- Modify: tests/EventBooking.Application.Tests/Slots/AcceptProposalHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/AcceptProposalHeadcountRevisionTests.cs (rename to tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/AdjustConfirmedSlotCapacityHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/CancelConfirmedSlotHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/CombinedManagerAuthorizationTests.cs (rename to tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/ConfirmedSlotImportParserTests.cs (rename to tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/GetManagerSlotBoardHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/ImportConfirmedSlotsHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/ManagerSlotBoardHeadcountRevisionTests.cs (rename to tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/ProposeSlotHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/SharedSlotAuthorizationTests.cs (rename to tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/SlotCancellationConcurrencyTests.cs (rename to tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/WithdrawAcceptanceHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs)
- Modify: tests/EventBooking.Application.Tests/Slots/WithdrawProposalHandlerTests.cs (rename to tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Access/StaffAccessAuditVocabularyTests.cs
- Modify: tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs
- Modify: tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs
- Modify: tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs
- Modify: tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs (rename to tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs (rename to tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs (rename to tests/EventBooking.Domain.Tests/Attendees/RequirementOverrideSurfaceTests.cs)
- Modify: tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs (rename to tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Invites/InviteTests.cs
- Modify: tests/EventBooking.Domain.Tests/OntologyEnumTests.cs
- Modify: tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotCancellationTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotImportTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventImportTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/ConfirmedSlotTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/ProposalAcceptanceHeadcountRevisionTests.cs (rename to tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/SlotCapacityHeadcountAdjustmentTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/SlotCapacityTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/SlotProposalAcceptanceTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/SlotProposalConfirmationTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/SlotProposalTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs)
- Modify: tests/EventBooking.Domain.Tests/Slots/SlotWindowTests.cs (rename to tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AppointmentWorkspaceRecentPastTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/AuditQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/BookingAppointmentPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/CandidateBookingCancellationPersistenceTests.cs (rename to tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/CandidateBookingQueryTests.cs (rename to tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/CandidateReadinessQueryTests.cs (rename to tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/CapacityAdjustmentConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/ConcurrencyHarness.cs
- Modify: tests/EventBooking.Infrastructure.Tests/ConfirmedSlotPersistenceTests.cs (rename to tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/DashboardQueryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/DurableEmailDeliveryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/EfAuditLoggerTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/EmployeeGroupPersistenceTests.cs (rename to tests/EventBooking.Infrastructure.Tests/AttendeeGroupPersistenceTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/EmployeeGroupRequiredMigrationTests.cs (rename to tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/LoggingEmailSenderTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/NoOverbookingTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/PostgresFixture.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RecoveryBookingPersistenceTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RecoveryConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepairCConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/RepositoryTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SchemaTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SlotCapacityRepositoryTests.cs (rename to tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs)
- Modify: tests/EventBooking.Infrastructure.Tests/StaffAccessConcurrencyTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/StaffAccessMigrationTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs
- Modify: tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs
- Modify: tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs (rename to tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs)
- Modify: tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs (rename to tests/EventBooking.Mcp.Tests/AttendeeParityMcpTests.cs)
- Modify: tests/EventBooking.Mcp.Tests/McpEndpointTests.cs
- Modify: tests/EventBooking.Mcp.Tests/McpFactory.cs
- Modify: tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs
- Modify: tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs
- Modify: tests/EventBooking.Mcp.Tests/SlotMcpTests.cs (rename to tests/EventBooking.Mcp.Tests/EventMcpTests.cs)
- Modify: tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs
- Modify: tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs
- Modify: tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs (rename to tests/EventBooking.SeedData.Tests/AttendeeGroupJourneySeedTests.cs)
- Modify: tests/EventBooking.SeedData.Tests/ReanchorTests.cs
- Modify: tests/EventBooking.SeedData.Tests/ReseedTests.cs
- Modify: tests/EventBooking.Web.Tests/AppointmentsClientTests.cs
- Modify: tests/EventBooking.Web.Tests/AppointmentsComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs
- Modify: tests/EventBooking.Web.Tests/AuditClientTests.cs
- Modify: tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/AuditPageTests.cs
- Modify: tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs
- Modify: tests/EventBooking.Web.Tests/BookingClientTests.cs
- Modify: tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs (rename to tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs)
- Modify: tests/EventBooking.Web.Tests/CandidateLayoutTests.cs (rename to tests/EventBooking.Web.Tests/AttendeeLayoutTests.cs)
- Modify: tests/EventBooking.Web.Tests/CandidatePresentationTests.cs (rename to tests/EventBooking.Web.Tests/AttendeePresentationTests.cs)
- Modify: tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs (rename to tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs)
- Modify: tests/EventBooking.Web.Tests/CandidatesClientTests.cs (rename to tests/EventBooking.Web.Tests/AttendeesClientTests.cs)
- Modify: tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs (rename to tests/EventBooking.Web.Tests/EventOperationsClientTests.cs)
- Modify: tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs (rename to tests/EventBooking.Web.Tests/EventsPageTests.cs)
- Modify: tests/EventBooking.Web.Tests/DashboardsClientTests.cs
- Modify: tests/EventBooking.Web.Tests/DashboardsComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/HelpPageTests.cs
- Modify: tests/EventBooking.Web.Tests/HomePageTests.cs
- Modify: tests/EventBooking.Web.Tests/MainLayoutTests.cs
- Modify: tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs
- Modify: tests/EventBooking.Web.Tests/SettingsTests.cs
- Modify: tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs (rename to tests/EventBooking.Web.Tests/EventsClientCapacityAdjustmentTests.cs)
- Modify: tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs (rename to tests/EventBooking.Web.Tests/EventsClientHeadcountRevisionTests.cs)
- Modify: tests/EventBooking.Web.Tests/SlotsClientTests.cs (rename to tests/EventBooking.Web.Tests/EventsClientTests.cs)
- Modify: tests/EventBooking.Web.Tests/StaffNavigationTests.cs
- Modify: tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs
- Test: tests/EventBooking.Api.Tests/VocabularyContractTests.cs
- Test: tests/EventBooking.Api.Tests/VocabularySurfaceTests.cs
- Test: tests/EventBooking.Mcp.Tests/VocabularyToolTests.cs

**Interfaces:**

Consumes Task 1's complete port. Produces these application persistence contracts under the canonical namespace; complete domain types and every other modified public contract are included in the after files.

```csharp
using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent repository for the current use case.</summary>
public interface IEventRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the transactional write guard for a event and returns its current state.
    /// Confirmation and every cancellation path that can change bookings on the event must take
    /// this guard before reading booking or capacity state for that eventItem.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Event?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Active events whose window falls on or after the given date, capacities loaded.</summary>
    /// <param name="onOrAfter">The on or after.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListActiveAsync(DateOnly onOrAfter, CancellationToken cancellationToken);

    /// <summary>Every eventItem, cancelled ones included — for the coordinator's events overview.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="eventItem">The eventItem.</param>
    void Add(Event eventItem);
}

using EventBooking.Domain.Events;

namespace EventBooking.Application.Abstractions;

/// <summary>Defines ievent proposal repository for the current use case.</summary>
public interface IEventProposalRepository
{
    /// <summary>Provides get async within this contract.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventProposal?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the row-level write lock for an existing proposal and loads its acceptances. Proposal
    /// lifecycle mutations must call this inside their unit-of-work transaction before observing
    /// status or changing an acceptance.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<EventProposal?> LockForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Provides list open async within this contract.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<EventProposal>> ListOpenAsync(CancellationToken cancellationToken);

    /// <summary>Provides add within this contract.</summary>
    /// <param name="proposal">The proposal.</param>
    void Add(EventProposal proposal);
}
```

**Context you need**

- The vocabulary table maps the predecessor person to Attendee, group to AttendeeGroup, proposal to EventProposal, booked window to Event and capacity counter to EventCapacity.
- EventWindow is still the predecessor fixed-duration shape here; Task 4 changes time behaviour.
- EventCapacity still has one row per existing fixed type here; Task 7 generalises the type set.
- Ontology invariant: remainingCapacity is never below zero or above totalHeadcount.
- Ontology invariant: changing an AttendeeGroup derives AttendeeRequirement rather than accepting arbitrary attendee requirements.
- REST base paths become /api/attendees, /api/event-proposals and /api/events.
- Web pages use /attendees, /events/negotiate and /events/operations.
- Email templates use AttendeeInvite, AttendeeReinvite and EventCancelledRebookingNeeded; BookingConfirmation is unchanged.
- Audit actions and staff capabilities use the exact §3 mapping; retired import members are removed in Task 3.
- No ontology addition is needed: these canonical concepts are already defined. Do not create aliases for the old terminology.
- TransitionalLocation is a temporary configuration/clock adapter name, not a new domain concept; it is replaced during Tasks 3–4.

- [ ] **Step 1: Write the failing tests**

Create these three complete test files before changing production code.

tests/EventBooking.Api.Tests/VocabularyContractTests.cs

```csharp
using System.Reflection;

namespace EventBooking.Api.Tests;

public sealed class VocabularyContractTests
{
    [Fact]
    public void Public_domain_and_application_contracts_use_canonical_names()
    {
        string[] forbidden = ["Candi" + "date", "Slo" + "t", "Employee" + "Group", "Head" + "Office"];
        var names = new[] { "EventBooking.Domain", "EventBooking.Application" }
            .Select(Assembly.Load)
            .SelectMany(assembly => assembly.GetExportedTypes())
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(member => type.FullName + "." + member.Name).Append(type.FullName!));
        Assert.DoesNotContain(names, name => forbidden.Any(term => name.Contains(term, StringComparison.Ordinal)));
    }
}
```

tests/EventBooking.Api.Tests/VocabularySurfaceTests.cs

```csharp
using System.Text.Json;

namespace EventBooking.Api.Tests;

[Collection("api")]
public sealed class VocabularySurfaceTests(ApiFactory factory)
{
    [Fact]
    public async Task OpenApi_paths_and_schemas_use_canonical_vocabulary()
    {
        using var document = JsonDocument.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas")
            .EnumerateObject().Select(p => p.Name);
        string[] retired = ["candi" + "date", "slo" + "t", "employee" + "group", "head" + "office"];
        Assert.DoesNotContain(paths.Concat(schemas), name => retired.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("/api/attendees", paths);
        Assert.Contains("/api/event-proposals", paths);
        Assert.Contains("/api/events/{id}", paths);
    }
}
```

tests/EventBooking.Mcp.Tests/VocabularyToolTests.cs

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EventBooking.Domain.Access;

namespace EventBooking.Mcp.Tests;

[Collection("mcp")]
public sealed class VocabularyToolTests(McpFactory factory)
{
    [Fact]
    public async Task Advertised_tools_use_canonical_vocabulary()
    {
        factory.SignedInAs = await factory.GivenStaffAsync([Role.Admin], null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":\"names\",\"method\":\"tools/list\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await factory.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var data = body.TrimStart().StartsWith('{') ? body : body.Split('\n').Select(x => x.Trim())
            .Last(x => x.StartsWith("data: "))["data: ".Length..];
        using var json = JsonDocument.Parse(data);
        var names = json.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!).ToArray();
        Assert.NotEmpty(names);
        string[] retired = ["candi" + "date", "slo" + "t", "employee" + "group", "head" + "office"];
        Assert.DoesNotContain(names, name => retired.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }
}
```

- [ ] **Step 2: Verify the naming tests fail**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~Vocabulary
dotnet test tests/EventBooking.Mcp.Tests --filter FullyQualifiedName~VocabularyToolTests
```

Expected: three failing tests against Task 1's old contracts. REST paths lack the canonical route bases and advertised names still contain predecessor words. A container-startup or compiler error is not the expected red result.

- [ ] **Step 3: Apply the exact before and after edits**

The 115 phase-0b-edits-NNN.md sections contain all 399 edits in full. Match the before side; apply the after side. The following command checks hashes and complete part sequences before changing any file. It refuses unrelated changes and is safe to rerun after a partially completed write. Old paths are removed only after their replacement contents have been verified and written; Git retains the previous version in Task 1's commit.

```bash
node --input-type=module <<'VOCABULARY_NODE'
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const plan='docs/detailed-implementations';
const root=fs.realpathSync('.');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const entries=new Map();
const names=fs.readdirSync(plan).filter(n=>/^phase-0b-edits-\d{3}\.md$/.test(n)).sort();
if(names.length!==115)throw Error('Expected 115 edit sections.');
for(const name of names){
  const source=fs.readFileSync(path.join(plan,name),'utf8');
  const pattern=/<!-- vocabulary-file: (.+) -->\n\n`{5}[^\n]*\n([\s\S]*?)\n`{5}/g;
  for(const match of source.matchAll(pattern)){
    const part=JSON.parse(match[1]);
    for(const file of [part.oldPath,part.newPath].filter(Boolean)){
      if(path.isAbsolute(file)||file.split('/').includes('..'))throw Error('Unsafe path.');
    }
    const entry=entries.get(part.id)??{...part,before:new Map(),after:new Map(),counts:{}};
    if(entry[part.side].has(part.part))throw Error('Duplicate part.');
    if(entry.newPath!==part.newPath||entry.afterSha!==part.afterSha)throw Error('Mismatched metadata.');
    entry[part.side].set(part.part,match[2]+'\n');entry.counts[part.side]=part.parts;
    entries.set(part.id,entry);
  }
}
if(entries.size!==399)throw Error('Incomplete edit set.');
const writes=[],removals=[];
for(const entry of entries.values()){
  for(const side of ['before','after']){
    if(!entry.counts[side])continue;
    if(entry[side].size!==entry.counts[side])throw Error('Missing part.');
    entry[side+'Text']=Array.from({length:entry.counts[side]},(_,i)=>entry[side].get(i+1)).join('');
    if(sha(entry[side+'Text'])!==entry[side+'Sha'])throw Error('Payload checksum mismatch.');
  }
  const old=entry.oldPath?path.join(root,entry.oldPath):null;
  const target=path.join(root,entry.newPath);
  for(const file of [old,target].filter(Boolean)){
    let parent=path.dirname(file);while(!fs.existsSync(parent))parent=path.dirname(parent);
    if(!fs.realpathSync(parent).startsWith(root+path.sep)&&fs.realpathSync(parent)!==root)throw Error('Parent escapes checkout.');
    if(fs.existsSync(file)&&fs.lstatSync(file).isSymbolicLink())throw Error('Symlink target.');
  }
  const targetHash=fs.existsSync(target)?sha(fs.readFileSync(target)):null;
  if(old&&fs.existsSync(old)){
    const actual=sha(fs.readFileSync(old));
    if(actual!==entry.beforeSha&&!(old===target&&actual===entry.afterSha))throw Error('Before mismatch: '+entry.oldPath);
    if(old!==target)removals.push(old);
  }else if(old&&targetHash!==entry.afterSha)throw Error('Missing before file: '+entry.oldPath);
  if(targetHash!==null&&targetHash!==entry.afterSha&&!(old===target&&targetHash===entry.beforeSha))throw Error('Target has unrelated edits: '+entry.newPath);
  writes.push([target,entry.afterText]);
}
for(const [target,body] of writes){fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,body);}
for(const old of removals)fs.unlinkSync(old);
console.log('Applied '+writes.length+' verified edits and removed '+removals.length+' superseded file paths.');
VOCABULARY_NODE
```

- [ ] **Step 4: Verify the public vocabulary**

```bash
dotnet test tests/EventBooking.Api.Tests --filter FullyQualifiedName~Vocabulary
dotnet test tests/EventBooking.Mcp.Tests --filter FullyQualifiedName~VocabularyToolTests
```

Expected: two API tests and one MCP test pass, zero skipped. STOP AND CHECK: do not merely change test expectations. The tests retrieve the real generated OpenAPI document and MCP tools/list response, and inspect the actual Domain/Application assemblies.

- [ ] **Step 5: Build and run all affected tests**

```bash
dotnet build EventBooking.sln -warnaserror
dotnet test EventBooking.sln
```

Expected: zero warnings/errors; 1427 passed, zero failed or skipped: Domain 230, Application 451, Infrastructure 157, API 228, MCP 35, Web 251 and SeedData 75. One inherited MCP identity test now explicitly clears and restores role claims so test order cannot turn its unassigned-caller fixture into an administrator. No production authorization change is made.

- [ ] **Step 6: Commit and push**

```bash
git add -- \
  'docs/user-guides/README.md' \
  'docs/user-guides/admin-guide.md' \
  'docs/user-guides/appointment-staff-guide.md' \
  'docs/user-guides/attendee-guide.md' \
  'docs/user-guides/candidate-guide.md' \
  'docs/user-guides/coordinator-guide.md' \
  'docs/user-guides/manager-guide.md' \
  'src/EventBooking.Api/Auth/RemoteIpRateLimiterPolicy.cs' \
  'src/EventBooking.Api/Contracts/AdministrationHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/AttendeeHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/CandidateHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/EventHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/OperationsHypermediaResponses.cs' \
  'src/EventBooking.Api/Contracts/SlotHypermediaResponses.cs' \
  'src/EventBooking.Api/Endpoints/AdminEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/ApiDiscoveryEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/AppointmentWorkspaceEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/AttendeeEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/AuditEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/BookingEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/CandidateEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/EventEndpoints.cs' \
  'src/EventBooking.Api/Endpoints/ResultResponses.cs' \
  'src/EventBooking.Api/Endpoints/SlotEndpoints.cs' \
  'src/EventBooking.Api/EventBookingConfiguration.cs' \
  'src/EventBooking.Api/InviteSweepService.cs' \
  'src/EventBooking.Api/OpenApi/AgentOperationCatalog.cs' \
  'src/EventBooking.Api/OpenApi/OpenApiConfiguration.cs' \
  'src/EventBooking.Api/Program.cs' \
  'src/EventBooking.Api/appsettings.Local.json' \
  'src/EventBooking.Api/appsettings.json' \
  'src/EventBooking.Application/Abstractions/IAppointmentWorkspaceQueries.cs' \
  'src/EventBooking.Application/Abstractions/IAttendeeBookingQueries.cs' \
  'src/EventBooking.Application/Abstractions/IAttendeeGroupRepository.cs' \
  'src/EventBooking.Application/Abstractions/IAttendeeReadinessQueries.cs' \
  'src/EventBooking.Application/Abstractions/IAttendeeRepository.cs' \
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
  'src/EventBooking.Application/Abstractions/IEventCapacityRepository.cs' \
  'src/EventBooking.Application/Abstractions/IEventProposalRepository.cs' \
  'src/EventBooking.Application/Abstractions/IEventRepository.cs' \
  'src/EventBooking.Application/Abstractions/IInviteRepository.cs' \
  'src/EventBooking.Application/Abstractions/ISlotCapacityRepository.cs' \
  'src/EventBooking.Application/Abstractions/ISlotProposalRepository.cs' \
  'src/EventBooking.Application/Access/StaffAccessAuthorizer.cs' \
  'src/EventBooking.Application/Access/StaffCapability.cs' \
  'src/EventBooking.Application/Access/SyncStaffAccessProfileRolesHandler.cs' \
  'src/EventBooking.Application/Appointments/AppointmentRosterCsvFormatter.cs' \
  'src/EventBooking.Application/Appointments/AppointmentWorkspaceAllowance.cs' \
  'src/EventBooking.Application/Appointments/AppointmentWorkspaceModels.cs' \
  'src/EventBooking.Application/Appointments/GetAppointmentWorkspaceHandler.cs' \
  'src/EventBooking.Application/Appointments/UpdateBookingAppointmentStatusHandler.cs' \
  'src/EventBooking.Application/Attendees/AttendeeCsvParser.cs' \
  'src/EventBooking.Application/Attendees/AttendeeGroupModels.cs' \
  'src/EventBooking.Application/Attendees/AttendeeReadiness.cs' \
  'src/EventBooking.Application/Attendees/AttendeeReadinessCalculator.cs' \
  'src/EventBooking.Application/Attendees/DeleteAttendeeHandler.cs' \
  'src/EventBooking.Application/Attendees/GetAttendeeBookingsHandler.cs' \
  'src/EventBooking.Application/Attendees/GetAttendeeReadinessHandler.cs' \
  'src/EventBooking.Application/Attendees/ImportAttendeesHandler.cs' \
  'src/EventBooking.Application/Attendees/ListAttendeeGroupsHandler.cs' \
  'src/EventBooking.Application/Attendees/ListAttendeesHandler.cs' \
  'src/EventBooking.Application/Attendees/SaveAttendeeHandler.cs' \
  'src/EventBooking.Application/Bookings/BookingCanceller.cs' \
  'src/EventBooking.Application/Bookings/CancelAttendeeBookingHandler.cs' \
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
  'src/EventBooking.Application/Dashboards/GetAuditHistoryHandler.cs' \
  'src/EventBooking.Application/Dashboards/GetAuditSearchHandler.cs' \
  'src/EventBooking.Application/Dashboards/GetDashboardsHandler.cs' \
  'src/EventBooking.Application/Dashboards/GetEventOperationsHandler.cs' \
  'src/EventBooking.Application/Dashboards/GetSlotOperationsHandler.cs' \
  'src/EventBooking.Application/DependencyInjection.cs' \
  'src/EventBooking.Application/Events/AcceptProposalHandler.cs' \
  'src/EventBooking.Application/Events/AdjustEventCapacityHandler.cs' \
  'src/EventBooking.Application/Events/CancelEventHandler.cs' \
  'src/EventBooking.Application/Events/EventImportParser.cs' \
  'src/EventBooking.Application/Events/GetManagerEventBoardHandler.cs' \
  'src/EventBooking.Application/Events/ImportEventsHandler.cs' \
  'src/EventBooking.Application/Events/ProposeEventHandler.cs' \
  'src/EventBooking.Application/Events/WithdrawAcceptanceHandler.cs' \
  'src/EventBooking.Application/Events/WithdrawProposalHandler.cs' \
  'src/EventBooking.Application/Invites/CancelRecoveryInviteHandler.cs' \
  'src/EventBooking.Application/Invites/EligibleEventFinder.cs' \
  'src/EventBooking.Application/Invites/EligibleSlotFinder.cs' \
  'src/EventBooking.Application/Invites/ExpireInvitesHandler.cs' \
  'src/EventBooking.Application/Invites/InviteIssuer.cs' \
  'src/EventBooking.Application/Invites/RecoveryRequirementSelector.cs' \
  'src/EventBooking.Application/Invites/StartRecoveryHandler.cs' \
  'src/EventBooking.Application/Invites/TriggerInviteHandler.cs' \
  'src/EventBooking.Application/Notifications/AttendeeEmailComposer.cs' \
  'src/EventBooking.Application/Notifications/AttendeePortalOptions.cs' \
  'src/EventBooking.Application/Notifications/CandidateEmailComposer.cs' \
  'src/EventBooking.Application/Notifications/CandidatePortalOptions.cs' \
  'src/EventBooking.Application/Notifications/EmailDeliveryService.cs' \
  'src/EventBooking.Application/Notifications/RetryEmailHandler.cs' \
  'src/EventBooking.Application/Slots/AcceptProposalHandler.cs' \
  'src/EventBooking.Application/Slots/AdjustConfirmedSlotCapacityHandler.cs' \
  'src/EventBooking.Application/Slots/CancelConfirmedSlotHandler.cs' \
  'src/EventBooking.Application/Slots/ConfirmedSlotImportParser.cs' \
  'src/EventBooking.Application/Slots/GetManagerSlotBoardHandler.cs' \
  'src/EventBooking.Application/Slots/ImportConfirmedSlotsHandler.cs' \
  'src/EventBooking.Application/Slots/ProposeSlotHandler.cs' \
  'src/EventBooking.Application/Slots/WithdrawAcceptanceHandler.cs' \
  'src/EventBooking.Application/Slots/WithdrawProposalHandler.cs' \
  'src/EventBooking.Domain/AttendeeGroups/AttendeeGroup.cs' \
  'src/EventBooking.Domain/AttendeeGroups/AttendeeGroupIds.cs' \
  'src/EventBooking.Domain/AttendeeGroups/AttendeeGroupRequirement.cs' \
  'src/EventBooking.Domain/Attendees/Attendee.cs' \
  'src/EventBooking.Domain/Attendees/AttendeeRequirement.cs' \
  'src/EventBooking.Domain/Attendees/AttendeeStatus.cs' \
  'src/EventBooking.Domain/Audit/ActorType.cs' \
  'src/EventBooking.Domain/Audit/AuditAction.cs' \
  'src/EventBooking.Domain/Audit/AuditEntityTypes.cs' \
  'src/EventBooking.Domain/Audit/AuditLog.cs' \
  'src/EventBooking.Domain/Bookings/Booking.cs' \
  'src/EventBooking.Domain/Bookings/BookingAppointment.cs' \
  'src/EventBooking.Domain/Bookings/BookingAppointmentStatus.cs' \
  'src/EventBooking.Domain/Candidates/Candidate.cs' \
  'src/EventBooking.Domain/Candidates/CandidateRequirement.cs' \
  'src/EventBooking.Domain/Candidates/CandidateStatus.cs' \
  'src/EventBooking.Domain/EmployeeGroups/EmployeeGroup.cs' \
  'src/EventBooking.Domain/EmployeeGroups/EmployeeGroupIds.cs' \
  'src/EventBooking.Domain/EmployeeGroups/EmployeeGroupRequirement.cs' \
  'src/EventBooking.Domain/Events/Event.cs' \
  'src/EventBooking.Domain/Events/EventCapacity.cs' \
  'src/EventBooking.Domain/Events/EventProposal.cs' \
  'src/EventBooking.Domain/Events/EventProposalStatus.cs' \
  'src/EventBooking.Domain/Events/EventStatus.cs' \
  'src/EventBooking.Domain/Events/EventWindow.cs' \
  'src/EventBooking.Domain/Events/ProposalAcceptance.cs' \
  'src/EventBooking.Domain/Invites/Invite.cs' \
  'src/EventBooking.Domain/Invites/InviteOption.cs' \
  'src/EventBooking.Domain/Invites/InviteStatus.cs' \
  'src/EventBooking.Domain/Notifications/EmailLog.cs' \
  'src/EventBooking.Domain/Notifications/EmailStatus.cs' \
  'src/EventBooking.Domain/Notifications/EmailTemplate.cs' \
  'src/EventBooking.Domain/Slots/ConfirmedSlot.cs' \
  'src/EventBooking.Domain/Slots/ConfirmedSlotStatus.cs' \
  'src/EventBooking.Domain/Slots/ProposalAcceptance.cs' \
  'src/EventBooking.Domain/Slots/SlotCapacity.cs' \
  'src/EventBooking.Domain/Slots/SlotProposal.cs' \
  'src/EventBooking.Domain/Slots/SlotProposalStatus.cs' \
  'src/EventBooking.Domain/Slots/SlotWindow.cs' \
  'src/EventBooking.Infrastructure/DependencyInjection.cs' \
  'src/EventBooking.Infrastructure/Email/LoggingEmailSender.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeGroupRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/AttendeeRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/BookingConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/CandidateRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/ConfirmedSlotConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EmailLogConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EmployeeGroupRequirementConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventCapacityConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/EventProposalConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/InviteOptionConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/ProposalAcceptanceConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/SlotCapacityConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/Configurations/SlotProposalConfiguration.cs' \
  'src/EventBooking.Infrastructure/Persistence/EventBookingDbContext.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905060413_InitialSchema.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_AttendeeStatusChangedAt.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905163200_CandidateStatusChangedAt.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeConfirmedSlotProposalIdNullable.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260905192057_MakeEventProposalIdNullable.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260906120000_ScopedMultiRoleAuthorization.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907125759_RepairCConcurrencyBackstops.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907180547_DurableEmailDelivery.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260907204442_AddBookingAppointments.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260908211939_AddStaffIdentity.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddAttendeeGroupsAndAttendeeAssociation.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909100000_AddEmployeeGroupsAndCandidateAssociation.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909110000_AddInviteRequirementSnapshots.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909120000_AddRecoveryBookings.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireAttendeeAttendeeGroup.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260909130000_RequireCandidateEmployeeGroup.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260911120000_RelaxStaffAccessProfileScopeConstraint.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916045451_AddAuditLogTimestampIndex.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/20260916052707_AddStaffIdentityDisplayName.Designer.cs' \
  'src/EventBooking.Infrastructure/Persistence/Migrations/EventBookingDbContextModelSnapshot.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/AppointmentWorkspaceQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/AttendeeBookingQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/AttendeeReadinessQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/AuditQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/CandidateBookingQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/CandidateReadinessQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Queries/DashboardQueries.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/AttendeeGroupRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/BookingAppointmentRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/EmployeeGroupRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/EventCapacityRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/Repositories.cs' \
  'src/EventBooking.Infrastructure/Persistence/Repositories/SlotCapacityRepository.cs' \
  'src/EventBooking.Infrastructure/Persistence/StatusStampingInterceptor.cs' \
  'src/EventBooking.Infrastructure/Time/HeadOfficeOptions.cs' \
  'src/EventBooking.Infrastructure/Time/SystemClock.cs' \
  'src/EventBooking.Infrastructure/Time/TransitionalLocationOptions.cs' \
  'src/EventBooking.Infrastructure/Tokens/TokenOptions.cs' \
  'src/EventBooking.Mcp/Program.cs' \
  'src/EventBooking.Mcp/Tools/AttendeeTools.cs' \
  'src/EventBooking.Mcp/Tools/CandidateTools.cs' \
  'src/EventBooking.Mcp/Tools/EventTools.cs' \
  'src/EventBooking.Mcp/Tools/OperationsTools.cs' \
  'src/EventBooking.Mcp/Tools/SlotTools.cs' \
  'src/EventBooking.Mcp/appsettings.Local.json' \
  'src/EventBooking.Mcp/appsettings.json' \
  'src/EventBooking.SeedData/DemoEmailOptions.cs' \
  'src/EventBooking.SeedData/DemoInvitationSeeder.cs' \
  'src/EventBooking.SeedData/DemoSeedSpec.cs' \
  'src/EventBooking.SeedData/DemoSeeder.cs' \
  'src/EventBooking.SeedData/Program.cs' \
  'src/EventBooking.SeedData/demo-seed.json' \
  'src/EventBooking.Web/Layout/AttendeeLayout.razor' \
  'src/EventBooking.Web/Layout/CandidateLayout.razor' \
  'src/EventBooking.Web/Layout/MainLayout.razor' \
  'src/EventBooking.Web/Pages/Appointments.razor' \
  'src/EventBooking.Web/Pages/Appointments.razor.css' \
  'src/EventBooking.Web/Pages/Attendees.razor' \
  'src/EventBooking.Web/Pages/Attendees.razor.css' \
  'src/EventBooking.Web/Pages/Audit.razor' \
  'src/EventBooking.Web/Pages/Book.razor' \
  'src/EventBooking.Web/Pages/Candidates.razor' \
  'src/EventBooking.Web/Pages/Candidates.razor.css' \
  'src/EventBooking.Web/Pages/ConfirmedSlots.razor' \
  'src/EventBooking.Web/Pages/ConfirmedSlots.razor.css' \
  'src/EventBooking.Web/Pages/Dashboards.razor' \
  'src/EventBooking.Web/Pages/EventNegotiation.razor' \
  'src/EventBooking.Web/Pages/EventNegotiation.razor.css' \
  'src/EventBooking.Web/Pages/EventOperations.razor' \
  'src/EventBooking.Web/Pages/EventOperations.razor.css' \
  'src/EventBooking.Web/Pages/Help.razor' \
  'src/EventBooking.Web/Pages/Home.razor' \
  'src/EventBooking.Web/Pages/ManageBooking.razor' \
  'src/EventBooking.Web/Pages/Settings.razor' \
  'src/EventBooking.Web/Pages/Slots.razor' \
  'src/EventBooking.Web/Pages/Slots.razor.css' \
  'src/EventBooking.Web/Program.cs' \
  'src/EventBooking.Web/Services/AppointmentsClient.cs' \
  'src/EventBooking.Web/Services/AttendeePresentation.cs' \
  'src/EventBooking.Web/Services/AttendeesClient.cs' \
  'src/EventBooking.Web/Services/AuditClient.cs' \
  'src/EventBooking.Web/Services/BookingClient.cs' \
  'src/EventBooking.Web/Services/CandidatePresentation.cs' \
  'src/EventBooking.Web/Services/CandidatesClient.cs' \
  'src/EventBooking.Web/Services/ConfirmedSlotsClient.cs' \
  'src/EventBooking.Web/Services/DashboardsClient.cs' \
  'src/EventBooking.Web/Services/EventOperationsClient.cs' \
  'src/EventBooking.Web/Services/EventsClient.cs' \
  'src/EventBooking.Web/Services/HeadOfficePageClock.cs' \
  'src/EventBooking.Web/Services/HeadOfficeTimePresentation.cs' \
  'src/EventBooking.Web/Services/SlotsClient.cs' \
  'src/EventBooking.Web/Services/StaffNavigation.cs' \
  'src/EventBooking.Web/Services/TransitionalLocationPageClock.cs' \
  'src/EventBooking.Web/Services/TransitionalLocationTimePresentation.cs' \
  'src/EventBooking.Web/Services/UserGuideCatalog.cs' \
  'src/EventBooking.Web/Shared/AuditHistory.razor' \
  'src/EventBooking.Web/wwwroot/appsettings.json' \
  'src/EventBooking.Web/wwwroot/css/app.css' \
  'src/EventBooking.Web/wwwroot/index.html' \
  'tests/EventBooking.Api.Tests/AgentOperationCatalogTests.cs' \
  'tests/EventBooking.Api.Tests/ApiDiscoveryTests.cs' \
  'tests/EventBooking.Api.Tests/AppointmentWorkspaceEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeBookingCancellationEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeHypermediaTests.cs' \
  'tests/EventBooking.Api.Tests/AttendeeReadinessEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AuditEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/AuthorizationMatrixTests.cs' \
  'tests/EventBooking.Api.Tests/BookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateBookingCancellationEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateHypermediaTests.cs' \
  'tests/EventBooking.Api.Tests/CandidateReadinessEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/ConfirmedSlotCapacityAdjustmentEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/DashboardEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventCapacityAdjustmentEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/EventEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/HealthTests.cs' \
  'tests/EventBooking.Api.Tests/ManageBookingEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/OpenApiContractTests.cs' \
  'tests/EventBooking.Api.Tests/ProposalAcceptanceRevisionEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/RecentPastWorkspaceBoundaryTests.cs' \
  'tests/EventBooking.Api.Tests/RecoveryInviteEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/SlotEndpointTests.cs' \
  'tests/EventBooking.Api.Tests/StaffHypermediaTests.cs' \
  'tests/EventBooking.Api.Tests/VocabularyContractTests.cs' \
  'tests/EventBooking.Api.Tests/VocabularySurfaceTests.cs' \
  'tests/EventBooking.Application.Tests/Access/AdminAttendeeDataIsolationTests.cs' \
  'tests/EventBooking.Application.Tests/Access/AdminCandidateDataIsolationTests.cs' \
  'tests/EventBooking.Application.Tests/Access/StaffAccessAuthorizerTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/AppointmentRosterCsvFormatterTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/GetAppointmentWorkspaceHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/LateNoShowOutcomeTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecentPastRecoveryEligibilityTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/RecoveryBookingOutcomeCoordinatorTests.cs' \
  'tests/EventBooking.Application.Tests/Appointments/UpdateBookingAppointmentStatusHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ActiveBookingRequirementTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeAttendeeGroupFlowTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeCsvParserTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeGroupLifecycleTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/AttendeeReadinessCalculatorTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/DeleteAttendeeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/GetAttendeeBookingsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/GetAttendeeReadinessHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ImportAttendeesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/ListAttendeesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Attendees/SaveAttendeeHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingAppointmentSnapshotTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/BookingSnapshotCancellationTests.cs' \
  'tests/EventBooking.Application.Tests/Bookings/CancelAttendeeBookingHandlerTests.cs' \
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
  'tests/EventBooking.Application.Tests/Dashboards/GetEventOperationsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Dashboards/GetSlotOperationsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AcceptProposalHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AcceptProposalHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Events/AdjustEventCapacityHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CancelEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/CombinedManagerAuthorizationTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventCancellationConcurrencyTests.cs' \
  'tests/EventBooking.Application.Tests/Events/EventImportParserTests.cs' \
  'tests/EventBooking.Application.Tests/Events/GetManagerEventBoardHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ImportEventsHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ManagerEventBoardHeadcountRevisionTests.cs' \
  'tests/EventBooking.Application.Tests/Events/ProposeEventHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/SharedEventAuthorizationTests.cs' \
  'tests/EventBooking.Application.Tests/Events/WithdrawAcceptanceHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Events/WithdrawProposalHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakeClock.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakeUnitOfWork.cs' \
  'tests/EventBooking.Application.Tests/Fakes/FakesSelfTests.cs' \
  'tests/EventBooking.Application.Tests/Fakes/InMemoryQueries.cs' \
  'tests/EventBooking.Application.Tests/Fakes/InMemoryRepositories.cs' \
  'tests/EventBooking.Application.Tests/Invites/EligibleEventFinderTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/EligibleSlotFinderTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/ExpireInvitesHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/InviteIssuerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/RecoveryInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Invites/TriggerInviteHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/AttendeeEmailComposerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/CandidateEmailComposerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/EmailDeliveryServiceTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/RetryEmailHandlerTests.cs' \
  'tests/EventBooking.Application.Tests/Notifications/SnapshotEmailAuthorityTests.cs' \
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
  'tests/EventBooking.Domain.Tests/AttendeeGroups/AttendeeGroupAttendeeTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/AttendeeStatusTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/AttendeeTests.cs' \
  'tests/EventBooking.Domain.Tests/Attendees/RequirementOverrideSurfaceTests.cs' \
  'tests/EventBooking.Domain.Tests/Audit/AuditLogTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/BookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Bookings/RecoveryBookingTests.cs' \
  'tests/EventBooking.Domain.Tests/Candidates/CandidateStatusTests.cs' \
  'tests/EventBooking.Domain.Tests/Candidates/CandidateTests.cs' \
  'tests/EventBooking.Domain.Tests/Candidates/RequirementOverrideSurfaceTests.cs' \
  'tests/EventBooking.Domain.Tests/EmployeeGroups/EmployeeGroupCandidateTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCancellationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityHeadcountAdjustmentTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventCapacityTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventImportTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalAcceptanceTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalConfirmationTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventProposalTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/EventWindowTests.cs' \
  'tests/EventBooking.Domain.Tests/Events/ProposalAcceptanceHeadcountRevisionTests.cs' \
  'tests/EventBooking.Domain.Tests/Invites/InviteTests.cs' \
  'tests/EventBooking.Domain.Tests/OntologyEnumTests.cs' \
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
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingCancellationPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeBookingQueryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeGroupPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeGroupRequiredMigrationTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/AttendeeReadinessQueryTests.cs' \
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
  'tests/EventBooking.Infrastructure.Tests/EventCapacityRepositoryTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/EventPersistenceTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/InviteRequirementPersistenceTests.cs' \
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
  'tests/EventBooking.Infrastructure.Tests/SystemClockTests.cs' \
  'tests/EventBooking.Infrastructure.Tests/TransactionLockTests.cs' \
  'tests/EventBooking.Mcp.Tests/AttendeeMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/AttendeeParityMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/CandidateMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/CandidateParityMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/EventMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/McpEndpointTests.cs' \
  'tests/EventBooking.Mcp.Tests/McpFactory.cs' \
  'tests/EventBooking.Mcp.Tests/McpScenarioSeeder.cs' \
  'tests/EventBooking.Mcp.Tests/OperationsMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/SlotMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/StaffIdentityMcpTests.cs' \
  'tests/EventBooking.Mcp.Tests/VocabularyToolTests.cs' \
  'tests/EventBooking.SeedData.Tests/AttendeeGroupJourneySeedTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoEmailOptionsTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationHostTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoInvitationSeederTests.cs' \
  'tests/EventBooking.SeedData.Tests/DemoSeedSpecTests.cs' \
  'tests/EventBooking.SeedData.Tests/EmployeeGroupJourneySeedTests.cs' \
  'tests/EventBooking.SeedData.Tests/ReanchorTests.cs' \
  'tests/EventBooking.SeedData.Tests/ReseedTests.cs' \
  'tests/EventBooking.Web.Tests/AppointmentsClientTests.cs' \
  'tests/EventBooking.Web.Tests/AppointmentsComponentTests.cs' \
  'tests/EventBooking.Web.Tests/AppointmentsRecentPastTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeeBookingCancellationComponentTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeeLayoutTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeePresentationTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeeRecoveryComponentTests.cs' \
  'tests/EventBooking.Web.Tests/AttendeesClientTests.cs' \
  'tests/EventBooking.Web.Tests/AuditClientTests.cs' \
  'tests/EventBooking.Web.Tests/AuditHistoryComponentTests.cs' \
  'tests/EventBooking.Web.Tests/AuditPageTests.cs' \
  'tests/EventBooking.Web.Tests/BookRecoveryHeadingTests.cs' \
  'tests/EventBooking.Web.Tests/BookingClientTests.cs' \
  'tests/EventBooking.Web.Tests/CandidateBookingCancellationComponentTests.cs' \
  'tests/EventBooking.Web.Tests/CandidateLayoutTests.cs' \
  'tests/EventBooking.Web.Tests/CandidatePresentationTests.cs' \
  'tests/EventBooking.Web.Tests/CandidateRecoveryComponentTests.cs' \
  'tests/EventBooking.Web.Tests/CandidatesClientTests.cs' \
  'tests/EventBooking.Web.Tests/ConfirmedSlotsClientTests.cs' \
  'tests/EventBooking.Web.Tests/ConfirmedSlotsPageTests.cs' \
  'tests/EventBooking.Web.Tests/DashboardsClientTests.cs' \
  'tests/EventBooking.Web.Tests/DashboardsComponentTests.cs' \
  'tests/EventBooking.Web.Tests/EventOperationsClientTests.cs' \
  'tests/EventBooking.Web.Tests/EventsClientCapacityAdjustmentTests.cs' \
  'tests/EventBooking.Web.Tests/EventsClientHeadcountRevisionTests.cs' \
  'tests/EventBooking.Web.Tests/EventsClientTests.cs' \
  'tests/EventBooking.Web.Tests/EventsPageTests.cs' \
  'tests/EventBooking.Web.Tests/HelpPageTests.cs' \
  'tests/EventBooking.Web.Tests/HomePageTests.cs' \
  'tests/EventBooking.Web.Tests/MainLayoutTests.cs' \
  'tests/EventBooking.Web.Tests/RepairBWebComponentTests.cs' \
  'tests/EventBooking.Web.Tests/RepairDNotificationComponentTests.cs' \
  'tests/EventBooking.Web.Tests/SettingsTests.cs' \
  'tests/EventBooking.Web.Tests/SlotsClientCapacityAdjustmentTests.cs' \
  'tests/EventBooking.Web.Tests/SlotsClientHeadcountRevisionTests.cs' \
  'tests/EventBooking.Web.Tests/SlotsClientTests.cs' \
  'tests/EventBooking.Web.Tests/StaffNavigationTests.cs' \
  'tests/EventBooking.Web.Tests/UserGuideCatalogTests.cs'
git diff --cached --name-only
git diff --cached
node scripts/check-ontology-terms.mjs
: "${EXECUTOR_COAUTHOR:?Set the executing harness co-author identity}"
git commit -m "refactor: apply the EventBooking vocabulary mapping" -m "Co-authored-by: $EXECUTOR_COAUTHOR"
git push -u origin HEAD
```

Proceed to Task 3 on the same branch. Do not open the Phase 0 PR before completing the retirement changes and full-suite gate.
