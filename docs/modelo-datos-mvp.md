# Modelo de datos — MVP (SQL Server)

**Versión:** 1.0  
**Alineación:** [documento-formal-mvp-solicitudes-tech-gov.md](./documento-formal-mvp-solicitudes-tech-gov.md), documentos 03 (contrato) y 05 (auditoría).

**Scripts:** [../database/sql/001_schema_mvp.sql](../database/sql/001_schema_mvp.sql)

---

## 1. Alcance del modelo

El esquema físico cubre:

- Catálogos **congelados** con identificadores numéricos iguales al contrato (`RequestStatus` 1–10, `RequestType` 1–7, `Priority` 1–4, `AppRole` 1–7).  
- Catálogo **maestro editable** de unidades organizativas.  
- **Usuarios** y **asignación de roles** con ámbito opcional de unidad para el Coordinador de área.  
- **Solicitudes** con `SpecificPayloadJson` para el bloque específico por tipo (validado en aplicación).  
- **Adjuntos** (metadatos; binarios fuera de la base de datos).  
- **Comentarios** (públicos o internos).  
- **Auditoría** append-only según el modelo lógico acordado.

Las **transiciones** válidas se implementan en la capa de dominio (no hay tabla de transiciones en MVP), coherente con la sección 11 del documento formal.

---

## 2. Diagrama entidad-relación (lógico)

```mermaid
erDiagram
    RequestStatus ||--o{ Request : status
    RequestType   ||--o{ Request : type
    Priority      ||--o{ Request : priority
    OrganizationalUnit ||--o{ Request : unit
    AppUser       ||--o{ Request : requester
    AppUser       ||--o{ Request : analyst
    AppUser       ||--o{ Request : implementer
    Request       ||--o{ RequestAttachment : has
    Request       ||--o{ RequestComment : has
    Request       ||--o{ AuditLog : logged
    AppUser       ||--o{ UserRole : has
    AppRole       ||--o{ UserRole : grants
    OrganizationalUnit ||--o{ UserRole : scopes
    RequestStatus ||--o{ AuditLog : from
    RequestStatus ||--o{ AuditLog : to
    AppUser       ||--o{ AuditLog : actor
```

---

## 3. Correspondencia API ↔ columna (principal)

| Concepto API | Tabla.columna / notas |
| ------------ | --------------------- |
| `RequestStatus` | `Request.StatusId` → `RequestStatus.StatusId` (`Code` = valor string API). |
| `RequestType` | `Request.RequestTypeId` → `RequestType.RequestTypeId`. |
| `Priority` | `Request.PriorityId` → `Priority.PriorityId`. |
| `Role` | `AppRole.Code`; asignación en `UserRole.RoleId`. |
| `specificPayload` | `Request.SpecificPayloadJson` (JSON; validación por tipo en aplicación). |
| `rowVersion` / ETag | `Request.RowVersion` (concurrency optimista). |

---

## 4. Decisiones de diseño

1. **IDs:** `UNIQUEIDENTIFIER` con `NEWSEQUENTIALID()` en tablas de alto volumen (sugerencia de índice secuencial en SQL Server). Las entidades de catálogo fijo usan `TINYINT`.  
2. **Folio:** único cuando no es nulo; suele asignarse al salir de `Draft` o al enviar (definición en servicio de dominio).  
3. **Coordinador de área:** `UserRole.OrganizationalUnitId` acota el ámbito cuando el rol es `AreaCoordinator`; puede existir la asignación de rol sin unidad según política institucional (validar en aplicación).  
4. **Política `CancelByRequester`:** usar `AssignedAnalystUserId IS NULL` en `Submitted` como regla típica (validación en dominio).  
5. **Auditoría:** sin `UPDATE`/`DELETE` en operación normal; aplicación solo `INSERT`. Opcionalmente añadir restricción o rol de BD en despliegue.  
6. **`AuditLog.EntityId`:** tipo `NVARCHAR(64)` para albergar GUID u otros identificadores según entidad.

---

## 5. Aplicación del script

1. Crear base de datos vacía (colación recomendada: `Modern_Spanish_CI_AS` o la estándar institucional).  
2. Ejecutar `001_schema_mvp.sql` con una cuenta con permisos `DDL`.  
3. Poblar `OrganizationalUnit` y `AppUser` / `UserRole` según el entorno (script de semilla aparte, no incluido en el MVP mínimo).

---

## 6. Evolución prevista (fuera de este script)

- Tabla de **notificaciones in-app** si se deja de calcular solo por consultas.  
- **Outbox** para integración de eventos.  
- **BPM** / definición de flujos en tablas (post-MVP).  
