import { ChangeDetectorRef, Component, OnInit, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { firstValueFrom } from "rxjs";
import { ApiClientService, AppUserListItemDto, PagedResult } from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-admin-users",
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: "./admin-users.component.html"
})
export class AdminUsersComponent implements OnInit {
  private readonly api = inject(ApiClientService);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);

  listError = "";
  busy = false;
  users: AppUserListItemDto[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 20;

  readonly createForm = this.fb.group({
    email: ["", [Validators.required, Validators.email]],
    displayName: ["", [Validators.required, Validators.minLength(2)]]
  });

  /** Filtro en cliente sobre la página cargada (la API no expone búsqueda por texto). */
  readonly listFilter = this.fb.nonNullable.control("");

  get isSystemAdministrator(): boolean {
    return this.auth.getPrimaryRoleFromToken() === "SystemAdministrator";
  }

  get filteredUsers(): AppUserListItemDto[] {
    const q = (this.listFilter.value ?? "").trim().toLowerCase();
    if (!q) {
      return this.users;
    }
    return this.users.filter(
      (u) =>
        u.email.toLowerCase().includes(q) ||
        u.displayName.toLowerCase().includes(q) ||
        String(u.userId).toLowerCase().includes(q)
    );
  }

  ngOnInit(): void {
    if (this.isSystemAdministrator) {
      void this.loadPage(1);
    }
  }

  async loadPage(page: number): Promise<void> {
    if (!this.isSystemAdministrator) {
      return;
    }
    this.listError = "";
    this.busy = true;
    this.currentPage = page;
    try {
      const res = await firstValueFrom(this.api.getAdminUsers(page, this.pageSize));
      this.applyPaged(res);
    } catch (e) {
      this.listError = getHttpErrorMessage(e, "No se pudo cargar la lista de usuarios.");
      this.users = [];
      this.totalCount = 0;
    } finally {
      this.busy = false;
      this.cdr.detectChanges();
    }
  }

  private applyPaged(res: PagedResult<AppUserListItemDto>): void {
    this.users = res.items;
    this.totalCount = res.totalCount;
    this.currentPage = res.page;
    this.pageSize = res.pageSize;
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  async createUser(): Promise<void> {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    const raw = this.createForm.getRawValue();
    this.listError = "";
    this.busy = true;
    try {
      await firstValueFrom(
        this.api.postAdminUser({
          email: (raw.email ?? "").trim(),
          displayName: (raw.displayName ?? "").trim()
        })
      );
      this.createForm.reset({ email: "", displayName: "" });
      await this.loadPage(1);
    } catch (e) {
      this.listError = getHttpErrorMessage(e, "No se pudo crear el usuario.");
    } finally {
      this.busy = false;
      this.cdr.detectChanges();
    }
  }
}
