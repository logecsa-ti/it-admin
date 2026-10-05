import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { ChangeRequestDto, QueryParams } from '@core/models';
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
import { changeFields } from '../operations-forms';
import { OperationsService } from '../operations.service';

interface ChangeFilters extends QueryParams {
  type?: string | null;
  status?: string | null;
  risk?: string | null;
  assignedToMe?: boolean | null;
}

@Component({
  selector: 'app-change-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
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
  ],
  template: `
    <div class="page">
      <app-page-header title="Gestion de cambios" subtitle="Solicitudes de cambio con revision, aprobacion e implementacion controlada.">
        <button mat-flat-button type="button" *appCan="P.ChangesCreate" (click)="create()"><mat-icon>add</mat-icon>Nuevo cambio</button>
      </app-page-header>
      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Numero o titulo" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Estado</mat-label>
            <mat-select [value]="list.filters().status ?? null" (selectionChange)="list.patchFilters({ status: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of statuses; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Tipo</mat-label>
            <mat-select [value]="list.filters().type ?? null" (selectionChange)="list.patchFilters({ type: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of types; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Riesgo</mat-label>
            <mat-select [value]="list.filters().risk ?? null" (selectionChange)="list.patchFilters({ risk: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of risks; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-checkbox [checked]="!!list.filters().assignedToMe" (change)="list.patchFilters({ assignedToMe: $event.checked || null })">Asignados a mi</mat-checkbox>
        </div>
        <app-data-table
          caption="Cambios"
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
          emptyIcon="published_with_changes"
        >
          <ng-template appCell="number" let-row><span class="mono">{{ row.number }}</span></ng-template>
          <ng-template appCell="type" let-row><app-status-badge kind="ChangeType" [value]="row.type" /></ng-template>
          <ng-template appCell="risk" let-row><app-status-badge kind="ChangeRisk" [value]="row.risk" /></ng-template>
          <ng-template appCell="planned" let-row>{{ row.plannedDate | appDate: 'short' }}</ng-template>
          <ng-template appCell="status" let-row><app-status-badge kind="ChangeStatus" [value]="row.status" /></ng-template>
        </app-data-table>
      </section>
    </div>
  `,
})
export class ChangeListComponent {
  private readonly operations = inject(OperationsService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly P = P;

  protected readonly statuses = enumOptions('ChangeStatus');
  protected readonly types = enumOptions('ChangeType');
  protected readonly risks = enumOptions('ChangeRisk');
  protected readonly list = pagedList<ChangeRequestDto, ChangeFilters>((page, f) => this.operations.changes.list(page, f), {});

  protected readonly columns: TableColumn<ChangeRequestDto>[] = [
    { key: 'number', header: 'Numero', sortKey: 'number', width: '150px' },
    { key: 'title', header: 'Titulo', value: (c) => c.title },
    { key: 'type', header: 'Tipo', width: '120px', hideOnMobile: true },
    { key: 'risk', header: 'Riesgo', sortKey: 'risk', width: '100px' },
    { key: 'requester', header: 'Solicitante', value: (c) => c.requestedByName, hideOnMobile: true },
    { key: 'planned', header: 'Planificado', sortKey: 'plannedDate', width: '140px', hideOnMobile: true },
    { key: 'status', header: 'Estado', sortKey: 'status', width: '150px' },
  ];

  open(row: ChangeRequestDto): void {
    void this.router.navigate(['/cambios', row.id]);
  }

  create(): void {
    this.dialogs
      .form({ title: 'Nueva solicitud de cambio', fields: changeFields(this.lookups, null), save: (value) => this.operations.changes.create(value as never) })
      .subscribe((created) => {
        this.toast.success(`Cambio ${created.number} creado como borrador.`);
        void this.router.navigate(['/cambios', created.id]);
      });
  }
}
