# Esquema de auditoría y retención

**Versión:** 1.0  
**Propósito:** Definir el formato de los registros de auditoría (incl. `payloadDiff`, `correlationId`), responsabilidades de capa y política de retención mínima alineada a un entorno gubernamental/empresarial.

---

## 1. Principios

1. **Append-only:** los registros de auditoría no se editan ni borran en operación normal (solo proceso controlado de archivo/purga según política).
2. **Trazabilidad de decisiones:** toda transición de estado y acción sensible genera evento con actor y contexto.
3. **Correlación:** cada request HTTP y cada comando de dominio pueden llevar `correlationId` para reconstruir secuencias en logs distribuidos.
4. **Mínimo privilegio en lectura:** solo `Auditor`, `Admin` (y roles definidos) consultan el detalle completo; el resto ve historial resumido en la UI de solicitud.

---

## 2. Eventos que generan fila de auditoría (MVP)


| Categoría          | Acciones                                                                                         |
| ------------------ | ------------------------------------------------------------------------------------------------ |
| **Solicitud**      | Creación, actualización de borrador, envío (`Submit`).                                           |
| **Transición**     | Cada `Transition` aplicada (ver [03-estados-y-contrato-api.md](./03-estados-y-contrato-api.md)). |
| **Adjunto**        | Subida, descarga registrada (opcional MVP), anulación lógica.                                    |
| **Comentario**     | Creación (distinguir interno / público en payload o tipo).                                       |
| **Administración** | Cambios en catálogos, roles de usuario, parámetros.                                              |
| **Autenticación**  | Login exitoso/fallido (opcional en mismo store o log de seguridad separado).                     |


---

## 3. Modelo lógico (tabla `AuditLog`)


| Columna          | Tipo sugerido                         | Descripción                                                                                         |
| ---------------- | ------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `AuditId`        | `BIGINT` identity                     | PK.                                                                                                 |
| `OccurredAtUtc`  | `DATETIME2(7)`                        | Momento del evento (UTC).                                                                           |
| `CorrelationId`  | `UNIQUEIDENTIFIER` NULL               | Id de correlación cliente/servidor.                                                                 |
| `ActorUserId`    | `UNIQUEIDENTIFIER` / `INT` FK         | Usuario que ejecutó la acción.                                                                      |
| `ActorRole`      | `NVARCHAR(64)`                        | Rol principal en el momento de la acción (snapshot).                                                |
| `Action`         | `NVARCHAR(64)`                        | Ej.: `RequestCreated`, `TransitionApplied`, `AttachmentUploaded`, `CommentAdded`, `CatalogUpdated`. |
| `EntityType`     | `NVARCHAR(64)`                        | Ej.: `Request`, `Attachment`, `Comment`, `CatalogItem`.                                             |
| `EntityId`       | `UNIQUEIDENTIFIER` / `BIGINT`         | Id de la entidad afectada.                                                                          |
| `RequestId`      | `UNIQUEIDENTIFIER` / `BIGINT` NULL    | FK denormalizada para consultas por solicitud (NULL para eventos globales).                         |
| `FromStatus`     | `TINYINT` NULL                        | Estado previo (solo transiciones).                                                                  |
| `ToStatus`       | `TINYINT` NULL                        | Estado nuevo.                                                                                       |
| `ClientIp`       | `VARBINARY(16)` o `NVARCHAR(45)` NULL | Si está disponible y permitido por política de privacidad.                                          |
| `UserAgent`      | `NVARCHAR(256)` NULL                  | Opcional.                                                                                           |
| `PayloadSummary` | `NVARCHAR(MAX)` NULL                  | JSON reducido: solo campos no sensibles o hash.                                                     |
| `PayloadDiff`    | `NVARCHAR(MAX)` NULL                  | Ver sección 4.                                                                                      |
| `Success`        | `BIT`                                 | `1` si la operación completó; `0` si falló después de intento autorizado (opcional).                |


**Índices:** `(RequestId, OccurredAtUtc)`, `(ActorUserId, OccurredAtUtc)`, `(Action, OccurredAtUtc)`.

---

## 4. Formato de `PayloadDiff`

Objetivo: reconstruir **qué cambió** sin guardar copias completas innecesarias en cada evento.

**Estrategia MVP:**

- Para **actualización de entidad** (borrador): JSON array de cambios:

```json
{
  "changes": [
    { "path": "title", "op": "replace", "old": "Antiguo", "new": "Nuevo" },
    { "path": "specificPayload.productName", "op": "replace", "old": "A", "new": "B" }
  ]
}
```

- Para **campos sensibles** (si existen): no registrar valores; registrar `{ "path": "…", "op": "masked" }`.
- Para **transiciones:** `PayloadDiff` puede ser redundante con `FromStatus`/`ToStatus`; incluir `reason` si aplica:

```json
{
  "transition": "RejectFromApproval",
  "reason": "…"
}
```

- **Tamaño:** si el diff supera **32 KB**, truncar y guardar referencia a snapshot en almacén (post-MVP) o solo `PayloadSummary` con lista de campos tocados.

---

## 5. `CorrelationId`


| Origen                                                   | Uso                                                                                                            |
| -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Cliente (header `X-Correlation-Id` o body en transición) | Enlazar clicks de UI con logs del API.                                                                         |
| Servidor                                                 | Si el cliente no envía, generar `Guid` al inicio del pipeline y propagar a logs estructurados (Serilog, etc.). |


**Contrato:** aceptar UUID; opcionalmente también string corto alfanumérico si el equipo lo estandariza.

---

## 6. Retención y archivo (mínimo recomendado)


| Tipo de dato                            | Retención MVP sugerida                                             | Notas                                               |
| --------------------------------------- | ------------------------------------------------------------------ | --------------------------------------------------- |
| `AuditLog` operativo                    | **5 años** o política institucional vigente (la que sea **mayor**) | Común en sector público para actos administrativos. |
| Logs de aplicación (archivos / Elastic) | Alineado a TI local; típicamente **90 días** hot, resto frío       | No sustituye `AuditLog` en BD.                      |
| Adjuntos                                | Misma retención que la solicitud asociada                          | Borrado solo por proceso legal/purga coordinada.    |


**Purga:** fuera de MVP; si se implementa, solo con job auditado y backup previo.

---

## 7. Cumplimiento y acceso

- Exportación de auditoría solo para roles autorizados ([02-matriz-rbac.md](./02-matriz-rbac.md)).
- Registro de **quién exportó** auditoría (`Action`: `AuditExported`) como evento adicional.

---

## 8. Implementación .NET (referencia breve)

- Middleware que asigna `CorrelationId`.
- **Domain events** → un handler publica filas en `AuditLog` en la misma transacción que la persistencia de la solicitud cuando sea posible (outbox opcional post-MVP).
- No escribir PII innecesaria en `PayloadDiff`; cumplir normativa local de datos personales.

