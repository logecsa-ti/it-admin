import { Pipe, PipeTransform, inject } from '@angular/core';
import { AppSettingsService } from '@core/config/app-settings.service';

export type AppDateFormat = 'date' | 'datetime' | 'short' | 'time' | 'relative';

/**
 * Fechas de la API: los instantes llegan en UTC (con Z) y se muestran en la zona de negocio (App.TimeZone);
 * las fechas de calendario (yyyy-MM-dd, p. ej. vencimientos) se muestran tal cual, sin conversion.
 */
@Pipe({ name: 'appDate' })
export class AppDatePipe implements PipeTransform {
  private readonly settings = inject(AppSettingsService);

  transform(value: string | Date | null | undefined, format: AppDateFormat = 'datetime'): string {
    if (!value) {
      return '—';
    }

    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
      const [year, month, day] = value.split('-').map(Number);
      return new Intl.DateTimeFormat('es-NI', { dateStyle: 'medium', timeZone: 'UTC' }).format(
        new Date(Date.UTC(year, month - 1, day)),
      );
    }

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }

    if (format === 'relative') {
      return relative(date);
    }

    const timeZone = this.settings.timeZone();
    const options: Intl.DateTimeFormatOptions =
      format === 'date'
        ? { dateStyle: 'medium', timeZone }
        : format === 'short'
          ? { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit', hour12: false, timeZone }
        : format === 'time'
          ? { timeStyle: 'short', timeZone }
          : { dateStyle: 'medium', timeStyle: 'short', timeZone };
    return new Intl.DateTimeFormat('es-NI', options).format(date);
  }
}

function relative(date: Date): string {
  const seconds = Math.round((date.getTime() - Date.now()) / 1000);
  const formatter = new Intl.RelativeTimeFormat('es', { numeric: 'auto' });
  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ['day', 86_400],
    ['hour', 3_600],
    ['minute', 60],
  ];
  for (const [unit, size] of units) {
    if (Math.abs(seconds) >= size) {
      return formatter.format(Math.round(seconds / size), unit);
    }
  }
  return 'ahora';
}
