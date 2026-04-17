import { HttpErrorResponse } from "@angular/common/http";
import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { distinctUntilChanged, filter, firstValueFrom, map, timeout } from "rxjs";
import {
  ApiClientService,
  RequestAttachment,
  RequestAuditEntry,
  RequestComment,
  RequestDraftResponse
} from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-request-detail",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./request-detail.component.html",
  styleUrl: "./request-detail.component.css"
})
export class RequestDetailComponent implements OnInit {
  private readonly api = inject(ApiClientService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  request: RequestDraftResponse | null = null;
  loadError = "";
  submitBusy = false;
  submitError = "";
  submitSuccess = "";

  auditEntries: RequestAuditEntry[] = [];
  auditError = "";
  auditLoading = false;

  attachments: RequestAttachment[] = [];
  attachmentsError = "";
  attachmentsLoading = false;
  uploadBusy = false;
  uploadError = "";

  comments: RequestComment[] = [];
  commentsLoading = false;
  commentsError = "";
  newCommentBody = "";
  newCommentInternal = false;
  postCommentBusy = false;
  postCommentError = "";

  /** Resolución D1 en UI: código y nombre (docs/06 B5). */
  requestingUnitLabel = "";

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        map((p) => p.get("id")?.trim()),
        filter((id): id is string => !!id),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((id) => {
        void this.loadPageForRequestId(id);
      });
  }

  /**
   * Carga el detalle y refresca la vista antes de la auditoría.
   * La auditoría va en segundo plano para no dejar la UI en «Cargando…» si esa petición falla o tarda.
   */
  private async loadPageForRequestId(id: string): Promise<void> {
    await this.loadRequest(id);
    this.cdr.detectChanges();
    if (this.request) {
      void this.loadAttachments(id);
      void this.loadComments(id);
      void this.loadAudit(id);
    }
  }

  /** Reintento manual (B6/B7): tras error de carga o 403/409 en acciones. */
  async retryLoadDetail(): Promise<void> {
    const id = this.route.snapshot.paramMap.get("id")?.trim();
    if (id) {
      await this.loadPageForRequestId(id);
    }
  }

  async loadRequest(id: string): Promise<void> {
    this.loadError = "";
    this.request = null;
    this.requestingUnitLabel = "";
    this.auditEntries = [];
    this.auditError = "";
    this.attachments = [];
    this.attachmentsError = "";
    this.comments = [];
    this.commentsError = "";
    this.newCommentBody = "";
    this.newCommentInternal = false;
    try {
      this.request = await firstValueFrom(this.api.getRequestById(id).pipe(timeout(30_000)));
      if (this.request) {
        void this.loadRequestingUnitLabel(this.request.requestingUnitId);
      }
    } catch (err: unknown) {
      this.loadError = getHttpErrorMessage(
        err,
        "No se pudo cargar la solicitud. Revisa el ID, el token en Auth (dev) y que la API esté disponible."
      );
    } finally {
      this.cdr.detectChanges();
    }
  }

  private async loadRequestingUnitLabel(unitId: number): Promise<void> {
    try {
      const res = await firstValueFrom(
        this.api.getOrganizationalUnitsCatalog(false).pipe(timeout(15_000))
      );
      const u = res.items?.find((x) => x.unitId === unitId);
      this.requestingUnitLabel = u
        ? `${u.code} — ${u.name}${!u.isActive ? " (inactiva)" : ""}`
        : `ID ${unitId}`;
    } catch {
      this.requestingUnitLabel = `Unidad ${unitId}`;
    }
    this.cdr.detectChanges();
  }

  async loadAttachments(requestId: string): Promise<void> {
    this.attachmentsError = "";
    this.attachmentsLoading = true;
    this.attachments = [];
    try {
      const res = await firstValueFrom(this.api.getRequestAttachments(requestId).pipe(timeout(30_000)));
      this.attachments = res.items ?? [];
    } catch (err: unknown) {
      this.attachmentsError = getHttpErrorMessage(err, "No se pudo cargar los adjuntos.");
    } finally {
      this.attachmentsLoading = false;
      this.cdr.detectChanges();
    }
  }

  async loadComments(requestId: string): Promise<void> {
    this.commentsError = "";
    this.commentsLoading = true;
    this.comments = [];
    try {
      const res = await firstValueFrom(this.api.getRequestComments(requestId).pipe(timeout(30_000)));
      this.comments = res.items ?? [];
    } catch (err: unknown) {
      this.commentsError = getHttpErrorMessage(err, "No se pudieron cargar los comentarios.");
    } finally {
      this.commentsLoading = false;
      this.cdr.detectChanges();
    }
  }

  async loadAudit(requestId: string): Promise<void> {
    this.auditError = "";
    this.auditLoading = true;
    this.auditEntries = [];
    try {
      const res = await firstValueFrom(this.api.getRequestAudit(requestId).pipe(timeout(30_000)));
      this.auditEntries = res.items ?? [];
    } catch (err: unknown) {
      this.auditError = getHttpErrorMessage(err, "No se pudo cargar la línea de tiempo de auditoría.");
    } finally {
      this.auditLoading = false;
      this.cdr.detectChanges();
    }
  }

  get canSubmitDraft(): boolean {
    const s = this.request?.status?.toLowerCase();
    return s === "draft";
  }

  get canEditDraft(): boolean {
    if (!this.request || this.request.status?.toLowerCase() !== "draft") {
      return false;
    }
    const role = this.auth.getPrimaryRoleFromToken() ?? "";
    if (role === "SystemAdministrator") {
      return true;
    }
    if (role !== "Requester" && role !== "AreaCoordinator") {
      return false;
    }
    const uid = this.auth.getUserIdFromToken();
    return !!uid && uid.toLowerCase() === String(this.request.requesterUserId).toLowerCase();
  }

  /** Matriz RBAC: el rol Auditor no crea comentarios en MVP. */
  get canUseCommentComposer(): boolean {
    const r = this.auth.getPrimaryRoleFromToken() ?? "";
    return r !== "Auditor";
  }

  /** Notas internas: Analista TIC, Aprobador, Implementador, Admin (API valida). */
  get canOfferInternalComment(): boolean {
    const r = this.auth.getPrimaryRoleFromToken() ?? "";
    return ["TicAnalyst", "InstitutionalApprover", "Implementer", "SystemAdministrator"].includes(r);
  }

  async submitDraft(): Promise<void> {
    if (!this.request || !this.canSubmitDraft) {
      return;
    }
    const id = this.request.requestId;
    this.submitBusy = true;
    this.submitError = "";
    this.submitSuccess = "";
    try {
      await firstValueFrom(
        this.api.transitionRequest(id, { transition: "Submit", reason: null }).pipe(timeout(30_000))
      );
      this.submitSuccess = "Solicitud enviada correctamente (estado Submitted).";
      await this.loadRequest(id);
      this.cdr.detectChanges();
      if (this.request) {
        void this.loadAttachments(id);
        void this.loadComments(id);
        void this.loadAudit(id);
      }
    } catch (err: unknown) {
      this.submitError = getHttpErrorMessage(
        err,
        "No se pudo enviar. Comprueba que sigas en borrador, que tu rol sea Requester (o AreaCoordinator) y que el UserId del token coincida con el solicitante."
      );
      if (err instanceof HttpErrorResponse && err.status === 409) {
        await this.loadRequest(id);
        this.cdr.detectChanges();
        if (this.request) {
          void this.loadAttachments(id);
          void this.loadComments(id);
          void this.loadAudit(id);
        }
      }
    } finally {
      this.submitBusy = false;
      this.cdr.detectChanges();
    }
  }

  onCommentBodyInput(ev: Event): void {
    this.newCommentBody = (ev.target as HTMLTextAreaElement).value;
  }

  onCommentInternalChange(ev: Event): void {
    this.newCommentInternal = (ev.target as HTMLInputElement).checked;
  }

  async submitComment(): Promise<void> {
    if (!this.request) {
      return;
    }
    const id = this.request.requestId;
    const body = this.newCommentBody.trim();
    if (!body) {
      return;
    }
    const isInternal = this.canOfferInternalComment && this.newCommentInternal;
    this.postCommentBusy = true;
    this.postCommentError = "";
    try {
      await firstValueFrom(
        this.api.postRequestComment(id, { body, isInternal }).pipe(timeout(30_000))
      );
      this.newCommentBody = "";
      this.newCommentInternal = false;
      await this.loadComments(id);
    } catch (err: unknown) {
      this.postCommentError = getHttpErrorMessage(err, "No se pudo publicar el comentario.");
    } finally {
      this.postCommentBusy = false;
      this.cdr.detectChanges();
    }
  }

  async onAttachmentFileSelected(ev: Event): Promise<void> {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = "";
    if (!file || !this.request) {
      return;
    }
    const id = this.request.requestId;
    this.uploadBusy = true;
    this.uploadError = "";
    try {
      await firstValueFrom(this.api.uploadRequestAttachment(id, file).pipe(timeout(120_000)));
      await this.loadAttachments(id);
    } catch (err: unknown) {
      this.uploadError = getHttpErrorMessage(err, "No se pudo subir el archivo.");
    } finally {
      this.uploadBusy = false;
      this.cdr.detectChanges();
    }
  }

  async downloadAttachment(attachmentId: string, fileName: string): Promise<void> {
    if (!this.request) {
      return;
    }
    const id = this.request.requestId;
    this.attachmentsError = "";
    try {
      const res = await firstValueFrom(
        this.api.downloadRequestAttachmentBlob(id, attachmentId).pipe(timeout(120_000))
      );
      const blob = res.body;
      if (!blob) {
        throw new Error("Respuesta vacía");
      }
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err: unknown) {
      this.attachmentsError = getHttpErrorMessage(err, "No se pudo descargar el adjunto.");
      this.cdr.detectChanges();
    }
  }

  formatBytes(n: number): string {
    if (n < 1024) {
      return `${n} B`;
    }
    if (n < 1024 * 1024) {
      return `${(n / 1024).toFixed(1)} KB`;
    }
    return `${(n / (1024 * 1024)).toFixed(1)} MB`;
  }

  /** Vista imprimible / PDF del navegador (docs/06 E2). */
  printView(): void {
    window.print();
  }

  formatTransition(entry: RequestAuditEntry): string {
    const from = entry.fromStatus ?? "—";
    const to = entry.toStatus ?? "—";
    if (entry.action === "RequestCreated") {
      return `Creación (estado ${to})`;
    }
    if (entry.fromStatus && entry.toStatus) {
      return `${entry.action}: ${from} → ${to}`;
    }
    return entry.action;
  }
}
