# Notification and escalation module file inventory

Paths are relative to the repository root. This inventory records the current Git added/modified files for this module, grouped by purpose. The reviewed migration is `20260915053927_NotificationsAndEscalations`; its migration source, designer metadata, and updated context snapshot are included below.

## Added (44)

### API and background scheduling

- [backend/MaintainPro.Api/Endpoints/NotificationEndpoints.cs](../backend/MaintainPro.Api/Endpoints/NotificationEndpoints.cs)
- [backend/MaintainPro.Api/Scheduling/NotificationProcessingService.cs](../backend/MaintainPro.Api/Scheduling/NotificationProcessingService.cs)

### Application services, workflow hooks, and contracts

- [backend/MaintainPro.Application/Notifications/EscalationQueryService.cs](../backend/MaintainPro.Application/Notifications/EscalationQueryService.cs)
- [backend/MaintainPro.Application/Notifications/EscalationSettingsService.cs](../backend/MaintainPro.Application/Notifications/EscalationSettingsService.cs)
- [backend/MaintainPro.Application/Notifications/NotificationContracts.cs](../backend/MaintainPro.Application/Notifications/NotificationContracts.cs)
- [backend/MaintainPro.Application/Notifications/NotificationEventService.cs](../backend/MaintainPro.Application/Notifications/NotificationEventService.cs)
- [backend/MaintainPro.Application/Notifications/NotificationRecipientResolver.cs](../backend/MaintainPro.Application/Notifications/NotificationRecipientResolver.cs)
- [backend/MaintainPro.Application/Notifications/NotificationService.cs](../backend/MaintainPro.Application/Notifications/NotificationService.cs)
- [backend/MaintainPro.Application/Notifications/NotificationWriter.cs](../backend/MaintainPro.Application/Notifications/NotificationWriter.cs)
- [backend/MaintainPro.Application/Notifications/ReminderProcessingService.cs](../backend/MaintainPro.Application/Notifications/ReminderProcessingService.cs)
- [backend/MaintainPro.Application/Notifications/WorkflowNotificationHooks.cs](../backend/MaintainPro.Application/Notifications/WorkflowNotificationHooks.cs)
- [backend/MaintainPro.Application/Notifications/WorkOrderTimingService.cs](../backend/MaintainPro.Application/Notifications/WorkOrderTimingService.cs)
- [backend/MaintainPro.Application/WorkOrders/WorkOrderAssignmentService.cs](../backend/MaintainPro.Application/WorkOrders/WorkOrderAssignmentService.cs)

### Domain entities and enums

- [backend/MaintainPro.Domain/Entities/EscalationSettings.cs](../backend/MaintainPro.Domain/Entities/EscalationSettings.cs)
- [backend/MaintainPro.Domain/Entities/Notification.cs](../backend/MaintainPro.Domain/Entities/Notification.cs)
- [backend/MaintainPro.Domain/Entities/NotificationDeliveryAttempt.cs](../backend/MaintainPro.Domain/Entities/NotificationDeliveryAttempt.cs)
- [backend/MaintainPro.Domain/Entities/NotificationEvent.cs](../backend/MaintainPro.Domain/Entities/NotificationEvent.cs)
- [backend/MaintainPro.Domain/Entities/WorkOrderEscalation.cs](../backend/MaintainPro.Domain/Entities/WorkOrderEscalation.cs)
- [backend/MaintainPro.Domain/Enums/NotificationChannel.cs](../backend/MaintainPro.Domain/Enums/NotificationChannel.cs)
- [backend/MaintainPro.Domain/Enums/NotificationDeliveryStatus.cs](../backend/MaintainPro.Domain/Enums/NotificationDeliveryStatus.cs)
- [backend/MaintainPro.Domain/Enums/NotificationPriority.cs](../backend/MaintainPro.Domain/Enums/NotificationPriority.cs)
- [backend/MaintainPro.Domain/Enums/NotificationType.cs](../backend/MaintainPro.Domain/Enums/NotificationType.cs)

### Migration artifacts and EF snapshot

- [backend/MaintainPro.Infrastructure/Persistence/Migrations/20260915053927_NotificationsAndEscalations.cs](../backend/MaintainPro.Infrastructure/Persistence/Migrations/20260915053927_NotificationsAndEscalations.cs)
- [backend/MaintainPro.Infrastructure/Persistence/Migrations/20260915053927_NotificationsAndEscalations.Designer.cs](../backend/MaintainPro.Infrastructure/Persistence/Migrations/20260915053927_NotificationsAndEscalations.Designer.cs)

### Persistence configuration and dependency injection

- [backend/MaintainPro.Infrastructure/Persistence/Configurations/EscalationSettingsConfiguration.cs](../backend/MaintainPro.Infrastructure/Persistence/Configurations/EscalationSettingsConfiguration.cs)
- [backend/MaintainPro.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs](../backend/MaintainPro.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs)
- [backend/MaintainPro.Infrastructure/Persistence/Configurations/NotificationDeliveryAttemptConfiguration.cs](../backend/MaintainPro.Infrastructure/Persistence/Configurations/NotificationDeliveryAttemptConfiguration.cs)
- [backend/MaintainPro.Infrastructure/Persistence/Configurations/NotificationEventConfiguration.cs](../backend/MaintainPro.Infrastructure/Persistence/Configurations/NotificationEventConfiguration.cs)
- [backend/MaintainPro.Infrastructure/Persistence/Configurations/WorkOrderEscalationConfiguration.cs](../backend/MaintainPro.Infrastructure/Persistence/Configurations/WorkOrderEscalationConfiguration.cs)

### Automated tests and shared fixtures

- [backend/MaintainPro.Tests/NotificationApiTests.cs](../backend/MaintainPro.Tests/NotificationApiTests.cs)
- [backend/MaintainPro.Tests/NotificationConcurrencyTests.cs](../backend/MaintainPro.Tests/NotificationConcurrencyTests.cs)
- [backend/MaintainPro.Tests/NotificationInboxTests.cs](../backend/MaintainPro.Tests/NotificationInboxTests.cs)
- [backend/MaintainPro.Tests/NotificationIntegrityTests.cs](../backend/MaintainPro.Tests/NotificationIntegrityTests.cs)
- [backend/MaintainPro.Tests/NotificationOutboxTests.cs](../backend/MaintainPro.Tests/NotificationOutboxTests.cs)
- [backend/MaintainPro.Tests/NotificationReminderTests.cs](../backend/MaintainPro.Tests/NotificationReminderTests.cs)
- [backend/MaintainPro.Tests/NotificationSettingsTests.cs](../backend/MaintainPro.Tests/NotificationSettingsTests.cs)
- [backend/MaintainPro.Tests/NotificationTestData.cs](../backend/MaintainPro.Tests/NotificationTestData.cs)
- [backend/MaintainPro.Tests/NotificationTimingTests.cs](../backend/MaintainPro.Tests/NotificationTimingTests.cs)
- [backend/MaintainPro.Tests/NotificationWorkflowTests.cs](../backend/MaintainPro.Tests/NotificationWorkflowTests.cs)
- [backend/MaintainPro.Tests/NotificationWorkOrderReadTests.cs](../backend/MaintainPro.Tests/NotificationWorkOrderReadTests.cs)

### Documentation and migration review

- [docs/database/NotificationsAndEscalations-review.md](../docs/database/NotificationsAndEscalations-review.md)
- [docs/database/NotificationsAndEscalations.sql](../docs/database/NotificationsAndEscalations.sql)
- [docs/notifications-escalations-files.md](../docs/notifications-escalations-files.md)
- [docs/notifications-escalations-module.md](../docs/notifications-escalations-module.md)

## Modified (15)

### API and background scheduling

- [backend/MaintainPro.Api/Security/Policies.cs](../backend/MaintainPro.Api/Security/Policies.cs)

### Application services, workflow hooks, and contracts

- [backend/MaintainPro.Application/Abstractions/IApplicationDbContext.cs](../backend/MaintainPro.Application/Abstractions/IApplicationDbContext.cs)
- [backend/MaintainPro.Application/Reviews/WorkOrderReviewService.cs](../backend/MaintainPro.Application/Reviews/WorkOrderReviewService.cs)
- [backend/MaintainPro.Application/Reviews/WorkOrderSubmissionService.cs](../backend/MaintainPro.Application/Reviews/WorkOrderSubmissionService.cs)
- [backend/MaintainPro.Application/WorkOrders/WorkOrderContracts.cs](../backend/MaintainPro.Application/WorkOrders/WorkOrderContracts.cs)
- [backend/MaintainPro.Application/WorkOrders/WorkOrderGenerationService.cs](../backend/MaintainPro.Application/WorkOrders/WorkOrderGenerationService.cs)
- [backend/MaintainPro.Application/WorkOrders/WorkOrderService.cs](../backend/MaintainPro.Application/WorkOrders/WorkOrderService.cs)

### Domain entities and enums

- [backend/MaintainPro.Domain/Entities/WorkOrder.cs](../backend/MaintainPro.Domain/Entities/WorkOrder.cs)

### Migration artifacts and EF snapshot

- [backend/MaintainPro.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs](../backend/MaintainPro.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs)

### Persistence configuration and dependency injection

- [backend/MaintainPro.Infrastructure/DependencyInjection.cs](../backend/MaintainPro.Infrastructure/DependencyInjection.cs)
- [backend/MaintainPro.Infrastructure/Persistence/ApplicationDbContext.cs](../backend/MaintainPro.Infrastructure/Persistence/ApplicationDbContext.cs)
- [backend/MaintainPro.Infrastructure/Persistence/Configurations/WorkOrderConfiguration.cs](../backend/MaintainPro.Infrastructure/Persistence/Configurations/WorkOrderConfiguration.cs)

### Automated tests and shared fixtures

- [backend/MaintainPro.Tests/ApplicationDbContextTests.cs](../backend/MaintainPro.Tests/ApplicationDbContextTests.cs)
- [backend/MaintainPro.Tests/ModuleFixture.cs](../backend/MaintainPro.Tests/ModuleFixture.cs)

## Removals and package changes

No files were removed. No packages or project references were added, removed, or upgraded for this module. Earlier migration files remain unchanged.

The generated SQL and migration review are included. Actual build/test and live PostgreSQL results are recorded in the [module guide](notifications-escalations-module.md) and migration review.
