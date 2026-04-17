# Backlog priorizado — MVP (pantallas y criterios de aceptación)

**Versión:** 1.0  
**Prioridad:** P0 = imprescindible para demo operable, P1 = MVP completo, P2 = deseable si sobra tiempo.

---

## Leyenda de historias

Formato: **Como** [rol] **quiero** [acción] **para** [beneficio].

**CA** = Criterios de aceptación (pruebas verificables).

---

## Epic A — Autenticación y contexto de usuario

### A1 — Login y sesión (P0)

- **Como** usuario **quiero** autenticarme **para** acceder solo a lo autorizado.
- **CA:**
  - Tras credenciales válidas, se redirige a la bandeja principal.
  - Sesión expira según política; mensaje claro al expirar.
  - Usuario sin roles asignados ve mensaje de contacto admin (no error genérico).

### A2 — Rol activo (P1)

- **Como** usuario con varios roles **quiero** elegir rol activo **para** ver bandejas y acciones correctas.
- **CA:**
  - El rol seleccionado filtra menús y acciones.
  - El token/claim enviado al API refleja el rol activo (o el servidor deriva permisos de forma consistente).

---

## Epic B — Solicitudes (ciclo de vida)

### B1 — Crear solicitud en borrador (P0)

- **Como** Solicitante **quiero** crear una solicitud con tipo y datos comunes **para** iniciar un trámite.
- **CA:**
  - Puede guardarse en `Draft` sin todos los campos obligatorios de envío.
  - Se muestra folio o “pendiente de asignación” según regla de negocio documentada.

### B2 — Completar plantilla por tipo (P0)

- **Como** Solicitante **quiero** ver campos según el tipo elegido **para** cumplir validaciones de [04-plantillas-campos-por-tipo.md](./04-plantillas-campos-por-tipo.md).
- **CA:**
  - Cambiar de tipo actualiza formulario dinámico y valida según nuevo tipo antes de `Submit`.
  - Errores de validación son por campo y accesibles (lectores de pantalla).

### B3 — Enviar solicitud (P0)

- **Como** Solicitante **quiero** enviar la solicitud **para** que ingrese a flujo institucional.
- **CA:**
  - `Submit` bloqueado hasta cumplir campos comunes + específicos + adjuntos mínimos.
  - Éxito: estado `Submitted` y evento visible en línea de tiempo.

### B4 — Bandejas por rol (P0)

- **Como** usuario **quiero** ver listados filtrados según mi rol **para** atender pendientes.
- **CA:**
  - Solicitante ve solo sus solicitudes (o las de su unidad si aplica Coordinador).
  - Analista ve cola de `Submitted` / `InTicAnalysis` según reglas.
  - Aprobador ve `PendingApproval` donde corresponda.
  - Implementador ve `Approved` / `InProgress` / asignadas.

### B5 — Detalle con línea de tiempo (P0)

- **Como** cualquier rol autorizado **quiero** ver historial de estados y acciones **para** trazabilidad.
- **CA:**
  - Cada transición muestra fecha, usuario, estado anterior/nuevo.
  - Motivos de rechazo/devolución/cancelación visibles según permiso.

### B6 — Transiciones desde UI (P0)

- **Como** usuario con permiso **quiero** ejecutar solo acciones permitidas **para** avanzar el flujo.
- **CA:**
  - Botones alineados a [03-estados-y-contrato-api.md](./03-estados-y-contrato-api.md).
  - 403 del API muestra mensaje amigable y opción de refrescar.

### B7 — Concurrencia (P1)

- **Como** usuario **quiero** que el sistema avise si otro cambió la solicitud **para** no sobrescribir estados.
- **CA:**
  - Ante 409, se muestra conflicto y se recarga detalle.

---

## Epic C — Comentarios y adjuntos

### C1 — Comentarios públicos e internos (P1)

- **Como** Analista **quiero** dejar nota interna **para** coordinación sin exponer al solicitante.
- **CA:**
  - Solicitante no ve comentarios marcados internos.
  - Auditor/Analista/Aprobador ven hilo completo según [02-matriz-rbac.md](./02-matriz-rbac.md).

### C2 — Adjuntos (P0)

- **Como** Solicitante **quiero** adjuntar archivos **para** sustentar la solicitud.
- **CA:**
  - Rechaza extensiones no permitidas y tamaño mayor al límite.
  - Lista muestra nombre, tamaño, fecha, autor de subida.

---

## Epic D — Administración

### D1 — Catálogos mínimos (P1)

- **Como** Admin **quiero** mantener unidades y parámetros **para** que los formularios sean correctos.
- **CA:**
  - Cambios en catálogo generan evento de auditoría.
  - No se puede eliminar unidad en uso sin política (bloqueo o soft-disable).

### D2 — Usuarios y roles (P1)

- **Como** Admin **quiero** asignar roles a usuarios **para** habilitar el flujo.
- **CA:**
  - Reflejo en permisos en la siguiente sesión o tras refresco de token según diseño.

---

## Epic E — Reportes

### E1 — Exportar listado (P1)

- **Como** usuario con permiso **quiero** exportar CSV/Excel **para** análisis externo.
- **CA:**
  - Export respeta mismos filtros que la grilla y alcance de datos del rol.
  - Archivo descargable con nombre que incluye fecha.

### E2 — Vista imprimible de solicitud (P2)

- **Como** Auditor **quiero** imprimir detalle **para** expediente físico.
- **CA:**
  - Layout legible; oculta datos que la política reserve en impresión.

---

## Epic F — No funcional

### F1 — Correlación y errores (P1)

- **CA:** Errores API usan `ProblemDetails`; front muestra `correlationId` en mensaje de error para soporte.

### F2 — Rendimiento básico (P2)

- **CA:** Listados principales con paginación; tiempo percibido < 3s en red institucional típica (objetivo orientativo).

---

## Orden sugerido de implementación

1. A1, B1, B2, B3, B4, B5, B6, C2
2. A2, B7, C1, D1, D2, E1
3. E2, F2

---

## Definición de “terminado” (MVP)

- Todos los ítems **P0** y **P1** cumplen sus CA en ambiente de pruebas.
- Matriz RBAC y transiciones cubiertas por pruebas manuales mínimas por rol.
- Auditoría de transiciones verificada en BD o pantalla admin/auditor.