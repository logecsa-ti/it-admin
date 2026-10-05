import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { AuditLogDto } from '@core/models';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';

interface FieldChange {
  field: string;
  before: string;
  after: string;
  changed: boolean;
}

/** Detalle de un registro de auditoria con los valores antes/despues campo por campo. */
@Component({
  selector: 'app-audit-detail-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, StatusBadgeComponent, AppDatePipe],
  template: `
    <h2 mat-dialog-title>{{ log.entityName }} #{{ log.entityId }}</h2>
    <mat-dialog-content>
      <dl class="meta">
        <div><dt>Accion</dt><dd><app-status-badge kind="AuditAction" [value]="log.action" /></dd></div>
        <div><dt>Fecha</dt><dd>{{ log.timestamp | appDate }}</dd></div>
        <div><dt>Usuario</dt><dd>{{ log.userName ?? 'Sistema' }}</dd></div>
        <div><dt>Modulo</dt><dd>{{ log.module }}</dd></div>
        <div><dt>IP</dt><dd class="mono">{{ log.ipAddress ?? '—' }}</dd></div>
        <div><dt>Correlacion</dt><dd class="mono">{{ log.correlationId ?? '—' }}</dd></div>
      </dl>
      @if (changes().length > 0) {
        <table class="diff" aria-label="Cambios">
          <thead><tr><th>Campo</th><th>Antes</th><th>Despues</th></tr></thead>
          <tbody>
            @for (change of changes(); track change.field) {
              <tr [class.changed]="change.changed">
                <td class="field">{{ change.field }}</td>
                <td class="before">{{ change.before }}</td>
                <td class="after">{{ change.after }}</td>
              </tr>
            }
          </tbody>
        </table>
      } @else {
        <p class="subtle">Este evento no registra valores.</p>
      }
      @if (log.userAgent) { <p class="subtle agent">{{ log.userAgent }}</p> }
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button type="button" mat-dialog-close>Cerrar</button></mat-dialog-actions>
  `,
  styles: `
    .meta { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px 20px; margin: 0 0 16px; }
    .meta dt { font-size: 0.72rem; font-weight: 600; text-transform: uppercase; color: var(--app-text-subtle); }
    .meta dd { margin: 2px 0 0; }
    .diff { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .diff th { text-align: left; padding: 6px 8px; border-bottom: 1px solid var(--app-border); color: var(--app-text-muted); }
    .diff td { padding: 6px 8px; border-bottom: 1px solid var(--app-border-soft); vertical-align: top; word-break: break-word; }
    .field { font-weight: 600; white-space: nowrap; }
    tr.changed .before { background: var(--app-tone-danger-bg); }
    tr.changed .after { background: var(--app-tone-success-bg); }
    .agent { margin-top: 12px; font-size: 0.75rem; }
  `,
})
export class AuditDetailDialogComponent {
  protected readonly log = inject<AuditLogDto>(MAT_DIALOG_DATA);

  protected readonly changes = computed<FieldChange[]>(() => {
    const before = parse(this.log.oldValues);
    const after = parse(this.log.newValues);
    const fields = [...new Set([...Object.keys(before), ...Object.keys(after)])];
    return fields.map((field) => {
      const b = format(before[field]);
      const a = format(after[field]);
      return { field, before: b, after: a, changed: b !== a };
    });
  });
}

function parse(json: string | null | undefined): Record<string, unknown> {
  if (!json) return {};
  try {
    const value = JSON.parse(json) as unknown;
    return value && typeof value === 'object' ? (value as Record<string, unknown>) : { valor: value };
  } catch {
    return { valor: json };
  }
}

function format(value: unknown): string {
  if (value === undefined) return '';
  if (value === null) return '—';
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}
