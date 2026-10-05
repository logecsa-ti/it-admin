import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/api/api.service';
import {
  CreateRoleRequest,
  CreateUserRequest,
  PageRequest,
  Paged,
  PermissionDto,
  QueryParams,
  RoleDetailDto,
  RoleDto,
  UpdateRoleRequest,
  UpdateUserRequest,
  UserDetailDto,
  UserListItemDto,
} from '@core/models';
import { ResourceApi } from '@shared/services/resource-api';

@Injectable({ providedIn: 'root' })
export class IdentityService {
  private readonly api = inject(ApiService);
  private readonly users = new ResourceApi<UserDetailDto, CreateUserRequest, UpdateUserRequest>(this.api, 'users');
  readonly roles = new ResourceApi<RoleDetailDto, CreateRoleRequest, UpdateRoleRequest>(this.api, 'roles');

  listUsers(page: PageRequest, filters: QueryParams): Observable<Paged<UserListItemDto>> {
    return this.api.getPaged<UserListItemDto>('users', page, filters);
  }

  getUser(id: number): Observable<UserDetailDto> {
    return this.users.get(id);
  }

  createUser(body: CreateUserRequest): Observable<UserDetailDto> {
    return this.users.create(body);
  }

  updateUser(id: number, body: UpdateUserRequest): Observable<UserDetailDto> {
    return this.users.update(id, body);
  }

  setActive(id: number, active: boolean): Observable<unknown> {
    return this.users.action(id, active ? 'activate' : 'deactivate');
  }

  setUserRoles(id: number, roles: string[]): Observable<UserDetailDto> {
    return this.api.put<UserDetailDto>(`users/${id}/roles`, { roles });
  }

  setUserPermissions(id: number, permissions: string[]): Observable<UserDetailDto> {
    return this.api.put<UserDetailDto>(`users/${id}/permissions`, { permissions });
  }

  listRoles(): Observable<RoleDto[]> {
    return this.api.get<RoleDto[]>('roles');
  }

  setRolePermissions(id: number, permissions: string[]): Observable<RoleDetailDto> {
    return this.api.put<RoleDetailDto>(`roles/${id}/permissions`, { permissions });
  }

  permissions(): Observable<PermissionDto[]> {
    return this.api.get<PermissionDto[]>('permissions');
  }
}
