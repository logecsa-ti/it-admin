import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { toApiError } from '@core/api/api-error';
import { CreateAssetRequest, UpdateAssetRequest } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { LookupService } from '@shared/services/lookup.service';
import { applyServerErrors, dateRangeValidator, fromDateOnly, toRequest } from '@shared/utils/form-utils';
import { AssetsService } from '../assets.service';

@Component({
  selector: 'app-asset-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AsyncPipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatProgressBarModule,
    PageHeaderComponent,
  ],
  template: `
    <div class="page narrow">
      <app-page-header
        [title]="id() ? 'Editar activo' : 'Nuevo activo'"
        [eyebrow]="code()"
        [backLink]="id() ? ['/activos', id()] : '/activos'"
      />

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <form [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (formError()) {
          <div class="form-error" role="alert">{{ formError() }}</div>
        }

        <section class="panel">
          <div class="panel-title"><h2>Identificacion</h2></div>
          <div class="panel-body form-grid">
            <mat-form-field>
              <mat-label>Codigo de activo</mat-label>
              <input matInput formControlName="assetCode" maxlength="30" />
              <mat-hint>Unico, p. ej. LT-0001</mat-hint>
              <mat-error>{{ error('assetCode') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Numero de serie</mat-label>
              <input matInput formControlName="serialNumber" maxlength="100" />
              <mat-error>{{ error('serialNumber') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Tipo de activo</mat-label>
              <mat-select formControlName="assetTypeId">
                @for (type of types$ | async; track type.id) {
                  <mat-option [value]="type.id">{{ type.label }}</mat-option>
                }
              </mat-select>
              <mat-error>{{ error('assetTypeId') }}</mat-error>
            </mat-form-field>
            <mat-form-field class="span-all">
              <mat-label>Nombre</mat-label>
              <input matInput formControlName="name" maxlength="150" />
              <mat-error>{{ error('name') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Marca</mat-label>
              <input matInput formControlName="brand" maxlength="100" />
            </mat-form-field>
            <mat-form-field>
              <mat-label>Modelo</mat-label>
              <input matInput formControlName="model" maxlength="100" />
            </mat-form-field>
            <mat-form-field class="span-all">
              <mat-label>Descripcion</mat-label>
              <textarea matInput rows="2" formControlName="description" maxlength="1000"></textarea>
            </mat-form-field>
          </div>
        </section>

        <section class="panel">
          <div class="panel-title"><h2>Ubicacion y responsable</h2></div>
          <div class="panel-body form-grid">
            <mat-form-field>
              <mat-label>Departamento</mat-label>
              <mat-select formControlName="departmentId">
                <mat-option [value]="null">Sin departamento</mat-option>
                @for (item of departments$ | async; track item.id) {
                  <mat-option [value]="item.id">{{ item.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Ubicacion</mat-label>
              <mat-select formControlName="locationId">
                <mat-option [value]="null">Sin ubicacion</mat-option>
                @for (item of locations$ | async; track item.id) {
                  <mat-option [value]="item.id">{{ item.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
        </section>

        <section class="panel">
          <div class="panel-title"><h2>Compra y garantia</h2></div>
          <div class="panel-body form-grid">
            <mat-form-field>
              <mat-label>Proveedor</mat-label>
              <mat-select formControlName="vendorId">
                <mat-option [value]="null">Sin proveedor</mat-option>
                @for (item of vendors$ | async; track item.id) {
                  <mat-option [value]="item.id">{{ item.label }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Fecha de compra</mat-label>
              <input matInput [matDatepicker]="purchase" formControlName="purchaseDate" />
              <mat-datepicker-toggle matIconSuffix [for]="purchase" />
              <mat-datepicker #purchase />
            </mat-form-field>
            <mat-form-field>
              <mat-label>Costo de compra</mat-label>
              <input matInput type="number" min="0" step="0.01" formControlName="purchaseCost" />
              <mat-error>{{ error('purchaseCost') }}</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Vencimiento de garantia</mat-label>
              <input matInput [matDatepicker]="warranty" formControlName="warrantyExpiration" />
              <mat-datepicker-toggle matIconSuffix [for]="warranty" />
              <mat-datepicker #warranty />
              @if (form.hasError('dateRange')) {
                <mat-hint class="warn">La garantia vence antes de la compra.</mat-hint>
              }
            </mat-form-field>
            <mat-form-field class="span-all">
              <mat-label>Notas</mat-label>
              <textarea matInput rows="2" formControlName="notes" maxlength="1000"></textarea>
            </mat-form-field>
          </div>
        </section>

        <div class="actions form-actions">
          <a mat-button [routerLink]="id() ? ['/activos', id()] : '/activos'">Cancelar</a>
          <button mat-flat-button type="submit" [disabled]="saving() || loading()">
            {{ id() ? 'Guardar cambios' : 'Crear activo' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: `
    .narrow { max-width: 1040px; }
    form { display: flex; flex-direction: column; gap: var(--app-gap); }
    .form-actions { justify-content: flex-end; }
    .form-error { padding: 10px 12px; border-radius: 4px; background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg); }
    .warn { color: var(--app-tone-danger-fg); }
  `,
})
export class AssetFormComponent implements OnInit {
  private readonly assets = inject(AssetsService);
  private readonly lookups = inject(LookupService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  /** Del parametro de ruta (:id) al editar. */
  readonly id = input<number | undefined, string | undefined>(undefined, {
    transform: (value: string | undefined) => (value ? Number(value) : undefined),
  });

  protected readonly types$ = this.lookups.assetTypes();
  protected readonly departments$ = this.lookups.departments();
  protected readonly locations$ = this.lookups.locations();
  protected readonly vendors$ = this.lookups.vendors();

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly code = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).group(
    {
      assetCode: ['', [Validators.required, Validators.maxLength(30)]],
      serialNumber: [null as string | null, Validators.maxLength(100)],
      name: ['', [Validators.required, Validators.maxLength(150)]],
      description: [null as string | null],
      assetTypeId: [null as number | null, Validators.required],
      brand: [null as string | null],
      model: [null as string | null],
      purchaseDate: [null as Date | null],
      purchaseCost: [null as number | null, Validators.min(0)],
      warrantyExpiration: [null as Date | null],
      locationId: [null as number | null],
      departmentId: [null as number | null],
      vendorId: [null as number | null],
      notes: [null as string | null],
    },
    { validators: dateRangeValidator('purchaseDate', 'warrantyExpiration') },
  );

  ngOnInit(): void {
    const id = this.id();
    if (!id) return;
    this.loading.set(true);
    this.form.controls.assetCode.disable();
    this.assets.get(id).subscribe({
      next: (asset) => {
        this.code.set(asset.assetCode);
        this.form.patchValue({
          ...asset,
          purchaseDate: fromDateOnly(asset.purchaseDate),
          warrantyExpiration: fromDateOnly(asset.warrantyExpiration),
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected error(control: keyof typeof this.form.controls): string {
    const errors = this.form.controls[control].errors ?? {};
    if (errors['required']) return 'Este campo es obligatorio.';
    if (errors['maxlength']) return 'Texto demasiado largo.';
    if (errors['min']) return 'Debe ser mayor o igual a cero.';
    return (errors['server'] as string) ?? '';
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.formError.set(null);
    const id = this.id();
    const body = toRequest(this.form.getRawValue());
    const request = id
      ? this.assets.update(id, body as unknown as UpdateAssetRequest)
      : this.assets.create(body as unknown as CreateAssetRequest);

    request.subscribe({
      next: (asset) => {
        this.toast.success(id ? 'Activo actualizado.' : 'Activo creado.');
        void this.router.navigate(['/activos', asset.id]);
      },
      error: (error: unknown) => {
        const info = toApiError(error);
        const unmatched = applyServerErrors(this.form, info);
        this.formError.set(unmatched.length ? unmatched.join(' ') : info.status === 409 ? info.message : null);
        this.saving.set(false);
      },
    });
  }
}
