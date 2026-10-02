# IMPLEMENTATION PLAN
# Sistema Administrativo de Tecnologías de Información (TI Admin)

**Versión:** 1.0
**Fecha:** Octubre 2026
**Referencia:** docs/SPECS.md

---

## 1. Objetivo

Este plan define la ruta de implementación paso a paso para construir el sistema TI Admin siguiendo los lineamientos de SPECS.md (Clean Architecture / Modular Monolith, ASP.NET Core 10, Angular 22.x, SQL Server 2022).

---

## 2. Alcance y Supuestos

- Arquitectura: Modular Monolith + Clean Architecture (API + SPA).
- Backend: .NET 10 / C# 14, EF Core 10, ASP.NET Core Identity, JWT/OIDC, Serilog, FluentValidation, AutoMapper/Mapster, xUnit.
- Frontend: Angular 22.x, standalone components, Signals/RxJS, SCSS, guards/interceptors.
- DB: SQL Server 2022+, migraciones EF Core.
- Seguridad: RBAC con permisos granulares, auditoría, HTTPS, headers, rate limiting.
- Entregable principal: aplicación funcional por fases con pruebas, lint/typecheck y documentación técnica.

Supuestos:
- Infra disponible para dev (SQL Server local/Docker).
- Repositorio Git existente; se seguirá estrategia feature/*.
- Se priorizan módulos de Alta prioridad (Autenticación, Usuarios/Roles, Organización, Activos, Asignaciones, Software, Help Desk, Solicitudes, Reportes, Auditoría, Configuración).

---

## 3. Organización del Plan

El plan se divide en fases incrementales y ejecutables. Cada fase incluye: Objetivo, Tareas, Entregables, Criterios de Aceptación, Dependencias y Estimado.

Fases:
- Fase 0: Preparación y Fundación
- Fase 1: Infraestructura Base (Solución, Capas, DB, Config)
- Fase 2: Autenticación, Autorización y Seguridad
- Fase 3: Núcleo Organizacional (Usuarios, Roles, Departamentos, Ubicaciones)
- Fase 4: Módulo de Activos TI y Asignaciones
- Fase 5: Software, Licenciamiento y Proveedores
- Fase 6: Help Desk, Solicitudes y SLA
- Fase 7: Mantenimientos, Contratos, Cambios y Compras
- Fase 8: Auditoría, Reportes, Dashboard y Configuración
- Fase 9: Frontend Core y Módulos Funcionales
- Fase 10: Integraciones, Notificaciones, Archivos y Caché
- Fase 11: Calidad, Pruebas, Observabilidad y CI/CD
- Fase 12: Despliegue, Seguridad Final, Go-Live y Documentación

---

## 4. FASE 0: Preparación y Fundación

**Objetivo:** Establecer entorno, gobernanza y lineamientos base.

### Tareas
1. **Repositorio y branching**
- [ ] Verificar estado Git (repo ya existe en `it-admin`). Confirmar ramas: `main`, `develop`.
- [ ] Definir política de PRs, convenciones de commits, plantilla de PR.
- [ ] Crear archivo `CONTRIBUTING.md` (opcional) y actualizar `README.md` con objetivos.

2. **Entorno de desarrollo**
- [ ] Confirmar requisitos: .NET 10 SDK, Node.js LTS, Angular 22 CLI, SQL Server 2022+, Docker (recomendado).
- [ ] Configurar `appsettings.Development.json` (plantillas), variables de entorno y user-secrets para secretos.
- [ ] Crear `docker-compose.yml` para desarrollo (API, SQL Server, Redis opcional).

3. **Documentación y reglas**
- [ ] Revisar SPECS.md y generar backlog inicial (Issues/Tickets) por módulos.
- [ ] Crear `docs/ARCHITECTURE.md` (diagramas y decisiones).
- [ ] Definir convenciones (C#, Angular, EF Core) en `docs/CONVENTIONS.md`.

4. **Análisis y modelado**
- [ ] Elaborar ERD inicial (entidades principales: Users, Roles, Departments, Locations, Assets, Tickets, Licenses, Vendors).
- [ ] Validar modelo con stakeholders (si aplica).

**Entregables:** Entorno listo, backlog inicial, ERD preliminar.
**Dependencias:** Ninguna.
**Estimado:** 0.5–1 día.

**Criterios de Aceptación:**
- `.gitignore` adecuado (.NET + Angular + Docker + secretos).
- `docker-compose up` levanta DB/API base.
- Decisiones registradas en docs.

---

## 5. FASE 1: Infraestructura Base (Solución, Capas y Persistencia)

**Objetivo:** Crear estructura de solución Clean Architecture y capa de persistencia base.

### Tareas
1. **Crear solución y proyectos**
- [ ] Crear solución `TIAdmin.sln`.
- [ ] Proyectos: `TIAdmin.Api`, `TIAdmin.Application`, `TIAdmin.Domain`, `TIAdmin.Infrastructure`, `TIAdmin.Tests` (Unit/Integration/Functional).
- [ ] Configurar referencias entre capas (Api → Application; Application → Domain; Infrastructure → Application/Domain).

2. **Dependencias base**
- [ ] Backend: ASP.NET Core 10, EF Core 10 (SqlServer), ASP.NET Core Identity, JWT Bearer, FluentValidation, AutoMapper/Mapster, Serilog, HealthChecks, Swashbuckle/OpenAPI.
- [ ] Tests: xUnit, Moq/NSubstitute, FluentAssertions (opcional), Testcontainers (para Integration).

3. **Configuración y opciones**
- [ ] `appsettings.*.json` por ambientes (Development/QA/Staging/Production).
- [ ] Options Pattern: `JwtOptions`, `DatabaseOptions`, `EmailOptions`, `StorageOptions`, `AppOptions`, `SlaOptions`.
- [ ] Config por ambiente + secretos (User Secrets / Azure Key Vault futuro).

4. **Persistencia y DbContext**
- [ ] Crear `TIAdminDbContext` (IdentityDbContext si usa ASP.NET Identity).
- [ ] Configuraciones Fluent API por entidad (`IEntityTypeConfiguration<T>`).
- [ ] Convenciones: Soft Delete global (query filters), UTC timestamps, nombres consistentes.
- [ ] Índices, FKs, constraints.
- [ ] Seed inicial: Roles, Permisos, Usuario SUPER_ADMIN (solo dev).

5. **Migraciones y base de datos**
- [ ] Primera migración `InitialCreate`.
- [ ] Scripts para creación/actualización de BD.
- [ ] Configurar HealthChecks: DB, etc.

**Entregables:** Solución estructurada, DbContext base, migración inicial, configuración por ambientes.
**Dependencias:** Fase 0.
**Estimado:** 1–2 días.

**Criterios de Aceptación:**
- Build limpio sin errores.
- API arranca con Swagger/OpenAPI habilitado.
- Migración aplicada correctamente a SQL Server.
- Health `/health` responde OK.

---

## 6. FASE 2: Autenticación, Autorización y Seguridad

**Objetivo:** Implementar identidad, JWT/OIDC, RBAC con permisos granulares y middleware de seguridad.

### Tareas
1. **Identidad (ASP.NET Core Identity)**
- [ ] Configurar Identity (Users/Roles custom si necesario: extender `IdentityUser<int>`, `IdentityRole<int>`).
- [ ] Password policy, lockout, email confirmation opcional.
- [ ] Password hashing, account management.

2. **Autenticación JWT**
- [ ] Generación/validación de tokens (Access/Refresh opcional).
- [ ] Config `JwtBearer` con validaciones (issuer/audience/expiry).
- [ ] Middleware de autenticación/autorización.
- [ ] Soporte para OpenID Connect (Entra ID) preparado vía interfaces (`IIdentityProvider`).

3. **Autorización RBAC + Permisos**
- [ ] Modelo: Users ↔ Roles ↔ Permissions + Permisos directos a usuario (recomendado).
- [ ] Implementar `IAuthorizationPolicyProvider` o `AuthorizationHandler` basado en claims/permissions (ej. `ASSETS.VIEW`).
- [ ] Sembrar matriz de permisos (secciones 16, 570+).
- [ ] Roles iniciales: SUPER_ADMIN, TI_ADMIN, TI_SUPPORT, TI_ASSET_MANAGER, TI_MANAGER, AUDITOR, USER.

4. **Seguridad transversal**
- [ ] HTTPS obligatorio, CORS restringido por ambientes.
- [ ] Headers de seguridad (HSTS, CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy).
- [ ] Rate limiting (global y por endpoint sensible: login).
- [ ] Protección CSRF (si cookies; API stateless JWT -> menos crítico).
- [ ] Validación de entradas (FluentValidation + model validation).
- [ ] Anti-SQL Injection garantizado por EF Core parametrizado.
- [ ] Manejo global de errores + `traceId` (Middleware). No exponer stack traces en prod.

5. **Auditoría base**
- [ ] Entidad `AuditLog` (Id, UserId, Action, EntityName, EntityId, OldValues, NewValues, IpAddress, UserAgent, Timestamp, CorrelationId).
- [ ] Middleware/Interceptor de auditoría para operaciones críticas (acciones de escritura).
- [ ] Solo lectura para usuarios normales.

6. **Endpoints Auth**
- [ ] `POST /api/v1/auth/login`
- [ ] `POST /api/v1/auth/logout` (opcional si stateless)
- [ ] `POST /api/v1/auth/refresh` (si implementado)
- [ ] `GET /api/v1/auth/me`

**Entregables:** Auth completa, RBAC/permissions, seguridad y auditoría base.
**Dependencias:** Fase 1.
**Estimado:** 2–3 días.

**Criterios de Aceptación:**
- Login con credenciales retorna JWT válido.
- Endpoints protegidos requieren permiso correcto.
- Rate limiting aplicado a login.
- Auditoría registra cambios críticos.
- Errores devuelven formato estándar + traceId.

---

## 7. FASE 3: Núcleo Organizacional (Usuarios, Roles, Departamentos, Ubicaciones)

**Objetivo:** CRUDs del núcleo organizacional + gestión de roles/permisos dinámica.

### Tareas
1. **Usuarios**
- [ ] Entidad `User` (extiende Identity o entidad propia ligada). Campos: Id, Username/Email, FirstName, LastName, EmployeeCode, DepartmentId, LocationId, IsActive, CreatedAt, UpdatedAt, IsDeleted, DeletedAt, DeletedBy.
- [ ] DTOs, Commands/Queries (CQRS por Features), Validaciones.
- [ ] Endpoints: `GET/POST/PUT/PATCH/DELETE /api/v1/users`, paginación, búsqueda, filtros (activo, depto, ubicación).
- [ ] Asignación de roles y permisos directos.

2. **Roles**
- [ ] CRUD roles, asignar/desasignar permisos, listar permisos disponibles.
- [ ] Endpoints `/api/v1/roles`.

3. **Departamentos**
- [ ] `Department` (Id, Code, Name, ManagerId, IsActive, IsDeleted...).
- [ ] CRUD + validaciones (códigos únicos).

4. **Ubicaciones**
- [ ] `Location` (Id, Code, Name, Address, IsActive).
- [ ] CRUD.

5. **Reglas y auditoría**
- [ ] Soft delete para entidades con historial.
- [ ] Auditoría en cambios de roles/permisos y usuarios.

**Entregables:** Módulos organizacionales completos, API documentada.
**Dependencias:** Fase 2.
**Estimado:** 1–2 días.

**Criterios de Aceptación:**
- CRUDs funcionales con paginación/filtros.
- RBAC dinámico (crear rol y asignar permisos sin modificar código).
- Validaciones y errores consistentes.
- Auditoría registrada.

---

## 8. FASE 4: Activos TI y Asignaciones

**Objetivo:** Implementar inventario de activos y control de asignaciones con historial.

### Tareas
1. **Catálogo de tipos de activo**
- [ ] `AssetType` (enum o tabla). Valores iniciales: Laptop, Desktop, Monitor, Printer, Server, Network Device, Mobile Device, UPS, Storage, Telephone, Peripheral, Other.
- [ ] Seed de tipos.

2. **Activos (`Asset`)**
- [ ] Entidad: AssetCode, SerialNumber, AssetTypeId, Brand, Model, PurchaseDate, PurchaseCost, WarrantyExpiration, Status, LocationId, DepartmentId, Notes, CreatedAt/UpdatedAt, IsDeleted.
- [ ] Estados: AVAILABLE, ASSIGNED, MAINTENANCE, REPAIR, RETIRED, LOST, DISPOSED.
- [ ] Validaciones: AssetCode y SerialNumber únicos.
- [ ] Features: Create/Update/Delete (soft)/Search/Filter (estado, tipo, depto, ubicación, rango fechas).

3. **Asignaciones de activos (`AssetAssignment`)**
- [ ] Entidad: AssetId, UserId, AssignmentDate, ReturnDate, AssignedBy, ReturnedBy, ConditionAtAssignment, ConditionAtReturn, Notes.
- [ ] Regla: **no sobrescribir historial** (crear nuevo registro al desasignar/asignar siguiente).
- [ ] Flujo: Asignar → cambia Asset.Status a ASSIGNED; Desasignar → cierra asignación (ReturnDate + ReturnedBy + condiciones) y cambia Asset.Status a AVAILABLE (o estado definido).
- [ ] Historial completo por activo.

4. **Movimientos (opcional pero recomendado)**
- [ ] `AssetMovement` para trazabilidad (Location/Department/Status changes).

5. **API Endpoints**
- [ ] `/api/v1/assets`, `/api/v1/asset-types`, `/api/v1/assignments`.
- [ ] Permisos: ASSETS.VIEW/CREATE/UPDATE/DELETE/ASSIGN/UNASSIGN.

6. **Documentación/adjuntos base**
- [ ] Preparar relación para Documents por EntityName/EntityId (Fase 10).

**Entregables:** Módulo Activos completo con historial inmutable.
**Dependencias:** Fase 3.
**Estimado:** 2–3 días.

**Criterios de Aceptación:**
- CRUD activos con filtros/paginación.
- Asignación/desasignación preserva historial.
- Estados coherentes entre Asset y AssetAssignments.
- Auditoría de movimientos relevantes.

---

## 9. FASE 5: Software, Licenciamiento, Proveedores y Contratos (Base)

**Objetivo:** Catálogo de software, licencias, proveedores y contratos con alertas.

### Tareas
1. **Software**
- [ ] `Software` (Id, Name, Publisher, Version, IsActive).
- [ ] CRUD.

2. **Proveedores (`Vendor`)**
- [ ] `Vendor`: Name, TaxId, ContactName, Email, Phone, Address, Status, Notes, IsDeleted.
- [ ] Soft delete (histórico con contratos/licencias/compras/mantenimientos).

3. **Software Licenses (`SoftwareLicense`)**
- [ ] Campos: SoftwareId, VendorId, LicenseType, LicenseKey (protegido/no logueado), Quantity, UsedQuantity, PurchaseDate, ExpirationDate, Cost, IsActive.
- [ ] Validaciones: UsedQuantity <= Quantity.
- [ ] Alertas: próximas a vencer (configurable), vencidas, sobreasignación, baja utilización.
- [ ] Cálculo de UsedQuantity por asignaciones (si aplica).

4. **Contratos (`Contract`)**
- [ ] Campos: Number, VendorId, Type, StartDate, EndDate, Value, Renewal, ResponsibleUserId, Status, Notes, IsDeleted.
- [ ] Alertas configurables: 90/60/30/15/7 días antes (parámetros en Configuración).
- [ ] Estados y validaciones de fechas.

5. **API**
- [ ] `/api/v1/software`, `/api/v1/licenses`, `/api/v1/vendors`, `/api/v1/contracts`.
- [ ] Permisos correspondientes.

**Entregables:** Módulos base de gestión TI con alertas configurables.
**Dependencias:** Fase 3–4.
**Estimado:** 1–2 días.

**Criterios de Aceptación:**
- Licencias no permiten sobreasignación (validación).
- Alertas de vencimiento calculadas correctamente.
- Soft delete respetado en Vendor/Contract.
- Claves de licencia no aparecen en logs ni en respuestas innecesarias.

---

## 10. FASE 6: Help Desk, Solicitudes y SLA

**Objetivo:** Tickets, categorías, solicitudes de servicio y cumplimiento de SLA.

### Tareas
1. **Categorías de Ticket**
- [ ] `TicketCategory` (Id, Name, DepartmentId, IsActive, SlaConfig opcional).
- [ ] Seed inicial.

2. **Tickets (`Ticket`)**
- [ ] Campos: TicketNumber (auto-generado), Title, Description, CategoryId, Priority, Status, RequesterId, AssignedToId, DepartmentId, CreatedAt, UpdatedAt, ResolvedAt, ClosedAt.
- [ ] Estados: NEW, OPEN, IN_PROGRESS, WAITING_USER, WAITING_VENDOR, RESOLVED, CLOSED, CANCELLED.
- [ ] Prioridades: LOW, MEDIUM, HIGH, CRITICAL.
- [ ] Flujo de estados validado (transiciones permitidas).
- [ ] Comentarios (`TicketComment`): TicketId, UserId, Content, IsInternal, CreatedAt.
- [ ] Adjuntos (`TicketAttachment`): vinculado a Ticket via EntityName/EntityId o tabla propia (usar Documents en Fase 10 recomendado).

3. **Solicitudes de Servicio (`ServiceRequest`)**
- [ ] `ServiceRequest` similar a Ticket o entidad diferenciada: RequestNumber, Type, Priority, Status, RequesterId, AssignedToId, DepartmentId, CreatedAt, etc. Según alcance (módulo Solicitudes).
- [ ] Endpoints `/api/v1/requests`.

4. **SLA**
- [ ] Configuración SLA por: Categoría/Prioridad/Tipo/Horario laboral/Departamento.
- [ ] Cálculo de tiempos: Tiempo respuesta y Tiempo resolución (horas/fechas límite).
- [ ] Detección de tickets vencidos (SLA breached).
- [ ] Valores configurables (no hardcodeados). Almacenar en tabla `SlaConfigurations` o `Configuration`.
- [ ] Métrica Cumplimiento de SLA para reportes/dashboard.

5. **API**
- [ ] `/api/v1/tickets`, `/api/v1/ticket-categories`, `/api/v1/requests`, `/api/v1/sla`.
- [ ] Permisos: TICKETS.VIEW/CREATE/UPDATE/ASSIGN/RESOLVE/CLOSE.

**Entregables:** Help Desk + Solicitudes + SLA funcional con transiciones y cálculo de vencimientos.
**Dependencias:** Fase 3.
**Estimado:** 2–3 días.

**Criterios de Aceptación:**
- TicketNumber autogenerado y único.
- Transiciones de estado válidas aplicadas.
- Comentarios internos/externos diferenciados.
- SLA calcula tiempos límite correctamente según prioridad/categoría/horario (configurable).
- Tickets vencidos identificables para reportes/notificaciones.

---

## 11. FASE 7: Mantenimientos, Contratos Avanzados, Cambios, Compras

**Objetivo:** Completar módulos Gestión TI.

### Tareas
1. **Mantenimientos (`Maintenance`)**
- [ ] Tipos: PREVENTIVE, CORRECTIVE, EMERGENCY.
- [ ] Estados: PLANNED, SCHEDULED, IN_PROGRESS, COMPLETED, CANCELLED.
- [ ] Relaciona: Asset, Technician (User), Vendor, Ticket, Cost, Date/ScheduledDate, Findings, Actions, Recommendations.
- [ ] CRUD + filtros (activo, tipo, estado, fechas).

2. **Gestión de Cambios (`ChangeRequest`)**
- [ ] Campos: Number (autogenerado), Title, Description, Type, Risk, Impact, RequestedBy, AssignedTo, PlannedDate, ImplementationDate, RollbackPlan, Status, Approval (datos/aprobador).
- [ ] Estados: DRAFT, REQUESTED, UNDER_REVIEW, APPROVED, REJECTED, IMPLEMENTING, COMPLETED, ROLLED_BACK, CLOSED.
- [ ] Flujo y aprobaciones (mínimo registro de aprobación).
- [ ] Endpoints `/api/v1/changes`.

3. **Compras TI (`PurchaseRequest`)**
- [ ] `PurchaseRequest`: Number, Title, Description, RequestedBy, DepartmentId, VendorId (opcional), Status, RequestedDate, NeededDate, TotalCost, Items, Notes.
- [ ] Estados según flujo interno.
- [ ] Endpoints `/api/v1/purchases`.

4. **Contratos (refuerzo)**
- [ ] Asegurar alertas periódicas (scheduler) y documentos asociados (Fase 10).

**Entregables:** Mantenimientos, Cambios, Compras implementados.
**Dependencias:** Fase 5–6.
**Estimado:** 1–2 días.

**Criterios de Aceptación:**
- Mantenimientos vinculables a Asset/Ticket/Vendor.
- ChangeRequest sigue flujo de estados con rollback plan registrado.
- Auditoría en aprobaciones/implementación de cambios.

---

## 12. FASE 8: Auditoría, Reportes, Dashboard y Configuración

**Objetivo:** Trazabilidad, indicadores gerenciales y parametrización del sistema.

### Tareas
1. **Auditoría (`AuditLog`)**
- [ ] CRUD solo lectura: `/api/v1/audit`.
- [ ] Filtros: UserId, Action, EntityName, EntityId, rango fechas, IpAddress.
- [ ] Paginación. Acceso restringido (AUDITOR/roles con AUDIT.VIEW).
- [ ] No editable/eliminable por usuarios normales.

2. **Reportes**
- [ ] Endpoints para reportes listados (sección 48):
  - Inventario activos, activos por usuario/depto/ubicación, activos en mantenimiento.
  - Software instalado, licencias vencidas/próximas a vencer.
  - Tickets por período/técnico/categoría, cumplimiento SLA.
  - Costos TI, contratos por vencer, historial movimientos, auditoría.
- [ ] Parámetros: filtros, rango fechas, paginación.
- [ ] Exportación: Excel/CSV/PDF (ver Fase 10). Procesamiento asíncrono para grandes volúmenes.
- [ ] Permisos `REPORTS.VIEW`, `REPORTS.EXPORT`.

3. **Dashboard**
- [ ] `GET /api/v1/dashboard/summary` con KPIs:
  - Total activos, por estado, por departamento/ubicación.
  - Tickets abiertos, vencidos, por prioridad, % SLA cumplimiento.
  - Licencias próximas/vencidas, % vigentes.
  - Contratos próximos a vencer.
  - Mantenimientos pendientes.
  - Costos TI (agregados).
- [ ] Optimizar consultas (agregados, proyecciones). Considerar caché para datos relativamente estáticos.
- [ ] Tiempo objetivo < 2s.

4. **Configuración del Sistema (`Configuration`)**
- [ ] Tabla `SystemConfiguration`: Key, Value, Description, Group, IsEncrypted, UpdatedBy, UpdatedAt.
- [ ] Gestión de parámetros globales: Zona horaria (default `America/Managua`), SLA por defecto, períodos alertas contratos/licencias, horario laboral, formatos, etc.
- [ ] Endpoints `/api/v1/configuration` (solo lectura para mayoría, escritura para TI_ADMIN/SUPER_ADMIN).
- [ ] Encriptar valores sensibles (IsEncrypted).

**Entregables:** Auditoría completa, reportes base, dashboard con KPIs, configuración parametrizable.
**Dependencias:** Fase 4–7.
**Estimado:** 1–2 días.

**Criterios de Aceptación:**
- AuditLog inmutable para roles no autorizados.
- Dashboard retorna KPIs agregados < 2s.
- Reportes soportan filtros y paginación.
- Configuración por ambiente y parámetros no hardcodeados.

---

## 13. FASE 9: Frontend Angular (Core + Módulos Funcionales)

**Objetivo:** SPA Angular 22.x con arquitectura modular, standalone components, seguridad y UX consistente.

### Tareas
1. **Estructura y scaffolding**
- [ ] Crear proyecto Angular 22: `ti-admin-web` (o `src/Web/`).
- [ ] Arquitectura: `core/`, `shared/`, `layout/`, `features/` (sección 29).
- [ ] Configurar rutas con lazy loading por feature.
- [ ] Environment files: `environment.ts`, `environment.qa.ts`, `environment.staging.ts`, `environment.prod.ts`.
- [ ] SCSS base, tema, variables, diseño responsive.

2. **Core (Base)**
- [ ] Modelos TypeScript (DTOs) alineados a API.
- [ ] Servicios base: `ApiService`, `AuthService`, `HttpErrorHandler`.
- [ ] Interceptors: Auth (token), Error (401/403, traceId), CorrelationId, Loading global.
- [ ] Guards: `AuthGuard`, `RoleGuard`, `PermissionGuard`.
- [ ] Auth store: Signals + RxJS para estado auth (user, roles, permissions, token).
- [ ] Utilities, constantes, enums.

3. **Layout y navegación**
- [ ] Header, Sidebar, Footer, Breadcrumbs.
- [ ] Menú dinámico según permisos/roles (sección 31).
- [ ] Responsive (mobile).

4. **Componentes compartidos (Shared UI)**
- [ ] DataTable, SearchBox, FilterPanel, Modal/Drawer, Form controls, DatePicker, Dropdown, StatusBadge, ConfirmDialog, Toast, Pagination, FileUploader, Timeline, DashboardCard, Chart (opcional).
- [ ] Directivas/pipes reutilizables.

5. **Módulos funcionales (features)**
- [ ] **Auth**: Login, Logout, Perfil (`/auth/login`, `/profile`).
- [ ] **Dashboard**: KPIs, gráficos, alertas (vencimientos).
- [ ] **Usuarios y Administración**: Users, Roles, Departments, Locations.
- [ ] **Activos**: Assets list/detail/create/edit, AssetTypes, Asignaciones (historial), Estados.
- [ ] **Software y Licencias**: Software, Licenses, alertas vencimiento.
- [ ] **Proveedores y Contratos**: Vendors, Contracts, alertas.
- [ ] **Help Desk**: Tickets (list, detail, create, assign, comments, transitions), Solicitudes, Categorías, SLA (vista).
- [ ] **Mantenimientos**: Maintenance calendar/list.
- [ ] **Cambios/Compras/Documentos**: CRUDs básicos.
- [ ] **Reportes**: Filtros, vista previa, exportación (Excel/CSV/PDF).
- [ ] **Auditoría**: Solo lectura con filtros.
- [ ] **Configuración**: Parámetros del sistema (acceso restringido).

6. **UX, Accesibilidad y Estados**
- [ ] Loading states, empty states, errores.
- [ ] Validación de formularios (Reactive Forms).
- [ ] Toasts/Confirmaciones para acciones destructivas.
- [ ] Accesibilidad básica (ARIA) y consistencia visual.

7. **Comunicación API**
- [ ] Servicios por feature (`AssetService`, `TicketService`, `UserService`, `LicenseService`, `VendorService`, `ReportService`, `AuditService`, `ConfigService`).
- [ ] No URLs hardcodeadas en componentes (usar environment + servicios).
- [ ] Paginación/filtros tipados.

**Entregables:** SPA funcional con login, navegación por permisos, CRUDs principales y dashboard.
**Dependencias:** Fase 2–8 (API endpoints disponibles).
**Estimado:** 4–6 días.

**Criterios de Aceptación:**
- Login funcional, guards protegen rutas por rol/permiso.
- Lazy loading por features.
- CRUDs principales operativos (Usuarios, Activos, Tickets, Licencias, Reportes).
- Dashboard muestra KPIs desde API.
- Interceptors manejan 401/403 y errores globalmente.
- UI responsive y consistente.

---

## 14. FASE 10: Integraciones, Notificaciones, Archivos, Caché y Exportación

**Objetivo:** Completar capacidades transversales.

### Tareas
1. **Almacenamiento de archivos (Documents)**
- [ ] Entidad `Document`: Id, FileName, MimeType, Size, StoragePath, EntityName, EntityId, UploadedBy, UploadedAt.
- [ ] `IFileStorage` + implementación local (dev) y abstracción para futuro (Azure Blob/S3).
- [ ] Endpoints: `POST /api/v1/documents/upload`, `GET /api/v1/documents/{id}/download`, `DELETE /api/v1/documents/{id}`.
- [ ] Adjuntos: Tickets, Assets, Contracts, Licenses, Changes, Maintenance.
- [ ] No almacenar archivos en BD (solo metadata). Validaciones de tipo/tamaño.

2. **Notificaciones**
- [ ] **Email**: `IEmailService` + implementación (SMTP dev, SendGrid/Azure Communication futuro). Plantillas HTML.
- [ ] Eventos: Ticket creado/asignado/actualizado/vencido, Licencia por vencer, Contrato por vencer, Mantenimiento próximo, Cambio aprobado/rechazado.
- [ ] **Notificaciones internas**: `Notification` (Id, UserId, Type, Title, Message, IsRead, CreatedAt). Endpoints marcar leído/todas.
- [ ] Jobs/background (Quartz/BackgroundService) para notificaciones programadas (vencimientos).

3. **Caché**
- [ ] `IMemoryCache` para estáticos: AssetTypes, TicketCategories, Departments, Locations, Configuration, Permissions.
- [ ] Redis (opcional, distribuido): evaluar si despliegue escalable. Configurable por ambiente.
- [ ] Invalidación de caché en CRUDs relevantes.

4. **Exportación asíncrona**
- [ ] Generación Excel (EPPlus/ClosedXML), CSV, PDF (QuestPDF/Rotativa/DinkToPdf o Puppeteer alternativa).
- [ ] Procesamiento asíncrono + almacenamiento temporal + notificación/link de descarga.
- [ ] Endpoints `/api/v1/reports/export` con jobId.

5. **Importación**
- [ ] Importación Excel/CSV: Usuarios, Activos, Software, Licencias, Proveedores.
- [ ] Validación de plantillas, errores por fila, reporte de resultados.

6. **Scheduler (Tareas programadas)**
- [ ] BackgroundService/Quartz para: alertas vencimientos (contratos/licencias), SLA vencidos, recordatorios mantenimientos, limpieza temporal.

**Entregables:** Gestión documental, notificaciones (email + internas), caché, import/export asíncronos y scheduler.
**Dependencias:** Fase 6–8.
**Estimado:** 2–3 días.

**Criterios de Aceptación:**
- Documentos subidos/descargados con metadata correcta. Archivos fuera de BD.
- Emails enviados con plantillas (dev: consola/log).
- Notificaciones internas leíbles/marcables.
- Exportaciones grandes asíncronas con link de descarga.
- Imports validan filas y reportan errores.

---

## 15. FASE 11: Calidad, Pruebas, Observabilidad y CI/CD

**Objetivo:** Garantizar calidad, cobertura, logging, health checks y pipeline automatizado.

### Tareas
1. **Logging y Observabilidad**
- [ ] Serilog estructurado con sinks (Console, File, Seq/ELK futuro).
- [ ] CorrelationId por request, incluir UserId, Endpoint, Method, StatusCode, Duration, IpAddress.
- [ ] Middleware para logging de requests/responses (sensibles omitidos).
- [ ] Integración con traceId en errores.

2. **Health Checks**
- [ ] `/health`, `/health/live`, `/health/ready`.
- [ ] Checks: DB (SQL), Storage (si aplica), Email (opcional), Redis (opcional), servicios críticos.
- [ ] Respuestas JSON detalladas por ambiente.

3. **Pruebas Unitarias (Backend)**
- [ ] Domain: entidades, value objects, reglas negocio.
- [ ] Application: Handlers (Commands/Queries), Validators, Behaviors.
- [ ] Infrastructure/Services: casos unitarios con mocks.
- [ ] Cobertura objetivo inicial: >= 60–70% (crítico >= 80%).

4. **Pruebas de Integración (Backend)**
- [ ] API endpoints (auth, CRUDs, autorización, auditoría).
- [ ] DB con Testcontainers (SQL Server) o BD efímera.
- [ ] Flujos transversales (asignación activos, transiciones tickets, SLA).

5. **Pruebas Funcionales/E2E mínimos**
- [ ] E2E Backend: Login → Crear activo → Asignar → Crear ticket → Asignar → Resolver → Cerrar → Licencia → Reporte (sección 1702).
- [ ] Frontend Unit: componentes, services, guards, interceptors, formularios (Jasmine/Karma o Jest opcional).
- [ ] Frontend E2E: Cypress/Playwright para flujos críticos.

6. **Lint, Formateo y Typecheck**
- [ ] Backend: Roslyn analyzers, `.editorconfig`, StyleCop opcional.
- [ ] Frontend: ESLint, Prettier, `ng lint`, `tsc --noEmit`.
- [ ] Hooks/pre-commit (Husky opcional) para validar antes de commit.

7. **CI/CD Pipeline**
- [ ] GitHub Actions (recomendado): Build, Unit Tests, Static Analysis, Security Scan, Integration Tests por PR a `develop/main`.
- [ ] Jobs separados: Backend (.NET), Frontend (Angular), Lint/Typecheck.
- [ ] Security: Snyk/Dependabot, CodeQL (opcional), análisis de secretos.
- [ ] Artefactos: publicar imágenes Docker (si aplica).
- [ ] Entornos: Development → QA → Staging → Production con approvals.

8. **Performance y carga (básico)**
- [ ] Benchmarks objetivos (sección 1528): API simple < 500ms, paginada < 1s, Dashboard < 2s.
- [ ] Identificar N+1 (Projections, Include/ThenInclude, AsNoTracking, `.Select()`), índices.

**Entregables:** Cobertura de pruebas, pipeline CI/CD funcional, health checks y observabilidad completa.
**Dependencias:** Fase 9–10.
**Estimado:** 2–3 días.

**Criterios de Aceptación:**
- Tests pasan en CI (unit + integration).
- Lint/typecheck sin errores.
- Health checks operativos.
- Logs estructurados con CorrelationId/traceId.
- Pipeline bloquea merge si fallan tests/lint.

---

## 16. FASE 12: Despliegue, Seguridad Final, Go-Live y Documentación

**Objetivo:** Preparar despliegue a entornos, hardening, backup/DR, cutover y documentación final.

### Tareas
1. **Docker y Contenerización**
- [ ] Dockerfiles: `TIAdmin.Api` (multi-stage), `ti-admin-web` (nginx o SSR opcional).
- [ ] `docker-compose.yml` para dev/QA. Producción evaluar infraestructura corporativa (SQL Server on-prem/cloud).
- [ ] Healthchecks en contenedores.

2. **Despliegue y Arquitectura**
- [ ] Configurar ambientes: Development/QA/Staging/Production (sección 1779).
- [ ] Reverse Proxy (IIS/Nginx/Azure App Gateway), HTTPS, certificados.
- [ ] Configuración de secretos: Azure Key Vault/User Secrets/Environment Variables (nunca en repo).

3. **Seguridad Hardening Final**
- [ ] Revisar OWASP Top 10: validaciones, authz, rate limiting, headers, CORS estricto.
- [ ] Rotación de secrets, JWT secrets por ambiente.
- [ ] Auditoría completa habilitada en prod.
- [ ] Deshabilitar Swagger en Production (o proteger).
- [ ] MFA preparado vía OIDC (Entra ID).

4. **Backup y Recuperación ante Desastres**
- [ ] **DB**: Full/Differential/Transaction Log, retención configurable, scripts automatizados.
- [ ] **Archivos**: estrategia de respaldo independiente (storage).
- [ ] **Configuración**: versionada (Infrastructure as Code opcional).
- [ ] Definir RPO/RTO (sugeridos 24h/8h) y aprobar con organización (sección 1880).

5. **Pruebas de Aceptación (UAT) y Performance Real**
- [ ] UAT con usuarios clave (flujos críticos por roles).
- [ ] Validar objetivos performance (sección 1528) en entorno Staging.
- [ ] Pruebas de carga ligeras (k6/JMeter) para Dashboard/Tickets/Assets.

6. **Documentación Final**
- [ ] `README.md` actualizado: instalación, configuración, ejecución (dev/Docker).
- [ ] `docs/ARCHITECTURE.md`: diagramas (capas, flujo, despliegue).
- [ ] `docs/API.md`: referencia OpenAPI (Swagger exportado).
- [ ] `docs/ERD.md`: modelo de datos actualizado.
- [ ] `docs/USER_GUIDE.md`: manual de usuario por roles.
- [ ] `docs/DEPLOYMENT.md`: guía de despliegue por ambiente.
- [ ] `docs/SECURITY.md`: políticas, matriz permisos, auditoría.
- [ ] `docs/RBAC.md`: roles/permisos detallados.
- [ ] `docs/BACKUP_DR.md`: estrategia backup/RPO/RTO.
- [ ] `docs/CONVENTIONS.md`: convenciones código/estilo.

7. **Go-Live**
- [ ] Checklist pre-release (migraciones, seeds, configuración, certificados, backups).
- [ ] Migración de datos iniciales (si existe inventario legacy) vía import Excel/CSV.
- [ ] Cutover planificado, ventana de mantenimiento.
- [ ] Monitoreo post-release (logs, health, errores).

**Entregables:** Aplicación lista para producción, documentación completa, runbooks y checklist Go-Live.
**Dependencias:** Fase 11.
**Estimado:** 1–2 días.

**Criterios de Aceptación:**
- Build/Tests verdes en main.
- Despliegue exitoso en Staging/Prod con health OK.
- UAT firmado (aprobación funcional).
- Backups verificados y DR probado conceptualmente.
- Documentación completa y actualizada.
- Seguridad hardening aplicado (Swagger deshabilitado en prod, headers OK).

---

## 17. Roadmap y Prioridades (Recomendado)

| Fase | Enfoque | Prioridad | Duración Estimada |
|---|---|---|---|
| 0 | Preparación | Alta | 0.5–1 día |
| 1 | Fundación + DB | Alta | 1–2 días |
| 2 | Auth + Seguridad + Auditoría Base | Alta | 2–3 días |
| 3 | Organización (Users/Roles/Dept/Loc) | Alta | 1–2 días |
| 4 | Activos + Asignaciones | Alta | 2–3 días |
| 5 | Software/Licencias/Proveedores/Contratos | Alta | 1–2 días |
| 6 | Help Desk + Solicitudes + SLA | Alta | 2–3 días |
| 7 | Mantenimientos/Cambios/Compras | Media | 1–2 días |
| 8 | Auditoría/Reportes/Dashboard/Config | Alta | 1–2 días |
| 9 | Frontend Core + Módulos | Alta | 4–6 días |
| 10 | Archivos/Notificaciones/Caché/Import-Export | Media | 2–3 días |
| 11 | Calidad/Tests/CI-CD/Observabilidad | Alta | 2–3 días |
| 12 | Despliegue/Docs/Go-Live | Alta | 1–2 días |

**Total estimado:** ~24–40 días hábiles (aprox. 5–8 semanas), dependiendo de equipo y alcance.

---

## 18. Riesgos y Mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|
| Complejidad RBAC/permisos | Media | Alto | Implementar matriz granular desde Fase 2, tests de autorización por rol. |
| SLA con horarios laborales | Media | Alto | Configurable (tabla SlaConfigurations), tests con fechas/horarios. |
| Historial de asignaciones (inmutable) | Baja | Alto | Nunca actualizar registros existentes; crear nuevos. Auditoría. |
| Gestión de licencias (clave sensible) | Baja | Alto | No loguear, no exponer en respuestas, cifrar en BD o restringir acceso, marcar IsEncrypted. |
| Performance Dashboard/Reportes | Media | Medio | Agregados con proyecciones, índices, caché, paginación, export asíncrono. |
| Migraciones EF en producción | Media | Alto | Revisar migraciones en Staging, backups previos, estrategia forward-only, rollback plan. |
| Hardening seguridad (OWASP) | Media | Alto | Rate limiting, headers, CORS estricto, validación exhaustiva, no exponer stack traces. |

---

## 19. Definition of Done (DoD)

Para cada módulo/tarea se considera completado cuando:
- Código cumple Clean Architecture, SOLID, convenciones C#/Angular.
- Sin secretos en código; configuración por ambiente.
- DTOs (no entidades EF) en API responses.
- Validaciones con FluentValidation y mensajes consistentes.
- Autorización verificada por permisos/roles.
- Auditoría registrada en operaciones críticas.
- Tests unitarios + integración mínimos pasan.
- Lint (`npm run lint`/analyzers) y typecheck (`tsc --noEmit`) sin errores.
- Swagger/OpenAPI actualizado y documentado.
- Manejo global de errores con traceId.
- Soft delete aplicado donde corresponde (histórico).
- Fechas almacenadas en UTC; presentación con zona horaria configurada.
- PR revisado, aprobado y mergeado a `develop`.

---

## 20. Próximos Pasos Inmediatos

1. **Aprobar plan** con stakeholders (arquitectura, producto, TI).
2. **Crear backlog** (Issues) por fases/módulos con estimados y etiquetas.
3. **Configurar entorno** (Fase 0): Docker Compose, user-secrets, branches.
4. **Iniciar Fase 1**: solución + capas + DbContext + migración inicial.
5. **Sembrar permisos/roles** en Fase 2 antes de construir CRUDs con autorización.

**Plan creado:** `docs/IMPLEMENTATION_PLAN.md`