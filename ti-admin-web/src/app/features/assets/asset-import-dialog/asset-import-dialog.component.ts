import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { saveFile } from '@core/api/api.service';
import { toApiError } from '@core/api/api-error';
import { ToastService } from '@core/services/toast.service';
import { AssetsService } from '../assets.service';

/**
 * Importacion de activos desde CSV/Excel (ADR-037): primero se valida (dryRun) y solo si no hay errores
 * se habilita importar. Todo o nada: con un error no se importa ninguna fila.
 */
@Component({
  selector: 'app-asset-import-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title>Importar activos</h2>
    <mat-dialog-content>
      <ol class="steps">
        <li>
          Descargue la plantilla:
          <button mat-button type="button" (click)="template('xlsx')">Excel</button>
          <button mat-button type="button" (click)="template('csv')">CSV</button>
        </li>
        <li>Complete una fila por activo. Columnas obligatorias: <strong>AssetCode</strong>, <strong>Name</strong> y <strong>AssetType</strong> (codigo del tipo).</li>
        <li>Valide el archivo; si no hay errores podra importarlo.</li>
      </ol>

      <div class="file-row">
        <input #fileInput type="file" accept=".csv,.xlsx" hidden (change)="choose(fileInput)" />
        <button mat-stroked-button type="button" (click)="fileInput.click()"><mat-icon>attach_file</mat-icon>Seleccionar archivo</button>
        <span class="muted">{{ file()?.name ?? 'Ningun archivo seleccionado' }}</span>
      </div>

      @if (busy()) {
        <mat-progress-bar mode="indeterminate" />
      }

      @if (validated() && errors().length === 0) {
        <div class="result ok" role="status">
          <mat-icon>check_circle</mat-icon>
          {{ rows() }} filas validas. Puede importar el archivo.
        </div>
      }

      @if (errors().length > 0) {
        <div class="result error" role="alert">
          <mat-icon>error</mat-icon>
          {{ message() }}
        </div>
        <ul class="errors">
          @for (error of errors(); track $index) {
            <li>{{ error }}</li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cerrar</button>
      <button mat-stroked-button type="button" [disabled]="!file() || busy()" (click)="run(true)">Validar</button>
      <button mat-flat-button type="button" [disabled]="!validated() || errors().length > 0 || busy()" (click)="run(false)">Importar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .steps { margin: 0 0 16px; padding-left: 20px; color: var(--app-text-muted); line-height: 2; }
    .file-row { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; }
    .result { display: flex; align-items: center; gap: 8px; margin-top: 12px; padding: 10px 12px; border-radius: 4px; }
    .ok { background: var(--app-tone-success-bg); color: var(--app-tone-success-fg); }
    .error { background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg); }
    .errors {
      max-height: 220px; overflow: auto; margin: 8px 0 0; padding: 8px 12px 8px 28px;
      border: 1px solid var(--app-border); border-radius: 4px; font-size: 0.85rem;
    }
  `,
})
export class AssetImportDialogComponent {
  private readonly assets = inject(AssetsService);
  private readonly toast = inject(ToastService);
  private readonly dialogRef = inject(MatDialogRef<AssetImportDialogComponent, boolean>);

  protected readonly file = signal<File | null>(null);
  protected readonly busy = signal(false);
  protected readonly validated = signal(false);
  protected readonly rows = signal(0);
  protected readonly errors = signal<string[]>([]);
  protected readonly message = signal('');

  template(format: 'xlsx' | 'csv'): void {
    this.assets.importTemplate(format).subscribe((file) => saveFile(file));
  }

  choose(input: HTMLInputElement): void {
    this.file.set(input.files?.[0] ?? null);
    input.value = '';
    this.validated.set(false);
    this.errors.set([]);
  }

  run(dryRun: boolean): void {
    const file = this.file();
    if (!file) return;
    this.busy.set(true);
    this.errors.set([]);
    this.assets.import(file, dryRun).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.rows.set(result.totalRows);
        if (dryRun) {
          this.validated.set(true);
        } else {
          this.toast.success(`${result.imported} activos importados.`);
          this.dialogRef.close(true);
        }
      },
      error: (error: unknown) => {
        const info = toApiError(error);
        this.busy.set(false);
        this.validated.set(true);
        this.message.set(info.message);
        this.errors.set(info.details.length ? info.details.map((d) => d.message) : [info.message]);
      },
    });
  }
}
