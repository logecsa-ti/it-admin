import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';

export interface WorkflowAction {
  id: string;
  label: string;
  icon: string;
  /** Boton principal visible; el resto va al menu "Mas acciones". */
  primary?: boolean;
  destructive?: boolean;
}

/** Botones de transicion de un flujo (cambios, compras, mantenimientos). */
@Component({
  selector: 'app-workflow-actions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatMenuModule],
  template: `
    @for (action of primary(); track action.id) {
      <button mat-flat-button type="button" (click)="run.emit(action.id)"><mat-icon>{{ action.icon }}</mat-icon>{{ action.label }}</button>
    }
    @if (secondary().length > 0) {
      <button mat-stroked-button type="button" [matMenuTriggerFor]="menu"><mat-icon>more_horiz</mat-icon>Acciones</button>
      <mat-menu #menu="matMenu" xPosition="before">
        @for (action of secondary(); track action.id) {
          <button mat-menu-item type="button" [class.destructive]="action.destructive" (click)="run.emit(action.id)">
            <mat-icon>{{ action.icon }}</mat-icon>{{ action.label }}
          </button>
        }
      </mat-menu>
    }
  `,
  styles: `
    :host { display: contents; }
    .destructive { color: var(--app-tone-danger-fg); }
    .destructive mat-icon { color: inherit; }
  `,
})
export class WorkflowActionsComponent {
  readonly actions = input.required<WorkflowAction[]>();
  readonly run = output<string>();

  protected readonly primary = computed(() => this.actions().filter((a) => a.primary));
  protected readonly secondary = computed(() => this.actions().filter((a) => !a.primary));
}
