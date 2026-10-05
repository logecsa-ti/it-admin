import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  TemplateRef,
  computed,
  contentChildren,
  inject,
  input,
  output,
} from '@angular/core';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

export interface TableColumn<T> {
  key: string;
  header: string;
  /** Texto de la celda cuando no hay plantilla `appCell`. */
  value?: (row: T) => string | number | null | undefined;
  /** Clave de orden aceptada por la API (SortBy); sin ella la columna no se ordena. */
  sortKey?: string;
  align?: 'start' | 'end' | 'center';
  width?: string;
  /** Oculta la columna en pantallas angostas. */
  hideOnMobile?: boolean;
}

/** Plantilla de celda: `<ng-template appCell="status" let-row>...</ng-template>`. */
@Directive({ selector: 'ng-template[appCell]' })
export class CellDefDirective {
  readonly appCell = input.required<string>();
  readonly template = inject(TemplateRef<{ $implicit: unknown }>);
}

/**
 * Tabla de datos (DataTable, SPECS.md 30) para listas paginadas en servidor.
 * Emite cambios de pagina/orden; la pagina contenedora decide que consultar (ver PagedList).
 */
@Component({
  selector: 'app-data-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatProgressBarModule,
    NgTemplateOutlet,
    EmptyStateComponent,
  ],
  template: `
    <div class="progress">
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando" />
      }
    </div>
    <div class="table-scroll">
      <table
        mat-table
        [dataSource]="rows()"
        matSort
        [matSortActive]="sortActive() ?? ''"
        [matSortDirection]="sortDirection()"
        (matSortChange)="sortChange.emit($event)"
        [attr.aria-label]="caption()"
        [attr.aria-busy]="loading()"
      >
        @for (column of columns(); track column.key) {
          <ng-container [matColumnDef]="column.key">
            @if (column.sortKey) {
              <th
                mat-header-cell
                *matHeaderCellDef
                [mat-sort-header]="column.sortKey"
                [style.width]="column.width"
                [class]="'align-' + (column.align ?? 'start')"
                [class.hide-mobile]="column.hideOnMobile"
              >
                {{ column.header }}
              </th>
            } @else {
              <th
                mat-header-cell
                *matHeaderCellDef
                [style.width]="column.width"
                [class]="'align-' + (column.align ?? 'start')"
                [class.hide-mobile]="column.hideOnMobile"
              >
                {{ column.header }}
              </th>
            }
            <td
              mat-cell
              *matCellDef="let row"
              [class]="'align-' + (column.align ?? 'start')"
              [class.hide-mobile]="column.hideOnMobile"
            >
              @if (templates()[column.key]; as template) {
                <ng-container *ngTemplateOutlet="template; context: { $implicit: row }" />
              } @else {
                {{ column.value ? (column.value(row) ?? '—') : '' }}
              }
            </td>
          </ng-container>
        }

        <tr mat-header-row *matHeaderRowDef="columnKeys()"></tr>
        <tr
          mat-row
          *matRowDef="let row; columns: columnKeys()"
          [class.is-clickable]="clickable()"
          [attr.tabindex]="clickable() ? 0 : null"
          (click)="clickable() && rowClick.emit(row)"
          (keydown.enter)="clickable() && rowClick.emit(row)"
        ></tr>
      </table>
    </div>

    @if (!loading() && rows().length === 0) {
      <app-empty-state [icon]="emptyIcon()" [title]="emptyTitle()" [message]="emptyMessage()" />
    }

    @if (paginated()) {
      <mat-paginator
        [length]="total()"
        [pageIndex]="page() - 1"
        [pageSize]="pageSize()"
        [pageSizeOptions]="[10, 25, 50, 100]"
        [showFirstLastButtons]="true"
        (page)="pageChange.emit($event)"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .progress {
      height: 4px;
    }
    table {
      width: 100%;
    }
    .align-end {
      text-align: end;
    }
    .align-center {
      text-align: center;
    }
    mat-paginator {
      border-top: 1px solid var(--app-border-soft);
      background: transparent;
    }
    @media (max-width: 720px) {
      .hide-mobile {
        display: none;
      }
    }
  `,
})
export class DataTableComponent<T> {
  readonly columns = input.required<TableColumn<T>[]>();
  readonly rows = input.required<readonly T[]>();
  readonly loading = input(false);
  readonly caption = input<string>('Tabla de datos');
  readonly clickable = input(false);

  readonly paginated = input(true);
  readonly total = input(0);
  readonly page = input(1);
  readonly pageSize = input(25);
  readonly sortActive = input<string | null>(null);
  readonly sortDirection = input<'asc' | 'desc' | ''>('');

  readonly emptyIcon = input('inbox');
  readonly emptyTitle = input('Sin resultados');
  readonly emptyMessage = input<string | null>('No hay registros que coincidan con los filtros.');

  readonly pageChange = output<PageEvent>();
  readonly sortChange = output<Sort>();
  readonly rowClick = output<T>();

  private readonly cellDefs = contentChildren(CellDefDirective);

  protected readonly columnKeys = computed(() => this.columns().map((column) => column.key));
  protected readonly templates = computed(() => {
    const map: Record<string, TemplateRef<{ $implicit: unknown }>> = {};
    for (const def of this.cellDefs()) {
      map[def.appCell()] = def.template;
    }
    return map;
  });
}
