# Base de datos — MVP

## Script principal

| Archivo | Descripción |
| ------- | ----------- |
| [sql/001_schema_mvp.sql](sql/001_schema_mvp.sql) | Creación de tablas, catálogos sembrados, índices y claves foráneas (SQL Server). |

## Documentación

- [docs/modelo-datos-mvp.md](../docs/modelo-datos-mvp.md) — modelo lógico, correspondencia con la API y diagrama ER.

## Uso sugerido

1. Crear una base de datos vacía en la instancia SQL Server de desarrollo.  
2. Ejecutar `001_schema_mvp.sql`.  
3. Insertar unidades (`OrganizationalUnit`), usuarios (`AppUser`) y roles (`UserRole`) para el entorno.  

## Docker local (opcional recomendado)

```powershell
docker compose -f database/docker-compose.sqlserver.yml up -d
```

Luego aplicar esquema:

```powershell
./scripts/apply-schema.ps1
```

No se incluye borrado físico de solicitudes: preservar trazabilidad y filas de auditoría; cualquier baja debe ser política explícita (post-MVP).
