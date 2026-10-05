# TI Admin Web

SPA de TI Admin (Fase 9): Angular 22 con componentes standalone, signals, Angular Material y SCSS. Consume la API REST de `src/TIAdmin.Api` (`/api/v1`).

## Requisitos

- Node.js `^22.22.3` o `^24.15.0` (lo exige Angular CLI 22).
- La API en ejecucion para desarrollar (`dotnet run --project src/TIAdmin.Api`, en `http://localhost:5142`). Su CORS ya permite `http://localhost:4200`.

## Comandos

```powershell
npm ci                               # dependencias exactas del lockfile
npm start                            # http://localhost:4200 contra la API local
npm run build                        # produccion (dist/ti-admin-web/browser)
npx ng build --configuration qa      # tambien: staging, development
npx ng test --watch=false            # pruebas unitarias (Vitest + jsdom)
npm run lint:types                   # chequeo estricto de tipos
```

CI (`.github/workflows/ci.yml`, job `frontend`) ejecuta `npm ci`, la verificacion de permisos, `lint:types`, las pruebas, el build de produccion y `npm audit`.

## Contrato con la API

Los modelos no se escriben a mano: salen del backend.

| Archivo | Origen | Regenerar |
|---|---|---|
| `openapi/tiadmin-api.json` | Swagger de la API (solo en Development) | `npm run api:snapshot` con la API levantada |
| `src/app/core/models/api.generated.ts` | `openapi/tiadmin-api.json` | `npm run api:types` |
| `src/app/core/auth/permissions.generated.ts` | `Permissions.cs` del backend | `npm run api:permissions` (CI falla si queda desfasado) |

`core/models/index.ts` reexporta los DTO con nombres cortos. Al agregar un valor de enum en el backend, regenerar los tipos y agregar su etiqueta en `shared/labels/enum-labels.ts`: `enum-labels.spec.ts` falla si falta alguna.

## Estructura

```
src/app/
  core/       ApiService, AuthService, guards, interceptores (auth, error, correlation id, loading), configuracion publica
  shared/     tabla de datos, catalogos, dialogos de formulario, documentos adjuntos, badges, pipes (appDate, money), utilidades
  layout/     shell, header, sidebar, breadcrumbs y el menu (navigation.ts)
  features/   una carpeta por modulo, cargada en diferido desde app.routes.ts
```

Alias de importacion: `@core/*`, `@shared/*`, `@layout/*`, `@features/*`, `@env/*`.

## Decisiones

- **Sesion** (ADR-038): el access token vive solo en memoria y el refresh token rotativo en `sessionStorage`. El interceptor renueva una sola vez ante varios 401 simultaneos.
- **Permisos**: el menu, las rutas (`permissionGuard` con `data.permissions`) y los botones (`*appCan`) usan los permisos del perfil. Es solo UX: la API valida cada operacion. Donde la visibilidad depende de los datos (tickets, "mis activos"), la ruta solo exige sesion y la API filtra.
- **Fechas** (ADR-005): los instantes llegan en UTC y se muestran en la zona de negocio (`App.TimeZone` de `/configuration/public`) con el pipe `appDate`. Las fechas de calendario (`yyyy-MM-dd`) se muestran sin conversion.
- **Errores**: `errorInterceptor` muestra un toast con el mensaje del sobre `ApiResponse` y, en errores 5xx, la referencia de correlacion. Las pantallas que muestran el error en linea marcan la peticion con `SILENT_ERRORS`.
- **URLs**: los componentes nunca arman URLs. Los servicios de cada feature componen `ResourceApi` sobre `ApiService`, que toma la base de `environment.apiUrl`. Fuera de desarrollo la SPA se publica en el mismo dominio que la API (`/api/v1`, proxy inverso).
- **Textos**: la interfaz esta en espanol, sin tildes en el codigo fuente.
