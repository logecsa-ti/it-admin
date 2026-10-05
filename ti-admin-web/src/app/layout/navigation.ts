import { PERMISSIONS as P, PermissionCode } from '@core/auth/permissions.generated';

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  queryParams?: Record<string, string>;
  /** Alguno de estos permisos; vacio = cualquier usuario autenticado. */
  permissions?: PermissionCode[];
  /** Coincidencia exacta de ruta para marcar activo. */
  exact?: boolean;
}

export interface NavGroup {
  label: string;
  items: NavItem[];
}

/** Menu (SPECS.md 31). Cada entrada se muestra solo con su permiso; la API valida siempre. */
export const NAVIGATION: NavGroup[] = [
  {
    label: 'General',
    items: [
      { label: 'Dashboard', icon: 'space_dashboard', route: '/dashboard', exact: true },
      { label: 'Mis activos', icon: 'devices', route: '/mis-activos' },
      { label: 'Notificaciones', icon: 'notifications', route: '/notificaciones' },
    ],
  },
  {
    label: 'Gestion TI',
    items: [
      { label: 'Activos', icon: 'inventory_2', route: '/activos', permissions: [P.AssetsView] },
      { label: 'Asignaciones', icon: 'assignment_ind', route: '/asignaciones', permissions: [P.AssignmentsView] },
      { label: 'Software', icon: 'apps', route: '/software', permissions: [P.SoftwareView] },
      { label: 'Licencias', icon: 'key', route: '/licencias', permissions: [P.LicensesView] },
      { label: 'Mantenimientos', icon: 'build', route: '/mantenimientos', permissions: [P.MaintenanceView] },
      { label: 'Cambios', icon: 'published_with_changes', route: '/cambios', permissions: [P.ChangesView] },
    ],
  },
  {
    label: 'Soporte',
    items: [
      { label: 'Tickets', icon: 'confirmation_number', route: '/tickets' },
      { label: 'Categorias', icon: 'category', route: '/categorias', permissions: [P.ConfigurationManage] },
      { label: 'Politicas SLA', icon: 'timer', route: '/sla', permissions: [P.TicketsView] },
    ],
  },
  {
    label: 'Gestion',
    items: [
      { label: 'Proveedores', icon: 'storefront', route: '/proveedores', permissions: [P.VendorsView] },
      { label: 'Contratos', icon: 'contract', route: '/contratos', permissions: [P.ContractsView] },
      { label: 'Compras', icon: 'shopping_cart', route: '/compras', permissions: [P.PurchasesView] },
    ],
  },
  {
    label: 'Reportes',
    items: [
      { label: 'Reportes', icon: 'bar_chart', route: '/reportes', permissions: [P.ReportsView] },
      { label: 'Auditoria', icon: 'policy', route: '/auditoria', permissions: [P.AuditView] },
    ],
  },
  {
    label: 'Administracion',
    items: [
      { label: 'Usuarios', icon: 'group', route: '/usuarios', permissions: [P.UsersView] },
      { label: 'Roles y permisos', icon: 'admin_panel_settings', route: '/roles', permissions: [P.RolesView] },
      { label: 'Departamentos', icon: 'corporate_fare', route: '/departamentos', permissions: [P.OrganizationView] },
      { label: 'Ubicaciones', icon: 'location_on', route: '/ubicaciones', permissions: [P.OrganizationView] },
      { label: 'Tipos de activo', icon: 'devices_other', route: '/tipos-activo', permissions: [P.AssetTypesManage] },
      { label: 'Configuracion', icon: 'settings', route: '/configuracion', permissions: [P.ConfigurationView] },
    ],
  },
];
