import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { map } from 'rxjs';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { QueryParams, UserListItemDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { CellDefDirective, DataTableComponent, TableColumn } from '@shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { SearchBoxComponent } from '@shared/components/search-box/search-box.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { pagedList } from '@shared/utils/paged-list';
import { valueOf } from '@shared/utils/resource-utils';
import { IdentityService } from '../identity.service';

@Component({
  selector: 'app-user-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    PageHeaderComponent,
    SearchBoxComponent,
    DataTableComponent,
    CellDefDirective,
    StatusBadgeComponent,
    CanDirective,
    AppDatePipe,
  ],
  template: `
    <div class="page">
      <app-page-header title="Usuarios" subtitle="Cuentas de acceso, roles y permisos. Los usuarios se desactivan, nunca se eliminan.">
        <button mat-flat-button type="button" *appCan="P.UsersCreate" (click)="create()"><mat-icon>person_add</mat-icon>Nuevo usuario</button>
      </app-page-header>
      <section class="panel">
        <div class="filters">
          <app-search-box placeholder="Nombre, usuario o correo" (search)="list.setSearch($event)" />
          <mat-form-field>
            <mat-label>Rol</mat-label>
            <mat-select [value]="list.filters()['role'] ?? null" (selectionChange)="list.patchFilters({ role: $event.value })">
              <mat-option [value]="null">Todos</mat-option>
              @for (role of roleNames(); track role) { <mat-option [value]="role">{{ role }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-button-toggle-group [value]="list.filters()['isActive'] === false ? 'false' : list.filters()['isActive'] ? 'true' : 'all'"
            (change)="list.patchFilters({ isActive: $event.value === 'all' ? null : $event.value === 'true' })" aria-label="Estado">
            <mat-button-toggle value="true">Activos</mat-button-toggle>
            <mat-button-toggle value="false">Inactivos</mat-button-toggle>
            <mat-button-toggle value="all">Todos</mat-button-toggle>
          </mat-button-toggle-group>
        </div>
        <app-data-table
          caption="Usuarios"
          [columns]="columns"
          [rows]="list.items()"
          [loading]="list.loading()"
          [total]="list.total()"
          [page]="list.page()"
          [pageSize]="list.pageSize()"
          [clickable]="true"
          (pageChange)="list.onPage($event)"
          (sortChange)="list.onSort($event)"
          (rowClick)="open($event)"
          emptyIcon="group"
        >
          <ng-template appCell="name" let-row>
            <div class="user-cell">
              <span class="avatar" aria-hidden="true">{{ row.firstName.charAt(0) }}{{ row.lastName.charAt(0) }}</span>
              <div><div class="strong">{{ row.fullName }}</div><div class="subtle">{{ row.userName }}</div></div>
            </div>
          </ng-template>
          <ng-template appCell="roles" let-row>
            <div class="roles">@for (role of row.roles; track role) { <span class="role">{{ role }}</span> }</div>
          </ng-template>
          <ng-template appCell="lastLogin" let-row>{{ row.lastLoginAt ? (row.lastLoginAt | appDate: 'relative') : 'Nunca' }}</ng-template>
          <ng-template appCell="state" let-row>
            <app-status-badge [label]="row.isActive ? 'Activo' : 'Inactivo'" [tone]="row.isActive ? 'success' : 'neutral'" />
          </ng-template>
        </app-data-table>
      </section>
    </div>
  `,
  styles: `
    .user-cell { display: flex; align-items: center; gap: 10px; }
    .avatar { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 50%; background: var(--app-olive-100); color: var(--app-olive-800); font-size: 0.75rem; font-weight: 700; flex: none; }
    .strong { font-weight: 500; }
    .roles { display: flex; flex-wrap: wrap; gap: 4px; }
    .role { padding: 1px 8px; border-radius: 4px; background: var(--app-tone-neutral-bg); font-size: 0.72rem; font-weight: 600; letter-spacing: 0.02em; }
  `,
})
export class UserListComponent {
  private readonly identity = inject(IdentityService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly P = P;

  private readonly roles = rxResource({ stream: () => this.identity.listRoles().pipe(map((roles) => roles.map((r) => r.name))) });
  protected readonly roleNames = () => valueOf(this.roles) ?? [];
  protected readonly list = pagedList<UserListItemDto, QueryParams>((page, f) => this.identity.listUsers(page, f), { isActive: true });

  protected readonly columns: TableColumn<UserListItemDto>[] = [
    { key: 'name', header: 'Usuario' },
    { key: 'email', header: 'Correo', value: (u) => u.email, hideOnMobile: true },
    { key: 'job', header: 'Cargo', value: (u) => u.jobTitle ?? '—', hideOnMobile: true },
    { key: 'roles', header: 'Roles' },
    { key: 'lastLogin', header: 'Ultimo acceso', width: '140px', hideOnMobile: true },
    { key: 'state', header: 'Estado', width: '110px' },
  ];

  open(user: UserListItemDto): void {
    void this.router.navigate(['/usuarios', user.id]);
  }

  create(): void {
    const toSelect = map((items: { id: number; label: string }[]) => items.map((i) => ({ value: i.id, label: i.label })));
    this.dialogs
      .form({
        title: 'Nuevo usuario',
        submitText: 'Crear usuario',
        fields: [
          { key: 'firstName', label: 'Nombres', required: true, maxLength: 100 },
          { key: 'lastName', label: 'Apellidos', required: true, maxLength: 100 },
          { key: 'userName', label: 'Usuario', required: true, maxLength: 100, hint: 'Para iniciar sesion' },
          { key: 'email', label: 'Correo', type: 'email', required: true, maxLength: 256 },
          { key: 'password', label: 'Contrasena inicial', required: true, maxLength: 128, hint: 'Minimo 10 caracteres con mayuscula, minuscula, numero y simbolo' },
          { key: 'employeeCode', label: 'Codigo de empleado', maxLength: 50 },
          { key: 'jobTitle', label: 'Cargo', maxLength: 150 },
          { key: 'departmentId', label: 'Departamento', type: 'select', emptyOption: 'Ninguno', options: this.lookups.departments().pipe(toSelect) },
          { key: 'locationId', label: 'Ubicacion', type: 'select', emptyOption: 'Ninguna', options: this.lookups.locations().pipe(toSelect) },
        ],
        save: (value) => this.identity.createUser(value as never),
      })
      .subscribe((user) => {
        this.toast.success('Usuario creado con el rol USER. Asigne roles adicionales si corresponde.');
        void this.router.navigate(['/usuarios', user.id]);
      });
  }
}
