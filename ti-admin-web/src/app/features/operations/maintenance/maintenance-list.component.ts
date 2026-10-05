import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { MaintenanceDto, QueryParams } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumOptions } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { pagedList } from '@shared/utils/paged-list';
import { maintenanceFields } from '../operations-forms';
import { MaintenanceCalendarComponent } from './maintenance-calendar.component';
import { OperationsService } from '../operations.service';

interface MaintenanceFilters extends QueryParams {
  type?: string | null;
  status?: string | null;
  overdue?: boolean | null;
}

@Component({
  selector: 'app-maintenance-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeaderComponent,
    SearchBoxComponent,
    DataTableComponent,
    CellDefDirective,
    StatusBadgeComponent,
    CanDirective,
    AppDatePipe,
    MaintenanceCalendarComponent,
  ],
  template: `
    <div class="page">
      <app-page-header title="Mantenimientos" subtitle="Mantenimiento preventivo y correctivo de los activos.">
        <button mat-flat-button type="button" *appCan="P.MaintenanceManage" (click)="create()"><mat-icon>add</mat-icon>Nuevo mantenimiento</button>
      </app-page-header>
      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Numero, titulo o activo" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Tipo</mat-label>
            <mat-select [value]="list.filters().type ?? null" (selectionChange)="list.patchFilters({ type: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of types; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Estado</mat-label>
            <mat-select [value]="list.filters().status ?? null" (selectionChange)="list.patchFilters({ status: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of statuses; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-checkbox [checked]="!!list.filters().overdue" (change)="list.patchFilters({ overdue: $event.checked || null })">Vencidos</mat-checkbox>
          <mat-button-toggle-group class="view-toggle" hideSingleSelectionIndicator [value]="view()" (change)="setView($event.value)" aria-label="Vista">
            <mat-button-toggle value="list"><mat-icon>list</mat-icon> Lista</mat-button-toggle>
            <mat-button-toggle value="calendar"><mat-icon>calendar_month</mat-icon> Calendario</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        @if (view() === 'calendar') {
          <app-maintenance-calendar [filters]="calendarFilters()" (select)="open($event)" />
        } @else {
        <app-data-table
          caption="Mantenimientos"
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
          emptyIcon="build"
        >
          <ng-template appCell="number" let-row><span class="mono">{{ row.number }}</span></ng-template>
          <ng-template appCell="title" let-row>
            <div class="name-cell"><span class="strong">{{ row.title }}</span><span class="subtle">{{ row.assetCode }} · {{ row.assetName }}</span></div>
          </ng-template>
          <ng-template appCell="type" let-row><app-status-badge kind="MaintenanceType" [value]="row.type" /></ng-template>
          <ng-template appCell="date" let-row>
            <span [class.overdue]="row.isOverdue">{{ row.scheduledDate | appDate: 'short' }}</span>
          </ng-template>
          <ng-template appCell="status" let-row>
            <app-status-badge kind="MaintenanceStatus" [value]="row.status" />
            @if (row.isOverdue) { <app-status-badge label="Vencido" tone="danger" /> }
          </ng-template>
        </app-data-table>
        }
      </section>
    </div>
  `,
  styles: `
    .name-cell { display: flex; flex-direction: column; }
    .strong { font-weight: 500; }
    .view-toggle { margin-left: auto; }
    .overdue { color: var(--app-tone-danger-fg); font-weight: 600; white-space: nowrap; }
  `,
})
export class MaintenanceListComponent {
  private readonly operations = inject(OperationsService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly P = P;

  protected readonly types = enumOptions('MaintenanceType');
  protected readonly statuses = enumOptions('MaintenanceStatus');
  protected readonly list = pagedList<MaintenanceDto, MaintenanceFilters>((page, f) => this.operations.maintenances.list(page, f), {});

  protected readonly view = signal<'list' | 'calendar'>(readView());
  /** El calendario comparte filtros y busqueda con la lista; el rango de fechas lo pone el calendario. */
  protected readonly calendarFilters = computed(() => ({ ...this.list.filters(), search: this.list.search() || null }));

  protected readonly columns: TableColumn<MaintenanceDto>[] = [
    { key: 'number', header: 'Numero', sortKey: 'number', width: '150px' },
    { key: 'title', header: 'Mantenimiento' },
    { key: 'type', header: 'Tipo', width: '130px', hideOnMobile: true },
    { key: 'technician', header: 'Tecnico', value: (m) => m.technicianName ?? 'Sin asignar', hideOnMobile: true },
    { key: 'date', header: 'Programado', sortKey: 'plannedDate', width: '140px' },
    { key: 'status', header: 'Estado', sortKey: 'status', width: '200px' },
  ];

  open(row: MaintenanceDto): void {
    void this.router.navigate(['/mantenimientos', row.id]);
  }

  create(): void {
    this.dialogs
      .form({ title: 'Nuevo mantenimiento', fields: maintenanceFields(this.lookups, null), save: (value) => this.operations.maintenances.create(value as never) })
      .subscribe((created) => {
        this.toast.success(`Mantenimiento ${created.number} registrado.`);
        void this.router.navigate(['/mantenimientos', created.id]);
      });
  }

  setView(view: 'list' | 'calendar'): void {
    this.view.set(view);
    try {
      localStorage.setItem(VIEW_KEY, view);
    } catch {
      // Preferencia opcional: sin almacenamiento se usa la lista.
    }
  }
}

const VIEW_KEY = 'tiadmin.maintenance.view';

function readView(): 'list' | 'calendar' {
  try {
    return localStorage.getItem(VIEW_KEY) === 'calendar' ? 'calendar' : 'list';
  } catch {
    return 'list';
  }
}
