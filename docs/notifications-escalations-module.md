# Notifications, reminders, overdue detection, and escalation

This module adds a private in-app inbox, workflow notices, deadline reminders, supervisor/manager escalation, configurable thresholds, and durable processing. Lifecycle remains the existing execution/approval workflow. Notifications never change a work order's lifecycle to `OVERDUE` or `ESCALATED`.

No Firebase, email, SMS, WhatsApp, breakdown, calibration, inventory, reporting, or frontend integration is included.

## Architecture and changed files

The existing project structure is retained. Domain entities hold notification, delivery, policy, and escalation facts. Application services own recipient selection, timing, authorization, deduplication, workflow integration, and transaction boundaries. Infrastructure supplies EF mappings and the existing PostgreSQL concurrency classification. API endpoints remain thin. A hosted service invokes independently testable application processing.

New application files are under `backend/MaintainPro.Application/Notifications`:

- `NotificationContracts.cs`: inbox, settings, escalation, writer, and dispatcher DTOs.
- `NotificationService.cs`: current-user listing, unread counts, and read state.
- `NotificationWriter.cs`: atomic inbox persistence and delivery-attempt records.
- `NotificationRecipientResolver.cs`: active-role routing, fallback reasons, and semantic notification keys.
- `NotificationEventService.cs`: durable events, partial delivery, replay, obsolete-event handling, and escalation resolution.
- `WorkflowNotificationHooks.cs`: assignment, submission, approval, and rejection hooks using existing business transactions.
- `EscalationSettingsService.cs` and `EscalationQueryService.cs`: management settings and scoped escalation queries.
- `WorkOrderTimingService.cs`: pure UTC deadline evaluation.
- `ReminderProcessingService.cs`: due work preparation and transactional reminder/escalation processing.

`WorkOrders/WorkOrderAssignmentService.cs` adds controlled assignment before execution starts. Existing generation, work-order read/contracts, submission, and review services integrate notifications. Domain gains five entities and notification enums; `WorkOrder` gains assignment-version/current-technician snapshot fields. Infrastructure adds the corresponding EF configurations and updates the context, database abstraction, dependency injection, and immutable-history guards. API additions are `Endpoints/NotificationEndpoints.cs` and `Scheduling/NotificationProcessingService.cs`, with startup and authorization policy registration. Tests extend service, API, timing, routing, persistence, concurrency, and history coverage.

No external delivery package or provider credential is required.

The complete Git added/modified inventory is in [notifications-escalations-files.md](notifications-escalations-files.md). No files were removed and no packages or project references changed for this module.

## Persistence model and migration

| New entity/table | Purpose |
| --- | --- |
| Notifications | One private recipient inbox item with immutable content, read state, priority, expiry, and unique semantic key |
| NotificationDeliveryAttempts | Channel/attempt result records; actual in-app delivery and explicit unconfigured external channels |
| EscalationSettings | Singleton editable deadline policy with optimistic concurrency |
| WorkOrderEscalations | Historical per-recipient escalation records and one-way resolution |
| NotificationEvents | Durable source events, including incomplete routing and retry state |

The fifth table is deliberate: an inbox row cannot exist without a valid recipient. A durable source event prevents assignment, submission, or review notices from being lost when no suitable active recipient exists. It also protects partially delivered events across retries and application restarts.

Notifications and events have globally unique deduplication keys. Escalation keys are unique. Delivery attempts are unique by notification, channel, and attempt number. Related users, work orders, notifications, and policy editors use `Restrict` foreign keys. Read-state and threshold constraints protect consistency.

Notification content, recipient, type, priority, and source links are immutable after creation. Only read state and the concurrency token may change, and the first read time cannot be overwritten. Delivery attempts are immutable. Escalation facts are immutable except for their first resolution time. Processed source events cannot be reopened or rewritten. Pending events may update processing status and diagnostics. Historical records are retained rather than deleted.

Migration `20260915053927_NotificationsAndEscalations` was generated, reviewed, and applied to `maintainpro_db`. Its [forward SQL](database/NotificationsAndEscalations.sql) and [migration review](database/NotificationsAndEscalations-review.md) document five new tables, three work-order columns, seven `Restrict` foreign keys, eleven secondary indexes including five unique indexes, and six check constraints. The forward operation contains no destructive table or business-data operation. The designer and context snapshot match the migration.

## In-app delivery and provider placeholders

Each newly persisted notification creates three attempt records in the same transaction:

| Channel | Current result | Meaning |
| --- | --- | --- |
| IN_APP | SENT | The notification is available through the persisted private inbox |
| PUSH | SKIPPED | No push provider is configured or called |
| EMAIL | SKIPPED | No email provider is configured or called |

External message IDs remain empty. Skipped provider placeholders are not failures and are not fabricated delivery successes. Channel/status fields reserve space for later provider integrations; this module does not create an external delivery worker or call any external service.

## Escalation policy and UTC timing

Default effective settings are:

```json
{
  "dueSoonDays": 1,
  "technicianOverdueDays": 1,
  "supervisorEscalationDays": 3,
  "managerEscalationDays": 5
}
```

The database setting replaces those defaults when a manager/admin updates it. Reading defaults does not create a fictitious edit event; `updatedAt`/`updatedByUserId` are empty until a persisted setting has editor metadata.

Validation requires `0 <= dueSoonDays <= 365` and `0 <= technicianOverdueDays <= supervisorEscalationDays <= managerEscalationDays <= 36500`. Equal thresholds are permitted. These bounds limit accidental configuration extremes and keep date arithmetic within a controlled lookahead.

Timing uses the UTC calendar date and the work order's `DateOnly` due date:

- Before due date and within `DueSoonDays`: due soon.
- On due date: due today, with zero days overdue.
- After due date: overdue, with whole calendar days overdue.
- Level 1 begins at the technician threshold; level 2 at the supervisor threshold; level 3 at the manager threshold. A threshold of zero still requires an actually overdue date; it does not make due-today work overdue.

**Execution reminders stop whenever `SubmittedAt` exists.** The rule is literal and continues through rejection, resume, correction, and resubmission, because those workflows preserve the original submitted timestamp. `AWAITING_APPROVAL`, `APPROVED`, and `CANCELLED` are also explicitly excluded, including inconsistent records missing a submitted timestamp.

This module does not invent a second execution deadline for corrections or an approval-lateness policy. The existing pending-approval queue's separate `reviewOverdue` flag remains a review-prioritization view; it does not trigger these execution reminders.

## Recipients and fallback behavior

Every candidate must be active and hold the required role. IDs are deduplicated so multi-role users receive one copy of the same semantic event.

| Event | Primary recipients |
| --- | --- |
| WORK_ORDER_ASSIGNED / WORK_ORDER_REASSIGNED | Assigned technician |
| WORK_ORDER_DUE_SOON / WORK_ORDER_DUE | Assigned technician |
| WORK_ORDER_OVERDUE | Assigned technician and assigned supervisor |
| ESCALATION_SUPERVISOR | Assigned supervisor |
| ESCALATION_MANAGER | All active users with MANAGER role |
| WORK_ORDER_SUBMITTED | Assigned supervisor |
| WORK_ORDER_APPROVED / WORK_ORDER_REJECTED | Technician |

If the technician is missing, inactive, or no longer has `TECHNICIAN`, routing falls back to the active assigned supervisor and then managers. If the supervisor is inactive or lacks `SUPERVISOR`, routing falls back to active managers. Fallback reasons are recorded and included in the message where useful. Generic event wording avoids claiming that the fallback recipient is the assigned technician.

**ADMIN alone is not a manager-notification fallback.** The manager strategy is all active `MANAGER` users. An admin can manage settings and manually run processing, but receives manager notices only when also assigned `MANAGER`.

If a required route has no recipient, the event remains pending with a diagnostic. Workflow success continues because the event itself is persisted transactionally. Partial delivery is also retained: for example, an active technician can receive the overdue notice while the unavailable supervisor route waits for an active manager. A later retry creates only missing recipient copies. There is no silent drop and no invented user.

## Workflow integration and controlled reassignment

Initial generation creates the assignment event in the same transaction as the work order, immutable definition, number allocation, schedule cursor, audit, and history. Unassigned generated work still invokes routing so a supervisor/manager can be alerted that assignment is needed.

Submission creates the supervisor event alongside the immutable submission and lifecycle change. Approval and rejection create the technician event alongside the exact-version decision. Each submission ID distinguishes its workflow notifications, so rejection and resubmission do not accidentally collapse notices from different versions.

`POST /api/v1/work-orders/{id}/assignment` accepts an active technician and optional reason. Only manager/admin can call it, and only before execution starts while the job is `PLANNED` or `ASSIGNED`. It increments `AssignmentVersion`, records history/audit, and creates the appropriate assignment/reassignment notice. Repeating the current assignment is a no-op. Execution or approved work cannot be reassigned through this endpoint.

The generated definition remains unchanged. Current assignment identity is held separately in `CurrentTechnicianName`/`CurrentTechnicianEmployeeId`; later execution and submission retain their own identity snapshots.

## Durable events, retries, and deduplication

The processor performs one serializable transaction per work order. A conflict rolls back its entire batch, clears tracked state, reloads facts, and retries a bounded number of times using the existing PostgreSQL conflict policy. Independent work orders can continue if one has a routing or processing issue.

Deduplication has two levels:

1. A durable event key identifies the source event. Assignment versions allow re-routing after a genuine assignment change; submission IDs identify specific workflow versions.
2. A recipient notification key identifies work order, event type, recipient, and the applicable workflow event/version. Reminder keys deliberately exclude policy thresholds and assignment versions, so hourly runs and setting changes cannot resend the same reminder to the same recipient.

Escalation rows similarly deduplicate by work order, escalation event type, and recipient. Annual work-order numbering and occurrence uniqueness retain their existing protections.

Pending persisted events are replayed once per work-order pass. Newly ensured events are not immediately dispatched a second time. Processing state is stored in PostgreSQL rather than a process-local scheduler flag, so another application instance or a restart uses the same keys and pending events.

Obsolete action notices are safely closed: old assignment versions, assignments after execution has started, expired due-soon/due-date windows, execution reminders after submission/closure, submissions already decided, and rejection notices after correction has resumed or superseded that version. Previously delivered inbox items remain historical records.

Pending timing events are checked against the current policy. If a raised threshold is not yet reached, the event waits rather than permanently consuming its semantic key. It can deliver when the new threshold becomes eligible. Previously delivered notices and escalation history are retained.

Approval notifications can be retried after a work order becomes immutable. That retry updates only notification/event/delivery records and audit; it does not change approved execution, the work-order history counter, or the original review.

## Scheduled processing and missed-run catch-up

`NotificationProcessingService` is a thin hosted service. It creates a dependency-injection scope and invokes `ReminderProcessingService`, the same core used by the protected manual endpoint. It runs by default in Development, is disabled in Testing, and requires explicit opt-in in other environments through `NotificationProcessing:Enabled`.

The default interval is 60 minutes. `NotificationProcessing:IntervalMinutes` accepts 1–1440. Processing failures/routing errors use a 15-minute retry delay. When upcoming generation made progress but reports more work, the hosted service follows up after one minute instead of waiting an hour. It does not busy-loop through a blocked generation backlog.

To make due-soon reminders possible, reminder processing first prepares upcoming occurrences through `today + DueSoonDays`, with a 500-occurrence generation budget per pass. The existing public due-only generation endpoint retains its date restriction. Newly prepared jobs receive ordinary immutable definitions, numbers, assignment notices, and occurrence uniqueness. `GeneratedWorkOrders` and `HasMoreGeneration` explain the extra preparation in processing summaries; notification totals include notices created during that preparation.

The original daily generation service remains. Its overlap with hourly reminder preparation is safe because both paths use the same application generation logic, serializable transactions, and unique occurrence keys. Missed escalation thresholds are caught up after downtime; expired due-soon and due-today notices are not back-sent.

## Inbox, escalation APIs, and authorization

All routes use the existing bearer authentication and problem-details behavior.

| Method and route | Access / behavior |
| --- | --- |
| GET `/api/v1/notifications` | Current user's inbox only |
| GET `/api/v1/notifications/unread-count` | Current user's unread count |
| PATCH `/api/v1/notifications/{id}/read` | Marks an owned, unexpired notice read |
| POST `/api/v1/notifications/read-all` | Marks all current unread, unexpired owned notices read |
| POST `/api/v1/notifications/process-due` | Manager/admin; invokes core processing |
| GET `/api/v1/escalations` | Manager/admin all; supervisor only supervised work |
| GET `/api/v1/escalations/{id}` | Same escalation scope |
| GET `/api/v1/settings/escalation` | Manager/admin |
| PUT `/api/v1/settings/escalation` | Manager/admin; validates and audits policy changes |
| POST `/api/v1/work-orders/{id}/assignment` | Manager/admin; controlled assignment before execution starts |

Inbox filters are `isRead`, `type`, `priority`, `from`, `to`, `page`, and `pageSize`. The date bounds are inclusive UTC calendar dates. Expired notifications (`ExpiresAt <= now`) are excluded from listing, unread counts, and read actions. Marking a notice read is idempotent and preserves the first read timestamp. Other users' notices return 404. There is no API to change notification content or mark it unread again.

Escalation filters are `workOrderId`, `technicianId`, `supervisorId`, `level`, `unresolvedOnly`, `from`, `to`, `page`, and `pageSize`. Levels are 1–3. `unresolvedOnly=true` restricts to unresolved rows; false/omitted includes historical resolved rows. Technician/supervisor filters use the current work-order assignment, while each record retains its original recipient. Security is applied before totals and pagination. Technicians cannot access the management list; their own work-order responses expose operational timing instead.

Page defaults are 1 and 20, with page sizes limited to 100. Read-all responds with `updatedCount`; unread count responds with `unreadCount`. The manual processor reports evaluated work orders, created notifications/escalations, skipped duplicates, errors and sanitized issues, plus upcoming generation progress.

## Work-order reads, history, and auditing

List, detail, and calendar responses derive `isDueSoon`, `isDueToday`, `isOverdue`, `daysOverdue`, and `escalationLevel` from current UTC time and effective policy. The existing `overdue` field is retained as an alias of `isOverdue`. Existing overdue filtering follows the same submitted/terminal-state gate. Lifecycle remains unchanged.

`lastEscalatedAt` comes from persisted escalation history and can remain present after resolution. The processor maintains the existing operational escalation field, while read results use fresh timing calculations. This avoids presenting a stale cached level as current truth.

History records meaningful due-soon, due-date, overdue-detected, supervisor-escalation, and manager-escalation events. A semantic transition is not appended again for hourly checks or re-routing an existing reminder. Submission/closure resolves open escalation records without deleting them; a meaningful resolution is audited and recorded in mutable work-order history where permitted. Approved work orders remain immutable.

Settings changes, assignment changes, dispatch outcomes, routing failures, deferred/obsolete events, and escalation resolution are audited. Repeated identical missing-recipient checks do not append identical audit failures every hour. Audit payloads contain IDs and safe scalar facts; they contain no credentials or binary evidence. Workflow notices persist inside the business transaction and have no dependency on provider availability.

## Validation record and scope boundary

Completed checks:

| Check | Actual result |
| --- | --- |
| Baseline suite | 326 tests passed before implementation |
| Final solution build | Passed with zero warnings and zero errors |
| Final full solution test run | 418 passed, 0 failed, 0 skipped; reported duration 2m29s |
| New notification coverage | 92 tests passed in the targeted notification suite |
| Read-only implementation review | No blocking defects found |
| Migration SQL and review | Generated and reviewed; additive forward operation, no destructive changes |
| PostgreSQL migration application | `20260915053927_NotificationsAndEscalations` successfully applied only to `maintainpro_db` |
| Live model/schema comparison | Tables, indexes, and foreign keys match; differences list empty |
| Final pending-model-change check | Exit code 0; no pending model changes |
| Live health endpoint | HTTP 200, `Healthy` |
| Live OpenAPI | HTTP 200; all ten new operations present with bearer authentication documentation |
| Live unauthenticated access checks | Notifications, escalations, and manual processing each returned HTTP 401 |
| Live hosted reminder worker | Completed with 0 orders, notifications, escalations, and errors; reading default settings inserted no row |
| Final Git diff check | Exit code 0; only LF-to-CRLF normalization advisories |

The full suite contains the 326 existing tests plus 92 new tests. Live PostgreSQL verification independently confirmed:

| Database measure | Before | After |
| --- | --- | --- |
| Application tables | 31 | 36 |
| Applied EF migrations | 4 | 5 |
| Foreign keys | 65 | 72 |
| Unique secondary indexes | 23 | 28 |

All 72 foreign keys use `Restrict`; there are zero non-restricted foreign keys. Every prior table's row count and all four role definitions were preserved exactly: four role rows and zero rows in the other prior business tables. All five new tables are empty. The inspector reported `ModelTablesIndexesAndForeignKeysMatch=true` and `differences=[]`. A final inspection after live startup and shutdown confirmed the same row counts and model match.

Authenticated workflow/API tests and concurrent-processing tests used isolated temporary SQLite databases. PostgreSQL verification covered the real migration, schema, preserved data, startup, authorization challenges, and an empty worker run; it did not create business fixtures or run multi-session workflow concurrency against the persistent database. The temporary live probe server was stopped after verification.

Live startup reported environment warnings about ephemeral Data Protection keys/no XML encryptor and the HTTP probe having no HTTPS redirect port. Bootstrap was skipped because its existing settings are absent. Only configuration-presence booleans were reported: `JwtSigningKeyConfigured=false` and `BootstrapConfigured=false`. Live authenticated use still requires the existing [identity setup](backend-module.md); no secret values were printed or moved.

Validation commands from `backend`:

```text
dotnet build MaintainPro.slnx
dotnet test MaintainPro.slnx
dotnet ef migrations has-pending-model-changes --project MaintainPro.Infrastructure --startup-project MaintainPro.Api --context ApplicationDbContext
```

Change boundaries: `maintainpro_db` was modified by the authorized additive migration. No other persistent database was modified; automated tests used temporary SQLite databases. No secrets were printed or moved. No Git commit or push was performed. The final Git inventory contains 44 added files, 15 modified files, no removals, and no package/reference changes.

Known limitations: external delivery is represented by explicit skipped provider placeholders; no push/email/SMS/WhatsApp sending exists. There is no approval-lateness escalation or separate correction deadline, notification retention/archival policy, repeated reminder cadence for an already-delivered semantic event, or broad reassignment after execution starts. The processor snapshots relevant IDs and processes them transactionally; very large deployments may need a paged durable work queue and provider workers. Missing required roles must be corrected operationally; ADMIN is not silently substituted for MANAGER.

The recommended next business module is breakdown/corrective-maintenance management after separate approval. No breakdown or calibration implementation is started here.
