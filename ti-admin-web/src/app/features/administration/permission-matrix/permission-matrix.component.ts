import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { PermissionDto } from '@core/models';
import { actionLabel, moduleLabel } from '@shared/labels/catalog-labels';

/** Permisos agrupados por modulo con casillas (roles y permisos directos de usuario). */
@Component({
  selector: 'app-permission-matrix',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCheckboxModule],
  template: `
    <div class="modules">
      @for (group of groups(); track group.module) {
        <fieldset [disabled]="disabled()">
          <legend>
            <mat-checkbox
              [checked]="allChecked(group.items)"
              [indeterminate]="someChecked(group.items)"
              [disabled]="disabled()"
              (change)="toggleGroup(group.items, $event.checked)"
            >{{ moduleLabel(group.module) }}</mat-checkbox>
          </legend>
          @for (permission of group.items; track permission.code) {
            <mat-checkbox [checked]="selected().includes(permission.code)" [disabled]="disabled()" (change)="toggle(permission.code, $event.checked)">
              <span class="action">{{ actionLabel(permission.action) }}</span>
              <span class="description">{{ permission.description }}</span>
            </mat-checkbox>
          }
        </fieldset>
      }
    </div>
  `,
  styles: `
    .modules { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 12px; }
    fieldset { margin: 0; padding: 8px 12px 10px; border: 1px solid var(--app-border); border-radius: var(--app-radius); display: flex; flex-direction: column; }
    legend { padding: 0 4px; font-weight: 600; }
    .action { font-weight: 500; margin-right: 6px; }
    .description { color: var(--app-text-subtle); font-size: 0.8rem; }
  `,
})
export class PermissionMatrixComponent {
  readonly permissions = input.required<PermissionDto[]>();
  readonly selected = model.required<string[]>();
  readonly disabled = input(false);
  protected readonly moduleLabel = moduleLabel;
  protected readonly actionLabel = actionLabel;

  protected readonly groups = computed(() => {
    const map = new Map<string, PermissionDto[]>();
    for (const permission of this.permissions()) {
      map.set(permission.module, [...(map.get(permission.module) ?? []), permission]);
    }
    return [...map.entries()].map(([module, items]) => ({ module, items }));
  });

  protected allChecked(items: PermissionDto[]): boolean {
    return items.every((p) => this.selected().includes(p.code));
  }

  protected someChecked(items: PermissionDto[]): boolean {
    const count = items.filter((p) => this.selected().includes(p.code)).length;
    return count > 0 && count < items.length;
  }

  protected toggle(code: string, checked: boolean): void {
    this.selected.update((current) => (checked ? [...new Set([...current, code])] : current.filter((c) => c !== code)));
  }

  protected toggleGroup(items: PermissionDto[], checked: boolean): void {
    const codes = items.map((p) => p.code);
    this.selected.update((current) => (checked ? [...new Set([...current, ...codes])] : current.filter((c) => !codes.includes(c))));
  }
}
