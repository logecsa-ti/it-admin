import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import {
  ContractAlertDto,
  DashboardSummaryDto,
  LicenseAlertDto,
  MaintenanceAlertDto,
} from '@core/models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = inject(ApiService);

  summary(): Observable<DashboardSummaryDto> {
    return this.api.get<DashboardSummaryDto>('dashboard/summary');
  }

  licenseAlerts(): Observable<LicenseAlertDto[]> {
    return this.api.get<LicenseAlertDto[]>('alerts/licenses');
  }

  contractAlerts(): Observable<ContractAlertDto[]> {
    return this.api.get<ContractAlertDto[]>('alerts/contracts');
  }

  maintenanceAlerts(): Observable<MaintenanceAlertDto[]> {
    return this.api.get<MaintenanceAlertDto[]>('alerts/maintenance');
  }
}
