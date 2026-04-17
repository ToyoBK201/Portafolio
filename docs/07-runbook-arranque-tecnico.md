# Runbook de arranque tecnico MVP

## 1) Instalar herramientas

1. Instalar `.NET SDK 8`.
2. Instalar `Node.js LTS` (incluye npm).
3. Instalar Angular CLI: `npm install -g @angular/cli`.
4. Instalar Docker Desktop (opcional recomendado).
5. Instalar `sqlcmd` o SSMS/Azure Data Studio.

Validar:

```powershell
./scripts/check-prerequisites.ps1
```

## 2) Levantar SQL Server local

```powershell
docker compose -f database/docker-compose.sqlserver.yml up -d
./scripts/apply-schema.ps1
```

## 3) Ejecutar API

```powershell
dotnet restore SolicitudesTechGov.sln
dotnet run --project apps/api/SolicitudesTechGov.Api.csproj
```

Validar endpoint:

- `GET /api/v1/health`

## 4) Ejecutar Angular

```powershell
cd apps/web
npm install
npm run start
```

La SPA consume `/api/v1/health` via proxy para validar conectividad.

## 5) Vertical slice inicial (P0)

- Caso de uso implementado: crear solicitud en borrador.
- Endpoint: `POST /api/v1/requests`.
- Validaciones de dominio incluidas:
  - `title` entre 5 y 200
  - `description` entre 20 y 8000
  - `businessJustification` minimo 20
  - `requestingUnitId` mayor a 0
  - `requesterUserId` requerido
