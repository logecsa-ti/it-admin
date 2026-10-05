import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, filter, map } from 'rxjs';
import { FormDialogComponent, FormDialogData, FormDialogResult } from '@shared/components/form-dialog/form-dialog.component';

/** Abre formularios en dialogo; emite solo cuando se guardo con exito. */
@Injectable({ providedIn: 'root' })
export class DialogsService {
  private readonly dialog = inject(MatDialog);

  form<T>(data: FormDialogData<T>): Observable<T> {
    return this.dialog
      .open<FormDialogComponent, FormDialogData<T>, FormDialogResult<T>>(FormDialogComponent, {
        data,
        width: '640px',
        maxWidth: '95vw',
        autoFocus: 'first-tabbable',
        disableClose: true,
      })
      .afterClosed()
      .pipe(
        filter((closed): closed is FormDialogResult<T> => closed?.saved === true),
        map((closed) => closed.result),
      );
  }
}
