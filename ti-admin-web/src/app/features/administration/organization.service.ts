import { Injectable, inject } from '@angular/core';
import { ApiService } from '@core/api/api.service';
import {
  AssetTypeDto,
  CreateAssetTypeRequest,
  CreateDepartmentRequest,
  CreateLocationRequest,
  DepartmentDto,
  LocationDto,
  UpdateAssetTypeRequest,
  UpdateDepartmentRequest,
  UpdateLocationRequest,
} from '@core/models';
import { ResourceApi } from '@shared/services/resource-api';

@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private readonly api = inject(ApiService);

  readonly departments = new ResourceApi<DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest>(this.api, 'departments');
  readonly locations = new ResourceApi<LocationDto, CreateLocationRequest, UpdateLocationRequest>(this.api, 'locations');
  readonly assetTypes = new ResourceApi<AssetTypeDto, CreateAssetTypeRequest, UpdateAssetTypeRequest>(this.api, 'asset-types');
}
