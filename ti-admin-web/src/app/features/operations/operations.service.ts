import { Injectable, inject } from '@angular/core';
import { ApiService } from '@core/api/api.service';
import {
  ChangeRequestBody,
  ChangeRequestDto,
  MaintenanceDto,
  MaintenanceRequest,
  PurchaseRequestBody,
  PurchaseRequestDto,
} from '@core/models';
import { ResourceApi } from '@shared/services/resource-api';

@Injectable({ providedIn: 'root' })
export class OperationsService {
  private readonly api = inject(ApiService);

  readonly maintenances = new ResourceApi<MaintenanceDto, MaintenanceRequest>(this.api, 'maintenances');
  readonly changes = new ResourceApi<ChangeRequestDto, ChangeRequestBody>(this.api, 'changes');
  readonly purchases = new ResourceApi<PurchaseRequestDto, PurchaseRequestBody>(this.api, 'purchases');
}
