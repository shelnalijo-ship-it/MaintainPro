# WorkOrderExecutionAndApproval SQL review

Migration: `20260915045159_WorkOrderExecutionAndApproval`.
Reviewed forward SQL: [WorkOrderExecutionAndApproval.sql](WorkOrderExecutionAndApproval.sql).

The forward migration creates 13 tables, adds `HistoryVersion` and `SubmissionVersion` to `WorkOrders` with zero defaults, adds 35 indexes (eight unique), and adds eight check constraints. All 33 new foreign keys specify `ON DELETE RESTRICT`. No existing table, column, index, row, or prior migration is removed or rewritten. The existing supervisor index is retained alongside the new pending-review index.

Unique indexes enforce private storage-key uniqueness, one execution per work order, one current answer per work-order/checklist-item pair, sequential submission versions, one decision per submission, unique history sequence numbers, and unique submitted item/file references. Check constraints enforce positive file sizes and part quantities, nonnegative counters/duration, positive submission/history versions, submission timestamp ordering, and rejection remarks.

The generated `Down` method reverses this new schema and would remove execution data. It has not been run and is not part of the authorized forward deployment.

Before implementation, read-only inspection confirmed `maintainpro_db` contained 18 application tables, three applied migrations, 32 Restrict foreign keys, and 15 unique secondary indexes. `Roles` contained four rows; the other 17 application tables were empty. There was no unexpected operational data.

Application services use serializable transactions and the existing work-order concurrency token for workflow changes. Append-only EF guards protect submitted facts, decisions, file metadata, and operational history; they also reject changes to approved work orders and frozen execution drafts. These guards apply to normal application persistence, not arbitrary administrator SQL or EF bulk SQL commands.

Automated tests use isolated SQLite databases. Their concurrency tests exercise stale-context optimistic conflicts and unique constraints; they do not establish PostgreSQL parallel transaction behavior. Live PostgreSQL verification checks actual schema metadata and unchanged row counts without creating business test rows.

The final application result and verification evidence are recorded in [the module report](../work-order-execution-module.md).
