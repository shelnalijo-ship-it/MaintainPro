START TRANSACTION;
ALTER TABLE "WorkOrders" ADD "HistoryVersion" integer NOT NULL DEFAULT 0;

ALTER TABLE "WorkOrders" ADD "SubmissionVersion" integer NOT NULL DEFAULT 0;

CREATE TABLE "FileRecords" (
    "Id" uuid NOT NULL,
    "StorageKey" text NOT NULL,
    "OriginalFilename" text NOT NULL,
    "MimeType" text NOT NULL,
    "FileSize" bigint NOT NULL,
    "UploadedByUserId" uuid NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FileRecords" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_FileRecords_Size" CHECK ("FileSize" > 0),
    CONSTRAINT "FK_FileRecords_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "SparePartUsages" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "PartName" text NOT NULL,
    "PartNumber" text,
    "Quantity" numeric NOT NULL,
    "Remarks" text,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SparePartUsages" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_SparePartUsages_Quantity" CHECK ("Quantity" > 0),
    CONSTRAINT "FK_SparePartUsages_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_SparePartUsages_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderChecklistResults" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "WorkOrderChecklistItemId" uuid NOT NULL,
    "BooleanValue" boolean,
    "NumericValue" numeric,
    "TextValue" text,
    "PassFailValue" text,
    "ConfirmationValue" boolean,
    "Comment" text,
    "CompletedAt" timestamp with time zone,
    "CompletedByUserId" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderChecklistResults" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WorkOrderChecklistResults_Users_CompletedByUserId" FOREIGN KEY ("CompletedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderChecklistResults_WorkOrderChecklistItems_WorkOrder~" FOREIGN KEY ("WorkOrderChecklistItemId") REFERENCES "WorkOrderChecklistItems" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderChecklistResults_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderDefects" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "Severity" text,
    "RequiresFollowUp" boolean NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderDefects" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WorkOrderDefects_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderDefects_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderExecutions" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "TechnicianId" uuid NOT NULL,
    "TechnicianEmployeeId" text NOT NULL,
    "TechnicianName" text NOT NULL,
    "OverallComments" text,
    "Observations" text,
    "AttemptStartedAt" timestamp with time zone,
    "AccumulatedDurationMinutes" numeric NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderExecutions" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrderExecutions_Duration" CHECK ("AccumulatedDurationMinutes" >= 0),
    CONSTRAINT "FK_WorkOrderExecutions_Users_TechnicianId" FOREIGN KEY ("TechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderExecutions_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderSubmissions" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "VersionNumber" integer NOT NULL,
    "SubmittedByUserId" uuid NOT NULL,
    "TechnicianEmployeeId" text NOT NULL,
    "TechnicianName" text NOT NULL,
    "SubmittedAt" timestamp with time zone NOT NULL,
    "OverallComments" text,
    "Observations" text,
    "StartedAt" timestamp with time zone NOT NULL,
    "CompletedAt" timestamp with time zone NOT NULL,
    "DurationMinutes" numeric NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderSubmissions" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrderSubmissions_VersionAndDuration" CHECK ("VersionNumber" > 0 AND "DurationMinutes" >= 0 AND "CompletedAt" >= "StartedAt" AND "SubmittedAt" >= "CompletedAt"),
    CONSTRAINT "FK_WorkOrderSubmissions_Users_SubmittedByUserId" FOREIGN KEY ("SubmittedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissions_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderAttachments" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "FileId" uuid NOT NULL,
    "WorkOrderChecklistItemId" uuid,
    "EvidenceType" text NOT NULL,
    "Description" text,
    "UploadedByUserId" uuid NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_WorkOrderAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WorkOrderAttachments_FileRecords_FileId" FOREIGN KEY ("FileId") REFERENCES "FileRecords" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderAttachments_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderAttachments_WorkOrderChecklistItems_WorkOrderCheck~" FOREIGN KEY ("WorkOrderChecklistItemId") REFERENCES "WorkOrderChecklistItems" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderAttachments_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderApprovals" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "WorkOrderSubmissionId" uuid NOT NULL,
    "SupervisorId" uuid NOT NULL,
    "SupervisorEmployeeId" text NOT NULL,
    "SupervisorName" text NOT NULL,
    "Decision" text NOT NULL,
    "Remarks" text,
    "DecisionAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderApprovals" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrderApprovals_RejectionReason" CHECK ("Decision" <> 'REJECTED' OR ("Remarks" IS NOT NULL AND length(trim("Remarks")) > 0)),
    CONSTRAINT "FK_WorkOrderApprovals_Users_SupervisorId" FOREIGN KEY ("SupervisorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderApprovals_WorkOrderSubmissions_WorkOrderSubmission~" FOREIGN KEY ("WorkOrderSubmissionId") REFERENCES "WorkOrderSubmissions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderApprovals_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderHistoryEvents" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "Action" text NOT NULL,
    "ActorUserId" uuid,
    "ActorName" text,
    "OccurredAt" timestamp with time zone NOT NULL,
    "WorkOrderSubmissionId" uuid,
    "Details" text,
    CONSTRAINT "PK_WorkOrderHistoryEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrderHistoryEvents_Sequence" CHECK ("SequenceNumber" > 0),
    CONSTRAINT "FK_WorkOrderHistoryEvents_Users_ActorUserId" FOREIGN KEY ("ActorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderHistoryEvents_WorkOrderSubmissions_WorkOrderSubmis~" FOREIGN KEY ("WorkOrderSubmissionId") REFERENCES "WorkOrderSubmissions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderHistoryEvents_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderSubmissionAttachments" (
    "Id" uuid NOT NULL,
    "WorkOrderSubmissionId" uuid NOT NULL,
    "FileId" uuid NOT NULL,
    "WorkOrderChecklistItemId" uuid,
    "EvidenceType" text NOT NULL,
    "Description" text,
    "UploadedByUserId" uuid NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderSubmissionAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WorkOrderSubmissionAttachments_FileRecords_FileId" FOREIGN KEY ("FileId") REFERENCES "FileRecords" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissionAttachments_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissionAttachments_WorkOrderChecklistItems_Work~" FOREIGN KEY ("WorkOrderChecklistItemId") REFERENCES "WorkOrderChecklistItems" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissionAttachments_WorkOrderSubmissions_WorkOrd~" FOREIGN KEY ("WorkOrderSubmissionId") REFERENCES "WorkOrderSubmissions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderSubmissionChecklistResults" (
    "Id" uuid NOT NULL,
    "WorkOrderSubmissionId" uuid NOT NULL,
    "WorkOrderChecklistItemId" uuid NOT NULL,
    "BooleanValue" boolean,
    "NumericValue" numeric,
    "TextValue" text,
    "PassFailValue" text,
    "ConfirmationValue" boolean,
    "Comment" text,
    "CompletedAt" timestamp with time zone,
    "CompletedByUserId" uuid NOT NULL,
    CONSTRAINT "PK_WorkOrderSubmissionChecklistResults" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WorkOrderSubmissionChecklistResults_Users_CompletedByUserId" FOREIGN KEY ("CompletedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissionChecklistResults_WorkOrderChecklistItems~" FOREIGN KEY ("WorkOrderChecklistItemId") REFERENCES "WorkOrderChecklistItems" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissionChecklistResults_WorkOrderSubmissions_Wo~" FOREIGN KEY ("WorkOrderSubmissionId") REFERENCES "WorkOrderSubmissions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderSubmissionDefects" (
    "Id" uuid NOT NULL,
    "WorkOrderSubmissionId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "Severity" text,
    "RequiresFollowUp" boolean NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderSubmissionDefects" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_WorkOrderSubmissionDefects_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissionDefects_WorkOrderSubmissions_WorkOrderSu~" FOREIGN KEY ("WorkOrderSubmissionId") REFERENCES "WorkOrderSubmissions" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderSubmissionPartUsages" (
    "Id" uuid NOT NULL,
    "WorkOrderSubmissionId" uuid NOT NULL,
    "PartName" text NOT NULL,
    "PartNumber" text,
    "Quantity" numeric NOT NULL,
    "Remarks" text,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrderSubmissionPartUsages" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrderSubmissionPartUsages_Quantity" CHECK ("Quantity" > 0),
    CONSTRAINT "FK_WorkOrderSubmissionPartUsages_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderSubmissionPartUsages_WorkOrderSubmissions_WorkOrde~" FOREIGN KEY ("WorkOrderSubmissionId") REFERENCES "WorkOrderSubmissions" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_WorkOrders_SupervisorId_LifecycleStatus_SubmittedAt" ON "WorkOrders" ("SupervisorId", "LifecycleStatus", "SubmittedAt");

ALTER TABLE "WorkOrders" ADD CONSTRAINT "CK_WorkOrders_ExecutionCounters" CHECK ("SubmissionVersion" >= 0 AND "HistoryVersion" >= 0);

CREATE UNIQUE INDEX "IX_FileRecords_StorageKey" ON "FileRecords" ("StorageKey");

CREATE INDEX "IX_FileRecords_UploadedByUserId" ON "FileRecords" ("UploadedByUserId");

CREATE INDEX "IX_SparePartUsages_CreatedByUserId" ON "SparePartUsages" ("CreatedByUserId");

CREATE INDEX "IX_SparePartUsages_WorkOrderId" ON "SparePartUsages" ("WorkOrderId");

CREATE INDEX "IX_WorkOrderApprovals_SupervisorId" ON "WorkOrderApprovals" ("SupervisorId");

CREATE INDEX "IX_WorkOrderApprovals_WorkOrderId" ON "WorkOrderApprovals" ("WorkOrderId");

CREATE UNIQUE INDEX "IX_WorkOrderApprovals_WorkOrderSubmissionId" ON "WorkOrderApprovals" ("WorkOrderSubmissionId");

CREATE INDEX "IX_WorkOrderAttachments_FileId" ON "WorkOrderAttachments" ("FileId");

CREATE INDEX "IX_WorkOrderAttachments_UploadedByUserId" ON "WorkOrderAttachments" ("UploadedByUserId");

CREATE INDEX "IX_WorkOrderAttachments_WorkOrderChecklistItemId" ON "WorkOrderAttachments" ("WorkOrderChecklistItemId");

CREATE INDEX "IX_WorkOrderAttachments_WorkOrderId_IsDeleted" ON "WorkOrderAttachments" ("WorkOrderId", "IsDeleted");

CREATE INDEX "IX_WorkOrderChecklistResults_CompletedByUserId" ON "WorkOrderChecklistResults" ("CompletedByUserId");

CREATE INDEX "IX_WorkOrderChecklistResults_WorkOrderChecklistItemId" ON "WorkOrderChecklistResults" ("WorkOrderChecklistItemId");

CREATE UNIQUE INDEX "IX_WorkOrderChecklistResults_WorkOrderId_WorkOrderChecklistIte~" ON "WorkOrderChecklistResults" ("WorkOrderId", "WorkOrderChecklistItemId");

CREATE INDEX "IX_WorkOrderDefects_CreatedByUserId" ON "WorkOrderDefects" ("CreatedByUserId");

CREATE INDEX "IX_WorkOrderDefects_WorkOrderId" ON "WorkOrderDefects" ("WorkOrderId");

CREATE INDEX "IX_WorkOrderExecutions_TechnicianId" ON "WorkOrderExecutions" ("TechnicianId");

CREATE UNIQUE INDEX "IX_WorkOrderExecutions_WorkOrderId" ON "WorkOrderExecutions" ("WorkOrderId");

CREATE INDEX "IX_WorkOrderHistoryEvents_ActorUserId" ON "WorkOrderHistoryEvents" ("ActorUserId");

CREATE UNIQUE INDEX "IX_WorkOrderHistoryEvents_WorkOrderId_SequenceNumber" ON "WorkOrderHistoryEvents" ("WorkOrderId", "SequenceNumber");

CREATE INDEX "IX_WorkOrderHistoryEvents_WorkOrderSubmissionId" ON "WorkOrderHistoryEvents" ("WorkOrderSubmissionId");

CREATE INDEX "IX_WorkOrderSubmissionAttachments_FileId" ON "WorkOrderSubmissionAttachments" ("FileId");

CREATE INDEX "IX_WorkOrderSubmissionAttachments_UploadedByUserId" ON "WorkOrderSubmissionAttachments" ("UploadedByUserId");

CREATE INDEX "IX_WorkOrderSubmissionAttachments_WorkOrderChecklistItemId" ON "WorkOrderSubmissionAttachments" ("WorkOrderChecklistItemId");

CREATE UNIQUE INDEX "IX_WorkOrderSubmissionAttachments_WorkOrderSubmissionId_FileId" ON "WorkOrderSubmissionAttachments" ("WorkOrderSubmissionId", "FileId");

CREATE INDEX "IX_WorkOrderSubmissionChecklistResults_CompletedByUserId" ON "WorkOrderSubmissionChecklistResults" ("CompletedByUserId");

CREATE INDEX "IX_WorkOrderSubmissionChecklistResults_WorkOrderChecklistItemId" ON "WorkOrderSubmissionChecklistResults" ("WorkOrderChecklistItemId");

CREATE UNIQUE INDEX "IX_WorkOrderSubmissionChecklistResults_WorkOrderSubmissionId_W~" ON "WorkOrderSubmissionChecklistResults" ("WorkOrderSubmissionId", "WorkOrderChecklistItemId");

CREATE INDEX "IX_WorkOrderSubmissionDefects_CreatedByUserId" ON "WorkOrderSubmissionDefects" ("CreatedByUserId");

CREATE INDEX "IX_WorkOrderSubmissionDefects_WorkOrderSubmissionId" ON "WorkOrderSubmissionDefects" ("WorkOrderSubmissionId");

CREATE INDEX "IX_WorkOrderSubmissionPartUsages_CreatedByUserId" ON "WorkOrderSubmissionPartUsages" ("CreatedByUserId");

CREATE INDEX "IX_WorkOrderSubmissionPartUsages_WorkOrderSubmissionId" ON "WorkOrderSubmissionPartUsages" ("WorkOrderSubmissionId");

CREATE INDEX "IX_WorkOrderSubmissions_SubmittedByUserId" ON "WorkOrderSubmissions" ("SubmittedByUserId");

CREATE UNIQUE INDEX "IX_WorkOrderSubmissions_WorkOrderId_VersionNumber" ON "WorkOrderSubmissions" ("WorkOrderId", "VersionNumber");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260915045159_WorkOrderExecutionAndApproval', '10.0.12');

COMMIT;
