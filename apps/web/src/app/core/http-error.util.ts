import { HttpErrorResponse } from "@angular/common/http";
import { formatApiProblemMessage, parseApiProblem } from "./api-problem";

export function getHttpErrorMessage(error: unknown, fallback: string): string {
  if (
    error !== null &&
    typeof error === "object" &&
    "name" in error &&
    (error as { name: string }).name === "TimeoutError"
  ) {
    return "La API no respondió a tiempo. Comprueba que esté en marcha (http://localhost:5000) y que uses ng serve con proxy (npm run start), no solo abrir el dist.";
  }

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return "Sin conexión con la API (red o CORS). ¿Backend ejecutándose? ¿Usas la URL del dev server (puerto 4200)?";
    }

    const problem = parseApiProblem(error.error);
    if (problem) {
      if (error.status === 409) {
        return formatApiProblemMessage({
          ...problem,
          title:
            problem.title ??
            "Conflicto: el estado de la solicitud cambió o la operación no aplica."
        });
      }
      if (problem.title || problem.detail) {
        return formatApiProblemMessage(problem);
      }
      if (problem.correlationId) {
        return `Error HTTP ${error.status}. Ref: ${problem.correlationId}.`;
      }
    }

    if (error.status === 401) {
      return "No hay sesión (401). Inicie sesión de nuevo o use «Token (dev)» en el menú.";
    }

    if (error.status === 403) {
      return (
        "Acceso denegado (403). Tu rol o el ámbito del solicitante no permiten esta operación." +
        correlationRefFromUnknown(error.error) +
        " Actualice la página o vuelva a la bandeja si el estado de la solicitud cambió."
      );
    }

    return (
      `HTTP ${error.status} ${error.statusText || ""}`.trim() + (error.message ? ` — ${error.message}` : "")
    );
  }

  return fallback;
}

function correlationRefFromUnknown(body: unknown): string {
  const p = parseApiProblem(body);
  return p?.correlationId ? ` Ref: ${p.correlationId}.` : "";
}
