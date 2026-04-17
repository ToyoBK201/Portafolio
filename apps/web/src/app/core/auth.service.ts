import { HttpClient } from "@angular/common/http";
import { Injectable, inject, signal } from "@angular/core";
import { firstValueFrom } from "rxjs";
import { DEFAULT_REQUESTER_USER_ID } from "./app.constants";
import { decodeJwtPayload } from "./jwt-payload.util";

const TOKEN_KEY = "stg_access_token";

export type DevTokenRole =
  | "Requester"
  | "AreaCoordinator"
  | "TicAnalyst"
  | "InstitutionalApprover"
  | "Implementer"
  | "SystemAdministrator"
  | "Auditor";

/** Valores admitidos por `POST /api/v1/auth/dev-token` (docs/06 A2). */
export const DEV_TOKEN_ROLES: readonly DevTokenRole[] = [
  "Requester",
  "AreaCoordinator",
  "TicAnalyst",
  "InstitutionalApprover",
  "Implementer",
  "SystemAdministrator",
  "Auditor"
];

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly http = inject(HttpClient);
  readonly hasToken = signal(false);
  /** Se incrementa al cambiar el JWT para que la UI reaccione (p. ej. rol activo A2). */
  readonly tokenVersion = signal(0);

  constructor() {
    this.hasToken.set(!!sessionStorage.getItem(TOKEN_KEY));
  }

  getToken(): string | null {
    return sessionStorage.getItem(TOKEN_KEY);
  }

  /** Último rol del JWT (Auth dev u OIDC); solo para UI. */
  getPrimaryRoleFromToken(): string | null {
    const payload = this.getPayload();
    if (!payload) {
      return null;
    }
    const short = payload["role"];
    const long = payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];
    const raw = short ?? long;
    if (typeof raw === "string") {
      return raw;
    }
    if (Array.isArray(raw) && raw.length > 0 && typeof raw[0] === "string") {
      return raw[0];
    }
    return null;
  }

  /** <code>sub</code> del JWT (mismo UserId que en formularios). */
  getUserIdFromToken(): string | null {
    const payload = this.getPayload();
    if (!payload) {
      return null;
    }
    const sub = payload["sub"];
    return typeof sub === "string" ? sub : null;
  }

  setToken(token: string): void {
    sessionStorage.setItem(TOKEN_KEY, token);
    this.hasToken.set(true);
    this.tokenVersion.update((v) => v + 1);
  }

  clearToken(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    this.hasToken.set(false);
    this.tokenVersion.update((v) => v + 1);
  }

  /** Claim `auth_method` del JWT: sesión por contraseña vs token de desarrollo (docs/06 A2). */
  getAuthMethodFromToken(): "password" | "dev" | null {
    const payload = this.getPayload();
    if (!payload) {
      return null;
    }
    const v = payload["auth_method"];
    if (v === "password" || v === "dev") {
      return v;
    }
    return null;
  }

  private getPayload(): Record<string, unknown> | null {
    const t = this.getToken();
    if (!t) {
      return null;
    }
    return decodeJwtPayload(t);
  }

  /** Login local contra la API (docs/06 A1). Misma forma de token que dev-token. */
  async loginWithPassword(email: string, password: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<{ accessToken: string }>("/api/v1/auth/login", { email, password })
    );
    this.setToken(res.accessToken);
  }

  /** Emite un JWT con otro rol asignado en BD (solo sesión `auth_method=password`). */
  async switchActiveRoleFromAccount(roleCode: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<{ accessToken: string }>("/api/v1/auth/switch-role", { roleCode })
    );
    this.setToken(res.accessToken);
  }

  async requestDevToken(userId: string, role: DevTokenRole): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<{ accessToken: string }>("/api/v1/auth/dev-token", { userId, role })
    );
    this.setToken(res.accessToken);
  }

  /**
   * Mantiene el mismo `sub` y emite un nuevo JWT con el rol indicado (modo desarrollo).
   * Cumple docs/06 A2: el token enviado al API refleja el rol activo.
   */
  async switchDevRole(role: DevTokenRole): Promise<void> {
    const userId = this.getUserIdFromToken() ?? DEFAULT_REQUESTER_USER_ID;
    await this.requestDevToken(userId, role);
  }
}
