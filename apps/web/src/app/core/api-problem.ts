/** Contrato alineado a ApiProblemBody en la API (RFC 7807 Problem Details + correlationId). */
export interface ApiProblem {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly correlationId?: string;
}

export function parseApiProblem(body: unknown): ApiProblem | null {
  if (typeof body !== "object" || body === null) {
    return null;
  }
  const o = body as Record<string, unknown>;
  const title = o["title"];
  const detail = o["detail"];
  const correlationId = o["correlationId"];
  const type = o["type"];
  const status = o["status"];
  if (
    typeof title !== "string" &&
    typeof detail !== "string" &&
    typeof correlationId !== "string"
  ) {
    return null;
  }
  return {
    type: typeof type === "string" ? type : undefined,
    title: typeof title === "string" ? title : undefined,
    status: typeof status === "number" ? status : undefined,
    detail: typeof detail === "string" ? detail : undefined,
    correlationId: typeof correlationId === "string" ? correlationId : undefined
  };
}

/**
 * Texto único para UI: si el servidor repite title en detail (caso ApiProblemBody con detail vacío), no duplica.
 */
export function formatApiProblemMessage(p: ApiProblem): string {
  const t = p.title?.trim() ?? "";
  const d = p.detail?.trim() ?? "";
  const core = t && d && t === d ? t : [t, d].filter(Boolean).join(" — ");
  const ref = p.correlationId ? ` Ref: ${p.correlationId}.` : "";
  return `${core}${ref}`.trim();
}
