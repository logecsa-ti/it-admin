# DEVELOPMENT MEMORY - TI Admin

**Proyecto:** Sistema Administrativo de Tecnologías de Información (TI Admin)
**Referencia:** `docs/SPECS.md`, `docs/IMPLEMENTATION_PLAN.md`
**Fecha de inicio:** 2026-10-02
**Última actualización:** 2026-10-02
**Estado general:** En progreso
**Fase actual:** Fase 0 - Preparación y Fundación (completada) → Fase 1 - Infraestructura Base

---

## 0. Bloqueos del entorno

| Recurso | Estado | Impacto |
|---|---|---|
| .NET SDK 10 | ❌ NO INSTALADO | Bloquea Fase 1 completa (creación de solución, proyectos, migraciones EF) |
| Node.js | ✅ v24.21.0 | Disponible para Angular |
| Docker CLI | ✅ instalado | Disponible |
| Docker daemon | ❌ NO CORRIENDO | Bloquea docker-compose (SQL Server local) |
| Git | ✅ v2.55.0 | Operativo |

**Acción requerida del usuario:** instalar .NET 10 SDK e iniciar Docker Desktop antes de iniciar Fase 1.

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
| Fase actual | **Fase 1 - Infraestructura Base** (lista para iniciar) |
| Fases completadas | 1 / 12 (Fase 0) |
| Tareas completadas (Fase 0) | 4 / 4 |
| Estado | En progreso |

### Progreso global

| Fase | Estado |
|---|---|
| 0 - Preparación y Fundación | ✅ Completada |
| 1 - Infraestructura Base | ⏸️ Bloqueada (requiere .NET 10 SDK) |
| 2 - Auth/Authz/Seguridad | ⬜ Pendiente |
| 3 - Núcleo Organizacional | ⬜ Pendiente |
| 4 - Activos y Asignaciones | ⬜ Pendiente |
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
**Estado:** ⏸️ Bloqueada - requiere instalar .NET 10 SDK y Docker Desktop

Tareas previstas:
- [ ] 1.1 Crear `TIAdmin.sln` + 5 proyectos
- [ ] 1.2 Agregar dependencias NuGet (EF Core 10, Identity, JWT, FluentValidation, Mapster, Serilog, HealthChecks, OpenAPI)
- [ ] 1.3 Configuración Options Pattern + `appsettings.*.json`
- [ ] 1.4 `TIAdminDbContext` + Fluent API + convenciones (soft delete, UTC timestamps)
- [ ] 1.5 Seed de roles/permisos + usuario SUPER_ADMIN (solo dev)
- [ ] 1.6 Migración `InitialCreate`
- [ ] 1.7 Health checks + `/health`

### Fase 2 - Autenticación, Autorización y Seguridad
**Estado:** Pendiente

### Fase 3 - Núcleo Organizacional
**Estado:** Pending

### Fase 4 - Activos TI y Asignaciones
**Estado:** Pending

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

---

## 6. Comandos Útiles

### Backend (.NET)
```powershell
dotnet build .\TIAdmin.sln
dotnet test .\TIAdmin.sln
dotnet ef migrations add <Name> --project TIAdmin.Infrastructure --startup-project TIAdmin.Api
dotnet ef database update --project TIAdmin.Infrastructure --startup-project TIAdmin.Api
dotnet run --project TIAdmin.Api
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
| B-01 | **.NET 10 SDK no instalado** en la máquina | Bloquea Fase 1 completa | Instalar .NET 10 SDK (https://dotnet.microsoft.com/download) |
| B-02 | **Docker Desktop daemon apagado** | Bloquea SQL Server local vía docker-compose | Iniciar Docker Desktop |

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

1. **USUARIO**: instalar .NET 10 SDK e iniciar Docker Desktop (desbloquea B-01, B-02)
2. Ejecutar `docker compose up -d sqlserver` para provisionar la BD de desarrollo
3. Iniciar **Fase 1**: crear `TIAdmin.sln` + 5 proyectos con `dotnet new`
4. Agregar paquetes NuGet y configurar Options Pattern + `appsettings`
5. Crear `TIAdminDbContext` con convenciones (soft delete, UTC) y migración `InitialCreate`
6. Actualizar esta memoria tras cada cambio significativo