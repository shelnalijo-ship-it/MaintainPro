# External services and machine documents backend

External service types are stored as an enum because the eight categories are stable workflow values and do not need supplier-master CRUD. Service providers and external technicians remain captured text, preserving exactly who performed the historical service without introducing supplier management.

Each service receives a persistent annual number in `ES-YYYY-NNNN` format. The year is the UTC creation year. Allocation uses the same serializable transaction as service creation, an annual counter row, an optimistic concurrency token, bounded retry, and a unique service-number index. It never uses `MAX + 1`.

External services retain the machine ID plus machine code and name snapshots. There is no physical delete route. Corrections preserve service number, machine reference and snapshot, creator, and creation time; manager/admin or the currently assigned supervisor may correct the remaining service facts, and the complete old/new values are audited. A dedicated void lifecycle was not introduced because the specification does not define void states or an approval process.

Managers and admins may create and read services for every machine. Supervisors may create/read services for supervised machines and correct those services. Technicians may create and read services for owned machines, but technician-only entries cannot set cost, purchase-order, or invoice fields and technicians cannot correct an existing service. Role checks use union semantics for multi-role users.

External-service attachments use the existing `FileRecord` and private file storage. PDF, JPG/JPEG, and PNG checks remain centralized in `LocalFileStorageService`. Authorized machine users may attach documents. Removal deactivates the relationship once; it does not delete the attachment row, file metadata, or bytes. Attachment history, including inactive entries, remains queryable and downloadable by users who retain machine access.

Machine documents are separate from calibration certificates and support manuals, datasheets, warranties, drawings, certificates, SOPs, service documents, and other documents. Supervisors for the machine and manager/admin users may upload, update metadata, and change active status. File identity and upload provenance cannot be edited. Technicians may read and download documents for owned machines.

Document expiry is derived using the UTC application date. A document with no expiry is `NO_EXPIRY`; an expiry on or before today is `EXPIRED`; 1 through 60 days is `EXPIRING_SOON`; and more than 60 days is `VALID`. No document-expiry notifications are sent in this module.

The protected `/api/v1/files/{id}` route now authorizes files through visible external services and machine documents in addition to work orders, breakdowns, and calibration certificates. Storage paths are never returned.

## HTTP routes

- `POST /api/v1/external-services`
- `GET /api/v1/external-services`
- `GET /api/v1/external-services/follow-ups`
- `GET /api/v1/external-services/{id}`
- `PUT /api/v1/external-services/{id}`
- `GET /api/v1/external-services/{id}/attachments`
- `POST /api/v1/external-services/{id}/attachments`
- `DELETE /api/v1/external-services/{id}/attachments/{attachmentId}`
- `GET /api/v1/machines/{id}/external-services`
- `GET /api/v1/machines/{id}/documents`
- `POST /api/v1/machines/{id}/documents`
- `GET /api/v1/machines/{id}/documents/{documentId}`
- `PUT /api/v1/machines/{id}/documents/{documentId}`
- `PATCH /api/v1/machines/{id}/documents/{documentId}/status`

The follow-up query is limited to supervisors, managers, and admins. It considers the earlier of follow-up and next-service dates for its response, supports upcoming and overdue filters, and includes current machine owner/supervisor IDs for future workflow integration.
