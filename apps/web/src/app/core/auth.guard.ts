import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { AuthService } from "./auth.service";

/** A1.1: protege rutas funcionales y conserva `returnUrl` para post-login. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  if (auth.hasToken()) {
    return true;
  }

  const router = inject(Router);
  const returnUrl = state.url?.trim() || "/solicitudes/bandeja";
  return router.createUrlTree(["/login"], { queryParams: { returnUrl } });
};
