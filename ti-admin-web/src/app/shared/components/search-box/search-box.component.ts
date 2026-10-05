import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { debounceTime, distinctUntilChanged } from 'rxjs';

/** Busqueda con espera de 350 ms (SearchBox, SPECS.md 30). */
@Component({
  selector: 'app-search-box',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatIconModule, MatButtonModule],
  template: `
    <mat-form-field class="search">
      <mat-icon matPrefix>search</mat-icon>
      <input matInput type="search" [formControl]="control" [placeholder]="placeholder()" [attr.aria-label]="placeholder()" />
      @if (control.value) {
        <button matSuffix mat-icon-button type="button" aria-label="Limpiar busqueda" (click)="control.setValue('')">
          <mat-icon>close</mat-icon>
        </button>
      }
    </mat-form-field>
  `,
  styles: `
    .search {
      width: 260px;
      max-width: 100%;
    }
    mat-icon[matPrefix] {
      margin: 0 4px 0 8px;
      color: var(--app-text-subtle);
    }
  `,
})
export class SearchBoxComponent implements OnInit {
  readonly placeholder = input('Buscar');
  readonly value = input('');
  readonly search = output<string>();

  protected readonly control = new FormControl('', { nonNullable: true });
  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.control.setValue(this.value(), { emitEvent: false });
    this.control.valueChanges
      .pipe(debounceTime(350), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.search.emit(value));
  }
}
