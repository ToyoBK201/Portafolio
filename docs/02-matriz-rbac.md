# Matriz RBAC — MVP (permisos × pantalla × API)

**Versión:** 1.0  
**Convenciones:** ✓ = permitido, — = denegado, ○ = solo propias / unidad / asignadas según nota.

**Roles:** `Solicitante` (Sol), `CoordinadorArea` (Crd), `AnalistaTIC` (Ana), `Aprobador` (Apr), `Implementador` (Imp), `Admin` (Adm), `Auditor` (Aud).

---

## 1. Pantallas (Angular)


| Pantalla / flujo                     | Sol                             | Crd                  | Ana           | Apr                     | Imp         | Adm      | Aud     |
| ------------------------------------ | ------------------------------- | -------------------- | ------------- | ----------------------- | ----------- | -------- | ------- |
| Login / selección rol activo         | ✓                               | ✓                    | ✓             | ✓                       | ✓           | ✓        | ✓       |
| Bandeja “Mis solicitudes”            | ○ propias                       | ○ unidad             | ✓ cola        | ✓ pendientes aprobación | ○ asignadas | ✓ todas  | ✓ todas |
| Bandeja “Pendientes” por rol         | —                               | ○ si aplica          | ✓             | ✓                       | ✓           | ✓        | —       |
| Alta / edición Borrador              | ○                               | ○                    | —             | —                       | —           | —        | —       |
| Detalle solicitud (ver)              | ○                               | ○                    | ✓             | ✓                       | ✓           | ✓        | ✓       |
| Detalle: acciones de transición      | según estado                    | según regla opcional | ✓             | ✓                       | ✓           | limitado | —       |
| Comentarios (público al solicitante) | ✓ crear                         | ✓                    | ✓             | ✓                       | ✓           | ✓        | —       |
| Comentarios internos                 | —                               | ○                    | ✓             | ✓                       | ✓           | ✓        | ✓ ver   |
| Adjuntos: subir                      | ○ en Borrador/Enviada si reglas | ○                    | ✓             | ✓                       | ✓           | ✓        | —       |
| Adjuntos: descargar                  | ○                               | ○                    | ✓             | ✓                       | ✓           | ✓        | ✓       |
| Administración: usuarios / roles     | —                               | —                    | —             | —                       | —           | ✓        | —       |
| Administración: catálogos            | —                               | —                    | —             | —                       | —           | ✓        | —       |
| Reportes / exportar listados         | ○ propias                       | ○ unidad             | ✓ filtros rol | ✓                       | ✓           | ✓        | ✓       |


**Notas:**

- **CoordinadorArea:** opcional; si la institución no lo usa, el rol no se asigna. Sus permisos “unidad” requieren `UnitId` en el perfil.
- **Auditor:** solo lectura y exportación; sin transiciones ni comentarios que alteren flujo (puede tener comentario de solo lectura deshabilitado en UI).

---

## 2. API REST (recursos y operaciones)

Convención de rutas base: `/api/v1`. Los nombres exactos pueden ajustarse al estilo del equipo; la **matriz de autorización** es la fuente de verdad.

### 2.1 Solicitudes (`/requests`)


| Operación             | HTTP  | Sol | Crd | Ana | Apr | Imp | Adm | Aud |
| --------------------- | ----- | --- | --- | --- | --- | --- | --- | --- |
| Listar (filtrado)     | GET   | ○   | ○   | ✓   | ✓   | ✓   | ✓   | ✓   |
| Obtener por id        | GET   | ○   | ○   | ✓   | ✓   | ✓   | ✓   | ✓   |
| Crear                 | POST  | ✓   | ✓   | —   | —   | —   | —   | —   |
| Actualizar Borrador   | PATCH | ○   | ○   | —   | —   | —   | —   | —   |
| Enviar (transición)   | POST  | ✓   | ✓   | —   | —   | —   | —   | —   |
| Cancelar (transición) | POST  | ○   | —   | —   | —   | —   | ✓   | —   |


`○` = solo si la solicitud pertenece al usuario/unidad y el estado lo permite.

### 2.2 Transiciones (`/requests/{id}/transitions`)


| Operación                  | HTTP | Ana | Apr | Imp | Sol | Adm        |
| -------------------------- | ---- | --- | --- | --- | --- | ---------- |
| Ejecutar transición válida | POST | ✓   | ✓   | ✓   | ✓   | ✓ limitado |


La **aplicación** valida: rol + estado actual + [03-estados-y-contrato-api.md](./03-estados-y-contrato-api.md). El API devuelve 403 si la política no aplica.

### 2.3 Adjuntos (`/requests/{id}/attachments`)


| Operación         | HTTP   | Sol | Ana | Apr | Imp | Adm             | Aud |
| ----------------- | ------ | --- | --- | --- | --- | --------------- | --- |
| Subir             | POST   | ○   | ✓   | ✓   | ✓   | ✓               | —   |
| Listar            | GET    | ○   | ✓   | ✓   | ✓   | ✓               | ✓   |
| Descargar         | GET    | ○   | ✓   | ✓   | ✓   | ✓               | ✓   |
| Eliminar / anular | DELETE | —   | —   | —   | —   | ✓ con auditoría | —   |


MVP recomendado: **no DELETE** para solicitante; solo anulación lógica por Admin con auditoría.

### 2.4 Comentarios (`/requests/{id}/comments`)


| Operación     | HTTP | Sol            | Internos           |
| ------------- | ---- | -------------- | ------------------ |
| Crear público | POST | ✓              | Ana, Apr, Imp, Crd |
| Crear interno | POST | —              | Ana, Apr, Imp, Adm |
| Listar        | GET  | ✓ sin internos | ✓ todos según rol  |


### 2.5 Catálogos (`/catalogs/`*)


| Operación     | HTTP | Adm | Otros                        |
| ------------- | ---- | --- | ---------------------------- |
| CRUD maestros | *    | ✓   | GET lectura para formularios |


### 2.6 Reportes (`/reports/`*)


| Operación        | HTTP | Adm | Ana | Apr | Imp | Aud | Sol              |
| ---------------- | ---- | --- | --- | --- | --- | --- | ---------------- |
| Export CSV/Excel | GET  | ✓   | ✓   | ✓   | ✓   | ✓   | ○ propias/unidad |


---

## 3. Políticas transversales

1. **Separación de funciones:** el mismo usuario **no** debe ser `Aprobador` e `Implementador` de la misma solicitud en modo estricto (configurable; por defecto **bloquear** en dominio).
2. **Menor privilegio:** JWT/claims cargan roles; el servidor **no confía** en el cliente para ocultar campos sensibles (filtrar comentarios internos en API).
3. **Auditoría:** toda transición y cambio crítico genera evento según [05-auditoria-esquema.md](./05-auditoria-esquema.md).

---

## 4. Mapeo rápido a políticas .NET (referencia)


| Política sugerida          | Roles                                                                   |
| -------------------------- | ----------------------------------------------------------------------- |
| `CanManageCatalogs`        | Admin                                                                   |
| `CanTransitionAnalyst`     | AnalistaTIC                                                             |
| `CanTransitionApprover`    | Aprobador                                                               |
| `CanTransitionImplementer` | Implementador                                                           |
| `CanReadAllRequests`       | Admin, Auditor, AnalistaTIC (ajustar si Ana solo ve cola)               |
| `CanExportReports`         | Admin, Auditor, AnalistaTIC, Aprobador, Implementador (+ alcance datos) |


Afinar en implementación con **handlers** por recurso si “ver todo” difiere entre Ana y Adm.