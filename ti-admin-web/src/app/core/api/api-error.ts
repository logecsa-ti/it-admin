import { HttpErrorResponse } from '@angular/common/http';
import { ApiEnvelope } from '@core/models';

export interface ApiErrorInfo {
  status: number;
  message: string;
  details: { code: string; message: string }[];
  traceId?: string | null;
}

/** Normaliza cualquier error HTTP al formato ApiResponse del backend (mensajes en espanol). */
export function toApiError(error: unknown): ApiErrorInfo {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: -1, message: 'Ocurrio un error inesperado.', details: [] };
  }

  if (error.status === 0) {
    return { status: 0, message: 'No hay conexion con el servidor. Verifique su red.', details: [] };
  }

  const body = error.error as Partial<ApiEnvelope<unknown>> | null;
  const details = Array.isArray(body?.errors) ? body.errors : [];
  return {
    status: error.status,
    message: body?.message || details[0]?.message || defaultMessage(error.status),
    details,
    traceId: body?.traceId ?? error.headers?.get('X-Correlation-Id'),
  };
}

function defaultMessage(status: number): string {
  switch (status) {
    case 400:
      return 'La solicitud contiene datos invalidos.';
    case 401:
      return 'Su sesion expiro. Inicie sesion nuevamente.';
    case 403:
      return 'No tiene permisos para realizar esta accion.';
    case 404:
      return 'El recurso solicitado no existe.';
    case 409:
      return 'El registro fue modificado o ya existe. Actualice e intente de nuevo.';
    case 429:
      return 'Demasiadas solicitudes. Espere un momento.';
    default:
      return status >= 500
        ? 'Error interno del servidor. Si persiste, contacte a soporte.'
        : 'No fue posible completar la solicitud.';
  }
}
