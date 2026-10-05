# DEVELOPMENT MEMORY - TI Admin

**Proyecto:** Sistema Administrativo de Tecnologías de Información (TI Admin)
**Referencia:** `docs/SPECS.md`, `docs/IMPLEMENTATION_PLAN.md`
**Fecha de inicio:** 2026-10-02
**Última actualización:** 2026-10-05
**Estado general:** En progreso
**Fase actual:** Fase 3 - Núcleo Organizacional (Fase 2 completada; adelanto de entidades de Fase 4)

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
| Fase actual | **Fase 3 - Núcleo Organizacional** |
| Fases completadas | 3 / 12 (Fase 0, 1, 2) |
| Tareas completadas (Fase 3) | 2 / 5 (Departamentos, Ubicaciones) |
| Tests | 56 / 56 (35 unit + 21 functional) |
| Estado | En progreso |

### Progreso global

| Fase | Estado |
|---|---|
| 0 - Preparación y Fundación | ✅ Completada |
| 1 - Infraestructura Base | ✅ Completada |
| 2 - Auth/Authz/Seguridad | ✅ Completada |
| 3 - Núcleo Organizacional | 🟡 En progreso |
| 4 - Activos y Asignaciones | 🟡 Entidades y migración creadas (sin endpoints) |
| 5 - Software/Licencias/Proveedores/Contratos | ⬜ Pendiente |
| 6 - Help Desk/Solicitudes/SLA | ⬜ Pendiente |
| 7 - Mantenimientos/Cambios/Compras | ⬜ Pendiente |
| 8 - Auditoría/Reportes/Dashboard/Config | ⬜ Pendiente |
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
**Estado:** 🟡 En progreso | **Tareas:** 2/5

- [x] 3.3 Departamentos: CRUD, paginación/búsqueda, código único, jerarquía sin ciclos, soft delete
- [x] 3.4 Ubicaciones: CRUD, paginación/búsqueda, código único, soft delete
- [ ] 3.1 Usuarios: CRUD `/api/v1/users` (paginación, filtros activo/depto/ubicación), asignación de roles y permisos directos. `ApplicationUser` necesita `DepartmentId`/`LocationId`/`EmployeeCode` (revisar `IdentityEntities.cs`)
- [ ] 3.2 Roles: CRUD `/api/v1/roles`, asignar/desasignar permisos, listar permisos disponibles (RBAC dinámico)
- [ ] 3.5 Auditoría explícita en cambios de usuarios/roles/permisos (las entidades de Identity no pasan por `AuditTrailInterceptor`)
- [x] Tests funcionales de endpoints (`WebApplicationFactory`): auth, seguridad, departamentos, ubicaciones
- [ ] Tests de integración contra SQL Server real (Testcontainers): índices filtrados, `IgnoreQueryFilters`, Ids identity

### Fase 4 - Activos TI y Asignaciones
**Estado:** 🟡 Adelantada parcialmente

- [x] Entidades `Asset` / `AssetAssignment`, configuración Fluent API, repositorios, migración `AddAssetsAndAssignments` (aplicada)
- [ ] Endpoints `/assets`, `/asset-types`, `/assignments`; flujo asignar/desasignar con historial inmutable; `AssetMovement`

### Fase 5 - Software/Licenciamiento/Proveedores/Contratos
**Estado:** Pending

### Fase 6 - Help Desk/Solicitudes/SLA
**Estado:** Pending

### Fase 7 - Mantenimientos/Cambios/Compras
**Estado:** Pending

### Fase 8 - Auditoría/Reportes/Dashboard/Configuración
**Estado:** Pending

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
| Q-07 | ¿`Tickets` y `ServiceRequests` unificados? | Negocio/TI |
| Q-09 | ¿Cifrado de `LicenseKey` con Data Protection o SQL Always Encrypted? | Seguridad |

---

## 8. Próximos Pasos

1. **Fase 3.1**: Usuarios (CRUD + roles/permisos directos)
2. **Fase 3.2**: Roles (CRUD + asignación de permisos)
3. **Fase 3.5**: auditoría explícita de cambios en Identity + tests funcionales de usuarios/roles
4. Tests de integración con Testcontainers (SQL Server real)
5. Actualizar esta memoria tras cada cambio significativo

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