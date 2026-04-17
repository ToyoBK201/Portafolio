import { HttpInterceptorFn } from "@angular/common/http";

/**
 * Un correlation id por petición HTTP para alinear trazas con el backend (header X-Correlation-Id).
 */
export const correlationIdInterceptor: HttpInterceptorFn = (req, next) => {
  const id = globalThis.crypto.randomUUID();
  const cloned = req.clone({
    setHeaders: { "X-Correlation-Id": id }
  });
  return next(cloned);
};
