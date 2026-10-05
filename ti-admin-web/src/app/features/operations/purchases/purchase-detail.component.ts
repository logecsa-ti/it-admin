import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { PurchaseRequestDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { valueOf } from '@shared/utils/resource-utils';
import { OperationsService } from '../operations.service';
import { WorkflowAction, WorkflowActionsComponent } from '../workflow-actions.component';

@Component({
  selector: 'app-purchase-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressBarModule, MatTableModule, PageHeaderComponent, StatusBadgeComponent, DocumentsPanelComponent, WorkflowActionsComponent, AppDatePipe, MoneyPipe],
  template: `
    <div class="page">
      @if (purchase.isLoading() && !data()) { <mat-progress-bar mode="indeterminate" /> }
      @if (data(); as p) {
        <app-page-header [title]="p.title" [eyebrow]="p.number" backLink="/compras" [subtitle]="'Solicitada por ' + p.requestedByName + ' · ' + (p.requestedDate | appDate: 'date')">
          <app-workflow-actions [actions]="actions()" (run)="run($event, p)" />
        </app-page-header>

        <div class="two-columns">
          <div class="page">
            <section class="panel">
              <div class="panel-title">
                <h2>Solicitud</h2>
                <app-status-badge kind="PurchaseStatus" [value]="p.status" />
              </div>
              <div class="panel-body">
                <dl class="detail-grid">
                  <div><dt>Monto estimado</dt><dd class="amount">{{ p.estimatedCost | money }}</dd></div>
                  <div><dt>Proveedor sugerido</dt><dd>{{ p.vendorName ?? '—' }}</dd></div>
                  <div><dt>Fecha requerida</dt><dd>{{ p.neededDate | appDate: 'date' }}</dd></div>
                  @if (p.approvedAt) { <div><dt>Aprobada</dt><dd>{{ p.approvedAt | appDate }}</dd></div> }
                  @if (p.orderedAt) { <div><dt>Ordenada</dt><dd>{{ p.orderedAt | appDate }}</dd></div> }
                  @if (p.receivedAt) { <div><dt>Recibida</dt><dd>{{ p.receivedAt | appDate }}</dd></div> }
                  @if (p.cancelledAt) { <div><dt>Cancelada</dt><dd>{{ p.cancelledAt | appDate }}</dd></div> }
                </dl>
                @if (p.justification) { <h3 class="sub">Justificacion</h3><p class="text">{{ p.justification }}</p> }
                @if (p.description) { <h3 class="sub">Descripcion</h3><p class="text">{{ p.description }}</p> }
                @if (p.rejectionReason) { <h3 class="sub">Motivo del rechazo</h3><p class="text">{{ p.rejectionReason }}</p> }
              </div>
            </section>

            <section class="panel">
              <div class="panel-title"><h2>Articulos</h2></div>
              <div class="table-scroll">
                <table mat-table [dataSource]="p.items" aria-label="Articulos de la compra">
                  <ng-container matColumnDef="description">
                    <th mat-header-cell *matHeaderCellDef>Descripcion</th>
                    <td mat-cell *matCellDef="let item">{{ item.description }}</td>
                    <td mat-footer-cell *matFooterCellDef><strong>Total</strong></td>
                  </ng-container>
                  <ng-container matColumnDef="quantity">
                    <th mat-header-cell *matHeaderCellDef class="end">Cantidad</th>
                    <td mat-cell *matCellDef="let item" class="end">{{ item.quantity }}</td>
                    <td mat-footer-cell *matFooterCellDef></td>
                  </ng-container>
                  <ng-container matColumnDef="unitPrice">
                    <th mat-header-cell *matHeaderCellDef class="end">Precio unitario</th>
                    <td mat-cell *matCellDef="let item" class="end">{{ item.unitPrice | money }}</td>
                    <td mat-footer-cell *matFooterCellDef></td>
                  </ng-container>
                  <ng-container matColumnDef="total">
                    <th mat-header-cell *matHeaderCellDef class="end">Subtotal</th>
                    <td mat-cell *matCellDef="let item" class="end">{{ item.totalPrice | money }}</td>
                    <td mat-footer-cell *matFooterCellDef class="end"><strong>{{ p.estimatedCost | money }}</strong></td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="itemColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: itemColumns"></tr>
                  <tr mat-footer-row *matFooterRowDef="itemColumns"></tr>
                </table>
              </div>
            </section>
          </div>
          <app-documents-panel entityName="PurchaseRequest" [entityId]="p.id" [canUpload]="canUpload()" [canManage]="auth.hasAny(P.PurchasesManage)" />
        </div>
      }
    </div>
  `,
  styles: `
    .amount { font-size: 1.15rem; font-weight: 600; }
    .sub { margin: 20px 0 6px; font-size: 0.8rem; color: var(--app-text-muted); text-transform: uppercase; letter-spacing: 0.04em; }
    .text { margin: 0; white-space: pre-line; }
    table { width: 100%; }
    .end { text-align: right; }
  `,
})
export class PurchaseDetailComponent {
  private readonly operations = inject(OperationsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly P = P;
  protected readonly itemColumns = ['description', 'quantity', 'unitPrice', 'total'];

  readonly id = input.required<number, string>({ transform: Number });
  protected readonly purchase = rxResource({ params: () => this.id(), stream: ({ params }) => this.operations.purchases.get(params) });
  protected readonly data = computed(() => valueOf(this.purchase));
  private readonly isRequester = computed(() => this.data()?.requestedById === this.auth.user()?.id);
  protected readonly canUpload = computed(() => this.auth.hasAny(P.PurchasesManage) || (this.isRequester() && this.auth.hasAny(P.PurchasesCreate)));

  protected readonly actions = computed<WorkflowAction[]>(() => {
    const p = this.data();
    if (!p) return [];
    const create = this.auth.hasAny(P.PurchasesCreate);
    const approver = this.auth.hasAny(P.PurchasesApprove) && !this.isRequester();
    const manage = this.auth.hasAny(P.PurchasesManage);
    const actions: WorkflowAction[] = [];
    if (p.status === 'Draft' && create) {
      actions.push({ id: 'submit', label: 'Enviar a aprobacion', icon: 'send', primary: true }, { id: 'edit', label: 'Editar', icon: 'edit' });
    }
    if (p.status === 'Submitted' && approver) {
      actions.push({ id: 'approve', label: 'Aprobar', icon: 'check', primary: true }, { id: 'reject', label: 'Rechazar', icon: 'close', destructive: true });
    }
    if (p.status === 'Approved' && manage) actions.push({ id: 'order', label: 'Marcar como ordenada', icon: 'local_shipping', primary: true });
    if (p.status === 'Ordered' && manage) actions.push({ id: 'receive', label: 'Registrar recepcion', icon: 'inventory', primary: true });
    if (['Draft', 'Submitted', 'Approved'].includes(p.status) && create) {
      actions.push({ id: 'cancel', label: 'Cancelar solicitud', icon: 'block', destructive: true });
    }
    return actions;
  });

  run(action: string, p: PurchaseRequestDto): void {
    const api = this.operations.purchases;
    switch (action) {
      case 'edit':
        void this.router.navigate(['/compras', p.id, 'editar']);
        break;
      case 'submit':
        api.action(p.id, 'submit').subscribe(() => this.changed('Solicitud enviada a aprobacion.'));
        break;
      case 'approve':
        this.confirmService
          .confirm({ title: 'Aprobar compra', message: `¿Aprobar ${p.number} por un monto estimado de ${p.estimatedCost}?`, confirmText: 'Aprobar' })
          .subscribe((ok) => ok && api.action(p.id, 'approve').subscribe(() => this.changed('Compra aprobada.')));
        break;
      case 'reject':
        this.confirmService
          .askReason({ title: 'Rechazar compra', message: `¿Rechazar ${p.number}?`, confirmText: 'Rechazar', destructive: true, reason: { label: 'Motivo', required: true } })
          .subscribe((reason) => reason && api.action(p.id, 'reject', { reason }).subscribe(() => this.changed('Compra rechazada.')));
        break;
      case 'order':
        api.action(p.id, 'order').subscribe(() => this.changed('Compra marcada como ordenada.'));
        break;
      case 'receive':
        api.action(p.id, 'receive').subscribe(() => this.changed('Recepcion registrada. Registre los activos recibidos en el inventario.'));
        break;
      case 'cancel':
        this.confirmService
          .confirm({ title: 'Cancelar solicitud', message: `¿Cancelar ${p.number}?`, confirmText: 'Cancelar solicitud', destructive: true })
          .subscribe((ok) => ok && api.action(p.id, 'cancel').subscribe(() => this.changed('Solicitud cancelada.')));
        break;
    }
  }

  private changed(message: string): void {
    this.toast.success(message);
    this.purchase.reload();
  }
}
