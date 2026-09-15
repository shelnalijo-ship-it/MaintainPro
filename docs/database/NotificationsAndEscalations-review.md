# NotificationsAndEscalations migration review

Migration: `20260915053927_NotificationsAndEscalations`.

The forward SQL was generated from the existing `20260915045159_WorkOrderExecutionAndApproval` migration. It is additive and runs inside a transaction.

## Schema changes

- Five new tables: `Notifications`, `NotificationDeliveryAttempts`, `NotificationEvents`, `WorkOrderEscalations`, and `EscalationSettings`.
- Three columns added to `WorkOrders`: `AssignmentVersion` (integer, non-null, default 0), `CurrentTechnicianEmployeeId` (nullable text), and `CurrentTechnicianName` (nullable text). Existing generated definition snapshots remain unchanged.
- Seven new foreign keys, all `ON DELETE RESTRICT`.
- Eleven new secondary indexes, including five unique indexes.
- Six checks protect read-state consistency, positive attempt numbers, escalation levels, policy threshold ordering/singleton identity, and non-negative assignment versions.
- One EF migration-history row is inserted.

The forward script contains no drop, truncate, business-data delete/update, table recreation, cascade delete, role reseeding, or database creation. Existing rows receive only the safe assignment-version default and nullable assignment snapshot columns. Previous migrations are unchanged.

## Unique indexes

| Table | Unique columns | Purpose |
| --- | --- | --- |
| Notifications | DeduplicationKey | One semantic recipient notice |
| NotificationDeliveryAttempts | NotificationId, Channel, AttemptNumber | One recorded channel attempt number |
| NotificationEvents | DeduplicationKey | One durable source event |
| WorkOrderEscalations | DeduplicationKey | One escalation per event and recipient |
| WorkOrderEscalations | NotificationId | One escalation for its exact notification |

The other indexes cover owned inbox/read-state/date queries, pending event replay, escalation work-order/resolution/level queries, and foreign-key lookups. The long delivery-attempt index name uses EF/Npgsql's normal PostgreSQL identifier shortening.

## Integrity and application behavior

User, work-order, and notification references use restricted deletion. Application save guards also reject deletion of all five new entity types, modification of delivery attempts, rewriting notification/event content, changing the first read timestamp, and rewriting resolved escalation facts. These guards protect ordinary EF writes; privileged direct SQL remains an administrative responsibility.

Settings defaults are effective application defaults until the first authorized settings update. The migration does not invent an administrator or populate a settings edit. Provider placeholders record `SKIPPED`; no external credentials or calls are required.

The generated `Down` method would remove the new schema and is destructive after data is present. It is not part of the reviewed forward operation and must not be applied as a routine rollback.

## Verification protocol

Apply the forward migration only after the complete solution build and test suite pass. Confirm the configured target is `maintainpro_db` through the existing development inspector without printing the connection string. Compare the actual application-table row counts before and after migration, along with role definitions, prior migrations, unique indexes, and validated restricted foreign keys. Finish with `has-pending-model-changes`.

Final execution results are recorded in the module guide.
