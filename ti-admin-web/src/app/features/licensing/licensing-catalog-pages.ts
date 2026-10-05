import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { SoftwareDto, VendorDto } from '@core/models';
import { CatalogConfig, CatalogPageComponent } from '@shared/components/catalog-page/catalog-page.component';
import { enumLabel, enumOptions } from '@shared/labels/enum-labels';
import { LookupService } from '@shared/services/lookup.service';
import { LicensingService } from './licensing.service';

@Component({
  selector: 'app-software-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CatalogPageComponent],
  template: `<app-catalog-page [config]="config" />`,
})
export class SoftwarePage {
  private readonly lookups = inject(LookupService);

  protected readonly config: CatalogConfig<SoftwareDto> = {
    title: 'Software',
    subtitle: 'Catalogo de productos de software y su uso de licencias.',
    entity: 'software',
    resource: inject(LicensingService).software,
    paged: true,
    activeFilter: true,
    showActive: true,
    canDelete: true,
    managePermission: P.SoftwareManage,
    searchPlaceholder: 'Nombre o fabricante',
    columns: [
      { key: 'name', header: 'Nombre', value: (s) => (s.version ? `${s.name} ${s.version}` : s.name), sortKey: 'name' },
      { key: 'publisher', header: 'Fabricante', value: (s) => s.publisher, sortKey: 'publisher' },
      { key: 'category', header: 'Categoria', value: (s) => s.category, sortKey: 'category', hideOnMobile: true },
      { key: 'licenses', header: 'Licencias', value: (s) => s.licenseCount, align: 'end', width: '100px' },
      { key: 'seats', header: 'Puestos en uso', value: (s) => `${s.usedSeats} / ${s.totalSeats}`, align: 'end', width: '140px' },
    ],
    fields: [
      { key: 'name', label: 'Nombre', required: true, maxLength: 150 },
      { key: 'version', label: 'Version', maxLength: 50 },
      { key: 'publisher', label: 'Fabricante', maxLength: 150 },
      { key: 'category', label: 'Categoria', maxLength: 100, hint: 'p. ej. Ofimatica, Seguridad, Diseno' },
      { key: 'description', label: 'Descripcion', type: 'textarea', maxLength: 1000 },
      { key: 'isActive', label: 'Activo', type: 'checkbox', defaultValue: true },
    ],
    afterChange: () => this.lookups.invalidate('software'),
  };
}

@Component({
  selector: 'app-vendors-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CatalogPageComponent],
  template: `<app-catalog-page [config]="config" />`,
})
export class VendorsPage {
  private readonly lookups = inject(LookupService);

  protected readonly config: CatalogConfig<VendorDto> = {
    title: 'Proveedores',
    subtitle: 'Empresas que suministran equipos, licencias y servicios.',
    entity: 'proveedor',
    resource: inject(LicensingService).vendors,
    paged: true,
    canDelete: true,
    managePermission: P.VendorsManage,
    searchPlaceholder: 'Nombre, codigo o RUC',
    columns: [
      { key: 'code', header: 'Codigo', value: (v) => v.code, sortKey: 'code', width: '120px' },
      { key: 'name', header: 'Nombre', value: (v) => v.name, sortKey: 'name' },
      { key: 'contact', header: 'Contacto', value: (v) => [v.contactName, v.email].filter(Boolean).join(' · '), hideOnMobile: true },
      { key: 'phone', header: 'Telefono', value: (v) => v.phone, hideOnMobile: true },
      { key: 'rating', header: 'Calificacion', value: (v) => (v.rating != null ? `${v.rating} / 5` : '—'), sortKey: 'rating', width: '120px' },
      { key: 'status', header: 'Estado', value: (v) => enumLabel('VendorStatus', v.status), width: '110px' },
    ],
    fields: [
      { key: 'code', label: 'Codigo', maxLength: 30 },
      { key: 'name', label: 'Razon social', required: true, maxLength: 200 },
      { key: 'taxId', label: 'RUC / identificacion fiscal', maxLength: 50 },
      { key: 'status', label: 'Estado', type: 'select', required: true, options: enumOptions('VendorStatus'), defaultValue: 'Active' },
      { key: 'contactName', label: 'Persona de contacto', maxLength: 150 },
      { key: 'email', label: 'Correo', type: 'email', maxLength: 200 },
      { key: 'phone', label: 'Telefono', maxLength: 50 },
      { key: 'website', label: 'Sitio web', type: 'url', maxLength: 300 },
      { key: 'address', label: 'Direccion', maxLength: 300, wide: true },
      { key: 'city', label: 'Ciudad', maxLength: 100 },
      { key: 'country', label: 'Pais', maxLength: 100 },
      { key: 'rating', label: 'Calificacion (1 a 5)', type: 'number', min: 1, max: 5 },
      { key: 'notes', label: 'Notas', type: 'textarea', maxLength: 1000 },
    ],
    afterChange: () => this.lookups.invalidate('vendors'),
  };
}
