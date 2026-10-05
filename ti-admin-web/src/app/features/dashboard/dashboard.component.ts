import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { BarListComponent } from '@shared/components/bar-list/bar-list.component';
import { KpiCardComponent } from '@shared/components/kpi-card/kpi-card.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { enumLabel } from '@shared/labels/enum-labels';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { valueOf } from '@shared/utils/resource-utils';
import { MoneyPipe } from '@shared/pipes/money.pipe';
import { DashboardService } from './dashboard.service';

@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatIconModule,
    MatProgressBarModule,
    PageHeaderComponent,
    KpiCardComponent,
    BarListComponent,
    StatusBadgeComponent,
    AppDatePipe,
    MoneyPipe,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly service = inject(DashboardService);
  private readonly auth = inject(AuthService);

  protected readonly summary = rxResource({ stream: () => this.service.summary() });
  protected readonly licenseAlerts = rxResource({
    stream: () => (this.auth.hasAny(P.LicensesView) ? this.service.licenseAlerts() : of([])),
  });
  protected readonly contractAlerts = rxResource({
    stream: () => (this.auth.hasAny(P.ContractsView) ? this.service.contractAlerts() : of([])),
  });
  protected readonly maintenanceAlerts = rxResource({
    stream: () => (this.auth.hasAny(P.MaintenanceView) ? this.service.maintenanceAlerts() : of([])),
  });

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const prefix = hour < 12 ? 'Buenos dias' : hour < 19 ? 'Buenas tardes' : 'Buenas noches';
    return `${prefix}, ${this.auth.user()?.firstName ?? ''}`;
  });

  protected readonly data = computed(() => valueOf(this.summary));
  protected readonly licenseAlertList = computed(() => valueOf(this.licenseAlerts) ?? []);
  protected readonly contractAlertList = computed(() => valueOf(this.contractAlerts) ?? []);
  protected readonly maintenanceAlertList = computed(() => valueOf(this.maintenanceAlerts) ?? []);
  protected readonly alertCount = computed(
    () => this.licenseAlertList().length + this.contractAlertList().length + this.maintenanceAlertList().length,
  );

  protected readonly assetStatusLabel = (value: string) => enumLabel('AssetStatus', value);
  protected readonly priorityLabel = (value: string) => enumLabel('TicketPriority', value);
}
