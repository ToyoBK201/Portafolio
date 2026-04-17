# Estados, transiciones y contrato API compartido

**Versión:** 1.0  
**Propósito:** Congelar **nombres estables** consumibles por SQL Server (tinyint/int + tabla), C# (`enum`), TypeScript/Angular y OpenAPI. Evitar drift: un solo origen de verdad documentado aquí; en código, generar o sincronizar desde este contrato en CI si el equipo lo adopta.

---

## 1. Identificadores en API

- **Formato JSON:** `PascalCase` para valores de enum (coincide con C# serialización por defecto `JsonStringEnumConverter` con nombres iguales).
- **Alternativa:** si el front prefiere `camelCase`, usar conversión explícita en Angular; **no** mezclar dos estilos en la misma API.

---

## 2. Enum `RequestStatus`


| Valor                        | Valor numérico sugerido (BD) | Descripción breve                      |
| ---------------------------- | ---------------------------- | -------------------------------------- |
| `Draft`                      | 1                            | Borrador editable.                     |
| `Submitted`                  | 2                            | Enviada formalmente.                   |
| `InTicAnalysis`              | 3                            | En análisis TIC.                       |
| `PendingApproval`            | 4                            | Pendiente de aprobación institucional. |
| `Approved`                   | 5                            | Aprobada; lista para ejecución.        |
| `Rejected`                   | 6                            | Rechazada (terminal).                  |
| `InProgress`                 | 7                            | En ejecución.                          |
| `PendingRequesterValidation` | 8                            | Pendiente validación del solicitante.  |
| `Closed`                     | 9                            | Cerrada satisfactoriamente (terminal). |
| `Cancelled`                  | 10                           | Cancelada (terminal).                  |


**Nota:** Los nombres del plan original (`Borrador`, `EnAnalisisTIC`, …) se mapean a **identificadores en inglés** para código y API estándar; las etiquetas UI siguen en español.


| Español (UI)                     | API (`RequestStatus`)        |
| -------------------------------- | ---------------------------- |
| Borrador                         | `Draft`                      |
| Enviada                          | `Submitted`                  |
| En análisis TIC                  | `InTicAnalysis`              |
| Pendiente aprobación             | `PendingApproval`            |
| Aprobada                         | `Approved`                   |
| Rechazada                        | `Rejected`                   |
| En ejecución                     | `InProgress`                 |
| Pendiente validación solicitante | `PendingRequesterValidation` |
| Cerrada                          | `Closed`                     |
| Cancelada                        | `Cancelled`                  |


---

## 3. Enum `RequestType`


| Valor API                    | BD id | Etiqueta UI (ES)                       |
| ---------------------------- | ----- | -------------------------------------- |
| `HardwareAcquisition`        | 1     | Adquisición de hardware                |
| `SoftwareLicensing`          | 2     | Adquisición/licenciamiento de software |
| `SystemDevelopment`          | 3     | Desarrollo o evolutivo de sistema      |
| `InfrastructureConnectivity` | 4     | Infraestructura y conectividad         |
| `MajorTechnicalSupport`      | 5     | Soporte técnico / incidente mayor      |
| `InformationSecurity`        | 6     | Seguridad de la información            |
| `DataInteroperability`       | 7     | Datos / interoperabilidad              |


---

## 4. Enum `Priority`


| Valor API  | Etiqueta UI |
| ---------- | ----------- |
| `Low`      | Baja        |
| `Medium`   | Media       |
| `High`     | Alta        |
| `Critical` | Crítica     |


---

## 5. Enum `Role` (aplicación)


| Valor API               |
| ----------------------- |
| `Requester`             |
| `AreaCoordinator`       |
| `TicAnalyst`            |
| `InstitutionalApprover` |
| `Implementer`           |
| `SystemAdministrator`   |
| `Auditor`               |


Alinear con claims del token o tabla `UserRole`.

---

## 6. Enum `Transition` (comando explícito en POST)

Cada transición es **explícita** en la API: el cliente envía `transition`, no el estado destino directo (el servidor valida).


| Valor API            | Desde estado                 | Hacia estado                 | Rol(es) típicos                                       |
| -------------------- | ---------------------------- | ---------------------------- | ----------------------------------------------------- |
| `Submit`             | `Draft`                      | `Submitted`                  | Requester, AreaCoordinator                            |
| `ReceiveForAnalysis` | `Submitted`                  | `InTicAnalysis`              | TicAnalyst                                            |
| `SendToApproval`     | `InTicAnalysis`              | `PendingApproval`            | TicAnalyst                                            |
| `RejectFromAnalysis` | `InTicAnalysis`              | `Rejected`                   | TicAnalyst                                            |
| `Approve`            | `PendingApproval`            | `Approved`                   | InstitutionalApprover                                 |
| `ReturnToAnalysis`   | `PendingApproval`            | `InTicAnalysis`              | InstitutionalApprover                                 |
| `RejectFromApproval` | `PendingApproval`            | `Rejected`                   | InstitutionalApprover                                 |
| `StartExecution`     | `Approved`                   | `InProgress`                 | Implementer                                           |
| `RequestValidation`  | `InProgress`                 | `PendingRequesterValidation` | Implementer                                           |
| `AcceptDelivery`     | `PendingRequesterValidation` | `Closed`                     | Requester                                             |
| `ReturnToExecution`  | `PendingRequesterValidation` | `InProgress`                 | Requester                                             |
| `CancelByRequester`  | `Submitted`                  | `Cancelled`                  | Requester (solo si política: sin asignación analista) |
| `CancelByAdmin`      | `InTicAnalysis`              | `Cancelled`                  | SystemAdministrator                                   |


**Motivo obligatorio** (`reason` string no vacío) en: `RejectFromAnalysis`, `RejectFromApproval`, `ReturnToAnalysis`, `ReturnToExecution`, `CancelByRequester`, `CancelByAdmin`.

---

## 7. Contrato REST mínimo (ejemplo)

### `POST /api/v1/requests/{id}/transitions`

**Request body:**

```json
{
  "transition": "SendToApproval",
  "reason": null,
  "correlationId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Responses:**

- `204 No Content` — transición aplicada.
- `400 Bad Request` — validación (motivo faltante, estado incorrecto).
- `403 Forbidden` — rol no autorizado.
- `409 Conflict` — estado ya cambió (concurrencia); cliente debe refrescar.

---

## 8. Fragmento OpenAPI (referencia)

```yaml
RequestStatus:
  type: string
  enum:
    - Draft
    - Submitted
    - InTicAnalysis
    - PendingApproval
    - Approved
    - Rejected
    - InProgress
    - PendingRequesterValidation
    - Closed
    - Cancelled

Transition:
  type: string
  enum:
    - Submit
    - ReceiveForAnalysis
    - SendToApproval
    - RejectFromAnalysis
    - Approve
    - ReturnToAnalysis
    - RejectFromApproval
    - StartExecution
    - RequestValidation
    - AcceptDelivery
    - ReturnToExecution
    - CancelByRequester
    - CancelByAdmin
```

---

## 9. Tabla de transición permitida (matriz compacta)

Fila = estado actual, columna = `Transition` (solo una celda válida por política de negocio).


| Estado actual                     | Transiciones permitidas                                 |
| --------------------------------- | ------------------------------------------------------- |
| `Draft`                           | `Submit`                                                |
| `Submitted`                       | `ReceiveForAnalysis`, `CancelByRequester`               |
| `InTicAnalysis`                   | `SendToApproval`, `RejectFromAnalysis`, `CancelByAdmin` |
| `PendingApproval`                 | `Approve`, `ReturnToAnalysis`, `RejectFromApproval`     |
| `Approved`                        | `StartExecution`                                        |
| `InProgress`                      | `RequestValidation`                                     |
| `PendingRequesterValidation`      | `AcceptDelivery`, `ReturnToExecution`                   |
| `Rejected`, `Closed`, `Cancelled` | *(ninguna)*                                             |


Implementación recomendada: tabla `AllowedTransition` en código (switch/strategy) o tabla de configuración versionada; tests unitarios por cada par (estado, transición, rol).