import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { map } from 'rxjs';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { SlaPolicyDto, TicketCategoryDto } from '@core/models';
import { CatalogConfig, CatalogPageComponent } from '@shared/components/catalog-page/catalog-page.component';
import { enumLabel, enumOptions } from '@shared/labels/enum-labels';
import { LookupService } from '@shared/services/lookup.service';
import { TicketsService } from './tickets.service';

/** Duracion en minutos → "4 h 30 min". */
function minutes(value: number): string {
  const hours = Math.floor(value / 60);
  const rest = value % 60;
  return [hours ? `${hours} h` : '', rest ? `${rest} min` : ''].filter(Boolean).join(' ') || '0 min';
}

@Component({
  selector: 'app-ticket-categories-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CatalogPageComponent],
  template: `<app-catalog-page [config]="config" />`,
})
export class TicketCategoriesPage {
  private readonly lookups = inject(LookupService);

  protected readonly config: CatalogConfig<TicketCategoryDto> = {
    title: 'Categorias de tickets',
    subtitle: 'Definen el tipo (incidente o solicitud), la prioridad por defecto y si requieren aprobacion.',
    entity: 'categoria',
    resource: inject(TicketsService).categories,
    activeFilter: true,
    showActive: true,
    managePermission: P.ConfigurationManage,
    searchPlaceholder: 'Buscar categoria',
    clientSearch: (c) => `${c.code} ${c.name}`,
    columns: [
      { key: 'code', header: 'Codigo', value: (c) => c.code, width: '150px' },
      { key: 'name', header: 'Nombre', value: (c) => c.name },
      { key: 'type', header: 'Tipo', value: (c) => enumLabel('TicketType', c.type) },
      { key: 'priority', header: 'Prioridad por defecto', value: (c) => enumLabel('TicketPriority', c.defaultPriority), hideOnMobile: true },
      { key: 'approval', header: 'Aprobacion', value: (c) => (c.requiresApproval ? 'Requerida' : 'No'), hideOnMobile: true },
    ],
    fields: [
      { key: 'code', label: 'Codigo', required: true, maxLength: 30, lockedOnEdit: true },
      { key: 'name', label: 'Nombre', required: true, maxLength: 100 },
      { key: 'type', label: 'Tipo', type: 'select', required: true, options: enumOptions('TicketType'), defaultValue: 'Incident', hint: 'No se puede cambiar si ya tiene tickets' },
      { key: 'defaultPriority', label: 'Prioridad por defecto', type: 'select', required: true, options: enumOptions('TicketPriority'), defaultValue: 'Medium' },
      {
        key: 'departmentId',
        label: 'Departamento',
        type: 'select',
        emptyOption: 'Todos',
        options: this.lookups.departments().pipe(map((items) => items.map((d) => ({ value: d.id, label: d.label })))),
      },
      { key: 'description', label: 'Descripcion', type: 'textarea', maxLength: 500 },
      { key: 'requiresApproval', label: 'Requiere aprobacion (solo solicitudes de servicio)', type: 'checkbox' },
      { key: 'isActive', label: 'Activa', type: 'checkbox', defaultValue: true },
    ],
  };
}

@Component({
  selector: 'app-sla-policies-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CatalogPageComponent],
  template: `<app-catalog-page [config]="config" />`,
})
export class SlaPoliciesPage {
  private readonly lookups = inject(LookupService);

  protected readonly config: CatalogConfig<SlaPolicyDto> = {
    title: 'Politicas SLA',
    subtitle: 'Tiempos de respuesta y resolucion. Gana la politica mas especifica (categoria, prioridad, tipo, departamento).',
    entity: 'politica',
    resource: inject(TicketsService).slaPolicies,
    showActive: true,
    canDelete: true,
    managePermission: P.ConfigurationManage,
    searchPlaceholder: 'Buscar politica',
    clientSearch: (s) => s.name,
    columns: [
      { key: 'name', header: 'Nombre', value: (s) => (s.isDefault ? `${s.name} (por defecto)` : s.name) },
      { key: 'scope', header: 'Aplica a', value: (s) => [s.ticketType && enumLabel('TicketType', s.ticketType), s.priority && enumLabel('TicketPriority', s.priority)].filter(Boolean).join(' · ') || 'Todos' },
      { key: 'response', header: 'Respuesta', value: (s) => minutes(s.responseTimeMinutes), width: '120px' },
      { key: 'resolution', header: 'Resolucion', value: (s) => minutes(s.resolutionTimeMinutes), width: '120px' },
      { key: 'hours', header: 'Horario', value: (s) => (s.businessHoursOnly ? `${s.workStartTime.slice(0, 5)}–${s.workEndTime.slice(0, 5)}` : '24 x 7'), hideOnMobile: true },
    ],
    fields: [
      { key: 'name', label: 'Nombre', required: true, maxLength: 100, wide: true },
      { key: 'ticketType', label: 'Tipo de ticket', type: 'select', emptyOption: 'Todos', options: enumOptions('TicketType') },
      { key: 'priority', label: 'Prioridad', type: 'select', emptyOption: 'Todas', options: enumOptions('TicketPriority') },
      {
        key: 'categoryId',
        label: 'Categoria',
        type: 'select',
        emptyOption: 'Todas',
        options: this.lookups.ticketCategories().pipe(map((items) => items.map((c) => ({ value: c.id, label: c.name })))),
      },
      {
        key: 'departmentId',
        label: 'Departamento',
        type: 'select',
        emptyOption: 'Todos',
        options: this.lookups.departments().pipe(map((items) => items.map((d) => ({ value: d.id, label: d.label })))),
      },
      { key: 'responseTimeMinutes', label: 'Respuesta (minutos)', type: 'number', required: true, min: 1 },
      { key: 'resolutionTimeMinutes', label: 'Resolucion (minutos)', type: 'number', required: true, min: 1 },
      { key: 'businessHoursOnly', label: 'Contar solo horario laboral', type: 'checkbox', defaultValue: true },
      { key: 'workStartTime', label: 'Inicio de jornada', type: 'time', defaultValue: '08:00' },
      { key: 'workEndTime', label: 'Fin de jornada', type: 'time', defaultValue: '17:00' },
      { key: 'workDays', label: 'Dias laborales', maxLength: 20, defaultValue: '1,2,3,4,5', hint: '1 = lunes … 7 = domingo, separados por comas' },
      { key: 'isDefault', label: 'Politica por defecto', type: 'checkbox' },
      { key: 'isActive', label: 'Activa', type: 'checkbox', defaultValue: true },
    ],
  };
}
