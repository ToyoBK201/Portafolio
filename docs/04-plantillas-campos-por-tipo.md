# Plantillas de campos por tipo de solicitud

**Versión:** 1.0  
**Propósito:** Definir campos obligatorios, validaciones y adjuntos mínimos **antes** del modelado físico en SQL Server. Los campos específicos pueden almacenarse como `JSON` validado por esquema por tipo (`RequestType`) o tablas normalizadas; el dominio debe aplicar **validadores por estrategia** según `RequestType`.

**Referencia de tipos:** [03-estados-y-contrato-api.md](./03-estados-y-contrato-api.md) (`RequestType`).

---

## 1. Campos comunes (todos los tipos)

Almacenamiento sugerido: columnas en tabla `Request`; `Title`, `Description`, etc.


| Campo API               | Obligatorio al enviar | Validación                                                    |
| ----------------------- | --------------------- | ------------------------------------------------------------- |
| `title`                 | Sí                    | 5–200 caracteres.                                             |
| `description`           | Sí                    | 20–8000 caracteres.                                           |
| `requestType`           | Sí                    | Enum `RequestType`.                                           |
| `priority`              | Sí                    | Enum `Priority`.                                              |
| `requestingUnitId`      | Sí                    | FK catálogo unidades.                                         |
| `requesterUserId`       | Sí                    | Usuario responsable (puede ser el logueado).                  |
| `businessJustification` | Sí                    | Texto; justificación de negocio / legal (mín. 20 caracteres). |
| `desiredDate`           | No                    | Fecha ≥ hoy al crear; puede quedar null si no aplica.         |


**Folio:** generado por sistema al salir de `Draft` (o al crear, según decisión de implementación); único e inmutable.

---

## 2. Adjuntos mínimos por tipo

Reglas globales: [01-alcance-congelado.md](./01-alcance-congelado.md) (tamaño, extensiones, cantidad).


| RequestType                  | Adjuntos mínimos (MVP)                                            | Notas                                                                  |
| ---------------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------------------- |
| `HardwareAcquisition`        | Al menos 1: cotización **o** cotización en elaboración (PDF nota) | Si no hay cotización, checkbox “en proceso” + justificación en texto.  |
| `SoftwareLicensing`          | 1: propuesta comercial o licenciamiento actual                    |                                                                        |
| `SystemDevelopment`          | Opcional MVP; recomendado 1: borrador de alcance                  | Si no adjunta: `scopeSummary` obligatorio ampliado (≥ 100 caracteres). |
| `InfrastructureConnectivity` | 1: diagrama o documento técnico (puede ser imagen/PDF)            |                                                                        |
| `MajorTechnicalSupport`      | 1: captura/evidencia del incidente                                |                                                                        |
| `InformationSecurity`        | Opcional; al menos un campo de hallazgo en texto obligatorio      | Ver sección 8.                                                         |
| `DataInteroperability`       | Opcional MVP; fuentes/destinos obligatorios en JSON               | Ver sección 9.                                                         |


**Validación al `Submit`:** el pipeline comprueba tipo + reglas de adjuntos y campos específicos.

---

## 3. `HardwareAcquisition` — campos específicos (`payload` JSON)


| Campo                      | Obligatorio | Validación                                     |
| -------------------------- | ----------- | ---------------------------------------------- |
| `quantity`                 | Sí          | Entero ≥ 1.                                    |
| `specification`            | Sí          | Mín. 20 caracteres (marca/modelo/componentes). |
| `replacementJustification` | Sí          | Si reemplazo, explicar fin de vida o daño.     |
| `installationLocation`     | Sí          | Texto (edificio/sala/puesto).                  |
| `compatibilityNotes`       | No          | Texto.                                         |


---

## 4. `SoftwareLicensing` — campos específicos


| Campo                       | Obligatorio | Validación                                          |
| --------------------------- | ----------- | --------------------------------------------------- |
| `productName`               | Sí          |                                                     |
| `licenseModel`              | Sí          | Enum: `Perpetual`, `Subscription`, `Other`.         |
| `seatOrUserCount`           | Sí          | Entero ≥ 1.                                         |
| `environment`               | Sí          | Enum: `Production`, `Test`, `Development`, `Mixed`. |
| `directoryOrSsoIntegration` | No          | Boolean + texto si true.                            |


---

## 5. `SystemDevelopment` — campos específicos


| Campo                   | Obligatorio | Validación                          |
| ----------------------- | ----------- | ----------------------------------- |
| `functionalScope`       | Sí          | Mín. 50 caracteres.                 |
| `affectedUsersEstimate` | Sí          | Entero ≥ 0 o rango texto permitido. |
| `systemsAffected`       | Sí          | Lista no vacía (strings).           |
| `acceptanceCriteria`    | Sí          | Lista mín. 1 ítem, máx. 20.         |
| `dependencies`          | No          | Texto.                              |


Si no hay adjunto de alcance: `functionalScope` mín. 100 caracteres y `acceptanceCriteria` mín. 2 ítems.

---

## 6. `InfrastructureConnectivity` — campos específicos


| Campo                  | Obligatorio | Validación                                                 |
| ---------------------- | ----------- | ---------------------------------------------------------- |
| `technicalDescription` | Sí          | Mín. 50 caracteres (si no hay diagrama adjunto, mín. 150). |
| `maintenanceWindow`    | Sí          | Texto (ventanas preferidas).                               |
| `riskNotes`            | No          | Texto.                                                     |


---

## 7. `MajorTechnicalSupport` — campos específicos


| Campo                        | Obligatorio | Validación                                                 |
| ---------------------------- | ----------- | ---------------------------------------------------------- |
| `symptoms`                   | Sí          | Mín. 20 caracteres.                                        |
| `impactDescription`          | Sí          | Enum impacto: `Individual`, `Department`, `Institutional`. |
| `affectedAreas`              | Sí          | Lista no vacía.                                            |
| `evidenceAttachmentRequired` | —           | Cubierto por adjunto mínimo tabla sección 2.               |


---

## 8. `InformationSecurity` — campos específicos


| Campo              | Obligatorio | Validación                                                           |
| ------------------ | ----------- | -------------------------------------------------------------------- |
| `assetOrSystem`    | Sí          |                                                                      |
| `controlType`      | Sí          | Enum sugerido: `Hardening`, `Review`, `Access`, `Incident`, `Other`. |
| `findingOrContext` | Sí          | Mín. 30 caracteres (hallazgo o contexto).                            |


---

## 9. `DataInteroperability` — campos específicos


| Campo                    | Obligatorio | Validación                                               |
| ------------------------ | ----------- | -------------------------------------------------------- |
| `sourceSystems`          | Sí          | Lista no vacía.                                          |
| `targetSystems`          | Sí          | Lista no vacía.                                          |
| `frequency`              | Sí          | Enum: `Realtime`, `Daily`, `Weekly`, `Monthly`, `AdHoc`. |
| `dataQualityExpectation` | Sí          | Texto mín. 20 caracteres.                                |
| `dataOwnerName`          | Sí          | Texto (responsable de datos).                            |


---

## 10. Esquema JSON de ejemplo (validación en API)

Por solicitud: `specificPayload` según `requestType` (un solo bloque validado con JSON Schema por tipo o validadores FluentValidation por estrategia).

```json
{
  "requestType": "SoftwareLicensing",
  "specificPayload": {
    "productName": "…",
    "licenseModel": "Subscription",
    "seatOrUserCount": 50,
    "environment": "Production",
    "directoryOrSsoIntegration": true
  }
}
```

---

## 11. Coherencia con transiciones

- En `Draft`, los campos comunes y `specificPayload` pueden estar incompletos; al `**Submit**`, se ejecutan todas las validaciones de este documento.
- El analista TIC puede completar ciertos campos técnicos si la política lo permite (definir lista de campos editables por rol en implementación).

