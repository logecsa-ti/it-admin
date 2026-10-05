import { map } from 'rxjs';
import { ChangeRequestDto, MaintenanceDto } from '@core/models';
import { FieldDef } from '@shared/components/form-dialog/form-dialog.component';
import { enumOptions } from '@shared/labels/enum-labels';
import { LookupService, Option } from '@shared/services/lookup.service';

const toSelect = (items: Option[]) => items.map((item) => ({ value: item.id, label: item.label }));

export function maintenanceFields(lookups: LookupService, editing: MaintenanceDto | null): FieldDef[] {
  return [
    { key: 'title', label: 'Titulo', required: true, maxLength: 200, wide: true },
    { key: 'type', label: 'Tipo', type: 'select', required: true, options: enumOptions('MaintenanceType'), defaultValue: 'Preventive' },
    { key: 'scheduledDate', label: 'Fecha programada', type: 'datetime', required: true },
    {
      key: 'assetId',
      label: 'Activo',
      type: 'picker',
      required: true,
      search: lookups.searchAssets,
      pickerLabel: () => (editing ? `${editing.assetCode} · ${editing.assetName}` : null),
    },
    { key: 'technicianId', label: 'Tecnico', type: 'picker', search: lookups.searchUsers, pickerLabel: () => editing?.technicianName },
    { key: 'vendorId', label: 'Proveedor externo', type: 'select', emptyOption: 'Ninguno', options: lookups.vendors().pipe(map(toSelect)) },
    { key: 'contractId', label: 'Contrato', type: 'select', emptyOption: 'Ninguno', options: lookups.contracts().pipe(map(toSelect)) },
    { key: 'estimatedCost', label: 'Costo estimado', type: 'number', min: 0 },
    { key: 'description', label: 'Descripcion', type: 'textarea', maxLength: 2000 },
  ];
}

export function changeFields(lookups: LookupService, editing: ChangeRequestDto | null): FieldDef[] {
  return [
    { key: 'title', label: 'Titulo', required: true, maxLength: 200, wide: true },
    { key: 'type', label: 'Tipo', type: 'select', required: true, options: enumOptions('ChangeType'), defaultValue: 'Normal', hint: 'Estandar: pre-aprobado y de bajo riesgo' },
    { key: 'risk', label: 'Riesgo', type: 'select', required: true, options: enumOptions('ChangeRisk'), defaultValue: 'Medium' },
    { key: 'impact', label: 'Impacto', type: 'select', required: true, options: enumOptions('ChangeImpact'), defaultValue: 'Medium' },
    { key: 'plannedDate', label: 'Fecha planificada', type: 'datetime' },
    { key: 'departmentId', label: 'Departamento afectado', type: 'select', emptyOption: 'Ninguno', options: lookups.departments().pipe(map(toSelect)) },
    { key: 'assetId', label: 'Activo afectado', type: 'picker', search: lookups.searchAssets, pickerLabel: () => (editing?.assetId ? `#${editing.assetId}` : null) },
    { key: 'description', label: 'Descripcion y justificacion', type: 'textarea', required: true, maxLength: 4000 },
    { key: 'rollbackPlan', label: 'Plan de reversion', type: 'textarea', maxLength: 4000, hint: 'Obligatorio para riesgo alto' },
  ];
}
