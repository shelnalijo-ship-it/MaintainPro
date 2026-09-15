# Work-order execution, evidence, submissions, and supervisor decisions

This module extends the existing backend from generated work orders through technician execution and final supervisor review. Earlier identity, machine, planning, checklist, recurrence, generation, calendar, and authorization behavior remains in place. There are no notification, escalation, inventory-management, breakdown, calibration, or frontend features in this change.

## Architecture and files

The existing five projects remain. Domain entities describe drafts and permanent history. Application services enforce transitions, ownership, validation, snapshots, and transactional auditing. Infrastructure configures PostgreSQL persistence and implements private local file storage. The API maps DTOs to services and applies the existing bearer authentication policies.

Application additions:

- `Application/Execution/ExecutionAccess.cs`, `ExecutionRules.cs`, `ExecutionHistory.cs`, and `ExecutionContracts.cs` centralize access, transitions, response validation, history sequencing, and contracts.
- `Application/Execution/WorkOrderExecutionService.cs` manages starting, current responses, comments, parts, defects, completion, and correction.
- `Application/Execution/WorkOrderEvidenceService.cs` manages authorized evidence upload, removal, and download.
- `Application/Abstractions/IFileStorageService.cs` separates storage operations from business logic.
- `Application/Reviews/WorkOrderSubmissionService.cs` validates and freezes a complete submission version.
- `Application/Reviews/WorkOrderReviewService.cs` provides the pending queue and exact-version approval/rejection.
- `Application/Reviews/WorkOrderHistoryService.cs` provides ordered work history and scoped approved machine history.
- `Application/Reviews/WorkOrderReviewContracts.cs` defines public submission, decision, queue, and history DTOs.

Infrastructure adds `Files/LocalFileStorageService.cs`, `Files/FileStorageOptions.cs`, and one EF configuration per new entity. Existing `ApplicationDbContext`, the application database abstraction, dependency injection, `WorkOrder`, work-order configuration, and generation history integration change. API startup and `Endpoints/ExecutionEndpoints.cs` register the module. The repository ignore rules exclude private development storage. New execution, submission, review, evidence, and API tests extend the existing suite.

Paths above are relative to `backend/MaintainPro.*` as indicated by their project/layer. The [complete file inventory](work-order-execution-files.md) lists 57 added and 13 modified files. No files were removed.

## Tables, relationships, and deletion behavior

Thirteen application tables are introduced:

| Table | Purpose |
| --- | --- |
| WorkOrderExecutions | One current execution record and execution-time technician identity per work order |
| WorkOrderChecklistResults | Current typed answers referencing immutable generated checklist items |
| SparePartUsages | Current basic part usage; no stock accounting |
| WorkOrderDefects | Current observations requiring possible follow-up |
| FileRecords | Private file metadata; binary bytes remain outside PostgreSQL |
| WorkOrderAttachments | Draft evidence associations, including optional checklist item and soft removal |
| WorkOrderSubmissions | Immutable submission headers and technician identity/time snapshots |
| WorkOrderSubmissionChecklistResults | Immutable submitted answers |
| WorkOrderSubmissionAttachments | Immutable exact file references, evidence types, and descriptions |
| WorkOrderSubmissionPartUsages | Immutable submitted part usage |
| WorkOrderSubmissionDefects | Immutable submitted defects |
| WorkOrderApprovals | One immutable final decision for an exact submission |
| WorkOrderHistoryEvents | Immutable ordered workflow events |

`WorkOrder` gains `SubmissionVersion` and `HistoryVersion` counters. Its existing `Version` concurrency token protects both counters and all execution writes. Unique indexes enforce one execution per work order, one current result per work-order/item pair, one submission per work-order/version pair, one final decision per submission, and one history event per work-order/sequence pair. Storage keys are unique. References to users, work orders, definitions, submissions, and files use `Restrict`.

Draft parts and defects can be removed while execution is editable; their submitted copies remain. Evidence removal marks the draft association deleted and retains both metadata and bytes. Submission headers/children, decisions, history events, file metadata, and prior generated checklist/definition snapshots reject tracked modification or deletion. Adding children to an already-persisted submission is also rejected. Approved work orders and their execution drafts are read-only through the application and its tracked EF changes.

These guards complement the service and database constraints. Direct SQL and bulk operations require trusted maintenance procedures; the module does not introduce database triggers or an independent database-permission system.

## Execution state and time

Allowed workflow:

1. An assigned technician starts a `PLANNED` or `ASSIGNED` job, producing `IN_PROGRESS`.
2. The technician saves draft work and marks execution complete. Completion preserves `IN_PROGRESS` so the technician can review before submission.
3. Submission changes the state to `AWAITING_APPROVAL` and locks the draft.
4. The assigned supervisor chooses `APPROVED` or `REJECTED` for that exact submission.
5. After rejection, the assigned technician explicitly resumes to `IN_PROGRESS`, corrects the draft, completes again, and submits the next version.

An unassigned `PLANNED` job cannot start. Cancelled, pending-review, and approved jobs cannot be executed or edited. There is no generic lifecycle setter.

`StartedAt` records the first start. `CompletedAt` records the latest completed technician attempt. `SubmittedAt` records the latest submission and is preserved when the supervisor decides. `ApprovedAt` is exclusively the supervisor approval time. Resuming clears current `CompletedAt`, retaining older times in the immutable submission. A meaningful draft edit after completion clears completion and starts another active interval, so the technician must complete again before submitting.

Duration sums active execution intervals in decimal minutes. Time spent awaiting a supervisor decision is excluded. Each submission freezes the accumulated duration at that version. This is elapsed execution timing, not an attendance or pause/resume timesheet system.

## Current checklist responses and completion validation

Current results reference `WorkOrderChecklistItem`, which is the generated immutable item, rather than a live plan/checklist template. The result API accepts the matching field only:

| Item type | Accepted answer and mandatory satisfaction |
| --- | --- |
| BOOLEAN | `booleanValue`, including `false` |
| PASS_FAIL | `passFailValue`: `PASS` or `FAIL` |
| NUMBER | Decimal `numericValue` |
| TEXT | `textValue`; mandatory text must contain content |
| CONFIRMATION | `confirmationValue`; mandatory confirmation requires `true` |
| PHOTO | Linked valid image evidence for that item |

Numbers report `BELOW`, `WITHIN`, or `ABOVE` against immutable item limits. An out-of-range reading is retained and warns the reader; it does not independently block submission. `FAIL` and boolean `false` are valid supplied answers. Optional unanswered items can remain empty.

Saving checklist results updates only the supplied items. Duplicate item IDs, items belonging to another work order, incompatible answer fields, and invalid enum values are rejected. Comments and observations are supported. Before submission the service checks completion, every mandatory response, every item photo requirement, the plan-level image requirement and minimum image count, required overall comments, and finalized nondeleted evidence. Validation errors are returned without creating a partial submission.

## Parts and defects

Basic part usage records part name, optional number, positive decimal quantity, remarks, creator, and creation time. Technicians can add/edit/remove these records while execution is editable. No inventory balance, supplier, purchasing, reservation, or reorder logic exists.

Defects record title, description, optional severity text, a follow-up flag, creator, and timestamps. They are maintenance findings, not new breakdown or corrective-maintenance jobs. Every submission copies current parts and defects into new immutable rows.

## Private file storage and evidence

`IFileStorageService` can later be implemented for Azure Blob or S3-compatible storage. The development implementation uses `FileStorage:RootDirectory`, defaulting to `.maintainpro-storage` relative to the server process working directory. The directory is private storage, not a static web directory. No user-specific path is hardcoded and no actual environment configuration or secrets need to be changed to use the default.

`FileStorage:MaxFileSizeBytes` defaults to 10 MiB. Configuration accepts 1 byte through 100 MiB; request/multipart limits allow corresponding protocol overhead. Both declared length and actual streamed byte count are checked.

Uploads accept `.jpg`, `.jpeg`, `.png`, and `.pdf` with matching MIME types and recognizable file structure. Extension alone is insufficient. Original filenames containing paths, traversal components, or control characters are rejected. Physical storage keys are generated opaque identifiers; only their narrow validated format can resolve to a stored file.

Evidence categories are `BEFORE_MAINTENANCE`, `DURING_MAINTENANCE`, `AFTER_MAINTENANCE`, `DEFECT`, `REPLACED_PART`, and `OTHER`. Evidence can optionally reference an immutable item in the same work order. JPEG and PNG images satisfy photo requirements; supporting PDFs do not count as maintenance photos.

Upload persists complete bytes before saving the metadata/association transaction. A pre-commit failure cleans up newly stored bytes. An uncertain commit outcome retains bytes to avoid deleting a potentially committed reference. Removal hides the draft attachment, but earlier submissions retain access to their exact file IDs.

Downloads pass work-order authorization through `GET /api/v1/files/{id}`. They expose neither storage keys nor local paths and use attachment download disposition, `nosniff`, and private/no-store caching. A technician can access evidence for assigned work, a supervisor for supervised work, and managers/admins for management-visible work. Access can arise from a current nondeleted draft association or a preserved submitted association.

The local implementation validates format structure; it does not fully decode all image/PDF content, scan malware, transcode files, implement quotas, or provide distributed/object-store durability. Multiple API instances using local storage must share the same private directory. Backups must preserve both PostgreSQL metadata and the corresponding storage bytes. Removed draft-only files and uncertain-commit orphans require a future retention/reconciliation policy; this module intentionally preserves historical bytes.

## Submission versions and immutable facts

Submission is one serializable transaction: validate current work, increment `WorkOrder.SubmissionVersion`, create the header and all four child snapshot collections, set `AWAITING_APPROVAL`/`SubmittedAt`, create an ordered history event, and append the audit entry. The parent concurrency token and unique version index prevent concurrent calls from allocating the same version. A duplicate submit is rejected once the job is pending review; it does not create an accidental extra version.

The header freezes the execution-time technician ID, employee ID, name, comments, observations, start/completion/submission times, and duration. Children freeze answers and their immutable item references, exact file IDs and evidence metadata, part usage, and defects. Historical reads query these submitted tables, never the mutable current draft. Item labels, type, units, and bounds come from the already-immutable generated item. File filename/MIME/size metadata is itself immutable.

Version 1 remains retrievable after rejection, draft correction, version 2 submission, and eventual approval. No submitted values are replaced by later draft values or master-data names.

## Supervisor decisions and queue

Approval/rejection requires the `SUPERVISOR` role and the exact work-order supervisor ID. Manager/admin roles provide management reads, but do not grant a review override. A user who also has `SUPERVISOR` can decide only when actually assigned as supervisor. A submission's technician cannot review their own work.

The request identifies `submissionId`. The service requires `AWAITING_APPROVAL`, the latest version, no previous final decision, and the exact work-order/submission relationship. Rejection requires nonblank remarks. Approval remarks are optional. The immutable decision freezes the supervisor ID, employee ID, name, decision, remarks, and UTC time. Decision, lifecycle change, event, and audit entry commit atomically. Unique submission review indexing and the work-order token protect competing decisions.

The pending queue is visible to supervisors for their supervised jobs and to managers/admins for all jobs. It applies security before counts/pagination, then supports priority, inclusive UTC submitted dates, technician, machine, and `overdue` filters. Default ordering is critical/high priority first, then past-due review items, then oldest submission. Priority ordering uses an explicit numeric rank because priority values are stored as text.

The queue field **`reviewOverdue` means `DueDate < today in UTC` while the job is awaiting review**. Its `overdue` query filter uses this definition to prioritize older pending jobs. Existing general work-order `overdue` remains unchanged and excludes `AWAITING_APPROVAL`, `APPROVED`, and `CANCELLED`. This queue flag does not perform escalation, send reminders, or introduce an extra lifecycle state.

## History and authorization

Major workflow actions have persisted `WorkOrderHistoryEvent` records. A parent counter assigns each event a unique sequence in the same transaction as its action, preserving order even when multiple actions share a timestamp. Generation records generated/assigned events for new jobs. Older jobs fall back to their existing generated/assigned audit facts when no corresponding event exists; history is not reconstructed from today's lifecycle or names.

The history response includes actor snapshots, submission ID/version when applicable, action, UTC time, details, and sequence. Submission endpoints independently return every version and exact decision. Machine maintenance history returns approved jobs and their approved version, original plan name, technician and deciding supervisor snapshots, dates, and duration.

All work-order/execution/submission/history reads use the union of applicable permissions: assigned technician, assigned supervisor, or manager/admin visibility. Machine history first checks machine/current-or-historical assignment visibility and still scopes each returned approved job. Current ownership never grants a technician another technician's work history. Unrelated resources return 404; known but disallowed operations return 403; conflicting workflow transitions return 409. Validation errors use the existing problem-details response behavior.

## API routes and example contracts

All routes use `/api/v1` and the existing bearer authentication. Entity objects and physical file paths are not response contracts.

| Method and route | Behavior |
| --- | --- |
| POST `/work-orders/{id}/start` | Assigned technician starts work |
| GET `/work-orders/{id}/execution` | Authorized current execution view |
| PUT `/work-orders/{id}/checklist-results` | Assigned technician saves supplied result items |
| PUT `/work-orders/{id}/execution-comments` | Saves overall comments and observations |
| POST `/work-orders/{id}/parts` | Adds basic part usage |
| PUT `/work-orders/{id}/parts/{partId}` | Edits current part usage |
| DELETE `/work-orders/{id}/parts/{partId}` | Removes current part usage |
| POST `/work-orders/{id}/defects` | Adds a defect observation |
| PUT `/work-orders/{id}/defects/{defectId}` | Edits a current defect |
| DELETE `/work-orders/{id}/defects/{defectId}` | Removes a current defect |
| POST `/work-orders/{id}/attachments` | Uploads multipart private evidence |
| DELETE `/work-orders/{id}/attachments/{attachmentId}` | Soft-removes current evidence |
| POST `/work-orders/{id}/complete` | Records completion and active duration |
| POST `/work-orders/{id}/submit` | Creates the next immutable submission |
| POST `/work-orders/{id}/resume` | Resumes a rejected job for correction |
| GET `/work-orders/pending-approvals` | Scoped, filtered pending review queue |
| GET `/work-orders/{id}/submissions` | Summaries of all versions and decisions |
| GET `/work-orders/{id}/submissions/{submissionId}` | Exact immutable submitted record |
| POST `/work-orders/{id}/approve` | Assigned supervisor approves the exact version |
| POST `/work-orders/{id}/reject` | Assigned supervisor rejects with a reason |
| GET `/work-orders/{id}/history` | Ordered persisted operational history |
| GET `/machines/{id}/maintenance-history` | Paged approved maintenance history |
| GET `/files/{id}` | Authorized private download |

Checklist request example (replace the item ID with one from the generated definition):

```json
{
  "results": [
    {
      "workOrderChecklistItemId": "00000000-0000-0000-0000-000000000001",
      "numericValue": 12.5,
      "comment": "Measured after maintenance."
    }
  ]
}
```

Comments request:

```json
{"overallComments":"Maintenance completed.","observations":"No additional issues observed."}
```

Part request:

```json
{"partName":"Seal","partNumber":"SEAL-01","quantity":1,"remarks":"Replaced worn seal."}
```

Defect request:

```json
{"title":"Cover damage","description":"Small crack found during inspection.","severity":"Low","requiresFollowUp":true}
```

Multipart evidence fields are `file`, `evidenceType`, optional `workOrderChecklistItemId`, and optional `description`. Start, complete, submit, and resume require no request body. Review requests contain the exact version's ID:

```json
{"submissionId":"00000000-0000-0000-0000-000000000002","remarks":"Please add a clear after-maintenance photo."}
```

Pending query parameters are `page`, `pageSize`, `priority`, `submittedFrom`, `submittedTo`, `overdue`, `technicianId`, and `machineId`. Machine history accepts `page` and `pageSize`. Defaults are page 1 and 20 items, with at most 100 items per page.

Submission detail wraps a `submission` summary, overall comments, observations, start time, and `checklistResults`, `attachments`, `partUsages`, and `defects`. The summary includes its immutable technician identity, completion/submission times, duration, and optional exact `review`. No DTO contains password hashes, access/refresh tokens, binary bytes, or storage paths.

## Auditing, concurrency, and failure handling

Start, meaningful checklist changes, comments, parts, defects, evidence, completion, submission, review decisions, and resume are audited within their workflow transactions. Major state transitions also create ordered history events. Audit entries contain identifiers and safe scalar facts, not uploaded binary contents or credential values.

Each draft mutation rotates the parent work-order token, even with a fixed clock or an otherwise unchanged parent row. This makes draft edits contend with submission, completion, and review so a stale edit cannot commit after the draft is locked. A concurrent conflict rolls back the whole transaction and returns the existing conflict behavior; callers should reload before retrying. Submission/decision requests are not silently applied to another version.

The existing daily generation scheduler remains responsible only for due work creation. It does not execute, submit, review, notify, or escalate jobs.

## Packages, migration, validation, and scope boundary

No new external package is required for this module's application services or development file storage. The implementation uses existing EF Core, Npgsql, ASP.NET Core, and test infrastructure.

Migration `20260915045159_WorkOrderExecutionAndApproval` was generated, reviewed, and successfully applied to `maintainpro_db` after the complete test suite passed. Its [SQL](database/WorkOrderExecutionAndApproval.sql) and [review](database/WorkOrderExecutionAndApproval-review.md) record 13 new tables, two work-order counters, 35 indexes including eight unique indexes, and 33 `Restrict` foreign keys. It retains existing tables and indexes and contains no drops. The pre-existing work-order supervisor index is explicitly retained while the new pending-approval index is added.

Validation commands run from the `backend` directory (the final test and EF checks reused the successful build with `--no-build`):

```text
dotnet build MaintainPro.slnx
dotnet test MaintainPro.slnx
dotnet ef migrations has-pending-model-changes --project MaintainPro.Infrastructure --startup-project MaintainPro.Api --context ApplicationDbContext
```

Final build succeeded with zero warnings and zero errors. The complete suite passed **326 tests, zero failures, zero skips**, preserving the original 205 tests and adding 121 execution cases. Coverage includes full HTTP rejection/correction/resubmission/approval, all response types and range warnings, evidence formats/size/path controls, file access, mandatory validation, part/defect snapshots, role combinations, exact decisions, ordered history, stale-context conflicts, immutable rows, and transactional audit failure paths. EF reported **no pending model changes** after migration application.

Read-only PostgreSQL inspection immediately before and after application established:

| Verified property | Before | After |
| --- | --- | --- |
| Application tables | 18 | 31 |
| Applied migrations | 3 | 4 |
| Foreign keys | 32 | 65 |
| Foreign keys with a non-Restrict delete action | 0 | 0 |
| Unique secondary indexes, excluding primary keys | 15 | 23 |
| Role rows | 4 | 4 |
| Rows in the other original application tables | 0 | 0 |

All original table names and row counts were preserved. All 13 new tables were empty. The inspector compared every expected EF table, index name/uniqueness, and validated Restrict foreign key against PostgreSQL metadata: no differences were found. No business test rows were created in the live database.

A temporary Development API instance returned HTTP 200/Healthy for `/health` and HTTP 200 for OpenAPI. OpenAPI included multipart evidence and bearer security. Unauthenticated pending-approval and file requests returned 401. The temporary server was stopped after verification.

Tests used ephemeral SQLite databases and temporary local evidence directories, with cleanup. Stale-context tests prove optimistic concurrency/uniqueness behavior; PostgreSQL parallel workflow races were not exercised. Live verification was limited to schema/data preservation, startup, health, documentation, and unauthenticated access. JWT signing and bootstrap settings remain unconfigured in the development environment; authenticated HTTP tests used isolated generated test credentials. The sandbox live probe logged existing ephemeral Data Protection and missing HTTPS-port warnings.

Known functional limits: no supervisor override, delegated reassignment, execution pause tracking, submission deletion, notification delivery, reminder/escalation engine, stock inventory, file malware scanning, distributed file lifecycle management, or user interface. The recommended next module is notifications and overdue reminders/escalation, as separately authorized by the user.

`maintainpro_db` was modified only by the authorized additive migration. No other PostgreSQL database or persistent application database was modified; automated tests created and discarded isolated SQLite databases. No secrets were printed, enumerated, moved, or changed. No Git commit, push, deployment, or environment-configuration change was performed. Work stopped at this module; notifications and escalation were not started.
