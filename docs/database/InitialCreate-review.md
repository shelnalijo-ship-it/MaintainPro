# InitialCreate migration review

Status: generated for review only. No migration or SQL was applied, and no PostgreSQL connection or persistence test was performed.

## Artifacts and tooling

- Migration: [20260914195546_InitialCreate.cs](../../backend/MaintainPro.Infrastructure/Persistence/Migrations/20260914195546_InitialCreate.cs).
- Designer: [20260914195546_InitialCreate.Designer.cs](../../backend/MaintainPro.Infrastructure/Persistence/Migrations/20260914195546_InitialCreate.Designer.cs).
- Snapshot: [ApplicationDbContextModelSnapshot.cs](../../backend/MaintainPro.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs).
- Forward SQL: [InitialCreate.sql](InitialCreate.sql), generated from migration `0` to `InitialCreate`.
- Context: `MaintainPro.Infrastructure.Persistence.ApplicationDbContext`; startup project: `MaintainPro.Api`; environment: Development.
- EF CLI, runtime/design packages: 10.0.12. Npgsql EF provider: 10.0.3. No package or project-reference changes were necessary in this phase. EF Design is already present in the API startup dependency graph and output through its existing tooling dependency.
- The existing configuration loaded `ConnectionStrings:DefaultConnection` through API startup. User Secrets were not listed, printed, copied, or modified. Startup only registers the context; it performs no database creation, migration, seeding, or queries.

## Tables and primary keys

These are the actual quoted PostgreSQL application table names. All eight correspond to existing foundation entities; no business module was added.

| Entity | Table | Primary key |
| --- | --- | --- |
| Role | `Roles` | `Id` (`uuid`) |
| User | `Users` | `Id` (`uuid`) |
| UserRole | `UserRoles` | Composite `UserId`, `RoleId` (both `uuid`) |
| Department | `Departments` | `Id` (`uuid`) |
| Location | `Locations` | `Id` (`uuid`) |
| MachineCategory | `MachineCategories` | `Id` (`uuid`) |
| Machine | `Machines` | `Id` (`uuid`) |
| MachineAssignmentHistory | `MachineAssignmentHistories` | `Id` (`uuid`) |

The SQL additionally creates EF's expected `__EFMigrationsHistory` bookkeeping table if missing, with `MigrationId` (`character varying(150)`) as its primary key and required `ProductVersion` (`character varying(32)`). Its only insert records this migration ID and EF version; it is not user/role/machine seeding.

## Foreign keys

All 13 constraints use `ON DELETE RESTRICT`. Every relationship has an explicit existing CLR foreign-key property. No additional shadow foreign keys or single `Users.RoleId` column were generated.

| Dependent table and column | Principal table and key | Nullable |
| --- | --- | --- |
| `UserRoles.UserId` | `Users.Id` | No |
| `UserRoles.RoleId` | `Roles.Id` | No |
| `Users.DepartmentId` | `Departments.Id` | Yes |
| `Locations.DepartmentId` | `Departments.Id` | Yes |
| `Machines.MachineOwnerUserId` | `Users.Id` | Yes |
| `Machines.SupervisorUserId` | `Users.Id` | Yes |
| `Machines.CategoryId` | `MachineCategories.Id` | Yes |
| `Machines.DepartmentId` | `Departments.Id` | Yes |
| `Machines.LocationId` | `Locations.Id` | Yes |
| `MachineAssignmentHistories.MachineId` | `Machines.Id` | No |
| `MachineAssignmentHistories.TechnicianId` | `Users.Id` | No |
| `MachineAssignmentHistories.SupervisorId` | `Users.Id` | Yes |
| `MachineAssignmentHistories.AssignedByUserId` | `Users.Id` | No |

Multiple roles are represented by multiple `UserRoles` rows. The composite key prevents the same user/role pair from appearing twice; it does not restrict a user to one role.

## Indexes

All six required unique indexes are present:

| Index | Table and column |
| --- | --- |
| `IX_Roles_Name` | `Roles.Name` |
| `IX_Users_EmployeeId` | `Users.EmployeeId` |
| `IX_Users_Email` | `Users.Email` |
| `IX_Departments_Name` | `Departments.Name` |
| `IX_MachineCategories_Name` | `MachineCategories.Name` |
| `IX_Machines_MachineCode` | `Machines.MachineCode` |

There are 18 explicit indexes in total: these six unique indexes and twelve nonunique foreign-key indexes. `UserRoles.UserId` is already the leading column of the composite primary key, so it does not need a separate generated index. Primary-key indexes are separate from those 18 explicit `CREATE INDEX` statements.

## Columns, nullability, and defaults

All identifiers and foreign keys map to `uuid`. Required text maps to `text NOT NULL`; optional text maps to nullable `text`. No application string maximum lengths are configured. Activation and requirement flags map to `boolean NOT NULL`.

| Table | Required non-key columns | Optional columns |
| --- | --- | --- |
| `Roles` | Name | Description |
| `Users` | EmployeeId, FirstName, LastName, Email, PasswordHash, IsActive, CreatedAt, UpdatedAt | Mobile, DepartmentId, LastLoginAt |
| `UserRoles` | Both columns form the required composite key | None |
| `Departments` | Name, IsActive | Description |
| `Locations` | Name, IsActive | Description, DepartmentId |
| `MachineCategories` | Name, IsActive | Description |
| `Machines` | MachineCode, Name, Status, Criticality, CalibrationRequired, PreventiveMaintenanceRequired, IsActive, CreatedAt, UpdatedAt | AssetNumber, CategoryId, Manufacturer, Model, SerialNumber, DepartmentId, LocationId, InstallationDate, CommissioningDate, WarrantyExpiryDate, MachineOwnerUserId, SupervisorUserId, Notes |
| `MachineAssignmentHistories` | MachineId, TechnicianId, EffectiveFrom, AssignedByUserId | SupervisorId, EffectiveTo, Reason |

The three optional machine dates use PostgreSQL `date` and .NET `DateOnly?`. Seven timestamp columns use `timestamp with time zone`: user CreatedAt/UpdatedAt/LastLoginAt, machine CreatedAt/UpdatedAt, and history EffectiveFrom/EffectiveTo. LastLoginAt and EffectiveTo are nullable. The application uses UTC `DateTime` values; the schema does not store a .NET `DateTime.Kind` flag or a company time-zone setting.

`Machines.Status` and `Machines.Criticality` store enum names as required `text`, not numeric values or PostgreSQL enum types. Current names are:

- Status: Operational, UnderMaintenance, Breakdown, OutOfService, Standby, Decommissioned.
- Criticality: Low, Medium, High, Critical.

No allowed-value checks or maximum string lengths were silently added. No database defaults were generated for IDs, timestamps, status, criticality, or activation flags. Existing initial values such as `Guid.NewGuid()`, `DateTime.UtcNow`, Operational, Medium, and `IsActive = true` are C# initializers. A future insert outside those entity constructors must supply the required values.

## Forward migration and rollback review

`Up` contains eight `CreateTable` operations and eighteen `CreateIndex` operations. It contains no drop, alter, delete, update, seed, or custom SQL operations. The forward SQL contains the expected application table/index creation, migration-history bookkeeping, and transaction statements. No existing application rows are deleted or overwritten. No credentials, connection string, or seeded accounts are included; `PasswordHash` is only an empty schema column definition.

The script is intended for a database without these application tables. Application table creation is not idempotent: only the EF history table has `IF NOT EXISTS`. Existing conflicting tables or a different migration baseline require review before application, not blind execution. No live schema inspection was performed in this task.

`Down` drops the eight application tables in dependency-safe order: MachineAssignmentHistories, UserRoles, Machines, Roles, Locations, MachineCategories, Users, Departments. These expected rollback removals would destroy data in those tables if executed later. They are distinct from the nondestructive forward creation, and neither direction was executed.

The generated designer and snapshot model bodies match exactly. The snapshot's string-based entity/property declarations are generated EF metadata, not newly introduced shadow properties in the application model.

## Preserved limitations and decisions before the next phase

1. **Text uniqueness:** the model specifies ordinary text uniqueness without normalization or explicit case-insensitive collation. The expected default behavior is case-sensitive. Decide login/email and business-identifier normalization before authentication or revise the reviewed schema with approval; live database collation was not inspected.
2. **UpdatedAt:** values initialize in C#, but update operations do not yet refresh them automatically. Choose the application/persistence policy before implementing writes that rely on timestamps.
3. **Live PostgreSQL behavior:** connectivity, permissions, server version, existing schema, constraints under real transactions, and persistence remain untested. Before applying, explicitly approve the target and application operation and review compatibility with the target's schema/migration baseline and recovery arrangements.
4. **Historical integrity:** Restrict prevents database cascades and deleting referenced principals while references remain. It does not prevent editing/deleting history rows themselves. Future authorization, audit, and immutability rules remain necessary.
5. **Authentication:** roles and users were not seeded. Select password hashing, normalization, provisioning, multi-role authorization, refresh/revocation, and disabled-user behavior before implementing authentication. No authentication code was added.
6. **Other constraints:** string lengths, enum allowed-value validation, assignment interval/overlap validation, and technician/supervisor role eligibility remain application/design decisions. The approved foundation was not redesigned during migration generation.

## Git cleanup and preservation

A root `.gitignore` was added with `**/bin/`, `**/obj/`, `.vs/`, and `**/TestResults/`. Exactly 318 previously tracked backend generated files were removed from the index using targeted `git rm -r --cached` on the ten bin/obj directories. All remain on disk. Those staged generated-file deletions are separate from untracked/unstaged source work; no source files were staged and no commit or push was performed.

The prior foundation entities, enums, configurations, tests, project references, and source changes were preserved. The original documents were not altered. Migration generation introduced only the expected migration, designer, and snapshot source files; the reviewed SQL and this report are documentation artifacts.

## Verification record

| Check | Result |
| --- | --- |
| Pre-generation `dotnet build MaintainPro.slnx` | Passed; 0 warnings, 0 errors |
| Pre-generation `dotnet test MaintainPro.slnx` | Passed; 12 passed, 0 failed, 0 skipped |
| Final `dotnet build MaintainPro.slnx` | Passed; 0 warnings, 0 errors |
| Final `dotnet test MaintainPro.slnx` | Passed; 12 passed, 0 failed, 0 skipped |
| `dotnet ef migrations has-pending-model-changes --project MaintainPro.Infrastructure --startup-project MaintainPro.Api --context ApplicationDbContext -- --environment Development` | Passed; no changes since the last migration |
| Independent migration/designer/snapshot/SQL review | Passed; no blocking mismatches found |

No PostgreSQL connectivity or persistence result is claimed. Migration application remains pending explicit user approval.
