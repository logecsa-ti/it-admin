import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AuthService } from '@core/auth/auth.service';
import { PermissionCode } from '@core/auth/permissions.generated';

/**
 * Muestra el contenido solo si el usuario tiene alguno de los permisos:
 * `<button *appCan="P.AssetsCreate">Nuevo</button>`. Es cosmetico: la API valida siempre.
 */
@Directive({ selector: '[appCan]' })
export class CanDirective {
  private readonly auth = inject(AuthService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);
  private rendered = false;

  readonly appCan = input.required<PermissionCode | PermissionCode[]>();

  constructor() {
    effect(() => {
      const required = this.appCan();
      const allowed = this.auth.hasAny(...(Array.isArray(required) ? required : [required]));
      if (allowed && !this.rendered) {
        this.container.createEmbeddedView(this.template);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.container.clear();
        this.rendered = false;
      }
    });
  }
}
