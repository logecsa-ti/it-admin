import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { AppSettingsService } from '@core/config/app-settings.service';
import { AppDatePipe } from './app-date.pipe';

describe('AppDatePipe', () => {
  const timeZone = signal('America/Managua');
  let pipe: AppDatePipe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppDatePipe, { provide: AppSettingsService, useValue: { timeZone } }],
    });
    pipe = TestBed.inject(AppDatePipe);
  });

  it('muestra los instantes UTC en la zona de negocio (ADR-005)', () => {
    // 02:30 UTC del 6 de octubre = 20:30 del 5 de octubre en Managua (UTC-6).
    expect(pipe.transform('2026-10-06T02:30:00Z', 'date')).toContain('5');
    expect(pipe.transform('2026-10-06T02:30:00Z', 'time')).toMatch(/8:30|20:30/);
  });

  it('las fechas de calendario se muestran sin conversion', () => {
    expect(pipe.transform('2026-10-06', 'date')).toContain('6');
  });

  it('devuelve un guion para valores vacios o invalidos', () => {
    expect(pipe.transform(null)).toBe('—');
    expect(pipe.transform('no-es-fecha')).toBe('—');
  });
});
