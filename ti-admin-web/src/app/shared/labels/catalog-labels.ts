/** Etiquetas en espanol para nombres tecnicos del backend (modulos de permisos, grupos de configuracion). */
const MODULES: Record<string, string> = {
  Assets: 'Activos',
  AssetTypes: 'Tipos de activo',
  Assignments: 'Asignaciones',
  Software: 'Software',
  Licenses: 'Licencias',
  Tickets: 'Tickets (incidentes)',
  Requests: 'Solicitudes de servicio',
  Maintenance: 'Mantenimiento',
  Vendors: 'Proveedores',
  Contracts: 'Contratos',
  Purchases: 'Compras',
  Changes: 'Cambios',
  Documents: 'Documentos',
  Users: 'Usuarios',
  Roles: 'Roles',
  Organization: 'Organizacion',
  Reports: 'Reportes',
  Audit: 'Auditoria',
  Configuration: 'Configuracion',
  Dashboard: 'Dashboard',
  Notifications: 'Notificaciones',
};

const ACTIONS: Record<string, string> = {
  View: 'Ver',
  Create: 'Crear',
  Update: 'Editar',
  Delete: 'Eliminar',
  Assign: 'Asignar',
  Unassign: 'Desasignar',
  Manage: 'Administrar',
  Resolve: 'Resolver',
  Close: 'Cerrar',
  Approve: 'Aprobar',
  Review: 'Revisar',
  Export: 'Exportar',
  Disable: 'Desactivar',
};

const CONFIG_GROUPS: Record<string, string> = {
  Alerts: 'Alertas',
  Changes: 'Cambios',
  Exports: 'Exportaciones',
  General: 'General',
  HelpDesk: 'Mesa de ayuda',
  Tickets: 'Tickets',
  Maintenance: 'Mantenimiento',
  Purchases: 'Compras',
  Security: 'Seguridad',
  Sla: 'SLA',
  Email: 'Correo',
  Notifications: 'Notificaciones',
};

export const moduleLabel = (module: string): string => MODULES[module] ?? module;
export const actionLabel = (action: string): string => ACTIONS[action] ?? action;
export const configGroupLabel = (group: string): string => CONFIG_GROUPS[group] ?? group;
