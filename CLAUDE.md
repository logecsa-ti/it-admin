# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

TI Admin: an enterprise IT administration system (assets, licenses, help desk, maintenance, audit). Backend is ASP.NET Core 10 / .NET 10 / EF Core 10 / SQL Server 2022. The Angular 22 frontend (`ti-admin-web/`) is planned (Phase 9) but **does not exist yet**. Docs, user-facing messages and code comments are in Spanish.

Before non-trivial work, read `docs/DEV_MEMORY.md` (current phase, change log, ADRs, open questions) and update it after significant changes. `docs/SPECS.md` is the full spec (sections are cited as `SPECS.md §N` in code comments), `docs/IMPLEMENTATION_PLAN.md` the phased plan, `docs/ERD.md` the data model, `docs/CONVENTIONS.md` the coding/API/SQL rules.

## Commands

`dotnet` may not be on PATH on this machine. Prefix with `$env:PATH = "C:\Program Files\dotnet;$env:PATH"` if needed.

The solution file is `TIAdmin.slnx` and projects live under `src/`. The README and CONVENTIONS still show `TIAdmin.sln` and paths without `src/`. Those are outdated, so use the commands below.

```powershell
docker compose up -d sqlserver                 # SQL Server 2022 on :1433 (redis: --profile cache)
dotnet build .\TIAdmin.slnx
dotnet test .\TIAdmin.slnx
dotnet test .\TIAdmin.slnx --filter "FullyQualifiedName~PermissionsTests"           # one class
dotnet test .\TIAdmin.slnx --filter "FullyQualifiedName~PermissionsTests.AllPermissions_ShouldHaveUniqueCodes"  # one test
dotnet run --project src/TIAdmin.Api           # http://localhost:5142, Swagger at /swagger (Development only)

dotnet ef migrations add <Name> --project src/TIAdmin.Infrastructure --startup-project src/TIAdmin.Api
dotnet ef database update --project src/TIAdmin.Infrastructure --startup-project src/TIAdmin.Api
```

Migrations go through `TIAdminDbContextFactory` (design-time). It reads appsettings/env vars and falls back to the local docker connection string, so `dotnet ef` doesn't need JWT secrets or a running API.

Runtime secrets (`ConnectionStrings:DefaultConnection`, `Jwt:*`, `Seed:Enabled`, `Seed:AdminPassword`) come from `dotnet user-secrets` on `TIAdmin.Api`. See `src/TIAdmin.Api/appsettings.Local.example.json` for the keys. The API refuses to start without the `Jwt` section. When `Seed:Enabled` is true, `DatabaseSeeder` runs at startup and is idempotent. The dev admin login is `admin` / `Admin123!Local`.

## Architecture

Clean Architecture modular monolith. Dependencies flow Api → Infrastructure → Application → Domain, and Tests references all four projects.

- **Domain**: has no framework dependencies. Entities derive from `Entity` / `AuditableEntity` / `AuditableSoftDeletableEntity` (`Domain/Common/Entity.cs`). Int `Id`, UTC timestamps, `CreatedBy`/`UpdatedBy` as user ids.
- **Application**: ports (`Common/Interfaces`: `IRepository<T>`, `IUnitOfWork`, `ICurrentUserService`, `IClock`, `ITokenService`, `IAuditContext`), DTO/request records (`Common/Models/*Models.cs`), FluentValidation validators (`Validators/`), `Options.cs` (Options pattern), and the permission catalog.
- **Infrastructure**: `TIAdminDbContext` (an `IdentityDbContext<ApplicationUser, ApplicationRole, int>`), Fluent API configs in `Persistence/Configurations` (auto-applied from the assembly), repositories + `UnitOfWork`, JWT/permission services, and all DI registration in `Extensions/ServiceCollectionExtensions.cs` (`AddPersistence`, `AddIdentity`, `AddApplicationOptions`).
- **Api**: `Program.cs` wires auth, authorization policies, rate limiting, CORS, health checks and the middleware order (ExceptionHandling → CorrelationId → SecurityHeaders → … → Auth). Controllers live in `Controllers/`.

### Current pattern vs. documented target

ARCHITECTURE.md describes MediatR vertical slices (`Application/Features/<Module>/<Action>/` with command, handler and validator, plus pipeline behaviors). **None of that exists yet.** MediatR and Mapster are referenced but unused. The code that does exist (e.g. `DepartmentsController`) injects `IUnitOfWork`, creates the FluentValidation validator by hand inside the action, maps entities to DTO records manually, and returns `ApiResponse<T>`. List endpoints take `[FromQuery] PagedQuery` plus filters and call a repository `SearchAsync`, which projects to DTOs and returns `PagedResult<TDto>` (sorting uses a whitelist of `SortBy` values). Repositories live in `Infrastructure/Persistence/Repositories` and must be added to both `IUnitOfWork`/`UnitOfWork` and the DI registrations. Follow the existing pattern unless asked to introduce the Features/MediatR structure.

### Cross-cutting behavior to be aware of

- **Soft delete**: `ModelBuilderExtensions.ApplyGlobalConventions` adds a global `!IsDeleted` query filter to every `ISoftDeletable`. `AuditSaveChangesInterceptor` sets `CreatedAt`/`UpdatedAt`/`CreatedBy`/`UpdatedBy` and turns a `Remove()` of an `ISoftDeletable` into an update that sets `IsDeleted`/`DeletedAt`/`DeletedBy`. Use `IgnoreQueryFilters()` to see deleted rows. Unique indexes (e.g. `Code`) still cover deleted rows, so uniqueness checks like `ExistsCodeAsync` must use `IgnoreQueryFilters()`.
- **Audit trail**: `AuditTrailInterceptor` writes `AuditLog` rows with before/after values on SaveChanges, including the correlation id from `IAuditContext`. It runs after `AuditSaveChangesInterceptor` (registration order matters) and logs a soft deletion as `AuditAction.Delete`. `Create` rows are deferred to `SavedChanges` (a second SaveChanges containing only AuditLogs) so they record the real identity id. Sensitive properties (`PasswordHash`, `TokenHash`, `LicenseKey`, …) are excluded through a hard-coded set. Add new secret-bearing columns to that set.
- **FKs to users**: Domain entities hold plain `int` user ids. The FK to `Users` is declared only in the Fluent config, via `builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(...)` with `Restrict`.
- **Table names**: set explicitly and in plural in each `IEntityTypeConfiguration` (ADR-009). Don't rely on conventions.
- **Authentication**: JWT only. `AddIdentity` registers Identity's cookie as the default authenticate/challenge scheme, so `Program.cs` explicitly sets every default scheme to JwtBearer. Don't revert that to `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)`, which leaves the cookie in charge (tokens ignored, 302 to `/Account/Login`). Login uses `CheckPasswordSignInAsync`, never `PasswordSignInAsync`, which would issue a cookie.
- **Authorization**: the fallback policy requires an authenticated user. Every permission code in `Permissions.All` is registered as a policy of the same name, enforced by `PermissionAuthorizationHandler`, and used as `[Authorize(Policy = Permissions.X)]`. `PermissionDefinition.Code` is derived from `Module` + `Action` (e.g. `AssetTypes`/`Manage` → `ASSET_TYPES.MANAGE`) and must never be hand-written (ADR-010). To add a permission, add it to `Permissions.cs`, include it in `All`, and grant it in the role matrix `Permissions.ForRole(...)` (role names are in `SystemRoles`). The seeder picks it up, and `PermissionsTests` checks format and uniqueness.
- **Responses**: always use the `ApiResponse` / `ApiResponse<T>` envelope (`success`, `data`, `message`, `errors[{code,message}]`). 401/403 responses are emitted in the same shape from the JwtBearer events. Validation errors use code `VALIDATION_ERROR`. Paging uses `PagedQuery` / `PagedResult<T>`.
- **Routes**: `/api/v1/<kebab-case-plural>`. Enums serialize as strings, and nulls are omitted.

## Conventions

- Conventional Commits with a scope, e.g. `feat(assets): …`. GitFlow-style branches: `main`, `develop` (integration and PR target), `feature/*`, `bugfix/*`, `release/*`, `hotfix/*`.
- Return DTOs, never EF entities. Use `AsNoTracking`/projections for reads, and propagate `CancellationToken`.
- Store all dates in UTC (`datetime2`). Binary files never go in SQL Server, only their metadata.
- Tests use xUnit, FluentAssertions and NSubstitute. `Unit/` covers persistence over EF InMemory with the real interceptors. `Functional/` drives the full HTTP pipeline through `TIAdminApiFactory` (a `WebApplicationFactory<Program>` on EF InMemory, seeded admin, plus a `TI_ASSET_MANAGER` test user for 403 cases). Use it as a class fixture and get clients via `CreateAdminClientAsync()` / `CreateAssetManagerClientAsync()`. Logins are cached per factory because the login rate limiter is shared. Program.cs reads config before `Build()`, so test settings must go through `UseSetting`, not `ConfigureAppConfiguration`. InMemory doesn't reproduce SQL Server behavior (identity keys, filtered indexes), so verify those against the docker DB. `Integration/` (Testcontainers) is still empty.
