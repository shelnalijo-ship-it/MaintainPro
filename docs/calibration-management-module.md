# Calibration management backend

Calibration certificates are immutable historical records. A renewal creates another certificate and links the renewal to it; it never edits or deletes the previous certificate. Certificate numbers are unique within a normalized provider name, so two providers may use the same number. The application stores provider names and certificate numbers trimmed and uppercase.

The current certificate is derived at read time. It is the record with the latest calibration date on or before the company date whose result is `PASS` or `CONDITIONAL`; creation time and ID provide deterministic tie breakers. A newer failed record and a future dated record remain in history but do not replace the applicable certificate. This also handles overlapping and late entered certificates without a mutable current pointer.

Validity and renewal are separate. Machines that do not require calibration return `NOT_REQUIRED`. A required machine with no applicable certificate is `EXPIRED`. An applicable certificate is `EXPIRED` on and after its expiry date, `EXPIRING_SOON` from 60 through 1 days remaining, and `VALID` above 60 days. Renewal is `IN_PROGRESS` only while an active renewal row exists; otherwise reads derive `NOT_STARTED`. An expired machine stays visibly expired during renewal.

The daily processor creates durable events at 60, 30, 7, and 0 days. If a run is missed, it catches up all crossed thresholds. Keys combine machine, certificate or machine source, event type, and recipient, with unique database indexes and serializable retry handling. Recipients are the active assigned supervisor and every active manager. A user with both roles receives one notification. Missing or inactive supervisors fall back to managers. Notifications consistently link to the machine through `EntityType=Machine` and `EntityId`.

The summary uses non-overlapping primary states: `Valid` counts only certificates above 60 days and `Expired` counts expired or missing certificates. The 60, 30, and 7 day fields are overlapping windows for certificates with positive days remaining. Renewal count is independent.

Certificate files use the existing private file storage and authorized download route. PDF, JPG/JPEG, and PNG validation, maximum size, generated storage keys, and content signature checks come from `LocalFileStorageService`; API responses never expose a filesystem path.

## HTTP routes

- `POST /api/v1/calibrations`
- `GET /api/v1/calibrations`
- `GET /api/v1/calibrations/summary`
- `GET /api/v1/calibrations/{id}`
- `POST /api/v1/calibrations/process-reminders`
- `GET /api/v1/machines/{id}/calibrations`
- `GET /api/v1/machines/{id}/calibration-status`
- `POST /api/v1/machines/{id}/calibration-renewals/start`
- `POST /api/v1/machines/{id}/calibration-renewals/complete`
- `POST /api/v1/machines/{id}/calibration-renewals/cancel`
- `GET /api/v1/machines/{id}/calibration-renewals`

Manager and admin roles administer certificates, renewals, and manual reminder processing. All roles may use reads, with technicians restricted to owned machines and supervisors restricted to supervised machines. Manager and admin reads cover all machines.

`CalibrationProcessing:Enabled` controls hosted processing. It defaults to enabled in Development, disabled in Testing, and requires explicit enablement in other environments. `CalibrationProcessing:IntervalHours` defaults to 24 and accepts 1 through 168. The hosted process and manual endpoint call the same application service.
