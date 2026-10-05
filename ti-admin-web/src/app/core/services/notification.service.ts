import { HttpContext } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import { BACKGROUND_REQUEST, SILENT_ERRORS } from '@core/interceptors/http-context';
import { NotificationDto, PageRequest, Paged } from '@core/models';

/** Notificaciones internas del usuario y contador de no leidas del encabezado. */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly api = inject(ApiService);
  readonly unread = signal(0);

  list(page: PageRequest, unreadOnly = false): Observable<Paged<NotificationDto>> {
    return this.api.getPaged<NotificationDto>('notifications', page, { unreadOnly: unreadOnly || null });
  }

  /** Sondeo silencioso: sin barra de progreso ni toasts si falla. */
  refreshUnread(): void {
    const context = new HttpContext().set(BACKGROUND_REQUEST, true).set(SILENT_ERRORS, true);
    this.api.get<number>('notifications/unread-count', undefined, context).subscribe({
      next: (count) => this.unread.set(count ?? 0),
      error: () => undefined,
    });
  }

  markRead(id: number): Observable<unknown> {
    return this.api.post(`notifications/${id}/read`).pipe(tap(() => this.refreshUnread()));
  }

  markAllRead(): Observable<number> {
    return this.api.post<number>('notifications/read-all').pipe(tap(() => this.unread.set(0)));
  }
}
