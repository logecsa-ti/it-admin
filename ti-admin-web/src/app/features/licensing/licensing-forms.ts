import { map } from 'rxjs';
import { ContractDto, LicenseDto } from '@core/models';
import { FieldDef } from '@shared/components/form-dialog/form-dialog.component';
import { enumOptions } from '@shared/labels/enum-labels';
import { LookupService, Option } from '@shared/services/lookup.service';

const toSelect = (items: Option[]) => items.map((item) => ({ value: item.id, label: item.label }));

/** Campos del formulario de licencias (alta y edicion). */
export function licenseFields(lookups: LookupService, editing: LicenseDto | null): FieldDef[] {
  return [
    { key: 'softwareId', label: 'Software', type: 'select', required: true, options: lookups.software().pipe(map(toSelect)) },
    { key: 'name', label: 'Nombre de la licencia', required: true, maxLength: 150, hint: 'p. ej. Microsoft 365 E3 - 2026' },
    { key: 'licenseType', label: 'Tipo', type: 'select', required: true, options: enumOptions('LicenseType'), defaultValue: 'Subscription' },
    { key: 'quantity', label: 'Puestos', type: 'number', required: true, min: 1, defaultValue: 1 },
    { key: 'vendorId', label: 'Proveedor', type: 'select', emptyOption: 'Sin proveedor', options: lookups.vendors().pipe(map(toSelect)) },
    { key: 'contractId', label: 'Contrato', type: 'select', emptyOption: 'Sin contrato', options: lookups.contracts().pipe(map(toSelect)) },
    { key: 'purchaseDate', label: 'Fecha de compra', type: 'date' },
    { key: 'expirationDate', label: 'Vencimiento', type: 'date', hint: 'Vacio para licencias perpetuas' },
    { key: 'supportEndDate', label: 'Fin de soporte', type: 'date' },
    { key: 'cost', label: 'Costo', type: 'number', min: 0 },
    {
      key: 'licenseKey',
      label: 'Clave de licencia',
      maxLength: 500,
      wide: true,
      hint: editing?.hasLicenseKey ? 'Se guarda cifrada. Dejar vacio para conservar la clave actual.' : 'Se guarda cifrada y solo se muestra con permiso de gestion (queda auditado).',
    },
    { key: 'notes', label: 'Notas', type: 'textarea', maxLength: 1000 },
    ...(editing ? [{ key: 'isActive', label: 'Activa', type: 'checkbox' as const }] : []),
  ];
}

/** Valores de edicion: la clave nunca viene en el DTO. */
export function licenseFormValue(license: LicenseDto): Record<string, unknown> {
  return { ...license, licenseKey: null };
}

export function contractFields(lookups: LookupService, editing: ContractDto | null): FieldDef[] {
  return [
    ...(editing ? [] : [{ key: 'number', label: 'Numero de contrato', required: true, maxLength: 50 } as FieldDef]),
    { key: 'name', label: 'Nombre', required: true, maxLength: 200, wide: !editing },
    { key: 'vendorId', label: 'Proveedor', type: 'select', required: true, options: lookups.vendors().pipe(map(toSelect)) },
    { key: 'type', label: 'Tipo', type: 'select', required: true, options: enumOptions('ContractType'), defaultValue: 'Service' },
    { key: 'startDate', label: 'Inicio', type: 'date', required: true },
    { key: 'endDate', label: 'Fin', type: 'date', required: true },
    { key: 'value', label: 'Valor', type: 'number', min: 0 },
    { key: 'currency', label: 'Moneda', maxLength: 3, defaultValue: 'USD', hint: 'Codigo ISO, p. ej. USD o NIO' },
    { key: 'renewalNoticeDays', label: 'Aviso de renovacion (dias)', type: 'number', min: 0, defaultValue: 30 },
    { key: 'responsibleUserId', label: 'Responsable', type: 'picker', search: lookups.searchUsers, pickerLabel: (v) => v['responsibleUserName'] as string | undefined },
    { key: 'notes', label: 'Notas', type: 'textarea', maxLength: 1000 },
    { key: 'autoRenew', label: 'Renovacion automatica', type: 'checkbox' },
    ...(editing ? [] : [{ key: 'activate', label: 'Activar al guardar', type: 'checkbox', defaultValue: true } as FieldDef]),
  ];
}

/** Campos del formulario de proveedores (alta y edicion). */
export function vendorFields(): FieldDef[] {
  return [
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
  ];
}
