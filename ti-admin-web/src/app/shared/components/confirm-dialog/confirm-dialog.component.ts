import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  /** Accion destructiva: boton en rojo apagado. */
  destructive?: boolean;
  /** Pide un texto (motivo de rechazo/cancelacion). */
  reason?: { label: string; required?: boolean; maxLength?: number };
}

/** Resultado: false si se cancela; true (o el motivo escrito) si se confirma. */
export type ConfirmResult = false | true | string;

@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, ReactiveFormsModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p class="message">{{ data.message }}</p>
      @if (data.reason) {
        <mat-form-field class="reason">
          <mat-label>{{ data.reason.label }}</mat-label>
          <textarea matInput rows="3" [formControl]="reason" [maxlength]="data.reason.maxLength ?? 1000"></textarea>
          @if (reason.hasError('required')) {
            <mat-error>Este campo es obligatorio.</mat-error>
          }
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="cancel()">{{ data.cancelText ?? 'Cancelar' }}</button>
      <button mat-flat-button type="button" [class.destructive]="data.destructive" (click)="confirm()">
        {{ data.confirmText ?? 'Confirmar' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .message {
      margin: 0 0 12px;
      color: var(--app-text-muted);
      white-space: pre-line;
    }
    .reason {
      width: 100%;
    }
    .destructive {
      --mat-button-filled-container-color: #8c2f22;
      --mat-button-filled-label-text-color: #ffffff;
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmOptions>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ConfirmDialogComponent, ConfirmResult>);

  protected readonly reason = new FormControl('', {
    nonNullable: true,
    validators: this.data.reason?.required ? [Validators.required] : [],
  });

  cancel(): void {
    this.dialogRef.close(false);
  }

  confirm(): void {
    if (!this.data.reason) {
      this.dialogRef.close(true);
      return;
    }
    if (this.reason.invalid) {
      this.reason.markAsTouched();
      return;
    }
    this.dialogRef.close(this.reason.value.trim());
  }
}
