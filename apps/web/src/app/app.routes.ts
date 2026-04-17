import { Routes } from "@angular/router";
import { authGuard } from "./core/auth.guard";

export const routes: Routes = [
  {
    path: "",
    loadComponent: () => import("./pages/dashboard/dashboard.component").then((m) => m.DashboardComponent),
    title: "Inicio"
  },
  {
    path: "login",
    loadComponent: () => import("./pages/login/login.component").then((m) => m.LoginComponent),
    title: "Iniciar sesión"
  },
  {
    path: "auth",
    loadComponent: () => import("./pages/auth-dev/auth-dev.component").then((m) => m.AuthDevComponent),
    title: "Autenticación (desarrollo)"
  },
  {
    path: "solicitudes/nueva",
    canActivate: [authGuard],
    loadComponent: () => import("./pages/draft-form/draft-form.component").then((m) => m.DraftFormComponent),
    title: "Nueva solicitud (borrador)"
  },
  {
    path: "solicitudes/:id/editar",
    canActivate: [authGuard],
    loadComponent: () => import("./pages/draft-form/draft-form.component").then((m) => m.DraftFormComponent),
    title: "Editar borrador"
  },
  {
    path: "solicitudes/bandeja",
    canActivate: [authGuard],
    loadComponent: () => import("./pages/bandeja/bandeja.component").then((m) => m.BandejaComponent),
    title: "Bandeja"
  },
  {
    path: "solicitudes/transiciones",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/transitions/transitions.component").then((m) => m.TransitionsComponent),
    title: "Transiciones"
  },
  {
    path: "admin/unidades-organizativas",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/admin-organizational-units/admin-organizational-units.component").then(
        (m) => m.AdminOrganizationalUnitsComponent
      ),
    title: "Unidades organizativas"
  },
  /** Ruta más específica antes que `admin/usuarios` para que el parámetro resuelva bien. */
  {
    path: "admin/usuarios/:userId",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/admin-user-detail/admin-user-detail.component").then((m) => m.AdminUserDetailComponent),
    title: "Roles de usuario"
  },
  {
    path: "admin/usuarios",
    canActivate: [authGuard],
    loadComponent: () => import("./pages/admin-users/admin-users.component").then((m) => m.AdminUsersComponent),
    title: "Usuarios (admin)"
  },
  {
    path: "admin/auditoria",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/admin-audit-log/admin-audit-log.component").then((m) => m.AdminAuditLogComponent),
    title: "Auditoría"
  },
  {
    path: "solicitudes/:id",
    canActivate: [authGuard],
    loadComponent: () =>
      import("./pages/request-detail/request-detail.component").then((m) => m.RequestDetailComponent),
    title: "Detalle de solicitud"
  },
  { path: "**", redirectTo: "" }
];
