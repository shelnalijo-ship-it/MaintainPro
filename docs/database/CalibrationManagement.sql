START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE TABLE "CalibrationCertificates" (
        "Id" uuid NOT NULL,
        "MachineId" uuid NOT NULL,
        "CertificateNumber" character varying(200) NOT NULL,
        "CalibrationProvider" character varying(300) NOT NULL,
        "CalibrationDate" date NOT NULL,
        "ExpiryDate" date NOT NULL,
        "Result" text NOT NULL,
        "Remarks" character varying(10000),
        "CertificateFileId" uuid,
        "CreatedByUserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_CalibrationCertificates" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_CalibrationCertificates_Dates" CHECK ("ExpiryDate" > "CalibrationDate"),
        CONSTRAINT "FK_CalibrationCertificates_FileRecords_CertificateFileId" FOREIGN KEY ("CertificateFileId") REFERENCES "FileRecords" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CalibrationCertificates_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CalibrationCertificates_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE TABLE "CalibrationRenewals" (
        "Id" uuid NOT NULL,
        "MachineId" uuid NOT NULL,
        "PreviousCertificateId" uuid,
        "Status" text NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "StartedByUserId" uuid NOT NULL,
        "CompletedAt" timestamp with time zone,
        "CompletedCertificateId" uuid,
        "Notes" character varying(10000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_CalibrationRenewals" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_CalibrationRenewals_Completion" CHECK (("Status" = 'IN_PROGRESS' AND "CompletedAt" IS NULL AND "CompletedCertificateId" IS NULL) OR ("Status" = 'COMPLETED' AND "CompletedAt" IS NOT NULL AND "CompletedCertificateId" IS NOT NULL) OR ("Status" = 'CANCELLED' AND "CompletedAt" IS NOT NULL AND "CompletedCertificateId" IS NULL)),
        CONSTRAINT "FK_CalibrationRenewals_CalibrationCertificates_CompletedCertif~" FOREIGN KEY ("CompletedCertificateId") REFERENCES "CalibrationCertificates" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CalibrationRenewals_CalibrationCertificates_PreviousCertifi~" FOREIGN KEY ("PreviousCertificateId") REFERENCES "CalibrationCertificates" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CalibrationRenewals_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CalibrationRenewals_Users_StartedByUserId" FOREIGN KEY ("StartedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE TABLE "CalibrationNotificationEvents" (
        "Id" uuid NOT NULL,
        "MachineId" uuid NOT NULL,
        "CalibrationCertificateId" uuid,
        "CalibrationRenewalId" uuid,
        "NotificationType" text NOT NULL,
        "Priority" text NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Message" character varying(10000) NOT NULL,
        "DeduplicationKey" character varying(500) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ProcessedAt" timestamp with time zone,
        "LastError" character varying(2000),
        "Version" uuid NOT NULL,
        CONSTRAINT "PK_CalibrationNotificationEvents" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CalibrationNotificationEvents_CalibrationCertificates_Calib~" FOREIGN KEY ("CalibrationCertificateId") REFERENCES "CalibrationCertificates" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CalibrationNotificationEvents_CalibrationRenewals_Calibrati~" FOREIGN KEY ("CalibrationRenewalId") REFERENCES "CalibrationRenewals" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CalibrationNotificationEvents_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE UNIQUE INDEX "IX_CalibrationCertificates_CalibrationProvider_CertificateNumb~" ON "CalibrationCertificates" ("CalibrationProvider", "CertificateNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationCertificates_CertificateFileId" ON "CalibrationCertificates" ("CertificateFileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationCertificates_CreatedByUserId" ON "CalibrationCertificates" ("CreatedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationCertificates_ExpiryDate" ON "CalibrationCertificates" ("ExpiryDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationCertificates_MachineId_CalibrationDate_CreatedAt" ON "CalibrationCertificates" ("MachineId", "CalibrationDate", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationNotificationEvents_CalibrationCertificateId" ON "CalibrationNotificationEvents" ("CalibrationCertificateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationNotificationEvents_CalibrationRenewalId" ON "CalibrationNotificationEvents" ("CalibrationRenewalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE UNIQUE INDEX "IX_CalibrationNotificationEvents_DeduplicationKey" ON "CalibrationNotificationEvents" ("DeduplicationKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationNotificationEvents_MachineId" ON "CalibrationNotificationEvents" ("MachineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationNotificationEvents_ProcessedAt_MachineId" ON "CalibrationNotificationEvents" ("ProcessedAt", "MachineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationRenewals_CompletedCertificateId" ON "CalibrationRenewals" ("CompletedCertificateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE UNIQUE INDEX "IX_CalibrationRenewals_MachineId" ON "CalibrationRenewals" ("MachineId") WHERE "Status" = 'IN_PROGRESS';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationRenewals_MachineId_StartedAt" ON "CalibrationRenewals" ("MachineId", "StartedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationRenewals_PreviousCertificateId" ON "CalibrationRenewals" ("PreviousCertificateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    CREATE INDEX "IX_CalibrationRenewals_StartedByUserId" ON "CalibrationRenewals" ("StartedByUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915072124_CalibrationManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915072124_CalibrationManagement', '10.0.12');
    END IF;
END $EF$;
COMMIT;

