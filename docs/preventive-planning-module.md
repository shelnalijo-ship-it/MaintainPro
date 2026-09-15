# Preventive maintenance planning, checklists, and work-order generation

This module extends the existing backend identity and machine management implementation. It adds maintenance planning and historical work definitions, without technician execution, results, submission, or approval workflows. The repository was clean at the start of this phase; the baseline build passed with no warnings/errors and all 100 existing tests passed.

## Architecture and files

Domain contains eight new entities and four explicit enums. Application contains maintenance-type/plan/checklist services, a deterministic recurrence calculator, the generation use case, annual numbering, and work-order read/calendar queries. Infrastructure supplies EF mappings, persistence guards, serializable transactions, and PostgreSQL conflict classification. API supplies thin protected endpoints and a Development scheduler. Existing authentication, auditing, current-user, transaction, validation, error, and OpenAPI infrastructure is reused.

The [file inventory](preventive-planning-files.md) lists all 47 added files (44 C# files and three documentation/SQL files) and twelve modified files. No packages, project references, or target frameworks were changed. No source files were removed. No environment configuration or secrets were changed.

## Database model and migration

New migration: `20260915041200_PreventivePlanningAndWorkOrderGeneration`. Its designer and the updated EF model snapshot are included. The [forward SQL review artifact](database/PreventivePlanning.sql) covers this migration only, following `IdentityAndMachineManagement`.

| New entity/table | Purpose |
| --- | --- |
| `MaintenanceTypes` | Unique named types, optional description, activation, UTC timestamps |
| `MaintenancePlans` | Machine/type, schedule and next occurrence, priority, instructions, evidence rules, defaults, supervisor, creator and timestamps |
| `ChecklistTemplates` | Retained plan-specific versions with creator/time |
| `ChecklistItems` | Ordered response definitions, numeric bounds, units and evidence requirements |
| `WorkOrders` | GUID identity, human-readable number, source/assignment IDs, planned/due dates, lifecycle and separate escalation level |
| `WorkOrderDefinitions` | One immutable operational snapshot per work order |
| `WorkOrderChecklistItems` | Immutable item definitions copied into that snapshot |
| `WorkOrderNumberSequences` | Persistent annual counter with a concurrency version |

All 16 new foreign keys use `Restrict`; the full model therefore has 32 restricted foreign keys. Existing tables are referenced, not recreated. Type and plan deletion is blocked in normal tracked workflows; deactivation is supported. Work orders and historical definitions cannot be physically deleted through those workflows.

Seven new unique indexes enforce type name, `(MaintenancePlanId, Version)`, `(ChecklistTemplateId, SequenceNumber)`, work-order number, `(MaintenancePlanId, PlannedDate)` when the plan ID is non-null, definition `WorkOrderId`, and `(WorkOrderDefinitionId, SequenceNumber)`. The full model has 15 non-primary unique indexes. Annual counters use `Year` as their primary key. Query indexes cover due active plans, work-order lifecycle/due date, technician/planned date and foreign keys.

Check constraints enforce positive template versions/item sequences/frequencies, consistent plan photo counts, nonnegative duration/escalation, valid numeric bounds, due date not before planned date, and valid sequence years/counts. Schedule dates use PostgreSQL `date`; UTC timestamps use `timestamp with time zone`; enums use string conversion; numeric checklist values use `numeric`.

The forward migration only creates new tables, keys, constraints and indexes. It contains no application seed data and does not modify old rows. Generated `Down` methods drop the new tables and were not executed. Startup does not apply migrations automatically.

## Plan and checklist behavior

Managers/admins manage maintenance types and plans. Type names are checked case-insensitively in serializable transactions and are unique at the database level. No business-specific types are seeded.

A plan requires an active, non-decommissioned machine, active type, active SUPERVISOR, and—when supplied—an active TECHNICIAN. ADMIN alone does not qualify for either assignment. Priorities are `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`. Estimated duration cannot be negative. When `photoRequired` is true, `minimumPhotoCount` must be positive; otherwise it must be zero. The plan retains the requested comment requirement and optional instructions.

Creation sets `NextDueDate = StartDate`. Future creation dates are allowed but cannot generate early. Default/unusable start dates and intervals that cannot advance within the supported calendar are rejected. Plans may be created before their checklist is configured; generation then reports a correctable checklist error and keeps the next-due cursor.

Each checklist PUT creates a new version, including edits to an unused definition. Versions and items are never edited in place. This deliberately avoids mutable draft semantics. All retained versions remain eligible (`IsActive = true`); the highest version is current. An explicit version query retrieves older versions. Each version contains 1–200 items with unique positive sequence numbers and required titles. Responses are `BOOLEAN`, `PASS_FAIL`, `NUMBER`, `TEXT`, `PHOTO`, or `CONFIRMATION`. Only NUMBER permits unit/minimum/maximum, and minimum cannot exceed maximum. Ordered item DTOs retain mandatory and photo flags.

The context blocks editing/deleting templates, items, definitions and snapshot items. It also blocks appending new items to existing versions/snapshots, and requires a work order and its definition to be created together. These safeguards apply to normal tracked EF operations; privileged direct SQL is outside their scope.

Plan updates affect subsequent generation only. Changing instructions, evidence, priority, technician, supervisor, type or machine never rewrites generated jobs or their snapshots. Disabling a plan stops generation and retains all jobs. Reactivation retains the cursor, so missed occurrences can be caught up. Duplicating a plan creates new identifiers, uses the requested start date/name/optional machine, and copies its current checklist into a new version 1; the source plan and jobs remain unchanged.

Changing the start date or frequency recalculates the first new occurrence on or after UTC today and strictly after the latest generated planned date, anchored to the newly supplied start date. This explicitly replaces any ungenerated backlog under the old recurrence. Other edits leave `NextDueDate` unchanged. Cancelled occurrences retain their occurrence keys and are not regenerated.

## Recurrence rules

| Frequency | Required value | Interval |
| --- | --- | --- |
| DAILY | 1 | 1 day |
| WEEKLY | 1 | 7 days |
| MONTHLY | 1 | 1 calendar month |
| DAYS | Positive integer | That many days |
| WEEKS | Positive integer | That many groups of 7 days |
| MONTHS | Positive integer | That many calendar months |
| QUARTERLY | 1 | 3 calendar months |
| HALF_YEARLY | 1 | 6 calendar months |
| YEARLY | 1 | 12 calendar months |

The recurrence service calculates from the original anchor day, clamping only when the target month is shorter. January 31 → February 28/29 → March 31. January 30 → February 28/29 → March 30. February 29 yearly → February 28 in non-leap years → February 29 in the next leap year. April 30 monthly → May 30; being the last day of the source month does not automatically force the last day of every target month. Thirty days is distinct from one calendar month. Arithmetic is direct rather than iterating through every elapsed day.

## Historical snapshot and lifecycle

Every generated job owns a definition containing the plan name, maintenance-type ID/name, instructions, estimated duration, evidence rules, priority, machine code/name, checklist ID/version/name, and copied checklist items. Technician and supervisor names/employee IDs are also copied. The work-order row retains the assigned IDs. Historical reads and searches use these copied facts, so renaming a machine/type/person or editing a plan/template does not change an existing work definition.

Lifecycle values are `PLANNED`, `ASSIGNED`, `IN_PROGRESS`, `AWAITING_APPROVAL`, `APPROVED`, `REJECTED`, `CANCELLED`. This phase generates only PLANNED (unassigned) or ASSIGNED jobs; it exposes no execution/state-transition endpoints. Lifecycle has no OVERDUE/ESCALATED members. Escalation level starts at zero and no escalation processing runs.

Planned date and due date both equal the schedule occurrence date in this module. A job becomes overdue when its due date is before UTC today and its lifecycle is not APPROVED, CANCELLED or AWAITING_APPROVAL. Today remains due through the end of the UTC day. No company-local timezone, lead-time generation, due-date offset, or separate approval deadline is configured in this phase.

## Generation and numbering

The generator evaluates active plans due through the requested cutoff (default UTC today). A future cutoff is rejected. It advances one occurrence per plan in round-robin order before revisiting plans with more backlog. The default batch permits 500 occurrence advances, maximum 1000. Repeated runs resume from persisted cursors after restarts and missed scheduler runs.

Each occurrence has one serializable transaction containing: reload/validate source facts, check the persistent occurrence key, allocate its number, copy the definition/items, create the job/audit, and advance the cursor. The database's unique plan/date index is the final duplicate barrier. A stale cursor pointing at an already-generated occurrence advances without allocating another number or changing the original job.

Numbers use `WO-YYYY-NNNN`, with the occurrence's planned year. A persistent annual row increments its `LastValue`; the year primary key and concurrency version arbitrate races. There is no `MAX + 1` query. Allocation requires an active transaction and also supports multiple allocations before saving within one transaction. Formatting uses a minimum of four digits, so 9999 is followed by 10000. A new year starts its own sequence. Allocation rolls back if the occurrence does not commit.

PostgreSQL serialization/deadlock/unique conflicts and EF concurrency conflicts trigger at most five attempts, each with a new transaction and freshly loaded data. Exhausted conflicts return a per-plan error for later retry. A bounded failure for one plan does not prevent evaluation of the others.

Inactive plans do not enter generation. Inactive/decommissioned machines are skipped without advancing the cursor. An inactive/role-invalid default technician produces unassigned work, preserving the schedule. An inactive type, invalid supervisor, or missing checklist produces a correctable error without advancing. Catch-up uses the operational definition current at generation time; the application does not reconstruct undocumented historical plan edits from before generation.

The summary contains `plansEvaluated`, `workOrdersCreated`, `skipped`, `errors`, `hasMore`, and safe per-plan `issues`. Evaluated counts refer to active plans due in this run. `hasMore` may include blocked plans; it is not a promise that an immediate retry will generate work.

## Scheduled generation

`MaintenanceGenerationService` is a thin BackgroundService. It runs at startup and then the next UTC midnight in Development. Testing explicitly disables it. Other environments opt in with `MaintenanceGeneration:Enabled = true`; setting false disables it in Development. No configuration value was written during this task.

The scheduler creates a fresh DI scope and invokes the same Application service as manual generation. System runs have a null audit actor; authorized manual runs retain their user actor. Successful bounded batches with more backlog retry after one minute; errors retry after fifteen minutes. Blocked/inactive plans cannot cause a busy loop merely because `hasMore` is true. Cancellation and provider failures are handled without logging configuration or provider exception details. Database uniqueness, concurrency versions and transactions protect multiple application instances; there is no process-local lock relied upon for correctness.

## API and authorization

All routes use `/api/v1`, camel-case JSON, string enum names, existing JWT authentication, typed DTO responses and centralized ProblemDetails.

| Routes | Access |
| --- | --- |
| `GET/POST /maintenance-types`, `PUT /maintenance-types/{id}`, `PATCH /maintenance-types/{id}/status` | MANAGER or ADMIN |
| `GET /maintenance-plans`, `GET /maintenance-plans/{id}` | MANAGER/ADMIN all; SUPERVISOR only plans naming them |
| `POST /maintenance-plans`, `PUT /maintenance-plans/{id}`, `POST /maintenance-plans/{id}/duplicate`, `PATCH /maintenance-plans/{id}/status` | MANAGER or ADMIN |
| `GET /maintenance-plans/{id}/checklist?version=1` | Same plan read scope; omit version for current |
| `PUT /maintenance-plans/{id}/checklist` | MANAGER or ADMIN; creates a version |
| `POST /maintenance-plans/generate-due` | MANAGER or ADMIN |
| `GET /work-orders`, `GET /work-orders/{id}`, `GET /work-orders/calendar` | Role union and stored assignment scope |

Named policies are `ManagePlanning`, `ReadPlanning`, `ReadWorkOrders`, with application-level checks as well. Technicians see assigned jobs; supervisors see jobs supervised by them; managers/admins see all. Multiple roles combine visibility. Security scope precedes filtering/counting/pagination and also applies to details/calendar. Inaccessible IDs return 404. Unrelated machine ownership does not grant access to a job; stored job assignments determine it.

Work-order list filters: `search`, `page` (default 1), `pageSize` (default 20, maximum 100), `machineId`, `maintenancePlanId`, `technicianId`, `supervisorId`, `lifecycleStatus`, `priority`, `plannedFrom`, `plannedTo`, `dueFrom`, `dueTo`, `overdue`. Search covers number and snapshot machine code/name/plan name. Dates are inclusive. Results contain items/page/pageSize/totalCount/totalPages. Read queries use `AsNoTracking` and stable ordering.

Calendar requires `from`/`to` and supports machine/technician/supervisor/lifecycle filters. It returns compact snapshot-based events whose planned dates are within the inclusive range. The date difference is limited to 366 days and the result to 5000 events; larger requests return 400 with a request to narrow the range.

Plan lists support search, paging, machine/type/supervisor/default technician/active filters. Maintenance-type lists support search and active state. Example request contracts (replace the ID placeholders with existing records):

```json
{
  "machineId": "<machine-id>",
  "planName": "Monthly inspection",
  "maintenanceTypeId": "<type-id>",
  "priority": "MEDIUM",
  "frequencyType": "MONTHLY",
  "frequencyValue": 1,
  "startDate": "2026-09-30",
  "supervisorId": "<supervisor-id>",
  "defaultTechnicianId": "<technician-id>",
  "photoRequired": true,
  "minimumPhotoCount": 1,
  "commentRequired": true
}
```

```json
{
  "name": "Inspection checklist",
  "items": [
    { "sequenceNumber": 1, "title": "Check guard", "responseType": "PASS_FAIL", "isMandatory": true },
    { "sequenceNumber": 2, "title": "Check pressure", "responseType": "NUMBER", "unit": "bar", "minimumValue": 2, "maximumValue": 5 }
  ]
}
```

Manual generation accepts an omitted body or `{ "maxOccurrences": 500 }`, with optional `throughDate` on/before UTC today. Duplicate uses planName/startDate/optional machineId; status uses `{ "isActive": false }`. Required mutation fields cannot silently default when omitted. Development OpenAPI includes the new routes and Bearer security.

Audit events cover type creation/edit/status, plan creation/edit/status/duplication, checklist version creation, and each generated work order. Mutations and audit commit together; the existing credential-redacting audit writer is reused.

## Validation and development database

Required commands, from `D:\MaintainPro\backend`:

```powershell
dotnet build MaintainPro.slnx
dotnet test MaintainPro.slnx
dotnet ef migrations has-pending-model-changes --project MaintainPro.Infrastructure --startup-project MaintainPro.Api --context ApplicationDbContext -- --environment Development
dotnet run --project MaintainPro.Api --no-launch-profile --no-build -- --environment Development --database-inspect
```

The Development inspector targets only the configured `maintainpro_db` and prints metadata, counts, role names and configuration-presence booleans. Baseline inspection found the two expected migrations, ten application tables, four standard roles and no other application rows. The added row-count metadata allows comparison after applying this migration without printing records or credentials.

The migration was applied successfully after the 205-test suite passed and the SQL review confirmed eight table creations, sixteen new restricted foreign keys, and no destructive forward operations. The two existing migration files and their designers remain unchanged.

Post-migration PostgreSQL inspection verified eighteen application tables plus the migration history table, all three expected migration IDs, 32 restricted foreign keys (zero non-Restrict), and fifteen unique indexes. Every pre-existing application table retained its previous row count: four standard roles, zero rows in the other existing application tables. New module tables remain empty; no arbitrary business data or credentials were seeded.

Live Development startup ran the scheduler successfully: zero due plans, zero created orders, zero errors. Health returned 200 Healthy. OpenAPI returned 37 paths / 48 operations with Bearer authentication, including all sixteen new operations. Anonymous plan/work-order/calendar reads returned 401. The temporary verification server was stopped afterward. The sandbox emitted the previously known ephemeral data-protection and HTTP-only probe HTTPS-port notices; these were not build or test failures.

| Final check | Result |
| --- | --- |
| `dotnet build MaintainPro.slnx` | Passed; 0 warnings, 0 errors |
| `dotnet test MaintainPro.slnx` | 205 passed, 0 failed, 0 skipped; 21-second final test run |
| EF pending-model check | Passed; no model changes since the last migration |
| `git diff --check` | No whitespace errors; only normal Windows LF-to-CRLF notices |
| Git scope review | 47 added / 12 modified files; no removals, project/package/configuration changes, or tracked bin/obj files |

The 205 cases include the 100-case baseline and new planning, recurrence, version/history, snapshot, filtering, authorization, API and scheduler cases. Two independent workers/connections exercise concurrent generation against a temporary shared SQLite database; two allocation workers verify the persistent counter. Tests also cover restart idempotence, rollback, year rollover, numbers beyond 9999, invalid calendar boundaries, immutable child append attempts, and transactional allocation safeguards.

## Limits and next module

- Workflow and actual multi-worker concurrency tests use isolated SQLite databases. PostgreSQL schema/migration/startup checks supplement them; PostgreSQL SSI under production load is not load-tested by this suite.
- Generation is bounded and retryable, with an annual counter contention point. Deployment-scale batching, distributed scheduling/monitoring and automatic repair of invalid plan references are future operational work.
- Checklist history is versioned on every save, including unused versions. No mutable draft or physical-delete API is provided.
- UTC is the scheduling/overdue boundary. Changing company timezone or recurrence replaces scheduling policy and should be designed explicitly.
- Deactivated plans/machines retain their due cursor. Re-enabling resumes catch-up; recurrence edits replace old ungenerated backlog. Generated assignments stay fixed; reassignment is a later explicit workflow.
- Normal EF snapshot/append-only guards do not replace database permissions or protect against privileged direct SQL.
- Existing missing JWT/bootstrap configuration still requires the [identity setup guide](backend-module.md#local-configuration-still-required). No credentials are needed to run the isolated tests, and none were invented.
- The recommended next module is technician execution against these snapshots: checklist answers, readings/evidence, progress and submission, followed by supervisor approval/rejection with retained history. None of those workflows was implemented here.

**Change boundaries:** `maintainpro_db` was modified only by the authorized additive migration and its migration-history entry. The scheduler's empty-plan run created no application data. No other PostgreSQL database was modified; automated tests used disposable SQLite databases. No secrets were printed, enumerated or moved. No Git commit or push was performed.
