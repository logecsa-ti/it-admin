# DEVELOPMENT MEMORY - TI Admin

**Proyecto:** Sistema Administrativo de Tecnologías de Información (TI Admin)
**Referencia:** `docs/SPECS.md`, `docs/IMPLEMENTATION_PLAN.md`
**Fecha de inicio:** 2026-10-02
**Última actualización:** 2026-10-05
**Estado general:** En progreso
**Fase actual:** Fase 9 - Frontend Angular (Fase 8 completada; backend funcional completo)

---

## 0. Bloqueos del entorno

| Recurso | Estado | Detalle |
|---|---|---|
| .NET SDK 10 | ✅ Operativo | 10.0.401 en `C:\Program Files\dotnet` (requiere agregar al PATH) |
| Node.js | ✅ Operativo | v24.21.0 |
| Docker | ✅ Operativo | Engine 29.7.2. Si el daemon está apagado, arrancar Docker Desktop antes de `docker compose up -d sqlserver` |
| Git | ✅ Operativo | v2.55.0 |

**Entorno desbloqueado.** B-01 y B-02 resueltos.

### Nota de PATH

`dotnet` no está en el PATH del sistema. Para usar los comandos de esta sesión se antepone:
```powershell
$env:PATH = "C:\Program Files\dotnet;$env:PATH"
```
Recomendación: agregar `C:\Program Files\dotnet` al PATH del sistema para futuras sesiones.

---

## 1. Resumen del Proyecto

- **Arquitectura:** Modular Monolith + Clean Architecture (API + SPA)
- **Backend:** ASP.NET Core 10 / .NET 10 / C# 14, EF Core 10, Identity, JWT/OIDC
- **Frontend:** Angular 22.x, Standalone Components, Signals/RxJS, SCSS
- **Base de datos:** SQL Server 2022+
- **Repositorio:** `it-admin` (ubicación: `C:\Users\Lenovo\source\repos\it-admin`)
- **Estrategia Git:** `main`, `develop`, `feature/*`, `bugfix/*`, `release/*`, `hotfix/*`

---

## 2. Estado Actual

| Métrica | Valor |
|---|---|
| Fase actual | **Fase 9 - Frontend Angular** |
| Fases completadas | 9 / 12 (Fase 0-8) |
| Tareas completadas (Fase 8) | 4 / 4 |
| Tests | 207 / 207 (119 unit + 88 functional) |
| Estado | En progreso |

### Progreso global

| Fase | Estado |
|---|---|
| 0 - Preparación y Fundación | ✅ Completada |
| 1 - Infraestructura Base | ✅ Completada |
| 2 - Auth/Authz/Seguridad | ✅ Completada |
| 3 - Núcleo Organizacional | ✅ Completada |
| 4 - Activos y Asignaciones | ✅ Completada |
| 5 - Software/Licencias/Proveedores/Contratos | ✅ Completada |
| 6 - Help Desk/Solicitudes/SLA | ✅ Completada |
| 7 - Mantenimientos/Cambios/Compras | ✅ Completada |
| 8 - Auditoría/Reportes/Dashboard/Config | ✅ Completada |
| 9 - Frontend Angular | ⬜ Pendiente |
| 10 - Integraciones/Notificaciones/Archivos/Caché | ⬜ Pendiente |
| 11 - Calidad/Tests/CI-CD | ⬜ Pendiente |
| 12 - Despliegue/Go-Live | ⬜ Pendiente |

---

## 3. Log de Cambios

| Fecha | Fase | Acción | Archivos afectados | Notas |
|---|---|---|---|---|
| 2026-10-02 | Inicial | Creado `IMPLEMENTATION_PLAN.md` | `docs/IMPLEMENTATION_PLAN.md` | Plan completo de implementación por fases |
| 2026-10-02 | Fase 0 | Creado `DEV_MEMORY.md` | `docs/DEV_MEMORY.md` | Estructura inicial de memoria de desarrollo |
| 2026-10-02 | Fase 0.1 | Verificado estado Git, creadas ramas `main` y `develop` | Repositorio Git | Commit raíz `ffa44b1`, `master` renombrado a `main` |
| 2026-10-02 | Fase 0.1 | Creados `CONTRIBUTING.md` y `README.md` | `CONTRIBUTING.md`, `README.md` | Commits `ffa44b1`, `6621f99` |
| 2026-10-02 | Fase 0.2 | Verificado entorno: .NET 10 ausente, Node 24.21 OK, Docker CLI OK (daemon apagado) | — | **Bloqueante detectado** para Fase 1 |
| 2026-10-02 | Fase 0.2 | Creado `.gitignore` (multi-stack: .NET/Angular/Docker/secretos) | `.gitignore` | Excluye secretos, bin/obj, node_modules, .env |
| 2026-10-02 | Fase 0.2 | Creado `docker-compose.yml` (SQL Server 2022 + Redis opcional) | `docker-compose.yml` | Redis bajo profile `cache`; healthcheck configurado |
| 2026-10-02 | Fase 0.2 | Creado `.env.example` | `.env.example` | Plantilla de variables de desarrollo (nunca `.env` real) |
| 2026-10-02 | Fase 0.3 | Creado `docs/CONVENTIONS.md` | `docs/CONVENTIONS.md` | Convenciones C#, SQL, Angular, API REST, commits, secretos |
| 2026-10-02 | Fase 0.3 | Creado `docs/ARCHITECTURE.md` | `docs/ARCHITECTURE.md` | Capas, módulos, flujo de request, decisiones de diseño |
| 2026-10-02 | Fase 0.4 | Creado `docs/ERD.md` | `docs/ERD.md` | ERD Mermaid + detalle de ~30 entidades, índices y cardinalidades |
| 2026-10-02 | Fase 0 | **Fase 0 completada** (4/4 tareas) | — | Documentación base lista |
| 2026-10-02 | Fase 1.1 | Creada solución `TIAdmin.slnx` (.NET 10) + 5 proyectos en `src/` con referencias cruzadas | `TIAdmin.slnx`, `src/**` | Domain ← Application ← Infrastructure ← Api; Tests referencia todo |
| 2026-10-02 | Fase 1.2 | Agregados ~30 paquetes NuGet | `*.csproj` | EF Core 10.0.12, Identity 10.0.12, MediatR 14, FluentValidation 12, Mapster 10, Serilog 10, Swashbuckle 10, xUnit, NSubstitute, FluentAssertions, Testcontainers |
| 2026-10-02 | Fase 1.3 | Options Pattern (`Options.cs`) + `appsettings.json`/`.Development.json` | `Application/Common/Models/Options.cs` | Jwt, App, Cors, RateLimiting, Storage, Email, Cache, Seed. Sin secretos en repo |
| 2026-10-02 | Fase 1.4 | Domain: `IEntity`/`ISoftDeletable`/`IAuditableEntity`, clases base, 30+ enums, excepciones, `IClock`/`ICurrentUserService` | `Domain/**` | Cero dependencias de frameworks |
| 2026-10-02 | Fase 1.4 | Catálogo de **54 permisos** y matriz de 7 roles | `Application/Common/Constants/Permissions.cs` | `PermissionDefinition` deriva `Code` de `Module.Action` (imposible divergir) |
| 2026-10-02 | Fase 1.5 | `TIAdminDbContext` (IdentityDbContext) + Fluent API + query filter soft delete + interceptor auditoría | `Infrastructure/Persistence/**` | `TIAdminDbContextFactory` design-time (requerido por `dotnet ef`) |
| 2026-10-02 | Fase 1.6 | `DatabaseSeeder`: permisos, roles, 12 asset types, 9 configuraciones, admin | `Infrastructure/Persistence/Seeding/DatabaseSeeder.cs` | Idempotente. `Seed:Enabled` debe ser false en producción |
| 2026-10-02 | Fase 1.7 | **Migración `InitialCreate` aplicada** a SQL Server 2022 (docker) | `Infrastructure/Persistence/Migrations/**` | 15 tablas. Sin cambios de modelo pendientes |
| 2026-10-02 | Fase 1.7 | **Corrección**: eliminada singularización global de tablas | `ModelBuilderExtensions.cs` | Producía `Department` vs `Users` inconsistente. Nombres ahora explícitos y plurales (SPECS §62) |
| 2026-10-02 | Fase 1.7 | Recreada la migración y base de datos desde cero | — | Historial limpio: solo `InitialCreate` |
| 2026-10-02 | Fase 1.8 | **Verificado**: 28/28 tests, `/health` `/health/live` `/health/ready` `/swagger` → 200, seed aplicado (54 perm, 7 roles, 200 role-permisos, 1 admin, 12 tipos, 9 configs) | — | Fase 1 completada |
| 2026-10-02 | Fase 1 | **Fase 1 completada** (8/8 tareas) | — | Backend base operativo y verificable |
| 2026-10-02 | Fase 2 | **Fase 2 completada** (commit `ad3b641`): `AuthController` (login/refresh/logout/me/change-password), `JwtTokenService` con refresh token rotativo, `PermissionAuthorizationHandler` + una política por permiso, middleware CorrelationId/ExceptionHandling/SecurityHeaders, rate limiting (global + `login`), CORS fail-closed, `AuditLog` + `AuditTrailInterceptor`, migración `AddAuthenticationAndAudit` | `src/**` | — |
| 2026-10-02 | Fase 3 | WIP sin commit: repositorios + `UnitOfWork`, CRUD `DepartmentsController` / `LocationsController`, validadores, entidades `Asset` / `AssetAssignment` | `src/**` | — |
| 2026-10-05 | Fase 3 | **Corrección crítica**: el soft delete nunca se aplicaba (`Remove()` borraba físicamente). `AuditSaveChangesInterceptor` ahora convierte `Deleted` → `IsDeleted/DeletedAt/DeletedBy`; `AuditTrailInterceptor` lo registra como `AuditAction.Delete` | `Interceptors/*.cs` | Viola ADR-004 hasta ahora |
| 2026-10-05 | Fase 3 | Limpieza WIP: eliminado enum `AssetStatus` duplicado en Application; `UnitOfWork` ya no libera el DbContext de DI; `ExistsCodeAsync` considera eliminados (evita 500 por índice único); `UpdateAssetRequest.IsActive` eliminado (Asset no lo tiene) | `Application/**`, `Repositories/**` | ADR-013 |
| 2026-10-05 | Fase 3 | Paginación/búsqueda/orden en `GET /departments` (`isActive`, `parentId`) y `GET /locations` (`isActive`); validación de padre existente, ciclos y borrado con subdepartamentos (409) | Controllers, Repositories | `PagedQuery` → `PagedResult<TDto>` con proyección |
| 2026-10-05 | Fase 4 | FKs reales a `Users` para `Assets.CurrentUserId` y `AssetAssignments.UserId/AssignedById/ReturnedById` (Restrict); migración `AddAssetsAndAssignments` generada | `AssetConfigurations.cs`, `Migrations/**` | **No aplicada**: Docker apagado. ADR-014 |
| 2026-10-05 | Fase 3 | +5 tests (`OrganizationPersistenceTests`: soft delete, auditoría de borrado, códigos reservados, ciclos, búsqueda paginada) sobre EF InMemory con interceptores reales | `Tests/Unit/OrganizationPersistenceTests.cs` | 33/33 verdes |
| 2026-10-05 | Fase 4 | Migración `AddAssetsAndAssignments` **aplicada** a SQL Server (docker) | — | B-03 resuelto |
| 2026-10-05 | Fase 2 | **Corrección crítica**: el JWT nunca se validaba. `AddIdentity` fija la cookie de Identity como `DefaultAuthenticate/ChallengeScheme`, que tienen prioridad sobre `AddAuthentication(JwtBearer)`. Todo endpoint protegido devolvía 401 y los 401 redirigían (302) a `/Account/Login`. Ahora todos los esquemas por defecto son JwtBearer | `Program.cs` | Detectado al probar contra SQL Server; los tests no lo cubrían |
| 2026-10-05 | Fase 2 | Login usa `CheckPasswordSignInAsync` (lockout sin emitir cookie; antes `PasswordSignInAsync` emitía cookie de Identity). Cuenta bloqueada responde 401 `ACCOUNT_LOCKED` (antes 200 con `success:false`) | `AuthController.cs` | API stateless |
| 2026-10-05 | Fase 2 | **Corrección**: los AuditLog de `Create` guardaban el Id temporal de EF (`-2147482647`). `AuditTrailInterceptor` difiere las altas a `SavedChanges` y las guarda con el Id real (segundo SaveChanges solo con AuditLog) | `AuditTrailInterceptor.cs` | +1 test (34/34) |
| 2026-10-05 | Fase 2 | **Corrección**: CORS nunca permitía ningún origen. `Program.cs` leía la sección `Cors` como `string[]`, pero la configuración es `Cors:AllowedOrigins`. Ahora se enlaza `CorsOptions` | `Program.cs` | Detectado por test funcional |
| 2026-10-05 | Fase 3 | **Tests funcionales** (`WebApplicationFactory` + EF InMemory, pipeline real): `TIAdminApiFactory` (config vía `UseSetting`, usuario `TI_ASSET_MANAGER` de prueba, login por HTTP cacheado), `AuthAndSecurityTests` (401 JSON sin redirección, token alterado, credenciales inválidas, sin cookies, validación, `/me`, 403 por permiso, rotación y reutilización de refresh token, logout, health, headers de seguridad, correlation id, CORS permitido/denegado), `LoginRateLimitTests` (429), `OrganizationEndpointsTests` (CRUD, soft delete + AuditLog, código reservado, jerarquía, paginación) | `Tests/Functional/**`, `TIAdmin.Tests.csproj` (+`Microsoft.AspNetCore.Mvc.Testing` 10.0.12) | 56/56. Revertir el fix de esquemas JWT hace fallar 11 tests |
| 2026-10-05 | Fase 3.1 | Usuarios: `IUserManagementService` (Application) + `UserManagementService` (Infrastructure, sobre `UserManager`). `UsersController`: `GET /users` (paginación; filtros `isActive`, `departmentId`, `locationId`, `role`; búsqueda por usuario/correo/nombre/código), `GET /users/{id}`, `POST /users` (rol USER por defecto), `PUT /users/{id}`, `POST /users/{id}/deactivate` y `/activate` (revoca refresh tokens; no sobre uno mismo), `PUT /users/{id}/roles` y `/permissions` (exigen ROLES.MANAGE). Siempre queda ≥1 SUPER_ADMIN activo | `Application/**`, `Infrastructure/Services/**`, `Api/Controllers/UsersController.cs` | ADR-016 |
| 2026-10-05 | Fase 3.2 | Roles: `IRoleManagementService` + `RoleManagementService`. `GET/POST /roles`, `GET/PUT/DELETE /roles/{id}`, `PUT /roles/{id}/permissions`, `GET /permissions`. Roles de sistema: no se renombran ni eliminan; permisos de SUPER_ADMIN inmutables; no se elimina un rol con usuarios | `RolesController.cs` | RBAC dinámico verificado por test |
| 2026-10-05 | Fase 3.5 | Auditoría de Identity en `AuditTrailInterceptor` (User, UserRole, UserPermission, Role, RolePermission); ignora Updates sin cambios reales (LastLoginAt/ConcurrencyStamp/...) para no registrar cada login; `EntityId` compuesto `UserId=4;RoleId=3` | `AuditTrailInterceptor.cs` | ADR-015 |
| 2026-10-05 | Fase 3 | `ConflictException` (dominio) → 409 en `ExceptionHandlingMiddleware`. Reglas de contraseña compartidas (`PasswordPolicyRules`) con mensajes en español | `DomainExceptions.cs`, `AuthValidators.cs` | — |
| 2026-10-05 | Fase 3 | **Corrección**: índice único `IX_Users_EmployeeCode` sin filtro (SQL Server solo admite un NULL → el 2.º usuario sin código daba 500). Ahora `[EmployeeCode] IS NOT NULL`. FKs `Users.DepartmentId/LocationId` (Restrict). Migración `UsersOrganizationReferences` aplicada | `IdentityConfigurations.cs`, `Migrations/**` | Verificado en SQL Server |
| 2026-10-05 | Fase 3 | Seeder: los roles de sistema se marcan `IsSystemRole` (antes nunca se asignaba); la matriz por defecto solo se aplica al crear el rol (SUPER_ADMIN siempre recibe todo el catálogo) | `DatabaseSeeder.cs` | ADR-017 |
| 2026-10-05 | Fase 3 | +23 tests funcionales (`UsersEndpointsTests`, `RolesEndpointsTests`); verificado en SQL Server: usuarios sin código de empleado, auditoría de UserRole, sin auditoría por login, `IsSystemRole` en roles existentes | `Tests/Functional/**` | 79/79 |
| 2026-10-05 | Repo | `.gitattributes`: `* text=auto` (LF en el repo, nativo en checkout), `.sh`/Dockerfile LF, `.cmd`/`.ps1`/`.sln(x)` CRLF, binarios. Renormalización sin cambios (el índice ya era LF) | `.gitattributes` | — |
| 2026-10-05 | Fase 4 | Reglas del activo en el dominio: `Asset.AssignTo` / `Return` / `ChangeStatus` / `EnsureCanBeDeleted`, `Status` y `CurrentUserId` con setter privado, `AssetStatusRules` (transiciones permitidas), `AssetAssignment.Close` (historial: un registro cerrado no vuelve a cambiar) | `Domain/Entities/AssetEntities.cs` | ADR-018 |
| 2026-10-05 | Fase 4 | Entidad `AssetMovement` (bitácora inmutable: asignación, devolución, estado, ubicación, departamento; usuario, CorrelationId) + migración `AddAssetMovements` aplicada | `AssetConfigurations.cs`, `Migrations/**` | — |
| 2026-10-05 | Fase 4 | `IAssetService` / `AssetService` en Application (primer servicio de casos de uso; `AddApplication()`), puerto `IUserDirectory`. Endpoints: `GET/POST /assets`, `GET /assets/mine`, `GET/PUT/DELETE /assets/{id}`, `PATCH /assets/{id}/status`, `POST /assets/{id}/assign` y `/return`, `GET /assets/{id}/assignments` y `/movements`, `GET /assignments`, `GET/POST/PUT /asset-types` | `Application/Assets/**`, `Api/Controllers/Asset*.cs` | — |
| 2026-10-05 | Fase 4 | `UnitOfWork.SaveChangesAsync` traduce violaciones de índice único (SQL 2601/2627) a `ConflictException` 409 `DUPLICATE_RECORD`. Verificado: 6 asignaciones concurrentes del mismo activo → 1×200, 5×409, una sola asignación activa | `UnitOfWork.cs` | — |
| 2026-10-05 | Fase 4 | `ValidationExtensions.ValidateAsync` compartido por controllers (antes duplicado) | `Api/Controllers/ValidationExtensions.cs` | — |
| 2026-10-05 | General | **Corrección**: las fechas se devolvían sin `Z` (datetime2 no guarda el Kind → `Unspecified`); el frontend las habría interpretado como hora local (−6 h en Managua). `UtcDateTimeConverter` como convención para todo `DateTime`/`DateTime?` (sin cambio de esquema) | `TIAdminDbContext.cs`, `UtcDateTimeConverter.cs` | ADR-019. Verificado en SQL Server |
| 2026-10-05 | Fase 4 | +37 tests: `AssetTests` (dominio, sin BD) y `AssetsEndpointsTests` (ciclo de vida completo con historial, estados, movimientos, permisos, tipos). Verificado en SQL Server: proyecciones con subconsultas, historial de activos eliminados, serial reservado | `Tests/**` | 116/116 |
| 2026-10-05 | Fase 4 | **Q-10 resuelto**: el rol USER pierde `ASSETS.VIEW` (solo `GET /assets/mine`). Matriz en `Permissions.ForRole` + migración de datos `RemoveAssetsViewFromUserRole` para bases existentes (aplicada; verificado: USER → 403 en `/assets`, 200 en `/assets/mine`) | `Permissions.cs`, `Migrations/**`, tests | ADR-020. 117/117 |
| 2026-10-05 | Fase 5 | Dominio: `Vendor`, `Contract` (ciclo de vida + `ContractStatusRules`), `Software`, `SoftwareLicense` (`Install`/`Uninstall`/`SetQuantity`), `SoftwareInstallation`; `AuditAction.SensitiveRead` | `Domain/Entities/VendorEntities.cs`, `SoftwareEntities.cs` | ADR-022, ADR-023 |
| 2026-10-05 | Fase 5 | Servicios `VendorService`, `ContractService`, `SoftwareService`, `LicenseService`; puertos `ISecretProtector` (Data Protection), `ISystemSettings`, `IAuditLogger`. Endpoints `/vendors`, `/contracts` (+activate/terminate/renew), `/software`, `/licenses` (+key, installations), `/alerts/licenses`, `/alerts/contracts`. Config `Alerts.License.LowUtilizationPercent` (seed) y `DataProtection:KeysPath` | `Application/{Vendors,Licensing}/**`, `Api/Controllers/**` | ADR-021 |
| 2026-10-05 | Fase 5 | Migración `AddVendorsContractsAndLicensing` aplicada (5 tablas, 5 CHECK, rowversion en licencias, FK `Assets.VendorId`) | `Migrations/**` | — |
| 2026-10-05 | General | **Corrección**: `IClock.Today` devolvía la fecha UTC; ahora es la fecha en `App:TimeZone` | `SystemClock.cs` | ADR-024 |
| 2026-10-05 | General | **Trampas de EF Core detectadas** (regresión atrapada por tests): `IgnoreQueryFilters()` dentro de una subconsulta desactiva los filtros de toda la consulta (mostraba activos eliminados); navegar a un principal requerido con soft delete (`c.Vendor.Name`) filtra a los dependientes. Solución: `VendorNameLookup` en consulta aparte | `Repositories/**` | Documentado en CLAUDE.md |
| 2026-10-05 | Fase 5 | Módulo de auditoría: `SoftwareLicense`/`SoftwareInstallation` → "Licenses" (antes "Software") | `AuditTrailInterceptor.cs` | — |
| 2026-10-05 | Seguridad | Q-09 resuelta (ADR-021 aprobado): `DataProtection:CertificateThumbprint` cifra el anillo de claves; la API no arranca si el certificado no existe. Verificado con certificado autofirmado temporal. Checklist de Fase 12 actualizado | `ServiceCollectionExtensions.cs`, `IMPLEMENTATION_PLAN.md` | Commit `78b0613` |
| 2026-10-05 | Fase 6 | Dominio: `Ticket` (flujo, asignación, aprobación, SLA), `TicketStatusRules`, `TicketCategory`, `SlaPolicy` + `SlaPolicySelector`, `TicketComment`, `TicketStatusHistory`, `BusinessHours` (cálculo laboral en la zona de la organización); `TicketType` | `Domain/Entities/HelpDeskEntities.cs`, `Domain/Services/BusinessHours.cs` | ADR-025, ADR-027 |
| 2026-10-05 | Fase 6 | `TicketService`, `HelpDeskConfigService`; endpoints `/tickets` (+status, assign, approve, reject, comments, history), `/ticket-categories`, `/sla-policies`; `IClock.TimeZone`; `ISystemSettings.GetStringAsync`; seed de categorías, SLA y `Tickets.NumberPrefix` | `Application/HelpDesk/**`, `Api/Controllers/TicketsController.cs` | ADR-026, ADR-028, ADR-029 |
| 2026-10-05 | Fase 6 | Migración `AddHelpDesk` aplicada (5 tablas, 3 CHECK, índice único filtrado de SLA predeterminado, rowversion en tickets) + datos: USER pierde TICKETS.VIEW y REQUESTS.VIEW | `Migrations/**` | Verificado en SQL Server |
| 2026-10-05 | Fase 6 | +32 tests (24 unidad: horario laboral, selección de SLA, flujo de tickets; 8 funcionales). Verificado en SQL Server: numeración, alcance por usuario, filtro de vencidos, vencimientos correctos en hora de Managua | `Tests/**` | 183/183 |
| 2026-10-05 | Fase 7 | Dominio: `Maintenance`, `ChangeRequest`, `PurchaseRequest` + `PurchaseItem` (transiciones, segregación de funciones, totales). `DocumentNumbers` (numeración compartida con tickets) y `AssetMovementLog` (bitácora de movimientos compartida con `AssetService`) | `Domain/Entities/OperationsEntities.cs`, `Application/Common/**` | ADR-030, ADR-031 |
| 2026-10-05 | Fase 7 | `MaintenanceService`, `ChangeService`, `PurchaseService`; endpoints `/maintenances`, `/changes`, `/purchases`, `/alerts/maintenance`; prefijos `Maintenance/Changes/Purchases.NumberPrefix` (seed); rowversion en las tres entidades (aprobaciones concurrentes) | `Application/Operations/**`, `Api/Controllers/OperationsControllers.cs` | — |
| 2026-10-05 | Fase 7 | **Corrección**: módulo de auditoría de `ChangeRequest`/`PurchaseRequest` caía en "Requests" (se evaluaba "Request" antes que "Change"/"Purchase") | `AuditTrailInterceptor.cs` | — |
| 2026-10-05 | Fase 7 | Migración `AddMaintenanceChangesPurchases` aplicada (4 tablas, 5 CHECK). +19 tests (12 dominio + 7 funcionales). Verificado en SQL Server: ciclo de mantenimiento con estado del activo, alerta preventiva, totales de compra, cambio estándar preaprobado | `Migrations/**`, `Tests/**` | 202/202 |
| 2026-10-05 | Fase 8 | `IReportingQueries` (agregados en SQL, nombres por lookup) + `DashboardService`, `ReportService`, `AuditQueryService`, `ConfigurationService`; endpoints `/audit`, `/dashboard/summary`, `/reports/*`, `/configuration` | `Application/{Reporting,Administration}/**`, `Infrastructure/Reporting/**`, `Api/Controllers/AdministrationControllers.cs` | ADR-032, ADR-033 |
| 2026-10-05 | Fase 8 | `ISystemSettings` descifra parámetros `Encrypted`; el seed deja `Value` nulo para claves cifradas (el por defecto aplica hasta editarse) | `PlatformServices.cs`, `DatabaseSeeder.cs` | — |
| 2026-10-05 | Fase 8 | +5 tests funcionales (auditoría, dashboard por permisos, reportes, configuración con efecto real en la numeración, parámetro cifrado). Verificado en SQL Server: 11 endpoints, sin excepciones; dashboard 80 ms | `Tests/Functional/AdministrationEndpointsTests.cs` | 207/207. Sin cambios de esquema |
| 2026-10-05 | Fase 5 | +34 tests (22 dominio + 12 funcionales). Verificado en SQL Server: filtros de estado efectivo, alertas, clave cifrada (`CfDJ8…`) sin fugas en auditoría, CHECK `UsedQuantity <= Quantity`, carrera sobre el último puesto | `Tests/**` | 151/151 |
| 2026-10-05 | Fase 3 | **Verificado contra SQL Server**: login, 401 JSON sin token, CRUD departamentos (padre inexistente 400, ciclo 400, borrar con hijos 409, paginación/orden, soft delete + AuditLog Delete, código reservado 400), `GET /locations` paginado | — | Smoke test manual con curl |

---

## 4. Progreso por Fases

### Fase 0 - Preparación y Fundación
**Estado:** ✅ Completada | **Tareas:** 4/4

- [x] **0.1 Repositorio y branching** - Verificar estado Git, ramas, convenciones, CONTRIBUTING/README
- [x] **0.2 Entorno de desarrollo** - `.gitignore`, `docker-compose.yml`, `.env.example` creados. Bloqueante: falta instalar .NET 10 SDK y arrancar Docker Desktop
- [x] **0.3 Documentación y reglas** - `CONVENTIONS.md`, `ARCHITECTURE.md` creados; backlog en `IMPLEMENTATION_PLAN.md`
- [x] **0.4 Análisis y modelado** - `ERD.md` creado con ~30 entidades, índices y cardinalidades

**Entregables completados:** Git inicializado con `main`/`develop`, README, CONTRIBUTING, CONVENTIONS, ARCHITECTURE, ERD, docker-compose, .env.example.

### Fase 1 - Infraestructura Base (Solución, Capas, Persistencia)
**Estado:** ✅ Completada | **Tareas:** 8/8

- [x] 1.1 Solución `TIAdmin.slnx` + 5 proyectos con referencias cruzadas
- [x] 1.2 Dependencias NuGet (EF Core 10, Identity, MediatR, FluentValidation, Mapster, Serilog, Swashbuckle)
- [x] 1.3 Options Pattern + `appsettings.json` / `appsettings.Development.json` (sin secretos)
- [x] 1.4 Domain: contratos base, clases base, enums, excepciones, puertos (`IClock`, `ICurrentUserService`)
- [x] 1.5 `TIAdminDbContext` + Fluent API + query filter soft delete + `AuditSaveChangesInterceptor`
- [x] 1.6 Seed idempotente: 54 permisos, 7 roles, 200 asignaciones, 12 tipos de activo, 9 configuraciones, admin
- [x] 1.7 Migración `InitialCreate` aplicada (15 tablas, sin cambios pendientes)
- [x] 1.8 Verificación: 28/28 tests verdes, health checks y OpenAPI responden 200

**Entregables completados:** API .NET 10 arranque con Serilog, Swagger, Identity, health checks y base de datos SQL Server 2022 migrada y sembrada.

### Fase 2 - Autenticación, Autorización y Seguridad
**Estado:** ✅ Completada | **Tareas:** 5/5 (commit `ad3b641`)

- [x] 2.1 Endpoints de login / refresh / logout / me / change-password + JWT con claims de permisos
- [x] 2.2 `PermissionAuthorizationHandler` + políticas por permiso (fallback: usuario autenticado)
- [x] 2.3 Middleware de correlation ID + manejo global de errores con `traceId`
- [x] 2.4 Rate limiting (global + `login`), CORS fail-closed, headers de seguridad
- [x] 2.5 Entidad `AuditLog` + `AuditTrailInterceptor`

### Fase 3 - Núcleo Organizacional
**Estado:** ✅ Completada | **Tareas:** 5/5

- [x] 3.1 Usuarios: CRUD (desactivar en lugar de eliminar), paginación/filtros, roles y permisos directos
- [x] 3.2 Roles: CRUD, matriz de permisos, catálogo `/permissions` (RBAC dinámico)
- [x] 3.3 Departamentos: CRUD, paginación/búsqueda, código único, jerarquía sin ciclos, soft delete
- [x] 3.4 Ubicaciones: CRUD, paginación/búsqueda, código único, soft delete
- [x] 3.5 Auditoría de usuarios/roles/permisos (vía `AuditTrailInterceptor`)
- [x] Tests funcionales de endpoints (`WebApplicationFactory`)

Pendientes conocidos (no bloquean):
- Los cambios de roles/permisos aplican al renovar el access token (≤ `Jwt:AccessTokenMinutes`); un usuario desactivado conserva su access token vigente hasta que expira
- Sin endpoint de restablecimiento de contraseña por administrador
- Tests de integración contra SQL Server real (Testcontainers)

### Fase 4 - Activos TI y Asignaciones
**Estado:** ✅ Completada | **Tareas:** 5/6

- [x] 4.1 Catálogo de tipos de activo (`/asset-types`; se desactivan, no se eliminan; seed de 12 tipos)
- [x] 4.2 Activos: CRUD, búsqueda/filtros (estado, tipo, depto, ubicación, usuario, garantía), códigos/serial únicos, baja lógica, jerarquía padre-hijo sin ciclos
- [x] 4.3 Asignaciones con historial inmutable; `Asset.Status`/`CurrentUserId` coherentes con la asignación activa; índice único filtrado como respaldo ante concurrencia
- [x] 4.4 `AssetMovement`: asignación, devolución, cambio de estado, ubicación y departamento
- [x] 4.5 Endpoints y permisos ASSETS.* / ASSIGNMENTS.VIEW / ASSET_TYPES.MANAGE
- [ ] 4.6 Relación con Documents → Fase 10

Pendientes / decisiones abiertas:
- ~~Q-10~~ resuelto (ADR-020): USER ya no tiene `ASSETS.VIEW`; consulta solo sus activos con `GET /assets/mine` (sin acceso al detalle `GET /assets/{id}`)
- Desactivar un usuario con activos asignados no está bloqueado ni avisa
- `VendorId` en `Assets` sin FK hasta que exista `Vendors` (Fase 5)

### Fase 5 - Software/Licenciamiento/Proveedores/Contratos
**Estado:** ✅ Completada | **Tareas:** 5/5

- [x] 5.1 Software: catálogo (nombre + versión únicos), resumen de puestos por producto, baja lógica bloqueada si tiene licencias
- [x] 5.2 Proveedores: CRUD, nombre único entre no eliminados, estado (Active/Inactive/Blocked), baja lógica bloqueada con contratos abiertos; FK `Assets.VendorId`
- [x] 5.3 Licencias: clave cifrada (Data Protection) y revelada solo con LICENSES.MANAGE + auditoría `SensitiveRead`; instalaciones licencia↔activo que calculan `UsedQuantity`; sin sobreasignación (dominio + CHECK + rowversion)
- [x] 5.4 Contratos: Draft → Active → Terminated/Renewed; Expiring/Expired derivados de las fechas; renovación crea un contrato sucesor
- [x] 5.5 Alertas (`/alerts/licenses`, `/alerts/contracts`) con umbrales de `SystemConfigurations`

Pendientes / decisiones abiertas:
- Q-09 resuelta (ADR-021). **Producción**: `DataProtection:KeysPath` (persistente, compartido entre instancias) y `DataProtection:CertificateThumbprint`; respaldar anillo + certificado aparte de la BD (checklist Fase 12)
- Notificación activa de alertas (email/internas) y scheduler → Fase 10
- Documentos/evidencia de licencias y contratos → Fase 10

### Fase 6 - Help Desk/Solicitudes/SLA
**Estado:** ✅ Completada | **Tareas:** 5/5

- [x] 6.1 Categorías (`/ticket-categories`): tipo (incidente/solicitud), prioridad por defecto, aprobación; seed de 11 categorías
- [x] 6.2 Tickets (`/tickets`): número `TKT-AAAA-000001`, flujo de estados (SPECS §21.4 + cancelación y reapertura), asignación, comentarios públicos/internos, historial inmutable, concurrencia optimista (rowversion)
- [x] 6.3 Solicitudes de servicio como tipo de ticket (Q-07 / ADR-025) con aprobación (`/approve`, `/reject`, REQUESTS.APPROVE)
- [x] 6.4 SLA (`/sla-policies`): política más específica (categoría > prioridad > tipo > departamento), horario laboral en `App:TimeZone`, seed según SPECS §22, una sola predeterminada (índice filtrado)
- [x] 6.5 Detección de vencidos: estado del SLA calculado en cada consulta, filtro `overdue=true`, `IsSlaBreached` definitivo al resolver

Notas sobre el ERD: `ServiceRequests` y `RequestTypes` no se crearon (Q-07: entidad única, la categoría cumple el papel del tipo de solicitud); la tabla de SLA se llama `SlaPolicies` (el ERD la nombraba `SlaConfigurations`).

Pendientes / decisiones abiertas:
- Feriados (`BusinessCalendars`) no implementados: el cálculo laboral solo considera días y horas de la política
- El reloj del SLA no se pausa en `WaitingUser` / `WaitingVendor` (SPECS no lo exige; decidir si se requiere)
- `REQUESTS.MANAGE` queda sin uso: el flujo de ambos tipos usa TICKETS.*
- Notificaciones (ticket creado/asignado/vencido) y adjuntos → Fase 10

### Fase 7 - Mantenimientos/Cambios/Compras
**Estado:** ✅ Completada | **Tareas:** 4/4

- [x] 7.1 Mantenimientos (`/maintenances`): Planned → Scheduled → InProgress → Completed/Cancelled; vinculados a activo, técnico (requiere MAINTENANCE.MANAGE), proveedor, ticket y contrato; el activo disponible pasa a Maintenance al iniciar y vuelve a Available al terminar (con movimiento registrado); alertas `/alerts/maintenance` (próximos, vencidos, preventivos por `NextDueDate`)
- [x] 7.2 Cambios (`/changes`): flujo completo de SPECS §27; plan de rollback obligatorio salvo cambios estándar (preaprobados); sin autoaprobación; implementa el responsable asignado o CHANGES.MANAGE; cada transición queda en AuditLog
- [x] 7.3 Compras (`/purchases`): partidas con totales calculados, borrador editable, sin autoaprobación, ordenar exige proveedor, PURCHASES.MANAGE ordena/recibe
- [x] 7.4 Contratos (refuerzo): mantenimientos vinculados a contrato con validación de proveedor; alertas periódicas por scheduler → Fase 10

Pendientes / decisiones abiertas:
- Recibir una compra no crea activos automáticamente (las partidas tienen `AssetTypeId` para hacerlo en el futuro)
- Completar un preventivo no crea el siguiente automáticamente: `NextDueDate` alimenta la alerta `PreventiveDue`
- TI_MANAGER revisa y administra cambios pero no puede crearlos (no tiene CHANGES.CREATE en la matriz); confirmar si es intencional

### Fase 8 - Auditoría/Reportes/Dashboard/Configuración
**Estado:** ✅ Completada | **Tareas:** 4/4

- [x] 8.1 Auditoría (`/audit`, `/audit/{id}`): solo lectura con filtros (usuario, acción, módulo, entidad, IP, correlation id, rango), AUDIT.VIEW; sin escritura vía API
- [x] 8.2 Reportes JSON (`/reports/assets/summary`, `/assets/by-user`, `/tickets`, `/sla`, `/licenses`, `/costs`), REPORTS.VIEW; periodos con fechas de negocio inclusivas en `App:TimeZone` (máx. 731 días)
- [x] 8.3 Dashboard (`/dashboard/summary`): KPIs de SPECS §28 por secciones según permisos + sección "mis datos" (ADR-032). Verificado en SQL Server: 80 ms en caliente (objetivo < 2 s)
- [x] 8.4 Configuración (`/configuration`, `/configuration/public` anónimo, `PUT /{key}`, `POST /{key}/reset`): validación por tipo y por clave con normalización, cifrado de valores `Encrypted` (enmascarados al leer, descifrados por `ISystemSettings`), claves gestionadas por despliegue de solo lectura (ADR-033)

Pendientes / decisiones abiertas:
- Exportación Excel/CSV/PDF y procesamiento asíncrono de reportes grandes (REPORTS.EXPORT) → Fase 10
- Sin caché del dashboard (no fue necesaria con los volúmenes actuales; evaluar con datos reales)
- Reportes de historial de movimientos y de auditoría se cubren con `/assets/{id}/movements` y `/audit`

### Fase 9 - Frontend Angular (Core + Módulos)
**Estado:** Pending

### Fase 10 - Integraciones/Notificaciones/Archivos/Caché/Import-Export
**Estado:** Pending

### Fase 11 - Calidad/Pruebas/CI-CD/Observabilidad
**Estado:** Pending

### Fase 12 - Despliegue/Seguridad Final/Go-Live/Documentación
**Estado:** Pending

---

## 5. Decisiones Técnicas (ADRs)

| ID | Fecha | Decisión | Justificación | Estado |
|---|---|---|---|---|
| ADR-001 | 2026-10-02 | Usar Modular Monolith + Clean Architecture | Según SPECS.md, evita complejidad de microservicios inicialmente | Aprobado (Specs) |
| ADR-002 | 2026-10-02 | Ramas: `main` (estable) y `develop` (integración) | Alineado con SPECS.md sección 51; `master` renombrado a `main` | Aprobado |
| ADR-003 | 2026-10-02 | Commits con Conventional Commits | Legibilidad, trazabilidad y generación de changelog | Aprobado |
| ADR-004 | 2026-10-02 | Soft delete global vía EF Core query filter + `ISoftDeletable` | SPECS.md §37 exige preservar histórico; centraliza el comportamiento | Aprobado |
| ADR-005 | 2026-10-02 | Fechas persistidas en UTC (`datetime2`), conversión a TZ en frontend | SPECS.md §38; evita ambigüedad y permite operación multi-zona | Aprobado |
| ADR-006 | 2026-10-02 | Archivos binarios fuera de SQL Server (solo metadata) | SPECS.md §39; evita crecimiento de BD y permite CDN/object storage | Aprobado |
| ADR-007 | 2026-10-02 | Organización del código por feature (vertical slices) en `Application` | SPECS.md §9; alta cohesión, evita carpetas técnicas gigantes | Aprobado |
| ADR-008 | 2026-10-02 | `.env.example` + `.gitignore` para secretos; Docker Compose solo para SQL Server y Redis | SPECS.md §6.7/§17/§54 | Aprobado |
| ADR-009 | 2026-10-02 | Nombres de tabla explícitos y en plural en cada configuración Fluent API | Evita la divergencia `Department`/`Users` que introdujo la singularización automática; SPECS.md §62 usa plural | Aprobado |
| ADR-010 | 2026-10-02 | `PermissionDefinition.Code` se deriva de `Module`+`Action`, no se escribe a mano | Impide que el código y el módulo/acción diverjan (bug detectado por test) | Aprobado |
| ADR-011 | 2026-10-02 | `TIAdminDbContextFactory` (design-time) en vez de arrancar la API para migrar | Evita exigir secretos JWT y BD viva al ejecutar `dotnet ef` | Aprobado |
| ADR-012 | 2026-10-02 | Secretos de desarrollo en `dotnet user-secrets` (fuera del repo) | SPECS.md §17/§53; `appsettings.json` queda sin valores sensibles | Aprobado |
| ADR-013 | 2026-10-05 | Los códigos únicos (`Departments.Code`, `Locations.Code`) quedan reservados aunque el registro se elimine (soft delete) | El índice único cubre eliminados; conserva trazabilidad histórica y evita 500 por violación de índice | Propuesto |
| ADR-014 | 2026-10-05 | FKs hacia `Users` declaradas solo en Fluent API (`HasOne<ApplicationUser>()`), el dominio guarda ids `int` | Domain no depende de Identity; integridad referencial real (CONVENTIONS §2.2) | Propuesto |
| ADR-015 | 2026-10-05 | Auditar las entidades de Identity (usuarios, roles, asignaciones) en `AuditTrailInterceptor`, no endpoint por endpoint | Un único punto; cubre también cambios hechos fuera de los controllers. Se filtran Updates sin cambios reales | Propuesto |
| ADR-016 | 2026-10-05 | Los usuarios no se eliminan: se desactivan (`USERS.DISABLE`) y se revocan sus refresh tokens. Asignar roles/permisos exige `ROLES.MANAGE` | Conserva historial (asignaciones, tickets, auditoría); asignar roles es escalar privilegios | Propuesto |
| ADR-018 | 2026-10-05 | Reglas de estado del activo en la entidad de dominio (`Asset.AssignTo/Return/ChangeStatus`, setters privados); orquestación en un servicio de Application (`AssetService`) | Clean Architecture (CONVENTIONS §1.3): reglas testeables sin BD; los controllers quedan delgados. Patrón para flujos con varias entidades | Propuesto |
| ADR-019 | 2026-10-05 | Convención EF `UtcDateTimeConverter` para todo `DateTime`: se lee con `Kind=Utc` | ADR-005; JSON con `Z` para que el frontend convierta a la zona configurada | Propuesto |
| ADR-032 | 2026-10-05 | Dashboard sensible a permisos: cada sección (activos, tickets, licencias, contratos, mantenimiento, costos) requiere el permiso de su módulo (costos: REPORTS.VIEW); todos ven "mis datos" | DASHBOARD.VIEW lo tiene el rol USER; no debe revelar lo que Q-10 / ADR-026 ocultan | Propuesto |
| ADR-033 | 2026-10-05 | `App.TimeZone`, `App.PageSize`, `App.MaxPageSize` son de solo lectura vía API: su valor efectivo viene de appsettings (`AppOptions`, `PagedQuery`). `/configuration/public` devuelve la zona efectiva | Evita una segunda fuente de verdad que no surtiría efecto | Propuesto |
| ADR-030 | 2026-10-05 | Segregación de funciones: quien solicita un cambio o una compra no puede aprobarlo ni rechazarlo (también con permisos de revisión). Cambios estándar quedan preaprobados al enviarse (ITIL) | Control interno; evita autoaprobación | Propuesto |
| ADR-031 | 2026-10-05 | Mantenimiento ↔ estado del activo: iniciar sobre un activo Available lo pasa a Maintenance; completar/cancelar lo devuelve a Available si no hay otro en curso; un activo asignado conserva su estado. Ambos cambios se registran en `AssetMovements` | "Activos en mantenimiento" consistente sin edición manual; preventivo en sitio no desasigna | Propuesto |
| ADR-026 | 2026-10-05 | Visibilidad de tickets: TICKETS.VIEW / REQUESTS.VIEW = ver todos los incidentes / solicitudes; el solicitante siempre ve los suyos (sin comentarios internos); un ticket ajeno responde 404. El rol USER pierde ambos permisos (migración en `AddHelpDesk`) | Mismo principio que Q-10 (privacidad del usuario final) sin agregar permisos al catálogo | Propuesto |
| ADR-027 | 2026-10-05 | SLA: política más específica gana; vencimientos en horario laboral calculados en el dominio (`BusinessHours`) en `App:TimeZone`; en solicitudes con aprobación el reloj arranca al aprobar; el estado del SLA se calcula en cada consulta y `IsSlaBreached` se fija al resolver | Configurable sin código (SPECS §22); sin job para marcar vencidos | Propuesto |
| ADR-028 | 2026-10-05 | Número de ticket `PREFIJO-AAAA-{Id:000000}` asignado tras insertar (prefijo en `Tickets.NumberPrefix`) | Único y sin carreras sin depender de secuencias de SQL Server (los tests usan InMemory); no reinicia por año | Propuesto |
| ADR-029 | 2026-10-05 | Categorías de ticket y políticas de SLA se administran con CONFIGURATION.MANAGE; el flujo de incidentes y solicitudes usa TICKETS.UPDATE/ASSIGN/RESOLVE/CLOSE | Evita agregar permisos (requeriría migraciones de datos por rol, ADR-017) | Propuesto |
| ADR-025 | 2026-10-05 | Tickets y solicitudes de servicio son una sola entidad (`Ticket` con un tipo: incidente / solicitud), con flujo, SLA, comentarios y numeracion compartidos | Decision de negocio (Q-07); evita duplicar flujo de estados, SLA y reportes | Aprobado |
| ADR-021 | 2026-10-05 | `LicenseKey` cifrada con ASP.NET Core Data Protection detrás de `ISecretProtector`; nunca se proyecta en listados/detalle; `GET /licenses/{id}/key` exige LICENSES.MANAGE, responde `no-store` y audita `SensitiveRead` | Q-09 resuelta: misma protección que Always Encrypted frente a fuga de backups, sin su costo operativo ni romper los tests. Producción: `DataProtection:KeysPath` + `DataProtection:CertificateThumbprint` (anillo cifrado con certificado), respaldo del anillo y certificado aparte de la BD | Aprobado |
| ADR-022 | 2026-10-05 | `UsedQuantity` se deriva de instalaciones licencia↔activo (tabla opcional del ERD); sin sobreasignación garantizada por dominio + CHECK + rowversion en `SoftwareLicenses`. La alerta "sobreasignación" se reporta como `Exhausted` (sin puestos libres) | Uso real trazable por activo; el criterio del plan prohíbe sobreasignar. Verificado: 6 instalaciones concurrentes sobre 1 puesto → 1×200, 5×409 | Propuesto |
| ADR-023 | 2026-10-05 | Contratos: solo se persisten Draft/Active/Terminated/Renewed; Expiring/Expired se derivan de `EndDate` y `RenewalNoticeDays`. Renovar crea un contrato sucesor (`RenewedFromContractId`) | Un estado derivado de fechas no queda desactualizado sin un job; el historial del contrato original se conserva | Propuesto |
| ADR-024 | 2026-10-05 | `IClock.Today` es la fecha de negocio en `App:TimeZone` (America/Managua), no la fecha UTC | Los vencimientos (`DateOnly`) no deben cambiar a las 18:00 hora local | Propuesto |
| ADR-020 | 2026-10-05 | El rol USER no ve el inventario (`ASSETS.VIEW` retirado); solo sus activos asignados vía `GET /assets/mine`. Bases existentes: migración de datos `RemoveAssetsViewFromUserRole` | Decisión de negocio (Q-10). Por ADR-017, los cambios de matriz para roles existentes requieren migración de datos | Aprobado |
| ADR-017 | 2026-10-05 | El seed aplica la matriz de permisos por defecto solo al crear un rol de sistema; SUPER_ADMIN siempre recibe el catálogo completo | Los cambios hechos vía `/roles` no deben revertirse al reiniciar; permisos nuevos del catálogo llegan a SUPER_ADMIN y se asignan al resto vía API | Propuesto |

---

## 6. Comandos Útiles

### Backend (.NET)
```powershell
dotnet build .\TIAdmin.slnx
dotnet test .\TIAdmin.slnx
dotnet ef migrations add <Name> --project src/TIAdmin.Infrastructure --startup-project src/TIAdmin.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/TIAdmin.Infrastructure --startup-project src/TIAdmin.Api
dotnet run --project src/TIAdmin.Api
```

### Frontend (Angular)
```powershell
npm start
npm run build
npm run lint
npm run test
```

### Docker
```powershell
docker compose up -d
docker compose down
```

### Git
```powershell
git status
git branch -a
git log --oneline -5
```

---

## 7. Bloqueantes / Preguntas Abiertas

### Bloqueantes técnicos

| ID | Descripción | Impacto | Resolución requerida |
|---|---|---|---|
| B-01 | ~~.NET 10 SDK no instalado~~ | — | ✅ RESUELTO: SDK 10.0.401 instalado |
| B-02 | ~~Docker Desktop daemon apagado~~ | — | ✅ RESUELTO: Engine 29.7.2 activo |
| B-03 | ~~Docker Desktop daemon apagado (2026-10-05)~~ | — | ✅ RESUELTO: migración aplicada y endpoints verificados |

### Decisiones pendientes

| ID | Pregunta | Propietario |
|---|---|---|
| Q-01 | ¿AutoMapper o Mapster? | Equipo técnico |
| Q-02 | ¿Qué librería UI de Angular? | Equipo frontend |
| Q-06 | ¿Enums persistidos como `int` o `string`? | Equipo técnico |
| ~~Q-07~~ | ~~¿`Tickets` y `ServiceRequests` unificados?~~ → Sí, una sola entidad (ADR-025) | Resuelto 2026-10-05 |
| ~~Q-09~~ | ~~¿Data Protection o Always Encrypted?~~ → Data Protection con anillo protegido por certificado (ADR-021) | Resuelto 2026-10-05 |
| ~~Q-10~~ | ~~¿El rol USER debe ver todo el inventario?~~ → No, solo sus activos asignados (ADR-020) | Resuelto 2026-10-05 |

---

## 8. Próximos Pasos

1. **Fase 9**: Frontend Angular 22 (`ti-admin-web`): core (auth con JWT + refresh, interceptores, guards por permiso), layout con menú dinámico, componentes compartidos y módulos funcionales sobre la API ya disponible
2. Alternativa antes del frontend: Fase 10 (exportación, notificaciones, archivos, scheduler) y Fase 11 (CI/CD, Testcontainers) completarían el backend
3. Actualizar esta memoria tras cada cambio significativo

### Comandos de arranque (dev)

```powershell
docker compose up -d sqlserver
$env:PATH = "C:\Program Files\dotnet;$env:PATH"
dotnet build .\TIAdmin.slnx
dotnet run --project src/TIAdmin.Api      # http://localhost:5142 (launchSettings)
```

Secretos ya cargados en user-secrets del proyecto `TIAdmin.Api`:
`ConnectionStrings:DefaultConnection`, `Jwt:*`, `Seed:Enabled`, `Seed:AdminPassword`.

Admin de desarrollo: `admin` / `Admin123!Local`