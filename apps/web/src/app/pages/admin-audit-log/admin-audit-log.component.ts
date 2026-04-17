import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormBuilder, ReactiveFormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { debounceTime, firstValueFrom } from "rxjs";
import { timeout } from "rxjs/operators";
import { ApiClientService, AdminAuditLogEntryDto, PagedResult } from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-admin-audit-log",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: "./admin-audit-log.component.html"
})
export class AdminAuditLogComponent implements OnInit {
  private readonly api = inject(ApiClientService);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  listError = "";
  busy = false;
  exportBusy = false;
  exportError = "";
  entries: AdminAuditLogEntryDto[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 25;

  readonly filterForm = this.fb.group({
    entityType: [""],
    action: [""]
  });

  get canAccess(): boolean {
    const r = this.auth.getPrimaryRoleFromToken() ?? "";
    return r === "SystemAdministrator" || r === "Auditor";
  }

  ngOnInit(): void {
    if (!this.canAccess) {
      return;
    }
    this.filterForm.valueChanges
      .pipe(debounceTime(280), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.loadPage(1));
    void this.loadPage(1);
  }

  async loadPage(page: number): Promise<void> {
    if (!this.canAccess) {
      return;
    }
    this.listError = "";
    this.busy = true;
    this.currentPage = page;
    try {
      const raw = this.filterForm.getRawValue();
      const entityType = (raw.entityType ?? "").trim();
      const action = (raw.action ?? "").trim();
      const res = await firstValueFrom(
        this.api
          .getAdminAuditLog(
            page,
            this.pageSize,
            entityType.length > 0 ? entityType : undefined,
            action.length > 0 ? action : undefined
          )
          .pipe(timeout(60_000))
      );
      this.applyPaged(res);
    } catch (e: unknown) {
      this.listError = getHttpErrorMessage(e, "No se pudo cargar el registro de auditoría.");
      this.entries = [];
      this.totalCount = 0;
    } finally {
      this.busy = false;
      this.cdr.detectChanges();
    }
  }

  async downloadCsv(): Promise<void> {
    if (!this.canAccess) {
      return;
    }
    this.exportBusy = true;
    this.exportError = "";
    try {
      const raw = this.filterForm.getRawValue();
      const entityType = (raw.entityType ?? "").trim();
      const action = (raw.action ?? "").trim();
      const res = await firstValueFrom(
        this.api
          .exportAdminAuditLogCsv(
            entityType.length > 0 ? entityType : undefined,
            action.length > 0 ? action : undefined
          )
          .pipe(timeout(120_000))
      );
      const blob = res.body;
      if (!blob) {
        throw new Error("Respuesta vacía");
      }
      const cd = res.headers.get("Content-Disposition");
      let fileName = `audit-log_${new Date().toISOString().slice(0, 19).replace(/[:T]/g, "-")}.csv`;
      const m = cd?.match(/filename\*?=(?:UTF-8'')?["']?([^\";]+)/i);
      if (m?.[1]) {
        try {
          fileName = decodeURIComponent(m[1].trim());
        } catch {
          fileName = m[1].trim();
        }
      }
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e: unknown) {
      this.exportError = getHttpErrorMessage(e, "No se pudo descargar el CSV de auditoría.");
    } finally {
      this.exportBusy = false;
      this.cdr.detectChanges();
    }
  }

  private applyPaged(res: PagedResult<AdminAuditLogEntryDto>): void {
    this.entries = res.items;
    this.totalCount = res.totalCount;
    this.currentPage = res.page;
    this.pageSize = res.pageSize;
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  shortUtc(iso: string): string {
    return iso.length >= 19 ? iso.slice(0, 19).replace("T", " ") : iso;
  }

  shortUserId(id: string): string {
    return id.length > 10 ? `${id.slice(0, 8)}…` : id;
  }
}
