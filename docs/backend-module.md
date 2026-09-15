# MaintainPro backend foundation, identity, and machine management

This is the retained identity/machine module report. The subsequent [preventive planning module report](preventive-planning-module.md) records the new planning, versioned-checklist and work-order-generation implementation and its current validation results.

The existing five-project .NET 10 solution now implements this module. The approved PostgreSQL database was empty before migration; both reviewed forward migrations have been applied to `maintainpro_db`. Four standard roles are initialized. The first administrator and live token issuance still require the local configuration described below.

## Architecture and changes

| Layer | Responsibility |
| --- | --- |
| Domain | Existing eight foundation entities and machine enums, plus `AuditLog`, `RefreshToken`, role names, security stamp, and concurrency versions |
| Application | Request/result DTOs, identity/user/master-data/machine use cases, validation, record visibility, persistence/current-user/password/token interfaces, and safe audit payload construction |
| Infrastructure | PostgreSQL EF mappings and transactions, standard password hashing, JWT/refresh generation, initialization, and dependency injection |
| API | Thin `/api/v1` endpoints, named policies, JWT validation, current request identity, ProblemDetails, rate limiting, Development OpenAPI, and database health |
| Tests | Offline PostgreSQL model inspection, isolated SQLite relational service tests, and real HTTP-pipeline tests using `WebApplicationFactory` |

See [the complete file and package inventory](backend-module-files.md) for files added, modified, and removed. Earlier foundation files and existing unrelated changes were preserved. The weather-forecast code was removed from `Program.cs`. The earlier three `Class1.cs` removals and empty `UnitTest1.cs` removal remain in the working tree. There are no tracked `bin/obj` files; the earlier 318 index-only deletions retain local generated files. No commit or push was performed.

New package references are `Microsoft.EntityFrameworkCore` 10.0.12 in Application, `System.IdentityModel.Tokens.Jwt` 8.14.0 in Infrastructure, `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12 in API, and `Microsoft.EntityFrameworkCore.Sqlite` / `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 in Tests. Infrastructure references the ASP.NET Core shared framework for the standard password hasher. The existing Npgsql provider remains 10.0.3 and EF tooling remains 10.0.12.

## Database and historical integrity

Migrations:

1. Existing `20260914195546_InitialCreate` was retained and applied. It creates `Roles`, `Users`, `UserRoles`, `Departments`, `Locations`, `MachineCategories`, `Machines`, and `MachineAssignmentHistories` (the existing plural table convention).
2. New `20260914202309_IdentityAndMachineManagement` creates `AuditLogs` and `RefreshTokens`, adds user security/concurrency fields and machine concurrency version, and replaces the assignment machine index with a unique index filtered to active rows. Its designer and model snapshot are included.

The [reviewed forward SQL](database/BackendModule.sql) contains the complete migration chain. Forward operations create tables/add columns and strengthen the active-assignment index; they do not drop tables, drop columns, or delete data. Generated `Down` methods are destructive rollback instructions and were **not** run. Startup initializes identity data but never applies migrations automatically.

All 16 foreign keys use `Restrict`. `UserRole` retains the composite `(UserId, RoleId)` primary key. A user may have multiple roles and has no single `RoleId` property. User and machine physical deletion is also blocked by normal tracked `SaveChanges` workflows.

Relationships:

- User has an optional department and multiple role memberships through `UserRole`.
- Location has an optional department.
- Machine optionally references category, department, location, owner user, and supervisor user.
- Assignment history references machine, technician, optional supervisor, and assigning user.
- Audit optionally references its actor user.
- Refresh token references its user and optional replacement token; a family ID groups one login session's rotation chain.

The eight unique indexes cover `Role.Name`, `User.EmployeeId`, `User.Email`, `Department.Name`, `MachineCategory.Name`, `Machine.MachineCode`, refresh token hash, and active history `MachineId WHERE EffectiveTo IS NULL`. Foreign-key lookup indexes and audit date/session-family indexes are also mapped. Location names are validated case-insensitively within serializable application transactions; the approved foundation does not add a unique `Location.Name` database index. Employee IDs/machine codes are normalized to uppercase and email to lowercase on application writes.

## Identity and authorization

Passwords use Microsoft's `PasswordHasher<User>`. New passwords require 12–128 characters. Login accepts one `identifier` containing an employee ID or email. Unknown/inactive accounts and incorrect passwords receive the same generic 401; standard password verification also runs for unknown/inactive accounts to reduce account-discovery timing differences. Password hashes never appear in DTOs.

Access tokens use HS256 and configurable issuer, audience, signing key, lifetime, and refresh lifetime. Defaults: issuer `MaintainPro`, audience `MaintainPro.Client`, access 15 minutes, refresh 7 days. Configuration accepts access 1–60 minutes, refresh 1–90 days, and a signing key of at least 32 UTF-8 bytes. Validation checks signature, allowed algorithm, issuer, audience, expiry, and the live active session/security stamp; clock tolerance is 30 seconds. Claims include `sub`, `employee_id`, `email`, every assigned `role`, `jti`, `security_stamp`, and session `sid`.

Refresh credentials contain 64 cryptographically random bytes. Only their SHA-256 hashes are persisted. Rotation revokes the old token, links its replacement, and retains the family. Reuse of a revoked/replaced token revokes the entire family. Serializable transactions and version checks prevent two successful concurrent rotations; a concurrent conflict returns 409. Clients should serialize refresh calls. Logout revokes the supplied session family. Password changes and sensitive user/role/status changes revoke sessions and change the security stamp, invalidating access immediately.

Policies `RequireTechnician`, `RequireSupervisor`, `RequireManager`, and `RequireAdmin` require the exact named role. `ManageUsers` requires ADMIN; `ManageMachines` and `ViewAllMachines` accept MANAGER or ADMIN. `ReadMachines` accepts any standard role and is supplemented by record-level checks. A fallback policy requires authentication. ADMIN is not implicitly a valid technician or supervisor assignment.

Machine visibility is the union of current ownership for TECHNICIAN, current supervision for SUPERVISOR, and all machines for MANAGER/ADMIN. Queries apply scope before count/pagination. Details and history enforce the same scope; inaccessible machine IDs return 404.

Role initialization is idempotent and maintains exactly TECHNICIAN, SUPERVISOR, MANAGER, ADMIN. Unknown role definitions cause an explicit conflict instead of being silently removed. Bootstrap creates one ADMIN only when no application users exist and all five bootstrap fields are present and valid. Existing credentials are never overwritten. Missing bootstrap configuration logs a generic skip message and allows startup. No public registration endpoint exists.

## API contracts

All paths below use `/api/v1`. JSON property names use camel case. Machine enum values are strings (for example `Operational`, `Decommissioned`, `Medium`); integer enum values are rejected. Required request-constructor fields must be present. Optional values may be omitted where documented. Responses contain DTOs only.

| Area | Routes | Access |
| --- | --- | --- |
| Authentication | `POST /auth/login`, `POST /auth/refresh` | Anonymous; limited to 30 attempts/minute per client IP together with password change |
| Session | `POST /auth/logout`, `GET /auth/me`, `POST /auth/change-password` | Valid access token |
| Users | `GET /users`, `POST /users`, `GET /users/{id}`, `PUT /users/{id}`, `PATCH /users/{id}/status`, `PUT /users/{id}/roles` | ADMIN |
| User choices | `GET /users/technicians`, `GET /users/supervisors` | MANAGER or ADMIN; active users holding the required role |
| Departments | `GET /departments`, `POST /departments`, `PUT /departments/{id}`, `PATCH /departments/{id}/status` | MANAGER or ADMIN |
| Locations | `GET /locations`, `POST /locations`, `PUT /locations/{id}`, `PATCH /locations/{id}/status` | MANAGER or ADMIN |
| Categories | `GET /machine-categories`, `POST /machine-categories`, `PUT /machine-categories/{id}`, `PATCH /machine-categories/{id}/status` | MANAGER or ADMIN |
| Machine reads | `GET /machines`, `GET /machines/{id}`, `GET /machines/{id}/assignment-history` | Role union plus record scope |
| Machine writes | `POST /machines`, `PUT /machines/{id}`, `PATCH /machines/{id}/status`, `POST /machines/{id}/assign-owner` | MANAGER or ADMIN |

Login body: `{ "identifier": "<employee ID or email>", "password": "<password>" }`. Login/refresh response: `accessToken`, `refreshToken`, `accessTokenExpiresAt`, and `user`; all assigned roles are in `user.roles`. Refresh/logout body: `{ "refreshToken": "<refresh credential>" }`. Password change body: `{ "currentPassword": "<current>", "newPassword": "<new>" }`; it returns 204 and requires a new login. Token-bearing responses must be treated as credentials by clients.

User creation requires employee ID, first name, last name, email, initial password, and a nonempty array of standard role names. Optional fields are mobile, department ID, and active status. Employee IDs allow ASCII letters/digits, dots, hyphens, underscores. Role replacement uses `{ "roles": ["TECHNICIAN", "SUPERVISOR"] }`; status uses `{ "isActive": false }`. Self-lockout and removing/deactivating the last active administrator are rejected. Inactive users retain their historical references.

Master data writes take name, optional description, and active status; locations additionally accept an optional department ID. Lists accept optional `isActive`; locations also accept `departmentId`. Names and references are validated; there are no delete routes. Creates return 201 with the created DTO; master data has collection reads, so no unimplemented individual GET URL is advertised in a Location header.

Machine create/edit supports every approved field, including manufacturer/model/serial, all three dates, maintenance/calibration flags, owner, supervisor, notes, and active status. PUT replaces the editable record. References must exist and be active; department/location compatibility is checked. Owners must be active TECHNICIANs; supervisors must be active SUPERVISORs. Invalid dates/enums are rejected. Decommissioning sets inactive status. No DELETE route is available.

Machine list query parameters: `search`, `page` (default 1), `pageSize` (default 20; 1–100), `categoryId`, `departmentId`, `locationId`, `status`, `criticality`, `calibrationRequired`, `ownerUserId`, `supervisorUserId`, `isActive`. Search covers code, asset number, name, manufacturer, model, and serial number. Results are stable by machine code/ID and contain `items`, `page`, `pageSize`, `totalCount`, `totalPages`. User lists accept search, paging, active state, department, and role. Read-only queries use `AsNoTracking` where appropriate.

Assignment body requires both `machineOwnerUserId` and `supervisorUserId`; each may explicitly be null to clear it. `reason` is optional. On owner/supervisor change, the previous active history row receives only an end timestamp, then a new dated row is added when there is an owner. Past rows are retained. Clearing ownership closes history without creating a row with a missing required technician. Supervisor changes without an owner are retained in the audit trail. No-op reassignment does not create duplicate history. History/audit/machine mutations commit in one serializable transaction. A unique partial index prevents multiple active history records.

Audit events include user creation/profile/roles/status, password-change action without password values, master data changes, machine creation/edit/status/owner/supervisor changes. Safe old/new DTO values, actor, time, IP, and bounded device metadata are recorded in the same transaction as the mutation. Audit serialization excludes password/hash/token/secret/signing-key fields. Normal tracked workflows reject audit edits/deletes. There is no audit-management endpoint in this phase.

Errors use ProblemDetails: validation 400, missing/invalid authentication 401, authorization 403, missing/inaccessible record 404, uniqueness/concurrency/reference conflict 409, rate limit 429, unexpected failure 500, and unavailable token configuration 503. Responses omit database exception details, stack traces, and secrets. Application logging records sanitized failure type/status/trace ID. Console/debug logging avoids Windows Event Log write permissions.

`GET /health` is anonymous and checks database connectivity without exposing configuration. Development OpenAPI is at `/openapi/v1.json`, with Bearer security definitions and typed endpoint responses. Swagger UI is not installed.

## Local configuration still required

The existing `ConnectionStrings:DefaultConnection` was read through the API's Development configuration. It was never printed, enumerated, copied, or moved. JWT signing key and complete bootstrap configuration were not present. Therefore no live administrator or user session was invented.

Run this PowerShell block **once** from `D:\MaintainPro` to generate a local signing key and enter your chosen first administrator. It sends new settings to `dotnet user-secrets set` through JSON standard input, keeping passwords/keys out of command arguments and avoiding a secrets file in the repository. It does not read or overwrite the existing connection-string key. Repeating it changes the signing key and invalidates tokens signed with the previous key.

```powershell
$moduleKeyBytes = New-Object byte[] 64
$moduleRandom = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$moduleRandom.GetBytes($moduleKeyBytes)
$modulePassword = Read-Host 'First administrator password (12-128 characters)' -AsSecureString
$moduleSettings = @{
    'Jwt:SigningKey' = [Convert]::ToBase64String($moduleKeyBytes)
    'Jwt:Issuer' = 'MaintainPro'
    'Jwt:Audience' = 'MaintainPro.Client'
    'Jwt:AccessTokenMinutes' = '15'
    'Jwt:RefreshTokenDays' = '7'
    'BootstrapAdmin:EmployeeId' = (Read-Host 'Employee ID')
    'BootstrapAdmin:FirstName' = (Read-Host 'First name')
    'BootstrapAdmin:LastName' = (Read-Host 'Last name')
    'BootstrapAdmin:Email' = (Read-Host 'Email')
    'BootstrapAdmin:Password' = [Net.NetworkCredential]::new('', $modulePassword).Password
}
try {
    $moduleSettings | ConvertTo-Json | dotnet user-secrets set --project .\backend\MaintainPro.Api
    if ($LASTEXITCODE -ne 0) { throw 'User Secrets update failed.' }
}
finally {
    $moduleSettings.Clear()
    [Array]::Clear($moduleKeyBytes, 0, $moduleKeyBytes.Length)
    $modulePassword.Dispose()
    $moduleRandom.Dispose()
}
```

Start the existing HTTPS launch profile:

```powershell
dotnet run --project .\backend\MaintainPro.Api --launch-profile https
```

The first valid configured administrator is created on startup. After successful bootstrap, remove the one-time bootstrap values with these exact commands; existing user credentials remain unchanged:

```powershell
dotnet user-secrets remove 'BootstrapAdmin:Password' --project .\backend\MaintainPro.Api
dotnet user-secrets remove 'BootstrapAdmin:EmployeeId' --project .\backend\MaintainPro.Api
dotnet user-secrets remove 'BootstrapAdmin:FirstName' --project .\backend\MaintainPro.Api
dotnet user-secrets remove 'BootstrapAdmin:LastName' --project .\backend\MaintainPro.Api
dotnet user-secrets remove 'BootstrapAdmin:Email' --project .\backend\MaintainPro.Api
```

Restart after changing JWT settings. For deployments, equivalent environment keys use double underscores (for example `Jwt__SigningKey` and `BootstrapAdmin__Password`). No environment configuration or User Secrets were changed during implementation.

## Verification

Run from `D:\MaintainPro\backend`:

```powershell
dotnet build MaintainPro.slnx
dotnet test MaintainPro.slnx
dotnet ef migrations has-pending-model-changes --project MaintainPro.Infrastructure --startup-project MaintainPro.Api --context ApplicationDbContext -- --environment Development
dotnet run --project MaintainPro.Api --no-launch-profile -- --environment Development --database-inspect
```

The Development-only inspector connects only when the configured database name is `maintainpro_db`. It prints table metadata, row-presence booleans, migration IDs, standard role names, constraint/index counts, and booleans for exact required configuration keys. It performs no writes and never enumerates configuration or prints credentials.

Live verification completed: ten application tables plus `__EFMigrationsHistory`; both expected migration IDs; exactly ADMIN/MANAGER/SUPERVISOR/TECHNICIAN; 16 foreign keys, zero non-Restrict foreign keys, eight non-primary unique indexes. Roles are the only populated application table. Users, refresh tokens, machines, and audit logs remain empty. `/health` returned 200 Healthy, Development OpenAPI returned 25 paths/32 operations with Bearer authentication, and `/api/v1/machines` without a token returned 401. The temporary local verification server was stopped afterward.

Final validation results:

| Check | Result |
| --- | --- |
| `dotnet build MaintainPro.slnx` | Passed; 0 warnings, 0 errors |
| `dotnet test MaintainPro.slnx` | 100 passed, 0 failed, 0 skipped; 12-second test run |
| EF pending-model check | Passed: no changes since the last migration |
| `git diff --check` | No whitespace errors; Git reported existing LF-to-CRLF conversion notices |
| Git tracking check | 0 tracked `bin/obj` paths; 318 prior staged index-only deletions preserved; local build files retained; 0 tracked dotenv files |

Tests cover the required auth, user, machine, and audit cases, plus invalid JWT signature/issuer/audience/expiry, default query binding, omitted mutation fields, bootstrap idempotence, transaction rollback, concurrency conflict, and restricted deletion. The tests found and drove fixes for omitted paging defaults and unsafe request defaults. The security review also added dummy-hash login verification and scoped the history SELECT itself.

## Known limits and next modules

- Automated workflow/API tests use isolated SQLite, while model inspection, migrations, table/constraint checks, startup, health, and OpenAPI were also verified with PostgreSQL. A broader PostgreSQL concurrent-load test suite remains future work.
- Live administrator login needs the manual configuration above; authentication was exercised in automated tests with generated, isolated test credentials and signing keys.
- Logout requires a valid access token; an expired access token must be refreshed first. Refresh validity is rolling, with no absolute session-duration cap or background token cleanup yet.
- Serializability and concurrency versions reject overlapping writes. There is no client ETag/version contract yet, so a sequential stale edit may overwrite an earlier edit. Clients should reload after 409 responses. Concurrent first-time startup can require retrying one instance after another initializes roles.
- Role/status changes revoke sessions and preserve references; reassigning machines away from a deactivated user is an explicit management action.
- Audit append-only enforcement applies to normal tracked application workflows; database-level tamper protection, retention, and audit browsing/export are future work. Closed assignment rows are preserved by these use cases; direct privileged SQL is outside this protection.
- The basic auth limiter is in-process and uses the direct client IP; distributed limits, trusted proxy configuration, MFA, email verification, password recovery, and signing-key rotation infrastructure are not part of this module.
- The sandbox reported ephemeral ASP.NET data-protection keys and no HTTPS port for the temporary HTTP-only health probe. This module uses JWT/refresh sessions rather than protected cookies; the existing HTTPS profile is the documented local run mode.
- Maintenance plans, checklists, work orders, scheduling, calibration, breakdowns, external service implementation, files, notifications, reports, inventory, and frontend applications remain unimplemented. Basic external-service storage remains included in the MVP release plan.

**Change boundaries:** `maintainpro_db` was modified by the two authorized forward migrations and role initialization. No other PostgreSQL database was modified. No real secrets were printed, enumerated, or moved. No Git commit or push was performed.
