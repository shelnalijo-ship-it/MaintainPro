# Reporting, dashboards, and exports

## Architecture

Reporting is a read-oriented application module. `DashboardService`, `ReportQueryService`,
`SummaryReportingService`, and `MachineHistoryReportService` project authorized data into DTOs;
API endpoints do not expose EF entities. `ReportExportCoordinator` converts the same report
results into a neutral table model and delegates rendering to `IReportExportService`.

`ReportExportService` lives in Infrastructure. It generates server-side PDF files with PDFsharp
6.2.4 and XLSX files with ClosedXML 0.105.1. Both dependencies use the MIT license. The optional
`Reporting:ApplicationName` setting supplies the heading; its safe default is `MaintainPro`.

Exports are generated in memory and returned directly. They do not create public files or accept
filesystem names from callers. A filtered export is limited to 10,000 rows.

## UTC date definitions

All reporting dates use UTC calendar dates. Date ranges are inclusive at both ends. Timestamp
filters translate an inclusive `to` date into an exclusive UTC midnight boundary on the next day.
The default reporting period is the first day of the current UTC month through today.

- `MaintenanceDueToday`: an order due today that still requires technician execution.
- `MaintenanceDueThisWeek`: execution still required and due from today through Sunday of the
  current ISO week. It includes due-today orders.
- `MaintenanceOverdue`: due before today, no submission recorded, and not approved, cancelled, or
  awaiting approval. This matches the existing work-order timing rule.
- `MaintenanceAwaitingApproval`: current lifecycle is `AWAITING_APPROVAL`.
- `MaintenanceEscalated`: escalation level is above zero and the order is not approved/cancelled.
- `OpenBreakdowns`: status is neither `CLOSED` nor `CANCELLED`.
- `CriticalBreakdowns`: open breakdowns whose severity is `CRITICAL`.
- Calibration expiry is inclusive: a certificate expiring today is expired. Expiring windows are
  cumulative: 7-day results are also in 30- and 60-day counts. `CalibrationValid` means more than
  60 days remain, matching the existing calibration status service.
- `ExternalServiceFollowUpsDue`: the earlier non-null follow-up or next-service date is today or
  earlier.

Dashboard machine counts use active machines. Department and location filters use the machine's
current organizational placement. Dashboard compliance and workload use the selected period;
due/overdue alert cards are evaluated as of today.

## Preventive-maintenance compliance

The denominator is every scheduled work order with a non-null maintenance-plan ID whose due date
is in the selected period, excluding cancelled orders. The numerator is denominator orders whose
current lifecycle is `APPROVED`. Approval after the due date still counts in the numerator because
the maintenance was completed; the report's dates and overdue fields retain its lateness. Pending
is denominator minus approved. Overdue is the still-pending subset meeting the current overdue
rule. A zero denominator returns `percentage: null` instead of inventing a zero or perfect rate.

## Technician workload

Workload is operational data and is not an HR score.

- Assigned: non-cancelled orders due during the selected period.
- Started/Approved: corresponding timestamps fall in the period.
- Rejected: rejected submission decisions recorded in the period.
- Awaiting approval and overdue: current state for orders due in the period.
- Average execution minutes: submitted execution attempts completed in the period.
- Breakdown assignments: breakdowns reported in the period and currently assigned.
- Corrective closures: assigned breakdowns closed in the period.

## Authorization and financial fields

- Manager dashboard: `MANAGER` or `ADMIN`; all records, subject to requested filters.
- Supervisor dashboard and management reports: `SUPERVISOR`, `MANAGER`, or `ADMIN`.
  Supervisors are scoped to their supervised machines/work orders/breakdowns before counting or
  pagination. A multi-role user receives the union of their permitted technician and supervisor
  records.
- Technician dashboard: `TECHNICIAN`; explicitly scoped to that user's assignments.
- Machine history: all application roles, using the existing machine visibility union.
- External-service cost is present only for `MANAGER` and `ADMIN` reports and exports.

Every successful export adds a `Report.Exported` audit entry with report name, format, row count,
safe server filename, filters, UTC generation time, and actor. Binary content is never audited.

## Endpoints

Dashboards:

- `GET /api/v1/dashboard/manager`
- `GET /api/v1/dashboard/supervisor`
- `GET /api/v1/dashboard/technician`

Reports:

- `GET /api/v1/reports/preventive-maintenance`
- `GET /api/v1/reports/overdue-maintenance`
- `GET /api/v1/reports/breakdowns`
- `GET /api/v1/reports/calibration`
- `GET /api/v1/reports/technician-workload`
- `GET /api/v1/reports/external-services`
- `GET /api/v1/reports/monthly-summary`
- `GET /api/v1/reports/trends`
- `GET /api/v1/reports/export-history`
- `GET /api/v1/reports/machine-history/{machineId}`

Each tabular report has `/export/pdf` and `/export/excel` variants. Machine history uses
`/api/v1/reports/machine-history/{machineId}/export/pdf` and `/export/excel`.

Machine history combines work-order and breakdown history events, calibration certificates and
renewals, external services, machine documents, assignment history, and audited machine-status
changes. Results are reverse chronological and paginated; callers can filter by module/date.

Trend responses contain one real calendar bucket per requested month, including explicit zero
buckets, for maintenance compliance/completed/overdue, breakdown frequency/downtime, and aggregate
technician workload. Calibration status is the distribution at the end of the selected range.
Trend ranges are limited to 36 calendar months.
