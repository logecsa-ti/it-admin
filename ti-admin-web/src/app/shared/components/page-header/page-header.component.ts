import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/** Titulo de pagina con enlace de regreso opcional y acciones proyectadas a la derecha. */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatIconModule],
  template: `
    <div class="heading">
      @if (backLink()) {
        <a class="back" [routerLink]="backLink()" [attr.aria-label]="'Volver a ' + (backLabel() ?? 'la lista')">
          <mat-icon>arrow_back</mat-icon>
        </a>
      }
      <div>
        @if (eyebrow()) {
          <div class="eyebrow">{{ eyebrow() }}</div>
        }
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p class="subtitle">{{ subtitle() }}</p>
        }
      </div>
    </div>
    <div class="header-actions"><ng-content /></div>
  `,
  styles: `
    :host {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 12px 24px;
      padding-bottom: 4px;
    }
    .heading {
      display: flex;
      align-items: flex-start;
      gap: 10px;
    }
    .back {
      display: inline-flex;
      margin-top: 2px;
      padding: 4px;
      border-radius: 4px;
      color: var(--app-text-muted);
      &:hover { background: var(--app-olive-100); color: var(--app-olive-700); }
    }
    .eyebrow {
      font-size: 0.72rem;
      font-weight: 600;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--app-olive-600);
      margin-bottom: 2px;
    }
    .subtitle {
      margin: 4px 0 0;
      color: var(--app-text-muted);
    }
    .header-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>();
  readonly eyebrow = input<string | null>();
  readonly backLink = input<string | unknown[] | null>();
  readonly backLabel = input<string>();
}
