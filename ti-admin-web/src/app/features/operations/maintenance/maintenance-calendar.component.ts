import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AppSettingsService } from '@core/config/app-settings.service';
import { MaintenanceDto, QueryParams } from '@core/models';
import { enumLabel, enumTone } from '@shared/labels/enum-labels';
import { OperationsService } from '../operations.service';

/** Tope de la API (PagedQuery.MaxPageSize): si el periodo tiene mas, se avisa. */
const MAX_ITEMS = 200;
const DAY_MS = 86_400_000;
const WEEKDAYS = ['Lun', 'Mar', 'Mie', 'Jue', 'Vie', 'Sab', 'Dom'];

interface CalendarDay {
  key: string;
  day: number;
  inMonth: boolean;
  isToday: boolean;
  items: MaintenanceDto[];
}

/**
 * Calendario mensual de mantenimientos (IMPLEMENTATION_PLAN Fase 9: "Maintenance calendar/list").
 * Agrupa por fecha programada en la zona de negocio (App.TimeZone), no en la del navegador.
 */
@Component({
  selector: 'app-maintenance-calendar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, MatTooltipModule],
  template: `
    <div class="toolbar">
      <div class="nav">
        <button mat-icon-button type="button" aria-label="Mes anterior" (click)="move(-1)"><mat-icon>chevron_left</mat-icon></button>
        <button mat-icon-button type="button" aria-label="Mes siguiente" (click)="move(1)"><mat-icon>chevron_right</mat-icon></button>
        <h2 aria-live="polite">{{ monthLabel() }}</h2>
      </div>
      <button mat-stroked-button type="button" (click)="goToday()">Hoy</button>
    </div>
    <div class="progress">@if (data.isLoading()) { <mat-progress-bar mode="indeterminate" /> }</div>
    @if (truncated()) {
      <p class="subtle notice">Se muestran {{ max }} de {{ total() }} mantenimientos del periodo. Use los filtros para acotar.</p>
    }
    <div class="grid" role="grid" [attr.aria-label]="'Mantenimientos de ' + monthLabel()">
      <div class="row" role="row">
        @for (name of weekdays; track name) { <div class="weekday" role="columnheader">{{ name }}</div> }
      </div>
      @for (week of weeks(); track $index) {
        <div class="row" role="row">
          @for (day of week; track day.key) {
            <div class="day" role="gridcell" [class.outside]="!day.inMonth" [class.today]="day.isToday">
              <span class="number">{{ day.day }}</span>
              <div class="events">
                @for (m of day.items; track m.id) {
                  <button
                    type="button"
                    class="event"
                    [class]="'tone-' + tone(m)"
                    [class.overdue]="m.isOverdue"
                    [matTooltip]="m.number + ' · ' + m.assetCode + ' · ' + statusLabel(m)"
                    [attr.aria-label]="time(m) + ' ' + m.title + ', ' + statusLabel(m)"
                    (click)="select.emit(m)"
                  >
                    <span class="time">{{ time(m) }}</span>{{ m.title }}
                  </button>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .toolbar { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 8px 16px; }
    .nav { display: flex; align-items: center; gap: 4px; }
    h2 { margin: 0 0 0 8px; font-size: 1.05rem; font-weight: 500; }
    .progress { height: 4px; }
    .notice { margin: 4px 16px 8px; }
    .grid { border-top: 1px solid var(--app-border-soft); }
    .row { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); }
    .row + .row { border-top: 1px solid var(--app-border-soft); }
    .weekday { padding: 6px 8px; font-size: 0.8rem; color: var(--app-text-subtle); text-align: center; }
    .day {
      min-height: 104px; padding: 4px; display: flex; flex-direction: column; gap: 2px; min-width: 0;
      & + .day { border-left: 1px solid var(--app-border-soft); }
    }
    .outside { background: var(--app-tone-neutral-bg); .number { opacity: 0.5; } }
    .number { font-size: 0.8rem; color: var(--app-text-subtle); padding: 2px 4px; align-self: flex-end; }
    .today .number { background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); border-radius: 999px; min-width: 22px; text-align: center; }
    .events { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .event {
      all: unset; cursor: pointer; display: block; padding: 2px 6px; border-radius: 4px; font-size: 0.78rem;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
      &:hover, &:focus-visible { outline: 2px solid var(--mat-sys-primary); outline-offset: -2px; }
    }
    .time { font-weight: 600; margin-right: 4px; }
    .tone-neutral { background: var(--app-tone-neutral-bg); color: var(--app-tone-neutral-fg); }
    .tone-info { background: var(--app-tone-info-bg); color: var(--app-tone-info-fg); }
    .tone-accent { background: var(--app-tone-accent-bg); color: var(--app-tone-accent-fg); }
    .tone-success { background: var(--app-tone-success-bg); color: var(--app-tone-success-fg); }
    .tone-warning { background: var(--app-tone-warning-bg); color: var(--app-tone-warning-fg); }
    .tone-danger, .event.overdue { background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg); }
    /* Movil: cada mantenimiento es un punto del color de su estado. */
    @media (max-width: 720px) {
      .day { min-height: 64px; }
      .events { flex-flow: row wrap; gap: 3px; }
      .event, .event.overdue { font-size: 0; width: 10px; height: 10px; padding: 0; border-radius: 50%; background: currentColor; }
    }
  `,
})
export class MaintenanceCalendarComponent {
  private readonly operations = inject(OperationsService);
  private readonly settings = inject(AppSettingsService);

  /** Filtros de la lista (tipo, estado, busqueda); el rango de fechas lo pone el calendario. */
  readonly filters = input<QueryParams>({});
  readonly select = output<MaintenanceDto>();

  protected readonly weekdays = WEEKDAYS;
  protected readonly max = MAX_ITEMS;

  /** Anio y mes visibles, en la zona de negocio. */
  protected readonly month = signal(this.currentMonth());

  protected readonly monthLabel = computed(() => {
    const [year, month] = this.month();
    const label = new Intl.DateTimeFormat('es-NI', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(Date.UTC(year, month - 1, 1)));
    return label.charAt(0).toUpperCase() + label.slice(1);
  });

  /** Dias de la cuadricula: semanas de lunes a domingo que cubren el mes completo. */
  private readonly days = computed(() => {
    const [year, month] = this.month();
    const offset = (new Date(Date.UTC(year, month - 1, 1)).getUTCDay() + 6) % 7;
    const start = Date.UTC(year, month - 1, 1 - offset);
    const daysInMonth = new Date(Date.UTC(year, month, 0)).getUTCDate();
    const count = Math.ceil((offset + daysInMonth) / 7) * 7;
    return Array.from({ length: count }, (_, i) => new Date(start + i * DAY_MS));
  });

  protected readonly data = rxResource({
    params: () => {
      const days = this.days();
      // Un dia de margen por lado: la zona de negocio desplaza la fecha respecto de UTC.
      const from = new Date(days[0].getTime() - DAY_MS);
      const to = new Date(days[days.length - 1].getTime() + 2 * DAY_MS);
      return { ...this.filters(), scheduledFrom: from.toISOString(), scheduledTo: to.toISOString() };
    },
    stream: ({ params }) => this.operations.maintenances.list({ page: 1, pageSize: MAX_ITEMS, sortDirection: 'Ascending' }, params),
  });

  protected readonly total = computed(() => (this.data.hasValue() ? this.data.value().totalItems : 0));
  protected readonly truncated = computed(() => this.total() > MAX_ITEMS);

  protected readonly weeks = computed<CalendarDay[][]>(() => {
    const [, month] = this.month();
    const today = this.dateKey(new Date());
    const byDay = new Map<string, MaintenanceDto[]>();
    for (const item of this.data.hasValue() ? this.data.value().items : []) {
      const key = this.dateKey(new Date(item.scheduledDate));
      byDay.set(key, [...(byDay.get(key) ?? []), item]);
    }
    const days = this.days().map<CalendarDay>((date) => {
      const key = date.toISOString().slice(0, 10);
      return { key, day: date.getUTCDate(), inMonth: date.getUTCMonth() + 1 === month, isToday: key === today, items: byDay.get(key) ?? [] };
    });
    return Array.from({ length: days.length / 7 }, (_, i) => days.slice(i * 7, i * 7 + 7));
  });

  move(delta: number): void {
    this.month.update(([year, month]) => {
      const date = new Date(Date.UTC(year, month - 1 + delta, 1));
      return [date.getUTCFullYear(), date.getUTCMonth() + 1];
    });
  }

  goToday(): void {
    this.month.set(this.currentMonth());
  }

  protected tone(m: MaintenanceDto): string {
    return enumTone('MaintenanceStatus', m.status);
  }

  protected statusLabel(m: MaintenanceDto): string {
    const label = enumLabel('MaintenanceStatus', m.status);
    return m.isOverdue ? `${label} (vencido)` : label;
  }

  protected time(m: MaintenanceDto): string {
    return new Intl.DateTimeFormat('es-NI', { hour: '2-digit', minute: '2-digit', hour12: false, timeZone: this.settings.timeZone() }).format(new Date(m.scheduledDate));
  }

  /** yyyy-MM-dd del instante en la zona de negocio. */
  private dateKey(date: Date): string {
    return new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: this.settings.timeZone() }).format(date);
  }

  private currentMonth(): [number, number] {
    const [year, month] = this.dateKey(new Date()).split('-').map(Number);
    return [year, month];
  }
}
