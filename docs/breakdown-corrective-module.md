# Breakdown, corrective maintenance, and return to service

This module adds breakdown reporting, corrective execution, evidence, immutable versioned submissions, supervisor decisions, downtime, machine restoration, audit/history, and protected APIs. Calibration, external services, inventory control, reporting exports, and frontend work remain out of scope.

## Domain and persistence

The new aggregate is `Breakdown`, with `BreakdownNumberSequence`, immutable assignment/history events, `CorrectiveActionDraft`, `CorrectivePartUsage`, `CorrectiveSubmission`, immutable submission parts/evidence, `CorrectiveApproval`, and durable `BreakdownNotificationEvent`. All references use restricted deletion. The additive migration is `20260915062834_BreakdownAndCorrectiveMaintenance`; its [reviewed SQL](database/BreakdownAndCorrectiveMaintenance.sql) creates 12 tables, adds `Machine.StatusVersion`, 9 unique indexes, and 29 restricted foreign-key clauses. No existing table is dropped or recreated.

Breakdown numbers use a serializable annual sequence and `BD-YYYY-NNNN`. Assignment, submission, history, approval, and notification keys are unique where required. Original report facts, submitted versions, decisions, evidence metadata, and history are retained; only explicitly permitted draft fields, soft deletion, and one-way return-to-service timestamps can change.

## Lifecycle and authorization

Reporting creates `REPORTED`. Assignment moves it to `ASSIGNED`; the assigned active technician may start, edit, complete, and submit (`IN_PROGRESS` → `AWAITING_APPROVAL`). Rejection moves it to `REJECTED`, after which the assigned technician may resume and submit a new version. Approval references the exact latest submission and moves the breakdown to `CLOSED`. Return to service is a separate authorized action. `CANCELLED` is reserved for future cancellation behavior.

Technicians see assigned or reported breakdowns, supervisors see supervised breakdowns, and managers/admins see all. Corrective edits require the assigned technician; review and return require the assigned supervisor or manager/admin, with no self-review or manager override of an unrelated supervisor assignment. Filters and pagination are applied after this visibility scope.

## Downtime and machine state

For stopped machines, physical downtime starts at server `ReportedAt` and ends only at explicit return to service; it includes supervisor review waiting. Submission records snapshot downtime at submission time, while reads continue accruing until return. Non-stopped breakdowns have zero downtime. Overlapping stopped breakdowns share the original safe operational state and are all timestamped when the final approved stop is returned. Decommissioned, out-of-service, independently changed, or inconsistent states fail closed with a clear conflict; generic machine updates cannot bypass an outstanding stopped breakdown.

## Evidence and notifications

Breakdown evidence reuses `FileRecord`, `IFileStorageService`, and the existing authorized file-download route. Report authors may add initial evidence; assigned technicians may edit evidence during corrective execution. Submitted snapshots remain immutable and downloads require breakdown visibility. No local filesystem paths are exposed.

Critical, reported, assigned, submitted, approved, and rejected events use the existing in-app notification writer and explicit provider placeholders. Recipients are active users with the required role; multi-role users are deduplicated, missing recipients remain pending for retry, and no Firebase/email/SMS provider is called.

## APIs

Breakdown routes include reporting/list/detail, assignment and assignment history, breakdown history, machine breakdown history, start/resume/complete, corrective draft and parts, evidence upload/delete, submission list/detail, approve/reject, return-to-service, and protected pending-notification processing. All routes are under `/api/v1`; entities are never returned directly.

## Validation

The baseline was 418 passing tests. After implementation, the full suite passed **500 tests**, with zero failures or skips, and the build passed with zero warnings/errors. PostgreSQL `maintainpro_db` now has 48 application tables, 6 migrations, 101 foreign keys (all `RESTRICT`), and 37 unique indexes. Existing row counts and four role definitions were preserved; all new tables are empty. The database inspector reports `ModelTablesIndexesAndForeignKeysMatch=true` with no differences, and EF reports no pending model changes.

No other persistent database was modified. Secrets were not printed or moved. No commit, push, deployment, or environment configuration change was made. Temporary SQLite databases were used by tests; PostgreSQL multi-session workflow concurrency was not exercised with business fixtures.

Known limits are the deferred external providers, cancellation endpoint, approval-lateness policy, aggregate downtime reporting, and calibration module. The next recommended module is calibration only after separate approval.
