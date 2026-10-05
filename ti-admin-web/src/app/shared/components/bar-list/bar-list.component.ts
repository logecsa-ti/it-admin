import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface BarItem {
  label: string;
  count: number;
}

/** Grafico de barras horizontales sobrio (Chart, SPECS.md 30), sin dependencias. */
@Component({
  selector: 'app-bar-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (bars().length === 0) {
      <p class="subtle empty">Sin datos.</p>
    } @else {
      <ul [attr.aria-label]="caption()">
        @for (bar of bars(); track bar.label) {
          <li>
            <div class="row">
              <span class="label">{{ bar.label }}</span>
              <span class="count">{{ bar.count }}</span>
            </div>
            <div class="track" aria-hidden="true">
              <div class="fill" [style.width.%]="bar.percent"></div>
            </div>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    ul {
      display: flex;
      flex-direction: column;
      gap: 10px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      font-size: 0.85rem;
      margin-bottom: 4px;
    }
    .label {
      color: var(--app-text);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .count {
      font-weight: 600;
      color: var(--app-text-muted);
      font-variant-numeric: tabular-nums;
    }
    .track {
      height: 6px;
      border-radius: 3px;
      background: var(--app-border-soft);
      overflow: hidden;
    }
    .fill {
      height: 100%;
      border-radius: 3px;
      background: var(--app-olive-500);
    }
    .empty {
      margin: 0;
    }
  `,
})
export class BarListComponent {
  readonly items = input.required<readonly BarItem[]>();
  readonly caption = input('Distribucion');
  /** Traduce etiquetas (p. ej. valores de enum). */
  readonly labelFn = input<(label: string) => string>((label) => label);
  readonly limit = input(8);

  protected readonly bars = computed(() => {
    const items = [...this.items()].sort((a, b) => b.count - a.count).slice(0, this.limit());
    const max = Math.max(1, ...items.map((item) => item.count));
    return items.map((item) => ({
      label: this.labelFn()(item.label),
      count: item.count,
      percent: Math.round((item.count / max) * 100),
    }));
  });
}
