import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('usa el mensaje y los errores del sobre ApiResponse', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { success: false, message: 'El codigo ya existe.', errors: [{ code: 'DUPLICATE_RECORD', message: 'Duplicado' }] },
    });

    const info = toApiError(error);

    expect(info.message).toBe('El codigo ya existe.');
    expect(info.details[0].code).toBe('DUPLICATE_RECORD');
  });

  it('no muestra el texto tecnico de un fallo de parseo (HTML en lugar de JSON)', () => {
    const error = new HttpErrorResponse({ status: 200, error: new SyntaxError(`Unexpected token '<', "<!doctype "... is not valid JSON`) });

    expect(toApiError(error).message).toBe('El servidor envio una respuesta inesperada. Si persiste, contacte a soporte.');
  });

  it('ignora cuerpos que no son ApiResponse y usa el mensaje por estado', () => {
    const error = new HttpErrorResponse({ status: 502, error: '<html>Bad Gateway</html>', headers: new HttpHeaders({ 'X-Correlation-Id': 'abc' }) });

    const info = toApiError(error);

    expect(info.message).toBe('Error interno del servidor. Si persiste, contacte a soporte.');
    expect(info.traceId).toBe('abc');
  });

  it('sin conexion informa un problema de red', () => {
    expect(toApiError(new HttpErrorResponse({ status: 0 })).message).toContain('No hay conexion');
  });
});
