import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '@env/environment';
import { ApiEnvelope } from '@core/models';

/**
 * Parametros publicos del backend (/configuration/public): zona horaria de negocio y nombre del sistema.
 * Las fechas llegan en UTC y se muestran en esta zona (ADR-005).
 */
@Injectable({ providedIn: 'root' })
export class AppSettingsService {
  private readonly http = inject(HttpClient);
  private readonly values = signal<Record<string, string | null>>({});

  readonly timeZone = computed(
    () => validTimeZone(this.values()['App.TimeZone']) ?? environment.timeZone,
  );
  readonly appName = computed(() => this.values()['App.Name'] || 'TI Admin');

  async load(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.http.get<ApiEnvelope<Record<string, string | null>>>(
          `${environment.apiUrl}/configuration/public`,
        ),
      );
      this.values.set(response.data ?? {});
    } catch {
      // Sin backend la app arranca con los valores del environment; el login mostrara el error.
    }
  }

  get(key: string): string | null {
    return this.values()[key] ?? null;
  }
}

function validTimeZone(zone: string | null | undefined): string | null {
  if (!zone) {
    return null;
  }
  try {
    new Intl.DateTimeFormat('es', { timeZone: zone });
    return zone;
  } catch {
    return null;
  }
}
