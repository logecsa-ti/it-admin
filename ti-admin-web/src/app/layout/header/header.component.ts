import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { AuthService } from '@core/auth/auth.service';
import { AppSettingsService } from '@core/config/app-settings.service';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatBadgeModule,
    MatDividerModule,
    MatTooltipModule,
  ],
  template: `
    <button mat-icon-button class="menu-toggle" type="button" aria-label="Mostrar u ocultar menu" (click)="toggleMenu.emit()">
      <mat-icon>menu</mat-icon>
    </button>

    <a routerLink="/dashboard" class="brand" aria-label="Ir al inicio">
      <span class="mark" aria-hidden="true">TI</span>
      <span class="name">{{ settings.appName() }}</span>
    </a>

    <span class="spacer"></span>

    <a
      mat-icon-button
      routerLink="/notificaciones"
      matTooltip="Notificaciones"
      [attr.aria-label]="'Notificaciones: ' + notifications.unread() + ' sin leer'"
    >
      <mat-icon
        [matBadge]="badge()"
        [matBadgeHidden]="notifications.unread() === 0"
        matBadgeSize="small"
        matBadgeColor="warn"
        aria-hidden="false"
      >notifications</mat-icon>
    </a>

    <button mat-button class="user" type="button" [matMenuTriggerFor]="userMenu" aria-label="Menu de usuario">
      <span class="avatar" aria-hidden="true">{{ initials() }}</span>
      <span class="user-name">{{ auth.user()?.fullName }}</span>
      <mat-icon iconPositionEnd>expand_more</mat-icon>
    </button>

    <mat-menu #userMenu="matMenu" xPosition="before">
      <div class="menu-identity" role="presentation">
        <strong>{{ auth.user()?.fullName }}</strong>
        <span>{{ auth.user()?.email }}</span>
      </div>
      <mat-divider />
      <a mat-menu-item routerLink="/perfil"><mat-icon>person</mat-icon>Mi perfil</a>
      <button mat-menu-item type="button" (click)="logout()"><mat-icon>logout</mat-icon>Cerrar sesion</button>
    </mat-menu>
  `,
  styles: `
    :host {
      display: flex;
      align-items: center;
      gap: 8px;
      height: var(--app-header-height);
      padding: 0 12px 0 8px;
      background: var(--app-header-bg);
      color: var(--app-header-fg);
      border-bottom: 1px solid var(--app-olive-800);
      --mat-icon-button-icon-color: #ffffff;
      --mat-button-text-label-text-color: #ffffff;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 10px;
      color: inherit;
      text-decoration: none;
    }
    .mark {
      display: grid;
      place-items: center;
      width: 30px;
      height: 30px;
      border-radius: 4px;
      background: #ffffff;
      color: var(--app-olive-700);
      font-weight: 800;
      font-size: 0.8rem;
      letter-spacing: 0.02em;
    }
    .name {
      font-size: 1rem;
      font-weight: 600;
      letter-spacing: 0.01em;
    }
    .spacer {
      flex: 1;
    }
    .user {
      height: 40px;
    }
    .avatar {
      display: inline-grid;
      place-items: center;
      width: 28px;
      height: 28px;
      margin-right: 8px;
      border-radius: 50%;
      background: var(--app-olive-100);
      color: var(--app-olive-800);
      font-size: 0.75rem;
      font-weight: 700;
    }
    .menu-identity {
      display: flex;
      flex-direction: column;
      padding: 10px 16px 12px;
      span {
        font-size: 0.8rem;
        color: var(--app-text-muted);
      }
    }
    @media (min-width: 961px) {
      .menu-toggle {
        display: none;
      }
    }
    @media (max-width: 600px) {
      .user-name,
      .name {
        display: none;
      }
    }
  `,
})
export class HeaderComponent {
  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(AppSettingsService);
  protected readonly notifications = inject(NotificationService);
  readonly toggleMenu = output<void>();

  protected readonly badge = computed(() => {
    const count = this.notifications.unread();
    return count > 99 ? '99+' : String(count);
  });

  protected readonly initials = computed(() => {
    const user = this.auth.user();
    return user ? `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toUpperCase() : '';
  });

  logout(): void {
    void this.auth.logout();
  }
}
