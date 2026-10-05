import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

interface Crumb {
  label: string;
  url: string;
}

/** Migas de pan desde `data.breadcrumb` de cada nivel de ruta. */
@Component({
  selector: 'app-breadcrumbs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatIconModule],
  template: `
    @if (crumbs().length > 0) {
      <nav aria-label="Ruta de navegacion">
        <ol>
          <li><a routerLink="/dashboard">Inicio</a></li>
          @for (crumb of crumbs(); track crumb.url; let last = $last) {
            <li>
              <mat-icon aria-hidden="true">chevron_right</mat-icon>
              @if (last) {
                <span aria-current="page">{{ crumb.label }}</span>
              } @else {
                <a [routerLink]="crumb.url">{{ crumb.label }}</a>
              }
            </li>
          }
        </ol>
      </nav>
    }
  `,
  styles: `
    ol {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 2px;
      list-style: none;
      margin: 0 0 12px;
      padding: 0;
      font-size: 0.8rem;
      color: var(--app-text-subtle);
    }
    li {
      display: inline-flex;
      align-items: center;
      gap: 2px;
    }
    a {
      color: var(--app-text-muted);
      text-decoration: none;
      &:hover {
        color: var(--app-olive-700);
        text-decoration: underline;
      }
    }
    mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }
    [aria-current] {
      color: var(--app-text);
      font-weight: 500;
    }
  `,
})
export class BreadcrumbsComponent {
  private readonly router = inject(Router);
  // Se recorre el snapshot del router: los ActivatedRoute hijos aun no tienen snapshot en la primera navegacion.
  protected readonly crumbs = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      startWith(null),
      map(() => this.build(this.router.routerState.snapshot.root)),
    ),
    { initialValue: [] as Crumb[] },
  );

  private build(route: ActivatedRouteSnapshot, url = '', crumbs: Crumb[] = []): Crumb[] {
    for (const child of route.children) {
      const segment = child.url.map((part) => part.path).join('/');
      const next = segment ? `${url}/${segment}` : url;
      const label = child.data['breadcrumb'] as string | undefined;
      if (label && segment && crumbs.at(-1)?.label !== label) {
        crumbs.push({ label, url: next });
      }
      return this.build(child, next, crumbs);
    }
    return crumbs;
  }
}
