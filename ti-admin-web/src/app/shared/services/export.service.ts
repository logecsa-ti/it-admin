import { Injectable, inject, signal } from '@angular/core';
import { ApiService, saveFile, toDownloadedFile } from '@core/api/api.service';
import { QueryParams } from '@core/models';
import { ToastService } from '@core/services/toast.service';

export type ExportFormat = 'xlsx' | 'csv';

/**
 * Exportacion de reportes (GET /reports/{report}/export). Hasta Exports.AsyncThreshold filas la API
 * devuelve el archivo; por encima responde 202 con un trabajo y notifica cuando esta listo (ADR-036).
 */
@Injectable({ providedIn: 'root' })
export class ExportService {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);
  readonly busy = signal(false);

  export(report: string, format: ExportFormat, filters?: QueryParams): void {
    this.busy.set(true);
    this.api.getResponse(`reports/${report}/export`, { ...filters, format }).subscribe({
      next: (response) => {
        this.busy.set(false);
        if (response.status === 202) {
          this.toast.info('La exportacion es grande y se esta generando. Recibira una notificacion con el enlace de descarga.');
          return;
        }
        saveFile(toDownloadedFile(response));
      },
      error: () => this.busy.set(false),
    });
  }

  /** Descarga de un trabajo asincrono terminado (enlace de la notificacion). */
  downloadJob(jobId: number): void {
    this.api.download(`reports/exports/${jobId}/download`).subscribe((file) => saveFile(file));
  }
}
