import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

/** Paginas de acceso denegado (403) y no encontrada (404); el contenido viene de `data`. */
@Component({
  selector: 'app-status-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <div class="panel status">
      <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
      <div class="code">{{ code() }}</div>
      <h1>{{ heading() }}</h1>
      <p class="muted">{{ message() }}</p>
      <a mat-flat-button routerLink="/dashboard">Volver al inicio</a>
    </div>
  `,
  styles: `
    .status {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      max-width: 520px;
      margin: 48px auto;
      padding: 40px 32px;
      text-align: center;
    }
    mat-icon {
      width: 48px;
      height: 48px;
      font-size: 48px;
      color: var(--app-olive-500);
    }
    .code {
      font-size: 0.8rem;
      font-weight: 700;
      letter-spacing: 0.1em;
      color: var(--app-text-subtle);
    }
    p {
      margin: 0 0 12px;
    }
  `,
})
export class StatusPageComponent {
  readonly code = input('404');
  readonly icon = input('travel_explore');
  readonly heading = input('Pagina no encontrada');
  readonly message = input('La direccion que busca no existe o fue movida.');
}
