import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { map } from 'rxjs';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { AssetTypeDto, DepartmentDto, LocationDto } from '@core/models';
import { CatalogConfig, CatalogPageComponent } from '@shared/components/catalog-page/catalog-page.component';
import { LookupService } from '@shared/services/lookup.service';
import { OrganizationService } from './organization.service';

const ACTIVE_FIELD = { key: 'isActive', label: 'Activo', type: 'checkbox' as const };

@Component({
  selector: 'app-departments-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CatalogPageComponent],
  template: `<app-catalog-page [config]="config" />`,
})
export class DepartmentsPage {
  private readonly lookups = inject(LookupService);
  private readonly organization = inject(OrganizationService);

  protected readonly config: CatalogConfig<DepartmentDto> = {
    title: 'Departamentos',
    subtitle: 'Estructura organizacional para usuarios, activos y costos.',
    entity: 'departamento',
    resource: this.organization.departments,
    paged: true,
    activeFilter: true,
    showActive: true,
    canDelete: true,
    managePermission: P.OrganizationManage,
    searchPlaceholder: 'Buscar por codigo o nombre',
    columns: [
      { key: 'code', header: 'Codigo', value: (d) => d.code, sortKey: 'code', width: '140px' },
      { key: 'name', header: 'Nombre', value: (d) => d.name },
      { key: 'description', header: 'Descripcion', value: (d) => d.description, hideOnMobile: true },
    ],
    fields: [
      { key: 'code', label: 'Codigo', required: true, maxLength: 20 },
      { key: 'name', label: 'Nombre', required: true, maxLength: 150 },
      {
        key: 'parentId',
        label: 'Departamento superior',
        type: 'select',
        emptyOption: 'Ninguno',
        options: this.lookups.departments().pipe(map((items) => items.map((d) => ({ value: d.id, label: d.label })))),
      },
      { key: 'managerId', label: 'Responsable', type: 'picker', search: this.lookups.searchUsers, hint: 'Opcional' },
      { key: 'description', label: 'Descripcion', type: 'textarea', maxLength: 500 },
    ],
    editOnlyFields: [ACTIVE_FIELD],
    afterChange: () => this.lookups.invalidate('departments'),
  };
}

@Component({
  selector: 'app-locations-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CatalogPageComponent],
  template: `<app-catalog-page [config]="config" />`,
})
export class LocationsPage {
  private readonly lookups = inject(LookupService);

  protected readonly config: CatalogConfig<LocationDto> = {
    title: 'Ubicaciones',
    subtitle: 'Sedes, edificios y oficinas donde se encuentran los activos.',
    entity: 'ubicacion',
    resource: inject(OrganizationService).locations,
    paged: true,
    activeFilter: true,
    showActive: true,
    canDelete: true,
    managePermission: P.OrganizationManage,
    searchPlaceholder: 'Buscar ubicacion',
    columns: [
      { key: 'code', header: 'Codigo', value: (l) => l.code, sortKey: 'code', width: '140px' },
      { key: 'name', header: 'Nombre', value: (l) => l.name },
      { key: 'city', header: 'Ciudad', value: (l) => l.city, sortKey: 'city' },
      { key: 'country', header: 'Pais', value: (l) => l.country, hideOnMobile: true },
    ],
    fields: [
      { key: 'code', label: 'Codigo', required: true, maxLength: 20 },
      { key: 'name', label: 'Nombre', required: true, maxLength: 150 },
      { key: 'address', label: 'Direccion', maxLength: 300, wide: true },
      { key: 'city', label: 'Ciudad', maxLength: 100 },
      { key: 'country', label: 'Pais', maxLength: 100 },
    ],
    editOnlyFields: [ACTIVE_FIELD],
    afterChange: () => this.lookups.invalidate('locations'),
  };
}

@Component({
  selector: 'app-asset-types-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CatalogPageComponent],
  template: `<app-catalog-page [config]="config" />`,
})
export class AssetTypesPage {
  private readonly lookups = inject(LookupService);

  protected readonly config: CatalogConfig<AssetTypeDto> = {
    title: 'Tipos de activo',
    subtitle: 'Clasificacion del inventario (laptop, monitor, servidor...).',
    entity: 'tipo de activo',
    resource: inject(OrganizationService).assetTypes,
    activeFilter: true,
    showActive: true,
    managePermission: P.AssetTypesManage,
    searchPlaceholder: 'Buscar tipo',
    clientSearch: (t) => `${t.code} ${t.name}`,
    columns: [
      { key: 'code', header: 'Codigo', value: (t) => t.code, width: '160px' },
      { key: 'name', header: 'Nombre', value: (t) => t.name },
      { key: 'description', header: 'Descripcion', value: (t) => t.description, hideOnMobile: true },
    ],
    fields: [
      { key: 'code', label: 'Codigo', required: true, maxLength: 30, lockedOnEdit: true, hint: 'Se usa en la importacion de activos' },
      { key: 'name', label: 'Nombre', required: true, maxLength: 100 },
      { key: 'description', label: 'Descripcion', type: 'textarea', maxLength: 500 },
    ],
    editOnlyFields: [ACTIVE_FIELD],
    afterChange: () => this.lookups.invalidate('asset-types'),
  };
}
