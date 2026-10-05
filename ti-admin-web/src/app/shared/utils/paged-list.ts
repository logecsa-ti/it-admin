import { DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Sort } from '@angular/material/sort';
import { PageEvent } from '@angular/material/paginator';
import { Observable, catchError, debounceTime, of, switchMap, tap } from 'rxjs';
import { PageRequest, Paged, QueryParams } from '@core/models';

export const DEFAULT_PAGE_SIZE = 25;

/**
 * Estado de una lista paginada en servidor: pagina, orden, busqueda y filtros como signals;
 * cada cambio vuelve a consultar (cancelando la peticion anterior). Se crea en un contexto de inyeccion:
 * `readonly list = pagedList((page, filters) => this.assets.list(page, filters), { status: null });`
 */
export class PagedList<T, F extends QueryParams> {
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly search = signal('');
  readonly sort = signal<{ by: string | null; direction: 'Ascending' | 'Descending' }>({
    by: null,
    direction: 'Ascending',
  });
  readonly filters: ReturnType<typeof signal<F>>;
  readonly result = signal<Paged<T> | null>(null);
  readonly loading = signal(true);
  readonly failed = signal(false);
  private readonly version = signal(0);

  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.totalItems ?? 0);
  readonly isEmpty = computed(() => !this.loading() && this.items().length === 0);

  constructor(
    loader: (page: PageRequest, filters: F) => Observable<Paged<T>>,
    initialFilters: F,
    destroyRef: DestroyRef,
  ) {
    this.filters = signal(initialFilters);

    const request = computed(() => ({
      page: {
        page: this.page(),
        pageSize: this.pageSize(),
        search: this.search() || null,
        sortBy: this.sort().by,
        sortDirection: this.sort().direction,
      } satisfies PageRequest,
      filters: this.filters(),
      version: this.version(),
    }));

    toObservable(request)
      .pipe(
        debounceTime(0),
        tap(() => this.loading.set(true)),
        switchMap(({ page, filters }) =>
          loader(page, filters).pipe(
            tap(() => this.failed.set(false)),
            catchError(() => {
              this.failed.set(true);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((result) => {
        if (result) {
          this.result.set(result);
        }
        this.loading.set(false);
      });
  }

  reload(): void {
    this.version.update((v) => v + 1);
  }

  setSearch(value: string): void {
    this.search.set(value.trim());
    this.page.set(1);
  }

  /** Cambia filtros y vuelve a la primera pagina. */
  patchFilters(patch: Partial<F>): void {
    this.filters.update((current) => ({ ...current, ...patch }));
    this.page.set(1);
  }

  onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.page.set(event.pageIndex + 1);
  }

  onSort(event: Sort): void {
    this.sort.set({
      by: event.direction ? event.active : null,
      direction: event.direction === 'desc' ? 'Descending' : 'Ascending',
    });
    this.page.set(1);
  }
}

export function pagedList<T, F extends QueryParams>(
  loader: (page: PageRequest, filters: F) => Observable<Paged<T>>,
  initialFilters: F,
): PagedList<T, F> {
  return new PagedList(loader, initialFilters, inject(DestroyRef));
}

/** Pagina vacia (p. ej. mientras falta el id de la ruta). */
export function emptyPage<T>(): Paged<T> {
  return { items: [], page: 1, pageSize: DEFAULT_PAGE_SIZE, totalItems: 0, totalPages: 0, hasPrevious: false, hasNext: false };
}
