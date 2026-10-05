import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import {
  AddTicketCommentRequest,
  ChangeTicketStatusRequest,
  CreateTicketRequest,
  PageRequest,
  Paged,
  QueryParams,
  SlaPolicyDto,
  SlaPolicyRequest,
  TicketCategoryDto,
  TicketCategoryRequest,
  TicketCommentDto,
  TicketDetailDto,
  TicketHistoryDto,
  TicketListItemDto,
  TicketStatus,
  UpdateTicketRequest,
} from '@core/models';
import { ResourceApi } from '@shared/services/resource-api';

export interface TicketFilters extends QueryParams {
  type?: string | null;
  status?: string | null;
  priority?: string | null;
  categoryId?: number | null;
  overdue?: boolean | null;
  mine?: boolean | null;
  assignedToMe?: boolean | null;
}

/**
 * Transiciones de estado validas (TicketStatusRules del dominio). Quien puede ejecutar cada una lo decide
 * la API (agente, solicitante o permisos de resolver/cerrar).
 */
export const TICKET_TRANSITIONS: Record<TicketStatus, TicketStatus[]> = {
  New: ['Open', 'InProgress', 'Cancelled'],
  Open: ['InProgress', 'WaitingUser', 'WaitingVendor', 'Resolved', 'Cancelled'],
  InProgress: ['WaitingUser', 'WaitingVendor', 'Resolved', 'Cancelled'],
  WaitingUser: ['InProgress', 'Resolved', 'Cancelled'],
  WaitingVendor: ['InProgress', 'Resolved', 'Cancelled'],
  Resolved: ['Closed', 'InProgress'],
  Closed: [],
  Cancelled: [],
};

@Injectable({ providedIn: 'root' })
export class TicketsService {
  private readonly api = inject(ApiService);
  private readonly resource = new ResourceApi<TicketDetailDto, CreateTicketRequest, UpdateTicketRequest>(this.api, 'tickets');

  readonly categories = new ResourceApi<TicketCategoryDto, TicketCategoryRequest>(this.api, 'ticket-categories');
  readonly slaPolicies = new ResourceApi<SlaPolicyDto, SlaPolicyRequest>(this.api, 'sla-policies');

  list(page: PageRequest, filters: TicketFilters): Observable<Paged<TicketListItemDto>> {
    return this.api.getPaged<TicketListItemDto>('tickets', page, filters);
  }

  get(id: number): Observable<TicketDetailDto> {
    return this.resource.get(id);
  }

  create(body: CreateTicketRequest): Observable<TicketDetailDto> {
    return this.resource.create(body);
  }

  update(id: number, body: UpdateTicketRequest): Observable<TicketDetailDto> {
    return this.resource.update(id, body);
  }

  changeStatus(id: number, body: ChangeTicketStatusRequest): Observable<TicketDetailDto> {
    return this.resource.action(id, 'status', body);
  }

  assign(id: number, assignedToId: number): Observable<TicketDetailDto> {
    return this.resource.action(id, 'assign', { assignedToId });
  }

  approve(id: number, comment: string | null): Observable<TicketDetailDto> {
    return this.resource.action(id, 'approve', { comment });
  }

  reject(id: number, comment: string | null): Observable<TicketDetailDto> {
    return this.resource.action(id, 'reject', { comment });
  }

  comments(id: number): Observable<TicketCommentDto[]> {
    return this.api.get<TicketCommentDto[]>(`tickets/${id}/comments`);
  }

  addComment(id: number, body: AddTicketCommentRequest): Observable<TicketCommentDto[]> {
    return this.api.post<TicketCommentDto[]>(`tickets/${id}/comments`, body);
  }

  history(id: number): Observable<TicketHistoryDto[]> {
    return this.api.get<TicketHistoryDto[]>(`tickets/${id}/history`);
  }
}
