import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { PermissionCode } from '@core/auth/permissions.generated';

/** Requiere sesion; si no hay, envia al login recordando la ruta pedida. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Solo para usuarios sin sesion (pantalla de login). */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;

/**
 * Exige al menos uno de los permisos indicados en `data.permissions` (PermissionGuard, SPECS.md 31).
 * Sin permiso se muestra la pagina de acceso denegado; la API valida de nuevo cada operacion.
 */
export const permissionGuard: CanActivateFn & CanMatchFn = (route) => {
  const required = (route.data?.['permissions'] ?? []) as PermissionCode[];
  return inject(AuthService).hasAny(...required)
    ? true
    : inject(Router).createUrlTree(['/acceso-denegado']);
};

/** Exige uno de los roles de `data.roles` (RoleGuard); se prefiere permissionGuard. */
export const roleGuard: CanActivateFn = (route) => {
  const roles = (route.data?.['roles'] ?? []) as string[];
  const granted = inject(AuthService).roles();
  return roles.length === 0 || roles.some((role) => granted.has(role))
    ? true
    : inject(Router).createUrlTree(['/acceso-denegado']);
};
