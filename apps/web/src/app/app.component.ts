import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { firstValueFrom } from "rxjs";
import { timeout } from "rxjs/operators";
import { ApiClientService, AppRoleOptionDto } from "./core/api-client.service";
import { AuthService, DEV_TOKEN_ROLES, DevTokenRole } from "./core/auth.service";
import { getHttpErrorMessage } from "./core/http-error.util";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./app.component.html",
  styleUrl: "./app.component.css"
})
export class AppComponent {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly api = inject(ApiClientService);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly devTokenRoles = DEV_TOKEN_ROLES;

  /** Roles en BD cuando la sesión es por contraseña (docs/06 A2). */
  accountRoles: AppRoleOptionDto[] = [];
  accountRolesLoading = false;
  accountRolesError = "";
  accountRoleSwitchBusy = false;
  accountRoleSwitchError = "";

  roleSwitchBusy = false;
  roleSwitchError = "";

  constructor() {
    effect(() => {
      this.auth.tokenVersion();
      if (!this.auth.hasToken()) {
        this.accountRoles = [];
        this.accountRolesError = "";
        return;
      }
      if (this.auth.getAuthMethodFromToken() === "password") {
        void this.loadAccountRoles();
      } else {
        this.accountRoles = [];
      }
    });
  }

  private async loadAccountRoles(): Promise<void> {
    this.accountRolesLoading = true;
    this.accountRolesError = "";
    try {
      const res = await firstValueFrom(this.api.getMyRoles().pipe(timeout(15_000)));
      this.accountRoles = res.items ?? [];
    } catch (e: unknown) {
      this.accountRolesError = getHttpErrorMessage(e, "No se pudieron cargar los roles de la cuenta.");
      this.accountRoles = [];
    } finally {
      this.accountRolesLoading = false;
      this.cdr.detectChanges();
    }
  }

  isSystemAdministrator(): boolean {
    return this.auth.getPrimaryRoleFromToken() === "SystemAdministrator";
  }

  /** Listado global de auditoría (API GET /admin/audit-log). */
  showAuditNav(): boolean {
    const r = this.auth.getPrimaryRoleFromToken() ?? "";
    return r === "SystemAdministrator" || r === "Auditor";
  }

  /** Matriz RBAC §1: alta borrador solo Requester / Coordinador / Admin (pruebas). */
  showNavCreateRequest(): boolean {
    if (!this.auth.hasToken()) {
      return false;
    }
    const r = this.auth.getPrimaryRoleFromToken() ?? "";
    return r === "Requester" || r === "AreaCoordinator" || r === "SystemAdministrator";
  }

  /** Página de transición manual: no aplica a Auditor (solo lectura). */
  showNavTransitions(): boolean {
    if (!this.auth.hasToken()) {
      return false;
    }
    return (this.auth.getPrimaryRoleFromToken() ?? "") !== "Auditor";
  }

  /** Catálogo de unidades: solo administrador (docs/02). */
  showNavAdminOrgUnits(): boolean {
    return this.auth.hasToken() && this.auth.getPrimaryRoleFromToken() === "SystemAdministrator";
  }

  /** Selector de rol según BD (sesión por contraseña, más de un rol). */
  showAccountRoleStrip(): boolean {
    return (
      this.auth.hasToken() &&
      this.auth.getAuthMethodFromToken() === "password" &&
      this.accountRoles.length > 1 &&
      !this.accountRolesLoading
    );
  }

  /** Selector amplio para pruebas con token dev (claim auth_method=dev). */
  showDevRoleStrip(): boolean {
    return this.auth.hasToken() && this.auth.getAuthMethodFromToken() !== "password";
  }

  currentAccountRoleCode(): string {
    return this.auth.getPrimaryRoleFromToken() ?? "Requester";
  }

  async onAccountActiveRoleChange(ev: Event): Promise<void> {
    const el = ev.target as HTMLSelectElement;
    const role = el.value;
    if (role === this.currentAccountRoleCode()) {
      return;
    }
    this.accountRoleSwitchError = "";
    this.accountRoleSwitchBusy = true;
    try {
      await this.auth.switchActiveRoleFromAccount(role);
    } catch (e: unknown) {
      this.accountRoleSwitchError = getHttpErrorMessage(e, "No se pudo cambiar el rol activo.");
      el.value = this.currentAccountRoleCode();
    } finally {
      this.accountRoleSwitchBusy = false;
      this.cdr.detectChanges();
    }
  }

  /** Rol del JWT actual para el selector (docs/06 A2). */
  currentDevRole(): string {
    return this.auth.getPrimaryRoleFromToken() ?? "Requester";
  }

  async onDevActiveRoleChange(ev: Event): Promise<void> {
    const el = ev.target as HTMLSelectElement;
    const role = el.value as DevTokenRole;
    if (role === this.auth.getPrimaryRoleFromToken()) {
      return;
    }
    this.roleSwitchError = "";
    this.roleSwitchBusy = true;
    try {
      await this.auth.switchDevRole(role);
    } catch {
      this.roleSwitchError = "No se pudo cambiar el rol. ¿API en marcha y /auth/dev-token disponible?";
      el.value = this.currentDevRole();
    } finally {
      this.roleSwitchBusy = false;
      this.cdr.detectChanges();
    }
  }

  async logout(): Promise<void> {
    this.auth.clearToken();
    await this.router.navigate(["/login"]);
  }
}
