# Alcance MVP congelado — Solicitudes Tecnológicas Gubernamentales

**Versión:** 1.0  
**Estado:** Congelado para implementación del MVP  
**Propósito:** Fijar decisiones explícitas sobre qué entra y qué no en la primera versión operable, evitando ambigüedad entre negocio, Angular, .NET y SQL Server.

---

## 1. Resumen ejecutivo

El MVP no es “lo mínimo programable”, sino el **conjunto mínimo que permite operar el proceso real** con trazabilidad, roles y control. Este documento **congela** inclusiones, exclusiones y supuestos que deben validarse con stakeholders (ajustes posteriores = cambio de alcance versionado).

---

## 2. Decisiones congeladas (MVP)

### 2.1 Ámbito de usuarios: interno institucional


| Decisión                       | Valor MVP                                                                                                            |
| ------------------------------ | -------------------------------------------------------------------------------------------------------------------- |
| **Usuarios principales**       | Personal de la institución (funcionarios/contratistas con cuenta institucional).                                     |
| **Portal ciudadano / externo** | **Fuera de alcance** en MVP. Si en el futuro se requiere, se define como módulo o sistema acoplado por API (fase 2). |
| **Justificación**              | Reduce riesgo legal, autenticación y soporte; permite entregar trazabilidad y aprobaciones internas primero.         |


**Validación con stakeholders:** confirmar que ningún trámite ciudadano es obligatorio en el plazo del MVP.

### 2.2 Notificaciones


| Decisión               | Valor MVP                                                                                                                                                                      |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Obligatorio**        | Bandeja **in-app**: “Mis pendientes”, “Actividad reciente”, contadores por rol.                                                                                                |
| **Correo electrónico** | **Opcional** en MVP: si existe SMTP institucional, se envían notificaciones en eventos clave (envío, devolución, aprobación, asignación, cierre). Si no hay SMTP, solo in-app. |
| **SMS / push móvil**   | Fuera de alcance.                                                                                                                                                              |


### 2.3 Adjuntos


| Decisión                   | Valor MVP                                                                                                                                    |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| **Almacenamiento**         | Archivos en almacén compatible con .NET (ruta local dev / blob o file share en producción); **metadatos en SQL Server**.                     |
| **Límites**                | Tamaño máximo por archivo: **10 MB** (ajustable por parámetro de sistema). Máximo **10 archivos** por solicitud en MVP.                      |
| **Tipos permitidos**       | Lista blanca: `pdf`, `png`, `jpg`, `jpeg`, `docx`, `xlsx`, `csv`, `zip` (sin ejecutables).                                                   |
| **Antivirus**              | **Deseable** post-MVP o si hay servicio institucional; MVP: validación de extensión + MIME + tamaño.                                         |
| **Versionado de archivos** | MVP: un adjunto = una versión; reemplazo genera nuevo registro vinculado o nuevo adjunto (definir en modelo; no sobrescribir sin historial). |


### 2.4 Comentarios y visibilidad


| Decisión                       | Valor MVP                                                                                                                    |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| **Comentarios**                | Por solicitud, con autor, fecha y texto.                                                                                     |
| **Notas internas**             | Comentarios marcados como **internos** visibles solo para roles TIC, Aprobador, Admin, Auditor (no visibles al Solicitante). |
| **Comentarios al solicitante** | Visibles para el solicitante (sin marca interna).                                                                            |


### 2.5 Identidad y seguridad


| Decisión                       | Valor MVP                                                                                                                                                    |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Autenticación**              | Integración con proveedor institucional si existe (**Azure AD / AD / OIDC**); si no, cuenta local con política de contraseña mínima (definir en despliegue). |
| **Autorización**               | RBAC en aplicación; permisos alineados con [02-matriz-rbac.md](./02-matriz-rbac.md).                                                                         |
| **Firma electrónica avanzada** | Fuera de alcance; decisión registrada en auditoría con usuario/rol/fecha/IP.                                                                                 |


---

## 3. Incluye / excluye (tabla única)

### Incluye (v1)

- Ciclo de vida completo: crear → análisis → aprobación → ejecución → validación → cierre.
- Estados y transiciones según contrato [03-estados-y-contrato-api.md](./03-estados-y-contrato-api.md).
- Catálogos maestros mínimos (tipos, prioridad, unidad organizativa).
- Adjuntos con reglas de la sección 2.3.
- Auditoría según [05-auditoria-esquema.md](./05-auditoria-esquema.md).
- Reportes exportables (CSV/Excel) desde listados filtrados.

### Excluye (post-MVP)

- Integración en tiempo real con ERP/presupuesto.
- Motor BPM gráfico editable por usuario de negocio.
- Portal ciudadano, firma avanzada, SLA con escalamiento multi-nivel complejo.
- Reapertura de solicitudes cerradas (nueva solicitud vinculada en fase 2).

---

## 4. Criterio de aceptación del MVP (negocio)

1. Una solicitud puede recorrer **todo el flujo** sin pasos manuales fuera del sistema (excepto carga de datos maestros por Admin).
2. Cada cambio de estado deja **registro auditable** y **motivo** donde las reglas lo exigen.
3. Los roles solo ven y hacen lo definido en la matriz RBAC.

---

## 5. Registro de cambios posteriores


| Fecha | Cambio | Aprobado por |
| ----- | ------ | ------------ |
|       |        |              |


Cualquier cambio a este documento debe incrementar la versión y anotarse aquí.