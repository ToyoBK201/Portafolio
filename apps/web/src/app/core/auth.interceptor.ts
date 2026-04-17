import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { Router } from "@angular/router";
import { catchError, throwError } from "rxjs";
import { AuthService } from "./auth.service";

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.getToken();
  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        const url = req.url;
        const isAuthCall =
          url.includes("/api/v1/auth/login") || url.includes("/api/v1/auth/dev-token");
        if (!isAuthCall) {
          auth.clearToken();
          const currentPath = router.url || "/";
          void router.navigate(["/login"], {
            queryParams: { reason: "session", returnUrl: currentPath }
          });
        }
      }
      return throwError(() => err);
    })
  );
};
