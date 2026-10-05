import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { ContractDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { EnumLabelPipe } from '@shared/pipes/enum-label.pipe';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { fromDateOnly } from '@shared/utils/form-utils';
import { valueOf } from '@shared/utils/resource-utils';
import { contractFields } from '../licensing-forms';
import { LicensingService } from '../licensing.service';

@Component({
  selector: 'app-contract-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    DocumentsPanelComponent,
    CanDirective,
    EnumLabelPipe,
    AppDatePipe,
    MoneyPipe,
  ],
  template: `
    <div class="page">
      @if (contract.isLoading() && !data()) { <mat-progress-bar mode="indeterminate" /> }
      @if (data(); as c) {
        <app-page-header [title]="c.name" [eyebrow]="c.number" backLink="/contratos" [subtitle]="c.vendorName">
          <ng-container *appCan="P.ContractsManage">
            @if (c.status === 'Draft') {
              <button mat-flat-button type="button" (click)="activate(c)"><mat-icon>play_arrow</mat-icon>Activar</button>
            }
            @if (c.status === 'Active' || c.status === 'Expiring' || c.status === 'Expired') {
              <button mat-flat-button type="button" (click)="renew(c)"><mat-icon>autorenew</mat-icon>Renovar</button>
            }
            <button mat-stroked-button type="button" (click)="edit(c)"><mat-icon>edit</mat-icon>Editar</button>
            <button mat-icon-button type="button" [matMenuTriggerFor]="more" aria-label="Mas acciones"><mat-icon>more_vert</mat-icon></button>
            <mat-menu #more="matMenu" xPosition="before">
              @if (c.status === 'Active' || c.status === 'Expiring') {
                <button mat-menu-item type="button" (click)="terminate(c)"><mat-icon>cancel</mat-icon>Terminar</button>
              }
              <button mat-menu-item type="button" (click)="remove(c)"><mat-icon>delete</mat-icon>Eliminar</button>
            </mat-menu>
          </ng-container>
        </app-page-header>

        <div class="two-columns">
          <section class="panel">
            <div class="panel-title">
              <h2>Informacion del contrato</h2>
              <app-status-badge kind="ContractStatus" [value]="c.status" />
            </div>
            <div class="panel-body">
              <dl class="detail-grid">
                <div><dt>Proveedor</dt><dd>{{ c.vendorName }}</dd></div>
                <div><dt>Tipo</dt><dd>{{ c.type | enumLabel: 'ContractType' }}</dd></div>
                <div><dt>Inicio</dt><dd>{{ c.startDate | appDate: 'date' }}</dd></div>
                <div><dt>Fin</dt><dd>{{ c.endDate | appDate: 'date' }}</dd></div>
                <div><dt>Dias restantes</dt><dd>{{ c.daysRemaining }}</dd></div>
                <div><dt>Valor</dt><dd>{{ c.value | money: c.currency }}</dd></div>
                <div><dt>Renovacion automatica</dt><dd>{{ c.autoRenew ? 'Si' : 'No' }}</dd></div>
                <div><dt>Aviso de renovacion</dt><dd>{{ c.renewalNoticeDays }} dias antes</dd></div>
                <div><dt>Responsable</dt><dd>{{ c.responsibleUserName ?? '—' }}</dd></div>
                @if (c.renewedFromContractId) {
                  <div><dt>Renueva a</dt><dd><a [routerLink]="['/contratos', c.renewedFromContractId]">Contrato anterior</a></dd></div>
                }
              </dl>
              @if (c.notes) { <p class="notes subtle">{{ c.notes }}</p> }
            </div>
          </section>
          <app-documents-panel entityName="Contract" [entityId]="c.id" [canUpload]="canManage()" [canManage]="canManage()" />
        </div>
      }
    </div>
  `,
  styles: `.notes { margin: 16px 0 0; white-space: pre-line; }`,
})
export class ContractDetailComponent {
  private readonly licensing = inject(LicensingService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly P = P;

  readonly id = input.required<number, string>({ transform: Number });
  protected readonly contract = rxResource({ params: () => this.id(), stream: ({ params }) => this.licensing.contracts.get(params) });
  protected readonly data = computed(() => valueOf(this.contract));
  protected readonly canManage = computed(() => this.auth.hasAny(P.ContractsManage));

  activate(contract: ContractDto): void {
    this.licensing.contracts.action(contract.id, 'activate').subscribe(() => this.changed('Contrato activado.'));
  }

  terminate(contract: ContractDto): void {
    this.confirmService
      .confirm({ title: 'Terminar contrato', message: `¿Terminar ${contract.number} antes de su vencimiento?`, confirmText: 'Terminar', destructive: true })
      .subscribe((ok) => ok && this.licensing.contracts.action(contract.id, 'terminate').subscribe(() => this.changed('Contrato terminado.')));
  }

  renew(contract: ContractDto): void {
    const start = fromDateOnly(contract.endDate);
    start?.setDate(start.getDate() + 1);
    const end = start ? new Date(start.getFullYear() + 1, start.getMonth(), start.getDate() - 1) : null;
    this.dialogs
      .form({
        title: `Renovar ${contract.number}`,
        submitText: 'Renovar',
        fields: [
          { key: 'number', label: 'Numero del nuevo contrato', required: true, maxLength: 50, wide: true },
          { key: 'startDate', label: 'Inicio', type: 'date', required: true, defaultValue: start },
          { key: 'endDate', label: 'Fin', type: 'date', required: true, defaultValue: end },
          { key: 'value', label: 'Valor', type: 'number', min: 0, defaultValue: contract.value ?? null },
        ],
        save: (value) => this.licensing.renew(contract.id, value as never),
      })
      .subscribe((renewed) => {
        this.toast.success('Contrato renovado.');
        this.lookups.invalidate('contracts');
        void this.router.navigate(['/contratos', renewed.id]);
      });
  }

  edit(contract: ContractDto): void {
    this.dialogs
      .form({
        title: 'Editar contrato',
        fields: contractFields(this.lookups, contract),
        value: contract as unknown as Record<string, unknown>,
        save: (value) => this.licensing.contracts.update(contract.id, value as never),
      })
      .subscribe(() => this.changed('Contrato actualizado.'));
  }

  remove(contract: ContractDto): void {
    this.confirmService
      .confirm({ title: 'Eliminar contrato', message: `¿Eliminar ${contract.number}?`, confirmText: 'Eliminar', destructive: true })
      .subscribe((ok) => {
        if (ok) {
          this.licensing.contracts.remove(contract.id).subscribe(() => {
            this.toast.success('Contrato eliminado.');
            this.lookups.invalidate('contracts');
            void this.router.navigate(['/contratos']);
          });
        }
      });
  }

  private changed(message: string): void {
    this.toast.success(message);
    this.lookups.invalidate('contracts');
    this.contract.reload();
  }
}
