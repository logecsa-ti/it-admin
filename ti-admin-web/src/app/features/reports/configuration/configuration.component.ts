import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { ConfigurationItemDto } from '@core/models';
import { configGroupLabel } from '@shared/labels/catalog-labels';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { FieldDef } from '@shared/components/form-dialog/form-dialog.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { valueOf } from '@shared/utils/resource-utils';
import { ReportsService } from '../reports.service';

/** Parametros del sistema agrupados (SPECS.md 49); los Encrypted se muestran enmascarados. */
@Component({
  selector: 'app-configuration',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule, PageHeaderComponent, StatusBadgeComponent, AppDatePipe],
  template: `
    <div class="page">
      <app-page-header title="Configuracion" subtitle="Parametros del sistema. Los cambios se aplican en minutos (cache de 5 min) y quedan auditados." />
      @if (items.isLoading()) { <mat-progress-bar mode="indeterminate" /> }
      @for (group of groups(); track group.name) {
        <section class="panel">
          <div class="panel-title"><h2>{{ groupLabel(group.name) }}</h2></div>
          <ul class="items">
            @for (item of group.items; track item.key) {
              <li>
                <div class="info">
                  <div class="key mono">{{ item.key }}</div>
                  <div class="description">{{ item.description }}</div>
                  @if (item.updatedAt) { <div class="subtle">Modificado {{ item.updatedAt | appDate }}</div> }
                </div>
                <div class="value">
                  @if (item.dataType === 'Bool') {
                    <app-status-badge [label]="item.value === 'true' ? 'Si' : 'No'" [tone]="item.value === 'true' ? 'success' : 'neutral'" />
                  } @else {
                    <span class="mono">{{ item.value ?? '—' }}</span>
                  }
                  @if (item.value !== item.defaultValue && item.dataType !== 'Encrypted') {
                    <span class="subtle">Por defecto: {{ item.defaultValue ?? '—' }}</span>
                  }
                </div>
                <div class="item-actions">
                  @if (!item.isEditable || item.isRuntimeManaged) {
                    <mat-icon class="locked" matTooltip="Se define en la configuracion de despliegue">lock</mat-icon>
                  } @else if (canManage()) {
                    <button mat-icon-button type="button" matTooltip="Editar" [attr.aria-label]="'Editar ' + item.key" (click)="edit(item)"><mat-icon>edit</mat-icon></button>
                    @if (item.value !== item.defaultValue) {
                      <button mat-icon-button type="button" matTooltip="Restablecer" [attr.aria-label]="'Restablecer ' + item.key" (click)="reset(item)"><mat-icon>restart_alt</mat-icon></button>
                    }
                  }
                </div>
              </li>
            }
          </ul>
        </section>
      }
    </div>
  `,
  styles: `
    .items { list-style: none; margin: 0; padding: 0; }
    li { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1.2fr) 96px; gap: 16px; align-items: center; padding: 12px 20px; }
    li + li { border-top: 1px solid var(--app-border-soft); }
    .key { font-weight: 600; }
    .description { color: var(--app-text-muted); font-size: 0.85rem; }
    .value { display: flex; flex-direction: column; gap: 2px; word-break: break-all; }
    .item-actions { display: flex; justify-content: flex-end; }
    .locked { color: var(--app-text-subtle); }
    @media (max-width: 720px) { li { grid-template-columns: 1fr; } .item-actions { justify-content: flex-start; } }
  `,
})
export class ConfigurationComponent {
  private readonly reports = inject(ReportsService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);

  protected readonly items = rxResource({ stream: () => this.reports.configuration() });
  protected readonly groupLabel = configGroupLabel;
  protected readonly canManage = computed(() => this.auth.hasAny(P.ConfigurationManage));
  protected readonly groups = computed(() => {
    const map = new Map<string, ConfigurationItemDto[]>();
    for (const item of valueOf(this.items) ?? []) map.set(item.group, [...(map.get(item.group) ?? []), item]);
    return [...map.entries()].map(([name, items]) => ({ name, items }));
  });

  edit(item: ConfigurationItemDto): void {
    const field: FieldDef =
      item.dataType === 'Bool'
        ? { key: 'value', label: item.key, type: 'select', required: true, options: [{ value: 'true', label: 'Si' }, { value: 'false', label: 'No' }], wide: true }
        : item.dataType === 'Int'
          ? { key: 'value', label: item.key, type: 'number', required: true, wide: true }
          : item.dataType === 'Json'
            ? { key: 'value', label: item.key, type: 'textarea', maxLength: 4000 }
            : { key: 'value', label: item.key, required: item.dataType !== 'Encrypted', maxLength: 1000, wide: true, hint: item.dataType === 'Encrypted' ? 'Se guarda cifrado; escriba el nuevo valor completo' : undefined };

    this.dialogs
      .form({
        title: 'Editar parametro',
        fields: [field],
        value: { value: item.dataType === 'Encrypted' ? null : item.value },
        save: (value) => this.reports.updateConfiguration(item.key, value['value'] == null ? null : String(value['value'])),
      })
      .subscribe(() => {
        this.toast.success('Parametro actualizado.');
        this.items.reload();
      });
  }

  reset(item: ConfigurationItemDto): void {
    this.confirmService
      .confirm({ title: 'Restablecer parametro', message: `¿Volver ${item.key} a su valor por defecto (${item.defaultValue ?? 'vacio'})?`, confirmText: 'Restablecer' })
      .subscribe((ok) => {
        if (ok) this.reports.resetConfiguration(item.key).subscribe(() => {
          this.toast.success('Parametro restablecido.');
          this.items.reload();
        });
      });
  }
}
