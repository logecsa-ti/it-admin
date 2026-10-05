import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { toApiError } from '@core/api/api-error';
import { ToastService } from '@core/services/toast.service';
import { SILENT_ERRORS } from './http-context';

/**
 * Manejo centralizado de errores (CONVENTIONS.md 3.2): un toast con el mensaje de la API y la referencia
 * de correlacion. Los 401 los resuelve el interceptor de autenticacion; las paginas que muestran el error
 * en linea (formularios, login) marcan la peticion con SILENT_ERRORS.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const toast = inject(ToastService);

  return next(request).pipe(
    catchError((error: unknown) => {
      const info = toApiError(error);
      if (info.status !== 401 && !request.context.get(SILENT_ERRORS)) {
        toast.error(info.message, info.status >= 500 ? info.traceId : null);
      }
      return throwError(() => error);
    }),
  );
};
