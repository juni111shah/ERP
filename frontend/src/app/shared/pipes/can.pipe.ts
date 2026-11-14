import { Pipe, PipeTransform, inject } from '@angular/core';
import { SessionStore } from '@core/session/session.store';
import { holdsPermission } from '../directives/has-permission.directive';

/**
 * `[disabled]="!('finance.invoices.issue' | can)"` or `@if ('hr.payroll.post' | can) { ... }`: true when the user holds the permission
 * (a list: any one of them). Impure, because it must follow the session when an administrator changes this person's access.
 */
@Pipe({ name: 'can', standalone: true, pure: false })
export class CanPipe implements PipeTransform {
  private readonly session = inject(SessionStore);
  transform(permission: string | readonly string[] | null | undefined): boolean {
    return holdsPermission(this.session, permission);
  }
}
