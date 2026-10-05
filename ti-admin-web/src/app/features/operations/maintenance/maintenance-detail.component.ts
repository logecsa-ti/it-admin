import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { MaintenanceDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { valueOf } from '@shared/utils/resource-utils';
import { maintenanceFields } from '../operations-forms';
import { OperationsService } from '../operations.service';
import { WorkflowAction, WorkflowActionsComponent } from '../workflow-actions.component';

@Component({
  selector: 'app-maintenance-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatProgressBarModule, PageHeaderComponent, StatusBadgeComponent, DocumentsPanelComponent, WorkflowActionsComponent, AppDatePipe, MoneyPipe],
  template: `
    <div class="page">
      @if (maintenance.isLoading() && !data()) { <mat-progress-bar mode="indeterminate" /> }
      @if (data(); as m) {
        <app-page-header [title]="m.title" [eyebrow]="m.number" backLink="/mantenimientos">
          <app-workflow-actions [actions]="actions()" (run)="run($event, m)" />
        </app-page-header>

        @if (m.isOverdue) {
          <div class="banner" role="status">Este mantenimiento esta vencido: la fecha programada ya paso.</div>
        }

        <div class="two-columns">
          <div class="page">
            <section class="panel">
              <div class="panel-title">
                <h2>Planificacion</h2>
                <div class="badges">
                  <app-status-badge kind="MaintenanceType" [value]="m.type" />
                  <app-status-badge kind="MaintenanceStatus" [value]="m.status" />
                </div>
              </div>
              <div class="panel-body">
                <dl class="detail-grid">
                  <div><dt>Activo</dt><dd><a [routerLink]="['/activos', m.assetId]">{{ m.assetCode }}</a> · {{ m.assetName }}</dd></div>
                  <div><dt>Tecnico</dt><dd>{{ m.technicianName ?? 'Sin asignar' }}</dd></div>
                  <div><dt>Programado</dt><dd>{{ m.scheduledDate | appDate }}</dd></div>
                  <div><dt>Iniciado</dt><dd>{{ m.startedAt | appDate }}</dd></div>
                  <div><dt>Completado</dt><dd>{{ m.completedAt | appDate }}</dd></div>
                  <div><dt>Proximo mantenimiento</dt><dd>{{ m.nextDueDate | appDate: 'date' }}</dd></div>
                  <div><dt>Costo estimado</dt><dd>{{ m.estimatedCost | money }}</dd></div>
                  <div><dt>Costo real</dt><dd>{{ m.actualCost | money }}</dd></div>
                  @if (m.ticketId) {
                    <div><dt>Ticket</dt><dd><a [routerLink]="['/tickets', m.ticketId]">{{ m.ticketNumber }}</a></dd></div>
                  }
                </dl>
                @if (m.description) { <p class="text">{{ m.description }}</p> }
              </div>
            </section>

            @if (m.actions || m.findings || m.recommendations || m.cancellationReason) {
              <section class="panel">
                <div class="panel-title"><h2>Resultado</h2></div>
                <div class="panel-body">
                  <dl class="detail-grid">
                    @if (m.actions) { <div class="span-all"><dt>Acciones realizadas</dt><dd class="text">{{ m.actions }}</dd></div> }
                    @if (m.findings) { <div class="span-all"><dt>Hallazgos</dt><dd class="text">{{ m.findings }}</dd></div> }
                    @if (m.recommendations) { <div class="span-all"><dt>Recomendaciones</dt><dd class="text">{{ m.recommendations }}</dd></div> }
                    @if (m.cancellationReason) { <div class="span-all"><dt>Motivo de cancelacion</dt><dd class="text">{{ m.cancellationReason }}</dd></div> }
                  </dl>
                </div>
              </section>
            }
          </div>
          <app-documents-panel entityName="Maintenance" [entityId]="m.id" [canUpload]="canManage()" [canManage]="canManage()" />
        </div>
      }
    </div>
  `,
  styles: `
    .badges { display: flex; gap: 6px; }
    .text { white-space: pre-line; }
    p.text { margin: 16px 0 0; }
    .detail-grid .span-all { grid-column: 1 / -1; }
    .banner { padding: 10px 14px; border-radius: var(--app-radius); background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg); }
  `,
})
export class MaintenanceDetailComponent {
  private readonly operations = inject(OperationsService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly id = input.required<number, string>({ transform: Number });
  protected readonly maintenance = rxResource({ params: () => this.id(), stream: ({ params }) => this.operations.maintenances.get(params) });
  protected readonly data = computed(() => valueOf(this.maintenance));
  protected readonly canManage = computed(() => this.auth.hasAny(P.MaintenanceManage));

  protected readonly actions = computed<WorkflowAction[]>(() => {
    const m = this.data();
    if (!m || !this.canManage()) return [];
    const open = m.status === 'Planned' || m.status === 'Scheduled';
    return [
      ...(open ? [{ id: 'start', label: 'Iniciar', icon: 'play_arrow', primary: true }] : []),
      ...(m.status === 'InProgress' ? [{ id: 'complete', label: 'Completar', icon: 'task_alt', primary: true }] : []),
      ...(open ? [{ id: 'schedule', label: m.status === 'Planned' ? 'Programar' : 'Reprogramar', icon: 'event' }] : []),
      ...(open ? [{ id: 'edit', label: 'Editar', icon: 'edit' }] : []),
      ...(open || m.status === 'InProgress' ? [{ id: 'cancel', label: 'Cancelar', icon: 'block', destructive: true }] : []),
      ...(open ? [{ id: 'delete', label: 'Eliminar', icon: 'delete', destructive: true }] : []),
    ];
  });

  run(action: string, m: MaintenanceDto): void {
    const api = this.operations.maintenances;
    switch (action) {
      case 'start':
        api.action(m.id, 'start').subscribe(() => this.changed('Mantenimiento iniciado.'));
        break;
      case 'schedule':
        this.dialogs
          .form({
            title: 'Programar mantenimiento',
            submitText: 'Programar',
            fields: [{ key: 'scheduledDate', label: 'Fecha y hora', type: 'datetime', required: true, wide: true }],
            value: { scheduledDate: m.scheduledDate },
            save: (value) => api.action(m.id, 'schedule', value),
          })
          .subscribe(() => this.changed('Mantenimiento programado.'));
        break;
      case 'complete':
        this.dialogs
          .form({
            title: 'Completar mantenimiento',
            submitText: 'Completar',
            fields: [
              { key: 'actions', label: 'Acciones realizadas', type: 'textarea', required: true, maxLength: 4000 },
              { key: 'findings', label: 'Hallazgos', type: 'textarea', maxLength: 4000 },
              { key: 'recommendations', label: 'Recomendaciones', type: 'textarea', maxLength: 4000 },
              { key: 'actualCost', label: 'Costo real', type: 'number', min: 0 },
              { key: 'nextDueDate', label: 'Proximo mantenimiento', type: 'date', hint: 'Para preventivos periodicos' },
            ],
            save: (value) => api.action(m.id, 'complete', value),
          })
          .subscribe(() => this.changed('Mantenimiento completado.'));
        break;
      case 'cancel':
        this.confirmService
          .askReason({ title: 'Cancelar mantenimiento', message: `¿Cancelar ${m.number}?`, confirmText: 'Cancelar mantenimiento', destructive: true, reason: { label: 'Motivo', required: true } })
          .subscribe((reason) => reason && api.action(m.id, 'cancel', { reason }).subscribe(() => this.changed('Mantenimiento cancelado.')));
        break;
      case 'edit':
        this.dialogs
          .form({
            title: 'Editar mantenimiento',
            fields: maintenanceFields(this.lookups, m),
            value: m as unknown as Record<string, unknown>,
            save: (value) => api.update(m.id, value as never),
          })
          .subscribe(() => this.changed('Mantenimiento actualizado.'));
        break;
      case 'delete':
        this.confirmService
          .confirm({ title: 'Eliminar mantenimiento', message: `¿Eliminar ${m.number}?`, confirmText: 'Eliminar', destructive: true })
          .subscribe((ok) => {
            if (ok) api.remove(m.id).subscribe(() => {
              this.toast.success('Mantenimiento eliminado.');
              void this.router.navigate(['/mantenimientos']);
            });
          });
        break;
    }
  }

  private changed(message: string): void {
    this.toast.success(message);
    this.maintenance.reload();
  }
}
