# MaintainPro requirements comparison and implementation plan

Reviewed against the [Functional Requirements Document (FRD) version 2.0](functional-requirements-v2.md), dated 14 September 2026, and the subsequent [UI, workflow, database, and API specification version 3.0](ui-workflow-database-api-specification-v3.md). Both supplied documents are preserved unchanged.

This document compares the existing code with the supplied requirements and records the implementation plan. The later approved decisions and module implementation below supersede the original scaffold assessment; preventive maintenance and work-order workflows remain unimplemented.

Version 3 adds 19 mobile screens, 16 web screens, a conceptual color palette, database field lists, and API routes. Its explicit additions are incorporated below; omissions are not treated as cancellation of earlier requirements. The [Version 3 schema and API review](specification-v3-review.md) records the original contract findings.

**Approved decisions and current phase:** users have multiple roles through `User`, `Role`, and explicit `UserRole`; basic external-service storage is included in MVP; work-order lifecycle and overdue/escalation state will be separate. The user subsequently authorized the full backend foundation, identity, and machine management module, including reviewed forward migrations against an empty `maintainpro_db`. That module now includes ten entities, secure identity/session workflows, named authorization policies, user/master-data/machine APIs, assignment history, auditing, and tests. `InitialCreate` and `IdentityAndMachineManagement` have been applied and the four roles initialized. See the [current backend module report and setup instructions](backend-module.md). The [initial migration review](database/InitialCreate-review.md) remains the historical record of the earlier approval boundary. WorkOrder, external-service implementation, files, and frontend work remain future modules.

## 1. Original scaffold assessment

This table describes the repository before the foundation phase. The current implementation is documented in the backend module report linked above. The unused `Class1.cs` placeholders and empty scaffold test have been replaced or removed; the database schema has now been created through the authorized migrations.

The repository contains a .NET 10 backend solution with five projects. The installed local SDK reports version 10.0.401.

| Project or area | Observed implementation | Gap against the FRD |
| --- | --- | --- |
| `backend/MaintainPro.Api` | Minimal API startup, development OpenAPI, HTTPS redirection, sample `/weatherforecast` endpoint | No maintenance API, authentication, authorization policies, or application service registration |
| `backend/MaintainPro.Domain` | Empty `Class1` | No machine, plan, checklist, work order, approval, or other business entities and rules |
| `backend/MaintainPro.Application` | Empty `Class1`; references Domain | No use cases, validation, permission checks, or interfaces for persistence and external services |
| `backend/MaintainPro.Infrastructure` | Empty `Class1`; references Application and Domain; EF Core design and Npgsql PostgreSQL packages present | No database context, mappings, migrations, storage, notification delivery, or scheduled processing |
| `backend/MaintainPro.Tests` | One empty xUnit test; references Application | No assertions or functional coverage |
| Manager web application | No project present | All requested web screens and workflows remain to be built |
| Technician/supervisor mobile application | No project present | All requested mobile screens and workflows remain to be built |

The solution already has a useful separation of responsibilities. Keep it and implement features within those projects. A package reference alone does not establish that a database is connected or that a feature works.

Repository housekeeping update: the authorized cleanup added `.gitignore` and removed 318 backend `bin/obj` files from Git's index while retaining every file on disk. The resulting staged generated-file deletions are separate from existing source changes; no source files were staged.

## 2. Requirements that need a decision

### Initial release boundary

Version 3 section 93 explicitly includes calibration in the MVP, and section 92 schedules certificates, expiry monitoring, renewal reminders, and certificate history. Include these in the initial implementation; FRD acceptance criteria 17-18 are no longer awaiting a phase decision. Version 3 section 92 also schedules corrective maintenance and supervisor closure with breakdown management.

The user approved basic external-service storage as part of MVP, resolving FRD acceptance criterion 19. Milestone 10 must deliver at least basic service/report storage for the initial release even though Version 3 omitted it from the sprint sequence. External-service implementation remains excluded from the current domain/database foundation phase.

Advanced analytics and full inventory remain later work. Deliver the dashboard indicators and charts explicitly required by Version 3 W01, with backend-defined calculations; do not defer those screens merely because richer analytics appeared in a later FRD phase.

### Workflow and permission details

| Decision | Why it matters | Proposed approach for review |
| --- | --- | --- |
| Lifecycle status versus timing | FRD section 27 and Version 3 section 90 mix lifecycle states with overdue/escalated conditions | Approved: model lifecycle separately from overdue/escalation state. API/filter details remain to be defined; do not implement WorkOrder in this phase |
| Completion and approval labels | Version 3 section 90 omits the FRD's `Completed` state and lists `APPROVED`, but section 44 labels the approval outcome `Closed` | Proposed: completion records a timestamp; submission enters `AWAITING_APPROVAL`; maintenance approval ends in `APPROVED`; reserve `CLOSED` for breakdowns. Confirm this mapping before freezing contracts |
| Overdue work awaiting review | Escalation recipient depends on whether execution or approval is late | Define when execution becomes complete and whether supervisor review has its own deadline |
| Supervisor visibility and reassignment | Supervisors see assigned technicians and machines; ownership differs from work assignment | Define team boundaries and check access to each record in the backend; never grant access solely from a role name |
| Manager and administrator permissions | The user approved multiple roles instead of Version 3's single `USERS.role_id` | Implemented: composite `(UserId, RoleId)` key, four standard roles, named policies, ADMIN-only user management, manager/admin machine writes, and the union of current owner/supervisor visibility. ADMIN does not implicitly qualify as a machine supervisor |
| Calendar and recurrence | End-of-month dates, time zones, plan edits, inactive plans, and missed scheduler runs affect due dates | Choose a company time zone and explicit recurrence rules; generate each plan occurrence once and preserve existing work order history |
| Reminder delivery | Push, in-app, and optional email have different dependencies | Record delivery attempts, retry failures, and prevent duplicate event notifications; choose delivery providers before integration |
| Calibration renewal | FRD section 40 mixes computed validity with `Renewal in Progress`; Version 3 stores `status` without describing recalculation | Preserve expiry information while tracking renewal separately; a pending renewal should not conceal an expired certificate |
| Web and mobile technology | The FRD offers alternatives and neither client exists | Select the client stack before scaffolding; retain the existing .NET backend and PostgreSQL direction |

Version 3 also makes historical facts independent of current ownership, employee details, checklist versions, and certificate versions (section 95). Treat versioned submissions and retained facts as part of the first schema design, rather than retrofitting them after approvals exist.

## 3. Responsibilities within the existing projects

- **Domain:** entities and business rules, such as required checklist answers, valid state transitions, and immutable approval history. Keep these independent of HTTP and database libraries.
- **Application:** use cases such as create machine, assign work, submit maintenance, and review maintenance; permission evaluation; request/result types; interfaces for persistence, current user, clock, files, and notifications.
- **Infrastructure:** database context and entity mappings, identity persistence, secure file storage, notification delivery, scheduled processing, and implementations of Application interfaces.
- **API:** authenticated endpoints, request binding, consistent error responses, and registration of services. Both clients use the same use cases.
- **Tests:** business-rule tests, use-case authorization tests, database integration tests, and API workflow tests as each feature arrives.

The next sections preserve the broader release plan. Milestone 1's backend identity work and milestone 2's backend machine work are implemented; file workflows, password recovery, and client screens remain pending. The current module report records actual paths and delivered behavior where they differ from earlier proposals. Add abstractions when a working feature needs them rather than creating empty modules for every future entity.

## 4. Incremental delivery sequence

### Milestone 1: Identity, protected API, files, and UI foundations

**Purpose:** establish individual users and backend permission enforcement before exposing company records, plus the file and navigation foundations requested in Version 3 sprint group 1.

**Files and areas:** `MaintainPro.Infrastructure/Identity/`, `MaintainPro.Infrastructure/Persistence/`, `MaintainPro.Infrastructure/Files/`, `MaintainPro.Application/Authentication/`, `MaintainPro.Application/Files/`, `MaintainPro.Api/Endpoints/`, `MaintainPro.Api/Program.cs`, and corresponding tests. Proposed client roots are `web/` and `mobile/`, with reusable theme, components, navigation, and API-client areas after stack selection. Update project references/packages only where the implementation needs them.

**Behavior:** create individual users, login, logout/session invalidation, role assignment, user activation/deactivation, and password reset. Define a controlled first-administrator setup without hardcoded credentials. Enforce permissions in backend use cases as well as at endpoint boundaries. Persist audit events for access and role changes without recording secrets.

Use the specified `/api/v1/` route prefix and authentication routes, including refresh and `auth/me`. Agree refresh/session contracts before implementation. Establish authorized file upload/metadata and a way to attach/download files through permitted records. Build M01/M02, web sign-in, role-aware navigation, and reusable Onam theme components. Add record-specific upload behavior as those modules arrive.

**Checks:** reject unauthenticated requests; enforce all four roles and combined roles; reject inactive users and expired/revoked sessions; verify reset tokens are single-use and expire; check role changes affect subsequent access; verify refresh credential rotation/replay behavior after agreeing the contract. Exercise real database persistence, restricted file access, mobile session routing, and web login. Confirm shared components render clear validation, loading, and connection-error states.

Environment configuration, service credentials, applying migrations to an existing database, and deployment require explicit user authorization. Prepare code and reviewable migration files first.

### Milestone 2: Machine master and ownership

**Purpose:** provide the central asset record to which maintenance and evidence will belong.

**Files and areas:** `MaintainPro.Domain/Machines/`, `MaintainPro.Application/Machines/`, Infrastructure entity mappings and migrations, API machine endpoints, W02/W03 manager screens, M14/M15 mobile views, and tests.

**Behavior:** create, view, search, and edit machines; maintain machine status and master data; assign a primary technician and supervisor; preserve ownership/status changes in the audit trail. Start with required operational fields and add the remaining machine-master fields from FRD section 20 and Version 3 section 29 incrementally. Preserve decommissioned assets and their histories.

Persist dated assignment history from Version 3 section 54. Show maintenance/calibration/document/breakdown tabs as their modules become available. Distinguish features pending implementation from a real empty result. The final MVP must include the tab content required by its delivered modules.

**Checks:** enforce unique machine IDs at the database level; validate references and assignments; restrict technicians and supervisors to permitted records; distinguish machine ownership from temporary work assignment; persist updates across requests; verify audit before/after values and actor.

### Milestone 3: Checklists, plans, and automatic work orders

**Purpose:** turn recurring maintenance plans into concrete technician jobs.

**Files and areas:** Domain and Application folders for `Checklists`, `MaintenancePlans`, and `WorkOrders`; Infrastructure mappings/migrations and `Scheduling/`; API endpoints; manager checklist/plan/calendar screens; tests.

**Behavior:** reusable and versioned checklist templates with required/optional items, ordering, units, and expected numeric limits; recurring plans with supported frequencies; plan duplication/deactivation; work order generation; technician and supervisor assignment; due dates and priorities. W05 also configures required photo count and mandatory comments. Copy the applicable checklist, instructions, evidence rules, and historical facts into each generated work order so later edits do not change existing records. Restrict checklist deletion once associated history exists, as specified in Version 3 section 77.

**Checks:** cover supported day/week/month intervals, leap years and month ends; inactive plans; plan edits; missed runs and catch-up rules; concurrent/retried generation without duplicates. Verify an approval does not shift the next planned occurrence unless an explicit scheduling rule permits it.

Define request/response contracts for W04-W07 and work-order reads before connecting clients. If adopting the recommended `WO-YYYY-NNNN` numbering, specify collision-free allocation and behavior beyond four digits and at year rollover. A human-readable number must remain distinct from the record's internal ID.

### Milestone 4: Mobile execution and secure evidence

**Purpose:** deliver the technician path from assigned work to a valid submission.

**Files and areas:** Application work-order use cases and attachment interfaces; Infrastructure file storage and persistence; API execution/upload endpoints; mobile login, dashboard, jobs, machine details, checklist, readings, evidence, and submission screens; tests.

**Behavior:** deliver M03 and M05-M10: sort/filter jobs as specified, start work, save progress, complete checklist items, enter readings/comments, record parts used and defects, capture categorized photos, upload documents, and submit. Store files outside the relational database with authorized references. Preserve drafts through interrupted connectivity and show upload failures clearly. Confirm an upload is finalized before accepting it as mandatory evidence. Show M08 out-of-range readings as warnings; whether any limit blocks submission remains a separate rule to define.

**Checks:** reject missing mandatory answers/readings/photos/comments and insufficient required photo counts; reject edits by unrelated technicians; validate file type and size and enforce attachment access through the linked record; prevent duplicate submissions on retries; retain entered data after failed saves/uploads; test camera and upload behavior on supported devices. Verify photos deleted before submission no longer satisfy evidence rules. Basic part usage does not require the future inventory module.

### Milestone 5: Supervisor review and retained history

**Purpose:** complete the core approval and rejection loop.

**Files and areas:** Domain workflow rules; Application submission/review and assignment use cases; Infrastructure submission, approval, rejection, and audit records; API review/history and assignment endpoints; supervisor mobile dashboard, workload, assignment/reassignment, and approval queue/review screens; manager history screens; tests.

**Behavior:** display supervised work and technician workloads; assign/reassign work within authorized scope; review the exact submitted evidence; approve or reject within authorized scope; require rejection reasons; allow correction and resubmission; retain previous submissions and reviewer comments. Record submission author, reviewer, timestamps, and duration. Prevent approval of one's own submitted work, including when a user has multiple roles or assignments change.

Deliver M04 and M11-M13, including critical/overdue/oldest-submission approval sorting. Add W08 manager actions for reassignment, audited due-date changes, cancellation, and comments. Define allowed states for these actions so approved evidence cannot be rewritten and changes to due dates have explicit reminder/escalation consequences.

**Checks:** full submit/reject/correct/resubmit/approve scenario; self-approval attempts; unrelated supervisor attempts; missing rejection reason; duplicate or competing review requests; evidence changes during review; immutable approved records; retrievable machine history. Save workflow state, review history, and audit events atomically.

### Milestone 6: Notifications, reminders, and escalations

**Purpose:** make assignments and overdue work visible without manual chasing.

**Files and areas:** Application notification/escalation rules; Infrastructure scheduled processing, durable pending-delivery records, delivery adapters, and migrations; API notification/settings endpoints; mobile notifications and manager escalation screens; tests.

**Behavior:** in-app history with read/unread status; push delivery through the selected provider; authenticated device-token registration, refresh, and removal on logout/account changes; optional configured email; assignment/reassignment, submission/review, upcoming/due/overdue, and escalation events. Configure reminder thresholds and recipients and expose last notification time and escalation level. Make scheduling and retries resilient to restarts.

**Checks:** controllable-clock boundary cases for configured thresholds; correct technician/supervisor/manager recipients; no duplicate notification for the same event; delivery retries after failure; revoked device tokens; no execution reminders for cancelled/approved work; separate treatment of pending approvals according to the agreed rule.

### Milestone 7: Breakdown reporting, corrective work, and closure

**Purpose:** deliver Version 3 sprint group 6, including the supervisor-reviewed repair workflow.

**Files and areas:** Domain/Application `Breakdowns/` and `CorrectiveMaintenance/`; Infrastructure mappings and history/attachment persistence; API breakdown and corrective-action endpoints; M16/M17 and W09; tests.

**Behavior:** record stopped/non-stopped machines, severity, observations, and evidence; notify the assigned supervisor and manager immediately for critical breakdowns; assign repairs; record root cause, action, parts, duration, and downtime; submit corrective work for supervisor closure. Preserve reported, assigned, in-progress, awaiting-approval, and closed states. Define machine return-to-service rules when other open breakdowns or restrictions remain.

**Checks:** critical notification routing; assignment scope; complete corrective evidence; closure review and self-approval protection; downtime calculations for open and overlapping breakdowns under the agreed definition; no return to service merely because one of several unresolved breakdowns closes. Retain lifecycle history and document access after ownership changes.

### Milestone 8: MVP calibration management

**Purpose:** deliver the calibration module explicitly included in the Version 3 MVP and required by FRD acceptance criteria 17-18.

**Files and areas:** Domain/Application `Calibration/`; Infrastructure mappings, reminder processing, and secure certificate associations; API calibration endpoints; W10/W11 and calibration portions of M04/M14/M15/W01; tests.

**Behavior:** append new calibration certificates while retaining old records and attachments; computed validity plus renewal tracking; configurable 60/30/7-day and expiry reminders; renewal history; dashboard expiry windows and machine calibration views. Specify whether dashboard counts refer to machines/current certificates or all historical certificates and whether the expiry windows overlap.

**Checks:** renewals preserve every previous certificate and attachment; expiry/reminder boundaries and duplicate handling; no obsolete expiry alerts after a valid replacement; restricted certificate access; correct machine linkage; consistent counts and validity across screens. Uploading a replacement must create a new historical record rather than overwrite an existing certificate file.

### Milestone 9: Management reports, KPIs, and completed portal views

**Purpose:** finish Version 3 sprint group 8 after the underlying workflows supply meaningful data. Build thin management views alongside earlier features; complete cross-module calculations here.

**Files and areas:** Application dashboard, reporting, calendar, audit, and settings queries/use cases; Infrastructure query implementations and export generation; API dashboard/report/audit/settings contracts; W01/W06/W07/W08/W13-W16 and relevant mobile KPI cards; tests.

**Behavior:** manager exceptions, approval/escalation panels, daily/weekly/monthly calendar with a work-order side panel, consistent filters, and all seven report categories. Generate PDF/Excel reports on the server. Return dashboard KPIs from the backend, as required by Version 3 section 83. Deliver W01 charts, user administration, configurable master data and notification/escalation settings, and read-only audit review. Keep web navigation role-aware, including administrator and permitted supervisor access.

**Checks:** counts and charts match source records under filters; compliance denominators and date boundaries are defined; calendar opens the correct work order; exports honor scope/filters and include company, title, period, generation date, and requesting user; files open in appropriate viewers; audit records cannot be edited through the API. Check mobile/desktop layouts and all empty, loading, validation, and error states.

### Milestone 10: External services and document management

**Purpose:** deliver basic external-service storage approved for MVP and required by FRD acceptance criterion 19. This is a future implementation phase, outside the current foundation work.

**Files and areas:** Domain/Application `ExternalServices/` and document use cases; Infrastructure service records, multiple file associations, and report queries; API service/document contracts; M18/W12 and shared machine document views; tests.

**Behavior:** record provider, technician, service date/type, work description, findings, recommendations, follow-up, optional costs/references, and multiple uploaded reports. Support authorized downloads and machine history. Define the Documents sidebar screen and its related routes, which Version 3 names without a numbered screen specification.

**Checks:** report/photo upload and download; record/file permission checks; multiple attachments preserved; provider and service facts remain accurate in history; edits are audited; machine linkage and follow-up filters work. Basic service/report storage must pass before claiming all 22 original acceptance criteria are met.

### Later phases

Later phases add deeper downtime, reliability, root-cause, workload, and maintenance KPI analysis beyond the Version 3 dashboard requirements. Full inventory adds spare-part master data and stock movements. Keep Version 3 section 94 and FRD section 80 exclusions out of the initial implementation, including QR scanning and ERP/IoT integration.

## 5. Version 3 UI and sprint mapping

### Reusable UI foundations

Convert the supplied conceptual palette into shared semantic color tokens for both clients. Keep values centrally defined rather than styling every screen separately. These are the values supplied by Version 3 section 3, not newly selected colors:

| Palette name | Supplied approximate value | Intended use |
| --- | --- | --- |
| Primary Green | `#165B3A` | Primary actions, active navigation, approved/normal status |
| Secondary Green | `#DDEADF` | Background highlights and success surfaces |
| Kasavu Gold | `#C99A3D` | Highlights, borders, icons, and calendar accents |
| Deep Maroon | `#851B2B` | Branding and important highlights |
| Ivory | `#FAF7EF` | Main application background |
| Soft Terracotta | `#C96F4A` | Subtle decorative accents |

Critical Red has no supplied numeric value, and W07 introduces a scheduled-event Blue outside the palette. Version 3 also uses Maroon for breakdown calendar events while section 3 includes breakdown under Critical Red. Define these semantic tokens and their context consistently before UI acceptance. Typography, spacing, radii, focus states, and disabled states also need token definitions. Check contrast for actual text/background pairs and supplement every status color with a readable label or icon.

Build shared buttons, form fields and validation messages, cards, status badges, checklist controls, measurement controls, attachment previews, and loading/empty/error states. Web additionally needs tables, filters, side panels, and calendar events; mobile needs job cards, bottom navigation, evidence capture, and connection/draft status. Keep the theme subtle on operational screens, as required by Version 3 section 4. Decorative artwork is a later visual asset task, not a dependency for backend implementation.

Technician navigation follows Home / My Jobs / Machines / Notifications / More. Supervisor navigation follows Home / Jobs / Approvals / Machines / More, with notifications reachable from the header or More. Enforce the same record permissions in the backend regardless of route visibility. Define multi-role navigation explicitly instead of assuming a single `role_id`.

### Mobile screen coverage

All screens below are currently unimplemented. Milestones identify the first relevant implementation and later module dependencies.

| V3 screen | Screen | Milestone(s) and dependency |
| --- | --- | --- |
| M01 | Splash Screen | 1; session, role, active-user, and connection routing |
| M02 | Login | 1; login and password-reset flows |
| M03 | Technician Dashboard | 4; notification counts in 6 and final KPI contracts in 9 |
| M04 | Supervisor Dashboard | 5; escalation, breakdown, calibration data in 6-8; final KPI contracts in 9 |
| M05 | My Jobs | 4; sorting and filters over generated/assigned jobs |
| M06 | Work Order Details | 4; status-aware actions and submission view in 5 |
| M07 | Maintenance Checklist | 4; versioned checklist definitions from 3 |
| M08 | Meter / Measurement Entry | 4; types, units, limits, and out-of-range warnings |
| M09 | Maintenance Photos | 4; authorized upload, preview, categories, and draft removal |
| M10 | Review and Submit | 4-5; draft saving, required evidence validation, versioned submission |
| M11 | Rejected Job | 5; original submission and rejection retained |
| M12 | Supervisor Pending Approvals | 5; scoped queue and priority/delay sorting |
| M13 | Supervisor Approval Review | 5; exact submitted version and mandatory rejection reason |
| M14 | My Machines | 2; next PM and calibration data in 3 and 8 |
| M15 | Machine Detail | 2; maintenance, breakdown, calibration, and document tabs as modules arrive |
| M16 | Breakdown Reporting | 7; critical notifications use 6 |
| M17 | Corrective Maintenance | 7; repair evidence and supervisor closure |
| M18 | External Service Report | 10; basic storage approved for MVP, outside current foundation phase |
| M19 | Notifications | 6; read state and authorized related-record navigation |

### Web screen coverage

All screens below are currently unimplemented. Build usable views with their underlying feature and complete the cross-module portal in Milestone 9.

| V3 screen | Screen | Milestone(s) and dependency |
| --- | --- | --- |
| W01 | Manager Dashboard | 9; scoped backend counts/charts from 2-8 |
| W02 | Machine Master | 2; later next PM/calibration columns from 3 and 8 |
| W03 | Add / Edit Machine | 2; user/master-data lookup and authorized files from 1 |
| W04 | Maintenance Plans | 3; list, duplicate, edit, and disable |
| W05 | Maintenance Plan Editor | 3; recurrence, assignment, template version, evidence rules |
| W06 | Work Orders | 3; filters and completed operational views through 9 |
| W07 | Maintenance Calendar | 3; complete day/week/month views and side panel in 9 |
| W08 | Work Order Detail | 3 and 5; history and manager actions; escalations in 6 |
| W09 | Breakdown Management | 7; assignment, downtime, and reviewed closure |
| W10 | Calibration Dashboard | 8; agreed expiry windows and current-certificate counts |
| W11 | Add Calibration Certificate | 8; append new records and preserve previous files |
| W12 | External Services | 10; basic storage approved for MVP, outside current foundation phase |
| W13 | Employees | 1-2 for user setup/assignments; completed administration and counts in 9 |
| W14 | Reports | 9; seven categories and both export formats |
| W15 | Audit Logs | 9 for review; append records with every feature from 1 onward |
| W16 | Settings | 2 for required master data, 6 for policies, and completed administration in 9 |

The numbered catalog does not fully specify web sign-in/reset, the Documents sidebar destination, supervisor assignment/team screens, More/profile, or manager escalation detail. These remain required supporting views/flows from the navigation, quick actions, and FRD; include their contracts within the corresponding milestones. Version 3's omitted standalone web Notifications entry does not cancel the FRD requirement for manager notification visibility.

### Alignment with the supplied sprint groups

The milestones split the work into independently reviewable features. They do not imply sprint duration or a completion-date estimate.

| V3 section 92 group | Subject | Plan milestone(s) |
| --- | --- | --- |
| Group 1 | Foundation | 1 |
| Group 2 | Machine management | 2 |
| Group 3 | Preventive maintenance | 3-4 |
| Group 4 | Approval | 5 |
| Group 5 | Notifications and escalations | 6 |
| Group 6 | Breakdown management | 7 |
| Group 7 | Calibration | 8 |
| Group 8 | Reporting and dashboard KPIs | 9 |
| Not allocated in V3 | External services/document requirements | 10; basic service storage approved for MVP |

## 6. Initial acceptance criteria coverage

None of the 22 end-to-end acceptance criteria in FRD section 81 is complete. The domain/database foundation supports future implementation but provides no operational workflows. The mapping below identifies the planned milestone for each criterion.

| FRD criterion | Required behavior | Planned milestone(s) |
| --- | --- | --- |
| 1 | Technician mobile login | 1 |
| 2 | Supervisor mobile login | 1 |
| 3 | Manager web login | 1 |
| 4 | Create and maintain machines on web | 2 |
| 5 | Assign machine owners | 2 |
| 6 | Create preventive maintenance plans | 3 |
| 7 | Automatically generate work orders | 3 |
| 8 | Notify technicians of work | 6 |
| 9 | Complete maintenance on mobile | 4 |
| 10 | Capture and upload photos | 4 |
| 11 | Submit completed maintenance | 4 |
| 12 | Supervisor approval and rejection | 5 |
| 13 | Correct and resubmit rejected work | 5 |
| 14 | Automatically escalate overdue work | 6 |
| 15 | Manager visibility of overdue/escalated work | 6, 9 |
| 16 | Retrieve machine maintenance history | 5, extended in 7-10 |
| 17 | Store calibration certificates | 8; explicitly included in V3 MVP |
| 18 | Generate calibration expiry reminders | 8; explicitly included in V3 MVP |
| 19 | Store external service reports | 10; basic storage approved for MVP |
| 20 | Create breakdown records | 7 |
| 21 | Download reports | 9, with service data when 10 is included |
| 22 | Record major actions in the audit trail | 1 onward, with each mutation workflow; review interface in 9 |

## 7. Release verification

1. Map each agreed release requirement and all 35 named screens to implementation and test evidence. Include calibration criteria 17-18 and basic external-service criterion 19 in MVP acceptance.
2. Run backend tests with `dotnet test backend/MaintainPro.slnx`. Foundation model tests do not substitute for future database integration and end-to-end acceptance tests.
3. Exercise role and record-level access over the API, including file downloads, reports, reassignment, and multi-role users. Hiding a client control is not authorization.
4. Run an end-to-end scenario: manager creates machine/plan, scheduler generates one job, technician receives it and submits evidence, supervisor rejects, technician corrects, supervisor approves, manager retrieves history and an export.
5. Advance a controlled test clock to verify overdue escalation and calibration expiry reminders. Retry scheduler and notification processing to confirm duplicates are prevented.
6. Test interrupted mobile saves/uploads and supported Android/iOS camera behavior. Keep full offline operation outside the initial release unless operational needs require it.
7. Before production release, validate the configured HTTPS, database/file access, backup monitoring, and restoration procedure in an authorized environment. These are operational acceptance requirements, not features proven by unit tests.
8. Verify historical reports after employee edits, ownership changes, checklist updates, resubmission, and calibration renewal. The original performed/submitted/reviewed facts must remain retrievable.
9. Check navigation for each role, keyboard/focus behavior on web, touch interaction on mobile, actual color contrast, status labels, and the full set of loading/empty/error/draft states. Verify dashboard formulas are consistent because both clients receive them from the backend.

## 8. Current implementation boundary

The foundation phase is complete. The subsequently authorized phase cleans generated-file tracking and creates/reviews `InitialCreate`, its designer and snapshot, and review-only SQL. Stop after build, tests, pending-model-change verification, and review. Do not apply the migration or execute SQL without explicit further approval.

Multiple-role membership is approved and implemented in this foundation. Authentication/refresh, permission rules, work definitions/submissions, and other workflows remain future phases. Criticality values are Low, Medium, High, and Critical, with Medium as the new-machine default. Dates use `DateOnly`; timestamps use UTC `DateTime` mapped to PostgreSQL `timestamp with time zone`. `UpdatedAt` is initialized in the entity; future update operations must refresh it. Text uniqueness currently uses database-default case sensitivity.

The original V2/V3 documents remain unchanged. The foundation's existing EF Core Relational 10.0.12 runtime reference aligns with its design/tools packages; Npgsql remains 10.0.3. No package changes were needed for migration generation. Tooling uses normal Development configuration without copying or printing the connection string. No database schema creation, live connection test, migration application, or deployment is part of this phase. All foundation foreign keys use Restrict; this protects referenced rows but does not itself make historical records immutable or implement authorization.
