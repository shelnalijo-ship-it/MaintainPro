START TRANSACTION;
CREATE TABLE "MaintenanceTypes" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_MaintenanceTypes" PRIMARY KEY ("Id")
);

CREATE TABLE "WorkOrderNumberSequences" (
    "Year" integer NOT NULL,
    "LastValue" bigint NOT NULL,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_WorkOrderNumberSequences" PRIMARY KEY ("Year"),
    CONSTRAINT "CK_WorkOrderNumberSequences_Value" CHECK ("LastValue" >= 0),
    CONSTRAINT "CK_WorkOrderNumberSequences_Year" CHECK ("Year" >= 1 AND "Year" <= 9999)
);

CREATE TABLE "MaintenancePlans" (
    "Id" uuid NOT NULL,
    "MachineId" uuid NOT NULL,
    "PlanName" text NOT NULL,
    "MaintenanceTypeId" uuid NOT NULL,
    "Priority" text NOT NULL,
    "FrequencyType" text NOT NULL,
    "FrequencyValue" integer NOT NULL,
    "StartDate" date NOT NULL,
    "NextDueDate" date NOT NULL,
    "DefaultTechnicianId" uuid,
    "SupervisorId" uuid NOT NULL,
    "Instructions" text,
    "EstimatedDurationMinutes" integer,
    "PhotoRequired" boolean NOT NULL,
    "MinimumPhotoCount" integer NOT NULL,
    "CommentRequired" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_MaintenancePlans" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_MaintenancePlans_Duration" CHECK ("EstimatedDurationMinutes" IS NULL OR "EstimatedDurationMinutes" >= 0),
    CONSTRAINT "CK_MaintenancePlans_Evidence" CHECK (("PhotoRequired" AND "MinimumPhotoCount" >= 1) OR (NOT "PhotoRequired" AND "MinimumPhotoCount" = 0)),
    CONSTRAINT "CK_MaintenancePlans_Frequency" CHECK ("FrequencyValue" > 0),
    CONSTRAINT "FK_MaintenancePlans_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MaintenancePlans_MaintenanceTypes_MaintenanceTypeId" FOREIGN KEY ("MaintenanceTypeId") REFERENCES "MaintenanceTypes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MaintenancePlans_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MaintenancePlans_Users_DefaultTechnicianId" FOREIGN KEY ("DefaultTechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MaintenancePlans_Users_SupervisorId" FOREIGN KEY ("SupervisorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ChecklistTemplates" (
    "Id" uuid NOT NULL,
    "MaintenancePlanId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "Name" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ChecklistTemplates" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ChecklistTemplates_Version" CHECK ("Version" > 0),
    CONSTRAINT "FK_ChecklistTemplates_MaintenancePlans_MaintenancePlanId" FOREIGN KEY ("MaintenancePlanId") REFERENCES "MaintenancePlans" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ChecklistTemplates_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrders" (
    "Id" uuid NOT NULL,
    "WorkOrderNumber" text NOT NULL,
    "MachineId" uuid NOT NULL,
    "MaintenancePlanId" uuid,
    "AssignedTechnicianId" uuid,
    "SupervisorId" uuid NOT NULL,
    "PlannedDate" date NOT NULL,
    "DueDate" date NOT NULL,
    "Priority" text NOT NULL,
    "LifecycleStatus" text NOT NULL,
    "StartedAt" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "SubmittedAt" timestamp with time zone,
    "ApprovedAt" timestamp with time zone,
    "CancelledAt" timestamp with time zone,
    "EscalationLevel" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_WorkOrders" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrders_Dates" CHECK ("DueDate" >= "PlannedDate"),
    CONSTRAINT "CK_WorkOrders_Escalation" CHECK ("EscalationLevel" >= 0),
    CONSTRAINT "FK_WorkOrders_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrders_MaintenancePlans_MaintenancePlanId" FOREIGN KEY ("MaintenancePlanId") REFERENCES "MaintenancePlans" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrders_Users_AssignedTechnicianId" FOREIGN KEY ("AssignedTechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrders_Users_SupervisorId" FOREIGN KEY ("SupervisorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ChecklistItems" (
    "Id" uuid NOT NULL,
    "ChecklistTemplateId" uuid NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "Title" text NOT NULL,
    "Description" text,
    "ResponseType" text NOT NULL,
    "IsMandatory" boolean NOT NULL,
    "Unit" text,
    "MinimumValue" numeric,
    "MaximumValue" numeric,
    "PhotoRequired" boolean NOT NULL,
    CONSTRAINT "PK_ChecklistItems" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_ChecklistItems_Bounds" CHECK ("MinimumValue" IS NULL OR "MaximumValue" IS NULL OR "MinimumValue" <= "MaximumValue"),
    CONSTRAINT "CK_ChecklistItems_Sequence" CHECK ("SequenceNumber" > 0),
    CONSTRAINT "FK_ChecklistItems_ChecklistTemplates_ChecklistTemplateId" FOREIGN KEY ("ChecklistTemplateId") REFERENCES "ChecklistTemplates" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderDefinitions" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "MaintenanceTypeId" uuid NOT NULL,
    "ChecklistTemplateId" uuid NOT NULL,
    "ChecklistVersion" integer NOT NULL,
    "ChecklistName" text NOT NULL,
    "PlanName" text NOT NULL,
    "MaintenanceTypeName" text NOT NULL,
    "Instructions" text,
    "EstimatedDurationMinutes" integer,
    "PhotoRequired" boolean NOT NULL,
    "MinimumPhotoCount" integer NOT NULL,
    "CommentRequired" boolean NOT NULL,
    "Priority" text NOT NULL,
    "MachineCode" text NOT NULL,
    "MachineName" text NOT NULL,
    "AssignedTechnicianEmployeeId" text,
    "AssignedTechnicianName" text,
    "SupervisorEmployeeId" text NOT NULL,
    "SupervisorName" text NOT NULL,
    CONSTRAINT "PK_WorkOrderDefinitions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WorkOrderDefinitions_ChecklistTemplates_ChecklistTemplateId" FOREIGN KEY ("ChecklistTemplateId") REFERENCES "ChecklistTemplates" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderDefinitions_MaintenanceTypes_MaintenanceTypeId" FOREIGN KEY ("MaintenanceTypeId") REFERENCES "MaintenanceTypes" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderDefinitions_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderChecklistItems" (
    "Id" uuid NOT NULL,
    "WorkOrderDefinitionId" uuid NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "Title" text NOT NULL,
    "Description" text,
    "ResponseType" text NOT NULL,
    "IsMandatory" boolean NOT NULL,
    "Unit" text,
    "MinimumValue" numeric,
    "MaximumValue" numeric,
    "PhotoRequired" boolean NOT NULL,
    CONSTRAINT "PK_WorkOrderChecklistItems" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrderChecklistItems_Bounds" CHECK ("MinimumValue" IS NULL OR "MaximumValue" IS NULL OR "MinimumValue" <= "MaximumValue"),
    CONSTRAINT "CK_WorkOrderChecklistItems_Sequence" CHECK ("SequenceNumber" > 0),
    CONSTRAINT "FK_WorkOrderChecklistItems_WorkOrderDefinitions_WorkOrderDefin~" FOREIGN KEY ("WorkOrderDefinitionId") REFERENCES "WorkOrderDefinitions" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_ChecklistItems_ChecklistTemplateId_SequenceNumber" ON "ChecklistItems" ("ChecklistTemplateId", "SequenceNumber");

CREATE INDEX "IX_ChecklistTemplates_CreatedByUserId" ON "ChecklistTemplates" ("CreatedByUserId");

CREATE UNIQUE INDEX "IX_ChecklistTemplates_MaintenancePlanId_Version" ON "ChecklistTemplates" ("MaintenancePlanId", "Version");

CREATE INDEX "IX_MaintenancePlans_CreatedByUserId" ON "MaintenancePlans" ("CreatedByUserId");

CREATE INDEX "IX_MaintenancePlans_DefaultTechnicianId" ON "MaintenancePlans" ("DefaultTechnicianId");

CREATE INDEX "IX_MaintenancePlans_IsActive_NextDueDate" ON "MaintenancePlans" ("IsActive", "NextDueDate");

CREATE INDEX "IX_MaintenancePlans_MachineId" ON "MaintenancePlans" ("MachineId");

CREATE INDEX "IX_MaintenancePlans_MaintenanceTypeId" ON "MaintenancePlans" ("MaintenanceTypeId");

CREATE INDEX "IX_MaintenancePlans_SupervisorId" ON "MaintenancePlans" ("SupervisorId");

CREATE UNIQUE INDEX "IX_MaintenanceTypes_Name" ON "MaintenanceTypes" ("Name");

CREATE UNIQUE INDEX "IX_WorkOrderChecklistItems_WorkOrderDefinitionId_SequenceNumber" ON "WorkOrderChecklistItems" ("WorkOrderDefinitionId", "SequenceNumber");

CREATE INDEX "IX_WorkOrderDefinitions_ChecklistTemplateId" ON "WorkOrderDefinitions" ("ChecklistTemplateId");

CREATE INDEX "IX_WorkOrderDefinitions_MaintenanceTypeId" ON "WorkOrderDefinitions" ("MaintenanceTypeId");

CREATE UNIQUE INDEX "IX_WorkOrderDefinitions_WorkOrderId" ON "WorkOrderDefinitions" ("WorkOrderId");

CREATE INDEX "IX_WorkOrders_AssignedTechnicianId_PlannedDate" ON "WorkOrders" ("AssignedTechnicianId", "PlannedDate");

CREATE INDEX "IX_WorkOrders_LifecycleStatus_DueDate" ON "WorkOrders" ("LifecycleStatus", "DueDate");

CREATE INDEX "IX_WorkOrders_MachineId" ON "WorkOrders" ("MachineId");

CREATE UNIQUE INDEX "IX_WorkOrders_MaintenancePlanId_PlannedDate" ON "WorkOrders" ("MaintenancePlanId", "PlannedDate") WHERE "MaintenancePlanId" IS NOT NULL;

CREATE INDEX "IX_WorkOrders_SupervisorId" ON "WorkOrders" ("SupervisorId");

CREATE UNIQUE INDEX "IX_WorkOrders_WorkOrderNumber" ON "WorkOrders" ("WorkOrderNumber");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260915041200_PreventivePlanningAndWorkOrderGeneration', '10.0.12');

COMMIT;
