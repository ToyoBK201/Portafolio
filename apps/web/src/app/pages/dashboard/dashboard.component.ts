import { Component, OnInit, inject, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import { firstValueFrom, timeout } from "rxjs";
import {
  ApiClientService,
  HealthResponse,
  NotificationSummary,
  UserNotificationItem,
  WorkQueueSummaryResponse
} from "../../core/api-client.service";
import { AuthService } from "../../core/auth.service";
import { getHttpErrorMessage } from "../../core/http-error.util";

@Component({
  selector: "app-dashboard",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./dashboard.component.html"
})
export class DashboardComponent implements OnInit {
  private readonly apiClient = inject(ApiClientService);
  readonly auth = inject(AuthService);

  readonly checking = signal(true);
  readonly health = signal<HealthResponse | null>(null);
  readonly error = signal("");

  readonly notifLoading = signal(false);
  readonly notifError = signal("");
  readonly notifSummary = signal<NotificationSummary | null>(null);
  readonly notifItems = signal<UserNotificationItem[]>([]);
  readonly markAllBusy = signal(false);

  readonly workQueueLoading = signal(false);
  readonly workQueueError = signal("");
  readonly workQueueSummary = signal<WorkQueueSummaryResponse | null>(null);

  async ngOnInit(): Promise<void> {
    this.checking.set(true);
    this.error.set("");
    this.health.set(null);
    try {
      const h = await firstValueFrom(this.apiClient.getHealth().pipe(timeout(12_000)));
      this.health.set(h);
    } catch (err: unknown) {
      this.error.set(
        getHttpErrorMessage(
          err,
          "No se pudo leer el estado de la API. Comprueba que dotnet run esté activo en el puerto 5000."
        )
      );
    } finally {
      this.checking.set(false);
    }

    if (this.auth.hasToken()) {
      await Promise.all([this.loadNotifications(), this.loadWorkQueueSummary()]);
    }
  }

  async loadWorkQueueSummary(): Promise<void> {
    this.workQueueLoading.set(true);
    this.workQueueError.set("");
    try {
      const s = await firstValueFrom(this.apiClient.getWorkQueueSummary().pipe(timeout(15_000)));
      this.workQueueSummary.set(s);
    } catch (err: unknown) {
      this.workQueueSummary.set(null);
      this.workQueueError.set(getHttpErrorMessage(err, "No se pudo cargar el resumen de cola."));
    } finally {
      this.workQueueLoading.set(false);
    }
  }

  workQueueEntries(): { status: string; count: number }[] {
    const c = this.workQueueSummary()?.countsByStatus;
    if (!c) {
      return [];
    }
    return Object.entries(c)
      .map(([status, count]) => ({ status, count }))
      .sort((a, b) => a.status.localeCompare(b.status));
  }

  async loadNotifications(): Promise<void> {
    this.notifLoading.set(true);
    this.notifError.set("");
    try {
      const [summary, list] = await Promise.all([
        firstValueFrom(this.apiClient.getNotificationSummary().pipe(timeout(15_000))),
        firstValueFrom(this.apiClient.getMyNotifications(20, false).pipe(timeout(15_000)))
      ]);
      this.notifSummary.set(summary);
      this.notifItems.set(list.items ?? []);
    } catch (err: unknown) {
      this.notifSummary.set(null);
      this.notifItems.set([]);
      this.notifError.set(getHttpErrorMessage(err, "No se pudieron cargar las notificaciones."));
    } finally {
      this.notifLoading.set(false);
    }
  }

  async markAllRead(): Promise<void> {
    this.markAllBusy.set(true);
    try {
      await firstValueFrom(this.apiClient.markAllNotificationsRead().pipe(timeout(15_000)));
      await this.loadNotifications();
    } catch (err: unknown) {
      this.notifError.set(getHttpErrorMessage(err, "No se pudo marcar todo como leído."));
    } finally {
      this.markAllBusy.set(false);
    }
  }

  async onNotificationClick(n: UserNotificationItem): Promise<void> {
    if (!n.isRead) {
      try {
        await firstValueFrom(this.apiClient.markNotificationRead(n.notificationId).pipe(timeout(12_000)));
        this.notifItems.update((items) =>
          items.map((x) => (x.notificationId === n.notificationId ? { ...x, isRead: true } : x))
        );
        this.notifSummary.update((s) =>
          s ? { unreadCount: Math.max(0, s.unreadCount - 1) } : s
        );
      } catch {
        void this.loadNotifications();
      }
    }
  }
}
