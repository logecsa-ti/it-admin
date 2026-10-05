import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { DialogsService } from './dialogs.service';

/** Cierres de los dialogos compartidos con el MatDialog real: Cancelar nunca cuenta como guardar o confirmar. */
describe('Dialogos compartidos', () => {
  const button = (text: string) =>
    [...document.querySelectorAll<HTMLButtonElement>('.cdk-overlay-container button')].find((b) => b.textContent?.trim() === text)!;

  /** Deja correr la deteccion de cambios y la animacion de cierre (afterClosed emite al terminar). */
  async function settle(): Promise<void> {
    for (let i = 0; i < 5; i++) {
      TestBed.tick();
      await new Promise((resolve) => setTimeout(resolve, 60));
    }
  }

  afterEach(() => document.querySelector('.cdk-overlay-container')?.replaceChildren());

  describe('DialogsService.form', () => {
    const open = (save: () => Observable<unknown> = vi.fn(() => of({ id: 7 }))) => {
      const emitted = vi.fn();
      TestBed.inject(DialogsService)
        .form({ title: 'Asignar', submitText: 'Asignar', fields: [{ key: 'notes', label: 'Notas' }], save })
        .subscribe(emitted);
      return { emitted, save };
    };

    it('Cancelar no emite ni guarda', async () => {
      const { emitted, save } = open();
      await settle();

      button('Cancelar').click();
      await settle();

      expect(save).not.toHaveBeenCalled();
      expect(emitted).not.toHaveBeenCalled();
    });

    it('guardar emite el resultado de la API', async () => {
      const { emitted, save } = open();
      await settle();

      button('Asignar').click();
      await settle();

      expect(save).toHaveBeenCalledOnce();
      expect(emitted).toHaveBeenCalledWith({ id: 7 });
    });

    it('emite aunque la API no devuelva cuerpo', async () => {
      const { emitted } = open(vi.fn(() => of(undefined)));
      await settle();

      button('Asignar').click();
      await settle();

      expect(emitted).toHaveBeenCalledOnce();
    });
  });

  describe('ConfirmService', () => {
    it('Cancelar no confirma una accion destructiva', async () => {
      const result = vi.fn();
      TestBed.inject(ConfirmService).confirm({ title: 'Eliminar', message: '¿Eliminar?', confirmText: 'Eliminar', destructive: true }).subscribe(result);
      await settle();

      button('Cancelar').click();
      await settle();

      expect(result).toHaveBeenCalledWith(false);
    });

    it('confirmar emite true', async () => {
      const result = vi.fn();
      TestBed.inject(ConfirmService).confirm({ title: 'Eliminar', message: '¿Eliminar?', confirmText: 'Eliminar' }).subscribe(result);
      await settle();

      button('Eliminar').click();
      await settle();

      expect(result).toHaveBeenCalledWith(true);
    });

    it('askReason: Cancelar emite null y un motivo opcional vacio confirma con texto vacio', async () => {
      const service = TestBed.inject(ConfirmService);
      const cancelled = vi.fn();
      service.askReason({ title: 'Aprobar', message: '¿Aprobar?', confirmText: 'Aprobar', reason: { label: 'Comentario' } }).subscribe(cancelled);
      await settle();
      button('Cancelar').click();
      await settle();

      const confirmed = vi.fn();
      service.askReason({ title: 'Aprobar', message: '¿Aprobar?', confirmText: 'Aprobar', reason: { label: 'Comentario' } }).subscribe(confirmed);
      await settle();
      button('Aprobar').click();
      await settle();

      expect(cancelled).toHaveBeenCalledWith(null);
      expect(confirmed).toHaveBeenCalledWith('');
    });
  });
});
