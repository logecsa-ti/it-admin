import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { LicenseDto, QueryParams } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumLabel } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { ExportService } from '@shared/services/export.service';
import { LookupService } from '@shared/services/lookup.service';
import { pagedList } from '@shared/utils/paged-list';
import { licenseFields } from '../licensing-forms';
import { LicensingService } from '../licensing.service';

interface LicenseFilters extends QueryParams {
  softwareId?: number | null;
  isActive?: boolean | null;
}

@Component({
  selector: 'app-license-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AsyncPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatMenuModule,
    MatSelectModule,
    PageHeaderComponent,
    SearchBoxComponent,
    DataTableComponent,
    CellDefDirective,
    StatusBadgeComponent,
    CanDirective,
    AppDatePipe,
  ],
  template: `
    <div class="page">
      <app-page-header title="Licencias" subtitle="Licencias de software, puestos en uso y vencimientos.">
        <button mat-stroked-button type="button" [matMenuTriggerFor]="exportMenu" *appCan="P.ReportsExport" [disabled]="exporter.busy()">
          <mat-icon>download</mat-icon>Exportar uso
        </button>
        <mat-menu #exportMenu="matMenu">
          <button mat-menu-item type="button" (click)="exporter.export('licenses', 'xlsx')">Excel (.xlsx)</button>
          <button mat-menu-item type="button" (click)="exporter.export('licenses', 'csv')">CSV</button>
        </mat-menu>
        <button mat-flat-button type="button" *appCan="P.LicensesManage" (click)="create()"><mat-icon>add</mat-icon>Nueva licencia</button>
      </app-page-header>

      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Nombre de licencia" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Software</mat-label>
            <mat-select [value]="list.filters().softwareId ?? null" (selectionChange)="list.patchFilters({ softwareId: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (s of software$ | async; track s.id) { <mat-option [value]="s.id">{{ s.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-button-toggle-group [value]="list.filters().isActive === false ? 'false' : list.filters().isActive ? 'true' : 'all'"
            (change)="list.patchFilters({ isActive: $event.value === 'all' ? null : $event.value === 'true' })" aria-label="Estado">
            <mat-button-toggle value="true">Activas</mat-button-toggle>
            <mat-button-toggle value="false">Inactivas</mat-button-toggle>
            <mat-button-toggle value="all">Todas</mat-button-toggle>
          </mat-button-toggle-group>
        </div>

        <app-data-table
          caption="Licencias"
          [columns]="columns"
          [rows]="list.items()"
          [loading]="list.loading()"
          [total]="list.total()"
          [page]="list.page()"
          [pageSize]="list.pageSize()"
          [clickable]="true"
          (pageChange)="list.onPage($event)"
          (sortChange)="list.onSort($event)"
          (rowClick)="open($event)"
          emptyIcon="key"
        >
          <ng-template appCell="name" let-row>
            <div class="name-cell"><span class="strong">{{ row.name }}</span><span class="subtle">{{ row.softwareName }}</span></div>
          </ng-template>
          <ng-template appCell="usage" let-row>
            <div class="usage">
              <div class="bar" aria-hidden="true"><div [style.width.%]="percent(row)" [class.full]="row.availableQuantity <= 0"></div></div>
              <span>{{ row.usedQuantity }} / {{ row.quantity }}</span>
            </div>
          </ng-template>
          <ng-template appCell="expiration" let-row>
            @if (row.expirationDate) {
              {{ row.expirationDate | appDate: 'date' }}
            } @else {
              <span class="subtle">No vence</span>
            }
          </ng-template>
          <ng-template appCell="state" let-row>
            <app-status-badge [label]="row.isActive ? 'Activa' : 'Inactiva'" [tone]="row.isActive ? 'success' : 'neutral'" />
          </ng-template>
        </app-data-table>
      </section>
    </div>
  `,
  styles: `
    .name-cell { display: flex; flex-direction: column; }
    .strong { font-weight: 500; }
    .usage { display: flex; align-items: center; gap: 10px; white-space: nowrap; }
    .bar { width: 80px; height: 6px; border-radius: 3px; background: var(--app-border-soft); overflow: hidden; }
    .bar div { height: 100%; background: var(--app-olive-500); }
    .bar div.full { background: var(--app-tone-danger-fg); }
  `,
})
export class LicenseListComponent {
  private readonly licensing = inject(LicensingService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly exporter = inject(ExportService);
  protected readonly P = P;

  protected readonly software$ = this.lookups.software();
  protected readonly list = pagedList<LicenseDto, LicenseFilters>(
    (page, filters) => this.licensing.licenses.list(page, filters),
    { isActive: true },
  );

  protected readonly columns: TableColumn<LicenseDto>[] = [
    { key: 'name', header: 'Licencia', sortKey: 'name' },
    { key: 'type', header: 'Tipo', value: (l) => enumLabel('LicenseType', l.licenseType), hideOnMobile: true },
    { key: 'usage', header: 'Puestos', sortKey: 'usedQuantity', width: '170px' },
    { key: 'vendor', header: 'Proveedor', value: (l) => l.vendorName ?? '—', hideOnMobile: true },
    { key: 'expiration', header: 'Vencimiento', sortKey: 'expirationDate', width: '140px' },
    { key: 'state', header: 'Estado', width: '110px' },
  ];

  protected percent(license: LicenseDto): number {
    return license.quantity > 0 ? Math.min(100, Math.round((license.usedQuantity / license.quantity) * 100)) : 0;
  }

  open(license: LicenseDto): void {
    void this.router.navigate(['/licencias', license.id]);
  }

  create(): void {
    this.dialogs
      .form({ title: 'Nueva licencia', fields: licenseFields(this.lookups, null), save: (value) => this.licensing.licenses.create(value as never) })
      .subscribe((license) => {
        this.toast.success('Licencia creada.');
        void this.router.navigate(['/licencias', license.id]);
      });
  }
}
