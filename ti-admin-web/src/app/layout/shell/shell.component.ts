import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { RouterOutlet } from '@angular/router';
import { interval, map } from 'rxjs';
import { LoadingService } from '@core/services/loading.service';
import { NotificationService } from '@core/services/notification.service';
import { BreadcrumbsComponent } from '../breadcrumbs/breadcrumbs.component';
import { HeaderComponent } from '../header/header.component';
import { SidebarComponent } from '../sidebar/sidebar.component';

const NOTIFICATION_POLL_MS = 60_000;

/** Marco de la aplicacion autenticada: encabezado, menu lateral, migas y pie. */
@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, MatSidenavModule, MatProgressBarModule, HeaderComponent, SidebarComponent, BreadcrumbsComponent],
  template: `
    <app-header (toggleMenu)="sidenav.toggle()" />
    <div class="loading-bar">
      @if (loading.isLoading()) {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando" />
      }
    </div>
    <mat-sidenav-container class="container">
      <mat-sidenav
        #sidenav
        class="sidenav"
        [mode]="isMobile() ? 'over' : 'side'"
        [opened]="!isMobile()"
        [fixedInViewport]="isMobile()"
        fixedTopGap="56"
      >
        <app-sidebar (navigate)="isMobile() && sidenav.close()" />
      </mat-sidenav>
      <mat-sidenav-content class="content">
        <main id="contenido">
          <app-breadcrumbs />
          <router-outlet />
        </main>
        <footer>
          <span>TI Admin · Sistema Administrativo de Tecnologias de Informacion</span>
          <span>© {{ year }}</span>
        </footer>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100vh;
    }
    .loading-bar {
      position: relative;
      height: 0;
      z-index: 5;
      mat-progress-bar {
        position: absolute;
        top: 0;
      }
    }
    .container {
      flex: 1;
      background: var(--app-page-bg);
    }
    .sidenav {
      width: var(--app-sidebar-width);
      background: var(--app-sidebar-bg);
      border-right: 1px solid var(--app-border);
    }
    .content {
      display: flex;
      flex-direction: column;
    }
    main {
      flex: 1;
      padding: 20px 28px 32px;
    }
    footer {
      display: flex;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 8px;
      padding: 12px 28px;
      border-top: 1px solid var(--app-border);
      background: var(--app-surface);
      font-size: 0.75rem;
      color: var(--app-text-subtle);
    }
    @media (max-width: 960px) {
      main {
        padding: 16px;
      }
      footer {
        padding: 12px 16px;
      }
    }
  `,
})
export class ShellComponent implements OnInit {
  protected readonly loading = inject(LoadingService);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly year = new Date().getFullYear();

  protected readonly isMobile = toSignal(
    inject(BreakpointObserver)
      .observe('(max-width: 960px)')
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  ngOnInit(): void {
    this.notifications.refreshUnread();
    interval(NOTIFICATION_POLL_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.notifications.refreshUnread());
  }
}
