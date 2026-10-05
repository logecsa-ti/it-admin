import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { of } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { PERMISSIONS as P } from '@core/auth/permissions.generated';
import { RoleDto } from '@core/models';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@shared/components/confirm-dialog/confirm.service';
import { PageHeaderComponent } from '@shared/components/page-header/page-header.component';
import { StatusBadgeComponent } from '@shared/components/status-badge/status-badge.component';
import { CanDirective } from '@shared/directives/can.directive';
import { DialogsService } from '@shared/services/dialogs.service';
import { valueOf } from '@shared/utils/resource-utils';
import { IdentityService } from '../identity.service';
import { PermissionMatrixComponent } from '../permission-matrix/permission-matrix.component';

/** Roles a la izquierda; permisos del rol seleccionado a la derecha (ADR-010). */
@Component({
  selector: 'app-roles',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, PageHeaderComponent, StatusBadgeComponent, PermissionMatrixComponent, CanDirective],
  template: `
    <div class="page">
      <app-page-header title="Roles y permisos" subtitle="Los roles del sistema no se pueden renombrar ni eliminar. Los cambios aplican al renovar la sesion.">
        <button mat-flat-button type="button" *appCan="P.RolesManage" (click)="create()"><mat-icon>add</mat-icon>Nuevo rol</button>
      </app-page-header>

      <div class="layout">
        <section class="panel role-list" aria-label="Roles">
          @for (role of roles(); track role.id) {
            <button type="button" class="role" [class.active]="role.id === selectedId()" (click)="selectedId.set(role.id)">
              <div class="role-head">
                <span class="role-name">{{ role.name }}</span>
                @if (role.isSystemRole) { <app-status-badge label="Sistema" tone="neutral" /> }
              </div>
              <span class="subtle">{{ role.userCount }} usuarios · {{ role.permissionCount }} permisos</span>
            </button>
          }
        </section>

        <section class="panel">
          @if (detail.isLoading()) { <mat-progress-bar mode="indeterminate" /> }
          @if (detailData(); as role) {
            <div class="panel-title">
              <div>
                <h2>{{ role.name }}</h2>
                <span class="subtle">{{ role.description }}</span>
              </div>
              <div class="actions" *appCan="P.RolesManage">
                @if (!role.isSystemRole) {
                  <button mat-button type="button" (click)="edit(role)"><mat-icon>edit</mat-icon>Renombrar</button>
                  <button mat-button type="button" (click)="remove(role)"><mat-icon>delete</mat-icon>Eliminar</button>
                }
                @if (!immutable()) {
                  <button mat-flat-button type="button" [disabled]="saving()" (click)="save()">Guardar permisos</button>
                }
              </div>
            </div>
            <div class="panel-body">
              @if (immutable()) { <p class="subtle">Los permisos de SUPER_ADMIN son fijos: siempre tiene todos.</p> }
              <app-permission-matrix [permissions]="permissions()" [(selected)]="selected" [disabled]="immutable() || !canManage()" />
            </div>
          }
        </section>
      </div>
    </div>
  `,
  styles: `
    .layout { display: grid; grid-template-columns: 280px minmax(0, 1fr); gap: var(--app-gap); align-items: start; }
    .role-list { display: flex; flex-direction: column; padding: 6px; }
    .role { all: unset; display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; border-radius: 4px; cursor: pointer; }
    .role:hover { background: var(--app-olive-50); }
    .role.active { background: var(--app-olive-100); }
    .role:focus-visible { outline: 2px solid var(--app-olive-500); }
    .role-head { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    .role-name { font-weight: 600; }
    @media (max-width: 960px) { .layout { grid-template-columns: 1fr; } }
  `,
})
export class RolesComponent {
  private readonly identity = inject(IdentityService);
  private readonly dialogs = inject(DialogsService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly auth = inject(AuthService);
  protected readonly P = P;

  private readonly rolesResource = rxResource({ stream: () => this.identity.listRoles() });
  private readonly permissionsResource = rxResource({ stream: () => this.identity.permissions() });
  protected readonly roles = computed(() => valueOf(this.rolesResource) ?? []);
  protected readonly permissions = computed(() => valueOf(this.permissionsResource) ?? []);
  protected readonly selectedId = signal<number | null>(null);

  protected readonly detail = rxResource({
    params: () => this.selectedId(),
    stream: ({ params }) => (params ? this.identity.roles.get(params) : of(null)),
  });
  protected readonly detailData = computed(() => valueOf(this.detail) ?? null);
  protected readonly selected = signal<string[]>([]);
  protected readonly saving = signal(false);
  protected readonly canManage = computed(() => this.auth.hasAny(P.RolesManage));
  protected readonly immutable = computed(() => this.detailData()?.name === 'SUPER_ADMIN');

  constructor() {
    effect(() => {
      const roles = this.roles();
      if (roles.length && this.selectedId() === null) this.selectedId.set(roles[0].id);
    });
    effect(() => {
      const role = this.detailData();
      if (role) this.selected.set([...role.permissions]);
    });
  }

  save(): void {
    const role = this.detailData();
    if (!role) return;
    this.saving.set(true);
    this.identity.setRolePermissions(role.id, this.selected()).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(`Permisos de ${role.name} actualizados.`);
        this.rolesResource.reload();
      },
      error: () => this.saving.set(false),
    });
  }

  create(): void {
    this.dialogs
      .form({
        title: 'Nuevo rol',
        fields: [
          { key: 'name', label: 'Nombre', required: true, maxLength: 100, hint: 'En mayusculas, p. ej. TI_COMPRAS' },
          { key: 'description', label: 'Descripcion', type: 'textarea', maxLength: 300 },
        ],
        save: (value) => this.identity.roles.create(value as never),
      })
      .subscribe((role) => {
        this.toast.success('Rol creado. Asigne sus permisos.');
        this.rolesResource.reload();
        this.selectedId.set(role.id);
      });
  }

  edit(role: RoleDto | { id: number; name: string; description?: string | null }): void {
    this.dialogs
      .form({
        title: 'Editar rol',
        fields: [
          { key: 'name', label: 'Nombre', required: true, maxLength: 100 },
          { key: 'description', label: 'Descripcion', type: 'textarea', maxLength: 300 },
        ],
        value: role as Record<string, unknown>,
        save: (value) => this.identity.roles.update(role.id, value as never),
      })
      .subscribe(() => {
        this.toast.success('Rol actualizado.');
        this.rolesResource.reload();
        this.detail.reload();
      });
  }

  remove(role: { id: number; name: string }): void {
    this.confirmService
      .confirm({ title: 'Eliminar rol', message: `¿Eliminar ${role.name}? Los usuarios perderan los permisos que otorga.`, confirmText: 'Eliminar', destructive: true })
      .subscribe((ok) => {
        if (ok) this.identity.roles.remove(role.id).subscribe(() => {
          this.toast.success('Rol eliminado.');
          this.selectedId.set(null);
          this.rolesResource.reload();
        });
      });
  }
}
