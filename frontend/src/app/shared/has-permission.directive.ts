import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { SessionStore } from '../core/session/session.store';

/** *erpHasPermission="'core.parties.write'" renders the template only when the user holds the permission. */
@Directive({ selector: '[erpHasPermission]', standalone: true })
export class HasPermissionDirective {
  readonly erpHasPermission = input.required<string>();
  private readonly tpl = inject(TemplateRef<unknown>);
  private readonly vcr = inject(ViewContainerRef);
  private readonly session = inject(SessionStore);
  private shown = false;

  constructor() {
    effect(() => {
      const allowed = this.session.hasPermission(this.erpHasPermission());
      if (allowed && !this.shown) { this.vcr.createEmbeddedView(this.tpl); this.shown = true; }
      else if (!allowed && this.shown) { this.vcr.clear(); this.shown = false; }
    });
  }
}
