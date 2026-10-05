import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '@env/environment';
import { AuthService } from '@core/auth/auth.service';

/** Endpoints que no llevan token ni disparan renovacion. */
const ANONYMOUS = ['/auth/login', '/auth/refresh', '/configuration/public'];

/**
 * Agrega el Bearer a las peticiones a la API. Ante un 401 renueva la sesion una vez (single-flight)
 * y reintenta; si la renovacion falla, envia al login conservando la ruta.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiRequest(request) || ANONYMOUS.some((path) => request.url.includes(path))) {
    return next(request);
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  return next(withToken(request, auth.token)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap((token) => {
          if (!token) {
            void auth.expireSession(router.url);
            return throwError(() => error);
          }
          return next(withToken(request, token));
        }),
      );
    }),
  );
};

function isApiRequest(request: HttpRequest<unknown>): boolean {
  return request.url.startsWith(environment.apiUrl);
}

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}
