import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  forwardRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, FormControl, NG_VALUE_ACCESSOR, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Observable, catchError, debounceTime, distinctUntilChanged, filter, of, switchMap, tap } from 'rxjs';
import { Option } from '@shared/services/lookup.service';

/**
 * Selector con busqueda en servidor (usuarios, activos): el valor del control es el id.
 * `<app-entity-picker formControlName="userId" label="Usuario" [search]="lookups.searchUsers" />`
 */
@Component({
  selector: 'app-entity-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatFormFieldModule, MatInputModule, MatIconModule, MatButtonModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => EntityPickerComponent), multi: true }],
  template: `
    <mat-form-field class="picker">
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        [formControl]="text"
        [matAutocomplete]="auto"
        [placeholder]="placeholder()"
        (blur)="onBlur()"
      />
      @if (selectedId() !== null && !disabled()) {
        <button matSuffix mat-icon-button type="button" aria-label="Quitar seleccion" (click)="clear()">
          <mat-icon>close</mat-icon>
        </button>
      }
      @if (hint()) {
        <mat-hint>{{ hint() }}</mat-hint>
      }
      <mat-autocomplete #auto="matAutocomplete" (optionSelected)="select($event)">
        @for (option of options(); track option.id) {
          <mat-option [value]="option">
            <span>{{ option.label }}</span>
            @if (option.hint) {
              <small class="option-hint">{{ option.hint }}</small>
            }
          </mat-option>
        } @empty {
          @if (searched()) {
            <mat-option disabled>Sin coincidencias</mat-option>
          }
        }
      </mat-autocomplete>
    </mat-form-field>
  `,
  styles: `
    .picker {
      width: 100%;
    }
    .option-hint {
      margin-left: 8px;
      color: var(--app-text-subtle);
    }
  `,
})
export class EntityPickerComponent implements ControlValueAccessor, OnInit {
  readonly label = input.required<string>();
  readonly search = input.required<(term: string) => Observable<Option[]>>();
  /** Texto a mostrar para el valor inicial (p. ej. el nombre que ya trae el DTO). */
  readonly initialLabel = input<string | null | undefined>();
  readonly placeholder = input('Escriba para buscar');
  readonly hint = input<string | null>();

  protected readonly text = new FormControl<string | Option>('', { nonNullable: true });
  protected readonly options = signal<Option[]>([]);
  protected readonly selectedId = signal<number | null>(null);
  protected readonly searched = signal(false);
  protected readonly disabled = signal(false);
  private selectedLabel = '';
  private readonly destroyRef = inject(DestroyRef);
  private onChange: (value: number | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  ngOnInit(): void {
    this.text.valueChanges
      .pipe(
        filter((value): value is string => typeof value === 'string'),
        debounceTime(300),
        distinctUntilChanged(),
        tap(() => this.searched.set(false)),
        switchMap((term) => this.search()(term.trim()).pipe(catchError(() => of([])))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((options) => {
        this.options.set(options);
        this.searched.set(true);
      });
  }

  writeValue(value: number | null): void {
    this.selectedId.set(value ?? null);
    this.selectedLabel = value != null ? (this.initialLabel() ?? `#${value}`) : '';
    this.text.setValue(this.selectedLabel, { emitEvent: false });
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
    if (disabled) {
      this.text.disable({ emitEvent: false });
    } else {
      this.text.enable({ emitEvent: false });
    }
  }

  protected select(event: MatAutocompleteSelectedEvent): void {
    const option = event.option.value as Option;
    this.selectedId.set(option.id);
    this.selectedLabel = option.label;
    this.text.setValue(option.label, { emitEvent: false });
    this.onChange(option.id);
  }

  protected clear(): void {
    this.selectedId.set(null);
    this.selectedLabel = '';
    this.text.setValue('');
    this.onChange(null);
  }

  /** Si el texto no corresponde a una opcion elegida, se restaura la seleccion anterior. */
  protected onBlur(): void {
    this.onTouched();
    if (this.text.value !== this.selectedLabel && typeof this.text.value === 'string') {
      setTimeout(() => this.text.setValue(this.selectedLabel, { emitEvent: false }), 150);
    }
  }
}
