import { HttpErrorResponse } from "@angular/common/http";
import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { firstValueFrom, timeout } from "rxjs";
import {
  ApiClientService,
  CreateDraftRequest,
  OrganizationalUnitDto,
  RequestDraftResponse,
  UpdateDraftRequest
} from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";
import { DEFAULT_REQUESTER_USER_ID } from "../../core/app.constants";
import {
  PRIORITY_OPTIONS,
  REQUEST_TYPE_OPTIONS,
  buildSpecificPayloadGroup,
  serializeSpecificPayload
} from "./specific-payload-builders";

@Component({
  selector: "app-draft-form",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: "./draft-form.component.html"
})
export class DraftFormComponent implements OnInit {
  private readonly apiClient = inject(ApiClientService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly requestTypeOptions = REQUEST_TYPE_OPTIONS;
  readonly priorityOptions = PRIORITY_OPTIONS;

  /** Si viene de la ruta `solicitudes/:id/editar`, guarda el id para PATCH. */
  editRequestId: string | null = null;

  submitting = false;
  submitError = "";
  fetchError = "";
  /** Mensaje tras PATCH exitoso (evita confundir con la carga inicial). */
  updateSuccessMessage = "";
  createdDraft: RequestDraftResponse | null = null;
  loadedDraft: RequestDraftResponse | null = null;

  /** Catálogo D1 — `activeOnly=false` para poder mostrar la unidad al editar aunque esté inactiva. */
  organizationalUnits: OrganizationalUnitDto[] = [];
  loadingUnits = false;
  unitsCatalogError = "";

  readonly draftForm = this.fb.group({
    title: ["Solicitud de licenciamiento", [Validators.required, Validators.minLength(5), Validators.maxLength(200)]],
    description: [
      "Se requiere adquirir nuevas licencias de software para personal operativo.",
      [Validators.required, Validators.minLength(20), Validators.maxLength(8000)]
    ],
    businessJustification: [
      "Cumplir continuidad operativa y lineamientos institucionales.",
      [Validators.required, Validators.minLength(20)]
    ],
    requestType: [2, [Validators.required, Validators.min(1), Validators.max(7)]],
    priority: [2, [Validators.required, Validators.min(1), Validators.max(4)]],
    requestingUnitId: [1, [Validators.required, Validators.min(1)]],
    desiredDate: [""],
    specific: buildSpecificPayloadGroup(this.fb, 2)
  });

  ngOnInit(): void {
    this.draftForm
      .get("requestType")!
      .valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((v) => {
        const t = Number(v);
        if (Number.isFinite(t) && t >= 1 && t <= 7) {
          this.draftForm.setControl("specific", buildSpecificPayloadGroup(this.fb, t));
        }
      });

    void this.bootstrap();
  }

  private async bootstrap(): Promise<void> {
    await this.loadOrganizationalUnits();
    const routeId = this.route.snapshot.paramMap.get("id");
    if (routeId) {
      await this.loadExistingDraftForEdit(routeId);
    } else {
      this.applyDefaultRequestingUnitFromCatalog();
    }
  }

  private async loadOrganizationalUnits(): Promise<void> {
    this.unitsCatalogError = "";
    this.loadingUnits = true;
    try {
      const res = await firstValueFrom(
        this.apiClient.getOrganizationalUnitsCatalog(false).pipe(timeout(30_000))
      );
      this.organizationalUnits = res.items ?? [];
    } catch (err: unknown) {
      this.unitsCatalogError = getHttpErrorMessage(err, "No se pudo cargar el catálogo de unidades.");
      this.organizationalUnits = [];
    } finally {
      this.loadingUnits = false;
      this.cdr.detectChanges();
    }
  }

  /** Primera unidad activa (o la primera del catálogo) solo en alta; la edición pisa el valor. */
  private applyDefaultRequestingUnitFromCatalog(): void {
    const active = this.organizationalUnits.filter((u) => u.isActive);
    const pick = (active.length > 0 ? active : this.organizationalUnits)[0];
    if (pick) {
      this.draftForm.patchValue({ requestingUnitId: pick.unitId });
    }
  }

  /** Si el borrador referencia un id que no vino en el catálogo, añade una opción para no perder el valor. */
  private ensureRequestingUnitOption(unitId: number): void {
    if (!Number.isFinite(unitId) || unitId < 1) {
      return;
    }
    if (this.organizationalUnits.some((u) => u.unitId === unitId)) {
      return;
    }
    this.organizationalUnits = [
      ...this.organizationalUnits,
      {
        unitId,
        code: "?",
        name: `Unidad ${unitId} (no listada en catálogo)`,
        isActive: false,
        createdAtUtc: ""
      }
    ];
  }

  private requesterUserIdForCreate(): string {
    return this.auth.getUserIdFromToken() ?? DEFAULT_REQUESTER_USER_ID;
  }

  private patchSpecificFromJson(fg: FormGroup, data: Record<string, unknown>): void {
    for (const key of Object.keys(fg.controls)) {
      if (!(key in data)) {
        continue;
      }
      const c = fg.get(key);
      if (!c) {
        continue;
      }
      const v = data[key];
      if (v === undefined || v === null) {
        continue;
      }
      c.patchValue(v, { emitEvent: false });
    }
  }

  private async loadExistingDraftForEdit(id: string): Promise<void> {
    this.fetchError = "";
    this.editRequestId = null;
    try {
      const r = await firstValueFrom(this.apiClient.getRequestById(id).pipe(timeout(30_000)));
      if (String(r.status).toLowerCase() !== "draft") {
        this.fetchError = "Solo se pueden editar solicitudes en borrador (Draft).";
        return;
      }

      this.editRequestId = id;
      this.loadedDraft = r;

      let parsed: Record<string, unknown> = {};
      if (r.specificPayloadJson) {
        try {
          parsed = JSON.parse(r.specificPayloadJson) as Record<string, unknown>;
        } catch {
          parsed = {};
        }
      }

      const rt = Number(r.requestType);
      const desired =
        r.desiredDate && String(r.desiredDate).length >= 10 ? String(r.desiredDate).slice(0, 10) : "";

      this.draftForm.patchValue(
        {
          title: r.title,
          description: r.description,
          businessJustification: r.businessJustification,
          requestType: rt,
          priority: r.priority,
          requestingUnitId: r.requestingUnitId,
          desiredDate: desired
        },
        { emitEvent: false }
      );

      this.ensureRequestingUnitOption(r.requestingUnitId);

      this.draftForm.setControl("specific", buildSpecificPayloadGroup(this.fb, rt));
      const specFg = this.draftForm.get("specific") as FormGroup;
      this.patchSpecificFromJson(specFg, parsed);
    } catch (err: unknown) {
      this.fetchError = getHttpErrorMessage(err, "No se pudo cargar el borrador para editar.");
      this.editRequestId = null;
      this.loadedDraft = null;
    } finally {
      this.cdr.detectChanges();
    }
  }

  selectedRequestType(): number {
    const v = Number(this.draftForm.get("requestType")?.value);
    return Number.isFinite(v) && v >= 1 && v <= 7 ? v : 2;
  }

  async createDraft(): Promise<void> {
    if (this.draftForm.invalid) {
      this.draftForm.markAllAsTouched();
      this.submitError =
        "Hay campos con errores de validación (revisa mensajes debajo). " +
        "Reglas habituales: título mín. 5 caracteres; descripción y justificación de negocio mín. 20; " +
        "en licenciamiento, si marcas integración SSO el detalle es obligatorio.";
      return;
    }

    this.submitting = true;
    this.submitError = "";
    this.fetchError = "";
    this.updateSuccessMessage = "";
    if (!this.editRequestId) {
      this.createdDraft = null;
      this.loadedDraft = null;
    }

    try {
      const raw = this.draftForm.getRawValue();
      const requestType = Number(raw.requestType ?? 0);
      const specificPayloadJson = serializeSpecificPayload(
        requestType,
        (raw.specific ?? {}) as Record<string, unknown>
      );

      if (this.editRequestId) {
        const patchBody: UpdateDraftRequest = {
          title: raw.title ?? "",
          description: raw.description ?? "",
          businessJustification: raw.businessJustification ?? "",
          requestType,
          priority: Number(raw.priority ?? 0),
          requestingUnitId: Number(raw.requestingUnitId ?? 0),
          desiredDate: raw.desiredDate ? String(raw.desiredDate) : null,
          specificPayloadJson
        };
        const updated = await firstValueFrom(
          this.apiClient.patchRequestDraft(this.editRequestId, patchBody).pipe(timeout(60_000))
        );
        this.loadedDraft = updated;
        this.createdDraft = null;
        this.updateSuccessMessage = "Cambios guardados correctamente.";
      } else {
        const payload: CreateDraftRequest = {
          title: raw.title ?? "",
          description: raw.description ?? "",
          businessJustification: raw.businessJustification ?? "",
          requestType,
          priority: Number(raw.priority ?? 0),
          requestingUnitId: Number(raw.requestingUnitId ?? 0),
          requesterUserId: this.requesterUserIdForCreate(),
          desiredDate: raw.desiredDate ? String(raw.desiredDate) : null,
          specificPayloadJson
        };

        this.createdDraft = await firstValueFrom(
          this.apiClient.createDraft(payload).pipe(timeout(60_000))
        );
      }
    } catch (err: unknown) {
      this.submitError = getHttpErrorMessage(
        err,
        this.editRequestId
          ? "No se pudo guardar el borrador. Revisa validaciones y token."
          : "No se pudo crear el borrador. Revisa validaciones, token en Auth (dev) y que SQL Server esté disponible."
      );
      if (
        this.editRequestId &&
        err instanceof HttpErrorResponse &&
        err.status === 409
      ) {
        await this.loadExistingDraftForEdit(this.editRequestId);
        this.submitError =
          "La solicitud ya no está en borrador o cambió en el servidor. Se actualizó el formulario con los datos actuales.";
      }
    } finally {
      this.submitting = false;
      this.cdr.detectChanges();
    }
  }

  async loadDraftFromApi(): Promise<void> {
    if (!this.createdDraft?.requestId) {
      return;
    }

    this.fetchError = "";
    try {
      this.loadedDraft = await firstValueFrom(
        this.apiClient.getRequestById(this.createdDraft.requestId).pipe(timeout(30_000))
      );
    } catch (err: unknown) {
      this.fetchError = getHttpErrorMessage(err, "No se pudo consultar el borrador por ID.");
      this.loadedDraft = null;
    } finally {
      this.cdr.detectChanges();
    }
  }
}
