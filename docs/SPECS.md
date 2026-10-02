# SOFTWARE DESIGN DOCUMENT (SDD)

## Sistema Administrativo de Tecnologías de Información — TI Admin

**Versión:** 1.0  
**Estado:** Documento de Diseño Técnico  
**Fecha:** Octubre 2026  
**Backend:** ASP.NET Core 10 / .NET 10  
**Frontend:** Angular 22.x  
**Base de datos:** Microsoft SQL Server 2022+  
**Arquitectura:** Web API + SPA  
**Patrón arquitectónico:** Clean Architecture / Modular Monolith  
**API:** REST / JSON / OpenAPI  
**Autenticación:** JWT / OpenID Connect  
**ORM:** Entity Framework Core 10  

---

# 1. Propósito del documento

Este documento define la arquitectura, diseño técnico, componentes, estándares, interfaces, seguridad, modelo de datos y lineamientos de desarrollo para la construcción de un **Sistema Administrativo de Tecnologías de Información (TI Admin)**.

El sistema tendrá como objetivo centralizar y administrar los recursos, servicios, usuarios, activos tecnológicos, licencias, proveedores, incidencias, solicitudes, mantenimientos y demás procesos relacionados con la gestión del área de Tecnología de Información.

El SDD servirá como referencia técnica para:

- Desarrollo Backend.
- Desarrollo Frontend.
- Diseño de base de datos.
- Integraciones.
- Pruebas QA.
- Seguridad.
- DevOps.
- Despliegue.
- Mantenimiento y evolución futura.

---

# 2. Objetivos del sistema

## 2.1 Objetivo general

Desarrollar una plataforma web empresarial que permita administrar de manera centralizada los recursos tecnológicos y los servicios de TI de la organización.

## 2.2 Objetivos específicos

El sistema deberá permitir:

1. Administrar usuarios y permisos.
2. Administrar departamentos y ubicaciones.
3. Registrar y controlar activos tecnológicos.
4. Controlar asignaciones de equipos a usuarios.
5. Administrar software y licenciamiento.
6. Controlar garantías y contratos.
7. Administrar proveedores tecnológicos.
8. Gestionar tickets de soporte.
9. Gestionar solicitudes de servicio.
10. Registrar mantenimientos preventivos y correctivos.
11. Gestionar incidencias.
12. Registrar cambios tecnológicos.
13. Mantener historial y trazabilidad de las operaciones.
14. Generar reportes administrativos.
15. Proporcionar dashboards de indicadores de TI.
16. Permitir auditoría de las operaciones.
17. Facilitar futuras integraciones con servicios corporativos.

---

# 3. Alcance funcional

El sistema estará compuesto inicialmente por los siguientes módulos:

| Módulo | Prioridad | Descripción |
|---|---:|---|
| Autenticación | Alta | Inicio de sesión y control de identidad |
| Usuarios | Alta | Administración de usuarios |
| Roles y permisos | Alta | RBAC |
| Organización | Alta | Departamentos, áreas y ubicaciones |
| Activos TI | Alta | Inventario tecnológico |
| Asignaciones | Alta | Equipos asignados a usuarios |
| Software | Alta | Catálogo y licencias |
| Help Desk | Alta | Tickets e incidencias |
| Solicitudes | Alta | Solicitudes de servicios |
| Mantenimientos | Media | Mantenimientos preventivos/correctivos |
| Proveedores | Media | Gestión de proveedores |
| Contratos | Media | Contratos y garantías |
| Compras TI | Media | Solicitudes y adquisiciones |
| Cambios | Media | Gestión de cambios |
| Documentos | Media | Gestión documental |
| Reportes | Alta | Reportes operativos y gerenciales |
| Auditoría | Alta | Bitácora de operaciones |
| Configuración | Alta | Parámetros generales |

---

# 4. Arquitectura general

## 4.1 Modelo arquitectónico

Se utilizará una arquitectura **Modular Monolith con Clean Architecture**, evitando inicialmente la complejidad operacional de microservicios.

La solución se dividirá conceptualmente en:

```text
                    ┌─────────────────────┐
                    │      Usuario        │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │      Angular       │
                    │      Frontend      │
                    └──────────┬──────────┘
                               │ HTTPS
                               ▼
                    ┌─────────────────────┐
                    │   ASP.NET Core 10   │
                    │       Web API       │
                    └──────────┬──────────┘
                               │
               ┌───────────────┼────────────────┐
               │               │                │
               ▼               ▼                ▼
        ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
        │ Application │ │  Identity   │ │  Modules    │
        │   Services  │ │  Security   │ │   TI        │
        └──────┬──────┘ └─────────────┘ └──────┬──────┘
               │                               │
               └───────────────┬───────────────┘
                               ▼
                    ┌─────────────────────┐
                    │ Entity Framework    │
                    │ Core 10             │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌─────────────────────┐
                    │   SQL Server 2022   │
                    └─────────────────────┘
```

ASP.NET Core proporciona soporte integrado para DI, configuración por ambientes, logging, middleware, seguridad y APIs HTTP, elementos que serán utilizados como fundamentos de la solución. 

---

# 5. Stack tecnológico

## 5.1 Backend

- .NET 10 LTS
- ASP.NET Core 10
- C# 14
- Entity Framework Core 10
- ASP.NET Core Identity
- JWT Bearer Authentication
- OpenID Connect
- REST API
- OpenAPI
- FluentValidation
- AutoMapper o Mapster
- Serilog
- Health Checks
- xUnit
- Moq / NSubstitute

ASP.NET Core 10 soporta generación de documentación OpenAPI para APIs y permite documentar los endpoints de la aplicación. 

## 5.2 Frontend

- Angular 22.x
- TypeScript
- RxJS
- Angular Router
- Angular Reactive Forms
- Angular Signals cuando resulte apropiado
- Angular HTTP Client
- Angular Guards
- Angular Interceptors
- Componentes standalone
- CSS/SCSS
- Librería UI corporativa

## 5.3 Base de datos

- Microsoft SQL Server 2022+
- Entity Framework Core Migrations
- Índices optimizados
- Foreign Keys
- Constraints
- Stored Procedures únicamente cuando exista una necesidad justificada de rendimiento o integración.

## 5.4 Infraestructura

Producción:

```text
Internet / LAN
      │
      ▼
Reverse Proxy / Load Balancer
      │
      ├───────────────┐
      ▼               ▼
 Angular SPA       ASP.NET Core API
                      │
                      ▼
                 SQL Server
```

Opcionalmente:

```text
                    ┌──────────────┐
                    │ Redis        │
                    │ Cache        │
                    └──────┬───────┘
                           │
Angular ──> API ───────────┼──> SQL Server
                           │
                           ├──> File Storage
                           │
                           ├──> Email
                           │
                           └──> Identity Provider
```

---

# 6. Principios arquitectónicos

La solución deberá cumplir los siguientes principios:

### 6.1 Separation of Concerns

Cada componente deberá tener una responsabilidad claramente definida.

### 6.2 Dependency Inversion

Las capas superiores no deberán depender directamente de implementaciones concretas.

### 6.3 API First

Los contratos de API deberán definirse antes o simultáneamente con la implementación del frontend.

### 6.4 Secure by Design

La seguridad será considerada desde el diseño y no como una característica posterior.

### 6.5 Auditability

Las operaciones administrativas relevantes deberán ser trazables.

### 6.6 Stateless API

La API deberá ser esencialmente stateless para permitir escalabilidad horizontal.

### 6.7 Configuration by Environment

No deberán existir credenciales ni configuraciones sensibles dentro del código fuente.

---

# 7. Estructura de la solución Backend

Se recomienda la siguiente estructura:

```text
src/
│
├── TIAdmin.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Filters/
│   ├── Extensions/
│   ├── OpenApi/
│   └── Program.cs
│
├── TIAdmin.Application/
│   ├── Common/
│   ├── DTOs/
│   ├── Interfaces/
│   ├── Behaviors/
│   ├── Features/
│   │   ├── Users/
│   │   ├── Assets/
│   │   ├── Tickets/
│   │   ├── Software/
│   │   ├── Vendors/
│   │   └── Reports/
│   └── Validators/
│
├── TIAdmin.Domain/
│   ├── Entities/
│   ├── Enums/
│   ├── ValueObjects/
│   ├── Events/
│   ├── Exceptions/
│   └── Interfaces/
│
├── TIAdmin.Infrastructure/
│   ├── Persistence/
│   ├── Identity/
│   ├── Repositories/
│   ├── Services/
│   ├── Email/
│   ├── Storage/
│   └── Integrations/
│
└── TIAdmin.Tests/
    ├── Unit/
    ├── Integration/
    └── Functional/
```

---

# 8. Capas de aplicación

## 8.1 API

Responsabilidades:

- HTTP.
- Routing.
- Autenticación.
- Autorización.
- Validación inicial.
- Serialización.
- Manejo de errores.
- OpenAPI.
- Rate limiting.

La API no deberá contener lógica de negocio compleja.

---

# 9. Application Layer

Esta capa contiene los casos de uso del sistema.

Ejemplo:

```text
CreateAssetCommand
UpdateAssetCommand
AssignAssetCommand
CreateTicketCommand
CloseTicketCommand
CreateMaintenanceCommand
CreateSoftwareLicenseCommand
```

Se recomienda organizar las funcionalidades por **Feature**, en lugar de una estructura únicamente técnica.

Ejemplo:

```text
Features/
   Assets/
      Create/
      Update/
      Delete/
      Assign/
      Search/

   Tickets/
      Create/
      Update/
      Assign/
      Resolve/
      Close/
```

---

# 10. Domain Layer

Contendrá las reglas fundamentales del negocio.

Ejemplos de entidades:

```text
User
Department
Location
Asset
AssetType
AssetAssignment
Software
SoftwareLicense
Ticket
TicketComment
TicketAttachment
Maintenance
Vendor
Contract
PurchaseRequest
ChangeRequest
AuditLog
```

Las entidades deberán contener únicamente reglas de negocio y no depender de:

- ASP.NET Core.
- Entity Framework.
- Angular.
- SQL Server.
- Servicios externos.

---

# 11. Infrastructure Layer

Responsabilidades:

- Persistencia.
- Entity Framework Core.
- Identity.
- Repositorios.
- Email.
- almacenamiento de archivos.
- Integraciones externas.
- Servicios de terceros.

---

# 12. API REST

La API deberá utilizar una estructura consistente:

```text
/api/v1/auth
/api/v1/users
/api/v1/roles
/api/v1/departments
/api/v1/locations
/api/v1/assets
/api/v1/asset-types
/api/v1/assignments
/api/v1/software
/api/v1/licenses
/api/v1/tickets
/api/v1/requests
/api/v1/maintenance
/api/v1/vendors
/api/v1/contracts
/api/v1/purchases
/api/v1/changes
/api/v1/reports
/api/v1/audit
```

## 12.1 Convenciones HTTP

| Operación | Método |
|---|---|
| Consultar colección | GET |
| Consultar registro | GET |
| Crear | POST |
| Actualización completa | PUT |
| Actualización parcial | PATCH |
| Eliminar | DELETE |

---

# 13. Respuestas API

Las respuestas deberán utilizar un formato consistente.

Ejemplo:

```json
{
  "success": true,
  "data": {},
  "message": null,
  "errors": []
}
```

Para errores:

```json
{
  "success": false,
  "data": null,
  "message": "No fue posible procesar la solicitud.",
  "errors": [
    {
      "code": "ASSET_NOT_FOUND",
      "message": "El activo solicitado no existe."
    }
  ]
}
```

---

# 14. Paginación

Las consultas de listas deberán soportar:

```text
?page=1&pageSize=25
```

Respuesta:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 25,
  "totalItems": 150,
  "totalPages": 6
}
```

También deberán soportarse:

- búsqueda;
- filtros;
- ordenamiento;
- rango de fechas;
- filtros por estado.

---

# 15. Autenticación y autorización

## 15.1 Autenticación

Se recomienda soportar:

1. Usuario/contraseña corporativa.
2. Microsoft Entra ID / OpenID Connect.
3. JWT para consumo de API.

La arquitectura deberá permitir incorporar posteriormente otros proveedores de identidad sin modificar la lógica de negocio.

## 15.2 Autorización

Se utilizará RBAC:

```text
Usuario
   │
   ├── Roles
   │     ├── Permisos
   │     └── Permisos
   │
   └── Permisos
```

Roles iniciales:

```text
SUPER_ADMIN
TI_ADMIN
TI_SUPPORT
TI_ASSET_MANAGER
TI_MANAGER
AUDITOR
USER
```

---

# 16. Matriz de permisos

Los permisos deberán manejarse independientemente de los roles.

Ejemplo:

```text
ASSETS.VIEW
ASSETS.CREATE
ASSETS.UPDATE
ASSETS.DELETE
ASSETS.ASSIGN
ASSETS.UNASSIGN

TICKETS.VIEW
TICKETS.CREATE
TICKETS.UPDATE
TICKETS.ASSIGN
TICKETS.RESOLVE
TICKETS.CLOSE

USERS.VIEW
USERS.CREATE
USERS.UPDATE
USERS.DISABLE

REPORTS.VIEW
REPORTS.EXPORT

AUDIT.VIEW
```

Esto permitirá crear nuevos roles sin modificar código.

---

# 17. Seguridad

La aplicación deberá implementar como mínimo:

- HTTPS obligatorio.
- JWT/OIDC.
- Password hashing.
- MFA mediante proveedor de identidad cuando esté disponible.
- Protección contra CSRF cuando aplique.
- Protección contra XSS.
- Validación de entradas.
- Protección contra SQL Injection mediante EF Core/parametrización.
- Rate limiting.
- CORS restringido.
- Headers de seguridad.
- Control de expiración de tokens.
- Revocación de sesiones cuando sea necesario.
- Auditoría.
- Gestión segura de secretos.

Nunca deberán almacenarse:

```text
Passwords
Connection strings
JWT secrets
API keys
Client secrets
```

directamente en el repositorio.

---

# 18. Auditoría

Todas las operaciones administrativas críticas deberán generar registros de auditoría.

Entidad:

```text
AuditLog
```

Campos:

```text
Id
UserId
Action
EntityName
EntityId
OldValues
NewValues
IpAddress
UserAgent
Timestamp
CorrelationId
```

Ejemplo:

```text
Usuario: admin
Acción: UPDATE
Entidad: Asset
ID: 1524

Antes:
Status = "Assigned"

Después:
Status = "Maintenance"
```

La auditoría deberá ser de solo lectura para usuarios normales.

---

# 19. Módulo de activos TI

## 19.1 Funcionalidades

El módulo deberá permitir:

- Crear activos.
- Editar activos.
- Desactivar activos.
- Consultar activos.
- Filtrar activos.
- Asignar activos.
- Desasignar activos.
- Registrar movimientos.
- Registrar mantenimiento.
- Consultar historial.
- Registrar garantías.
- Registrar documentos.

## 19.2 Tipos de activos

Inicialmente:

```text
Laptop
Desktop
Monitor
Printer
Server
Network Device
Mobile Device
UPS
Storage
Telephone
Peripheral
Other
```

## 19.3 Datos principales

```text
Asset
├── AssetCode
├── SerialNumber
├── Name
├── AssetType
├── Brand
├── Model
├── PurchaseDate
├── PurchaseCost
├── WarrantyExpiration
├── Status
├── Location
├── Department
└── Notes
```

Estados:

```text
AVAILABLE
ASSIGNED
MAINTENANCE
REPAIR
RETIRED
LOST
DISPOSED
```

---

# 20. Asignación de activos

Una asignación deberá registrar:

```text
AssetId
UserId
AssignmentDate
ReturnDate
AssignedBy
ReturnedBy
ConditionAtAssignment
ConditionAtReturn
Notes
```

Nunca se deberá sobrescribir el historial anterior.

Ejemplo:

```text
Laptop LT-001

2026-01-10 → Juan Pérez
2026-07-20 → María López
2026-09-15 → Disponible
```

---

# 21. Help Desk

## 21.1 Ticket

Entidad:

```text
Ticket
```

Campos:

```text
Id
TicketNumber
Title
Description
CategoryId
Priority
Status
RequesterId
AssignedToId
DepartmentId
CreatedAt
UpdatedAt
ResolvedAt
ClosedAt
```

## 21.2 Estados

```text
NEW
OPEN
IN_PROGRESS
WAITING_USER
WAITING_VENDOR
RESOLVED
CLOSED
CANCELLED
```

## 21.3 Prioridades

```text
LOW
MEDIUM
HIGH
CRITICAL
```

## 21.4 Flujo

```text
NEW
 ↓
OPEN
 ↓
IN_PROGRESS
 ↓
WAITING_USER ──┐
 ↓              │
IN_PROGRESS <──┘
 ↓
RESOLVED
 ↓
CLOSED
```

---

# 22. SLA

El sistema deberá permitir configurar SLA por:

```text
Categoría
Prioridad
Tipo de solicitud
Horario laboral
Departamento
```

Ejemplo:

| Prioridad | Tiempo respuesta | Tiempo resolución |
|---|---:|---:|
| Critical | 15 min | 4 horas |
| High | 30 min | 8 horas |
| Medium | 4 horas | 24 horas |
| Low | 8 horas | 72 horas |

Los valores anteriores deberán ser configurables y no estar hardcodeados.

---

# 23. Software y licenciamiento

El módulo deberá permitir:

- Catálogo de software.
- Fabricante.
- Versión.
- Tipo de licencia.
- Cantidad adquirida.
- Cantidad utilizada.
- Fecha de adquisición.
- Fecha de vencimiento.
- Costo.
- Proveedor.
- Contrato.
- Evidencia documental.

Ejemplo:

```text
SoftwareLicense
├── SoftwareId
├── VendorId
├── LicenseType
├── LicenseKey
├── Quantity
├── UsedQuantity
├── PurchaseDate
├── ExpirationDate
└── Cost
```

El sistema deberá generar alertas de:

- Licencias próximas a vencer.
- Licencias vencidas.
- Sobreasignación.
- Baja utilización.

---

# 24. Proveedores

El módulo deberá administrar:

```text
Vendor
├── Name
├── TaxId
├── ContactName
├── Email
├── Phone
├── Address
├── Status
└── Notes
```

Relaciones:

```text
Vendor
 ├── Contracts
 ├── SoftwareLicenses
 ├── Purchases
 ├── Maintenance
 └── Assets
```

---

# 25. Contratos

El sistema deberá controlar:

- Número de contrato.
- Proveedor.
- Tipo.
- Fecha inicial.
- Fecha final.
- Valor.
- Renovación.
- Responsable.
- Documentos.
- Estado.

Alertas:

```text
90 días antes
60 días antes
30 días antes
15 días antes
7 días antes
```

Los períodos deberán ser configurables.

---

# 26. Mantenimientos

Tipos:

```text
PREVENTIVE
CORRECTIVE
EMERGENCY
```

Estados:

```text
PLANNED
SCHEDULED
IN_PROGRESS
COMPLETED
CANCELLED
```

Un mantenimiento deberá relacionarse con:

```text
Asset
Technician
Vendor
Ticket
Cost
Date
Findings
Actions
Recommendations
```

---

# 27. Gestión de cambios

El módulo Change Management deberá registrar:

```text
ChangeRequest
├── Number
├── Title
├── Description
├── Type
├── Risk
├── Impact
├── RequestedBy
├── AssignedTo
├── PlannedDate
├── ImplementationDate
├── RollbackPlan
├── Status
└── Approval
```

Estados:

```text
DRAFT
REQUESTED
UNDER_REVIEW
APPROVED
REJECTED
IMPLEMENTING
COMPLETED
ROLLED_BACK
CLOSED
```

---

# 28. Dashboard

El dashboard principal deberá mostrar indicadores como:

```text
┌────────────────┬────────────────┬────────────────┐
│ Activos        │ Tickets        │ Licencias      │
│ 1,245          │ 38 abiertos    │ 94% vigentes   │
└────────────────┴────────────────┴────────────────┘

┌────────────────┬────────────────┬────────────────┐
│ SLA            │ Mantenimiento  │ Contratos      │
│ 96.4%          │ 12 pendientes  │ 5 por vencer   │
└────────────────┴────────────────┴────────────────┘
```

Indicadores sugeridos:

- Total de activos.
- Activos por estado.
- Activos por departamento.
- Tickets abiertos.
- Tickets vencidos.
- Cumplimiento de SLA.
- Tickets por prioridad.
- Licencias próximas a vencer.
- Contratos próximos a vencer.
- Mantenimientos pendientes.
- Costos de TI.
- Activos por ubicación.

---

# 29. Frontend Angular

La aplicación Angular deberá seguir una arquitectura modular.

```text
src/app/
│
├── core/
│   ├── auth/
│   ├── guards/
│   ├── interceptors/
│   ├── services/
│   └── models/
│
├── shared/
│   ├── components/
│   ├── directives/
│   ├── pipes/
│   └── utilities/
│
├── layout/
│   ├── header/
│   ├── sidebar/
│   └── footer/
│
└── features/
    ├── dashboard/
    ├── users/
    ├── assets/
    ├── tickets/
    ├── software/
    ├── vendors/
    ├── maintenance/
    ├── contracts/
    ├── reports/
    └── administration/
```

---

# 30. Diseño UI

La interfaz deberá ser:

- Responsive.
- Empresarial.
- Limpia.
- Consistente.
- Accesible.
- Orientada a productividad.

Componentes comunes:

```text
DataTable
SearchBox
FilterPanel
Modal
Drawer
Form
DatePicker
Dropdown
StatusBadge
ConfirmDialog
Toast
Pagination
FileUploader
Timeline
DashboardCard
Chart
```

---

# 31. Navegación

Menú propuesto:

```text
Dashboard

Gestión TI
├── Activos
├── Asignaciones
├── Software
├── Licencias
├── Mantenimientos
└── Cambios

Soporte
├── Tickets
├── Solicitudes
├── Categorías
└── SLA

Administración
├── Usuarios
├── Roles
├── Departamentos
├── Ubicaciones
├── Proveedores
└── Configuración

Gestión
├── Contratos
├── Compras
└── Documentos

Reportes
├── Activos
├── Tickets
├── Licencias
├── Costos
└── Auditoría
```

---

# 32. Manejo de estado

El estado deberá administrarse preferentemente mediante:

- Signals para estado local y derivado.
- RxJS para flujos asíncronos.
- Servicios para estado compartido simple.
- Store global únicamente cuando la complejidad lo justifique.

No deberá utilizarse un estado global innecesario.

---

# 33. Comunicación Angular → API

Todas las llamadas deberán centralizarse mediante servicios:

```text
AssetService
TicketService
UserService
LicenseService
VendorService
ReportService
```

Ejemplo conceptual:

```typescript
getAssets(params): Observable<PagedResult<Asset>>
createAsset(request): Observable<Asset>
updateAsset(id, request): Observable<Asset>
deleteAsset(id): Observable<void>
```

El frontend no deberá construir URLs de API directamente dentro de componentes.

---

# 34. Interceptors

Se utilizarán interceptors para:

- Adjuntar token.
- Manejar errores.
- Agregar correlation ID.
- Manejar respuestas 401/403.
- Mostrar estados globales de carga cuando corresponda.

---

# 35. Modelo de datos

Modelo conceptual:

```text
User
 │
 ├── Department
 ├── Location
 ├── Roles
 ├── Tickets
 └── AssetAssignments
             │
             ▼
           Asset
             │
       ┌─────┼──────────┐
       ▼     ▼          ▼
 Maintenance Software  Documents
                  │
                  ▼
                License

Vendor
 ├── Contract
 ├── License
 ├── Purchase
 └── Maintenance
```

---

# 36. Entidades principales

## Users

```text
Users
- Id
- Username
- Email
- FirstName
- LastName
- EmployeeCode
- DepartmentId
- LocationId
- IsActive
- CreatedAt
- UpdatedAt
```

## Departments

```text
Departments
- Id
- Code
- Name
- ManagerId
- IsActive
```

## Locations

```text
Locations
- Id
- Code
- Name
- Address
- IsActive
```

## Assets

```text
Assets
- Id
- AssetCode
- SerialNumber
- AssetTypeId
- Brand
- Model
- PurchaseDate
- PurchaseCost
- WarrantyExpiration
- Status
- LocationId
- CreatedAt
- UpdatedAt
```

## Tickets

```text
Tickets
- Id
- TicketNumber
- Title
- Description
- CategoryId
- Priority
- Status
- RequesterId
- AssignedToId
- CreatedAt
- UpdatedAt
- ResolvedAt
- ClosedAt
```

---

# 37. Soft Delete

Las entidades administrativas no deberán eliminarse físicamente cuando exista información histórica asociada.

Se recomienda:

```text
IsDeleted
DeletedAt
DeletedBy
```

Ejemplo:

Un proveedor que tiene contratos históricos no deberá desaparecer de la base de datos.

---

# 38. Fechas y zona horaria

Todas las fechas almacenadas en base de datos deberán utilizar UTC.

La presentación al usuario deberá convertirlas a la zona horaria configurada para la organización.

Configuración inicial:

```text
America/Managua
```

---

# 39. Archivos y documentos

Los documentos no deberán almacenarse directamente en SQL Server salvo casos específicos.

Se recomienda:

```text
Database
    │
    └── Document metadata

File Storage
    │
    └── Actual file
```

Metadata:

```text
Document
- Id
- FileName
- MimeType
- Size
- StoragePath
- EntityName
- EntityId
- UploadedBy
- UploadedAt
```

---

# 40. Notificaciones

El sistema deberá soportar:

### Email

Para:

- Ticket creado.
- Ticket asignado.
- Ticket actualizado.
- Ticket vencido.
- Licencia por vencer.
- Contrato por vencer.
- Mantenimiento próximo.
- Cambio aprobado/rechazado.

### Notificaciones internas

Opcionalmente:

```text
Notification
- Id
- UserId
- Type
- Title
- Message
- IsRead
- CreatedAt
```

---

# 41. Logging

Se utilizará logging estructurado.

Cada request deberá incluir:

```text
CorrelationId
UserId
Endpoint
HttpMethod
StatusCode
Duration
IpAddress
```

Los logs deberán poder enviarse posteriormente a una plataforma centralizada de observabilidad.

---

# 42. Health Checks

La API deberá implementar:

```text
/health
/health/live
/health/ready
```

Se deberán comprobar como mínimo:

- API.
- Base de datos.
- Storage.
- Servicios externos críticos.

---

# 43. Manejo global de errores

No deberán enviarse stack traces al usuario final.

Formato:

```json
{
  "success": false,
  "message": "Ha ocurrido un error inesperado.",
  "traceId": "abc123"
}
```

El `traceId` deberá permitir encontrar el error correspondiente en los logs.

---

# 44. Performance

Objetivos iniciales:

| Métrica | Objetivo |
|---|---:|
| API simple | < 500 ms |
| Consulta paginada | < 1 s |
| Dashboard | < 2 s |
| Login | < 2 s |
| Exportación pequeña | < 5 s |

Los objetivos deberán validarse mediante pruebas de rendimiento reales.

---

# 45. Caché

Se utilizará caché para información relativamente estática:

```text
AssetTypes
TicketCategories
Departments
Locations
Configuration
Permissions
```

Opciones:

```text
IMemoryCache
```

para escenarios simples, o:

```text
Redis
```

cuando se requiera caché distribuido.

---

# 46. Integraciones futuras

La arquitectura deberá permitir integración con:

```text
Microsoft Entra ID
Microsoft 365
Active Directory
Google Workspace
ERP
HR System
Email
Teams
CMDB
Monitoring
Backup systems
Network monitoring
Procurement
```

Las integraciones deberán implementarse mediante interfaces:

```text
IIdentityProvider
IEmailService
IFileStorage
IAssetDiscoveryService
INotificationService
```

---

# 47. Importación y exportación

El sistema deberá soportar:

### Importación

```text
Excel
CSV
```

Casos:

- Usuarios.
- Activos.
- Software.
- Licencias.
- Proveedores.

### Exportación

```text
Excel
CSV
PDF
```

Las exportaciones grandes deberán procesarse de forma asíncrona.

---

# 48. Reportes

Reportes iniciales:

1. Inventario de activos.
2. Activos por usuario.
3. Activos por departamento.
4. Activos por ubicación.
5. Activos en mantenimiento.
6. Software instalado.
7. Licencias vencidas.
8. Licencias próximas a vencer.
9. Tickets por período.
10. Tickets por técnico.
11. Tickets por categoría.
12. Cumplimiento de SLA.
13. Costos de TI.
14. Contratos por vencer.
15. Historial de movimientos.
16. Auditoría.

---

# 49. Testing

## Unit Tests

Se deberá probar:

- Reglas de negocio.
- Validators.
- Services.
- Domain entities.
- Casos de uso.

## Integration Tests

Se deberá probar:

- API.
- Base de datos.
- Autenticación.
- Autorización.
- Integraciones.

## Frontend Tests

Se deberá probar:

- Componentes.
- Services.
- Guards.
- Interceptors.
- Formularios.
- Flujos críticos.

## E2E

Flujos mínimos:

```text
Login
Crear activo
Asignar activo
Crear ticket
Asignar ticket
Resolver ticket
Cerrar ticket
Crear licencia
Generar reporte
```

---

# 50. CI/CD

Pipeline recomendado:

```text
Developer
    │
    ▼
Git
    │
    ▼
Pull Request
    │
    ├── Build
    ├── Unit Tests
    ├── Static Analysis
    ├── Security Scan
    └── Integration Tests
    │
    ▼
Development
    │
    ▼
QA
    │
    ▼
Staging
    │
    ▼
Production
```

---

# 51. Estrategia Git

Se recomienda:

```text
main
develop
feature/*
bugfix/*
release/*
hotfix/*
```

Los cambios deberán entrar a `main` mediante Pull Request.

---

# 52. Versionamiento API

La API deberá utilizar versionamiento:

```text
/api/v1/
```

Cuando existan cambios incompatibles:

```text
/api/v2/
```

No se deberán introducir breaking changes silenciosamente.

---

# 53. Configuración por ambientes

Ambientes:

```text
Development
QA
Staging
Production
```

Cada ambiente deberá tener configuración independiente.

Ejemplo:

```text
ConnectionStrings
Jwt
Storage
Email
ExternalServices
Logging
```

Los secretos deberán administrarse mediante un gestor seguro.

---

# 54. Docker

Se recomienda contenerizar:

```text
Frontend
Backend
```

Ejemplo:

```text
docker-compose
├── frontend
├── api
├── sqlserver
└── redis
```

Para producción, SQL Server deberá evaluarse según la infraestructura corporativa disponible.

---

# 55. Despliegue

Arquitectura recomendada:

```text
                  Internet / LAN
                       │
                       ▼
                 Reverse Proxy
                       │
          ┌────────────┴────────────┐
          ▼                         ▼
    Angular SPA                ASP.NET API
                                    │
                         ┌──────────┼──────────┐
                         ▼          ▼          ▼
                      SQL Server  Redis     Storage
```

---

# 56. Backup

La solución deberá contemplar:

### Base de datos

- Full backup.
- Differential backup.
- Transaction log backup.
- Retención configurable.

### Archivos

Los documentos deberán tener estrategia independiente de respaldo.

### Configuración

Los archivos de configuración no deberán depender exclusivamente de backups manuales.

---

# 57. Recuperación ante desastres

Deberán definirse:

```text
RPO
RTO
```

Valores iniciales sugeridos para evaluación:

```text
RPO: 24 horas
RTO: 8 horas
```

Los valores definitivos deberán ser aprobados por la organización según criticidad.

---

# 58. Requisitos no funcionales

## Seguridad

El sistema deberá proteger:

- Información de usuarios.
- Información de activos.
- Información financiera.
- Credenciales.
- Documentos.

## Disponibilidad

Objetivo inicial:

```text
99.5%
```

## Escalabilidad

La API deberá poder escalar horizontalmente.

## Mantenibilidad

El código deberá seguir:

- SOLID.
- Clean Code.
- Convenciones de C#.
- Convenciones Angular.
- Code Reviews.

---

# 59. Reglas de desarrollo Backend

1. No utilizar lógica de negocio dentro de Controllers.
2. No acceder directamente a DbContext desde Controllers.
3. Utilizar DTOs.
4. No devolver entidades EF directamente.
5. Validar todos los comandos.
6. Utilizar async/await.
7. Evitar consultas N+1.
8. Utilizar `CancellationToken`.
9. Utilizar `AsNoTracking()` para consultas de solo lectura cuando corresponda.
10. Registrar operaciones críticas.
11. No incluir secretos en código.
12. Mantener métodos pequeños y enfocados.

---

# 60. Reglas de desarrollo Angular

1. Utilizar componentes standalone.
2. Evitar componentes excesivamente grandes.
3. Separar presentación y lógica.
4. Utilizar servicios para comunicación con API.
5. Centralizar manejo de errores.
6. Utilizar lazy loading por módulos funcionales.
7. Evitar suscripciones manuales innecesarias.
8. Utilizar Signals cuando aporten claridad.
9. Mantener componentes reutilizables.
10. Evitar lógica de negocio duplicada.

---

# 61. Reglas de base de datos

1. Todas las tablas deberán tener Primary Key.
2. Las relaciones deberán utilizar Foreign Keys.
3. Los campos críticos deberán tener índices.
4. Evitar duplicación innecesaria.
5. Utilizar nombres consistentes.
6. Registrar fechas de creación y modificación.
7. Utilizar migraciones.
8. No modificar producción manualmente sin procedimiento controlado.

---

# 62. Convenciones de nombres

## C#

```text
PascalCase
camelCase
```

Ejemplo:

```csharp
public class AssetService
{
    public async Task<AssetDto> GetAssetAsync(...)
}
```

## SQL

```text
PascalCase
```

Ejemplo:

```text
Assets
AssetAssignments
SoftwareLicenses
```

## Angular

```text
asset-list.component.ts
asset-detail.component.ts
asset.service.ts
```

---

# 63. Correlation ID

Cada request deberá manejar un identificador:

```text
X-Correlation-ID
```

Este identificador deberá propagarse entre:

```text
Frontend
   ↓
API
   ↓
Application
   ↓
Infrastructure
   ↓
External Services
```

Esto permitirá rastrear una operación completa.

---

# 64. Observabilidad

La solución deberá prepararse para:

```text
Logs
Metrics
Traces
Health Checks
```

Métricas iniciales:

```text
Requests/sec
Response time
Error rate
Database latency
Active users
Tickets created
Tickets resolved
```

---

# 65. Documentación API

La API deberá generar documentación OpenAPI.

Cada endpoint deberá documentar:

- Descripción.
- Parámetros.
- Request.
- Response.
- Códigos HTTP.
- Errores.
- Requisitos de autorización.

Ejemplo:

```text
POST /api/v1/assets

201 Created
400 Bad Request
401 Unauthorized
403 Forbidden
409 Conflict
422 Unprocessable Entity
```

---

# 66. HTTP Status Codes

Se deberán utilizar correctamente:

```text
200 OK
201 Created
204 No Content
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Entity
429 Too Many Requests
500 Internal Server Error
```

---

# 67. Roadmap de implementación

## Fase 1 — Foundation

```text
Solution
Authentication
Authorization
Database
API
Angular
Logging
CI/CD
```

## Fase 2 — Core Administration

```text
Users
Roles
Departments
Locations
Configuration
Audit
```

## Fase 3 — Asset Management

```text
Assets
Assignments
Inventory
Maintenance
Documents
```

## Fase 4 — Service Management

```text
Tickets
Requests
Categories
SLA
Notifications
```

## Fase 5 — Software Management

```text
Software
Licenses
Compliance
Renewals
```

## Fase 6 — Vendors & Contracts

```text
Vendors
Contracts
Purchases
Renewals
```

## Fase 7 — Reporting

```text
Dashboard
Reports
Exports
KPIs
```

## Fase 8 — Integrations

```text
Entra ID
Email
Microsoft 365
ERP
Monitoring
HR
```

---

# 68. MVP

El primer MVP deberá incluir:

### Seguridad

- Login.
- Roles.
- Permisos.
- Auditoría.

### Administración

- Usuarios.
- Departamentos.
- Ubicaciones.

### Activos

- Inventario.
- Tipos de activos.
- Asignaciones.
- Historial.

### Help Desk

- Tickets.
- Categorías.
- Prioridades.
- Estados.
- Asignación.
- Comentarios.

### Dashboard

- Indicadores principales.

### Reportes

- Inventario.
- Tickets.
- Asignaciones.

---

# 69. Criterios de aceptación técnicos

El sistema podrá considerarse técnicamente listo para producción cuando:

- La solución compile sin errores.
- Todos los tests críticos estén aprobados.
- Las migraciones de base de datos estén versionadas.
- La API tenga documentación OpenAPI.
- La autenticación esté implementada.
- Los permisos estén implementados.
- Las operaciones críticas tengan auditoría.
- Los endpoints críticos tengan pruebas.
- El frontend maneje errores de API.
- Exista pipeline CI/CD.
- Existan backups.
- Existan health checks.
- No existan secretos en el repositorio.
- Se haya realizado análisis de seguridad.
- Se haya realizado prueba de rendimiento.
- Se haya validado recuperación desde backup.

---

# 70. Decisiones arquitectónicas

| Decisión | Selección |
|---|---|
| Backend | ASP.NET Core 10 |
| Runtime | .NET 10 LTS |
| Lenguaje | C# 14 |
| Frontend | Angular 22.x |
| Arquitectura | Modular Monolith |
| Patrón | Clean Architecture |
| API | REST |
| Documentación | OpenAPI |
| ORM | EF Core 10 |
| DB | SQL Server 2022+ |
| Auth | OIDC / JWT |
| Autorización | RBAC |
| Logging | Structured Logging |
| Cache | Redis opcional |
| Files | Object/File Storage |
| CI/CD | Pipeline automatizado |
| Containers | Docker |
| Testing | Unit + Integration + E2E |

---

# 71. Evolución futura

La arquitectura deberá permitir evolucionar posteriormente hacia:

```text
                     API Gateway
                         │
          ┌──────────────┼───────────────┐
          ▼              ▼               ▼
     Asset Service   Helpdesk Service   License Service
          │              │               │
          └──────────────┼───────────────┘
                         ▼
                    Shared Services
```

La extracción a microservicios únicamente deberá realizarse cuando exista una necesidad real de:

- Escalabilidad independiente.
- Equipos de desarrollo independientes.
- Límites de dominio claramente establecidos.
- Necesidades de disponibilidad diferentes.
- Integraciones que justifiquen separación.

No se recomienda comenzar el proyecto como microservicios.

---

# 72. Resumen de arquitectura final

```text
                         ┌─────────────────────┐
                         │       Usuarios      │
                         └──────────┬──────────┘
                                    │
                                  HTTPS
                                    │
                                    ▼
                         ┌─────────────────────┐
                         │   Angular 22 SPA    │
                         │                     │
                         │ Dashboard           │
                         │ Assets              │
                         │ Tickets             │
                         │ Licenses            │
                         │ Users               │
                         │ Reports             │
                         └──────────┬──────────┘
                                    │ REST/JSON
                                    ▼
                   ┌─────────────────────────────────┐
                   │       ASP.NET Core 10 API       │
                   │                                 │
                   │ Authentication                  │
                   │ Authorization                   │
                   │ Controllers / Endpoints         │
                   │ Application Services             │
                   │ Domain                           │
                   │ Validation                       │
                   │ Audit                            │
                   └────────────────┬────────────────┘
                                    │
                   ┌────────────────┼────────────────┐
                   │                │                │
                   ▼                ▼                ▼
             ┌───────────┐   ┌────────────┐   ┌────────────┐
             │ SQL Server│   │   Redis    │   │  Storage   │
             │           │   │  Optional  │   │  Documents │
             └───────────┘   └────────────┘   └────────────┘
                                   
                   ┌────────────────────────────────┐
                   │        External Services       │
                   │                                │
                   │ Entra ID / AD                   │
                   │ Email                           │
                   │ Microsoft 365                   │
                   │ ERP                             │
                   │ Monitoring                      │
                   └────────────────────────────────┘
```

# 73. Referencias técnicas

La implementación deberá basarse en la documentación oficial de Microsoft para ASP.NET Core/.NET 10 y en la documentación oficial de Angular para la versión seleccionada.

Las versiones exactas de paquetes NuGet/NPM deberán fijarse durante la creación del proyecto y mantenerse mediante actualización controlada.

---

# 74. Próximo nivel de especificación

Este SDD constituye la **arquitectura base**. Para iniciar directamente el desarrollo, deberá complementarse con los siguientes documentos:

1. **BRD — Business Requirements Document**
2. **FRD — Functional Requirements Document**
3. **Database Design — ERD completo**
4. **API Specification — OpenAPI/Swagger**
5. **UI/UX Specification**
6. **Security Design**
7. **Deployment Architecture**
8. **Test Plan**
9. **Product Backlog**
10. **User Stories + Acceptance Criteria**

La siguiente versión del diseño deberá convertir cada módulo anterior en especificaciones detalladas de **pantallas, entidades, campos, relaciones, endpoints, DTOs, validaciones, permisos y reglas de negocio**, de manera que un equipo de desarrollo pueda comenzar a implementar el sistema sin tener que tomar decisiones arquitectónicas importantes durante la programación.