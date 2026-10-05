import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
    <p class="title">{{ title() }}</p>
    @if (message()) {
      <p class="message">{{ message() }}</p>
    }
    <ng-content />
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 40px 16px;
      text-align: center;
      color: var(--app-text-muted);
    }
    mat-icon {
      width: 40px;
      height: 40px;
      font-size: 40px;
      color: var(--app-text-subtle);
      margin-bottom: 4px;
    }
    .title {
      margin: 0;
      font-weight: 600;
      color: var(--app-text);
    }
    .message {
      margin: 0;
      max-width: 420px;
    }
  `,
})
export class EmptyStateComponent {
  readonly icon = input('inbox');
  readonly title = input('Sin resultados');
  readonly message = input<string | null>();
}
