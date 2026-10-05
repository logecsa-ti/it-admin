import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { AuditLogDto, QueryParams } from '@core/models';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumOptions } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { ExportService } from '@shared/services/export.service';
import { pagedList } from '@shared/utils/paged-list';
import { ReportsService } from '../reports.service';
import { AuditDetailDialogComponent } from './audit-detail-dialog.component';

const MODULES = ['Assets', 'Organization', 'Users', 'Roles', 'Software', 'Licenses', 'Vendors', 'Contracts', 'Tickets', 'Requests', 'Maintenance', 'Changes', 'Purchases', 'Configuration', 'General'];

/** Bitacora de auditoria, solo lectura (SPECS.md 40). */
@Component({
  selector: 'app-audit-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
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
      <app-page-header title="Auditoria" subtitle="Registro inmutable de quien hizo que, cuando y desde donde.">
        <button mat-stroked-button type="button" *appCan="P.ReportsExport" [disabled]="exporter.busy()" (click)="exporter.export('audit', 'xlsx', list.filters())">
          <mat-icon>download</mat-icon>Exportar
        </button>
      </app-page-header>
      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Entidad, id o correlacion" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Accion</mat-label>
            <mat-select [value]="list.filters()['action'] ?? null" (selectionChange)="list.patchFilters({ action: $event.value })">
              <mat-option [value]="null">Todas</mat-option>
              @for (o of actions; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Modulo</mat-label>
            <mat-select [value]="list.filters()['module'] ?? null" (selectionChange)="list.patchFilters({ module: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (m of modules; track m) { <mat-option [value]="m">{{ m }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Desde</mat-label>
            <input matInput [matDatepicker]="fromPicker" (dateChange)="setDate('from', $event.value)" />
            <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
            <mat-datepicker #fromPicker />
          </mat-form-field>
          <mat-form-field>
            <mat-label>Hasta</mat-label>
            <input matInput [matDatepicker]="toPicker" (dateChange)="setDate('to', $event.value)" />
            <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
            <mat-datepicker #toPicker />
          </mat-form-field>
        </div>
        <app-data-table
          caption="Auditoria"
          [columns]="columns"
          [rows]="list.items()"
          [loading]="list.loading()"
          [total]="list.total()"
          [page]="list.page()"
          [pageSize]="list.pageSize()"
          [clickable]="true"
          (pageChange)="list.onPage($event)"
          (rowClick)="open($event)"
          emptyIcon="policy"
        >
          <ng-template appCell="date" let-row>{{ row.timestamp | appDate: 'short' }}</ng-template>
          <ng-template appCell="action" let-row><app-status-badge kind="AuditAction" [value]="row.action" /></ng-template>
          <ng-template appCell="entity" let-row>{{ row.entityName }} <span class="subtle mono">#{{ row.entityId }}</span></ng-template>
        </app-data-table>
      </section>
    </div>
  `,
})
export class AuditListComponent {
  private readonly reports = inject(ReportsService);
  private readonly dialog = inject(MatDialog);
  protected readonly exporter = inject(ExportService);
  protected readonly P = P;

  protected readonly actions = enumOptions('AuditAction');
  protected readonly modules = MODULES;
  protected readonly list = pagedList<AuditLogDto, QueryParams>((page, f) => this.reports.audit(page, f), {});

  protected readonly columns: TableColumn<AuditLogDto>[] = [
    { key: 'date', header: 'Fecha', width: '140px' },
    { key: 'user', header: 'Usuario', value: (a) => a.userName ?? 'Sistema' },
    { key: 'action', header: 'Accion', width: '150px' },
    { key: 'module', header: 'Modulo', value: (a) => a.module, hideOnMobile: true },
    { key: 'entity', header: 'Registro' },
    { key: 'ip', header: 'IP', value: (a) => a.ipAddress ?? '—', hideOnMobile: true },
  ];

  /** Rango en la fecha local: desde las 00:00 hasta el final del dia, enviado como instante UTC. */
  setDate(key: 'from' | 'to', date: Date | null): void {
    if (!date) {
      this.list.patchFilters({ [key]: null });
      return;
    }
    const value = new Date(date);
    if (key === 'to') value.setHours(23, 59, 59, 999);
    this.list.patchFilters({ [key]: value.toISOString() });
  }

  open(log: AuditLogDto): void {
    this.dialog.open(AuditDetailDialogComponent, { data: log, width: '820px', maxWidth: '95vw' });
  }
}
