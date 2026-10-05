import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { toApiError } from '@core/api/api-error';
import { PurchaseRequestBody } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { LookupService } from '@shared/services/lookup.service';
import { fromDateOnly, toDateOnly } from '@shared/utils/form-utils';
import { OperationsService } from '../operations.service';

@Component({
  selector: 'app-purchase-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AsyncPipe,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule,
    PageHeaderComponent,
    MoneyPipe,
  ],
  template: `
    <div class="page narrow">
      <app-page-header [title]="id() ? 'Editar solicitud de compra' : 'Nueva solicitud de compra'" [backLink]="id() ? ['/compras', id()] : '/compras'" />

      <form [formGroup]="form" (ngSubmit)="save()" novalidate class="page">
        @if (formError()) { <div class="form-error" role="alert">{{ formError() }}</div> }

        <section class="panel">
          <div class="panel-title"><h2>Solicitud</h2></div>
          <div class="panel-body form-grid">
            <mat-form-field class="span-all">
              <mat-label>Titulo</mat-label>
              <input matInput formControlName="title" maxlength="200" />
              <mat-error>Ingrese un titulo.</mat-error>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Departamento</mat-label>
              <mat-select formControlName="departmentId">
                <mat-option [value]="null">Ninguno</mat-option>
                @for (d of departments$ | async; track d.id) { <mat-option [value]="d.id">{{ d.label }}</mat-option> }
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Proveedor sugerido</mat-label>
              <mat-select formControlName="vendorId">
                <mat-option [value]="null">Ninguno</mat-option>
                @for (v of vendors$ | async; track v.id) { <mat-option [value]="v.id">{{ v.label }}</mat-option> }
              </mat-select>
            </mat-form-field>
            <mat-form-field>
              <mat-label>Fecha requerida</mat-label>
              <input matInput [matDatepicker]="needed" formControlName="neededDate" />
              <mat-datepicker-toggle matIconSuffix [for]="needed" />
              <mat-datepicker #needed />
            </mat-form-field>
            <mat-form-field class="span-all">
              <mat-label>Justificacion</mat-label>
              <textarea matInput rows="3" formControlName="justification" maxlength="2000"></textarea>
            </mat-form-field>
            <mat-form-field class="span-all">
              <mat-label>Descripcion</mat-label>
              <textarea matInput rows="2" formControlName="description" maxlength="2000"></textarea>
            </mat-form-field>
          </div>
        </section>

        <section class="panel">
          <div class="panel-title">
            <h2>Articulos</h2>
            <button mat-stroked-button type="button" (click)="addItem()"><mat-icon>add</mat-icon>Agregar articulo</button>
          </div>
          <div class="items" formArrayName="items">
            @for (item of items.controls; track item; let i = $index) {
              <div class="item" [formGroupName]="i">
                <mat-form-field class="desc">
                  <mat-label>Descripcion</mat-label>
                  <input matInput formControlName="description" maxlength="300" />
                  <mat-error>Obligatorio.</mat-error>
                </mat-form-field>
                <mat-form-field class="qty">
                  <mat-label>Cantidad</mat-label>
                  <input matInput type="number" min="1" formControlName="quantity" />
                </mat-form-field>
                <mat-form-field class="price">
                  <mat-label>Precio unitario</mat-label>
                  <input matInput type="number" min="0" step="0.01" formControlName="unitPrice" />
                </mat-form-field>
                <mat-form-field class="type">
                  <mat-label>Tipo de activo</mat-label>
                  <mat-select formControlName="assetTypeId">
                    <mat-option [value]="null">—</mat-option>
                    @for (t of types$ | async; track t.id) { <mat-option [value]="t.id">{{ t.label }}</mat-option> }
                  </mat-select>
                </mat-form-field>
                <div class="line-total">{{ lineTotal(i) | money }}</div>
                <button mat-icon-button type="button" matTooltip="Quitar" aria-label="Quitar articulo" [disabled]="items.length === 1" (click)="items.removeAt(i)">
                  <mat-icon>delete</mat-icon>
                </button>
              </div>
            }
          </div>
          <div class="total">
            <span class="muted">Total estimado</span>
            <strong>{{ total() | money }}</strong>
          </div>
        </section>

        <div class="actions form-actions">
          <a mat-button [routerLink]="id() ? ['/compras', id()] : '/compras'">Cancelar</a>
          <button mat-flat-button type="submit" [disabled]="saving()">Guardar borrador</button>
        </div>
      </form>
    </div>
  `,
  styles: `
    .narrow { max-width: 1100px; }
    .form-actions { justify-content: flex-end; }
    .form-error { padding: 10px 12px; border-radius: 4px; background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg); }
    .items { padding: 12px 20px 0; }
    .item { display: grid; grid-template-columns: minmax(200px, 3fr) 100px 140px minmax(140px, 1.5fr) 120px 40px; gap: 12px; align-items: center; }
    .line-total { text-align: right; font-weight: 500; padding-bottom: 20px; font-variant-numeric: tabular-nums; }
    .item button { margin-bottom: 20px; }
    .total { display: flex; justify-content: flex-end; align-items: baseline; gap: 16px; padding: 12px 20px 16px; border-top: 1px solid var(--app-border-soft); }
    .total strong { font-size: 1.25rem; color: var(--app-olive-800); }
    @media (max-width: 900px) { .item { grid-template-columns: 1fr 1fr; } .line-total { text-align: left; } }
  `,
})
export class PurchaseFormComponent implements OnInit {
  private readonly operations = inject(OperationsService);
  private readonly lookups = inject(LookupService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);

  readonly id = input<number | undefined, string | undefined>(undefined, { transform: (v: string | undefined) => (v ? Number(v) : undefined) });

  protected readonly departments$ = this.lookups.departments();
  protected readonly vendors$ = this.lookups.vendors();
  protected readonly types$ = this.lookups.assetTypes();
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: [null as string | null],
    departmentId: [null as number | null],
    vendorId: [null as number | null],
    justification: [null as string | null],
    neededDate: [null as Date | null],
    items: this.fb.array([this.newItem()]),
  });

  protected get items(): FormArray {
    return this.form.controls.items;
  }

  private readonly itemValues = toSignal(this.form.controls.items.valueChanges, { initialValue: this.form.controls.items.value });
  protected readonly total = computed(() =>
    this.itemValues().reduce((sum, item) => sum + (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0), 0),
  );

  ngOnInit(): void {
    const id = this.id();
    if (!id) return;
    this.operations.purchases.get(id).subscribe((purchase) => {
      this.items.clear();
      for (const item of purchase.items) this.items.push(this.newItem(item));
      this.form.patchValue({ ...purchase, neededDate: fromDateOnly(purchase.neededDate) });
    });
  }

  protected lineTotal(index: number): number {
    const item = this.itemValues()[index];
    return item ? (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0) : 0;
  }

  addItem(): void {
    this.items.push(this.newItem());
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.formError.set(null);
    const value = this.form.getRawValue();
    const body: PurchaseRequestBody = {
      title: value.title!.trim(),
      description: value.description?.trim() || null,
      departmentId: value.departmentId,
      vendorId: value.vendorId,
      justification: value.justification?.trim() || null,
      neededDate: toDateOnly(value.neededDate),
      items: value.items.map((item) => ({
        description: String(item.description).trim(),
        quantity: Number(item.quantity),
        unitPrice: Number(item.unitPrice),
        assetTypeId: item.assetTypeId ?? null,
        vendorId: null,
        notes: null,
      })),
    };
    const id = this.id();
    (id ? this.operations.purchases.update(id, body) : this.operations.purchases.create(body)).subscribe({
      next: (purchase) => {
        this.toast.success(id ? 'Solicitud actualizada.' : `Solicitud ${purchase.number} creada como borrador.`);
        void this.router.navigate(['/compras', purchase.id]);
      },
      error: (error: unknown) => {
        this.formError.set(toApiError(error).message);
        this.saving.set(false);
      },
    });
  }

  private newItem(item?: { description: string; quantity: number; unitPrice: number; assetTypeId?: number | null }) {
    return this.fb.group({
      description: [item?.description ?? '', [Validators.required, Validators.maxLength(300)]],
      quantity: [item?.quantity ?? 1, [Validators.required, Validators.min(1)]],
      unitPrice: [item?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      assetTypeId: [item?.assetTypeId ?? (null as number | null)],
    });
  }
}
