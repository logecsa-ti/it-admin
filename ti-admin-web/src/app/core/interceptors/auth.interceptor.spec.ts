import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { environment } from '@env/environment';
import { AuthService } from '@core/auth/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  const auth = { token: 'old', refresh: vi.fn(), expireSession: vi.fn() };

  beforeEach(() => {
    auth.token = 'old';
    auth.refresh.mockReset();
    auth.expireSession.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('agrega el Bearer solo a la API', () => {
    http.get(`${environment.apiUrl}/assets`).subscribe();
    http.get('https://otro.dominio/recurso').subscribe();

    expect(backend.expectOne(`${environment.apiUrl}/assets`).request.headers.get('Authorization')).toBe('Bearer old');
    expect(backend.expectOne('https://otro.dominio/recurso').request.headers.has('Authorization')).toBe(false);
  });

  it('ante un 401 renueva y reintenta con el token nuevo', () => {
    auth.refresh.mockReturnValue(of('new'));
    let body: unknown;

    http.get(`${environment.apiUrl}/assets`).subscribe((b) => (body = b));
    backend.expectOne(`${environment.apiUrl}/assets`).flush(null, { status: 401, statusText: 'Unauthorized' });
    const retry = backend.expectOne(`${environment.apiUrl}/assets`);
    retry.flush({ ok: true });

    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    expect(body).toEqual({ ok: true });
  });

  it('si no se puede renovar envia al login', () => {
    auth.refresh.mockReturnValue(of(null));

    http.get(`${environment.apiUrl}/assets`).subscribe({ error: () => undefined });
    backend.expectOne(`${environment.apiUrl}/assets`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.expireSession).toHaveBeenCalled();
  });

  it('no intercepta el login ni la renovacion', () => {
    http.post(`${environment.apiUrl}/auth/login`, {}).subscribe({ error: () => undefined });
    const login = backend.expectOne(`${environment.apiUrl}/auth/login`);
    login.flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(login.request.headers.has('Authorization')).toBe(false);
    expect(auth.refresh).not.toHaveBeenCalled();
  });
});
