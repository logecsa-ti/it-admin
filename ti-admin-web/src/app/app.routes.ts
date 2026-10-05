import { Routes } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { authGuard, guestGuard, permissionGuard } from '@core/guards/auth.guards';

/** Rutas: cada feature se carga en diferido (CONVENTIONS.md 3.2) y declara sus permisos en `data`. */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Iniciar sesion · TI Admin',
    loadComponent: () => import('@features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('@layout/shell/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · TI Admin',
        loadComponent: () => import('@features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'mis-activos',
        title: 'Mis activos · TI Admin',
        data: { breadcrumb: 'Mis activos' },
        loadComponent: () => import('@features/assets/my-assets/my-assets.component').then((m) => m.MyAssetsComponent),
      },
      {
        path: 'activos',
        canActivate: [permissionGuard],
        data: { permissions: [P.AssetsView], breadcrumb: 'Activos' },
        loadChildren: () => import('@features/assets/assets.routes').then((m) => m.ASSET_ROUTES),
      },
      {
        path: 'asignaciones',
        title: 'Asignaciones · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.AssignmentsView], breadcrumb: 'Asignaciones' },
        loadComponent: () => import('@features/assets/assignment-list/assignment-list.component').then((m) => m.AssignmentListComponent),
      },
      {
        path: 'tickets',
        data: { breadcrumb: 'Tickets' },
        loadChildren: () => import('@features/tickets/tickets.routes').then((m) => m.TICKET_ROUTES),
      },
      {
        path: 'categorias',
        title: 'Categorias · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.ConfigurationManage], breadcrumb: 'Categorias' },
        loadComponent: () => import('@features/tickets/helpdesk-config-pages').then((m) => m.TicketCategoriesPage),
      },
      {
        path: 'sla',
        title: 'Politicas SLA · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.TicketsView], breadcrumb: 'Politicas SLA' },
        loadComponent: () => import('@features/tickets/helpdesk-config-pages').then((m) => m.SlaPoliciesPage),
      },
      {
        path: 'software',
        title: 'Software · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.SoftwareView], breadcrumb: 'Software' },
        loadComponent: () => import('@features/licensing/licensing-catalog-pages').then((m) => m.SoftwarePage),
      },
      {
        path: 'proveedores',
        canActivate: [permissionGuard],
        data: { permissions: [P.VendorsView], breadcrumb: 'Proveedores' },
        children: [
          { path: '', title: 'Proveedores · TI Admin', loadComponent: () => import('@features/licensing/licensing-catalog-pages').then((m) => m.VendorsPage) },
          { path: ':id', title: 'Proveedor · TI Admin', data: { breadcrumb: 'Detalle' }, loadComponent: () => import('@features/licensing/vendor-detail/vendor-detail.component').then((m) => m.VendorDetailComponent) },
        ],
      },
      {
        path: 'licencias',
        canActivate: [permissionGuard],
        data: { permissions: [P.LicensesView], breadcrumb: 'Licencias' },
        children: [
          { path: '', title: 'Licencias · TI Admin', loadComponent: () => import('@features/licensing/license-list/license-list.component').then((m) => m.LicenseListComponent) },
          { path: ':id', title: 'Licencia · TI Admin', data: { breadcrumb: 'Detalle' }, loadComponent: () => import('@features/licensing/license-detail/license-detail.component').then((m) => m.LicenseDetailComponent) },
        ],
      },
      {
        path: 'contratos',
        canActivate: [permissionGuard],
        data: { permissions: [P.ContractsView], breadcrumb: 'Contratos' },
        children: [
          { path: '', title: 'Contratos · TI Admin', loadComponent: () => import('@features/licensing/contract-list/contract-list.component').then((m) => m.ContractListComponent) },
          { path: ':id', title: 'Contrato · TI Admin', data: { breadcrumb: 'Detalle' }, loadComponent: () => import('@features/licensing/contract-detail/contract-detail.component').then((m) => m.ContractDetailComponent) },
        ],
      },
      {
        path: 'mantenimientos',
        canActivate: [permissionGuard],
        data: { permissions: [P.MaintenanceView], breadcrumb: 'Mantenimientos' },
        loadChildren: () => import('@features/operations/operations.routes').then((m) => m.MAINTENANCE_ROUTES),
      },
      {
        path: 'cambios',
        canActivate: [permissionGuard],
        data: { permissions: [P.ChangesView], breadcrumb: 'Cambios' },
        loadChildren: () => import('@features/operations/operations.routes').then((m) => m.CHANGE_ROUTES),
      },
      {
        path: 'compras',
        canActivate: [permissionGuard],
        data: { permissions: [P.PurchasesView], breadcrumb: 'Compras' },
        loadChildren: () => import('@features/operations/operations.routes').then((m) => m.PURCHASE_ROUTES),
      },
      {
        path: 'usuarios',
        canActivate: [permissionGuard],
        data: { permissions: [P.UsersView], breadcrumb: 'Usuarios' },
        children: [
          { path: '', title: 'Usuarios · TI Admin', loadComponent: () => import('@features/administration/users/user-list.component').then((m) => m.UserListComponent) },
          { path: ':id', title: 'Usuario · TI Admin', data: { breadcrumb: 'Detalle' }, loadComponent: () => import('@features/administration/users/user-detail.component').then((m) => m.UserDetailComponent) },
        ],
      },
      {
        path: 'roles',
        title: 'Roles y permisos · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.RolesView], breadcrumb: 'Roles y permisos' },
        loadComponent: () => import('@features/administration/roles/roles.component').then((m) => m.RolesComponent),
      },
      {
        path: 'reportes',
        title: 'Reportes · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.ReportsView], breadcrumb: 'Reportes' },
        loadComponent: () => import('@features/reports/reports.component').then((m) => m.ReportsComponent),
      },
      {
        path: 'auditoria',
        title: 'Auditoria · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.AuditView], breadcrumb: 'Auditoria' },
        loadComponent: () => import('@features/reports/audit/audit-list.component').then((m) => m.AuditListComponent),
      },
      {
        path: 'configuracion',
        title: 'Configuracion · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.ConfigurationView], breadcrumb: 'Configuracion' },
        loadComponent: () => import('@features/reports/configuration/configuration.component').then((m) => m.ConfigurationComponent),
      },
      {
        path: 'notificaciones',
        title: 'Notificaciones · TI Admin',
        data: { breadcrumb: 'Notificaciones' },
        loadComponent: () => import('@features/notifications/notifications.component').then((m) => m.NotificationsComponent),
      },
      {
        path: 'perfil',
        title: 'Mi perfil · TI Admin',
        data: { breadcrumb: 'Mi perfil' },
        loadComponent: () => import('@features/auth/profile/profile.component').then((m) => m.ProfileComponent),
      },
      {
        path: 'departamentos',
        title: 'Departamentos · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.OrganizationView], breadcrumb: 'Departamentos' },
        loadComponent: () => import('@features/administration/catalog-pages').then((m) => m.DepartmentsPage),
      },
      {
        path: 'ubicaciones',
        title: 'Ubicaciones · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.OrganizationView], breadcrumb: 'Ubicaciones' },
        loadComponent: () => import('@features/administration/catalog-pages').then((m) => m.LocationsPage),
      },
      {
        path: 'tipos-activo',
        title: 'Tipos de activo · TI Admin',
        canActivate: [permissionGuard],
        data: { permissions: [P.AssetTypesManage], breadcrumb: 'Tipos de activo' },
        loadComponent: () => import('@features/administration/catalog-pages').then((m) => m.AssetTypesPage),
      },
      {
        path: 'acceso-denegado',
        title: 'Acceso denegado · TI Admin',
        loadComponent: () => import('@features/errors/status-page.component').then((m) => m.StatusPageComponent),
        data: {
          code: '403',
          icon: 'lock',
          heading: 'Acceso denegado',
          message: 'Su usuario no tiene permisos para ver esta seccion. Si lo necesita, solicitelo al area de TI.',
        },
      },
      {
        path: '**',
        title: 'Pagina no encontrada · TI Admin',
        loadComponent: () => import('@features/errors/status-page.component').then((m) => m.StatusPageComponent),
      },
    ],
  },
];
