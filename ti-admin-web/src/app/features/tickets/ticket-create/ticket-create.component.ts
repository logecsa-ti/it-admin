import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { toApiError } from '@core/api/api-error';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { CreateTicketRequest, TicketCategoryDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { EntityPickerComponent } from '@shared/components/entity-picker/entity-picker.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { enumLabel, enumOptions } from '@shared/labels/enum-labels';
import { LookupService } from '@shared/services/lookup.service';
import { applyServerErrors, toRequest } from '@shared/utils/form-utils';
import { valueOf } from '@shared/utils/resource-utils';
import { TicketsService } from '../tickets.service';

/** Alta de incidente o solicitud: el tipo lo define la categoria (Q-07, ADR-025). */
@Component({
  selector: 'app-ticket-create',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatIconModule,
    PageHeaderComponent,
    EntityPickerComponent,
  ],
  template: `
    <div class="page narrow">
      <app-page-header title="Nuevo ticket" subtitle="Describa el problema o lo que necesita; TI le respondera segun la prioridad." backLink="/tickets" />

      <form class="panel" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="panel-body form-grid">
          @if (formError()) {
            <div class="form-error span-all" role="alert">{{ formError() }}</div>
          }

          <mat-form-field class="span-all">
            <mat-label>Categoria</mat-label>
            <mat-select formControlName="categoryId">
              @for (group of groups(); track group.type) {
                <mat-optgroup [label]="group.label">
                  @for (category of group.items; track category.id) {
                    <mat-option [value]="category.id">{{ category.name }}</mat-option>
                  }
                </mat-optgroup>
              }
            </mat-select>
            @if (category(); as c) {
              <mat-hint>
                {{ c.type === 'Incident' ? 'Incidente' : 'Solicitud de servicio' }}
                @if (c.requiresApproval) { · requiere aprobacion antes de atenderse }
              </mat-hint>
            }
            <mat-error>Seleccione una categoria.</mat-error>
          </mat-form-field>

          <mat-form-field class="span-all">
            <mat-label>Titulo</mat-label>
            <input matInput formControlName="title" maxlength="200" placeholder="Resumen breve, p. ej. No enciende el monitor" />
            <mat-error>{{ form.controls.title.hasError('server') ? form.controls.title.getError('server') : 'Ingrese un titulo.' }}</mat-error>
          </mat-form-field>

          <mat-form-field class="span-all">
            <mat-label>Descripcion</mat-label>
            <textarea matInput rows="6" formControlName="description" maxlength="4000"
              placeholder="Que ocurre, desde cuando, mensajes de error y a cuantas personas afecta."></textarea>
            <mat-error>Describa el problema o la solicitud.</mat-error>
          </mat-form-field>

          <mat-form-field>
            <mat-label>Prioridad</mat-label>
            <mat-select formControlName="priority">
              <mat-option [value]="null">Por defecto de la categoria{{ category() ? ' (' + priorityLabel(category()!.defaultPriority) + ')' : '' }}</mat-option>
              @for (o of priorities; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
            </mat-select>
          </mat-form-field>

          @if (category()?.type === 'ServiceRequest') {
            <mat-form-field>
              <mat-label>Fecha requerida</mat-label>
              <input matInput [matDatepicker]="needed" formControlName="neededByDate" />
              <mat-datepicker-toggle matIconSuffix [for]="needed" />
              <mat-datepicker #needed />
            </mat-form-field>
          }

          @if (canPickAsset()) {
            <app-entity-picker formControlName="assetId" label="Activo relacionado" [search]="lookups.searchAssets" hint="Opcional" />
          }
          @if (canPickRequester()) {
            <app-entity-picker formControlName="requesterId" label="En nombre de" [search]="lookups.searchUsers" hint="Opcional: registrar por otro usuario" />
          }
        </div>
        <div class="actions form-actions">
          <a mat-button routerLink="/tickets">Cancelar</a>
          <button mat-flat-button type="submit" [disabled]="saving()"><mat-icon>send</mat-icon>Enviar ticket</button>
        </div>
      </form>
    </div>
  `,
  styles: `
    .narrow { max-width: 860px; }
    .form-actions { justify-content: flex-end; padding: 12px 20px; border-top: 1px solid var(--app-border-soft); }
    .form-error { padding: 10px 12px; border-radius: 4px; background: var(--app-tone-danger-bg); color: var(--app-tone-danger-fg); margin-bottom: 8px; }
  `,
})
export class TicketCreateComponent {
  private readonly tickets = inject(TicketsService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);
  protected readonly lookups = inject(LookupService);

  protected readonly priorities = enumOptions('TicketPriority');
  protected readonly priorityLabel = (value: string) => enumLabel('TicketPriority', value);
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).group({
    categoryId: [null as number | null, Validators.required],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]],
    priority: [null as string | null],
    neededByDate: [null as Date | null],
    assetId: [null as number | null],
    requesterId: [null as number | null],
  });

  private readonly categories = rxResource({ stream: () => this.lookups.ticketCategories() });
  private readonly categoryId = toSignal(this.form.controls.categoryId.valueChanges, { initialValue: null });

  /** Solo las categorias cuyo tipo el usuario puede crear. */
  protected readonly groups = computed(() => {
    const all = valueOf(this.categories) ?? [];
    const allowed = (c: TicketCategoryDto) =>
      c.type === 'Incident' ? this.auth.hasAny(P.TicketsCreate) : this.auth.hasAny(P.RequestsCreate);
    return (['Incident', 'ServiceRequest'] as const)
      .map((type) => ({
        type,
        label: type === 'Incident' ? 'Incidentes' : 'Solicitudes de servicio',
        items: all.filter((c) => c.type === type && allowed(c)),
      }))
      .filter((group) => group.items.length > 0);
  });

  protected readonly category = computed(() => (valueOf(this.categories) ?? []).find((c) => c.id === this.categoryId()) ?? null);
  protected readonly canPickAsset = computed(() => this.auth.hasAny(P.AssetsView));
  protected readonly canPickRequester = computed(() => this.auth.hasAny(P.TicketsUpdate) && this.auth.hasAny(P.UsersView));

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.formError.set(null);
    const body = toRequest(this.form.getRawValue()) as unknown as CreateTicketRequest;
    if (this.category()?.type !== 'ServiceRequest') {
      body.neededByDate = null;
    }
    this.tickets.create(body).subscribe({
      next: (ticket) => {
        this.toast.success(`Ticket ${ticket.ticketNumber} creado.`);
        void this.router.navigate(['/tickets', ticket.id]);
      },
      error: (error: unknown) => {
        const info = toApiError(error);
        const unmatched = applyServerErrors(this.form, info);
        this.formError.set(unmatched.length ? unmatched.join(' ') : null);
        this.saving.set(false);
      },
    });
  }
}
