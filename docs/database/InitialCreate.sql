CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Departments" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Departments" PRIMARY KEY ("Id")
);

CREATE TABLE "MachineCategories" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_MachineCategories" PRIMARY KEY ("Id")
);

CREATE TABLE "Roles" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
);

CREATE TABLE "Locations" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "DepartmentId" uuid,
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Locations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Locations_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "EmployeeId" text NOT NULL,
    "FirstName" text NOT NULL,
    "LastName" text NOT NULL,
    "Email" text NOT NULL,
    "Mobile" text,
    "PasswordHash" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "DepartmentId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "LastLoginAt" timestamp with time zone,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Users_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Machines" (
    "Id" uuid NOT NULL,
    "MachineCode" text NOT NULL,
    "AssetNumber" text,
    "Name" text NOT NULL,
    "CategoryId" uuid,
    "Manufacturer" text,
    "Model" text,
    "SerialNumber" text,
    "DepartmentId" uuid,
    "LocationId" uuid,
    "InstallationDate" date,
    "CommissioningDate" date,
    "WarrantyExpiryDate" date,
    "Status" text NOT NULL,
    "Criticality" text NOT NULL,
    "CalibrationRequired" boolean NOT NULL,
    "PreventiveMaintenanceRequired" boolean NOT NULL,
    "MachineOwnerUserId" uuid,
    "SupervisorUserId" uuid,
    "Notes" text,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Machines" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Machines_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Machines_Locations_LocationId" FOREIGN KEY ("LocationId") REFERENCES "Locations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Machines_MachineCategories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "MachineCategories" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Machines_Users_MachineOwnerUserId" FOREIGN KEY ("MachineOwnerUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Machines_Users_SupervisorUserId" FOREIGN KEY ("SupervisorUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "UserRoles" (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    CONSTRAINT "PK_UserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_UserRoles_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_UserRoles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MachineAssignmentHistories" (
    "Id" uuid NOT NULL,
    "MachineId" uuid NOT NULL,
    "TechnicianId" uuid NOT NULL,
    "SupervisorId" uuid,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveTo" timestamp with time zone,
    "AssignedByUserId" uuid NOT NULL,
    "Reason" text,
    CONSTRAINT "PK_MachineAssignmentHistories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MachineAssignmentHistories_Machines_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Machines" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MachineAssignmentHistories_Users_AssignedByUserId" FOREIGN KEY ("AssignedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MachineAssignmentHistories_Users_SupervisorId" FOREIGN KEY ("SupervisorId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MachineAssignmentHistories_Users_TechnicianId" FOREIGN KEY ("TechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Departments_Name" ON "Departments" ("Name");

CREATE INDEX "IX_Locations_DepartmentId" ON "Locations" ("DepartmentId");

CREATE INDEX "IX_MachineAssignmentHistories_AssignedByUserId" ON "MachineAssignmentHistories" ("AssignedByUserId");

CREATE INDEX "IX_MachineAssignmentHistories_MachineId" ON "MachineAssignmentHistories" ("MachineId");

CREATE INDEX "IX_MachineAssignmentHistories_SupervisorId" ON "MachineAssignmentHistories" ("SupervisorId");

CREATE INDEX "IX_MachineAssignmentHistories_TechnicianId" ON "MachineAssignmentHistories" ("TechnicianId");

CREATE UNIQUE INDEX "IX_MachineCategories_Name" ON "MachineCategories" ("Name");

CREATE INDEX "IX_Machines_CategoryId" ON "Machines" ("CategoryId");

CREATE INDEX "IX_Machines_DepartmentId" ON "Machines" ("DepartmentId");

CREATE INDEX "IX_Machines_LocationId" ON "Machines" ("LocationId");

CREATE UNIQUE INDEX "IX_Machines_MachineCode" ON "Machines" ("MachineCode");

CREATE INDEX "IX_Machines_MachineOwnerUserId" ON "Machines" ("MachineOwnerUserId");

CREATE INDEX "IX_Machines_SupervisorUserId" ON "Machines" ("SupervisorUserId");

CREATE UNIQUE INDEX "IX_Roles_Name" ON "Roles" ("Name");

CREATE INDEX "IX_UserRoles_RoleId" ON "UserRoles" ("RoleId");

CREATE INDEX "IX_Users_DepartmentId" ON "Users" ("DepartmentId");

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

CREATE UNIQUE INDEX "IX_Users_EmployeeId" ON "Users" ("EmployeeId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260914195546_InitialCreate', '10.0.12');

COMMIT;

