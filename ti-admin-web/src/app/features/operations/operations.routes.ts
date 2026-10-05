import { Routes } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { permissionGuard } from '@core/guards/auth.guards';

export const MAINTENANCE_ROUTES: Routes = [
  { path: '', title: 'Mantenimientos · TI Admin', loadComponent: () => import('./maintenance/maintenance-list.component').then((m) => m.MaintenanceListComponent) },
  { path: ':id', title: 'Mantenimiento · TI Admin', data: { breadcrumb: 'Detalle' }, loadComponent: () => import('./maintenance/maintenance-detail.component').then((m) => m.MaintenanceDetailComponent) },
];

export const CHANGE_ROUTES: Routes = [
  { path: '', title: 'Cambios · TI Admin', loadComponent: () => import('./changes/change-list.component').then((m) => m.ChangeListComponent) },
  { path: ':id', title: 'Cambio · TI Admin', data: { breadcrumb: 'Detalle' }, loadComponent: () => import('./changes/change-detail.component').then((m) => m.ChangeDetailComponent) },
];

export const PURCHASE_ROUTES: Routes = [
  { path: '', title: 'Compras · TI Admin', loadComponent: () => import('./purchases/purchase-list.component').then((m) => m.PurchaseListComponent) },
  {
    path: 'nueva',
    title: 'Nueva compra · TI Admin',
    canActivate: [permissionGuard],
    data: { permissions: [P.PurchasesCreate], breadcrumb: 'Nueva' },
    loadComponent: () => import('./purchases/purchase-form.component').then((m) => m.PurchaseFormComponent),
  },
  { path: ':id', title: 'Compra · TI Admin', data: { breadcrumb: 'Detalle' }, loadComponent: () => import('./purchases/purchase-detail.component').then((m) => m.PurchaseDetailComponent) },
  {
    path: ':id/editar',
    title: 'Editar compra · TI Admin',
    canActivate: [permissionGuard],
    data: { permissions: [P.PurchasesCreate], breadcrumb: 'Editar' },
    loadComponent: () => import('./purchases/purchase-form.component').then((m) => m.PurchaseFormComponent),
  },
];
