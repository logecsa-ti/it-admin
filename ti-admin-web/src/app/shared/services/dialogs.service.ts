import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, filter } from 'rxjs';
import { FormDialogComponent, FormDialogData } from '@shared/components/form-dialog/form-dialog.component';

/** Abre formularios en dialogo; emite solo cuando se guardo con exito. */
@Injectable({ providedIn: 'root' })
export class DialogsService {
  private readonly dialog = inject(MatDialog);

  form<T>(data: FormDialogData<T>): Observable<T> {
    return this.dialog
      .open<FormDialogComponent, FormDialogData<T>, T>(FormDialogComponent, {
        data,
        width: '640px',
        maxWidth: '95vw',
        autoFocus: 'first-tabbable',
        disableClose: true,
      })
      .afterClosed()
      .pipe(filter((result): result is T => result !== undefined && result !== null));
  }
}
