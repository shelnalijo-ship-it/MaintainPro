# MaintainPro V3 database, API, and workflow review

Reviewed on 14 September 2026 against the supplied [UI Specification, Workflow, Database Schema and API Requirements V3](ui-workflow-database-api-specification-v3.md) and [Functional Requirements Document V2](functional-requirements-v2.md). Section references below use **V3** or **FRD** to distinguish the documents.

**Subsequent approved decisions:** users have multiple roles through an explicit `UserRole` entity; basic external-service storage is included in MVP; work-order lifecycle and overdue/escalation state will be separate. These decisions supersede the corresponding open proposals in this original review. The [implementation plan](implementation-plan.md) records the current foundation-only phase; WorkOrder and external-service implementation remain excluded from it.

At the time of the original review, the API exposed only the sample `/weatherforecast` endpoint and the other layers contained placeholders. The plan now tracks the foundation implementation. The findings below describe future workflow contracts rather than defects in existing maintenance code.

Except for the three subsequently approved decisions above, each **Proposal** below remains a recommendation rather than an approved product rule or delivered feature. This review does not amend either supplied specification. The field lists in V3 provide a useful starting point but do not yet specify complete relationships, validation, permissions, or state transitions.

## 1. Release boundary and document precedence

**Finding:** V3 §93 explicitly includes web calibration, and §92 includes certificates, reminders, and history. This resolves the earlier calibration ambiguity between FRD §77 and acceptance criteria 17–18 in favor of MVP inclusion.

**Remaining gap:** External services have mobile/web screens and schema/API coverage in V3 §§24,38,65,81 but are absent from the development sequence and MVP list (§§92–93). FRD §81 criterion 19 still requires external service report storage for initial acceptance.

**Proposal:** Include basic external service records with multiple report attachments in the initial acceptance scope, or explicitly defer criterion 19. Record that release decision before declaring MVP acceptance; absence from a list is not an explicit cancellation of the FRD requirement.

## 2. Users, roles, and record permissions

**Finding:** V3 §48 has one `USERS.role_id`. FRD §9 permits Manager and Administrator to be the same user, and §68 lists `UserRoles`. A single role foreign key does not directly represent that membership. V3 §§5,39,49,86 describe roles but do not fully define record visibility or combined-role behavior.

**Proposal:** Use explicit user-role memberships, with unique membership pairs, while retaining separate permissions for each role. Define supervisor team boundaries, machine/job visibility, reassignment authority, and whether a dual-role supervisor can review work they submitted. FRD §6 already prohibits technicians from approving their own work; overlapping roles must not silently bypass that rule.

**Before implementation:** Specify identifier uniqueness, email versus username login, role-change/session behavior, reset-access behavior, and who may create the first administrator. Review nullable assignment and inactive-user rules before migrations.

## 3. Historical facts and ownership

**Finding:** V3 §§54,91,95 require historical ownership and facts to survive later changes. A work order's technician ID helps preserve assignment, but live employee names, plan instructions, machine labels, and mutable evidence could still change what an old report displays. Assignment history alone does not preserve a work submission.

**Proposal:** Separate current reference data from immutable historical values. Define the employee identity/display values, machine identity, assignment, instructions, and evidence rules captured for an issued job and for each submitted version. Preserve stable IDs alongside any required snapshots; record who changed an assignment, when, and why.

**Before implementation:** Agree which values are frozen at work-order creation, execution, submission, and review. Historical technician identity must reflect who performed/submitted the work, even after reassignment. Define correction behavior without overwriting approved work or deleting protected history.

## 4. Reusable checklists and version binding

**Finding:** FRD §25 requires reusable templates. V3 §56 attaches a template directly to one maintenance plan; sharing between plans is unspecified. Although it has `version`, work orders (§58) do not bind to a template version. Results (§59) reference checklist items whose content can be edited through §77.

**Proposal:** Define a reusable template with immutable published versions, and associate each plan with its selected version. Bind generated work orders to an immutable checklist/instruction/evidence definition or equivalent snapshot. Editing a used template should create a new version; old answers must keep their original question, type, limits, units, order, and required-evidence meaning.

**Before implementation:** Define draft versus published template behavior, plan duplication, checklist reordering, comment requirements, and applicability of edits to existing unstarted work. Retain V3 §77's restriction on deleting checklist items with associated work history.

## 5. Drafts, submissions, and review evidence

**Finding:** V3 §§16–19 require drafts, original submissions, rejection history, and resubmission. `WORK_ORDER_APPROVALS.submission_version` (§61) has no corresponding submission entity. Results/photos/parts (§§59–60,67) link only to a work order, so a later edit could alter the evidence behind an earlier decision.

**Proposal:** Model editable execution drafts and immutable numbered submissions. Link each submitted checklist result, reading, photo/document, part usage entry, comment, defect, and duration to the submitted version, plus its submitter and timestamps. Link each approval/rejection to that exact submission and reviewer identity; preserve previous decisions and reasons.

**Before implementation:** Specify draft save/update semantics and typed-answer validation, including how numeric zero and a valid negative response differ from a missing answer. Define photo-to-checklist-item association so item-specific evidence can be validated. Work-order-level `rejection_reason` may summarize the latest decision but cannot be the historical source of truth.

## 6. Files, documents, and secure association

**Finding:** V3 §66 stores file metadata, §60 associates work-order photos, and §64 links a certificate file. However, machine documents (§§21,29,75), external service attachments (§§24,38,81), breakdown/corrective photos (§§22–23), and work-order supporting documents (FRD §§28–29,44,62) lack complete association schemas. `FILES.uploaded_by` does not describe all records authorized to use or download a file.

**Proposal:** Define document metadata and explicit entity/file associations, including multiple external service attachments and links to submission versions where relevant. Validate both permission to the target record and permission to attach the uploaded file. Authorize downloads against the related record; document whether §85's `url` is a protected route or a short-lived authorized link.

**Before implementation:** Specify supported MIME types, size/count limits, interrupted-upload retry behavior, upload finalization, and unattached-upload lifecycle. Define draft photo removal without erasing submitted evidence, historical certificates, or files referenced by another record. Add authenticated retrieval/download and association-removal contracts where required by the screens.

## 7. Lifecycle state versus time-based labels

**Finding:** V3 §44 maps approval to `Closed`; §90 lists `APPROVED` but no work-order `CLOSED`, consistent with FRD §30's `Approved`. V3 also lists `OVERDUE` and `ESCALATED` alongside execution/review states. FRD §27 additionally has `Due` and `Completed`, which V3 omits.

**Proposal:** Confirm one canonical work-order state machine and UI label mapping, provisionally using `APPROVED` as the approval terminal state. Keep due/overdue classification and escalation level independent so an overdue job can still be awaiting approval. Breakdown `Closed` can remain a separately defined lifecycle state.

**Before implementation:** Define allowed actor/action/from/to transitions, rejection correction, cancellation, due-date changes, and reassignment at each state. Decide whether completing execution stops lateness, whether approval has a separate deadline, and how reporting counts submitted but unapproved work.

## 8. Recurrence, reminders, and escalation

**Finding:** V3 §§45–46,55,69,87 define dates, intervals, and events but omit recurrence edge cases and durable delivery contracts. `ESCALATIONS.work_order_id` cannot alone associate the critical breakdown and calibration escalation workflows described in §§22,46.

**Proposal:** Define a company time zone, calendar boundary rules, recurrence anchor, month-end behavior, scheduling horizon, missed-run recovery, and effects of disabled plans or changed due dates. Use a unique plan-occurrence identity to prevent duplicate jobs. Define escalation targets for work orders, breakdowns, and calibration, with durable delivery attempts and deduplication keys.

**Before implementation:** Specify recipient selection, reminder thresholds, resolution behavior, and recalculation after rescheduling or renewal. Configurable calibration reminders should not be limited by the hardcoded 60/30/7 event names. Specify push-device registration, token removal, channel preferences, retries, and notification links that recheck current record access.

## 9. Breakdown and corrective-maintenance lifecycle

**Finding:** V3 §§22–23,35,62–63,79 describe reporting, corrective work, supervisor review, and closure, but do not define corrective submission/rejection/version history or how closure affects machine status. Spare-part usage (§67) only links to work orders, while corrective work also records parts.

**Proposal:** Define the relationship between a breakdown, its corrective action(s), any corrective work order, evidence, and part usage. Apply explicit review and history rules to corrective submissions. Record downtime start/end facts and calculation rules; define whether multiple simultaneous breakdowns prevent returning a machine to service.

**Before implementation:** Specify who can start, submit, reject, resubmit, and close corrective work; identify which routes represent those actions. Confirm the intended MVP depth: §92 includes corrective maintenance and closure, while §93's mobile list names breakdown reporting only.

## 10. Calibration and external service completeness

**Finding:** V3 §§37,64,91 prohibit overwriting historical certificates, but §80 allows a certificate upload to an existing calibration ID without defining replacement rules. Calibration status (§64) has no derivation contract; FRD §§39–42 also require next calibration date and renewal state. V3 §65 lacks an explicit attachment association and does not directly capture all FRD §37 fields such as work completed and textual next-service recommendation.

**Proposal:** Model each renewal as a new historical calibration record, and define whether file attachment completion is permitted only for a draft record. Derive validity from agreed date boundaries while tracking renewal separately; explicitly define the current certificate and what happens when calibration is not required.

**Before implementation:** Map every required calibration/service field to storage and API payloads, including optional versus required values. A `next_service_date` alone cannot preserve a textual recommendation. Specify append/correction behavior for service records and retain certificate history on machine decommissioning.

## 11. Settings and master-data persistence

**Finding:** V3 §42 requires notification/escalation settings and editable departments, locations, machine categories, maintenance types, and priorities. §§50–53 define some master tables, but `maintenance_type_id` (§55) has no described table; priorities and settings have no persistence or API contracts. No settings/master-data routes appear in §§73–85.

**Proposal:** Define those resources and their read/update authorization, validation, defaults, effective timing, and audit behavior. Specify whether priorities are configurable records or fixed values and how their ordering maps to approval sorting. Prefer inactivation for referenced master data so historical records remain readable.

**Before implementation:** Distinguish runtime business settings from deployment/environment configuration. Runtime settings belong to application workflows; this review does not authorize changing environment configuration.

## 12. Screen actions missing explicit API contracts

V3's route inventory does not yet explain all screen actions. The following are **proposed contract additions or clarifications**, not approved endpoint names; existing endpoints may cover some actions once their payloads and permissions are defined.

| Screen or requirement | Missing or incomplete contract |
| --- | --- |
| W05 checklist/plan editor (§31) | Duplicate plan, reuse/select template version, reorder items, and require comments |
| M07–M10 execution (§§13–16) | Save/read/update draft, edit/remove draft parts and photos, comments/defects/duration, and submitted-version retrieval |
| M11/M13, W08 (§§17,19,34) | Original submission with review history; manager due-date change, cancellation, and comments |
| W07 calendar (§33) | Date-range query, event schema, time zone, filters, and shared status mapping; may use a defined work-order query |
| W13 employees (§39) | Role assignment, reset access, team/assignment counts, and active-user lookup for assignment controls |
| W15/W16 (§§41–42) | Read-only audit-log queries, settings, and master-data administration |
| Documents/service/repair screens (§§21–24,29,38) | Document associations and authorized downloads, supporting evidence, and corrective review actions |
| Notifications (§§25,87–88) | Push-device registration lifecycle and safe related-record navigation |
| Reports/dashboards (§§27,40,83–84) | KPI formulas, common filters, date boundaries, historical identity sources, and export response behavior |

## 13. Authentication and shared API conventions

**Finding:** V3 §72 exempts only login and password-reset endpoints from authentication, while §73 includes refresh. Requiring a currently valid access token for refresh prevents renewal after that token expires. Paths and verbs alone also leave request/response shapes, failures, list behavior, and race handling unspecified.

**Proposal:** Explicitly permit refresh without a valid access token when a valid refresh credential is presented; define credential rotation, revocation, reuse behavior, inactive-user checks, and logout. Clarify forgot/reset-password exceptions and credential transport for each client without weakening access checks on business endpoints.

**Before implementation:** Agree DTOs, required/nullable fields, enum serialization, UTC instants versus local business dates, validation errors, authorization failures, not-found behavior, and conflict responses. Specify pagination, stable sorting, filter syntax, date ranges, maximum page size, and ownership-scoped list access.

**Proposal:** Add optimistic concurrency for edits and transitions; a reviewer must approve the exact current submitted version. Define retry/idempotence behavior for job generation, draft writes, uploads/association, submit/approve/reject, numbering, and notifications. Use transactional writes for state, immutable history, audit, and pending notifications so partial failures do not lose facts or duplicate actions.

## 14. Readiness checks before implementation

The next design increment should produce reviewable entity relationships, request/response examples, a role/action/record-access matrix, state transition tables, and explicit answers to the release and timing decisions above. Keep the existing Domain/Application/Infrastructure/API boundaries while adding one working feature at a time.

Planned verification should cover: two roles on one user; assignment and employee changes leaving old history intact; template edits preserving old jobs; reject/correct/resubmit retaining each evidence version; unauthorized file attachment/download; simultaneous approval/reassignment conflict; duplicate scheduler/retry safety; month-end/time-zone boundaries; certificate renewal without overwrite; and settings affecting future reminders as specified.

The original review changed documentation only. Subsequent implementation is tracked in the implementation plan. No migration is authorized for the current foundation phase.
