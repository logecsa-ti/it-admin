import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { InstallationDto, LicenseDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { DocumentsPanelComponent } from '@shared/components/documents-panel/documents-panel.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { EnumLabelPipe } from '@shared/pipes/enum-label.pipe';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { emptyPage, pagedList } from '@shared/utils/paged-list';
import { valueOf } from '@shared/utils/resource-utils';
import { licenseFields, licenseFormValue } from '../licensing-forms';
import { LicensingService } from '../licensing.service';

@Component({
  selector: 'app-license-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    DataTableComponent,
    CellDefDirective,
    DocumentsPanelComponent,
    CanDirective,
    EnumLabelPipe,
    AppDatePipe,
    MoneyPipe,
  ],
  template: `
    <div class="page">
      @if (license.isLoading() && !data()) { <mat-progress-bar mode="indeterminate" /> }
      @if (data(); as l) {
        <app-page-header [title]="l.name" [eyebrow]="l.softwareName" backLink="/licencias">
          <button mat-stroked-button type="button" *appCan="P.LicensesManage" (click)="edit(l)"><mat-icon>edit</mat-icon>Editar</button>
          <button mat-stroked-button type="button" *appCan="P.LicensesManage" (click)="remove(l)"><mat-icon>delete</mat-icon>Eliminar</button>
        </app-page-header>

        <div class="two-columns">
          <div class="page">
            <section class="panel">
              <div class="panel-title">
                <h2>Informacion</h2>
                <app-status-badge [label]="l.isActive ? 'Activa' : 'Inactiva'" [tone]="l.isActive ? 'success' : 'neutral'" />
              </div>
              <div class="panel-body">
                <dl class="detail-grid">
                  <div><dt>Software</dt><dd>{{ l.softwareName }}</dd></div>
                  <div><dt>Tipo</dt><dd>{{ l.licenseType | enumLabel: 'LicenseType' }}</dd></div>
                  <div><dt>Proveedor</dt><dd>{{ l.vendorName ?? '—' }}</dd></div>
                  <div>
                    <dt>Contrato</dt>
                    <dd>
                      @if (l.contractId) { <a [routerLink]="['/contratos', l.contractId]">{{ l.contractNumber }}</a> } @else { — }
                    </dd>
                  </div>
                  <div><dt>Compra</dt><dd>{{ l.purchaseDate | appDate: 'date' }}</dd></div>
                  <div><dt>Vencimiento</dt><dd>{{ l.expirationDate ? (l.expirationDate | appDate: 'date') : 'No vence' }}</dd></div>
                  <div><dt>Fin de soporte</dt><dd>{{ l.supportEndDate | appDate: 'date' }}</dd></div>
                  <div><dt>Costo</dt><dd>{{ l.cost | money }}</dd></div>
                </dl>
                @if (l.notes) { <p class="notes subtle">{{ l.notes }}</p> }
              </div>
            </section>

            <section class="panel">
              <div class="panel-title">
                <h2>Instalaciones</h2>
                <div class="actions">
                  <mat-button-toggle-group [value]="installations.filters().activeOnly ? 'active' : 'all'" (change)="installations.patchFilters({ activeOnly: $event.value === 'active' })" aria-label="Filtrar instalaciones">
                    <mat-button-toggle value="active">Vigentes</mat-button-toggle>
                    <mat-button-toggle value="all">Historial</mat-button-toggle>
                  </mat-button-toggle-group>
                  @if (l.isActive && l.availableQuantity > 0) {
                    <button mat-flat-button type="button" *appCan="P.LicensesManage" (click)="install(l)"><mat-icon>add</mat-icon>Instalar</button>
                  }
                </div>
              </div>
              <app-data-table
                caption="Instalaciones"
                [columns]="installationColumns()"
                [rows]="installations.items()"
                [loading]="installations.loading()"
                [total]="installations.total()"
                [page]="installations.page()"
                [pageSize]="installations.pageSize()"
                (pageChange)="installations.onPage($event)"
                emptyTitle="Sin instalaciones"
                emptyMessage="Esta licencia no esta instalada en ningun equipo."
              >
                <ng-template appCell="asset" let-row>
                  <a [routerLink]="['/activos', row.assetId]" class="mono" (click)="$event.stopPropagation()">{{ row.assetCode }}</a> · {{ row.assetName }}
                </ng-template>
                <ng-template appCell="installed" let-row>{{ row.installedAt | appDate: 'date' }}</ng-template>
                <ng-template appCell="state" let-row>
                  @if (row.isActive) { <app-status-badge label="Instalada" tone="success" /> }
                  @else { <span class="subtle">Retirada {{ row.uninstalledAt | appDate: 'date' }}</span> }
                </ng-template>
                <ng-template appCell="actions" let-row>
                  @if (row.isActive) {
                    <button mat-icon-button type="button" matTooltip="Desinstalar" aria-label="Desinstalar" (click)="uninstall(l, row)"><mat-icon>remove_circle_outline</mat-icon></button>
                  }
                </ng-template>
              </app-data-table>
            </section>
          </div>

          <div class="page">
            <section class="panel">
              <div class="panel-title"><h2>Uso</h2></div>
              <div class="panel-body usage">
                <div class="usage-numbers">
                  <span class="big">{{ l.usedQuantity }}</span><span class="muted">de {{ l.quantity }} puestos en uso</span>
                </div>
                <div class="bar" aria-hidden="true"><div [style.width.%]="usagePercent()" [class.full]="l.availableQuantity <= 0"></div></div>
                <span class="subtle">{{ l.availableQuantity }} disponibles</span>
              </div>
            </section>

            <section class="panel" *appCan="P.LicensesManage">
              <div class="panel-title"><h2>Clave de licencia</h2></div>
              <div class="panel-body">
                @if (!l.hasLicenseKey) {
                  <p class="subtle">No se registro clave.</p>
                } @else if (key() !== null) {
                  <code class="key">{{ key() || '(vacia)' }}</code>
                  <button mat-button type="button" (click)="key.set(null)"><mat-icon>visibility_off</mat-icon>Ocultar</button>
                } @else {
                  <p class="subtle">La clave se guarda cifrada. Mostrarla queda registrado en la auditoria.</p>
                  <button mat-stroked-button type="button" (click)="reveal(l)"><mat-icon>visibility</mat-icon>Mostrar clave</button>
                }
              </div>
            </section>

            <app-documents-panel entityName="License" [entityId]="l.id" [canUpload]="canManage()" [canManage]="canManage()" />
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .notes { margin: 16px 0 0; white-space: pre-line; }
    .usage { display: flex; flex-direction: column; gap: 8px; }
    .usage-numbers { display: flex; align-items: baseline; gap: 8px; }
    .big { font-size: 1.75rem; font-weight: 600; color: var(--app-olive-700); }
    .bar { height: 8px; border-radius: 4px; background: var(--app-border-soft); overflow: hidden; }
    .bar div { height: 100%; background: var(--app-olive-500); }
    .bar div.full { background: var(--app-tone-danger-fg); }
    .key { display: block; margin-bottom: 8px; padding: 10px 12px; border-radius: 4px; background: var(--app-tone-neutral-bg); word-break: break-all; }
  `,
})
export class LicenseDetailComponent {
  private readonly licensing = inject(LicensingService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly P = P;

  readonly id = input.required<number, string>({ transform: Number });

  protected readonly license = rxResource({ params: () => this.id(), stream: ({ params }) => this.licensing.licenses.get(params) });
  protected readonly data = computed(() => valueOf(this.license));
  protected readonly key = signal<string | null>(null);
  protected readonly canManage = computed(() => this.auth.hasAny(P.LicensesManage));
  protected readonly usagePercent = computed(() => {
    const l = this.data();
    return l && l.quantity > 0 ? Math.min(100, Math.round((l.usedQuantity / l.quantity) * 100)) : 0;
  });

  protected readonly installations = pagedList<InstallationDto, { id: number; activeOnly: boolean }>(
    (page, f) => (f.id ? this.licensing.installations(f.id, page, f.activeOnly) : of(emptyPage<InstallationDto>())),
    { id: 0, activeOnly: true },
  );

  protected readonly installationColumns = computed<TableColumn<InstallationDto>[]>(() => [
    { key: 'asset', header: 'Equipo' },
    { key: 'user', header: 'Usuario', value: (i) => i.userName ?? '—', hideOnMobile: true },
    { key: 'installed', header: 'Instalada', width: '130px' },
    { key: 'state', header: 'Estado', width: '170px' },
    ...(this.canManage() ? [{ key: 'actions', header: '', width: '56px', align: 'end' as const }] : []),
  ]);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.installations.patchFilters({ id }));
    });
  }

  reveal(license: LicenseDto): void {
    this.licensing.revealKey(license.id).subscribe((result) => this.key.set(result.licenseKey ?? ''));
  }

  edit(license: LicenseDto): void {
    this.dialogs
      .form({
        title: 'Editar licencia',
        fields: licenseFields(this.lookups, license),
        value: licenseFormValue(license),
        save: (value) => this.licensing.licenses.update(license.id, value as never),
      })
      .subscribe(() => {
        this.toast.success('Licencia actualizada.');
        this.key.set(null);
        this.license.reload();
      });
  }

  install(license: LicenseDto): void {
    this.dialogs
      .form({
        title: 'Registrar instalacion',
        submitText: 'Instalar',
        fields: [
          { key: 'assetId', label: 'Equipo', type: 'picker', required: true, wide: true, search: this.lookups.searchAssets },
          { key: 'userId', label: 'Usuario (opcional)', type: 'picker', wide: true, search: this.lookups.searchUsers },
        ],
        save: (value) => this.licensing.install(license.id, value as { assetId: number }),
      })
      .subscribe(() => this.changed('Instalacion registrada.'));
  }

  uninstall(license: LicenseDto, installation: InstallationDto): void {
    this.confirmService
      .confirm({ title: 'Desinstalar', message: `¿Retirar la licencia de ${installation.assetCode}? Se liberara un puesto.`, confirmText: 'Desinstalar' })
      .subscribe((ok) => ok && this.licensing.uninstall(license.id, installation.id).subscribe(() => this.changed('Licencia desinstalada.')));
  }

  remove(license: LicenseDto): void {
    this.confirmService
      .confirm({ title: 'Eliminar licencia', message: `¿Eliminar "${license.name}"?`, confirmText: 'Eliminar', destructive: true })
      .subscribe((ok) => {
        if (ok) {
          this.licensing.licenses.remove(license.id).subscribe(() => {
            this.toast.success('Licencia eliminada.');
            void this.router.navigate(['/licencias']);
          });
        }
      });
  }

  private changed(message: string): void {
    this.toast.success(message);
    this.license.reload();
    this.installations.reload();
  }
}
