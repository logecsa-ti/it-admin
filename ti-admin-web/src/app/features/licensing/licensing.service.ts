import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import {
  ContractDto,
  CreateContractRequest,
  CreateLicenseRequest,
  InstallationDto,
  InstallLicenseRequest,
  LicenseDto,
  LicenseKeyDto,
  PageRequest,
  Paged,
  RenewContractRequest,
  SoftwareDto,
  SoftwareRequest,
  UpdateContractRequest,
  UpdateLicenseRequest,
  VendorDto,
  VendorRequest,
} from '@core/models';
import { ResourceApi } from '@shared/services/resource-api';

@Injectable({ providedIn: 'root' })
export class LicensingService {
  private readonly api = inject(ApiService);

  readonly software = new ResourceApi<SoftwareDto, SoftwareRequest>(this.api, 'software');
  readonly vendors = new ResourceApi<VendorDto, VendorRequest>(this.api, 'vendors');
  readonly licenses = new ResourceApi<LicenseDto, CreateLicenseRequest, UpdateLicenseRequest>(this.api, 'licenses');
  readonly contracts = new ResourceApi<ContractDto, CreateContractRequest, UpdateContractRequest>(this.api, 'contracts');

  installations(licenseId: number, page: PageRequest, activeOnly: boolean): Observable<Paged<InstallationDto>> {
    return this.api.getPaged<InstallationDto>(`licenses/${licenseId}/installations`, page, { activeOnly: activeOnly || null });
  }

  install(licenseId: number, body: InstallLicenseRequest): Observable<InstallationDto> {
    return this.api.post<InstallationDto>(`licenses/${licenseId}/installations`, body);
  }

  uninstall(licenseId: number, installationId: number): Observable<string | null> {
    return this.api.delete(`licenses/${licenseId}/installations/${installationId}`);
  }

  /** Lectura auditada de la clave (SensitiveRead, ADR-021). */
  revealKey(licenseId: number): Observable<LicenseKeyDto> {
    return this.api.get<LicenseKeyDto>(`licenses/${licenseId}/key`);
  }

  renew(contractId: number, body: RenewContractRequest): Observable<ContractDto> {
    return this.contracts.action(contractId, 'renew', body);
  }
}
