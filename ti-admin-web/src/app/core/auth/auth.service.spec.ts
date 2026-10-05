import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '@env/environment';
import { AuthService } from './auth.service';

const session = (token: string) => ({
  success: true,
  errors: [],
  data: {
    accessToken: token,
    refreshToken: `refresh-${token}`,
    accessTokenExpiresAt: new Date(Date.now() + 30 * 60_000).toISOString(),
    refreshTokenExpiresAt: new Date(Date.now() + 7 * 86_400_000).toISOString(),
    user: { id: 7, userName: 'maria', email: 'm@x', firstName: 'Maria', lastName: 'Lopez', fullName: 'Maria Lopez', isActive: true, roles: ['USER'], permissions: ['TICKETS.CREATE'] },
  },
});

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('inicia sesion, guarda solo el refresh token y expone permisos', () => {
    auth.login('maria', 'secret').subscribe();
    http.expectOne(`${environment.apiUrl}/auth/login`).flush(session('a1'));

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.token).toBe('a1');
    expect(sessionStorage.getItem('tiadmin.refreshToken')).toBe('refresh-a1');
    expect(JSON.stringify(sessionStorage)).not.toContain('"a1"');
    expect(auth.hasAny('TICKETS.CREATE')).toBe(true);
    expect(auth.hasAny('ASSETS.VIEW')).toBe(false);
  });

  it('renueva una sola vez aunque varias peticiones lo pidan a la vez', () => {
    sessionStorage.setItem('tiadmin.refreshToken', 'r0');
    const tokens: (string | null)[] = [];

    auth.refresh().subscribe((t) => tokens.push(t));
    auth.refresh().subscribe((t) => tokens.push(t));
    http.expectOne(`${environment.apiUrl}/auth/refresh`).flush(session('a2'));

    expect(tokens).toEqual(['a2', 'a2']);
  });

  it('un 429 al renovar no borra la sesion de la pestana', () => {
    sessionStorage.setItem('tiadmin.refreshToken', 'r0');

    auth.refresh().subscribe();
    http.expectOne(`${environment.apiUrl}/auth/refresh`).flush(null, { status: 429, statusText: 'Too Many Requests' });

    expect(sessionStorage.getItem('tiadmin.refreshToken')).toBe('r0');
  });

  it('un refresh token rechazado (401) cierra la sesion', () => {
    sessionStorage.setItem('tiadmin.refreshToken', 'r0');

    auth.refresh().subscribe();
    http.expectOne(`${environment.apiUrl}/auth/refresh`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem('tiadmin.refreshToken')).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
