import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/** Mensajes breves no bloqueantes (Toast, SPECS.md 30). */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, 'Cerrar', { duration: 3500 });
  }

  info(message: string): void {
    this.snackBar.open(message, 'Cerrar', { duration: 4000 });
  }

  error(message: string, traceId?: string | null): void {
    const text = traceId ? `${message} (Ref. ${traceId})` : message;
    this.snackBar.open(text, 'Cerrar', { duration: 7000, panelClass: 'toast-error' });
  }
}
