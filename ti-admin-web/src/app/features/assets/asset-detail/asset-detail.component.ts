import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { of } from 'rxjs';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { AssetAssignmentDto, AssetDetailDto, AssetMovementDto, AssetStatus } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { DataTableComponent, CellDefDirective, TableColumn } from '@shared/components/data-table/data-table.component';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumLabel, enumOptions } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { valueOf } from '@shared/utils/resource-utils';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { emptyPage, pagedList } from '@shared/utils/paged-list';
import { AssetsService } from '../assets.service';

/** Estados a los que se puede pasar manualmente (Assigned solo via asignacion; reglas en AssetStatusRules). */
const MANUAL_STATUSES: AssetStatus[] = ['Available', 'Maintenance', 'Repair', 'Retired', 'Lost', 'Disposed'];

@Component({
  selector: 'app-asset-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTabsModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    DataTableComponent,
    CellDefDirective,
    DocumentsPanelComponent,
    CanDirective,
    AppDatePipe,
    MoneyPipe,
  ],
  templateUrl: './asset-detail.component.html',
  styles: `
    .title-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .assignment { display: flex; align-items: center; gap: 14px; padding: 16px 20px; }
    .assignment mat-icon { color: var(--app-olive-600); }
    .assignment .who { font-weight: 600; }
    mat-tab-group { margin-top: 4px; }
  `,
})
export class AssetDetailComponent {
  private readonly assets = inject(AssetsService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly lookups = inject(LookupService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly P = P;

  readonly id = input.required<number, string>({ transform: Number });

  protected readonly asset = rxResource({ params: () => this.id(), stream: ({ params }) => this.assets.get(params) });
  protected readonly data = computed(() => valueOf(this.asset));

  protected readonly assignments = pagedList<AssetAssignmentDto, { id: number }>(
    (page, filters) => (filters.id ? this.assets.assignments(filters.id, page) : of(emptyPage<AssetAssignmentDto>())),
    { id: 0 },
  );
  protected readonly movements = pagedList<AssetMovementDto, { id: number }>(
    (page, filters) => (filters.id ? this.assets.movements(filters.id, page) : of(emptyPage<AssetMovementDto>())),
    { id: 0 },
  );

  protected readonly canUploadDocs = computed(() => this.auth.hasAny(P.AssetsUpdate, P.DocumentsManage));

  protected readonly assignmentColumns: TableColumn<AssetAssignmentDto>[] = [
    { key: 'user', header: 'Usuario', value: (a) => a.userName },
    { key: 'from', header: 'Asignado' },
    { key: 'to', header: 'Devuelto' },
    { key: 'by', header: 'Asigno', value: (a) => a.assignedByName, hideOnMobile: true },
    { key: 'condition', header: 'Condicion', value: (a) => [a.conditionAtAssignment, a.conditionAtReturn].filter(Boolean).join(' → ') || '—', hideOnMobile: true },
  ];

  protected readonly movementColumns: TableColumn<AssetMovementDto>[] = [
    { key: 'date', header: 'Fecha', width: '170px' },
    { key: 'type', header: 'Movimiento', value: (m) => enumLabel('AssetMovementType', m.movementType) },
    { key: 'change', header: 'Cambio', value: (m) => [m.fromValue, m.toValue].filter(Boolean).join(' → ') || '—' },
    { key: 'user', header: 'Usuario', value: (m) => m.userName ?? '—', hideOnMobile: true },
    { key: 'notes', header: 'Notas', value: (m) => m.notes ?? '', hideOnMobile: true },
  ];

  constructor() {
    // Las tablas de historial siguen al id de la ruta.
    effect(() => {
      const id = this.id();
      untracked(() => {
        this.assignments.patchFilters({ id });
        this.movements.patchFilters({ id });
      });
    });
  }

  assign(asset: AssetDetailDto): void {
    this.dialogs
      .form({
        title: `Asignar ${asset.assetCode}`,
        submitText: 'Asignar',
        fields: [
          { key: 'userId', label: 'Usuario', type: 'picker', required: true, search: this.lookups.searchUsers, wide: true },
          { key: 'condition', label: 'Condicion de entrega', maxLength: 200, wide: true },
          { key: 'notes', label: 'Notas', type: 'textarea', maxLength: 500 },
        ],
        save: (value) => this.assets.assign(asset.id, value as { userId: number }),
      })
      .subscribe(() => this.changed('Activo asignado.'));
  }

  returnAsset(asset: AssetDetailDto): void {
    this.dialogs
      .form({
        title: `Registrar devolucion de ${asset.assetCode}`,
        submitText: 'Registrar devolucion',
        fields: [
          { key: 'condition', label: 'Condicion al devolver', maxLength: 200, wide: true },
          {
            key: 'resultingStatus',
            label: 'Estado resultante',
            type: 'select',
            defaultValue: 'Available',
            options: enumOptions('AssetStatus').filter((o) => ['Available', 'Maintenance', 'Repair'].includes(o.value)),
          },
          { key: 'notes', label: 'Notas', type: 'textarea', maxLength: 500 },
        ],
        save: (value) => this.assets.returnAsset(asset.id, value),
      })
      .subscribe(() => this.changed('Devolucion registrada.'));
  }

  changeStatus(asset: AssetDetailDto): void {
    this.dialogs
      .form({
        title: 'Cambiar estado',
        submitText: 'Cambiar estado',
        fields: [
          {
            key: 'status',
            label: 'Nuevo estado',
            type: 'select',
            required: true,
            options: enumOptions('AssetStatus').filter((o) => MANUAL_STATUSES.includes(o.value as AssetStatus) && o.value !== asset.status),
          },
          { key: 'notes', label: 'Motivo', type: 'textarea', maxLength: 500 },
        ],
        save: (value) => this.assets.changeStatus(asset.id, value as { status: AssetStatus }),
      })
      .subscribe(() => this.changed('Estado actualizado.'));
  }

  remove(asset: AssetDetailDto): void {
    this.confirmService
      .confirm({
        title: 'Eliminar activo',
        message: `¿Eliminar ${asset.assetCode}? El registro y su historial se conservan para auditoria.`,
        confirmText: 'Eliminar',
        destructive: true,
      })
      .subscribe((ok) => {
        if (!ok) return;
        this.assets.remove(asset.id).subscribe(() => {
          this.toast.success('Activo eliminado.');
          void this.router.navigate(['/activos']);
        });
      });
  }

  private changed(message: string): void {
    this.toast.success(message);
    this.asset.reload();
    this.assignments.reload();
    this.movements.reload();
  }
}
