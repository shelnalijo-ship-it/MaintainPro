START TRANSACTION;
ALTER TABLE "Machines" ADD "StatusVersion" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

CREATE TABLE "BreakdownNumberSequences" (
    "Year" integer NOT NULL,
    "LastValue" bigint NOT NULL,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_BreakdownNumberSequences" PRIMARY KEY ("Year"),
    CONSTRAINT "CK_BreakdownNumberSequences_Value" CHECK ("LastValue" >= 0),
    CONSTRAINT "CK_BreakdownNumberSequences_Year" CHECK ("Year" BETWEEN 1 AND 9999)
);

CREATE TABLE "Breakdowns" (
    "Id" uuid NOT NULL,
    "BreakdownNumber" text NOT NULL,
    "MachineId" uuid NOT NULL,
    "MachineCode" text NOT NULL,
    "MachineName" text NOT NULL,
    "ReportedByUserId" uuid NOT NULL,
    "ReporterEmployeeId" text NOT NULL,
    "ReporterName" text NOT NULL,
    "ReportedAt" timestamp with time zone NOT NULL,
    "Severity" text NOT NULL,
    "MachineStopped" boolean NOT NULL,
    "Description" text NOT NULL,
    "InitialObservation" text,
    "AssignedTechnicianId" uuid,
    "TechnicianEmployeeId" text,
    "TechnicianName" text,
    "SupervisorId" uuid NOT NULL,
    "SupervisorEmployeeId" text NOT NULL,
    "SupervisorName" text NOT NULL,
    "Status" text NOT NULL,
    "PreviousMachineStatus" text,
    "RestoreMachineStatus" text,
    "MachineStatusVersionAtStop" uuid,
    "StartedAt" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "SubmittedAt" timestamp with time zone,
    "ClosedAt" timestamp with time zone,
    "ReturnedToServiceAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Version" uuid NOT NULL,
    "AssignmentVersion" integer NOT NULL,
    "SubmissionVersion" integer NOT NULL,
    "HistoryVersion" integer NOT NULL,
    CONSTRAINT "PK_Breakdowns" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Breakdowns_ReturnedToService" CHECK ("ReturnedToServiceAt" IS NULL OR ("MachineStopped" AND "ClosedAt" IS NOT NULL AND "Status" = 'CLOSED' AND "ReturnedToServiceAt" >= "ReportedAt")),
    CONSTRAINT "CK_Breakdowns_Versions" CHECK ("AssignmentVersion" >= 0 AND "SubmissionVersion" >= 0 AND "HistoryVersion" >= 0),
    CONSTRAINT "FK_Breakdowns_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Breakdowns_Users_AssignedTechnicianId" FOREIGN KEY ("AssignedTechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Breakdowns_Users_ReportedByUserId" FOREIGN KEY ("ReportedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Breakdowns_Users_SupervisorId" FOREIGN KEY ("SupervisorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "BreakdownAssignmentHistories" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "TechnicianId" uuid NOT NULL,
    "TechnicianEmployeeId" text NOT NULL,
    "TechnicianName" text NOT NULL,
    "SupervisorId" uuid NOT NULL,
    "SupervisorEmployeeId" text NOT NULL,
    "SupervisorName" text NOT NULL,
    "AssignedByUserId" uuid NOT NULL,
    "AssignedAt" timestamp with time zone NOT NULL,
    "Reason" text,
    CONSTRAINT "PK_BreakdownAssignmentHistories" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_BreakdownAssignmentHistories_Sequence" CHECK ("SequenceNumber" > 0),
    CONSTRAINT "FK_BreakdownAssignmentHistories_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BreakdownAssignmentHistories_Users_AssignedByUserId" FOREIGN KEY ("AssignedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BreakdownAssignmentHistories_Users_SupervisorId" FOREIGN KEY ("SupervisorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BreakdownAssignmentHistories_Users_TechnicianId" FOREIGN KEY ("TechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "BreakdownAttachments" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "FileId" uuid NOT NULL,
    "EvidenceType" text NOT NULL,
    "Description" text,
    "UploadedByUserId" uuid NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_BreakdownAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_BreakdownAttachments_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BreakdownAttachments_FileRecords_FileId" FOREIGN KEY ("FileId") REFERENCES "FileRecords" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BreakdownAttachments_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "BreakdownNotificationEvents" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "NotificationType" text NOT NULL,
    "EventReferenceId" uuid,
    "AssignmentVersion" integer NOT NULL,
    "Priority" text NOT NULL,
    "Title" text NOT NULL,
    "Message" text NOT NULL,
    "DeduplicationKey" character varying(500) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ProcessedAt" timestamp with time zone,
    "LastError" text,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_BreakdownNotificationEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_BreakdownNotificationEvents_AssignmentVersion" CHECK ("AssignmentVersion" >= 0),
    CONSTRAINT "FK_BreakdownNotificationEvents_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CorrectiveActionDrafts" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "TechnicianId" uuid NOT NULL,
    "TechnicianEmployeeId" text NOT NULL,
    "TechnicianName" text NOT NULL,
    "RootCause" text,
    "CorrectiveAction" text,
    "Comments" text,
    "AttemptStartedAt" timestamp with time zone,
    "AccumulatedDurationMinutes" numeric NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CorrectiveActionDrafts" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_CorrectiveActionDrafts_Duration" CHECK ("AccumulatedDurationMinutes" >= 0),
    CONSTRAINT "FK_CorrectiveActionDrafts_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectiveActionDrafts_Users_TechnicianId" FOREIGN KEY ("TechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CorrectivePartUsages" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "PartName" text NOT NULL,
    "PartNumber" text,
    "Quantity" numeric NOT NULL,
    "Remarks" text,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CorrectivePartUsages" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_CorrectivePartUsages_Quantity" CHECK ("Quantity" > 0),
    CONSTRAINT "FK_CorrectivePartUsages_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectivePartUsages_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CorrectiveSubmissions" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "VersionNumber" integer NOT NULL,
    "TechnicianId" uuid NOT NULL,
    "TechnicianEmployeeId" text NOT NULL,
    "TechnicianName" text NOT NULL,
    "RootCause" text NOT NULL,
    "CorrectiveAction" text NOT NULL,
    "Comments" text,
    "StartedAt" timestamp with time zone NOT NULL,
    "CompletedAt" timestamp with time zone NOT NULL,
    "DurationMinutes" numeric NOT NULL,
    "DowntimeMinutes" numeric NOT NULL,
    "SubmittedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CorrectiveSubmissions" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_CorrectiveSubmissions_Durations" CHECK ("DurationMinutes" >= 0 AND "DowntimeMinutes" >= 0),
    CONSTRAINT "CK_CorrectiveSubmissions_Times" CHECK ("CompletedAt" >= "StartedAt" AND "SubmittedAt" >= "CompletedAt"),
    CONSTRAINT "CK_CorrectiveSubmissions_Version" CHECK ("VersionNumber" > 0),
    CONSTRAINT "FK_CorrectiveSubmissions_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectiveSubmissions_Users_TechnicianId" FOREIGN KEY ("TechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "BreakdownHistoryEvents" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "Action" text NOT NULL,
    "ActorUserId" uuid,
    "ActorName" text,
    "OccurredAt" timestamp with time zone NOT NULL,
    "CorrectiveSubmissionId" uuid,
    "SubmissionVersion" integer,
    "Details" text,
    CONSTRAINT "PK_BreakdownHistoryEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_BreakdownHistoryEvents_Sequence" CHECK ("SequenceNumber" > 0),
    CONSTRAINT "FK_BreakdownHistoryEvents_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BreakdownHistoryEvents_CorrectiveSubmissions_CorrectiveSubm~" FOREIGN KEY ("CorrectiveSubmissionId") REFERENCES "CorrectiveSubmissions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BreakdownHistoryEvents_Users_ActorUserId" FOREIGN KEY ("ActorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CorrectiveApprovals" (
    "Id" uuid NOT NULL,
    "BreakdownId" uuid NOT NULL,
    "CorrectiveSubmissionId" uuid NOT NULL,
    "SupervisorId" uuid NOT NULL,
    "SupervisorEmployeeId" text NOT NULL,
    "SupervisorName" text NOT NULL,
    "Decision" text NOT NULL,
    "Remarks" text,
    "DecisionAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CorrectiveApprovals" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_CorrectiveApprovals_RejectionRemarks" CHECK ("Decision" <> 'REJECTED' OR ("Remarks" IS NOT NULL AND length(trim("Remarks")) > 0)),
    CONSTRAINT "FK_CorrectiveApprovals_Breakdowns_BreakdownId" FOREIGN KEY ("BreakdownId") REFERENCES "Breakdowns" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectiveApprovals_CorrectiveSubmissions_CorrectiveSubmiss~" FOREIGN KEY ("CorrectiveSubmissionId") REFERENCES "CorrectiveSubmissions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectiveApprovals_Users_SupervisorId" FOREIGN KEY ("SupervisorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CorrectiveSubmissionAttachments" (
    "Id" uuid NOT NULL,
    "CorrectiveSubmissionId" uuid NOT NULL,
    "FileId" uuid NOT NULL,
    "EvidenceType" text NOT NULL,
    "Description" text,
    "UploadedByUserId" uuid NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CorrectiveSubmissionAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CorrectiveSubmissionAttachments_CorrectiveSubmissions_Corre~" FOREIGN KEY ("CorrectiveSubmissionId") REFERENCES "CorrectiveSubmissions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectiveSubmissionAttachments_FileRecords_FileId" FOREIGN KEY ("FileId") REFERENCES "FileRecords" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectiveSubmissionAttachments_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CorrectiveSubmissionPartUsages" (
    "Id" uuid NOT NULL,
    "CorrectiveSubmissionId" uuid NOT NULL,
    "PartName" text NOT NULL,
    "PartNumber" text,
    "Quantity" numeric NOT NULL,
    "Remarks" text,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CorrectiveSubmissionPartUsages" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_CorrectiveSubmissionPartUsages_Quantity" CHECK ("Quantity" > 0),
    CONSTRAINT "FK_CorrectiveSubmissionPartUsages_CorrectiveSubmissions_Correc~" FOREIGN KEY ("CorrectiveSubmissionId") REFERENCES "CorrectiveSubmissions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CorrectiveSubmissionPartUsages_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_BreakdownAssignmentHistories_AssignedByUserId" ON "BreakdownAssignmentHistories" ("AssignedByUserId");

CREATE UNIQUE INDEX "IX_BreakdownAssignmentHistories_BreakdownId_SequenceNumber" ON "BreakdownAssignmentHistories" ("BreakdownId", "SequenceNumber");

CREATE INDEX "IX_BreakdownAssignmentHistories_SupervisorId" ON "BreakdownAssignmentHistories" ("SupervisorId");

CREATE INDEX "IX_BreakdownAssignmentHistories_TechnicianId" ON "BreakdownAssignmentHistories" ("TechnicianId");

CREATE UNIQUE INDEX "IX_BreakdownAttachments_BreakdownId_FileId" ON "BreakdownAttachments" ("BreakdownId", "FileId");

CREATE INDEX "IX_BreakdownAttachments_FileId" ON "BreakdownAttachments" ("FileId");

CREATE INDEX "IX_BreakdownAttachments_UploadedByUserId" ON "BreakdownAttachments" ("UploadedByUserId");

CREATE INDEX "IX_BreakdownHistoryEvents_ActorUserId" ON "BreakdownHistoryEvents" ("ActorUserId");

CREATE UNIQUE INDEX "IX_BreakdownHistoryEvents_BreakdownId_SequenceNumber" ON "BreakdownHistoryEvents" ("BreakdownId", "SequenceNumber");

CREATE INDEX "IX_BreakdownHistoryEvents_CorrectiveSubmissionId" ON "BreakdownHistoryEvents" ("CorrectiveSubmissionId");

CREATE INDEX "IX_BreakdownNotificationEvents_BreakdownId" ON "BreakdownNotificationEvents" ("BreakdownId");

CREATE UNIQUE INDEX "IX_BreakdownNotificationEvents_DeduplicationKey" ON "BreakdownNotificationEvents" ("DeduplicationKey");

CREATE INDEX "IX_BreakdownNotificationEvents_ProcessedAt_BreakdownId" ON "BreakdownNotificationEvents" ("ProcessedAt", "BreakdownId");

CREATE INDEX "IX_Breakdowns_AssignedTechnicianId_Status" ON "Breakdowns" ("AssignedTechnicianId", "Status");

CREATE UNIQUE INDEX "IX_Breakdowns_BreakdownNumber" ON "Breakdowns" ("BreakdownNumber");

CREATE INDEX "IX_Breakdowns_MachineId_MachineStopped_ReturnedToServiceAt" ON "Breakdowns" ("MachineId", "MachineStopped", "ReturnedToServiceAt");

CREATE INDEX "IX_Breakdowns_ReportedByUserId" ON "Breakdowns" ("ReportedByUserId");

CREATE INDEX "IX_Breakdowns_Status_ReportedAt" ON "Breakdowns" ("Status", "ReportedAt");

CREATE INDEX "IX_Breakdowns_SupervisorId_Status" ON "Breakdowns" ("SupervisorId", "Status");

CREATE UNIQUE INDEX "IX_CorrectiveActionDrafts_BreakdownId" ON "CorrectiveActionDrafts" ("BreakdownId");

CREATE INDEX "IX_CorrectiveActionDrafts_TechnicianId" ON "CorrectiveActionDrafts" ("TechnicianId");

CREATE INDEX "IX_CorrectiveApprovals_BreakdownId" ON "CorrectiveApprovals" ("BreakdownId");

CREATE UNIQUE INDEX "IX_CorrectiveApprovals_CorrectiveSubmissionId" ON "CorrectiveApprovals" ("CorrectiveSubmissionId");

CREATE INDEX "IX_CorrectiveApprovals_SupervisorId" ON "CorrectiveApprovals" ("SupervisorId");

CREATE INDEX "IX_CorrectivePartUsages_BreakdownId" ON "CorrectivePartUsages" ("BreakdownId");

CREATE INDEX "IX_CorrectivePartUsages_CreatedByUserId" ON "CorrectivePartUsages" ("CreatedByUserId");

CREATE UNIQUE INDEX "IX_CorrectiveSubmissionAttachments_CorrectiveSubmissionId_File~" ON "CorrectiveSubmissionAttachments" ("CorrectiveSubmissionId", "FileId");

CREATE INDEX "IX_CorrectiveSubmissionAttachments_FileId" ON "CorrectiveSubmissionAttachments" ("FileId");

CREATE INDEX "IX_CorrectiveSubmissionAttachments_UploadedByUserId" ON "CorrectiveSubmissionAttachments" ("UploadedByUserId");

CREATE INDEX "IX_CorrectiveSubmissionPartUsages_CorrectiveSubmissionId" ON "CorrectiveSubmissionPartUsages" ("CorrectiveSubmissionId");

CREATE INDEX "IX_CorrectiveSubmissionPartUsages_CreatedByUserId" ON "CorrectiveSubmissionPartUsages" ("CreatedByUserId");

CREATE UNIQUE INDEX "IX_CorrectiveSubmissions_BreakdownId_VersionNumber" ON "CorrectiveSubmissions" ("BreakdownId", "VersionNumber");

CREATE INDEX "IX_CorrectiveSubmissions_TechnicianId" ON "CorrectiveSubmissions" ("TechnicianId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260915062834_BreakdownAndCorrectiveMaintenance', '10.0.12');

COMMIT;
