START TRANSACTION;
CREATE INDEX "IX_WorkOrders_DueDate" ON "WorkOrders" ("DueDate");

CREATE INDEX "IX_Machines_DepartmentId_LocationId_Status" ON "Machines" ("DepartmentId", "LocationId", "Status");

CREATE INDEX "IX_MachineAssignmentHistories_MachineId_EffectiveFrom" ON "MachineAssignmentHistories" ("MachineId", "EffectiveFrom");

CREATE INDEX "IX_ExternalServices_ServiceDate" ON "ExternalServices" ("ServiceDate");

CREATE INDEX "IX_Breakdowns_ReportedAt" ON "Breakdowns" ("ReportedAt");

CREATE INDEX "IX_AuditLogs_EntityType_EntityId_CreatedAt" ON "AuditLogs" ("EntityType", "EntityId", "CreatedAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260915095918_ReportingQueryIndexes', '10.0.12');

COMMIT;

