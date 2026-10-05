import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { ContractDto, QueryParams } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumLabel, enumOptions } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { pagedList } from '@shared/utils/paged-list';
import { contractFields } from '../licensing-forms';
import { LicensingService } from '../licensing.service';

interface ContractFilters extends QueryParams {
  vendorId?: number | null;
  type?: string | null;
  status?: string | null;
}

@Component({
  selector: 'app-contract-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AsyncPipe,
    MatButtonModule,
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
    MoneyPipe,
  ],
  template: `
    <div class="page">
      <app-page-header title="Contratos" subtitle="Contratos con proveedores, vigencias y renovaciones.">
        <button mat-flat-button type="button" *appCan="P.ContractsManage" (click)="create()"><mat-icon>add</mat-icon>Nuevo contrato</button>
      </app-page-header>
      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Numero o nombre" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Proveedor</mat-label>
            <mat-select [value]="list.filters().vendorId ?? null" (selectionChange)="list.patchFilters({ vendorId: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (v of vendors$ | async; track v.id) { <mat-option [value]="v.id">{{ v.label }}</mat-option> }
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
            <mat-label>Estado</mat-label>
            <mat-select [value]="list.filters().status ?? null" (selectionChange)="list.patchFilters({ status: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of statuses; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <app-data-table
          caption="Contratos"
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
          emptyIcon="contract"
        >
          <ng-template appCell="contract" let-row>
            <div class="name-cell"><span class="strong">{{ row.name }}</span><span class="subtle mono">{{ row.number }}</span></div>
          </ng-template>
          <ng-template appCell="period" let-row>{{ row.startDate | appDate: 'date' }} – {{ row.endDate | appDate: 'date' }}</ng-template>
          <ng-template appCell="remaining" let-row>
            @if (row.status === 'Active' || row.status === 'Expiring') {
              <span [class.warn]="row.daysRemaining <= row.renewalNoticeDays">{{ row.daysRemaining }} dias</span>
            } @else { — }
          </ng-template>
          <ng-template appCell="value" let-row>{{ row.value | money: row.currency }}</ng-template>
          <ng-template appCell="status" let-row><app-status-badge kind="ContractStatus" [value]="row.status" /></ng-template>
        </app-data-table>
      </section>
    </div>
  `,
  styles: `
    .name-cell { display: flex; flex-direction: column; }
    .strong { font-weight: 500; }
    .warn { color: var(--app-tone-warning-fg); font-weight: 600; }
  `,
})
export class ContractListComponent {
  private readonly licensing = inject(LicensingService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly P = P;

  protected readonly vendors$ = this.lookups.vendors();
  protected readonly types = enumOptions('ContractType');
  protected readonly statuses = enumOptions('ContractStatus');
  protected readonly list = pagedList<ContractDto, ContractFilters>((page, f) => this.licensing.contracts.list(page, f), {});

  protected readonly columns: TableColumn<ContractDto>[] = [
    { key: 'contract', header: 'Contrato', sortKey: 'name' },
    { key: 'vendor', header: 'Proveedor', value: (c) => c.vendorName },
    { key: 'type', header: 'Tipo', value: (c) => enumLabel('ContractType', c.type), hideOnMobile: true },
    { key: 'period', header: 'Vigencia', sortKey: 'startDate', hideOnMobile: true },
    { key: 'remaining', header: 'Restan', width: '100px' },
    { key: 'value', header: 'Valor', align: 'end', hideOnMobile: true },
    { key: 'status', header: 'Estado', width: '120px' },
  ];

  open(contract: ContractDto): void {
    void this.router.navigate(['/contratos', contract.id]);
  }

  create(): void {
    this.dialogs
      .form({ title: 'Nuevo contrato', fields: contractFields(this.lookups, null), save: (value) => this.licensing.contracts.create(value as never) })
      .subscribe((contract) => {
        this.toast.success('Contrato creado.');
        this.lookups.invalidate('contracts');
        void this.router.navigate(['/contratos', contract.id]);
      });
  }
}
