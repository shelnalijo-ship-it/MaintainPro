START TRANSACTION;
ALTER TABLE "WorkOrders" ADD "AssignmentVersion" integer NOT NULL DEFAULT 0;

ALTER TABLE "WorkOrders" ADD "CurrentTechnicianEmployeeId" text;

ALTER TABLE "WorkOrders" ADD "CurrentTechnicianName" text;

CREATE TABLE "EscalationSettings" (
    "Id" integer NOT NULL,
    "DueSoonDays" integer NOT NULL,
    "TechnicianOverdueDays" integer NOT NULL,
    "SupervisorEscalationDays" integer NOT NULL,
    "ManagerEscalationDays" integer NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "UpdatedByUserId" uuid,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_EscalationSettings" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_EscalationSettings_SingletonAndThresholds" CHECK ("Id" = 1 AND "DueSoonDays" BETWEEN 0 AND 365 AND "TechnicianOverdueDays" >= 0 AND "SupervisorEscalationDays" >= "TechnicianOverdueDays" AND "ManagerEscalationDays" >= "SupervisorEscalationDays" AND "ManagerEscalationDays" <= 36500),
    CONSTRAINT "FK_EscalationSettings_Users_UpdatedByUserId" FOREIGN KEY ("UpdatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "NotificationEvents" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
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
    "EscalationLevel" integer NOT NULL,
    CONSTRAINT "PK_NotificationEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_NotificationEvents_Level" CHECK ("EscalationLevel" BETWEEN 0 AND 3),
    CONSTRAINT "FK_NotificationEvents_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Notifications" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "NotificationType" text NOT NULL,
    "Title" text NOT NULL,
    "Message" text NOT NULL,
    "EntityType" text,
    "EntityId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ReadAt" timestamp with time zone,
    "IsRead" boolean NOT NULL,
    "Priority" text NOT NULL,
    "DeduplicationKey" character varying(500) NOT NULL,
    "ExpiresAt" timestamp with time zone,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_Notifications" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Notifications_ReadState" CHECK (("IsRead" AND "ReadAt" IS NOT NULL) OR (NOT "IsRead" AND "ReadAt" IS NULL)),
    CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "NotificationDeliveryAttempts" (
    "Id" uuid NOT NULL,
    "NotificationId" uuid NOT NULL,
    "Channel" text NOT NULL,
    "AttemptNumber" integer NOT NULL,
    "Status" text NOT NULL,
    "AttemptedAt" timestamp with time zone NOT NULL,
    "ErrorMessage" text,
    "ExternalMessageId" text,
    CONSTRAINT "PK_NotificationDeliveryAttempts" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_NotificationDeliveryAttempts_AttemptNumber" CHECK ("AttemptNumber" > 0),
    CONSTRAINT "FK_NotificationDeliveryAttempts_Notifications_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "Notifications" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "WorkOrderEscalations" (
    "Id" uuid NOT NULL,
    "WorkOrderId" uuid NOT NULL,
    "Level" integer NOT NULL,
    "TriggerType" text NOT NULL,
    "TriggeredAt" timestamp with time zone NOT NULL,
    "RecipientUserId" uuid NOT NULL,
    "NotificationId" uuid NOT NULL,
    "ResolvedAt" timestamp with time zone,
    "DeduplicationKey" character varying(500) NOT NULL,
    "RoutingReason" text,
    "Version" uuid NOT NULL,
    CONSTRAINT "PK_WorkOrderEscalations" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_WorkOrderEscalations_Level" CHECK ("Level" BETWEEN 1 AND 3),
    CONSTRAINT "FK_WorkOrderEscalations_Notifications_NotificationId" FOREIGN KEY ("NotificationId") REFERENCES "Notifications" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderEscalations_Users_RecipientUserId" FOREIGN KEY ("RecipientUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_WorkOrderEscalations_WorkOrders_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES "WorkOrders" ("Id") ON DELETE RESTRICT
);

ALTER TABLE "WorkOrders" ADD CONSTRAINT "CK_WorkOrders_AssignmentVersion" CHECK ("AssignmentVersion" >= 0);

CREATE INDEX "IX_EscalationSettings_UpdatedByUserId" ON "EscalationSettings" ("UpdatedByUserId");

CREATE UNIQUE INDEX "IX_NotificationDeliveryAttempts_NotificationId_Channel_Attempt~" ON "NotificationDeliveryAttempts" ("NotificationId", "Channel", "AttemptNumber");

CREATE UNIQUE INDEX "IX_NotificationEvents_DeduplicationKey" ON "NotificationEvents" ("DeduplicationKey");

CREATE INDEX "IX_NotificationEvents_ProcessedAt_WorkOrderId" ON "NotificationEvents" ("ProcessedAt", "WorkOrderId");

CREATE INDEX "IX_NotificationEvents_WorkOrderId" ON "NotificationEvents" ("WorkOrderId");

CREATE UNIQUE INDEX "IX_Notifications_DeduplicationKey" ON "Notifications" ("DeduplicationKey");

CREATE INDEX "IX_Notifications_UserId_IsRead_CreatedAt" ON "Notifications" ("UserId", "IsRead", "CreatedAt");

CREATE UNIQUE INDEX "IX_WorkOrderEscalations_DeduplicationKey" ON "WorkOrderEscalations" ("DeduplicationKey");

CREATE UNIQUE INDEX "IX_WorkOrderEscalations_NotificationId" ON "WorkOrderEscalations" ("NotificationId");

CREATE INDEX "IX_WorkOrderEscalations_RecipientUserId" ON "WorkOrderEscalations" ("RecipientUserId");

CREATE INDEX "IX_WorkOrderEscalations_WorkOrderId_ResolvedAt_Level" ON "WorkOrderEscalations" ("WorkOrderId", "ResolvedAt", "Level");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260915053927_NotificationsAndEscalations', '10.0.12');

COMMIT;
