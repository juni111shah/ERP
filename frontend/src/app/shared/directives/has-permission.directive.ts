import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { SessionStore } from '@core/session/session.store';

/**
 * Renders the template only when the user holds the permission:
 * `*erpHasPermission="'core.parties.write'"`, any one of several: `*erpHasPermission="['finance.bills.post', 'finance.bills.submit']"`,
 * all of them: `*erpHasPermission="['a.b.write', 'a.b.post']; mode: 'all'"`. An empty value always renders.
 * It follows the session live, so a permission given or taken away (see AccessSync) shows or hides the element at once.
 */
@Directive({ selector: '[erpHasPermission]', standalone: true })
export class HasPermissionDirective {
  readonly erpHasPermission = input.required<string | readonly string[] | null | undefined>();
  /** 'any' (default): one of the listed permissions is enough; 'all': every one is needed. */
  readonly erpHasPermissionMode = input<'any' | 'all'>('any');
  private readonly tpl = inject(TemplateRef<unknown>);
  private readonly vcr = inject(ViewContainerRef);
  private readonly session = inject(SessionStore);
  private shown = false;

  constructor() {
    effect(() => {
      const allowed = holdsPermission(this.session, this.erpHasPermission(), this.erpHasPermissionMode());
      if (allowed && !this.shown) { this.vcr.createEmbeddedView(this.tpl); this.shown = true; }
      else if (!allowed && this.shown) { this.vcr.clear(); this.shown = false; }
    });
  }
}

/** Whether the session holds the permission(s): a string, or a list checked as any-of (default) or all-of. Empty means allowed. */
export function holdsPermission(session: Pick<SessionStore, 'hasPermission'>, wanted: string | readonly string[] | null | undefined, mode: 'any' | 'all' = 'any'): boolean {
  if (!wanted || (Array.isArray(wanted) && wanted.length === 0)) return true;
  const list = typeof wanted === 'string' ? [wanted] : wanted;
  return mode === 'all' ? list.every((p) => session.hasPermission(p)) : list.some((p) => session.hasPermission(p));
}
