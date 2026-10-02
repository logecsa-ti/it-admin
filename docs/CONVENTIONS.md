# Convenciones de Desarrollo - TI Admin

Referencia: `docs/SPECS.md` (secciones 59-62, 69).

---

## 1. C# / Backend

### 1.1 Nomenclatura

| Elemento | Convención | Ejemplo |
|---|---|---|
| Namespaces | PascalCase, coincide con proyecto | `TIAdmin.Application.Features.Assets.Create` |
| Clases / interfaces | PascalCase | `CreateAssetCommand`, `IAssetRepository` |
| Métodos | PascalCase | `GetAssetAsync` |
| Variables / parámetros | camelCase | `assetId` |
| Campos privados | `_camelCase` | `_assetRepository` |
| Constantes | PascalCase | `DefaultPageSize` |
| Interfaces | Prefijo `I` | `IEmailService` |

### 1.2 Reglas de código

1. **Prohibido** lógica de negocio en `Controllers`.
2. **Prohibido** acceso directo a `DbContext` desde `Controllers` o `Application`.
3. Siempre usar **DTOs**; nunca devolver entidades EF directamente.
4. Todos los comandos/querys se validan con **FluentValidation**.
5. `async`/`await` en toda operación I/O; propagar `CancellationToken`.
6. Evitar N+1: usar `Select()` con proyección, `Include`/`ThenInclude` solo cuando se requiere la entidad.
7. `AsNoTracking()` en consultas de solo lectura.
8. Métodos pequeños y con una sola responsabilidad.
9. `ConfigureAwait(false)` no requerido en ASP.NET Core (no hay sync context).
10. Usar `required`, `init` y nullable reference types (`<Nullable>enable</Nullable>`).

### 1.3 Entidades de dominio

- Reglas de negocio encapsuladas en la entidad (métodos como `AssignTo()`, `MarkAsAvailable()`).
- Sin dependencias de ASP.NET Core, EF Core, Angular, SQL Server o servicios externos.
- Enums de dominio en `TIAdmin.Domain/Enums`.
- Colecciones expuestas como `IReadOnlyCollection<T>`.

### 1.4 EF Core

- Configuración mediante **Fluent API** en `IEntityTypeConfiguration<T>` (nunca Data Annotations salvo excepciones justificadas).
- Un `DbContext` por agregado/módulo cuando el tamaño lo justifique.
- `SaveChangesAsync` con **interceptor** para `AuditLog`, `IsDeleted`, `CreatedAt`/`UpdatedAt`.
- Migraciones siempre revisadas antes de merge a `main`.

---

## 2. SQL / Base de datos

### 2.1 Nomenclatura

| Elemento | Convención | Ejemplo |
|---|---|---|
| Tablas | PascalCase plural | `Assets`, `AssetAssignments` |
| Columnas | PascalCase | `AssetCode`, `CreatedAt` |
| PK | `Id` (int identity) | `Id` |
| FK | `<Entidad>Id` | `DepartmentId` |
| Índices | `IX_<Tabla>_<Columnas>` | `IX_Assets_SerialNumber` |
| Unique | `UQ_<Tabla>_<Columnas>` | `UQ_Users_Email` |
| Check | `CK_<Tabla>_<Regla>` | `CK_Assets_PurchaseCost` |

### 2.2 Reglas

1. Toda tabla con **Primary Key**.
2. Relaciones con **Foreign Keys** reales (no solo navegación lógica).
3. Índices en columnas usadas frequently en filtros/búsquedas/joins.
4. Sin duplicación innecesaria; normalizar.
5. `CreatedAt`/`UpdatedAt` obligatorios en entidades de negocio.
6. Solo **migraciones EF Core**; nunca modificar esquema a mano en producción.
7. Fechas en **UTC** (`datetime2`).

---

## 3. Angular / Frontend

### 3.1 Nomenclatura

| Elemento | Convención | Ejemplo |
|---|---|---|
| Componentes | `kebab-case` + `.component.ts` | `asset-list.component.ts` |
| Servicios | `kebab-case.service.ts` | `asset.service.ts` |
| Guards | `kebab-case.guard.ts` | `permission.guard.ts` |
| Interceptors | `kebab-case.interceptor.ts` | `auth.interceptor.ts` |
| Clases/Types | PascalCase | `Asset`, `PagedResult<T>` |
| Variables | camelCase | `assetId` |
| Constantes | UPPER_SNAKE | `API_URL` |

### 3.2 Reglas

1. **Solo componentes standalone** (sin `NgModule`).
2. Componentes pequeños; nada de "god components" (límite recomendado: ~300 líneas).
3. Separar presentación de lógica (contenedores/presentacionales cuando aplique).
4. Toda llamada HTTP pasa por un **servicio**; prohibido construir URLs dentro de componentes.
5. Manejo de errores centralizado en interceptor + servicio base.
6. **Lazy loading** por ruta de feature (`loadComponent` / `loadChildren`).
7. Evitar suscripciones manuales innecesarias: preferir `async` pipe, signals, `takeUntilDestroyed`.
8. `ChangeDetectionStrategy.OnPush` por defecto.
9. Signals para estado local/derivado; RxJS para flujos asíncronos.
10. Estado global (store) solo si la complejidad lo justifica.
11. Prohibido `any` implícito; `strict: true` en `tsconfig.json`.
12. Accesibilidad: labels, roles ARIA, navegación por teclado en componentes compartidos.

### 3.3 Estructura

```text
src/app/
├── core/       # auth, guards, interceptors, services base, models
├── shared/     # components, directives, pipes, utilities
├── layout/     # header, sidebar, footer
└── features/   # dashboard, users, assets, tickets, software, ...
```

Regla de dependencias: `features` → `shared` → `core`. Nunca al revés.

---

## 4. API REST

### 4.1 Rutas

- Prefijo obligatorio: `/api/v1/...` (versionado en URL, SPECS.md sección 52).
- Kebab-case en segmentos multi-palabra: `/asset-types`, `/ticket-categories`.

### 4.2 Verbos HTTP

| Operación | Método |
|---|---|
| Consultar colección | GET |
| Consultar registro | GET |
| Crear | POST |
| Actualización completa | PUT |
| Actualización parcial | PATCH |
| Eliminar (soft) | DELETE |

### 4.3 Formato de respuesta

Éxito:

```json
{
  "success": true,
  "data": {},
  "message": null,
  "errors": []
}
```

Error:

```json
{
  "success": false,
  "data": null,
  "message": "No fue posible procesar la solicitud.",
  "errors": [{ "code": "ASSET_NOT_FOUND", "message": "El activo solicitado no existe." }],
  "traceId": "abc123"
}
```

`traceId` solo se expone en errores inesperados (5xx) y nunca se envían stack traces al usuario final.

### 4.4 Paginación

Query: `?page=1&pageSize=25&search=&sortBy=&sortDirection=`
Respuesta: `{ "items": [], "page": 1, "pageSize": 25, "totalItems": 150, "totalPages": 6 }`

Límites: `pageSize` mínimo 1, máximo 200 (configurable).

### 4.5 Códigos de estado

| Código | Uso |
|---|---|
| 200 | Éxito con cuerpo |
| 201 | Creado (`Location` header) |
| 204 | Sin contenido (DELETE/PATCH) |
| 400 | Error de validación / negocio |
| 401 | No autenticado / token expirado |
| 403 | Autenticado sin permiso |
| 404 | No encontrado |
| 409 | Conflicto (duplicado) |
| 422 | Regla de negocio no satisfecha |
| 429 | Rate limit excedido |
| 500 | Error inesperado (con `traceId`) |

---

## 5. Commits y Pull Requests

- **Conventional Commits**: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, `perf:`, `style:`.
- Ramas: `main`, `develop`, `feature/*`, `bugfix/*`, `release/*`, `hotfix/*`.
- PR hacia `develop`; `main` solo vía release/merge verificado.
- Todo PR pasa build + tests + lint + typecheck.

Ejemplos:

```text
feat(assets): add asset assignment endpoint with history
fix(tickets): prevent transition from CLOSED to OPEN
docs: update ERD with license alerts
```

---

## 6. Secretos y Configuración

- **Nunca** commitear: passwords, connection strings, JWT secrets, API keys, client secrets.
- Desarrollo: `dotnet user-secrets`.
- QA/Staging/Production: variables de entorno o gestor de secretos (Azure Key Vault).
- `appsettings.json` solo contiene valores por defecto **no sensibles**.
- `.env` no se commitea; `.env.example` sí.

---

## 7. Verificación antes de commit

```powershell
dotnet build .\TIAdmin.sln          # Backend build
dotnet test .\TIAdmin.sln           # Tests
cd ti-admin-web; npm run lint       # Lint
cd ti-admin-web; npm run build     # Typecheck + build
```
