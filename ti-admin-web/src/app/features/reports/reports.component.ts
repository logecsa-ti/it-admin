import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { BarListComponent } from '@shared/components/bar-list/bar-list.component';
import { KpiCardComponent } from '@shared/components/kpi-card/kpi-card.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { CanDirective } from '@shared/directives/can.directive';
import { enumLabel } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { ExportFormat, ExportService } from '@shared/services/export.service';
import { toDateOnly } from '@shared/utils/form-utils';
import { valueOf } from '@shared/utils/resource-utils';
import { DateRange, ReportsService } from './reports.service';

const TABS = ['activos', 'tickets', 'sla', 'licencias', 'costos'] as const;
type Tab = (typeof TABS)[number];
const MONTHS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

/** Reportes con filtros y exportacion (SPECS.md 48). PDF pendiente de Q-03. */
@Component({
  selector: 'app-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
    PageHeaderComponent,
    KpiCardComponent,
    BarListComponent,
    CanDirective,
    AppDatePipe,
    MoneyPipe,
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
})
export class ReportsComponent {
  private readonly reports = inject(ReportsService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  protected readonly exporter = inject(ExportService);
  protected readonly P = P;

  /** ?tab=costos desde el dashboard. */
  readonly tab = input<string>();
  protected readonly tabIndex = computed(() => Math.max(0, TABS.indexOf((this.tab() ?? 'activos') as Tab)));

  protected readonly from = signal<Date | null>(new Date(new Date().getFullYear(), 0, 1));
  protected readonly to = signal<Date | null>(new Date());
  private readonly range = computed<DateRange>(() => ({ from: toDateOnly(this.from()), to: toDateOnly(this.to()) }));

  protected readonly canAssets = computed(() => this.auth.hasAny(P.AssetsView));
  protected readonly canTickets = computed(() => this.auth.hasAny(P.TicketsView));
  protected readonly canLicenses = computed(() => this.auth.hasAny(P.LicensesView));

  private readonly assetSummaryRes = rxResource({ stream: () => (this.canAssets() ? this.reports.assetSummary() : of(null)) });
  private readonly byUserRes = rxResource({ stream: () => (this.canAssets() ? this.reports.assetsByUser() : of([])) });
  private readonly ticketsRes = rxResource({ params: () => this.range(), stream: ({ params }) => (this.canTickets() ? this.reports.tickets(params) : of(null)) });
  private readonly slaRes = rxResource({ params: () => this.range(), stream: ({ params }) => (this.canTickets() ? this.reports.sla(params) : of(null)) });
  private readonly licensesRes = rxResource({ stream: () => (this.canLicenses() ? this.reports.licenses() : of([])) });
  private readonly costsRes = rxResource({ params: () => this.range(), stream: ({ params }) => this.reports.costs(params) });

  protected readonly assetSummary = computed(() => valueOf(this.assetSummaryRes) ?? null);
  protected readonly byUser = computed(() => valueOf(this.byUserRes) ?? []);
  protected readonly ticketReport = computed(() => valueOf(this.ticketsRes) ?? null);
  protected readonly slaReport = computed(() => valueOf(this.slaRes) ?? null);
  protected readonly licenseRows = computed(() => valueOf(this.licensesRes) ?? []);
  protected readonly costReport = computed(() => valueOf(this.costsRes) ?? null);
  protected readonly loading = computed(
    () => this.ticketsRes.isLoading() || this.slaRes.isLoading() || this.costsRes.isLoading() || this.assetSummaryRes.isLoading(),
  );

  protected readonly costMax = computed(() => Math.max(1, ...(this.costReport()?.byMonth ?? []).map((m) => m.total)));

  protected readonly assetStatusLabel = (v: string) => enumLabel('AssetStatus', v);
  protected readonly ticketStatusLabel = (v: string) => enumLabel('TicketStatus', v);
  protected readonly priorityLabel = (v: string) => enumLabel('TicketPriority', v);
  protected readonly typeLabel = (v: string) => enumLabel('TicketType', v);

  protected monthLabel(month: number, year: number): string {
    return `${MONTHS[month - 1]} ${String(year).slice(2)}`;
  }

  selectTab(index: number): void {
    void this.router.navigate([], { queryParams: { tab: TABS[index] }, replaceUrl: true });
  }

  export(report: string, format: ExportFormat, withRange = false): void {
    this.exporter.export(report, format, withRange ? this.range() : undefined);
  }
}
