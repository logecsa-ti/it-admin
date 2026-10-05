import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService, saveFile } from '@core/api/api.service';
import { AuthService } from '@core/auth/auth.service';
import { DocumentDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { valueOf } from '@shared/utils/resource-utils';

/**
 * Documentos adjuntos de una entidad (FileUploader, SPECS.md 30). El acceso lo decide la API segun la
 * entidad duena (ADR-034); `canUpload` solo controla si se muestra el boton.
 */
@Component({
  selector: 'app-documents-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule, AppDatePipe],
  template: `
    <section class="panel">
      <div class="panel-title">
        <h2>Documentos</h2>
        @if (canUpload()) {
          <input #file type="file" hidden [accept]="accept" (change)="upload(file)" />
          <button mat-stroked-button type="button" [disabled]="uploading()" (click)="file.click()">
            <mat-icon>upload_file</mat-icon>Adjuntar
          </button>
        }
      </div>
      @if (uploading() || documents.isLoading()) {
        <mat-progress-bar mode="indeterminate" />
      }
      @if (items().length === 0 && !documents.isLoading()) {
        <p class="subtle empty">No hay documentos adjuntos.</p>
      } @else {
        <ul>
          @for (doc of items(); track doc.id) {
            <li>
              <mat-icon aria-hidden="true">{{ icon(doc.mimeType) }}</mat-icon>
              <div class="info">
                <button type="button" class="name" (click)="download(doc)">{{ doc.fileName }}</button>
                <span class="subtle">{{ size(doc.size) }} · {{ doc.uploadedByName }} · {{ doc.uploadedAt | appDate }}</span>
              </div>
              @if (canDelete(doc)) {
                <button mat-icon-button type="button" matTooltip="Eliminar" [attr.aria-label]="'Eliminar ' + doc.fileName" (click)="remove(doc)">
                  <mat-icon>delete</mat-icon>
                </button>
              }
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: `
    ul { list-style: none; margin: 0; padding: 4px 0; }
    li { display: flex; align-items: center; gap: 12px; padding: 8px 20px; }
    li + li { border-top: 1px solid var(--app-border-soft); }
    li > mat-icon { color: var(--app-text-subtle); }
    .info { display: flex; flex-direction: column; flex: 1; min-width: 0; }
    .name {
      all: unset; cursor: pointer; color: var(--mat-sys-primary); font-weight: 500;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      &:hover, &:focus-visible { text-decoration: underline; }
    }
    .empty { margin: 0; padding: 16px 20px; }
  `,
})
export class DocumentsPanelComponent {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly entityName = input.required<string>();
  readonly entityId = input.required<number>();
  readonly canUpload = input(false);
  /** Puede eliminar documentos de otros (gestion de la entidad). */
  readonly canManage = input(false);

  protected readonly accept = '.pdf,.doc,.docx,.xls,.xlsx,.csv,.txt,.png,.jpg,.jpeg,.zip';
  protected readonly uploading = signal(false);
  protected readonly documents = rxResource({
    params: () => ({ entityName: this.entityName(), entityId: this.entityId() }),
    stream: ({ params }) => this.api.get<DocumentDto[]>('documents', params),
  });
  protected readonly items = computed(() => valueOf(this.documents) ?? []);

  /** Vuelve a consultar la lista (p. ej. cuando el servidor adjunta un acta al asignar). */
  reload(): void {
    this.documents.reload();
  }

  upload(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    const form = new FormData();
    form.append('file', file);
    form.append('entityName', this.entityName());
    form.append('entityId', String(this.entityId()));
    this.uploading.set(true);
    this.api.upload<DocumentDto>('documents', form).subscribe({
      next: () => {
        this.uploading.set(false);
        this.toast.success('Documento adjuntado.');
        this.documents.reload();
      },
      error: () => this.uploading.set(false),
    });
  }

  download(doc: DocumentDto): void {
    this.api.download(`documents/${doc.id}/download`).subscribe((file) => saveFile({ ...file, fileName: doc.fileName }));
  }

  remove(doc: DocumentDto): void {
    this.confirmService
      .confirm({ title: 'Eliminar documento', message: `¿Eliminar "${doc.fileName}"?`, confirmText: 'Eliminar', destructive: true })
      .subscribe((ok) => {
        if (ok) {
          this.api.delete(`documents/${doc.id}`).subscribe(() => {
            this.toast.success('Documento eliminado.');
            this.documents.reload();
          });
        }
      });
  }

  protected canDelete(doc: DocumentDto): boolean {
    return this.canManage() || doc.uploadedById === this.auth.user()?.id;
  }

  protected icon(mime: string): string {
    if (mime.startsWith('image/')) return 'image';
    if (mime.includes('pdf')) return 'picture_as_pdf';
    if (mime.includes('sheet') || mime.includes('excel') || mime.includes('csv')) return 'table_chart';
    if (mime.includes('zip')) return 'folder_zip';
    return 'description';
  }

  protected size(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }
}
