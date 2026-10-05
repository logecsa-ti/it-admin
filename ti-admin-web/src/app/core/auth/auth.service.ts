import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, firstValueFrom, map, of, shareReplay, tap, catchError } from 'rxjs';
import { environment } from '@env/environment';
import { ApiEnvelope, LoginResponse, UserProfileResponse } from '@core/models';
import { PermissionCode } from './permissions.generated';

const REFRESH_TOKEN_KEY = 'tiadmin.refreshToken';
/** Renovar el access token este tiempo antes de que expire. */
const RENEW_BEFORE_MS = 60_000;

/**
 * Sesion del usuario (ADR-038): el access token vive solo en memoria y el refresh token rotativo en
 * sessionStorage (sobrevive a recargar la pestana, no a cerrarla). Los permisos viajan en el perfil
 * y deciden menu, rutas y botones; la API vuelve a validarlos siempre.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private readonly userState = signal<UserProfileResponse | null>(null);
  private accessToken: string | null = null;
  private refreshInFlight: Observable<string | null> | null = null;
  private renewTimer: ReturnType<typeof setTimeout> | null = null;

  readonly user = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => this.userState() !== null);
  readonly permissions = computed(() => new Set(this.userState()?.permissions ?? []));
  readonly roles = computed(() => new Set(this.userState()?.roles ?? []));

  get token(): string | null {
    return this.accessToken;
  }

  /** Al iniciar la app: recupera la sesion de la pestana si hay refresh token. */
  async restore(): Promise<void> {
    if (readRefreshToken()) {
      await firstValueFrom(this.refresh());
    }
  }

  login(userName: string, password: string): Observable<UserProfileResponse> {
    return this.http
      .post<ApiEnvelope<LoginResponse>>(`${this.baseUrl}/login`, { userName, password })
      .pipe(
        map((response) => response.data as LoginResponse),
        tap((session) => this.startSession(session)),
        map((session) => session.user),
      );
  }

  /** Renueva la sesion una sola vez aunque varias peticiones reciban 401 a la vez. */
  refresh(): Observable<string | null> {
    const refreshToken = readRefreshToken();
    if (!refreshToken) {
      return of(null);
    }

    this.refreshInFlight ??= this.http
      .post<ApiEnvelope<LoginResponse>>(`${this.baseUrl}/refresh`, { refreshToken })
      .pipe(
        map((response) => {
          const session = response.data as LoginResponse;
          this.startSession(session);
          return session.accessToken;
        }),
        catchError((error: unknown) => {
          // Solo un rechazo del refresh token (400/401) cierra la sesion; un 429, 5xx o falta de red
          // es transitorio y no debe obligar a iniciar sesion de nuevo al recargar.
          const status = error instanceof HttpErrorResponse ? error.status : 0;
          if (status === 400 || status === 401) {
            this.clearSession();
          }
          return of(null);
        }),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay(1),
      );
    return this.refreshInFlight;
  }

  async logout(): Promise<void> {
    const refreshToken = readRefreshToken();
    if (refreshToken && this.accessToken) {
      try {
        await firstValueFrom(this.http.post(`${this.baseUrl}/logout`, { refreshToken }));
      } catch {
        // El refresh token expira solo; cerrar la sesion local es lo importante.
      }
    }
    this.clearSession();
    await this.router.navigate(['/login']);
  }

  /** La API rechazo la renovacion: se pide iniciar sesion conservando la ruta actual. */
  async expireSession(returnUrl: string): Promise<void> {
    this.clearSession();
    await this.router.navigate(['/login'], { queryParams: { returnUrl, expired: true } });
  }

  /** Recarga el perfil (p. ej. tras cambiar datos propios). */
  reloadProfile(): Observable<UserProfileResponse> {
    return this.http.get<ApiEnvelope<UserProfileResponse>>(`${this.baseUrl}/me`).pipe(
      map((response) => response.data as UserProfileResponse),
      tap((user) => this.userState.set(user)),
    );
  }

  changePassword(currentPassword: string, newPassword: string): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/change-password`, { currentPassword, newPassword });
  }

  /** Verdadero si el usuario tiene al menos uno de los permisos. */
  hasAny(...codes: PermissionCode[]): boolean {
    const granted = this.permissions();
    return codes.length === 0 || codes.some((code) => granted.has(code));
  }

  hasAll(...codes: PermissionCode[]): boolean {
    const granted = this.permissions();
    return codes.every((code) => granted.has(code));
  }

  private startSession(session: LoginResponse): void {
    this.accessToken = session.accessToken;
    writeRefreshToken(session.refreshToken);
    this.userState.set(session.user);
    this.scheduleRenewal(session.accessTokenExpiresAt);
  }

  private clearSession(): void {
    this.accessToken = null;
    this.userState.set(null);
    writeRefreshToken(null);
    if (this.renewTimer) {
      clearTimeout(this.renewTimer);
      this.renewTimer = null;
    }
  }

  private scheduleRenewal(expiresAt: string): void {
    if (this.renewTimer) {
      clearTimeout(this.renewTimer);
    }
    const delay = new Date(expiresAt).getTime() - Date.now() - RENEW_BEFORE_MS;
    this.renewTimer = setTimeout(() => this.refresh().subscribe(), Math.max(delay, 5_000));
  }
}

function readRefreshToken(): string | null {
  try {
    return sessionStorage.getItem(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

function writeRefreshToken(token: string | null): void {
  try {
    if (token) {
      sessionStorage.setItem(REFRESH_TOKEN_KEY, token);
    } else {
      sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    }
  } catch {
    // Almacenamiento bloqueado: la sesion dura mientras la pagina siga abierta.
  }
}
