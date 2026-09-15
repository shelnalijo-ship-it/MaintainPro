# Preventive maintenance planning and work-order generation: file inventory

This inventory covers the preventive maintenance planning, versioned checklist, and work-order generation phase. Existing identity and machine-management files are listed only where this phase changes them. Project paths below are relative to the named directory under `backend/`.

## MaintainPro.Domain

Added:

- `Entities/MaintenanceType.cs`
- `Entities/MaintenancePlan.cs`
- `Entities/ChecklistTemplate.cs`
- `Entities/ChecklistItem.cs`
- `Entities/WorkOrder.cs`
- `Entities/WorkOrderDefinition.cs`
- `Entities/WorkOrderChecklistItem.cs`
- `Entities/WorkOrderNumberSequence.cs`
- `Enums/MaintenancePriority.cs`
- `Enums/MaintenanceFrequencyType.cs`
- `Enums/ChecklistResponseType.cs`
- `Enums/WorkOrderLifecycleStatus.cs`

No existing Domain file is modified.

## MaintainPro.Application

Added:

- `Planning/PlanningContracts.cs`
- `Planning/PlanningValidation.cs`
- `Planning/RecurrenceService.cs`
- `Planning/MaintenanceTypeService.cs`
- `Planning/MaintenancePlanService.cs`
- `WorkOrders/WorkOrderContracts.cs`
- `WorkOrders/IGenerationConcurrency.cs`
- `WorkOrders/WorkOrderNumberAllocator.cs`
- `WorkOrders/WorkOrderGenerationService.cs`
- `WorkOrders/WorkOrderService.cs`

Modified:

- `Abstractions/IApplicationDbContext.cs`: exposes the new entity sets through the existing persistence abstraction.

## MaintainPro.Infrastructure

Added:

- `Persistence/Configurations/MaintenanceTypeConfiguration.cs`
- `Persistence/Configurations/MaintenancePlanConfiguration.cs`
- `Persistence/Configurations/ChecklistTemplateConfiguration.cs`
- `Persistence/Configurations/ChecklistItemConfiguration.cs`
- `Persistence/Configurations/WorkOrderConfiguration.cs`
- `Persistence/Configurations/WorkOrderDefinitionConfiguration.cs`
- `Persistence/Configurations/WorkOrderChecklistItemConfiguration.cs`
- `Persistence/Configurations/WorkOrderNumberSequenceConfiguration.cs`
- `Planning/GenerationConcurrency.cs`
- `Persistence/Migrations/20260915041200_PreventivePlanningAndWorkOrderGeneration.cs`
- `Persistence/Migrations/20260915041200_PreventivePlanningAndWorkOrderGeneration.Designer.cs`

Modified:

- `DependencyInjection.cs`: registers recurrence, planning, generation, numbering, work-order queries, and generation concurrency services.
- `Persistence/ApplicationDbContext.cs`: exposes the new entity sets and extends concurrency, timestamp, deletion, and immutable-definition safeguards.
- `Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`: includes the preventive maintenance and work-order model.

The existing `InitialCreate` and `IdentityAndMachineManagement` migration files are retained unchanged.

## MaintainPro.Api

Added:

- `Endpoints/PlanningEndpoints.cs`
- `Endpoints/WorkOrderEndpoints.cs`
- `Scheduling/MaintenanceGenerationService.cs`

Modified:

- `Development/DatabaseInspector.cs`: includes planning and work-order schema metadata in the existing development inspection workflow.
- `Program.cs`: registers the generation hosted service and maps the planning and work-order endpoints.
- `Security/Policies.cs`: adds named planning and work-order access policies.

## MaintainPro.Tests

Added:

- `PlanningTestData.cs`
- `MaintenancePlanningTests.cs`
- `ChecklistVersionTests.cs`
- `RecurrenceServiceTests.cs`
- `WorkOrderGenerationTests.cs`
- `GenerationConcurrencyTests.cs`
- `WorkOrderReadTests.cs`
- `GenerationSchedulerTests.cs`

Modified:

- `ApplicationDbContextTests.cs`: extends model checks for new entities, relationships, indexes, and immutable snapshots.
- `ModuleFixture.cs`: exposes planning services and extends the isolated relational test setup.
- `ApiIntegrationTests.cs`: extends API integration coverage for planning, work-order reads, and protected generation.

## Documentation

Added:

- `docs/preventive-planning-module.md`: module design, operational behavior, and validation report.
- `docs/preventive-planning-files.md`: this inventory.
- `docs/database/PreventivePlanning.sql`: generated migration SQL for review.

Modified:

- `docs/implementation-plan.md`: records the completed module's scope and remaining roadmap.
- `docs/backend-module.md`: identifies the identity and machine-management report as the preceding phase.

## Packages, project files, and removals

No package, project-reference, framework-reference, or project-file changes are introduced in this phase. All five projects retain their existing .NET 10 targets and dependencies. No files are removed.

Build, test, migration-review, and development-database results are recorded separately in [the module report](preventive-planning-module.md).
