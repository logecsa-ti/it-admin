import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { ContractDto, LicenseDto, VendorDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumLabel } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { emptyPage, pagedList } from '@shared/utils/paged-list';
import { valueOf } from '@shared/utils/resource-utils';
import { vendorFields } from '../licensing-forms';
import { LicensingService } from '../licensing.service';

/** Ficha del proveedor: datos, contratos y licencias asociados y documentos adjuntos (DocumentService.Rules "Vendor"). */
@Component({
  selector: 'app-vendor-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    DataTableComponent,
    CellDefDirective,
    DocumentsPanelComponent,
    CanDirective,
    AppDatePipe,
    MoneyPipe,
  ],
  template: `
    <div class="page">
      @if (vendor.isLoading() && !data()) { <mat-progress-bar mode="indeterminate" /> }
      @if (data(); as v) {
        <app-page-header [title]="v.name" [eyebrow]="v.code ?? undefined" backLink="/proveedores" [subtitle]="v.taxId ? 'RUC ' + v.taxId : undefined">
          <button mat-stroked-button type="button" *appCan="P.VendorsManage" (click)="edit(v)"><mat-icon>edit</mat-icon>Editar</button>
          <button mat-stroked-button type="button" *appCan="P.VendorsManage" (click)="remove(v)"><mat-icon>delete</mat-icon>Eliminar</button>
        </app-page-header>

        <div class="two-columns">
          <div class="page">
            <section class="panel">
              <div class="panel-title">
                <h2>Informacion</h2>
                <app-status-badge kind="VendorStatus" [value]="v.status" />
              </div>
              <div class="panel-body">
                <dl class="detail-grid">
                  <div><dt>Contacto</dt><dd>{{ v.contactName ?? '—' }}</dd></div>
                  <div><dt>Correo</dt><dd>@if (v.email) { <a [href]="'mailto:' + v.email">{{ v.email }}</a> } @else { — }</dd></div>
                  <div><dt>Telefono</dt><dd>{{ v.phone ?? '—' }}</dd></div>
                  <div>
                    <dt>Sitio web</dt>
                    <dd>@if (v.website) { <a [href]="v.website" target="_blank" rel="noopener noreferrer">{{ v.website }}</a> } @else { — }</dd>
                  </div>
                  <div><dt>Direccion</dt><dd>{{ address() || '—' }}</dd></div>
                  <div><dt>Calificacion</dt><dd>{{ v.rating != null ? v.rating + ' / 5' : '—' }}</dd></div>
                </dl>
                @if (v.notes) { <p class="notes subtle">{{ v.notes }}</p> }
              </div>
            </section>

            @if (canViewContracts()) {
              <section class="panel">
                <div class="panel-title"><h2>Contratos</h2></div>
                <app-data-table
                  caption="Contratos del proveedor"
                  [columns]="contractColumns"
                  [rows]="contracts.items()"
                  [loading]="contracts.loading()"
                  [total]="contracts.total()"
                  [page]="contracts.page()"
                  [pageSize]="contracts.pageSize()"
                  [clickable]="true"
                  (pageChange)="contracts.onPage($event)"
                  (rowClick)="openContract($event)"
                  emptyIcon="contract"
                  emptyTitle="Sin contratos"
                  emptyMessage="Este proveedor no tiene contratos registrados."
                >
                  <ng-template appCell="contract" let-row>
                    <div class="name-cell"><span class="strong">{{ row.name }}</span><span class="subtle mono">{{ row.number }}</span></div>
                  </ng-template>
                  <ng-template appCell="period" let-row>{{ row.startDate | appDate: 'date' }} – {{ row.endDate | appDate: 'date' }}</ng-template>
                  <ng-template appCell="value" let-row>{{ row.value | money: row.currency }}</ng-template>
                  <ng-template appCell="status" let-row><app-status-badge kind="ContractStatus" [value]="row.status" /></ng-template>
                </app-data-table>
              </section>
            }

            @if (canViewLicenses()) {
              <section class="panel">
                <div class="panel-title"><h2>Licencias</h2></div>
                <app-data-table
                  caption="Licencias del proveedor"
                  [columns]="licenseColumns"
                  [rows]="licenses.items()"
                  [loading]="licenses.loading()"
                  [total]="licenses.total()"
                  [page]="licenses.page()"
                  [pageSize]="licenses.pageSize()"
                  [clickable]="true"
                  (pageChange)="licenses.onPage($event)"
                  (rowClick)="openLicense($event)"
                  emptyIcon="key"
                  emptyTitle="Sin licencias"
                  emptyMessage="Este proveedor no tiene licencias registradas."
                >
                  <ng-template appCell="name" let-row>
                    <div class="name-cell"><span class="strong">{{ row.name }}</span><span class="subtle">{{ row.softwareName }}</span></div>
                  </ng-template>
                  <ng-template appCell="expiration" let-row>
                    @if (row.expirationDate) { {{ row.expirationDate | appDate: 'date' }} } @else { <span class="subtle">No vence</span> }
                  </ng-template>
                  <ng-template appCell="state" let-row>
                    <app-status-badge [label]="row.isActive ? 'Activa' : 'Inactiva'" [tone]="row.isActive ? 'success' : 'neutral'" />
                  </ng-template>
                </app-data-table>
              </section>
            }
          </div>

          <app-documents-panel entityName="Vendor" [entityId]="v.id" [canUpload]="canManage()" [canManage]="canManage()" />
        </div>
      }
    </div>
  `,
  styles: `
    .notes { margin: 16px 0 0; white-space: pre-line; }
    .name-cell { display: flex; flex-direction: column; }
    .strong { font-weight: 500; }
  `,
})
export class VendorDetailComponent {
  private readonly licensing = inject(LicensingService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly P = P;

  readonly id = input.required<number, string>({ transform: Number });
  protected readonly vendor = rxResource({ params: () => this.id(), stream: ({ params }) => this.licensing.vendors.get(params) });
  protected readonly data = computed(() => valueOf(this.vendor));
  protected readonly canManage = computed(() => this.auth.hasAny(P.VendorsManage));
  protected readonly canViewContracts = computed(() => this.auth.hasAny(P.ContractsView));
  protected readonly canViewLicenses = computed(() => this.auth.hasAny(P.LicensesView));
  protected readonly address = computed(() => {
    const v = this.data();
    return v ? [v.address, v.city, v.country].filter(Boolean).join(', ') : '';
  });

  /** Solo se consulta con id y con el permiso del modulo (si no, la API responderia 403). */
  protected readonly contracts = pagedList<ContractDto, { vendorId: number }>(
    (page, f) => (f.vendorId && this.canViewContracts() ? this.licensing.contracts.list(page, f) : of(emptyPage<ContractDto>())),
    { vendorId: 0 },
  );
  protected readonly licenses = pagedList<LicenseDto, { vendorId: number }>(
    (page, f) => (f.vendorId && this.canViewLicenses() ? this.licensing.licenses.list(page, f) : of(emptyPage<LicenseDto>())),
    { vendorId: 0 },
  );

  protected readonly contractColumns: TableColumn<ContractDto>[] = [
    { key: 'contract', header: 'Contrato' },
    { key: 'type', header: 'Tipo', value: (c) => enumLabel('ContractType', c.type), hideOnMobile: true },
    { key: 'period', header: 'Vigencia', hideOnMobile: true },
    { key: 'value', header: 'Valor', align: 'end', hideOnMobile: true },
    { key: 'status', header: 'Estado', width: '120px' },
  ];

  protected readonly licenseColumns: TableColumn<LicenseDto>[] = [
    { key: 'name', header: 'Licencia' },
    { key: 'seats', header: 'Puestos', value: (l) => `${l.usedQuantity} / ${l.quantity}`, align: 'end', width: '100px' },
    { key: 'expiration', header: 'Vencimiento', width: '140px' },
    { key: 'state', header: 'Estado', width: '110px' },
  ];

  constructor() {
    effect(() => {
      const vendorId = this.id();
      untracked(() => {
        this.contracts.patchFilters({ vendorId });
        this.licenses.patchFilters({ vendorId });
      });
    });
  }

  openContract(contract: ContractDto): void {
    void this.router.navigate(['/contratos', contract.id]);
  }

  openLicense(license: LicenseDto): void {
    void this.router.navigate(['/licencias', license.id]);
  }

  edit(vendor: VendorDto): void {
    this.dialogs
      .form({
        title: 'Editar proveedor',
        fields: vendorFields(),
        value: vendor as unknown as Record<string, unknown>,
        save: (value) => this.licensing.vendors.update(vendor.id, value as never),
      })
      .subscribe(() => {
        this.toast.success('Proveedor actualizado.');
        this.lookups.invalidate('vendors');
        this.vendor.reload();
      });
  }

  remove(vendor: VendorDto): void {
    this.confirmService
      .confirm({ title: 'Eliminar proveedor', message: `¿Eliminar ${vendor.name}?`, confirmText: 'Eliminar', destructive: true })
      .subscribe((ok) => {
        if (ok) {
          this.licensing.vendors.remove(vendor.id).subscribe(() => {
            this.toast.success('Proveedor eliminado.');
            this.lookups.invalidate('vendors');
            void this.router.navigate(['/proveedores']);
          });
        }
      });
  }
}
