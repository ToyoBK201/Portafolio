import { ChangeDetectorRef, Component, OnInit, inject } from "@angular/core";
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { firstValueFrom } from "rxjs";
import { ApiClientService, OrganizationalUnitDto } from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-admin-organizational-units",
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: "./admin-organizational-units.component.html"
})
export class AdminOrganizationalUnitsComponent implements OnInit {
  private readonly api = inject(ApiClientService);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);

  listError = "";
  busy = false;
  rowBusy: Record<number, boolean> = {};
  activeOnly = false;

  readonly createForm = this.fb.group({
    code: ["", [Validators.required, Validators.maxLength(32)]],
    name: ["", [Validators.required, Validators.maxLength(200)]]
  });

  readonly rows = this.fb.array<FormGroup>([]);

  /** Filtro en cliente sobre el catálogo cargado. */
  readonly listFilter = this.fb.nonNullable.control("");

  get isSystemAdministrator(): boolean {
    return this.auth.getPrimaryRoleFromToken() === "SystemAdministrator";
  }

  /** Índices de filas visibles según el filtro de texto. */
  get filteredRowIndices(): number[] {
    const q = (this.listFilter.value ?? "").trim().toLowerCase();
    const n = this.rows.length;
    if (!q) {
      return Array.from({ length: n }, (_, i) => i);
    }
    const out: number[] = [];
    for (let i = 0; i < n; i++) {
      const g = this.rowGroupAt(i);
      const id = String(g.get("unitId")?.value ?? "");
      const code = String(g.get("code")?.value ?? "").toLowerCase();
      const name = String(g.get("name")?.value ?? "").toLowerCase();
      if (id.includes(q) || code.includes(q) || name.includes(q)) {
        out.push(i);
      }
    }
    return out;
  }

  ngOnInit(): void {
    void this.reload();
  }

  async reload(): Promise<void> {
    this.listError = "";
    this.busy = true;
    try {
      const res = await firstValueFrom(this.api.getOrganizationalUnitsCatalog(this.activeOnly));
      this.rebuildRows(res.items);
    } catch (e) {
      this.listError = getHttpErrorMessage(e, "No se pudo cargar el catálogo de unidades.");
    } finally {
      this.busy = false;
      this.cdr.detectChanges();
    }
  }

  private rebuildRows(items: OrganizationalUnitDto[]): void {
    this.rows.clear();
    for (const u of items) {
      this.rows.push(
        this.fb.group({
          unitId: [u.unitId],
          code: [u.code, [Validators.required, Validators.maxLength(32)]],
          name: [u.name, [Validators.required, Validators.maxLength(200)]],
          isActive: [u.isActive],
          createdAtUtc: [u.createdAtUtc]
        })
      );
    }
  }

  async toggleActiveOnly(): Promise<void> {
    this.activeOnly = !this.activeOnly;
    await this.reload();
  }

  async createUnit(): Promise<void> {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    const raw = this.createForm.getRawValue();
    this.listError = "";
    this.busy = true;
    try {
      await firstValueFrom(
        this.api.postAdminOrganizationalUnit({
          code: (raw.code ?? "").trim(),
          name: (raw.name ?? "").trim()
        })
      );
      this.createForm.reset({ code: "", name: "" });
      await this.reload();
    } catch (e) {
      this.listError = getHttpErrorMessage(e, "No se pudo crear la unidad.");
    } finally {
      this.busy = false;
      this.cdr.detectChanges();
    }
  }

  rowGroupAt(i: number): FormGroup {
    return this.rows.at(i) as FormGroup;
  }

  async saveRow(i: number): Promise<void> {
    const g = this.rowGroupAt(i);
    if (g.invalid) {
      g.markAllAsTouched();
      return;
    }
    const unitId = Number(g.get("unitId")?.value);
    const code = String(g.get("code")?.value ?? "").trim();
    const name = String(g.get("name")?.value ?? "").trim();
    const isActive = Boolean(g.get("isActive")?.value);
    this.listError = "";
    this.rowBusy[unitId] = true;
    try {
      await firstValueFrom(this.api.patchAdminOrganizationalUnit(unitId, { code, name, isActive }));
      await this.reload();
    } catch (e) {
      this.listError = getHttpErrorMessage(e, "No se pudo actualizar la unidad.");
    } finally {
      delete this.rowBusy[unitId];
      this.cdr.detectChanges();
    }
  }
}
