import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { authGuard, permissionGuard } from './auth.guards';

describe('guards', () => {
  const auth = { isAuthenticated: vi.fn(), hasAny: vi.fn() };
  const state = { url: '/activos/5' } as RouterStateSnapshot;
  const route = (permissions: string[]) => ({ data: { permissions } }) as unknown as ActivatedRouteSnapshot;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
  });

  it('sin sesion redirige al login conservando la ruta', () => {
    auth.isAuthenticated.mockReturnValue(false);

    const result = TestBed.runInInjectionContext(() => authGuard(route([]), state)) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login?returnUrl=%2Factivos%2F5');
  });

  it('sin el permiso de la ruta muestra acceso denegado', () => {
    auth.hasAny.mockReturnValue(false);

    const result = TestBed.runInInjectionContext(() => permissionGuard(route(['ASSETS.VIEW']), state)) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/acceso-denegado');
    expect(auth.hasAny).toHaveBeenCalledWith('ASSETS.VIEW');
  });

  it('con el permiso deja pasar', () => {
    auth.hasAny.mockReturnValue(true);

    expect(TestBed.runInInjectionContext(() => permissionGuard(route(['ASSETS.VIEW']), state))).toBe(true);
  });
});
