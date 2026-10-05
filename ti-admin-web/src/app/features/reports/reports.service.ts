import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import {
  AssetSummaryReport,
  AssetsByUserRow,
  AuditLogDto,
  ConfigurationItemDto,
  CostReport,
  LicenseUsageReportRow,
  PageRequest,
  Paged,
  QueryParams,
  SlaReport,
  TicketReport,
} from '@core/models';

export interface DateRange extends QueryParams {
  from?: string | null;
  to?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly api = inject(ApiService);

  assetSummary(): Observable<AssetSummaryReport> {
    return this.api.get<AssetSummaryReport>('reports/assets/summary');
  }

  assetsByUser(departmentId?: number | null): Observable<AssetsByUserRow[]> {
    return this.api.get<AssetsByUserRow[]>('reports/assets/by-user', { departmentId });
  }

  tickets(range: DateRange): Observable<TicketReport> {
    return this.api.get<TicketReport>('reports/tickets', range);
  }

  sla(range: DateRange): Observable<SlaReport> {
    return this.api.get<SlaReport>('reports/sla', range);
  }

  licenses(): Observable<LicenseUsageReportRow[]> {
    return this.api.get<LicenseUsageReportRow[]>('reports/licenses');
  }

  costs(range: DateRange): Observable<CostReport> {
    return this.api.get<CostReport>('reports/costs', range);
  }

  audit(page: PageRequest, filters: QueryParams): Observable<Paged<AuditLogDto>> {
    return this.api.getPaged<AuditLogDto>('audit', page, filters);
  }

  configuration(): Observable<ConfigurationItemDto[]> {
    return this.api.get<ConfigurationItemDto[]>('configuration');
  }

  updateConfiguration(key: string, value: string | null): Observable<ConfigurationItemDto> {
    return this.api.put<ConfigurationItemDto>(`configuration/${encodeURIComponent(key)}`, { value });
  }

  resetConfiguration(key: string): Observable<ConfigurationItemDto> {
    return this.api.post<ConfigurationItemDto>(`configuration/${encodeURIComponent(key)}/reset`);
  }
}
