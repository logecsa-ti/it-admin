import { HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import { SILENT_ERRORS } from '@core/interceptors/http-context';
import {
  AssetAssignmentDto,
  AssetDetailDto,
  AssetListItemDto,
  AssetMovementDto,
  AssignAssetRequest,
  ChangeAssetStatusRequest,
  CreateAssetRequest,
  DownloadedFile,
  ImportResult,
  PageRequest,
  Paged,
  QueryParams,
  ReturnAssetRequest,
  UpdateAssetRequest,
} from '@core/models';
import { ResourceApi } from '@shared/services/resource-api';

export interface AssetFilters extends QueryParams {
  status?: string | null;
  assetTypeId?: number | null;
  departmentId?: number | null;
  locationId?: number | null;
  currentUserId?: number | null;
  warrantyExpiresBefore?: string | null;
}

@Injectable({ providedIn: 'root' })
export class AssetsService {
  private readonly api = inject(ApiService);
  private readonly resource = new ResourceApi<AssetDetailDto, CreateAssetRequest, UpdateAssetRequest>(this.api, 'assets');

  list(page: PageRequest, filters: AssetFilters): Observable<Paged<AssetListItemDto>> {
    return this.api.getPaged<AssetListItemDto>('assets', page, filters);
  }

  mine(page: PageRequest): Observable<Paged<AssetListItemDto>> {
    return this.api.getPaged<AssetListItemDto>('assets/mine', page);
  }

  get(id: number): Observable<AssetDetailDto> {
    return this.resource.get(id);
  }

  create(body: CreateAssetRequest): Observable<AssetDetailDto> {
    return this.resource.create(body);
  }

  update(id: number, body: UpdateAssetRequest): Observable<AssetDetailDto> {
    return this.resource.update(id, body);
  }

  remove(id: number): Observable<string | null> {
    return this.resource.remove(id);
  }

  changeStatus(id: number, body: ChangeAssetStatusRequest): Observable<AssetDetailDto> {
    return this.api.patch<AssetDetailDto>(`assets/${id}/status`, body);
  }

  assign(id: number, body: AssignAssetRequest): Observable<AssetDetailDto> {
    return this.resource.action(id, 'assign', body);
  }

  returnAsset(id: number, body: ReturnAssetRequest): Observable<AssetDetailDto> {
    return this.resource.action(id, 'return', body);
  }

  assignments(id: number, page: PageRequest): Observable<Paged<AssetAssignmentDto>> {
    return this.api.getPaged<AssetAssignmentDto>(`assets/${id}/assignments`, page);
  }

  allAssignments(page: PageRequest, filters: QueryParams): Observable<Paged<AssetAssignmentDto>> {
    return this.api.getPaged<AssetAssignmentDto>('assignments', page, filters);
  }

  movements(id: number, page: PageRequest): Observable<Paged<AssetMovementDto>> {
    return this.api.getPaged<AssetMovementDto>(`assets/${id}/movements`, page);
  }

  import(file: File, dryRun: boolean): Observable<ImportResult> {
    const form = new FormData();
    form.append('file', file);
    return this.api.upload<ImportResult>('assets/import', form, { dryRun }, new HttpContext().set(SILENT_ERRORS, true));
  }

  importTemplate(format: 'xlsx' | 'csv'): Observable<DownloadedFile> {
    return this.api.download('assets/import/template', { format });
  }
}
