import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { AssetListItemDto } from '@core/models';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { pagedList } from '@shared/utils/paged-list';
import { AssetsService } from '../assets.service';

/** Activos asignados al usuario actual (el rol USER solo ve los suyos, Q-10). */
@Component({
  selector: 'app-my-assets',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule, PageHeaderComponent, DataTableComponent, CellDefDirective, StatusBadgeComponent, AppDatePipe],
  template: `
    <div class="page">
      <app-page-header title="Mis activos" subtitle="Equipos asignados a su nombre. Si alguno falla, reporte un ticket.">
        <a mat-flat-button routerLink="/tickets/nuevo"><mat-icon>support_agent</mat-icon>Reportar un problema</a>
      </app-page-header>
      <section class="panel">
        <app-data-table
          caption="Mis activos"
          [columns]="columns"
          [rows]="list.items()"
          [loading]="list.loading()"
          [total]="list.total()"
          [page]="list.page()"
          [pageSize]="list.pageSize()"
          (pageChange)="list.onPage($event)"
          emptyIcon="devices"
          emptyTitle="No tiene activos asignados"
          emptyMessage="Cuando TI le entregue un equipo aparecera aqui."
        >
          <ng-template appCell="code" let-row><span class="mono">{{ row.assetCode }}</span></ng-template>
          <ng-template appCell="status" let-row><app-status-badge kind="AssetStatus" [value]="row.status" /></ng-template>
          <ng-template appCell="warranty" let-row>{{ row.warrantyExpiration | appDate: 'date' }}</ng-template>
        </app-data-table>
      </section>
    </div>
  `,
})
export class MyAssetsComponent {
  private readonly assets = inject(AssetsService);

  protected readonly list = pagedList<AssetListItemDto, Record<string, never>>((page) => this.assets.mine(page), {});

  protected readonly columns: TableColumn<AssetListItemDto>[] = [
    { key: 'code', header: 'Codigo', width: '140px' },
    { key: 'name', header: 'Activo', value: (a) => a.name },
    { key: 'type', header: 'Tipo', value: (a) => a.assetTypeName },
    { key: 'model', header: 'Marca / modelo', value: (a) => [a.brand, a.model].filter(Boolean).join(' ') || '—', hideOnMobile: true },
    { key: 'status', header: 'Estado', width: '150px' },
    { key: 'warranty', header: 'Garantia', width: '130px', hideOnMobile: true },
  ];
}
