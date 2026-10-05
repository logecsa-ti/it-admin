import { Pipe, PipeTransform, inject } from '@angular/core';
import { AppSettingsService } from '@core/config/app-settings.service';

/** Montos con la moneda del sistema (App.Currency, USD por defecto). */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  private readonly settings = inject(AppSettingsService);

  transform(value: number | null | undefined, currency?: string | null): string {
    if (value === null || value === undefined) {
      return '—';
    }
    const code = currency || this.settings.get('App.Currency') || 'USD';
    try {
      return new Intl.NumberFormat('es-NI', { style: 'currency', currency: code }).format(value);
    } catch {
      return `${code} ${value.toFixed(2)}`;
    }
  }
}
