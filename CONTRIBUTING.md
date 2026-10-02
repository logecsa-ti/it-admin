# Contribución a TI Admin

## Convenciones de Commits

Usar [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` nueva funcionalidad
- `fix:` corrección de bugs
- `docs:` documentación
- `refactor:` refactorización sin cambio funcional
- `test:` pruebas
- `chore:` tareas de mantenimiento/configuración
- `perf:` mejoras de rendimiento
- `style:` formato/estilo

## Estrategia de Branching

- `main`: rama estable (producción)
- `develop`: rama de integración
- `feature/*`: nuevas funcionalidades
- `bugfix/*`: correcciones
- `release/*`: preparación de releases
- `hotfix/*`: correcciones urgentes en producción

## Pull Requests

- Todo cambio debe pasar por PR hacia `develop` (y luego a `main` vía release)
- PR debe incluir descripción clara, referencia a issue (si aplica)
- Debe pasar: build, tests, lint/typecheck
- Requiere al menos 1 aprobación (recomendado)

## Estándares

- Backend: Clean Architecture, SOLID, convenciones C# (.editorconfig)
- Frontend: Angular standalone, ESLint, Prettier, TypeScript strict
- No commitear secretos (usar user-secrets/env vars)
- Seguir `docs/CONVENTIONS.md` cuando esté disponible
- Mantener cobertura de tests adecuada en código nuevo

## Definition of Done

Ver `docs/IMPLEMENTATION_PLAN.md` sección 19.
