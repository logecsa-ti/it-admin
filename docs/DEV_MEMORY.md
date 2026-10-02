# DEVELOPMENT MEMORY - TI Admin

**Proyecto:** Sistema Administrativo de Tecnologías de Información (TI Admin)
**Referencia:** `docs/SPECS.md`, `docs/IMPLEMENTATION_PLAN.md`
**Fecha de inicio:** 2026-10-02
**Última actualización:** 2026-10-02
**Estado general:** En progreso
**Fase actual:** Fase 0 - Preparación y Fundación

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
| Fase actual | **Fase 0 - Preparación y Fundación** |
| Fases completadas | 0 / 12 |
| Tareas completadas (Fase 0) | 0 / 4 |
| Estado | In Progress |

---

## 3. Log de Cambios

| Fecha | Fase | Acción | Archivos afectados | Notas |
|---|---|---|---|---|
| 2026-10-02 | Inicial | Creado `IMPLEMENTATION_PLAN.md` | `docs/IMPLEMENTATION_PLAN.md` | Plan completo de implementación por fases |
| 2026-10-02 | Fase 0 | Creado `DEV_MEMORY.md` | `docs/DEV_MEMORY.md` | Estructura inicial de memoria de desarrollo |

---

## 4. Progreso por Fases

### Fase 0 - Preparación y Fundación
**Estado:** In Progress | **Tareas:** 0/4 completadas

- [ ] **0.1 Repositorio y branching** - Verificar estado Git, ramas, convenciones, CONTRIBUTING/README
- [ ] **0.2 Entorno de desarrollo** - .NET/Node/Angular/SQL/Docker, user-secrets, docker-compose
- [ ] **0.3 Documentación y reglas** - CONVENTIONS.md, ARCHITECTURE.md, backlog
- [ ] **0.4 Análisis y modelado** - ERD inicial entidades principales

**Entregables pendientes:** Entorno listo, backlog, ERD preliminar, docs base.

### Fase 1 - Infraestructura Base (Solución, Capas, Persistencia)
**Estado:** Pending

### Fase 2 - Autenticación, Autorización y Seguridad
**Estado:** Pending

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

Ninguno por el momento.

---

## 8. Próximos Pasos

1. Completar **Fase 0.1** - Verificar estado Git y ramas existentes
2. Completar **Fase 0.2** - Configurar entorno y docker-compose para desarrollo
3. Completar **Fase 0.3** - Crear `CONVENTIONS.md` y `ARCHITECTURE.md`
4. Completar **Fase 0.4** - Crear ERD preliminar
5. Actualizar memoria tras cada cambio
6. Avanzar a **Fase 1** una vez completada Fase 0