import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, ValidatorFn, Validators } from "@angular/forms";

export const REQUEST_TYPE_OPTIONS: { id: number; label: string }[] = [
  { id: 1, label: "Adquisición de hardware" },
  { id: 2, label: "Licenciamiento de software" },
  { id: 3, label: "Desarrollo o evolutivo de sistema" },
  { id: 4, label: "Infraestructura y conectividad" },
  { id: 5, label: "Soporte técnico / incidente mayor" },
  { id: 6, label: "Seguridad de la información" },
  { id: 7, label: "Datos / interoperabilidad" }
];

export const PRIORITY_OPTIONS: { id: number; label: string }[] = [
  { id: 1, label: "Baja" },
  { id: 2, label: "Media" },
  { id: 3, label: "Alta" },
  { id: 4, label: "Crítica" }
];

function linesToArray(text: string | null | undefined): string[] {
  return String(text ?? "")
    .split(/\r?\n/)
    .map((s) => s.trim())
    .filter(Boolean);
}

function listLinesValidator(minLines: number, maxLines: number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const n = linesToArray(control.value).length;
    if (n < minLines) {
      return { minLines: { min: minLines, actual: n } };
    }
    if (n > maxLines) {
      return { maxLines: { max: maxLines, actual: n } };
    }
    return null;
  };
}

function softwareSsoValidator(group: AbstractControl): ValidationErrors | null {
  const g = group as FormGroup;
  const use = g.get("directoryOrSsoIntegration")?.value === true;
  const details = String(g.get("directoryOrSsoDetails")?.value ?? "").trim();
  if (use && details.length < 3) {
    return { ssoDetails: true };
  }
  return null;
}

export function buildSpecificPayloadGroup(fb: FormBuilder, requestTypeId: number): FormGroup {
  switch (requestTypeId) {
    case 1:
      return fb.group({
        quantity: [1, [Validators.required, Validators.min(1)]],
        specification: [
          "Equipos de escritorio estándar marca HP/Dell con SSD y 16GB RAM para renovación de puesto.",
          [Validators.required, Validators.minLength(20)]
        ],
        replacementJustification: [
          "Fin de vida del hardware actual y fallas recurrentes en disco.",
          [Validators.required, Validators.minLength(10)]
        ],
        installationLocation: ["Edificio central — sala de sistemas y puestos administrativos 2.º piso", [Validators.required]],
        compatibilityNotes: [""]
      });
    case 2:
      return fb.group(
        {
          productName: ["Licenciamiento suite ofimática institucional", [Validators.required]],
          licenseModel: ["Subscription", [Validators.required]],
          seatOrUserCount: [50, [Validators.required, Validators.min(1)]],
          environment: ["Production", [Validators.required]],
          directoryOrSsoIntegration: [false],
          directoryOrSsoDetails: [""]
        },
        { validators: [softwareSsoValidator] }
      );
    case 3:
      return fb.group({
        functionalScope: [
          "Automatizar el flujo de aprobación de solicitudes TIC con notificaciones y trazabilidad completa.",
          [Validators.required, Validators.minLength(50)]
        ],
        affectedUsersEstimate: [120, [Validators.required, Validators.min(0)]],
        systemsAffectedText: ["Sistema de gestión documental\nPortal intranet", [Validators.required, listLinesValidator(1, 200)]],
        acceptanceCriteriaText: ["Usuarios reciben confirmación por correo\nReporte exportable CSV", [Validators.required, listLinesValidator(1, 20)]],
        dependencies: ["Integración con directorio activo institucional."]
      });
    case 4:
      return fb.group({
        technicalDescription: [
          "Ampliación de ancho de banda en enlace principal y firewall perimetral con reglas de segmentación detalladas.",
          [Validators.required, Validators.minLength(50)]
        ],
        maintenanceWindow: [
          "Ventanas preferidas: sábados 02:00–08:00 UTC, coordinar con TIC.",
          [Validators.required]
        ],
        riskNotes: [""]
      });
    case 5:
      return fb.group({
        symptoms: [
          "Caída intermitente del servicio de correo y latencia >10s en portal interno.",
          [Validators.required, Validators.minLength(20)]
        ],
        impactDescription: ["Institutional", [Validators.required]],
        affectedAreasText: ["Dirección general\nSecretaría académica", [Validators.required, listLinesValidator(1, 100)]]
      });
    case 6:
      return fb.group({
        assetOrSystem: ["Portal de trámites — módulo autenticación", [Validators.required]],
        controlType: ["Review", [Validators.required]],
        findingOrContext: [
          "Revisión periódica detectó configuración de sesión sin bloqueo tras inactividad en terminales compartidas.",
          [Validators.required, Validators.minLength(30)]
        ]
      });
    case 7:
      return fb.group({
        sourceSystemsText: ["ERP legado SQL\nSistema de inventario", [Validators.required, listLinesValidator(1, 200)]],
        targetSystemsText: ["Data lake institucional\nAPI de reporting", [Validators.required, listLinesValidator(1, 200)]],
        frequency: ["Daily", [Validators.required]],
        dataQualityExpectation: [
          "Validación de claves únicas y rechazo de duplicados con log de incidencias.",
          [Validators.required, Validators.minLength(20)]
        ],
        dataOwnerName: ["Coordinación de datos institucionales", [Validators.required]]
      });
    default:
      return fb.group({});
  }
}

/** Serializa el bloque `specific` del formulario a JSON para `specificPayloadJson`. */
export function serializeSpecificPayload(requestTypeId: number, specific: Record<string, unknown>): string {
  switch (requestTypeId) {
    case 1:
      return JSON.stringify({
        quantity: Number(specific["quantity"] ?? 1),
        specification: String(specific["specification"] ?? "").trim(),
        replacementJustification: String(specific["replacementJustification"] ?? "").trim(),
        installationLocation: String(specific["installationLocation"] ?? "").trim(),
        compatibilityNotes: trimOrNull(specific["compatibilityNotes"])
      });
    case 2: {
      const dir = specific["directoryOrSsoIntegration"] === true;
      const o: Record<string, unknown> = {
        productName: String(specific["productName"] ?? "").trim(),
        licenseModel: String(specific["licenseModel"] ?? "").trim(),
        seatOrUserCount: Number(specific["seatOrUserCount"] ?? 0),
        environment: String(specific["environment"] ?? "").trim(),
        directoryOrSsoIntegration: dir
      };
      if (dir) {
        o["directoryOrSsoDetails"] = String(specific["directoryOrSsoDetails"] ?? "").trim();
      }
      return JSON.stringify(o);
    }
    case 3: {
      const o: Record<string, unknown> = {
        functionalScope: String(specific["functionalScope"] ?? "").trim(),
        affectedUsersEstimate: Number(specific["affectedUsersEstimate"] ?? 0),
        systemsAffected: linesToArray(specific["systemsAffectedText"] as string),
        acceptanceCriteria: linesToArray(specific["acceptanceCriteriaText"] as string)
      };
      const dep = trimOrNull(specific["dependencies"]);
      if (dep !== null) {
        o["dependencies"] = dep;
      }
      return JSON.stringify(o);
    }
    case 4:
      return JSON.stringify({
        technicalDescription: String(specific["technicalDescription"] ?? "").trim(),
        maintenanceWindow: String(specific["maintenanceWindow"] ?? "").trim(),
        riskNotes: trimOrNull(specific["riskNotes"])
      });
    case 5:
      return JSON.stringify({
        symptoms: String(specific["symptoms"] ?? "").trim(),
        impactDescription: String(specific["impactDescription"] ?? "").trim(),
        affectedAreas: linesToArray(specific["affectedAreasText"] as string)
      });
    case 6:
      return JSON.stringify({
        assetOrSystem: String(specific["assetOrSystem"] ?? "").trim(),
        controlType: String(specific["controlType"] ?? "").trim(),
        findingOrContext: String(specific["findingOrContext"] ?? "").trim()
      });
    case 7:
      return JSON.stringify({
        sourceSystems: linesToArray(specific["sourceSystemsText"] as string),
        targetSystems: linesToArray(specific["targetSystemsText"] as string),
        frequency: String(specific["frequency"] ?? "").trim(),
        dataQualityExpectation: String(specific["dataQualityExpectation"] ?? "").trim(),
        dataOwnerName: String(specific["dataOwnerName"] ?? "").trim()
      });
    default:
      return JSON.stringify({});
  }
}

function trimOrNull(v: unknown): string | null {
  const s = String(v ?? "").trim();
  return s.length > 0 ? s : null;
}
