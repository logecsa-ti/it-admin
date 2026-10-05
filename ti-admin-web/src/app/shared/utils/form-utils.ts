import { AbstractControl, FormGroup, ValidationErrors, ValidatorFn } from '@angular/forms';
import { ApiErrorInfo } from '@core/api/api-error';

/**
 * Prepara el valor de un formulario para la API: textos vacios → null, Date → 'yyyy-MM-dd'
 * (fechas de calendario, DateOnly en el backend).
 */
export function toRequest<T extends object>(value: T): T {
  const result: Record<string, unknown> = {};
  for (const [key, raw] of Object.entries(value)) {
    if (typeof raw === 'string') {
      const trimmed = raw.trim();
      result[key] = trimmed === '' ? null : trimmed;
    } else if (raw instanceof Date) {
      result[key] = toDateOnly(raw);
    } else {
      result[key] = raw === undefined ? null : raw;
    }
  }
  return result as T;
}

/** Fecha de calendario local → 'yyyy-MM-dd' (sin desfase de zona horaria). */
export function toDateOnly(date: Date | null | undefined): string | null {
  if (!date) {
    return null;
  }
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** 'yyyy-MM-dd' → Date local para el datepicker. */
export function fromDateOnly(value: string | null | undefined): Date | null {
  if (!value) {
    return null;
  }
  const [year, month, day] = value.slice(0, 10).split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** Instante ISO (UTC) → valor para <input type="datetime-local"> en la hora local del navegador. */
export function toLocalInput(value: string | null | undefined): string {
  if (!value) {
    return '';
  }
  const date = new Date(value);
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

/** <input type="datetime-local"> → instante ISO UTC para la API. */
export function fromLocalInput(value: string | null | undefined): string | null {
  return value ? new Date(value).toISOString() : null;
}

/** La fecha final no puede ser anterior a la inicial. */
export function dateRangeValidator(startKey: string, endKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = group.get(startKey)?.value as Date | string | null;
    const end = group.get(endKey)?.value as Date | string | null;
    if (!start || !end) {
      return null;
    }
    return new Date(end) < new Date(start) ? { dateRange: true } : null;
  };
}

/**
 * Muestra en el formulario los errores de validacion de la API (VALIDATION_ERROR con mensajes
 * "Campo: mensaje" o codigos por campo). Devuelve los mensajes que no se pudieron asociar a un control.
 */
export function applyServerErrors(form: FormGroup, error: ApiErrorInfo): string[] {
  const unmatched: string[] = [];
  for (const detail of error.details) {
    const control = Object.keys(form.controls).find(
      (key) => detail.message.toLowerCase().startsWith(key.toLowerCase()) || detail.code.toLowerCase() === key.toLowerCase(),
    );
    if (control) {
      form.controls[control].setErrors({ server: detail.message });
      form.controls[control].markAsTouched();
    } else {
      unmatched.push(detail.message);
    }
  }
  return unmatched;
}
