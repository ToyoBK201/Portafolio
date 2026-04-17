import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormBuilder, ReactiveFormsModule } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { debounceTime, firstValueFrom, merge, timeout } from "rxjs";
import {
  ApiClientService,
  PagedResult,
  RequestListFilters,
  RequestDraftResponse
} from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { DEFAULT_REQUESTER_USER_ID } from "../../core/app.constants";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-bandeja",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: "./bandeja.component.html"
})
export class BandejaComponent implements OnInit {
  private readonly apiClient = inject(ApiClientService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  listError = "";
  requests: RequestDraftResponse[] = [];
  totalRequests = 0;
  currentPage = 1;
  pageSize = 10;
  loadingList = false;
  exportBusy = false;
  exportError = "";

  readonly listFiltersForm = this.fb.group({
    status: [""],
    requesterUserId: [DEFAULT_REQUESTER_USER_ID],
    sortBy: ["createdAtUtc"],
    sortDirection: ["desc"]
  });

  ngOnInit(): void {
    const role = this.auth.getPrimaryRoleFromToken();
    const patch: Record<string, string> = {};
    if (role === "Requester") {
      patch["requesterUserId"] = this.auth.getUserIdFromToken() ?? DEFAULT_REQUESTER_USER_ID;
    } else {
      patch["requesterUserId"] = "";
    }
    const statusFromRoute = this.route.snapshot.queryParamMap.get("status");
    if (statusFromRoute) {
      patch["status"] = statusFromRoute;
    } else {
      const defaultStatus = this.defaultStatusFilterForRole(role);
      patch["status"] = defaultStatus ?? "";
    }
    this.listFiltersForm.patchValue(patch, { emitEvent: false });

    merge(
      this.listFiltersForm.get("status")!.valueChanges.pipe(debounceTime(280)),
      this.listFiltersForm.get("requesterUserId")!.valueChanges.pipe(debounceTime(280)),
      this.listFiltersForm.get("sortBy")!.valueChanges,
      this.listFiltersForm.get("sortDirection")!.valueChanges
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.loadRequestsPage(1));

    void this.loadRequestsPage(1);
  }

  /** Colas sugeridas por rol (docs/06 B4); el usuario puede borrar el filtro. */
  private defaultStatusFilterForRole(role: string | null): string {
    switch (role) {
      case "TicAnalyst":
        return "Submitted";
      case "InstitutionalApprover":
        return "PendingApproval";
      case "Implementer":
        return "InProgress";
      default:
        return "";
    }
  }

  async loadRequests(): Promise<void> {
    await this.loadRequestsPage(1);
  }

  async nextPage(): Promise<void> {
    if (this.currentPage * this.pageSize >= this.totalRequests) {
      return;
    }
    await this.loadRequestsPage(this.currentPage + 1);
  }

  async previousPage(): Promise<void> {
    if (this.currentPage <= 1) {
      return;
    }
    await this.loadRequestsPage(this.currentPage - 1);
  }

  private async loadRequestsPage(page: number): Promise<void> {
    this.loadingList = true;
    this.listError = "";
    try {
      const raw = this.listFiltersForm.getRawValue();
      const filters: RequestListFilters = {};
      if (raw.status) {
        filters.status = raw.status;
      }
      if (raw.requesterUserId) {
        filters.requesterUserId = raw.requesterUserId;
      }
      filters.page = page;
      filters.pageSize = this.pageSize;
      filters.sortBy = (raw.sortBy as RequestListFilters["sortBy"]) ?? "createdAtUtc";
      filters.sortDirection = (raw.sortDirection as RequestListFilters["sortDirection"]) ?? "desc";

      const result: PagedResult<RequestDraftResponse> = await firstValueFrom(
        this.apiClient.listRequests(filters).pipe(timeout(60_000))
      );
      this.requests = result.items;
      this.totalRequests = result.totalCount;
      this.currentPage = result.page;
    } catch (err: unknown) {
      this.listError = getHttpErrorMessage(
        err,
        "No se pudo cargar la bandeja. ¿Token en Auth (dev) y API + SQL en marcha?"
      );
      this.requests = [];
      this.totalRequests = 0;
    } finally {
      this.loadingList = false;
      this.cdr.detectChanges();
    }
  }

  async downloadCsv(): Promise<void> {
    await this.downloadExport("csv");
  }

  async downloadXlsx(): Promise<void> {
    await this.downloadExport("xlsx");
  }

  private async downloadExport(format: "csv" | "xlsx"): Promise<void> {
    this.exportBusy = true;
    this.exportError = "";
    try {
      const raw = this.listFiltersForm.getRawValue();
      const res = await firstValueFrom(
        this.apiClient
          .exportRequestsFile(format, {
            status: raw.status || undefined,
            requesterUserId: raw.requesterUserId || undefined,
            sortBy: (raw.sortBy as RequestListFilters["sortBy"]) ?? "createdAtUtc",
            sortDirection: (raw.sortDirection as RequestListFilters["sortDirection"]) ?? "desc",
            maxRows: 5000
          })
          .pipe(timeout(120_000))
      );
      const blob = res.body;
      if (!blob) {
        throw new Error("Respuesta vacía");
      }
      const cd = res.headers.get("Content-Disposition");
      const ext = format === "csv" ? ".csv" : ".xlsx";
      let fileName = `solicitudes_${new Date().toISOString().slice(0, 19).replace(/[:T]/g, "-")}${ext}`;
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
    } catch (err: unknown) {
      this.exportError = getHttpErrorMessage(
        err,
        format === "csv" ? "No se pudo exportar el CSV." : "No se pudo exportar el Excel."
      );
    } finally {
      this.exportBusy = false;
      this.cdr.detectChanges();
    }
  }
}
