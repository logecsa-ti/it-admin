import { Injectable, inject } from '@angular/core';
import { Observable, map, of, shareReplay } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P, PermissionCode } from '@core/auth/permissions.generated';
import {
  AssetListItemDto,
  AssetTypeDto,
  ContractDto,
  DepartmentDto,
  LocationDto,
  Paged,
  SoftwareDto,
  TicketCategoryDto,
  UserListItemDto,
  VendorDto,
} from '@core/models';

export interface Option {
  id: number;
  label: string;
  hint?: string | null;
}

const CATALOG_PAGE = { page: 1, pageSize: 200 };

/**
 * Catalogos para selects y busquedas de autocompletado. Los catalogos se cachean por sesion de pagina
 * (se invalidan al editarlos); sin el permiso de lectura devuelven una lista vacia en vez de un 403.
 */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly cache = new Map<string, Observable<Option[]>>();

  departments(): Observable<Option[]> {
    return this.cached('departments', P.OrganizationView, () =>
      this.api
        .getPaged<DepartmentDto>('departments', CATALOG_PAGE, { isActive: true })
        .pipe(map((page) => page.items.map((d) => ({ id: d.id, label: d.name, hint: d.code })))),
    );
  }

  locations(): Observable<Option[]> {
    return this.cached('locations', P.OrganizationView, () =>
      this.api
        .getPaged<LocationDto>('locations', CATALOG_PAGE, { isActive: true })
        .pipe(map((page) => page.items.map((l) => ({ id: l.id, label: l.name, hint: l.city })))),
    );
  }

  assetTypes(): Observable<Option[]> {
    return this.cached('asset-types', null, () =>
      this.api
        .get<AssetTypeDto[]>('asset-types', { isActive: true })
        .pipe(map((types) => types.map((t) => ({ id: t.id, label: t.name, hint: t.code })))),
    );
  }

  vendors(): Observable<Option[]> {
    return this.cached('vendors', P.VendorsView, () =>
      this.api
        .getPaged<VendorDto>('vendors', CATALOG_PAGE, { status: 'Active' })
        .pipe(map((page) => page.items.map((v) => ({ id: v.id, label: v.name, hint: v.code })))),
    );
  }

  software(): Observable<Option[]> {
    return this.cached('software', P.SoftwareView, () =>
      this.api
        .getPaged<SoftwareDto>('software', CATALOG_PAGE, { isActive: true })
        .pipe(
          map((page) =>
            page.items.map((s) => ({ id: s.id, label: s.version ? `${s.name} ${s.version}` : s.name, hint: s.publisher })),
          ),
        ),
    );
  }

  contracts(): Observable<Option[]> {
    return this.cached('contracts', P.ContractsView, () =>
      this.api
        .getPaged<ContractDto>('contracts', CATALOG_PAGE)
        .pipe(map((page) => page.items.map((c) => ({ id: c.id, label: `${c.number} · ${c.name}`, hint: c.vendorName })))),
    );
  }

  ticketCategories(): Observable<TicketCategoryDto[]> {
    return this.api.get<TicketCategoryDto[]>('ticket-categories', { isActive: true });
  }

  /** Busqueda de usuarios activos (requiere USERS.VIEW). */
  readonly searchUsers = (term: string): Observable<Option[]> => {
    if (!this.auth.hasAny(P.UsersView)) {
      return of([]);
    }
    return this.api
      .getPaged<UserListItemDto>('users', { page: 1, pageSize: 15, search: term || null }, { isActive: true })
      .pipe(map((page: Paged<UserListItemDto>) => page.items.map((u) => ({ id: u.id, label: u.fullName, hint: u.email }))));
  };

  /** Busqueda de activos por codigo, serie o nombre (requiere ASSETS.VIEW). */
  readonly searchAssets = (term: string): Observable<Option[]> => {
    if (!this.auth.hasAny(P.AssetsView)) {
      return of([]);
    }
    return this.api
      .getPaged<AssetListItemDto>('assets', { page: 1, pageSize: 15, search: term || null })
      .pipe(map((page) => page.items.map((a) => ({ id: a.id, label: `${a.assetCode} · ${a.name}`, hint: a.assetTypeName }))));
  };

  invalidate(catalog: string): void {
    this.cache.delete(catalog);
  }

  private cached(key: string, permission: PermissionCode | null, load: () => Observable<Option[]>): Observable<Option[]> {
    if (permission && !this.auth.hasAny(permission)) {
      return of([]);
    }
    let entry = this.cache.get(key);
    if (!entry) {
      entry = load().pipe(shareReplay({ bufferSize: 1, refCount: false }));
      this.cache.set(key, entry);
    }
    return entry;
  }
}
