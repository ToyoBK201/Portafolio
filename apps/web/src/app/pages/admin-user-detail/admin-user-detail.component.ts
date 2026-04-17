import { ChangeDetectorRef, Component, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { firstValueFrom, forkJoin } from "rxjs";
import { timeout } from "rxjs/operators";
import {
  ApiClientService,
  AppRoleOptionDto,
  AppUserDetailDto,
  OrganizationalUnitDto
} from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

const HTTP_TIMEOUT_MS = 60_000;

@Component({
  selector: "app-admin-user-detail",
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, RouterLink],
  templateUrl: "./admin-user-detail.component.html"
})
export class AdminUserDetailComponent {
  private readonly api = inject(ApiClientService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly auth = inject(AuthService);

  userId = "";
  detail: AppUserDetailDto | null = null;
  roleOptions: AppRoleOptionDto[] = [];
  units: OrganizationalUnitDto[] = [];

  loadError = "";
  saveError = "";
  busy = false;
  saving = false;

  readonly assignments = this.fb.array<FormGroup>([]);

  get isSystemAdministrator(): boolean {
    return this.auth.getPrimaryRoleFromToken() === "SystemAdministrator";
  }

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.userId = params.get("userId") ?? "";
      this.loadError = "";
      this.detail = null;
      if (!this.userId) {
        this.cdr.markForCheck();
        return;
      }
      if (!this.isSystemAdministrator) {
        this.cdr.markForCheck();
        return;
      }
      void this.loadAll();
    });
  }

  private rebuildAssignmentsFromDetail(d: AppUserDetailDto): void {
    this.assignments.clear();
    const roles = d.roles ?? [];
    for (const r of roles) {
      this.assignments.push(
        this.fb.group({
          roleId: [r.roleId, [Validators.required]],
          organizationalUnitId: [r.organizationalUnitId]
        })
      );
    }
  }

  async loadAll(): Promise<void> {
    if (!this.userId || !this.isSystemAdministrator) {
      return;
    }
    this.loadError = "";
    this.busy = true;
    this.detail = null;
    this.cdr.markForCheck();
    try {
      const { detail, rolesRes, unitsRes } = await firstValueFrom(
        forkJoin({
          detail: this.api.getAdminUser(this.userId).pipe(timeout(HTTP_TIMEOUT_MS)),
          rolesRes: this.api.getAppRolesCatalog().pipe(timeout(HTTP_TIMEOUT_MS)),
          unitsRes: this.api.getOrganizationalUnitsCatalog(true).pipe(timeout(HTTP_TIMEOUT_MS))
        })
      );
      this.detail = detail;
      this.roleOptions = rolesRes.items;
      this.units = unitsRes.items;
      this.rebuildAssignmentsFromDetail(detail);
    } catch (e) {
      this.loadError = getHttpErrorMessage(e, "No se pudo cargar el usuario.");
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  assignmentAt(i: number): FormGroup {
    return this.assignments.at(i) as FormGroup;
  }

  addAssignmentRow(): void {
    const firstRoleId = this.roleOptions[0]?.roleId ?? 1;
    this.assignments.push(
      this.fb.group({
        roleId: [firstRoleId, [Validators.required]],
        organizationalUnitId: [null as number | null]
      })
    );
  }

  removeAssignmentRow(i: number): void {
    this.assignments.removeAt(i);
  }

  async saveRoles(): Promise<void> {
    if (this.assignments.invalid) {
      this.assignments.markAllAsTouched();
      return;
    }
    this.saveError = "";
    this.saving = true;
    try {
      const list = this.assignments.getRawValue() as { roleId: number; organizationalUnitId: number | null }[];
      const assignments = list.map((x) => ({
        roleId: Number(x.roleId),
        organizationalUnitId:
          x.organizationalUnitId === null || x.organizationalUnitId === undefined ? null : Number(x.organizationalUnitId)
      }));
      await firstValueFrom(this.api.putAdminUserRoles(this.userId, { assignments }));
      await this.loadAll();
    } catch (e) {
      this.saveError = getHttpErrorMessage(e, "No se pudieron guardar los roles.");
    } finally {
      this.saving = false;
      this.cdr.detectChanges();
    }
  }

}
