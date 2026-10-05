# TI Admin - Sistema Administrativo de Tecnologías de Información

Sistema web empresarial para centralizar y administrar recursos tecnológicos, servicios de TI, activos, licenciamiento, tickets de soporte, mantenimientos y auditoría.

## Stack Tecnológico

- **Backend:** ASP.NET Core 10 / .NET 10 / C# 14
- **Frontend:** Angular 22.x / TypeScript
- **Base de Datos:** Microsoft SQL Server 2022+
- **Arquitectura:** Modular Monolith + Clean Architecture
- **API:** REST / JSON / OpenAPI (Swagger)
- **Autenticación:** JWT / OpenID Connect
- **ORM:** Entity Framework Core 10

## Requisitos Previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js LTS](https://nodejs.org/) + npm
- [Angular CLI 22](https://angular.io/cli)
- [SQL Server 2022+](https://www.microsoft.com/sql-server) (o Docker)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (opcional)

## Documentación

- [Especificación Técnica (SPECS.md)](docs/SPECS.md)
- [Plan de Implementación (IMPLEMENTATION_PLAN.md)](docs/IMPLEMENTATION_PLAN.md)
- [Memoria de Desarrollo (DEV_MEMORY.md)](docs/DEV_MEMORY.md)
- [Arquitectura](docs/ARCHITECTURE.md) _(en desarrollo)_
- [Convenciones](docs/CONVENTIONS.md) _(en desarrollo)_

## Configuración de Desarrollo

### Opción 1: Docker Compose (Recomendado)

```powershell
docker compose up -d
```

### Opción 2: Local

1. **Base de datos SQL Server**: Asegurar instancia disponible
2. **Backend API**:
```powershell
dotnet run --project TIAdmin.Api
```
3. **Frontend**:
```powershell
cd ti-admin-web
npm ci
npm start
```

## Variables de Entorno y Secretos

Los secretos sensibles NO deben commitearse. Usar:
- **Development**: .NET User Secrets (`dotnet user-secrets`)
- **Otros ambientes**: Variables de entorno / Azure Key Vault

## Scripts Útiles

```powershell
# Build solución
dotnet build .\TIAdmin.sln

# Ejecutar tests
dotnet test .\TIAdmin.sln

# Migraciones EF Core
dotnet ef migrations add <Nombre> --project TIAdmin.Infrastructure --startup-project TIAdmin.Api
dotnet ef database update --project TIAdmin.Infrastructure --startup-project TIAdmin.Api

# Frontend (desde ti-admin-web; detalles en ti-admin-web/README.md)
npm run lint:types
npm run build
npx ng test --watch=false
```

## Contribución

Ver [CONTRIBUTING.md](CONTRIBUTING.md).

## Licencia

_En definición por organización._
