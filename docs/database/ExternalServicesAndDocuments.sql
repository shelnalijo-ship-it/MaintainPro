START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE TABLE "ExternalServiceNumberSequences" (
        "Year" integer NOT NULL,
        "LastValue" bigint NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_ExternalServiceNumberSequences" PRIMARY KEY ("Year"),
        CONSTRAINT "CK_ExternalServiceNumberSequences_Value" CHECK ("LastValue" >= 0),
        CONSTRAINT "CK_ExternalServiceNumberSequences_Year" CHECK ("Year" BETWEEN 1 AND 9999)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE TABLE "ExternalServices" (
        "Id" uuid NOT NULL,
        "ServiceNumber" character varying(32) NOT NULL,
        "MachineId" uuid NOT NULL,
        "MachineCode" character varying(100) NOT NULL,
        "MachineName" character varying(200) NOT NULL,
        "ServiceCompany" character varying(300) NOT NULL,
        "ServiceTechnician" character varying(200),
        "ServiceDate" date NOT NULL,
        "ServiceType" text NOT NULL,
        "Description" character varying(10000) NOT NULL,
        "Findings" character varying(10000),
        "WorkCompleted" character varying(10000),
        "PartsReplaced" character varying(10000),
        "Cost" numeric(18,2),
        "PurchaseOrderNumber" character varying(200),
        "InvoiceNumber" character varying(200),
        "FollowUpDate" date,
        "NextServiceDate" date,
        "Recommendation" character varying(10000),
        "Comments" character varying(10000),
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_ExternalServices" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_ExternalServices_Cost" CHECK ("Cost" IS NULL OR "Cost" >= 0),
        CONSTRAINT "CK_ExternalServices_Dates" CHECK (("FollowUpDate" IS NULL OR "FollowUpDate" >= "ServiceDate") AND ("NextServiceDate" IS NULL OR "NextServiceDate" >= "ServiceDate")),
        CONSTRAINT "FK_ExternalServices_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ExternalServices_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE TABLE "MachineDocuments" (
        "Id" uuid NOT NULL,
        "MachineId" uuid NOT NULL,
        "FileId" uuid NOT NULL,
        "DocumentType" text NOT NULL,
        "Title" character varying(300) NOT NULL,
        "Description" character varying(2000),
        "DocumentDate" date,
        "ExpiryDate" date,
        "UploadedByUserId" uuid NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_MachineDocuments" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_MachineDocuments_Dates" CHECK ("ExpiryDate" IS NULL OR "DocumentDate" IS NULL OR "ExpiryDate" >= "DocumentDate"),
        CONSTRAINT "FK_MachineDocuments_FileRecords_FileId" FOREIGN KEY ("FileId") REFERENCES "FileRecords" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MachineDocuments_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MachineDocuments_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE TABLE "ExternalServiceAttachments" (
        "Id" uuid NOT NULL,
        "ExternalServiceId" uuid NOT NULL,
        "FileId" uuid NOT NULL,
        "DocumentType" text NOT NULL,
        "Description" character varying(2000),
        "UploadedByUserId" uuid NOT NULL,
        "UploadedAt" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_ExternalServiceAttachments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExternalServiceAttachments_ExternalServices_ExternalService~" FOREIGN KEY ("ExternalServiceId") REFERENCES "ExternalServices" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ExternalServiceAttachments_FileRecords_FileId" FOREIGN KEY ("FileId") REFERENCES "FileRecords" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ExternalServiceAttachments_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE UNIQUE INDEX "IX_ExternalServiceAttachments_ExternalServiceId_FileId" ON "ExternalServiceAttachments" ("ExternalServiceId", "FileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServiceAttachments_ExternalServiceId_IsActive" ON "ExternalServiceAttachments" ("ExternalServiceId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServiceAttachments_FileId" ON "ExternalServiceAttachments" ("FileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServiceAttachments_UploadedByUserId" ON "ExternalServiceAttachments" ("UploadedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServices_CreatedByUserId" ON "ExternalServices" ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServices_FollowUpDate" ON "ExternalServices" ("FollowUpDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServices_MachineId_ServiceDate" ON "ExternalServices" ("MachineId", "ServiceDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServices_NextServiceDate" ON "ExternalServices" ("NextServiceDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_ExternalServices_ServiceCompany" ON "ExternalServices" ("ServiceCompany");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE UNIQUE INDEX "IX_ExternalServices_ServiceNumber" ON "ExternalServices" ("ServiceNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_MachineDocuments_ExpiryDate" ON "MachineDocuments" ("ExpiryDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_MachineDocuments_FileId" ON "MachineDocuments" ("FileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_MachineDocuments_MachineId_DocumentType_IsActive" ON "MachineDocuments" ("MachineId", "DocumentType", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE UNIQUE INDEX "IX_MachineDocuments_MachineId_FileId" ON "MachineDocuments" ("MachineId", "FileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_MachineDocuments_UploadedAt" ON "MachineDocuments" ("UploadedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    CREATE INDEX "IX_MachineDocuments_UploadedByUserId" ON "MachineDocuments" ("UploadedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915085123_ExternalServicesAndDocuments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915085123_ExternalServicesAndDocuments', '10.0.12');
    END IF;
END $EF$;
COMMIT;

