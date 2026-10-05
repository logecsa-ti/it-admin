import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { map } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { UserDetailDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { AppDatePipe } from '@shared/pipes/app-date.pipe';
import { DialogsService } from '@shared/services/dialogs.service';
import { LookupService } from '@shared/services/lookup.service';
import { valueOf } from '@shared/utils/resource-utils';
import { IdentityService } from '../identity.service';
import { PermissionMatrixComponent } from '../permission-matrix/permission-matrix.component';

@Component({
  selector: 'app-user-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressBarModule,
    MatTabsModule,
    PageHeaderComponent,
    StatusBadgeComponent,
    PermissionMatrixComponent,
    CanDirective,
    AppDatePipe,
  ],
  template: `
    <div class="page">
      @if (user.isLoading() && !data()) { <mat-progress-bar mode="indeterminate" /> }
      @if (data(); as u) {
        <app-page-header [title]="u.fullName" [eyebrow]="u.userName" backLink="/usuarios" [subtitle]="u.jobTitle ?? u.email">
          <button mat-stroked-button type="button" *appCan="P.UsersUpdate" (click)="edit(u)"><mat-icon>edit</mat-icon>Editar</button>
          @if (u.id !== myId()) {
            <ng-container *appCan="P.UsersDisable">
              @if (u.isActive) {
                <button mat-stroked-button type="button" (click)="toggleActive(u)"><mat-icon>person_off</mat-icon>Desactivar</button>
              } @else {
                <button mat-flat-button type="button" (click)="toggleActive(u)"><mat-icon>person</mat-icon>Activar</button>
              }
            </ng-container>
          }
        </app-page-header>

        <section class="panel">
          <div class="panel-title">
            <h2>Cuenta</h2>
            <app-status-badge [label]="u.isActive ? 'Activo' : 'Inactivo'" [tone]="u.isActive ? 'success' : 'neutral'" />
          </div>
          <div class="panel-body">
            <dl class="detail-grid">
              <div><dt>Correo</dt><dd>{{ u.email }}</dd></div>
              <div><dt>Codigo de empleado</dt><dd>{{ u.employeeCode ?? '—' }}</dd></div>
              <div><dt>Departamento</dt><dd>{{ name(departments(), u.departmentId) }}</dd></div>
              <div><dt>Ubicacion</dt><dd>{{ name(locations(), u.locationId) }}</dd></div>
              <div><dt>Ultimo acceso</dt><dd>{{ u.lastLoginAt ? (u.lastLoginAt | appDate) : 'Nunca' }}</dd></div>
              @if (u.lockoutEnd) { <div><dt>Bloqueado hasta</dt><dd class="danger">{{ u.lockoutEnd | appDate }}</dd></div> }
            </dl>
          </div>
        </section>

        <section class="panel">
          <mat-tab-group animationDuration="0ms">
            <mat-tab label="Roles">
              <div class="panel-body">
                <p class="subtle">Los cambios de roles y permisos se aplican cuando el usuario renueva su sesion.</p>
                <div class="role-list">
                  @for (role of roles(); track role.id) {
                    <mat-checkbox [checked]="selectedRoles().includes(role.name)" [disabled]="!canManageRoles()" (change)="toggleRole(role.name, $event.checked)">
                      <span class="role-name">{{ role.name }}</span>
                      <span class="subtle">{{ role.description }}</span>
                    </mat-checkbox>
                  }
                </div>
                @if (canManageRoles()) {
                  <div class="actions save-row">
                    <button mat-flat-button type="button" [disabled]="saving() || !rolesDirty()" (click)="saveRoles(u)">Guardar roles</button>
                  </div>
                }
              </div>
            </mat-tab>
            <mat-tab label="Permisos directos">
              <div class="panel-body">
                <p class="subtle">Excepciones puntuales ademas de los permisos que otorgan sus roles. Prefiera asignar roles.</p>
                <app-permission-matrix [permissions]="permissions()" [(selected)]="selectedPermissions" [disabled]="!canManageRoles()" />
                @if (canManageRoles()) {
                  <div class="actions save-row">
                    <button mat-flat-button type="button" [disabled]="saving()" (click)="savePermissions(u)">Guardar permisos</button>
                  </div>
                }
              </div>
            </mat-tab>
          </mat-tab-group>
        </section>
      }
    </div>
  `,
  styles: `
    .role-list { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 4px 16px; }
    .role-name { font-weight: 600; margin-right: 8px; }
    .save-row { justify-content: flex-end; margin-top: 16px; }
    .danger { color: var(--app-tone-danger-fg); }
  `,
})
export class UserDetailComponent {
  private readonly identity = inject(IdentityService);
  private readonly lookups = inject(LookupService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);
  protected readonly P = P;

  readonly id = input.required<number, string>({ transform: Number });
  protected readonly user = rxResource({ params: () => this.id(), stream: ({ params }) => this.identity.getUser(params) });
  private readonly rolesResource = rxResource({ stream: () => this.identity.listRoles() });
  private readonly permissionsResource = rxResource({ stream: () => this.identity.permissions() });
  private readonly departmentsResource = rxResource({ stream: () => this.lookups.departments() });
  private readonly locationsResource = rxResource({ stream: () => this.lookups.locations() });

  protected readonly data = computed(() => valueOf(this.user));
  protected readonly roles = computed(() => valueOf(this.rolesResource) ?? []);
  protected readonly permissions = computed(() => valueOf(this.permissionsResource) ?? []);
  protected readonly departments = computed(() => valueOf(this.departmentsResource) ?? []);
  protected readonly locations = computed(() => valueOf(this.locationsResource) ?? []);
  protected readonly myId = computed(() => this.auth.user()?.id);
  protected readonly canManageRoles = computed(() => this.auth.hasAny(P.RolesManage));

  protected readonly selectedRoles = signal<string[]>([]);
  protected readonly selectedPermissions = signal<string[]>([]);
  protected readonly saving = signal(false);
  protected readonly rolesDirty = computed(() => {
    const current = this.data()?.roles ?? [];
    const selected = this.selectedRoles();
    return current.length !== selected.length || current.some((r) => !selected.includes(r));
  });

  constructor() {
    effect(() => {
      const user = this.data();
      if (user) {
        this.selectedRoles.set([...user.roles]);
        this.selectedPermissions.set([...user.directPermissions]);
      }
    });
  }

  protected name(items: { id: number; label: string }[], id: number | null | undefined): string {
    return items.find((i) => i.id === id)?.label ?? '—';
  }

  toggleRole(role: string, checked: boolean): void {
    this.selectedRoles.update((roles) => (checked ? [...roles, role] : roles.filter((r) => r !== role)));
  }

  saveRoles(user: UserDetailDto): void {
    this.saving.set(true);
    this.identity.setUserRoles(user.id, this.selectedRoles()).subscribe({
      next: () => this.saved('Roles actualizados.'),
      error: () => this.saving.set(false),
    });
  }

  savePermissions(user: UserDetailDto): void {
    this.saving.set(true);
    this.identity.setUserPermissions(user.id, this.selectedPermissions()).subscribe({
      next: () => this.saved('Permisos directos actualizados.'),
      error: () => this.saving.set(false),
    });
  }

  toggleActive(user: UserDetailDto): void {
    const activate = !user.isActive;
    this.confirmService
      .confirm({
        title: activate ? 'Activar usuario' : 'Desactivar usuario',
        message: activate ? `¿Permitir de nuevo el acceso a ${user.fullName}?` : `${user.fullName} no podra iniciar sesion. Su historial se conserva.`,
        confirmText: activate ? 'Activar' : 'Desactivar',
        destructive: !activate,
      })
      .subscribe((ok) => ok && this.identity.setActive(user.id, activate).subscribe(() => this.saved(activate ? 'Usuario activado.' : 'Usuario desactivado.')));
  }

  edit(user: UserDetailDto): void {
    const toSelect = map((items: { id: number; label: string }[]) => items.map((i) => ({ value: i.id, label: i.label })));
    this.dialogs
      .form({
        title: 'Editar usuario',
        fields: [
          { key: 'firstName', label: 'Nombres', required: true, maxLength: 100 },
          { key: 'lastName', label: 'Apellidos', required: true, maxLength: 100 },
          { key: 'email', label: 'Correo', type: 'email', required: true, maxLength: 256 },
          { key: 'employeeCode', label: 'Codigo de empleado', maxLength: 50 },
          { key: 'jobTitle', label: 'Cargo', maxLength: 150 },
          { key: 'departmentId', label: 'Departamento', type: 'select', emptyOption: 'Ninguno', options: this.lookups.departments().pipe(toSelect) },
          { key: 'locationId', label: 'Ubicacion', type: 'select', emptyOption: 'Ninguna', options: this.lookups.locations().pipe(toSelect) },
        ],
        value: user as unknown as Record<string, unknown>,
        save: (value) => this.identity.updateUser(user.id, value as never),
      })
      .subscribe(() => this.saved('Usuario actualizado.'));
  }

  private saved(message: string): void {
    this.saving.set(false);
    this.toast.success(message);
    this.user.reload();
  }
}
