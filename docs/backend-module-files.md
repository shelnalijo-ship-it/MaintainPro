# Backend foundation, identity and machine management: file inventory

This inventory compares the module with the completed domain/database foundation and `InitialCreate` phases. Git HEAD predates those phases, so existing foundation files still appear as untracked additions and earlier scaffold removals still appear as deletions. They were preserved rather than recreated. Paths below are relative to the named project under `backend/`.

## MaintainPro.Domain

Added:

- `Entities/AuditLog.cs`
- `Entities/RefreshToken.cs`
- `Security/RoleNames.cs`

Modified:

- `Entities/User.cs`: security stamp and optimistic concurrency version.
- `Entities/Machine.cs`: optimistic concurrency version.

The existing `Role`, `UserRole`, `Department`, `Location`, `MachineCategory`, and `MachineAssignmentHistory` entities and `MachineStatus`/`MachineCriticality` enums belong to the previous foundation. Their files were retained.

## MaintainPro.Application

Added:

- `Abstractions/IApplicationDbContext.cs` (also defines the transaction abstraction)
- `Abstractions/IAuditWriter.cs`
- `Abstractions/ICurrentUser.cs`
- `Audit/AuditWriter.cs`
- `Common/AppException.cs`
- `Common/Guard.cs`
- `Common/PagedResult.cs`
- `Identity/AuthService.cs`
- `Identity/IdentityContracts.cs`
- `Identity/IdentityValidation.cs`
- `Identity/SessionRevocation.cs`
- `Users/UserContracts.cs`
- `Users/UserService.cs`
- `MasterData/MasterDataContracts.cs`
- `MasterData/MasterDataService.cs`
- `Machines/MachineContracts.cs`
- `Machines/MachineService.cs`

Modified: `MaintainPro.Application.csproj`, adding EF Core for database query abstractions and application queries.

## MaintainPro.Infrastructure

Added:

- `Identity/IdentityInitializer.cs`
- `Identity/PasswordService.cs`
- `Identity/RefreshTokenConfiguration.cs`
- `Identity/TokenService.cs` (also defines `JwtOptions`)
- `Persistence/Configurations/AuditLogConfiguration.cs`
- `Persistence/Migrations/20260914202309_IdentityAndMachineManagement.cs`
- `Persistence/Migrations/20260914202309_IdentityAndMachineManagement.Designer.cs`

Modified:

- `DependencyInjection.cs`: registers application, identity, audit, options and persistence services.
- `Persistence/ApplicationDbContext.cs`: adds audit/session sets, transactions, timestamp/concurrency handling and mutation guards.
- `Persistence/Configurations/UserConfiguration.cs`: concurrency mapping.
- `Persistence/Configurations/MachineConfiguration.cs`: concurrency mapping.
- `Persistence/Configurations/MachineAssignmentHistoryConfiguration.cs`: unique active-assignment index.
- `Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`: reflects this module's model.
- `MaintainPro.Infrastructure.csproj`: JWT package and ASP.NET Core framework reference.

The existing department, location, category, role and user-role configurations remain foundation files. `20260914195546_InitialCreate.cs` and its `.Designer.cs` were created in the previous migration phase and retained. This module adds the second migration and updates the existing snapshot.

## MaintainPro.Api

Added:

- `Development/DatabaseInspector.cs`
- `Endpoints/AuthEndpoints.cs`
- `Endpoints/UserEndpoints.cs`
- `Endpoints/MasterDataEndpoints.cs`
- `Endpoints/MachineEndpoints.cs`
- `Health/DatabaseHealthCheck.cs`
- `Middleware/ApiExceptionMiddleware.cs`
- `Security/AuthenticationConfiguration.cs`
- `Security/HttpCurrentUser.cs`
- `Security/IdentityInitializationService.cs`
- `Security/Policies.cs`

Modified:

- `Program.cs`: authentication/authorization, endpoint registration, error handling, initialization, rate limiting, health, Development OpenAPI, and explicit Console/Debug logging providers.
- `MaintainPro.Api.csproj`: JWT Bearer package.

Tracked application settings, launch settings and the existing User Secrets identifier were retained. Credentials are not part of this inventory or these additions.

## MaintainPro.Tests

Added:

- `ModuleFixture.cs`
- `IdentityServiceTests.cs`
- `UserServiceTests.cs`
- `MasterDataServiceTests.cs`
- `MachineServiceTests.cs`
- `AuditAndInitializerTests.cs`
- `ApiIntegrationTests.cs`

Modified:

- `ApplicationDbContextTests.cs`: extends the previous foundation model tests for the new entities, indexes and concurrency mapping.
- `MaintainPro.Tests.csproj`: SQLite and API integration-test dependencies; reference to the API project.

## Packages and project references

| Project | Added in this module | Purpose |
|---|---|---|
| API | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.12 | Bearer authentication |
| Application | `Microsoft.EntityFrameworkCore` 10.0.12 | EF query abstractions and queries |
| Infrastructure | `System.IdentityModel.Tokens.Jwt` 8.14.0 | JWT generation and signing |
| Tests | `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 | Relational service tests |
| Tests | `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 | API integration tests |

Infrastructure adds the `Microsoft.AspNetCore.App` framework reference for the standard password hasher and hosting/configuration abstractions. The API already receives that framework through the Web SDK. Tests add a project reference to `MaintainPro.Api`; their Infrastructure reference was added in the earlier foundation phase. Other project references remain unchanged: API to Application/Infrastructure, Application to Domain, Infrastructure to Application/Domain, and Tests to Application/Infrastructure.

`Microsoft.EntityFrameworkCore.Relational` 10.0.12 was added to Infrastructure in the earlier foundation phase and remains in the working-tree diff; it is not a new module dependency. Existing versions were retained: OpenAPI/EF Tools/EF Design 10.0.12, Npgsql EF provider 10.0.3, test SDK 17.14.1, xUnit 2.9.3, Visual Studio xUnit runner 3.1.4, and coverlet collector 6.0.4. No project target framework changed; all remain .NET 10.

## Documentation

Added in this module:

- `docs/backend-module.md`: module report, operation and manual configuration instructions.
- `docs/backend-module-files.md`: this inventory.
- `docs/database/BackendModule.sql`: generated schema review script.

Modified: `docs/implementation-plan.md`, recording current module status, and `.gitignore`, adding `.env` / `.env.*` exclusions.

The original requirements/specification copies, `docs/specification-v3-review.md`, `docs/database/InitialCreate.sql`, and `docs/database/InitialCreate-review.md` are retained from previous phases.

## Removals and existing cleanup

No source file was removed in this module. The working tree still contains these earlier scaffold removals:

- `backend/MaintainPro.Application/Class1.cs`
- `backend/MaintainPro.Domain/Class1.cs`
- `backend/MaintainPro.Infrastructure/Class1.cs`
- `backend/MaintainPro.Tests/UnitTest1.cs`

The 318 staged `bin/obj` removals are the previous phase's removal from Git tracking only; local generated files were kept. They are not source deletions introduced by this module. The previous `.gitignore` retained `**/bin/`, `**/obj/`, `.vs/`, and `**/TestResults/`; this module added `.env` and `.env.*`. Existing uncommitted foundation work and cleanup were preserved.

Validation and database execution results are documented separately in `docs/backend-module.md`.
