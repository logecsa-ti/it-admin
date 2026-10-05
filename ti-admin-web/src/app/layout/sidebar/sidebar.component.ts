import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { NAVIGATION } from '../navigation';

/** Menu lateral dinamico segun permisos (SPECS.md 31). */
@Component({
  selector: 'app-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, MatIconModule],
  template: `
    <nav aria-label="Menu principal">
      @for (group of groups(); track group.label) {
        <div class="group">
          <div class="group-label">{{ group.label }}</div>
          <ul>
            @for (item of group.items; track item.route) {
              <li>
                <a
                  [routerLink]="item.route"
                  [queryParams]="item.queryParams"
                  routerLinkActive="active"
                  [routerLinkActiveOptions]="{ exact: !!item.exact }"
                  ariaCurrentWhenActive="page"
                  (click)="navigate.emit()"
                >
                  <mat-icon aria-hidden="true">{{ item.icon }}</mat-icon>
                  <span>{{ item.label }}</span>
                </a>
              </li>
            }
          </ul>
        </div>
      }
    </nav>
  `,
  styles: `
    :host {
      display: block;
      padding: 12px 10px 24px;
    }
    .group + .group {
      margin-top: 14px;
    }
    .group-label {
      padding: 6px 12px;
      font-size: 0.7rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--app-text-subtle);
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    a {
      position: relative;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px 12px;
      border-radius: 4px;
      color: var(--app-text);
      font-weight: 500;
      text-decoration: none;
      &:hover {
        background: var(--app-olive-50);
      }
      &.active {
        background: var(--app-olive-100);
        color: var(--app-olive-800);
        font-weight: 600;
        &::before {
          content: '';
          position: absolute;
          left: -10px;
          top: 6px;
          bottom: 6px;
          width: 3px;
          border-radius: 0 2px 2px 0;
          background: var(--app-olive-600);
        }
        mat-icon {
          color: var(--app-olive-700);
        }
      }
    }
    mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
      color: var(--app-text-muted);
    }
  `,
})
export class SidebarComponent {
  private readonly auth = inject(AuthService);
  readonly navigate = output<void>();

  protected readonly groups = computed(() => {
    this.auth.permissions(); // recalcula si cambia la sesion
    return NAVIGATION.map((group) => ({
      ...group,
      items: group.items.filter((item) => this.auth.hasAny(...(item.permissions ?? []))),
    })).filter((group) => group.items.length > 0);
  });
}
