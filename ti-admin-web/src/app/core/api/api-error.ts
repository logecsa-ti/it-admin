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

  const body = isEnvelope(error.error) ? error.error : null;
  const details = Array.isArray(body?.errors) ? body.errors : [];
  return {
    status: error.status,
    message: body?.message || details[0]?.message || defaultMessage(error.status),
    details,
    traceId: body?.traceId ?? error.headers?.get('X-Correlation-Id'),
  };
}

/**
 * Solo el sobre ApiResponse trae mensajes para el usuario. Un HTML de un proxy o un fallo de parseo
 * (con fetch, `error.error` es el SyntaxError) no deben mostrarse tal cual.
 */
function isEnvelope(body: unknown): body is Partial<ApiEnvelope<unknown>> {
  return typeof body === 'object' && body !== null && !(body instanceof Error) && ('success' in body || 'errors' in body);
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
      if (status >= 200 && status < 300) {
        return 'El servidor envio una respuesta inesperada. Si persiste, contacte a soporte.';
      }
      return status >= 500
        ? 'Error interno del servidor. Si persiste, contacte a soporte.'
        : 'No fue posible completar la solicitud.';
  }
}
