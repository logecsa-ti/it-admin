import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { ChangeRequestDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { valueOf } from '@shared/utils/resource-utils';
import { changeFields } from '../operations-forms';
import { OperationsService } from '../operations.service';
import { WorkflowAction, WorkflowActionsComponent } from '../workflow-actions.component';

@Component({
  selector: 'app-change-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatProgressBarModule, PageHeaderComponent, StatusBadgeComponent, DocumentsPanelComponent, WorkflowActionsComponent, AppDatePipe],
  template: `
    <div class="page">
      @if (change.isLoading() && !data()) { <mat-progress-bar mode="indeterminate" /> }
      @if (data(); as c) {
        <app-page-header [title]="c.title" [eyebrow]="c.number" backLink="/cambios" [subtitle]="'Solicitado por ' + c.requestedByName + ' · ' + (c.createdAt | appDate)">
          <app-workflow-actions [actions]="actions()" (run)="run($event, c)" />
        </app-page-header>

        <div class="two-columns">
          <div class="page">
            <section class="panel">
              <div class="panel-title">
                <h2>Solicitud</h2>
                <div class="badges">
                  <app-status-badge kind="ChangeType" [value]="c.type" />
                  <app-status-badge kind="ChangeStatus" [value]="c.status" />
                </div>
              </div>
              <div class="panel-body">
                <dl class="detail-grid">
                  <div><dt>Riesgo</dt><dd><app-status-badge kind="ChangeRisk" [value]="c.risk" /></dd></div>
                  <div><dt>Impacto</dt><dd><app-status-badge kind="ChangeImpact" [value]="c.impact" /></dd></div>
                  <div><dt>Fecha planificada</dt><dd>{{ c.plannedDate | appDate }}</dd></div>
                  <div><dt>Responsable</dt><dd>{{ c.assignedToName ?? 'Sin asignar' }}</dd></div>
                  @if (c.assetId) { <div><dt>Activo</dt><dd><a [routerLink]="['/activos', c.assetId]">Ver activo</a></dd></div> }
                </dl>
                <h3 class="sub">Descripcion</h3>
                <p class="text">{{ c.description }}</p>
                @if (c.rollbackPlan) {
                  <h3 class="sub">Plan de reversion</h3>
                  <p class="text">{{ c.rollbackPlan }}</p>
                }
              </div>
            </section>

            @if (c.approvedByName || c.rejectionReason || c.implementationNotes) {
              <section class="panel">
                <div class="panel-title"><h2>Decision e implementacion</h2></div>
                <div class="panel-body">
                  <dl class="detail-grid">
                    @if (c.approvedByName) { <div><dt>Aprobado por</dt><dd>{{ c.approvedByName }} · {{ c.approvedAt | appDate }}</dd></div> }
                    @if (c.implementationDate) { <div><dt>Inicio de implementacion</dt><dd>{{ c.implementationDate | appDate }}</dd></div> }
                    @if (c.completedAt) { <div><dt>Finalizado</dt><dd>{{ c.completedAt | appDate }}</dd></div> }
                    @if (c.closedAt) { <div><dt>Cerrado</dt><dd>{{ c.closedAt | appDate }}</dd></div> }
                  </dl>
                  @if (c.reviewComment) { <h3 class="sub">Comentario de revision</h3><p class="text">{{ c.reviewComment }}</p> }
                  @if (c.rejectionReason) { <h3 class="sub">Motivo del rechazo</h3><p class="text">{{ c.rejectionReason }}</p> }
                  @if (c.implementationNotes) { <h3 class="sub">Notas de implementacion</h3><p class="text">{{ c.implementationNotes }}</p> }
                </div>
              </section>
            }
          </div>
          <app-documents-panel entityName="ChangeRequest" [entityId]="c.id" [canUpload]="canEdit()" [canManage]="canManage()" />
        </div>
      }
    </div>
  `,
  styles: `
    .badges { display: flex; gap: 6px; }
    .sub { margin: 20px 0 6px; font-size: 0.8rem; color: var(--app-text-muted); text-transform: uppercase; letter-spacing: 0.04em; }
    .text { margin: 0; white-space: pre-line; }
  `,
})
export class ChangeDetailComponent {
  private readonly operations = inject(OperationsService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  readonly id = input.required<number, string>({ transform: Number });
  protected readonly change = rxResource({ params: () => this.id(), stream: ({ params }) => this.operations.changes.get(params) });
  protected readonly data = computed(() => valueOf(this.change));

  protected readonly canManage = computed(() => this.auth.hasAny(P.ChangesManage));
  private readonly isRequester = computed(() => this.data()?.requestedById === this.auth.user()?.id);
  protected readonly canEdit = computed(() => this.auth.hasAny(P.ChangesCreate) && (this.isRequester() || this.canManage()));

  protected readonly actions = computed<WorkflowAction[]>(() => {
    const c = this.data();
    if (!c) return [];
    const review = this.auth.hasAny(P.ChangesReview) && !this.isRequester(); // segregacion de funciones
    const implementer = c.assignedToId === this.auth.user()?.id || this.canManage();
    const actions: WorkflowAction[] = [];
    switch (c.status) {
      case 'Draft':
        if (this.canEdit()) {
          actions.push({ id: 'submit', label: 'Enviar a revision', icon: 'send', primary: true }, { id: 'edit', label: 'Editar', icon: 'edit' }, { id: 'delete', label: 'Eliminar', icon: 'delete', destructive: true });
        }
        break;
      case 'Requested':
      case 'UnderReview':
        if (review) {
          if (c.status === 'Requested') actions.push({ id: 'review', label: 'Tomar en revision', icon: 'rate_review' });
          actions.push({ id: 'approve', label: 'Aprobar', icon: 'check', primary: true }, { id: 'reject', label: 'Rechazar', icon: 'close', destructive: true });
        }
        break;
      case 'Approved':
        if (this.canManage()) actions.push({ id: 'assign', label: c.assignedToId ? 'Reasignar' : 'Asignar responsable', icon: 'person_add' });
        if (implementer) actions.push({ id: 'start', label: 'Iniciar implementacion', icon: 'play_arrow', primary: true });
        break;
      case 'Implementing':
        if (implementer) {
          actions.push({ id: 'complete', label: 'Completar', icon: 'task_alt', primary: true }, { id: 'rollback', label: 'Revertir', icon: 'undo', destructive: true });
        }
        break;
      case 'Completed':
      case 'RolledBack':
      case 'Rejected':
        if (this.canManage()) actions.push({ id: 'close', label: 'Cerrar', icon: 'lock', primary: true });
        break;
    }
    return actions;
  });

  run(action: string, c: ChangeRequestDto): void {
    const api = this.operations.changes;
    switch (action) {
      case 'submit':
        api.action(c.id, 'submit').subscribe((r) => this.changed(r.status === 'Approved' ? 'Cambio estandar aprobado automaticamente.' : 'Cambio enviado a revision.'));
        break;
      case 'review':
        api.action(c.id, 'review').subscribe(() => this.changed('Cambio en revision.'));
        break;
      case 'approve':
        this.confirmService
          .askReason({ title: 'Aprobar cambio', message: `¿Aprobar ${c.number}?`, confirmText: 'Aprobar', reason: { label: 'Comentario (opcional)' } })
          .subscribe((comment) => comment !== null && api.action(c.id, 'approve', { comment: comment || null }).subscribe(() => this.changed('Cambio aprobado.')));
        break;
      case 'reject':
        this.confirmService
          .askReason({ title: 'Rechazar cambio', message: `¿Rechazar ${c.number}?`, confirmText: 'Rechazar', destructive: true, reason: { label: 'Motivo del rechazo', required: true } })
          .subscribe((reason) => reason && api.action(c.id, 'reject', { reason }).subscribe(() => this.changed('Cambio rechazado.')));
        break;
      case 'assign':
        this.dialogs
          .form({
            title: 'Asignar responsable',
            submitText: 'Asignar',
            fields: [{ key: 'userId', label: 'Responsable de implementar', type: 'picker', required: true, wide: true, search: this.lookups.searchUsers }],
            save: (value) => api.action(c.id, 'assign', value),
          })
          .subscribe(() => this.changed('Responsable asignado.'));
        break;
      case 'start':
        api.action(c.id, 'start').subscribe(() => this.changed('Implementacion iniciada.'));
        break;
      case 'complete':
        this.dialogs
          .form({
            title: 'Completar implementacion',
            submitText: 'Completar',
            fields: [{ key: 'notes', label: 'Notas de implementacion', type: 'textarea', maxLength: 4000 }],
            save: (value) => api.action(c.id, 'complete', value),
          })
          .subscribe(() => this.changed('Cambio completado.'));
        break;
      case 'rollback':
        this.confirmService
          .askReason({ title: 'Revertir cambio', message: 'Registre que se aplico el plan de reversion.', confirmText: 'Revertir', destructive: true, reason: { label: 'Que ocurrio', required: true } })
          .subscribe((reason) => reason && api.action(c.id, 'rollback', { reason }).subscribe(() => this.changed('Cambio revertido.')));
        break;
      case 'close':
        api.action(c.id, 'close').subscribe(() => this.changed('Cambio cerrado.'));
        break;
      case 'edit':
        this.dialogs
          .form({ title: 'Editar cambio', fields: changeFields(this.lookups, c), value: c as unknown as Record<string, unknown>, save: (value) => api.update(c.id, value as never) })
          .subscribe(() => this.changed('Cambio actualizado.'));
        break;
      case 'delete':
        this.confirmService
          .confirm({ title: 'Eliminar borrador', message: `¿Eliminar ${c.number}?`, confirmText: 'Eliminar', destructive: true })
          .subscribe((ok) => {
            if (ok) api.remove(c.id).subscribe(() => {
              this.toast.success('Borrador eliminado.');
              void this.router.navigate(['/cambios']);
            });
          });
        break;
    }
  }

  private changed(message: string): void {
    this.toast.success(message);
    this.change.reload();
  }
}
