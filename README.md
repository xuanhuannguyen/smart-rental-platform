# smart-rental-platform

Nền tảng quản lý thuê trọ / tìm trọ - ASP.NET Core Web API + React + PostgreSQL.

## Tech stack

- Backend: ASP.NET Core Web API, Entity Framework Core, PostgreSQL.
- Frontend: React, TypeScript, Vite.
- Database/local infra: Docker Compose.

## Project structure

```txt
client/                         React + Vite frontend
server/
  SmartRentalPlatform.slnx       Backend solution
  src/
    SmartRentalPlatform.Api
    SmartRentalPlatform.Application
    SmartRentalPlatform.Contracts
    SmartRentalPlatform.Domain
    SmartRentalPlatform.Infrastructure
docs/                            Architecture notes and interval plans
docker/                          Docker-related files
```

## Backend convention

- `Api` exposes controllers, middleware, authentication, Swagger and HTTP concerns.
- `Application` owns business use cases and service interfaces.
- `Domain` owns entities and enums.
- `Infrastructure` implements persistence, storage, security and external services.
- `Contracts` owns API DTOs. DTO folders that are split into `Requests` and `Responses` use matching namespaces, for example `SmartRentalPlatform.Contracts.Auth.Requests`.

## Frontend convention

```txt
client/src/
  app/             app shell, providers, router
  config/          runtime config
  features/        feature-owned pages, components, services, types
  shared/          reusable API, UI, feedback and utility code
  styles/          global styles
```

Feature folders should use `pages`, `components`, `hooks`, `services`, `types` and `utils` when the feature grows large enough.

## Run locally

Start database:

```powershell
docker compose up -d
```

Run backend:

```powershell
dotnet build server/SmartRentalPlatform.slnx
dotnet run --project server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj
```

Run frontend:

```powershell
cd client
npm install
npm run dev
```

Production build checks:

```powershell
dotnet build server/SmartRentalPlatform.slnx
cd client
npm run build
```

## Refactor notes

Internal 2 structural refactor follows `docs/interval-plan/OverallRefactorExecutionPlan.md`.
