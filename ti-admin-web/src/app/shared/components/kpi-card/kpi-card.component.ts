import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { Tone } from '@shared/labels/enum-labels';

/** Indicador del dashboard (DashboardCard, SPECS.md 30). */
@Component({
  selector: 'app-kpi-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, RouterLink, NgTemplateOutlet],
  host: { '[class]': '"tone-" + tone()' },
  template: `
    @if (link()) {
      <a class="card" [routerLink]="link()" [queryParams]="linkParams()">
        <ng-container *ngTemplateOutlet="body" />
      </a>
    } @else {
      <div class="card"><ng-container *ngTemplateOutlet="body" /></div>
    }
    <ng-template #body>
      <div class="top">
        <span class="label">{{ label() }}</span>
        <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
      </div>
      <div class="value">{{ value() ?? '—' }}</div>
      @if (hint()) {
        <div class="hint">{{ hint() }}</div>
      }
    </ng-template>
  `,
  styles: `
    :host {
      display: block;
      --accent: var(--app-olive-600);
    }
    :host(.tone-warning) { --accent: var(--app-tone-warning-fg); }
    :host(.tone-danger) { --accent: var(--app-tone-danger-fg); }
    :host(.tone-info) { --accent: var(--app-tone-info-fg); }
    :host(.tone-success) { --accent: var(--app-tone-success-fg); }
    .card {
      display: block;
      height: 100%;
      padding: 16px 18px;
      background: var(--app-surface);
      border: 1px solid var(--app-border);
      border-left: 3px solid var(--accent);
      border-radius: var(--app-radius);
      color: inherit;
      text-decoration: none;
    }
    a.card:hover {
      border-color: var(--app-olive-500);
      border-left-color: var(--accent);
      background: var(--app-olive-50);
    }
    .top {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 8px;
    }
    .label {
      font-size: 0.78rem;
      font-weight: 600;
      color: var(--app-text-muted);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    mat-icon {
      color: var(--accent);
      font-size: 20px;
      width: 20px;
      height: 20px;
    }
    .value {
      margin-top: 8px;
      font-size: 1.75rem;
      font-weight: 600;
      color: var(--app-text);
      line-height: 1.1;
    }
    .hint {
      margin-top: 4px;
      font-size: 0.8rem;
      color: var(--app-text-subtle);
    }
  `,
})
export class KpiCardComponent {
  readonly label = input.required<string>();
  readonly value = input<string | number | null | undefined>();
  readonly icon = input('insights');
  readonly hint = input<string | null>();
  readonly tone = input<Tone>('neutral');
  readonly link = input<string | null>();
  readonly linkParams = input<Record<string, string | number | boolean> | null>();
}
