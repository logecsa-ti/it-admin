import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { EnumName, Tone, enumLabel, enumTone } from '@shared/labels/enum-labels';

/** Estado con color semantico apagado: `<app-status-badge kind="TicketStatus" [value]="t.status" />`. */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': '"badge tone-" + resolvedTone()', role: 'status' },
  template: `<span class="dot" aria-hidden="true"></span>{{ resolvedLabel() }}`,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 2px 9px 2px 7px;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
      line-height: 1.5;
      white-space: nowrap;
      background: var(--tone-bg);
      color: var(--tone-fg);
    }
    .dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
      opacity: 0.8;
    }
    :host(.tone-neutral) { --tone-bg: var(--app-tone-neutral-bg); --tone-fg: var(--app-tone-neutral-fg); }
    :host(.tone-info) { --tone-bg: var(--app-tone-info-bg); --tone-fg: var(--app-tone-info-fg); }
    :host(.tone-success) { --tone-bg: var(--app-tone-success-bg); --tone-fg: var(--app-tone-success-fg); }
    :host(.tone-warning) { --tone-bg: var(--app-tone-warning-bg); --tone-fg: var(--app-tone-warning-fg); }
    :host(.tone-danger) { --tone-bg: var(--app-tone-danger-bg); --tone-fg: var(--app-tone-danger-fg); }
    :host(.tone-accent) { --tone-bg: var(--app-tone-accent-bg); --tone-fg: var(--app-tone-accent-fg); }
  `,
})
export class StatusBadgeComponent {
  readonly kind = input<EnumName>();
  readonly value = input<string | null | undefined>();
  /** Alternativa sin enum: etiqueta y tono explicitos. */
  readonly label = input<string>();
  readonly tone = input<Tone>();

  protected readonly resolvedLabel = computed(() => {
    const kind = this.kind();
    return this.label() ?? (kind ? enumLabel(kind, this.value()) : (this.value() ?? '—'));
  });

  protected readonly resolvedTone = computed(() => {
    const kind = this.kind();
    return this.tone() ?? (kind ? enumTone(kind, this.value()) : 'neutral');
  });
}
