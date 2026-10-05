import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AppSettingsService } from '@core/config/app-settings.service';
import { MaintenanceDto } from '@core/models';
import { OperationsService } from '../operations.service';
import { MaintenanceCalendarComponent } from './maintenance-calendar.component';

const maintenance = (id: number, scheduledDate: string): MaintenanceDto =>
  ({ id, number: `MNT-2026-00000${id}`, title: `Mantenimiento ${id}`, status: 'Scheduled', assetCode: 'LAP-001', scheduledDate, isOverdue: false }) as MaintenanceDto;

describe('MaintenanceCalendarComponent', () => {
  const list = vi.fn();

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-10-15T18:00:00Z'));
    list.mockReset();
    list.mockReturnValue(
      of({
        items: [maintenance(1, '2026-10-06T02:30:00Z'), maintenance(2, '2026-10-20T15:00:00Z')],
        page: 1, pageSize: 200, totalItems: 2, totalPages: 1, hasPrevious: false, hasNext: false,
      }),
    );
    TestBed.configureTestingModule({
      providers: [
        { provide: OperationsService, useValue: { maintenances: { list } } },
        { provide: AppSettingsService, useValue: { timeZone: signal('America/Managua') } },
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  async function render() {
    const fixture = TestBed.createComponent(MaintenanceCalendarComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const dayOf = (root: HTMLElement, title: string) =>
    [...root.querySelectorAll('.day')].find((day) => day.textContent?.includes(title))?.querySelector('.number')?.textContent?.trim();

  it('consulta el rango visible del mes con margen y agrupa por fecha en la zona de negocio', async () => {
    const root = await render();

    // Octubre 2026 empieza en jueves: la cuadricula va del lunes 28-sep al domingo 1-nov.
    const [, filters] = list.mock.calls[0];
    expect(filters.scheduledFrom).toBe('2026-09-27T00:00:00.000Z');
    expect(filters.scheduledTo).toBe('2026-11-03T00:00:00.000Z');
    expect(root.querySelector('h2')?.textContent).toContain('Octubre de 2026');
    // 02:30 UTC del 6-oct es el 5-oct a las 20:30 en Managua (UTC-6).
    expect(dayOf(root, 'Mantenimiento 1')).toBe('5');
    expect(dayOf(root, 'Mantenimiento 2')).toBe('20');
    expect(root.querySelector('.today .number')?.textContent?.trim()).toBe('15');
  });

  it('pasa los filtros de la lista y cambia de mes', async () => {
    const fixture = TestBed.createComponent(MaintenanceCalendarComponent);
    fixture.componentRef.setInput('filters', { status: 'Scheduled' });
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.move(1);
    fixture.detectChanges();
    await fixture.whenStable();

    const [, filters] = list.mock.calls.at(-1)!;
    expect(filters.status).toBe('Scheduled');
    expect(filters.scheduledFrom).toBe('2026-10-25T00:00:00.000Z');
  });
});
