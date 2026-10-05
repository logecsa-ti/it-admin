import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { NotificationDto } from '@core/models';
import { NotificationService } from '@core/services/notification.service';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { ExportService } from '@shared/services/export.service';
import { pagedList } from '@shared/utils/paged-list';

/** Rutas del frontend para las entidades que generan notificaciones. */
const ENTITY_ROUTES: Record<string, string> = {
  Ticket: '/tickets',
  Asset: '/activos',
  License: '/licencias',
  Contract: '/contratos',
  Maintenance: '/mantenimientos',
  ChangeRequest: '/cambios',
  PurchaseRequest: '/compras',
};

const ICONS: Record<string, string> = {
  Ticket: 'confirmation_number',
  License: 'key',
  Contract: 'contract',
  Maintenance: 'build',
  ChangeRequest: 'published_with_changes',
  PurchaseRequest: 'shopping_cart',
  ExportJob: 'download',
};

@Component({
  selector: 'app-notifications',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatButtonToggleModule, MatIconModule, MatPaginatorModule, MatProgressBarModule, EmptyStateComponent, PageHeaderComponent, AppDatePipe],
  template: `
    <div class="page narrow">
      <app-page-header title="Notificaciones" subtitle="Avisos de tickets, aprobaciones, vencimientos y exportaciones.">
        <button mat-stroked-button type="button" [disabled]="notifications.unread() === 0" (click)="markAll()"><mat-icon>done_all</mat-icon>Marcar todas como leidas</button>
      </app-page-header>
      <section class="panel">
        <div class="filters">
          <mat-button-toggle-group [value]="list.filters().unreadOnly ? 'unread' : 'all'" (change)="list.patchFilters({ unreadOnly: $event.value === 'unread' })" aria-label="Filtro">
            <mat-button-toggle value="all">Todas</mat-button-toggle>
            <mat-button-toggle value="unread">No leidas ({{ notifications.unread() }})</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        <div class="progress">@if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }</div>
        <ul class="list">
          @for (n of list.items(); track n.id) {
            <li [class.unread]="!n.isRead">
              <button type="button" class="item" (click)="open(n)">
                <mat-icon aria-hidden="true">{{ icon(n) }}</mat-icon>
                <div class="text">
                  <div class="title">{{ n.title }}</div>
                  <div class="message">{{ n.message }}</div>
                  <div class="subtle">{{ n.createdAt | appDate: 'relative' }}</div>
                </div>
                @if (!n.isRead) { <span class="dot" aria-label="No leida"></span> }
              </button>
            </li>
          }
        </ul>
        @if (list.isEmpty()) {
          <app-empty-state icon="notifications_off" title="Sin notificaciones" message="Aqui vera los avisos que el sistema le envie." />
        }
        <mat-paginator [length]="list.total()" [pageIndex]="list.page() - 1" [pageSize]="list.pageSize()" [pageSizeOptions]="[25, 50]" (page)="list.onPage($event)" />
      </section>
    </div>
  `,
  styles: `
    .narrow { max-width: 900px; }
    .progress { height: 4px; }
    .list { list-style: none; margin: 0; padding: 0; }
    li + li { border-top: 1px solid var(--app-border-soft); }
    .item { all: unset; box-sizing: border-box; display: flex; align-items: flex-start; gap: 14px; width: 100%; padding: 14px 20px; cursor: pointer; }
    .item:hover { background: var(--app-olive-50); }
    .item:focus-visible { outline: 2px solid var(--app-olive-500); outline-offset: -2px; }
    .item mat-icon { color: var(--app-text-subtle); margin-top: 2px; }
    li.unread .item mat-icon { color: var(--app-olive-600); }
    li.unread .title { font-weight: 700; }
    .text { flex: 1; min-width: 0; }
    .title { font-weight: 500; }
    .message { color: var(--app-text-muted); }
    .dot { width: 8px; height: 8px; margin-top: 8px; border-radius: 50%; background: var(--app-olive-600); flex: none; }
    mat-paginator { border-top: 1px solid var(--app-border-soft); background: transparent; }
  `,
})
export class NotificationsComponent {
  protected readonly notifications = inject(NotificationService);
  private readonly exporter = inject(ExportService);
  private readonly router = inject(Router);

  protected readonly list = pagedList<NotificationDto, { unreadOnly: boolean }>(
    (page, f) => this.notifications.list(page, f.unreadOnly),
    { unreadOnly: false },
  );

  protected icon(n: NotificationDto): string {
    return ICONS[n.entityName ?? ''] ?? 'notifications';
  }

  open(n: NotificationDto): void {
    if (!n.isRead) {
      this.notifications.markRead(n.id).subscribe(() => this.list.reload());
    }
    if (n.entityName === 'ExportJob' && n.entityId) {
      this.exporter.downloadJob(n.entityId);
      return;
    }
    const base = ENTITY_ROUTES[n.entityName ?? ''];
    if (base && n.entityId) {
      void this.router.navigate([base, n.entityId]);
    }
  }

  markAll(): void {
    this.notifications.markAllRead().subscribe(() => this.list.reload());
  }
}
