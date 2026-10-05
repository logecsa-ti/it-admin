import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable, isObservable, of } from 'rxjs';
import { toApiError } from '@core/api/api-error';
import { EntityPickerComponent } from '@shared/components/entity-picker/entity-picker.component';
import { Option } from '@shared/services/lookup.service';
import { applyServerErrors, fromDateOnly, fromLocalInput, toLocalInput, toRequest } from '@shared/utils/form-utils';

export interface SelectOption {
  value: string | number | null;
  label: string;
}

export type FieldType = 'text' | 'textarea' | 'number' | 'email' | 'url' | 'time' | 'date' | 'datetime' | 'select' | 'checkbox' | 'picker';

export interface FieldDef {
  key: string;
  label: string;
  type?: FieldType;
  required?: boolean;
  maxLength?: number;
  min?: number;
  max?: number;
  hint?: string;
  /** Ocupa toda la fila. */
  wide?: boolean;
  /** Solo lectura al editar (p. ej. codigos). */
  lockedOnEdit?: boolean;
  options?: SelectOption[] | Observable<SelectOption[]>;
  /** Para selects: agrega "Ninguno" (null). */
  emptyOption?: string;
  /** Para type 'picker'. */
  search?: (term: string) => Observable<Option[]>;
  pickerLabel?: (value: Record<string, unknown>) => string | null | undefined;
  defaultValue?: unknown;
}

/** Resultado al guardar. Envuelto para no confundirlo con un cierre (Cancelar, Escape) que no trae valor. */
export interface FormDialogResult<T> {
  saved: true;
  result: T;
}

export interface FormDialogData<T = unknown> {
  title: string;
  fields: FieldDef[];
  /** Valores actuales al editar. */
  value?: Record<string, unknown> | null;
  submitText?: string;
  save: (value: Record<string, unknown>) => Observable<T>;
}

/** Formulario en dialogo definido por esquema, para catalogos simples (Modal + Form, SPECS.md 30). */
@Component({
  selector: 'app-form-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    AsyncPipe,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatDatepickerModule,
    EntityPickerComponent,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <mat-dialog-content>
        @if (formError()) {
          <div class="form-error" role="alert">{{ formError() }}</div>
        }
        <div class="form-grid">
          @for (field of data.fields; track field.key) {
            @switch (field.type ?? 'text') {
              @case ('checkbox') {
                <mat-checkbox class="span-all" [formControlName]="field.key">{{ field.label }}</mat-checkbox>
              }
              @case ('select') {
                <mat-form-field [class.span-all]="field.wide">
                  <mat-label>{{ field.label }}</mat-label>
                  <mat-select [formControlName]="field.key">
                    @if (field.emptyOption) {
                      <mat-option [value]="null">{{ field.emptyOption }}</mat-option>
                    }
                    @for (option of optionsOf(field) | async; track option.value) {
                      <mat-option [value]="option.value">{{ option.label }}</mat-option>
                    }
                  </mat-select>
                  @if (field.hint) { <mat-hint>{{ field.hint }}</mat-hint> }
                  <mat-error>{{ errorOf(field) }}</mat-error>
                </mat-form-field>
              }
              @case ('picker') {
                <app-entity-picker
                  [class.span-all]="field.wide"
                  [formControlName]="field.key"
                  [label]="field.label"
                  [required]="!!field.required"
                  [search]="field.search!"
                  [initialLabel]="field.pickerLabel?.(data.value ?? {})"
                  [hint]="field.hint ?? null"
                />
              }
              @case ('date') {
                <mat-form-field [class.span-all]="field.wide">
                  <mat-label>{{ field.label }}</mat-label>
                  <input matInput [matDatepicker]="picker" [formControlName]="field.key" />
                  <mat-datepicker-toggle matIconSuffix [for]="picker" />
                  <mat-datepicker #picker />
                  @if (field.hint) { <mat-hint>{{ field.hint }}</mat-hint> }
                  <mat-error>{{ errorOf(field) }}</mat-error>
                </mat-form-field>
              }
              @case ('textarea') {
                <mat-form-field class="span-all">
                  <mat-label>{{ field.label }}</mat-label>
                  <textarea matInput rows="3" [formControlName]="field.key" [attr.maxlength]="field.maxLength ?? null"></textarea>
                  @if (field.hint) { <mat-hint>{{ field.hint }}</mat-hint> }
                  <mat-error>{{ errorOf(field) }}</mat-error>
                </mat-form-field>
              }
              @default {
                <mat-form-field [class.span-all]="field.wide">
                  <mat-label>{{ field.label }}</mat-label>
                  <input
                    matInput
                    [type]="field.type === 'datetime' ? 'datetime-local' : (field.type ?? 'text')"
                    [formControlName]="field.key"
                    [attr.maxlength]="field.maxLength ?? null"
                    [attr.min]="field.min ?? null"
                    [attr.max]="field.max ?? null"
                  />
                  @if (field.hint) { <mat-hint>{{ field.hint }}</mat-hint> }
                  <mat-error>{{ errorOf(field) }}</mat-error>
                </mat-form-field>
              }
            }
          }
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="cancel()">Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ data.submitText ?? 'Guardar' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .form-grid { min-width: min(560px, 80vw); padding-top: 4px; }
    .form-error {
      margin-bottom: 12px; padding: 10px 12px; border-radius: 4px;
      background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg);
    }
    mat-checkbox { margin: 4px 0 12px; }
  `,
})
export class FormDialogComponent {
  protected readonly data = inject<FormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<FormDialogComponent>);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly form = new FormGroup<Record<string, FormControl>>({});
  private readonly optionCache = new Map<string, Observable<SelectOption[]>>();

  constructor() {
    const editing = !!this.data.value;
    for (const field of this.data.fields) {
      const current = this.data.value?.[field.key];
      const initial =
        current !== undefined
          ? field.type === 'date' && typeof current === 'string'
            ? fromDateOnly(current)
            : field.type === 'datetime' && typeof current === 'string'
              ? toLocalInput(current)
              : current
          : (field.defaultValue ?? (field.type === 'checkbox' ? false : null));
      const control = new FormControl(initial, this.validatorsOf(field));
      if (editing && field.lockedOnEdit) {
        control.disable();
      }
      this.form.addControl(field.key, control);
    }
  }

  protected optionsOf(field: FieldDef): Observable<SelectOption[]> {
    let cached = this.optionCache.get(field.key);
    if (!cached) {
      cached = isObservable(field.options) ? field.options : of(field.options ?? []);
      this.optionCache.set(field.key, cached);
    }
    return cached;
  }

  protected errorOf(field: FieldDef): string {
    const errors = this.form.controls[field.key]?.errors ?? {};
    if (errors['required']) return 'Este campo es obligatorio.';
    if (errors['maxlength']) return `Maximo ${field.maxLength} caracteres.`;
    if (errors['email']) return 'Correo electronico invalido.';
    if (errors['min']) return `El valor minimo es ${field.min}.`;
    if (errors['max']) return `El valor maximo es ${field.max}.`;
    if (errors['server']) return errors['server'] as string;
    return 'Valor invalido.';
  }

  /** Cancelar cierra sin resultado: quien abrio el dialogo no debe tratarlo como guardado. */
  cancel(): void {
    this.dialogRef.close();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.formError.set(null);
    const raw = this.form.getRawValue();
    for (const field of this.data.fields.filter((f) => f.type === 'datetime')) {
      raw[field.key] = fromLocalInput(raw[field.key] as string | null);
    }
    this.data.save(toRequest(raw)).subscribe({
      next: (result) => this.dialogRef.close({ saved: true, result } satisfies FormDialogResult<unknown>),
      error: (error: unknown) => {
        const info = toApiError(error);
        const unmatched = applyServerErrors(this.form, info);
        this.formError.set(unmatched.length ? unmatched.join(' ') : info.details.length ? null : info.message);
        this.saving.set(false);
      },
    });
  }

  private validatorsOf(field: FieldDef): ValidatorFn[] {
    const validators: ValidatorFn[] = [];
    if (field.required) validators.push(field.type === 'checkbox' ? Validators.requiredTrue : Validators.required);
    if (field.maxLength) validators.push(Validators.maxLength(field.maxLength));
    if (field.type === 'email') validators.push(Validators.email);
    if (field.min !== undefined) validators.push(Validators.min(field.min));
    if (field.max !== undefined) validators.push(Validators.max(field.max));
    return validators;
  }
}
