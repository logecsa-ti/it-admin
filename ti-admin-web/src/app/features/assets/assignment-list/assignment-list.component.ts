import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { Router } from '@angular/router';
import { AssetAssignmentDto } from '@core/models';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { pagedList } from '@shared/utils/paged-list';
import { AssetsService } from '../assets.service';

/** Historial global de asignaciones (nunca se reescribe: cada devolucion cierra la fila). */
@Component({
  selector: 'app-assignment-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonToggleModule, PageHeaderComponent, SearchBoxComponent, DataTableComponent, CellDefDirective, StatusBadgeComponent, AppDatePipe],
  template: `
    <div class="page">
      <app-page-header title="Asignaciones" subtitle="Entregas y devoluciones de activos a usuarios." />
      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Activo o usuario" (search)="list.setSearch($event)" />
          <mat-button-toggle-group [value]="list.filters().activeOnly ? 'active' : 'all'" (change)="list.patchFilters({ activeOnly: $event.value === 'active' || null })" aria-label="Filtrar asignaciones">
            <mat-button-toggle value="active">Vigentes</mat-button-toggle>
            <mat-button-toggle value="all">Todas</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        <app-data-table
          caption="Asignaciones"
          [columns]="columns"
          [rows]="list.items()"
          [loading]="list.loading()"
          [total]="list.total()"
          [page]="list.page()"
          [pageSize]="list.pageSize()"
          [clickable]="true"
          (pageChange)="list.onPage($event)"
          (rowClick)="open($event)"
          emptyIcon="assignment_ind"
        >
          <ng-template appCell="asset" let-row>
            <span class="mono">{{ row.assetCode }}</span> · {{ row.assetName }}
          </ng-template>
          <ng-template appCell="from" let-row>{{ row.assignmentDate | appDate }}</ng-template>
          <ng-template appCell="state" let-row>
            @if (row.isActive) {
              <app-status-badge label="Vigente" tone="info" />
            } @else {
              <span class="subtle">Devuelto {{ row.returnDate | appDate: 'date' }}</span>
            }
          </ng-template>
        </app-data-table>
      </section>
    </div>
  `,
})
export class AssignmentListComponent {
  private readonly assets = inject(AssetsService);
  private readonly router = inject(Router);

  protected readonly list = pagedList<AssetAssignmentDto, { activeOnly: boolean | null }>(
    (page, filters) => this.assets.allAssignments(page, filters),
    { activeOnly: true },
  );

  protected readonly columns: TableColumn<AssetAssignmentDto>[] = [
    { key: 'asset', header: 'Activo' },
    { key: 'user', header: 'Usuario', value: (a) => a.userName },
    { key: 'from', header: 'Asignado', width: '170px' },
    { key: 'by', header: 'Asigno', value: (a) => a.assignedByName, hideOnMobile: true },
    { key: 'state', header: 'Estado', width: '190px' },
  ];

  open(row: AssetAssignmentDto): void {
    void this.router.navigate(['/activos', row.assetId]);
  }
}
