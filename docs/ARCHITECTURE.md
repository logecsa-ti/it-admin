# Arquitectura - TI Admin

Referencia: `docs/SPECS.md` (secciones 4-12).

---

## 1. Estilo arquitectónico

**Modular Monolith con Clean Architecture.**

Se evita microservicios en la etapa inicial por complejidad operacional (despliegue, red, transacciones distribuidas, observabilidad) que no aporta valor al problema de negocio actual. La separación en módulos y capas permite extraer microservicios en el futuro sin reescritura.

---

## 2. Vista de contextos

```text
┌─────────────────────┐
│      Usuario        │
└──────────┬──────────┘
           │ HTTPS
           ▼
┌─────────────────────┐
│  Angular 22 SPA     │  (Core / Shared / Layout / Features)
└──────────┬──────────┘
           │ REST / JSON (JWT Bearer)
           ▼
┌─────────────────────┐
│  TIAdmin.Api        │  HTTP, routing, authn, authz, errores, OpenAPI
└──────────┬──────────┘
           │
   ┌───────┼────────────────┬──────────────────┐
   ▼       ▼                ▼                  ▼
┌────────┐ ┌──────────┐ ┌──────────┐   ┌────────────┐
│Application│ Identity │ │  Modules │   │  Integrations│
│ (Features) │ Security │ │   TI    │   │  (Email,    │
└────┬────┘ └──────────┘ └────┬─────┘   │   Storage)  │
     │                        │         └────────────┘
     └───────────┬────────────┘
                 ▼
        ┌─────────────────┐
        │  TIAdmin.Domain │  Entidades, reglas, eventos (sin deps)
        └────────┬────────┘
                 ▼
        ┌─────────────────────┐
        │ TIAdmin.Infrastructure│ EF Core, Identity, repos, servicios
        └──────────┬──────────┘
                   ▼
        ┌─────────────────────┐
        │   SQL Server 2022   │
        └─────────────────────┘
```

---

## 3. Capas y responsabilidades

### 3.1 `TIAdmin.Domain` (núcleo)

- Entidades, value objects, enums, eventos de dominio, excepciones de negocio.
- **Cero dependencias** de frameworks: sin ASP.NET Core, sin EF Core, sin SQL Server.
- Contiene las reglas de negocio encapsuladas (p. ej. `Asset.AssignTo(user, date)`).

### 3.2 `TIAdmin.Application` (casos de uso)

- Organizado **por feature** (vertical slices), no por tipo técnico:

```text
Features/
├── Assets/
│   ├── Create/   CreateAssetCommand.cs, CreateAssetHandler.cs, CreateAssetValidator.cs
│   ├── Update/
│   ├── Delete/
│   ├── Assign/
│   └── Search/
├── Tickets/
│   ├── Create/
│   ├── Assign/
│   ├── Resolve/
│   └── Close/
└── ...
```

- Define puertos (interfaces) que implementa `Infrastructure`: `IAssetRepository`, `IEmailService`, `IFileStorage`, `ICurrentUserService`, `IClock`.
- DTOs, validators, behaviors de pipeline (validación, logging, auditoría, cache).
- **No** conoce EF Core ni SQL Server.

### 3.3 `TIAdmin.Infrastructure` (adaptadores)

- `TIAdminDbContext` y configuraciones Fluent API.
- Repositorios EF Core e implementaciones de interfaces.
- ASP.NET Core Identity, emisión/validación de JWT.
- Servicios: Email, File Storage, Cache, Integraciones externas.
- Migraciones EF Core.

### 3.4 `TIAdmin.Api` (entrada HTTP)

- Controllers delgados: rutean, delegan al handler, devuelven DTO.
- Middleware: correlation id, manejo global de errores, rate limiting, CORS, headers de seguridad.
- OpenAPI/Swagger, health checks, Serilog request logging.
- Registro de DI (`Extensions/`).

### 3.5 `TIAdmin.Tests`

```text
Unit/          # Domain + Application (mocks, sin DB)
Integration/   # DB real (Testcontainers) + repositorios
Functional/    # WebApplicationFactory: endpoints completos
```

---

## 4. Módulos de negocio

Cada módulo agrupa sus features y expone sus propias tablas.

| Módulo | Entidades principales | Prioridad |
|---|---|---|
| Identity & Security | Users, Roles, Permissions, UserRoles, RolePermissions | Alta |
| Organization | Departments, Locations | Alta |
| Assets | Assets, AssetTypes, AssetAssignments, AssetMovements | Alta |
| Software | Softwares, SoftwareLicenses | Alta |
| Vendors & Contracts | Vendors, Contracts | Media |
| Help Desk | Tickets, TicketComments, TicketAttachments, TicketCategories | Alta |
| Service Requests | ServiceRequests | Alta |
| Maintenance | Maintenances | Media |
| Changes | ChangeRequests | Media |
| Purchases | PurchaseRequests, PurchaseItems | Media |
| Documents | Documents | Media |
| Audit | AuditLogs | Alta |
| Configuration | SystemConfigurations, SlaConfigurations | Alta |
| Notifications | Notifications | Media |

---

## 5. Decisiones clave

### 5.1 API stateless

JWT Bearer; el estado de sesión vive en el token (access token corto) con refresh token rotativo. Permite escalado horizontal sin afinidad de sesión.

### 5.2 RBAC con permisos granulares

Roles agrupan permisos; los permisos son strings (`ASSETS.VIEW`, `TICKETS.CLOSE`). La autorización se evalúa por permiso, no por rol, para permitir crear roles nuevos sin tocar código.

### 5.3 Soft delete global

Filtro global de EF Core sobre `ISoftDeletable` (`IsDeleted`, `DeletedAt`, `DeletedBy`). Las entidades con historial nunca se eliminan físicamente.

### 5.4 Fechas en UTC

Todo se almacena en UTC (`datetime2`). La conversión a zona horaria de la organización (`America/Managua` por defecto) ocurre en el frontend usando la configuración del backend.

### 5.5 Archivos fuera de la base de datos

Solo metadata en `Documents`; el binario vive en File Storage local (dev) o Azure Blob/S3 (prod) detrás de `IFileStorage`.

### 5.6 Caché

`IMemoryCache` por defecto para catálogos estáticos (AssetTypes, Categories, Departments, Locations, Config, Permissions). Redis como opción distribuida cuando el despliegue lo requiera.

---

## 6. Flujo de una request (ejemplo: crear activo)

```text
1. Request POST /api/v1/assets
2. Middleware: CorrelationId, logging, rate limit
3. JwtBearer valida token → ClaimsPrincipal
4. Policy-based authz valida permiso ASSETS.CREATE
5. Controller valida ModelState, mappea DTO → CreateAssetCommand
6. FluentValidation ejecuta CreateAssetValidator
7. Pipeline behaviors: Logging → Validation → Audit
8. Handler: construye Asset (Domain), llama IAssetRepository
9. DbContext.SaveChangesAsync → interceptor escribe AuditLog
10. Handler devuelve AssetDto
11. Controller responde 201 + ApiResponse<AssetDto>
12. Serilog registra con CorrelationId/TraceId
```

---

## 7. Despliegue de referencia

```text
                 Internet / LAN
                       │
                       ▼
              Reverse Proxy (IIS/Nginx)
                       │
          ┌────────────┴────────────┐
          ▼                         ▼
    Angular SPA                ASP.NET API
      (estático)                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
              SQL Server        File Storage      Redis
                                              (opcional)
```

Ambientes: `Development` → `QA` → `Staging` → `Production`, con configuración independiente y secretos gestionados fuera del repositorio.

---

## 8. Decisiones pendientes de confirmar

| ID | Tema | Estado |
|---|---|---|
| Q-01 | Mapper de objetos: AutoMapper vs Mapster (SPECS.md deja ambos como opción) | Pendiente de decisión en Fase 1 |
| Q-02 | Librería UI: Angular Material vs PrimeNG vs componente propio | Pendiente de decisión en Fase 9 |
| Q-03 | Generación de PDF: QuestPDF vs DinkToPdf | Pendiente de decisión en Fase 10 |
| Q-04 | Scheduler de tareas: Quartz.NET vs BackgroundService | Pendiente de decisión en Fase 10 |
| Q-05 | Strategy GitFlow vs trunk-based (SPECS.md sugiere ambas ramas `main`/`develop`) | Confirmado GitFlow simplificado (ADR-002) |
