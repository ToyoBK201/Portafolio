import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";

export type HealthResponse = {
  service: string;
  status: string;
  timestampUtc: string;
};

export type CreateDraftRequest = {
  title: string;
  description: string;
  businessJustification: string;
  requestType: number;
  priority: number;
  requestingUnitId: number;
  requesterUserId: string;
  desiredDate: string | null;
  specificPayloadJson: string | null;
};

/** Cuerpo de PATCH borrador (mismo que crear salvo solicitante). */
export type UpdateDraftRequest = Omit<CreateDraftRequest, "requesterUserId">;

export type RequestDraftResponse = {
  requestId: string;
  title: string;
  description: string;
  businessJustification: string;
  requestType: number;
  priority: number;
  requestingUnitId: number;
  requesterUserId: string;
  status: string;
  desiredDate: string | null;
  createdAtUtc: string;
  /** JSON específico por tipo (validado en servidor al enviar). */
  specificPayloadJson: string | null;
};

export type RequestListFilters = {
  status?: string;
  requesterUserId?: string;
  page?: number;
  pageSize?: number;
  sortBy?: "createdAtUtc" | "title" | "status";
  sortDirection?: "asc" | "desc";
};

export type PagedResult<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type RequestAuditEntry = {
  auditId: number;
  occurredAtUtc: string;
  correlationId: string | null;
  actorUserId: string;
  actorRole: string;
  action: string;
  fromStatus: string | null;
  toStatus: string | null;
  payloadSummary: string | null;
};

export type RequestAuditResponse = {
  items: RequestAuditEntry[];
};

export type TransitionRequest = {
  transition: string;
  reason?: string | null;
};

export type RequestAttachment = {
  attachmentId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
};

export type RequestAttachmentsResponse = {
  items: RequestAttachment[];
};

export type RequestComment = {
  commentId: string;
  authorUserId: string;
  authorDisplayName: string;
  body: string;
  isInternal: boolean;
  createdAtUtc: string;
};

export type RequestCommentsResponse = {
  items: RequestComment[];
};

export type PostCommentPayload = {
  body: string;
  isInternal: boolean;
};

export type NotificationSummary = {
  unreadCount: number;
};

export type UserNotificationItem = {
  notificationId: string;
  requestId: string | null;
  title: string;
  message: string;
  category: string;
  isRead: boolean;
  createdAtUtc: string;
};

export type UserNotificationsResponse = {
  items: UserNotificationItem[];
};

export type MarkAllReadResponse = {
  marked: number;
};

export type WorkQueueSummaryResponse = {
  countsByStatus: Record<string, number>;
};

/** Catálogo D1 — unidades organizativas. */
export type OrganizationalUnitDto = {
  unitId: number;
  code: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
};

export type OrganizationalUnitsCatalogResponse = {
  items: OrganizationalUnitDto[];
};

export type AppRoleOptionDto = {
  roleId: number;
  code: string;
  labelEs: string;
  sortOrder: number;
};

export type AppRolesCatalogResponse = {
  items: AppRoleOptionDto[];
};

export type AppUserListItemDto = {
  userId: string;
  email: string;
  displayName: string;
  isActive: boolean;
  createdAtUtc: string;
};

/** Fila de listado global de auditoría (GET /api/v1/admin/audit-log). */
export type AdminAuditLogEntryDto = {
  auditId: number;
  occurredAtUtc: string;
  correlationId: string | null;
  actorUserId: string;
  actorRole: string;
  action: string;
  entityType: string;
  entityId: string;
  requestId: string | null;
  fromStatus: string | null;
  toStatus: string | null;
  payloadSummary: string | null;
  success: boolean;
};

export type RoleAssignmentDto = {
  roleId: number;
  roleCode: string;
  roleLabelEs: string;
  organizationalUnitId: number | null;
};

export type AppUserDetailDto = {
  userId: string;
  email: string;
  displayName: string;
  isActive: boolean;
  createdAtUtc: string;
  roles: RoleAssignmentDto[];
};

export type CreateOrganizationalUnitPayload = {
  code: string;
  name: string;
};

export type PatchOrganizationalUnitPayload = {
  code?: string | null;
  name?: string | null;
  isActive?: boolean | null;
};

export type CreateAppUserPayload = {
  email: string;
  displayName: string;
};

export type RoleAssignmentPayload = {
  roleId: number;
  organizationalUnitId?: number | null;
};

export type PutUserRolesPayload = {
  assignments: RoleAssignmentPayload[];
};

@Injectable({ providedIn: "root" })
export class ApiClientService {
  private readonly http = inject(HttpClient);

  getHealth() {
    return this.http.get<HealthResponse>("/api/v1/health");
  }

  createDraft(payload: CreateDraftRequest) {
    return this.http.post<RequestDraftResponse>("/api/v1/requests", payload);
  }

  patchRequestDraft(requestId: string, payload: UpdateDraftRequest) {
    return this.http.patch<RequestDraftResponse>(`/api/v1/requests/${requestId}`, payload);
  }

  getRequestById(requestId: string) {
    return this.http.get<RequestDraftResponse>(`/api/v1/requests/${requestId}`);
  }

  getRequestAudit(requestId: string) {
    return this.http.get<RequestAuditResponse>(`/api/v1/requests/${requestId}/audit`);
  }

  getRequestAttachments(requestId: string) {
    return this.http.get<RequestAttachmentsResponse>(`/api/v1/requests/${requestId}/attachments`);
  }

  uploadRequestAttachment(requestId: string, file: File) {
    const body = new FormData();
    body.append("file", file, file.name);
    return this.http.post<RequestAttachment>(`/api/v1/requests/${requestId}/attachments`, body);
  }

  /** Descarga con cabecera Authorization (usa el mismo flujo que el resto de la API). */
  downloadRequestAttachmentBlob(requestId: string, attachmentId: string) {
    return this.http.get(`/api/v1/requests/${requestId}/attachments/${attachmentId}/file`, {
      responseType: "blob",
      observe: "response"
    });
  }

  getRequestComments(requestId: string) {
    return this.http.get<RequestCommentsResponse>(`/api/v1/requests/${requestId}/comments`);
  }

  postRequestComment(requestId: string, payload: PostCommentPayload) {
    return this.http.post<RequestComment>(`/api/v1/requests/${requestId}/comments`, payload);
  }

  listRequests(filters: RequestListFilters) {
    const params = new URLSearchParams();
    if (filters.status) {
      params.set("status", filters.status);
    }
    if (filters.requesterUserId) {
      params.set("requesterUserId", filters.requesterUserId);
    }
    params.set("page", String(filters.page ?? 1));
    params.set("pageSize", String(filters.pageSize ?? 10));
    params.set("sortBy", filters.sortBy ?? "createdAtUtc");
    params.set("sortDirection", filters.sortDirection ?? "desc");

    const query = params.toString();
    const url = query ? `/api/v1/requests?${query}` : "/api/v1/requests";
    return this.http.get<PagedResult<RequestDraftResponse>>(url);
  }

  /** Mismos criterios que listRequests, sin paginación (hasta maxRows filas). format: csv | xlsx */
  exportRequestsFile(
    format: "csv" | "xlsx",
    filters: {
      status?: string;
      requesterUserId?: string;
      sortBy?: RequestListFilters["sortBy"];
      sortDirection?: RequestListFilters["sortDirection"];
      createdFromUtc?: string;
      createdToUtc?: string;
      maxRows?: number;
    }
  ) {
    const params = new URLSearchParams();
    params.set("format", format);
    if (filters.status) {
      params.set("status", filters.status);
    }
    if (filters.requesterUserId) {
      params.set("requesterUserId", filters.requesterUserId);
    }
    if (filters.createdFromUtc) {
      params.set("createdFromUtc", filters.createdFromUtc);
    }
    if (filters.createdToUtc) {
      params.set("createdToUtc", filters.createdToUtc);
    }
    params.set("sortBy", filters.sortBy ?? "createdAtUtc");
    params.set("sortDirection", filters.sortDirection ?? "desc");
    params.set("maxRows", String(filters.maxRows ?? 5000));
    return this.http.get(`/api/v1/requests/export?${params.toString()}`, {
      responseType: "blob",
      observe: "response"
    });
  }

  transitionRequest(requestId: string, payload: TransitionRequest) {
    return this.http.post<void>(`/api/v1/requests/${requestId}/transitions`, payload);
  }

  getNotificationSummary() {
    return this.http.get<NotificationSummary>("/api/v1/me/notifications/summary");
  }

  getMyNotifications(take = 30, unreadOnly = false) {
    const params = new URLSearchParams();
    params.set("take", String(take));
    params.set("unreadOnly", String(unreadOnly));
    return this.http.get<UserNotificationsResponse>(`/api/v1/me/notifications?${params.toString()}`);
  }

  markNotificationRead(notificationId: string) {
    return this.http.post<void>(`/api/v1/me/notifications/${notificationId}/read`, {});
  }

  markAllNotificationsRead() {
    return this.http.post<MarkAllReadResponse>("/api/v1/me/notifications/read-all", {});
  }

  /** Conteos por estado según alcance del rol (Requester: solo propias; resto: cola global). */
  getWorkQueueSummary() {
    return this.http.get<WorkQueueSummaryResponse>("/api/v1/me/work-queue-summary");
  }

  /** Catálogo para formularios (cualquier rol autenticado). */
  getOrganizationalUnitsCatalog(activeOnly = false) {
    const q = activeOnly ? "?activeOnly=true" : "";
    return this.http.get<OrganizationalUnitsCatalogResponse>(`/api/v1/catalogs/organizational-units${q}`);
  }

  getAppRolesCatalog() {
    return this.http.get<AppRolesCatalogResponse>("/api/v1/catalogs/app-roles");
  }

  /** Roles asignados al usuario actual en BD (docs/06 A2). */
  getMyRoles() {
    return this.http.get<{ items: AppRoleOptionDto[] }>("/api/v1/me/roles");
  }

  postAdminOrganizationalUnit(payload: CreateOrganizationalUnitPayload) {
    return this.http.post<{ unitId: number }>("/api/v1/admin/organizational-units", payload);
  }

  patchAdminOrganizationalUnit(unitId: number, payload: PatchOrganizationalUnitPayload) {
    return this.http.patch<void>(`/api/v1/admin/organizational-units/${unitId}`, payload);
  }

  getAdminUsers(page = 1, pageSize = 20) {
    const params = new URLSearchParams();
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return this.http.get<PagedResult<AppUserListItemDto>>(`/api/v1/admin/users?${params.toString()}`);
  }

  /** Listado paginado de AuditLog (SystemAdministrator o Auditor). */
  getAdminAuditLog(page = 1, pageSize = 25, entityType?: string, action?: string) {
    const params = new URLSearchParams();
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    if (entityType) {
      params.set("entityType", entityType);
    }
    if (action) {
      params.set("action", action);
    }
    return this.http.get<PagedResult<AdminAuditLogEntryDto>>(`/api/v1/admin/audit-log?${params.toString()}`);
  }

  /** CSV UTF-8 con BOM; nombre de archivo con fecha (docs/06 E1 complemento). */
  exportAdminAuditLogCsv(entityType?: string, action?: string) {
    const params = new URLSearchParams();
    if (entityType) {
      params.set("entityType", entityType);
    }
    if (action) {
      params.set("action", action);
    }
    const q = params.toString();
    const url = q ? `/api/v1/admin/audit-log/export?${q}` : "/api/v1/admin/audit-log/export";
    return this.http.get(url, { responseType: "blob", observe: "response" });
  }

  postAdminUser(payload: CreateAppUserPayload) {
    return this.http.post<{ userId: string }>("/api/v1/admin/users", payload);
  }

  getAdminUser(userId: string) {
    return this.http.get<AppUserDetailDto>(`/api/v1/admin/users/${userId}`);
  }

  putAdminUserRoles(userId: string, payload: PutUserRolesPayload) {
    return this.http.put<void>(`/api/v1/admin/users/${userId}/roles`, payload);
  }
}
