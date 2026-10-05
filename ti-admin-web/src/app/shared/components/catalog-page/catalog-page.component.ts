import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PermissionCode } from '@core/auth/permissions.generated';
import { QueryParams } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { FieldDef } from '@shared/components/form-dialog/form-dialog.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { DialogsService } from '@shared/services/dialogs.service';
import { ResourceApi } from '@shared/services/resource-api';
import { pagedList } from '@shared/utils/paged-list';
import { valueOf } from '@shared/utils/resource-utils';

export interface CatalogConfig<T extends { id: number }> {
  title: string;
  subtitle?: string;
  /** Singular en minusculas, para titulos y mensajes ("departamento"). */
  entity: string;
  resource: ResourceApi<T, unknown, unknown>;
  columns: TableColumn<T>[];
  fields: FieldDef[];
  managePermission: PermissionCode;
  /** El endpoint devuelve PagedResult (si no, una lista completa filtrada en cliente). */
  paged?: boolean;
  /** Filtro Activos/Inactivos/Todos via `isActive`. */
  activeFilter?: boolean;
  /** Columna con badge Activo/Inactivo segun `isActive`. */
  showActive?: boolean;
  canDelete?: boolean;
  /** DTO → valores del formulario de edicion (por defecto el DTO tal cual). */
  toForm?: (row: T) => Record<string, unknown>;
  /** Campos solo para editar (p. ej. isActive). */
  editOnlyFields?: FieldDef[];
  rowLink?: (row: T) => unknown[];
  afterChange?: () => void;
  searchPlaceholder?: string;
  /** Texto buscado en el cliente cuando la lista no es paginada. */
  clientSearch?: (row: T) => string;
}

/** Pagina estandar de catalogo: lista con busqueda y filtro, alta/edicion en dialogo y baja logica. */
@Component({
  selector: 'app-catalog-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatMenuModule,
    PageHeaderComponent,
    SearchBoxComponent,
    DataTableComponent,
    CellDefDirective,
    StatusBadgeComponent,
  ],
  template: `
    <div class="page">
      <app-page-header [title]="config().title" [subtitle]="config().subtitle">
        @if (canManage()) {
          <button mat-flat-button type="button" (click)="create()"><mat-icon>add</mat-icon>Nuevo {{ config().entity }}</button>
        }
      </app-page-header>

      <section class="panel">
        <div class="filters">
          <app-search-box [placeholder]="config().searchPlaceholder ?? 'Buscar'" (search)="onSearch($event)" />
          @if (config().activeFilter) {
            <mat-button-toggle-group [value]="activeValue()" (change)="setActive($event.value)" aria-label="Filtrar por estado">
              <mat-button-toggle value="true">Activos</mat-button-toggle>
              <mat-button-toggle value="false">Inactivos</mat-button-toggle>
              <mat-button-toggle value="all">Todos</mat-button-toggle>
            </mat-button-toggle-group>
          }
        </div>

        <app-data-table
          [columns]="columns()"
          [rows]="rows()"
          [loading]="loading()"
          [paginated]="!!config().paged"
          [total]="list.total()"
          [page]="list.page()"
          [pageSize]="list.pageSize()"
          [clickable]="!!config().rowLink"
          (pageChange)="list.onPage($event)"
          (sortChange)="list.onSort($event)"
          (rowClick)="open($event)"
          [caption]="config().title"
        >
          <ng-template appCell="isActive" let-row>
            <app-status-badge [label]="row.isActive ? 'Activo' : 'Inactivo'" [tone]="row.isActive ? 'success' : 'neutral'" />
          </ng-template>
          <ng-template appCell="actions" let-row>
            @if (canManage()) {
              <button mat-icon-button type="button" [matMenuTriggerFor]="menu" aria-label="Acciones" (click)="$event.stopPropagation()">
                <mat-icon>more_vert</mat-icon>
              </button>
              <mat-menu #menu="matMenu" xPosition="before">
                <button mat-menu-item type="button" (click)="edit(row)"><mat-icon>edit</mat-icon>Editar</button>
                @if (config().canDelete) {
                  <button mat-menu-item type="button" (click)="remove(row)"><mat-icon>delete</mat-icon>Eliminar</button>
                }
              </mat-menu>
            }
          </ng-template>
        </app-data-table>
      </section>
    </div>
  `,
})
export class CatalogPageComponent<T extends { id: number }> {
  readonly config = input.required<CatalogConfig<T>>();

  private readonly auth = inject(AuthService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly canManage = computed(() => this.auth.hasAny(this.config().managePermission));

  /** Listas paginadas en servidor. */
  protected readonly list = pagedList<T, QueryParams>(
    (page, filters) => (this.config().paged ? this.config().resource.list(page, filters) : of({ items: [], page: 1, pageSize: 0, totalItems: 0, totalPages: 0, hasPrevious: false, hasNext: false })),
    { isActive: true },
  );

  /** Listas completas (endpoints sin paginacion), filtradas en el cliente. */
  private readonly full = rxResource({
    params: () => ({ paged: this.config().paged, isActive: this.list.filters()['isActive'] }),
    stream: ({ params }): Observable<T[]> =>
      params.paged ? of([]) : this.config().resource.all(this.config().activeFilter ? { isActive: params.isActive as boolean | undefined } : undefined),
  });

  protected readonly activeValue = computed(() => {
    const value = this.list.filters()['isActive'];
    return value === undefined || value === null ? 'all' : String(value);
  });

  protected readonly rows = computed<readonly T[]>(() => {
    if (this.config().paged) {
      return this.list.items();
    }
    const term = this.list.search().toLowerCase();
    const items = valueOf(this.full) ?? [];
    const text = this.config().clientSearch;
    return term && text ? items.filter((row) => text(row).toLowerCase().includes(term)) : items;
  });

  protected readonly loading = computed(() => (this.config().paged ? this.list.loading() : this.full.isLoading()));

  protected readonly columns = computed<TableColumn<T>[]>(() => {
    const config = this.config();
    const columns = [...config.columns];
    if (config.showActive) columns.push({ key: 'isActive', header: 'Estado', width: '120px' });
    if (this.canManage()) columns.push({ key: 'actions', header: '', width: '56px', align: 'end' });
    return columns;
  });

  onSearch(term: string): void {
    this.list.setSearch(term);
  }

  setActive(value: string): void {
    this.list.patchFilters({ isActive: value === 'all' ? undefined : value === 'true' });
  }

  open(row: T): void {
    const link = this.config().rowLink?.(row);
    if (link) void this.router.navigate(link);
  }

  create(): void {
    const config = this.config();
    this.dialogs
      .form({ title: `Nuevo ${config.entity}`, fields: config.fields, save: (value) => config.resource.create(value) })
      .subscribe(() => this.changed(`${capitalize(config.entity)} creado.`));
  }

  edit(row: T): void {
    const config = this.config();
    this.dialogs
      .form({
        title: `Editar ${config.entity}`,
        fields: [...config.fields, ...(config.editOnlyFields ?? [])],
        value: config.toForm ? config.toForm(row) : (row as unknown as Record<string, unknown>),
        save: (value) => config.resource.update(row.id, value),
      })
      .subscribe(() => this.changed('Cambios guardados.'));
  }

  remove(row: T): void {
    const config = this.config();
    this.confirmService
      .confirm({ title: `Eliminar ${config.entity}`, message: `¿Desea eliminar este ${config.entity}? Esta accion no se puede deshacer desde la aplicacion.`, confirmText: 'Eliminar', destructive: true })
      .subscribe((ok) => {
        if (ok) config.resource.remove(row.id).subscribe(() => this.changed(`${capitalize(config.entity)} eliminado.`));
      });
  }

  private changed(message: string): void {
    this.toast.success(message);
    this.config().afterChange?.();
    if (this.config().paged) this.list.reload();
    else this.full.reload();
  }
}

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}
