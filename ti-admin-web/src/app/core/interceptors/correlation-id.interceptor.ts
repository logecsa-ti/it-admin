import { HttpInterceptorFn } from '@angular/common/http';

export const CORRELATION_HEADER = 'X-Correlation-Id';

/**
 * Cada peticion lleva un X-Correlation-Id propio (SPECS.md 63): la API lo registra en logs y auditoria,
 * y se muestra como referencia en los mensajes de error para soporte.
 */
export const correlationIdInterceptor: HttpInterceptorFn = (request, next) =>
  next(request.clone({ setHeaders: { [CORRELATION_HEADER]: newCorrelationId() } }));

function newCorrelationId(): string {
  return `web-${crypto.randomUUID().replace(/-/g, '').slice(0, 20)}`;
}
