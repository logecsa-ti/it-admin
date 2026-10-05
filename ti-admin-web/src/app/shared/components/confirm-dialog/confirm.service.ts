import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';
import { ConfirmDialogComponent, ConfirmOptions, ConfirmResult } from './confirm-dialog.component';

/** Confirmacion de acciones destructivas o irreversibles (ConfirmDialog, SPECS.md 30). */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  confirm(options: ConfirmOptions): Observable<boolean> {
    return this.open(options).pipe(map((result) => result !== false && result !== undefined));
  }

  /** Confirma pidiendo un motivo; emite el texto o null si se cancela. */
  askReason(options: ConfirmOptions & { reason: NonNullable<ConfirmOptions['reason']> }): Observable<string | null> {
    return this.open(options).pipe(map((result) => (typeof result === 'string' ? result : null)));
  }

  private open(options: ConfirmOptions): Observable<ConfirmResult | undefined> {
    return this.dialog
      .open<ConfirmDialogComponent, ConfirmOptions, ConfirmResult>(ConfirmDialogComponent, {
        data: options,
        width: '440px',
        autoFocus: options.reason ? 'first-tabbable' : 'dialog',
      })
      .afterClosed();
  }
}
