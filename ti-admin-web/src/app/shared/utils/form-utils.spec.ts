import { FormControl, FormGroup } from '@angular/forms';
import { applyServerErrors, dateRangeValidator, fromDateOnly, toDateOnly, toRequest } from './form-utils';

describe('form-utils', () => {
  it('toRequest recorta textos, convierte vacios en null y fechas en yyyy-MM-dd', () => {
    const result = toRequest({ name: '  Laptop  ', notes: '   ', when: new Date(2026, 0, 5), qty: 0, flag: false });

    expect(result).toEqual({ name: 'Laptop', notes: null, when: '2026-01-05', qty: 0, flag: false });
  });

  it('las fechas de calendario no se desplazan por la zona horaria', () => {
    expect(toDateOnly(fromDateOnly('2026-12-31'))).toBe('2026-12-31');
  });

  it('valida que la fecha final no sea anterior a la inicial', () => {
    const group = new FormGroup(
      { start: new FormControl(new Date(2026, 5, 10)), end: new FormControl(new Date(2026, 5, 1)) },
      { validators: dateRangeValidator('start', 'end') },
    );

    expect(group.hasError('dateRange')).toBe(true);
  });

  it('asocia los errores de validacion de la API a los controles', () => {
    const form = new FormGroup({ assetCode: new FormControl('X'), name: new FormControl('Y') });

    const unmatched = applyServerErrors(form, {
      status: 400,
      message: 'Validacion',
      details: [
        { code: 'VALIDATION_ERROR', message: 'AssetCode: ya existe' },
        { code: 'VALIDATION_ERROR', message: 'Error general' },
      ],
    });

    expect(form.controls.assetCode.errors).toEqual({ server: 'AssetCode: ya existe' });
    expect(unmatched).toEqual(['Error general']);
  });
});
