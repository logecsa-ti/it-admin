import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { toApiError } from '@core/api/api-error';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';

/** Misma politica que Identity en el backend (10+ caracteres, mayuscula, minuscula, numero y simbolo). */
const PASSWORD_POLICY = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,}$/;

function matchPasswords(group: AbstractControl): ValidationErrors | null {
  return group.get('newPassword')?.value === group.get('confirm')?.value ? null : { mismatch: true };
}

@Component({
  selector: 'app-profile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, PageHeaderComponent, AppDatePipe],
  template: `
    <div class="page narrow">
      <app-page-header title="Mi perfil" />
      @if (auth.user(); as user) {
        <section class="panel">
          <div class="panel-title"><h2>Datos de la cuenta</h2></div>
          <div class="panel-body">
            <dl class="detail-grid">
              <div><dt>Nombre</dt><dd>{{ user.fullName }}</dd></div>
              <div><dt>Usuario</dt><dd>{{ user.userName }}</dd></div>
              <div><dt>Correo</dt><dd>{{ user.email }}</dd></div>
              <div><dt>Ultimo acceso</dt><dd>{{ user.lastLoginAt | appDate }}</dd></div>
              <div class="wide"><dt>Roles</dt><dd>{{ user.roles.join(', ') }}</dd></div>
            </dl>
            <p class="subtle">Para corregir sus datos o permisos, solicitelo al area de TI.</p>
          </div>
        </section>
      }

      <form class="panel" [formGroup]="form" (ngSubmit)="changePassword()" novalidate>
        <div class="panel-title"><h2>Cambiar contrasena</h2></div>
        <div class="panel-body password-grid">
          @if (error()) { <div class="form-error" role="alert">{{ error() }}</div> }
          <mat-form-field>
            <mat-label>Contrasena actual</mat-label>
            <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
            <mat-error>Ingrese su contrasena actual.</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Nueva contrasena</mat-label>
            <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
            <mat-hint>Minimo 10 caracteres con mayuscula, minuscula, numero y simbolo.</mat-hint>
            <mat-error>No cumple la politica de contrasenas.</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Confirmar nueva contrasena</mat-label>
            <input matInput type="password" formControlName="confirm" autocomplete="new-password" />
            @if (form.hasError('mismatch') && form.controls.confirm.touched) { <mat-hint class="warn">Las contrasenas no coinciden.</mat-hint> }
          </mat-form-field>
          <div class="actions">
            <button mat-flat-button type="submit" [disabled]="saving()">Cambiar contrasena</button>
          </div>
        </div>
      </form>
    </div>
  `,
  styles: `
    .narrow { max-width: 820px; }
    .wide { grid-column: 1 / -1; }
    .password-grid { display: flex; flex-direction: column; gap: 8px; max-width: 420px; }
    .form-error { padding: 10px 12px; border-radius: 4px; background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg); }
    .warn { color: var(--app-tone-danger-fg); }
    .actions { margin-top: 8px; }
  `,
})
export class ProfileComponent {
  protected readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_POLICY)]],
      confirm: ['', Validators.required],
    },
    { validators: matchPasswords },
  );

  changePassword(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.saving.set(false);
        this.form.reset();
        this.toast.success('Contrasena actualizada.');
      },
      error: (error: unknown) => {
        const info = toApiError(error);
        this.error.set(info.details.map((d) => d.message).join(' ') || info.message);
        this.saving.set(false);
      },
    });
  }
}
