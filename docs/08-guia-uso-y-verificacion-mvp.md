# Guia de uso y verificacion MVP

> Nota de autoria: este MVP fue desarrollado como proyecto personal por **Jorge Alberto Gutierrez Chaidez**, con apoyo de asistencia de IA durante la implementacion y la documentacion.

## 1. Objetivo de esta guia

Esta guia sirve para dos cosas:

1. Entender el proceso funcional de extremo a extremo.
2. Verificar que el MVP cumple P0 y P1 del backlog (`docs/06-backlog-ui-y-criterios.md`).

Referencias base:

- RBAC: `docs/02-matriz-rbac.md`
- Estados/transiciones: `docs/03-estados-y-contrato-api.md`
- Backlog y criterios: `docs/06-backlog-ui-y-criterios.md`

---

## 2. Preparacion del entorno

### 2.1 Prerrequisitos

- .NET SDK 8
- Node.js LTS (npm incluido)
- Docker Desktop (recomendado para SQL Server local)
- Angular CLI (opcional global)

### 2.2 Levantar base de datos

```powershell
docker compose -f database/docker-compose.sqlserver.yml up -d
./scripts/apply-schema.ps1
```

### 2.3 Levantar API

Desde la raiz:

```powershell
dotnet restore SolicitudesTechGov.sln
dotnet run --project apps/api/SolicitudesTechGov.Api.csproj
```

Validar salud:

- `GET http://localhost:5000/api/v1/health`

### 2.4 Levantar frontend

```powershell
cd apps/web
npm install
npm run start
```

Abrir la SPA en `http://localhost:4200`.

---

## 3. Como entrar al sistema

Hay dos formas:

1. **Login por credenciales** (`/login`): para validar A1 (sesion y errores).
2. **Token dev** (`/auth`): para cambiar rapido de rol y probar flujos RBAC.

Recomendacion para pruebas funcionales:

- usar `Token (dev)` para recorrer roles rapido;
- usar `Login` al menos una vez para validar expiracion/mensajes y A1/A2 con cuenta real.

---

## 4. Flujo funcional completo (proceso de negocio)

Este es el camino ideal de una solicitud:

1. `Draft` (Solicitante/Coordinador crea borrador).
2. `Submitted` (envio formal).
3. `InTicAnalysis` (analisis TIC).
4. `PendingApproval` (aprobacion institucional).
5. `Approved` (aprobada).
6. `InProgress` (implementacion).
7. `PendingRequesterValidation` (espera validacion de solicitante).
8. `Closed` (cierre exitoso).

Flujos alternos:

- Rechazos: `Rejected` desde analisis o aprobacion.
- Devoluciones: de aprobacion a analisis, o de validacion a ejecucion.
- Cancelaciones: segun politica (`CancelByRequester`, `CancelByAdmin`).

---

## 5. Checklist rapido por historia (P0/P1)

## A. Autenticacion y rol activo

- [o] Login valido redirige a bandeja.
- [o] Sesion expirada/red invalida muestra mensaje claro.
- [o] Usuario sin roles ve mensaje de contacto admin (no generico).
- [o] Cambio de rol activo refleja menu/acciones (A2).

## B. Solicitudes y transiciones

- [o] Crear borrador sin requerir todos los campos de envio (B1).
- [o] Formulario cambia por tipo y valida por campo (B2).
- [o] Envio bloqueado si faltan obligatorios + adjuntos minimos (B3) *(parcial: campos obligatorios verificados; regla de adjuntos minimos queda como pendiente controlado PC-01)*.
- [o] Bandeja filtra por rol/alcance (B4).
- [o] Detalle muestra linea de tiempo con actor/fecha/estado (B5).
- [o] Solo aparecen/funcionan transiciones permitidas por estado/rol (B6).
- [o] Ante `409`, UI avisa conflicto y permite refrescar/recarga (B7).

## C. Comentarios y adjuntos

- [o] Solicitante no ve comentarios internos (C1).
- [o] Roles autorizados pueden marcar comentario interno (C1).
- [o] Adjuntos rechazan extension/peso no valido (C2).
- [o] Lista de adjuntos muestra nombre, tamano, fecha, autor (C2) *(parcial: autor no expuesto en DTO/UI actual; pendiente controlado PC-02)*.

## D. Administracion

- [o] Admin puede gestionar usuarios/roles y surte efecto al refrescar sesion (D2).
- [o] Cambios de catalogos quedan auditados (D1).

## E/F. Reportes y no funcionales

- [o] Export de bandeja respeta filtros y alcance por rol (E1).
- [o] Export de auditoria funciona (admin/auditor) y descarga archivo con fecha.
- [o] Errores muestran `correlationId` cuando API lo envía (F1).

---

## 6. Prueba guiada por rol (paso a paso)

## 6.1 Solicitante (`Requester`)

1. Ingresar con rol `Requester`.
2. Ir a **Nueva solicitud**.
3. Crear borrador con datos minimos.
4. Cambiar tipo de solicitud y comprobar campos dinamicos.
5. Adjuntar archivo valido.
6. Enviar solicitud (`Submit`).
7. Verificar en detalle:
   - estado `Submitted`;
   - evento en auditoria;
   - comentario publico visible.
8. Si llega a `PendingRequesterValidation`, ejecutar:
   - `AcceptDelivery` para cerrar, o
   - `ReturnToExecution` para devolver.

Resultado esperado:

- solo ve sus solicitudes;
- no ve notas internas;
- no puede ejecutar acciones fuera de su estado/alcance.

## 6.2 Analista TIC (`TicAnalyst`)

1. Cambiar a rol `TicAnalyst`.
2. Abrir bandeja filtrada por `Submitted`/`InTicAnalysis`.
3. Tomar solicitud en `Submitted` y aplicar `ReceiveForAnalysis`.
4. Agregar comentario interno.
5. Probar:
   - `SendToApproval`, y en otro caso
   - `RejectFromAnalysis` con motivo.

Resultado esperado:

- puede ver comentarios internos;
- transiciones invalidas dan error amigable (403/400).

## 6.3 Aprobador (`InstitutionalApprover`)

1. Cambiar a `InstitutionalApprover`.
2. Tomar solicitud en `PendingApproval`.
3. Probar:
   - `Approve`,
   - `ReturnToAnalysis` (con motivo),
   - `RejectFromApproval` (con motivo).

Resultado esperado:

- solo opera estados de aprobacion;
- no ejecuta acciones de implementador.

## 6.4 Implementador (`Implementer`)

1. Cambiar a `Implementer`.
2. Tomar solicitud `Approved`.
3. Ejecutar `StartExecution` y luego `RequestValidation`.
4. Confirmar auditoria y comentarios internos.

## 6.5 Administrador (`SystemAdministrator`)

1. Cambiar a `SystemAdministrator`.
2. Verificar menu admin:
   - usuarios,
   - unidades organizativas,
   - auditoria global.
3. Probar asignacion de rol a un usuario y reingreso de sesion.
4. Probar export de auditoria CSV.

## 6.6 Auditor (`Auditor`)

1. Cambiar a `Auditor`.
2. Verificar que menu de transiciones no aparece.
3. Abrir solicitudes y revisar auditoria/linea de tiempo.
4. Descargar export de auditoria.

Resultado esperado:

- solo lectura;
- sin acciones de mutacion.

---

## 7. Pruebas negativas recomendadas

1. **403 por permiso**: intentar transicion con rol no autorizado.
2. **409 por concurrencia**: abrir la misma solicitud en dos sesiones/roles y aplicar transiciones seguidas.
3. **Validaciones de adjuntos**: archivo mayor a limite y extension no permitida.
4. **Motivo obligatorio**: ejecutar transicion que exige `reason` sin enviarlo.
5. **Sesion**: invalidar token y navegar para verificar redireccion a login y mensaje.

---

## 8. Evidencia minima para dar por cerrado el MVP

Guardar (captura o registro breve):

- una ejecucion completa feliz (`Draft` -> `Closed`);
- una ruta de rechazo/cancelacion;
- al menos un caso de `403` y uno de `409`;
- export de bandeja y export de auditoria;
- evidencia de que solicitante no ve comentarios internos;
- evidencia de `correlationId` en un error de API.

---

## 9. Comandos de validacion tecnica antes de merge

Desde raiz:

```powershell
dotnet test SolicitudesTechGov.sln -c Release
```

Frontend:

```powershell
cd apps/web
npm run test
npm run build
```

Si todo pasa y el checklist funcional esta completo, el MVP queda listo para cierre de iteracion.

> Resultado de cierre: MVP aceptado con pendientes controlados PC-01 y PC-02, documentados en `docs/09-acta-cierre-y-aceptacion-mvp.md`.

---

## 10. Nota para portafolio profesional

Este MVP puede presentarse como evidencia de competencias en:

- desarrollo full-stack (Angular + .NET + SQL Server),
- diseno de flujo de negocio con estados y transiciones,
- control de acceso por roles (RBAC),
- validacion funcional por criterios de aceptacion,
- manejo de errores y trazabilidad de cierre.

Resumen sugerido (CV/LinkedIn):

> "Desarrolle de forma individual un MVP de gestion de solicitudes tecnologicas, implementando flujo completo con autenticacion, roles, transiciones, auditoria, adjuntos, exportes y documentacion de aceptacion de cierre, con apoyo de asistencia de IA."
