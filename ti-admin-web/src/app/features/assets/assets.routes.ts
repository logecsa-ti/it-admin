import { Routes } from '@angular/router';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { permissionGuard } from '@core/guards/auth.guards';

export const ASSET_ROUTES: Routes = [
  {
    path: '',
    title: 'Activos · TI Admin',
    loadComponent: () => import('./asset-list/asset-list.component').then((m) => m.AssetListComponent),
  },
  {
    path: 'nuevo',
    title: 'Nuevo activo · TI Admin',
    canActivate: [permissionGuard],
    data: { permissions: [P.AssetsCreate], breadcrumb: 'Nuevo' },
    loadComponent: () => import('./asset-form/asset-form.component').then((m) => m.AssetFormComponent),
  },
  {
    path: ':id',
    title: 'Activo · TI Admin',
    data: { breadcrumb: 'Detalle' },
    loadComponent: () => import('./asset-detail/asset-detail.component').then((m) => m.AssetDetailComponent),
  },
  {
    path: ':id/editar',
    title: 'Editar activo · TI Admin',
    canActivate: [permissionGuard],
    data: { permissions: [P.AssetsUpdate], breadcrumb: 'Editar' },
    loadComponent: () => import('./asset-form/asset-form.component').then((m) => m.AssetFormComponent),
  },
];
