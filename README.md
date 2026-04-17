# Sistema de Solicitudes Tecnologicas Gubernamentales

Monorepo MVP con arquitectura `Angular + .NET + SQL Server`, alineado a la documentacion funcional y tecnica del proyecto.

## Estructura base

- `apps/api`: API .NET (entrypoint).
- `apps/web`: cliente Angular.
- `src/backend/domain`: reglas de dominio, enums y entidades.
- `src/backend/application`: casos de uso, DTOs y puertos.
- `src/backend/infrastructure`: implementaciones de persistencia y wiring.
- `src/backend/contracts`: contrato OpenAPI.
- `database/sql`: DDL SQL Server.
- `tests/backend`: pruebas del backend.
- `scripts`: utilidades de verificacion y setup local.

## Documentos MVP

| Documento | Contenido |
| --- | --- |
| [docs/01-alcance-congelado.md](docs/01-alcance-congelado.md) | Inclusión/exclusión, decisiones (interno, notificaciones, adjuntos) |
| [docs/02-matriz-rbac.md](docs/02-matriz-rbac.md) | Roles × pantallas × API |
| [docs/03-estados-y-contrato-api.md](docs/03-estados-y-contrato-api.md) | Enums, transiciones, contrato REST |
| [docs/04-plantillas-campos-por-tipo.md](docs/04-plantillas-campos-por-tipo.md) | Campos obligatorios y validaciones por tipo |
| [docs/05-auditoria-esquema.md](docs/05-auditoria-esquema.md) | Auditoría, `PayloadDiff`, correlación, retención |
| [docs/06-backlog-ui-y-criterios.md](docs/06-backlog-ui-y-criterios.md) | Backlog priorizado y criterios de aceptación |
| [docs/documento-formal-mvp-solicitudes-tech-gov.md](docs/documento-formal-mvp-solicitudes-tech-gov.md) | Especificación formal unificada |
| [docs/modelo-datos-mvp.md](docs/modelo-datos-mvp.md) | Modelo de datos SQL Server + ER |
| [database/sql/001_schema_mvp.sql](database/sql/001_schema_mvp.sql) | Script DDL del esquema MVP |

## Prerrequisitos

- .NET SDK 8
- Node.js LTS + npm
- Angular CLI
- Docker Desktop (opcional, recomendado para SQL local)
- SQL Server Command Line Tools (`sqlcmd`) o SSMS/Azure Data Studio

Verifica prerequisitos:

```powershell
./scripts/check-prerequisites.ps1
```

## Base de datos local

Levanta SQL Server local con Docker:

```powershell
docker compose -f database/docker-compose.sqlserver.yml up -d
```

Aplica el esquema MVP:

```powershell
./scripts/apply-schema.ps1
```

## Backend API (arranque inicial)

```powershell
dotnet restore SolicitudesTechGov.sln
dotnet run --project apps/api/SolicitudesTechGov.Api.csproj
```

Health check:

- `GET http://localhost:5000/api/v1/health`

Endpoint inicial P0:

- `POST /api/v1/requests` crea solicitud en estado `Draft`.

## Frontend Angular (arranque inicial)

```powershell
cd apps/web
npm install
npm run start
```

La app usa proxy a la API por `proxy.conf.json` (`/api -> http://localhost:5000`).

## Calidad y flujo Git

- Ver [CONTRIBUTING.md](CONTRIBUTING.md) para ramas, commits y PR checklist.
- Template de PR en `.github/pull_request_template.md`.
