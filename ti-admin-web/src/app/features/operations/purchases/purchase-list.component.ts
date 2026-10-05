import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { PurchaseRequestDto, QueryParams } from '@core/models';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumOptions } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { pagedList } from '@shared/utils/paged-list';
import { OperationsService } from '../operations.service';

@Component({
  selector: 'app-purchase-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
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
      <app-page-header title="Compras" subtitle="Solicitudes de compra de equipos, licencias y servicios.">
        <a mat-flat-button routerLink="nueva" *appCan="P.PurchasesCreate"><mat-icon>add</mat-icon>Nueva solicitud</a>
      </app-page-header>
      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Numero o titulo" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Estado</mat-label>
            <mat-select [value]="list.filters()['status'] ?? null" (selectionChange)="list.patchFilters({ status: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (o of statuses; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
        </div>
        <app-data-table
          caption="Compras"
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
          emptyIcon="shopping_cart"
        >
          <ng-template appCell="number" let-row><span class="mono">{{ row.number }}</span></ng-template>
          <ng-template appCell="cost" let-row>{{ row.estimatedCost | money }}</ng-template>
          <ng-template appCell="needed" let-row>{{ row.neededDate | appDate: 'date' }}</ng-template>
          <ng-template appCell="status" let-row><app-status-badge kind="PurchaseStatus" [value]="row.status" /></ng-template>
        </app-data-table>
      </section>
    </div>
  `,
})
export class PurchaseListComponent {
  private readonly operations = inject(OperationsService);
  private readonly router = inject(Router);
  protected readonly P = P;

  protected readonly statuses = enumOptions('PurchaseStatus');
  protected readonly list = pagedList<PurchaseRequestDto, QueryParams>((page, f) => this.operations.purchases.list(page, f), {});

  protected readonly columns: TableColumn<PurchaseRequestDto>[] = [
    { key: 'number', header: 'Numero', sortKey: 'number', width: '150px' },
    { key: 'title', header: 'Titulo', value: (p) => p.title },
    { key: 'requester', header: 'Solicitante', value: (p) => p.requestedByName, hideOnMobile: true },
    { key: 'cost', header: 'Monto estimado', sortKey: 'estimatedCost', align: 'end', width: '150px' },
    { key: 'needed', header: 'Requerida', sortKey: 'neededDate', width: '130px', hideOnMobile: true },
    { key: 'status', header: 'Estado', sortKey: 'status', width: '130px' },
  ];

  open(row: PurchaseRequestDto): void {
    void this.router.navigate(['/compras', row.id]);
  }
}
