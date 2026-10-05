import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { AssetListItemDto } from '@core/models';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumOptions } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { ExportService } from '@shared/services/export.service';
import { LookupService } from '@shared/services/lookup.service';
import { pagedList } from '@shared/utils/paged-list';
import { AssetFilters, AssetsService } from '../assets.service';
import { AssetImportDialogComponent } from '../asset-import-dialog/asset-import-dialog.component';

@Component({
  selector: 'app-asset-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AsyncPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatFormFieldModule,
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
      <app-page-header title="Activos" subtitle="Inventario de equipos y recursos tecnologicos.">
        <button mat-stroked-button type="button" [matMenuTriggerFor]="exportMenu" *appCan="P.ReportsExport" [disabled]="exporter.busy()">
          <mat-icon>download</mat-icon>Exportar
        </button>
        <mat-menu #exportMenu="matMenu">
          <button mat-menu-item type="button" (click)="export('xlsx')">Excel (.xlsx)</button>
          <button mat-menu-item type="button" (click)="export('csv')">CSV</button>
        </mat-menu>
        <button mat-stroked-button type="button" *appCan="P.AssetsCreate" (click)="openImport()">
          <mat-icon>upload</mat-icon>Importar
        </button>
        <a mat-flat-button routerLink="nuevo" *appCan="P.AssetsCreate"><mat-icon>add</mat-icon>Nuevo activo</a>
      </app-page-header>

      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Codigo, serie o nombre" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Estado</mat-label>
            <mat-select [value]="list.filters().status ?? null" (selectionChange)="list.patchFilters({ status: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (option of statuses; track option.value) {
                <mat-option [value]="option.value">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Tipo</mat-label>
            <mat-select [value]="list.filters().assetTypeId ?? null" (selectionChange)="list.patchFilters({ assetTypeId: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (option of types$ | async; track option.id) {
                <mat-option [value]="option.id">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Departamento</mat-label>
            <mat-select [value]="list.filters().departmentId ?? null" (selectionChange)="list.patchFilters({ departmentId: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (option of departments$ | async; track option.id) {
                <mat-option [value]="option.id">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Ubicacion</mat-label>
            <mat-select [value]="list.filters().locationId ?? null" (selectionChange)="list.patchFilters({ locationId: $event.value })">
              <mat-option [value]="null">Todas</mat-option>
              @for (option of locations$ | async; track option.id) {
                <mat-option [value]="option.id">{{ option.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <app-data-table
          caption="Activos"
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
          emptyIcon="inventory_2"
        >
          <ng-template appCell="code" let-row>
            <span class="mono strong">{{ row.assetCode }}</span>
          </ng-template>
          <ng-template appCell="name" let-row>
            <div class="name-cell">
              <span>{{ row.name }}</span>
              <span class="subtle">{{ row.brand }} {{ row.model }}</span>
            </div>
          </ng-template>
          <ng-template appCell="status" let-row>
            <app-status-badge kind="AssetStatus" [value]="row.status" />
          </ng-template>
          <ng-template appCell="warranty" let-row>
            {{ row.warrantyExpiration | appDate: 'date' }}
          </ng-template>
        </app-data-table>
      </section>
    </div>
  `,
  styles: `
    .strong { font-weight: 600; }
    .name-cell { display: flex; flex-direction: column; }
  `,
})
export class AssetListComponent {
  private readonly assets = inject(AssetsService);
  private readonly lookups = inject(LookupService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  protected readonly exporter = inject(ExportService);
  protected readonly P = P;

  protected readonly statuses = enumOptions('AssetStatus');
  protected readonly types$ = this.lookups.assetTypes();
  protected readonly departments$ = this.lookups.departments();
  protected readonly locations$ = this.lookups.locations();

  protected readonly list = pagedList<AssetListItemDto, AssetFilters>(
    (page, filters) => this.assets.list(page, filters),
    { status: null, assetTypeId: null, departmentId: null, locationId: null },
  );

  protected readonly columns: TableColumn<AssetListItemDto>[] = [
    { key: 'code', header: 'Codigo', width: '130px' },
    { key: 'name', header: 'Activo', sortKey: 'name' },
    { key: 'type', header: 'Tipo', value: (a) => a.assetTypeName, hideOnMobile: true },
    { key: 'status', header: 'Estado', sortKey: 'status', width: '150px' },
    { key: 'user', header: 'Asignado a', value: (a) => a.currentUserName ?? '—' },
    { key: 'department', header: 'Departamento', value: (a) => a.departmentName ?? '—', hideOnMobile: true },
    { key: 'location', header: 'Ubicacion', value: (a) => a.locationName ?? '—', hideOnMobile: true },
    { key: 'warranty', header: 'Garantia', sortKey: 'warrantyExpiration', width: '130px', hideOnMobile: true },
  ];

  open(asset: AssetListItemDto): void {
    void this.router.navigate(['/activos', asset.id]);
  }

  export(format: 'xlsx' | 'csv'): void {
    this.exporter.export('assets', format, { ...this.list.filters(), search: this.list.search() || null });
  }

  openImport(): void {
    this.dialog
      .open(AssetImportDialogComponent, { width: '720px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((imported: boolean | undefined) => imported && this.list.reload());
  }
}
