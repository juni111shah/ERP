import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AdminApi, RoleDto, RolesApi, UserSummary } from '@core/api/admin.api';
import { applyProblemDetailsToForm, asProblem, problemMessage, controlError } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';

import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Users: invite, change roles, deactivate / reactivate. The server refuses to deactivate yourself or the last administrator. */
@Component({
  selector: 'erp-users',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatePanelComponent, StatCardComponent, TranslatePipe, ReactiveFormsModule, FormsModule, HasPermissionDirective, RevealDirective, TableToolsComponent, NzToolTipModule, NzTableModule, NzButtonModule, NzDrawerModule, NzFormModule, NzInputModule, NzSelectModule, NzTagModule, NzIconModule],
  styleUrl: './users.page.scss',
  templateUrl: './users.page.html',
})
export class UsersPage {
  private readonly api = inject(AdminApi);
  private readonly rolesApi = inject(RolesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  readonly users = signal<UserSummary[]>([]);
  readonly roles = signal<RoleDto[]>([]);
  readonly q = signal('');
  readonly status = signal<'active' | 'inactive' | null>(null);
  /** Client-side filter: GET /admin/users takes no query parameters. */
  readonly shown = computed(() => this.users().filter((u) => matchText(this.q(), u.fullName, u.email, u.roles.join(' ')) && (!this.status() || u.isActive === (this.status() === 'active'))));

  readonly stats = computed(() => {
    const list = this.users();
    const active = list.filter((u) => u.isActive).length;
    const admins = list.filter((u) => u.roles.some((r) => r.toLowerCase().includes('admin'))).length;
    const recent = list.filter((u) => !!u.lastSignInAt).length;
    return { active, admins, recent, total: list.length };
  });

  initials(name?: string): string {
    return (name || '').split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase() || 'U';
  }

  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly editing = signal<UserSummary | null>(null);
  readonly form = this.fb.nonNullable.group({
    fullName: [''], email: [''], temporaryPassword: [''], roleIds: [[] as string[], Validators.required],
  });

  constructor() {
    this.load();
    this.rolesApi.list({ page: 1, pageSize: 100, sort: 'name' }).subscribe((r) => this.roles.set(r.items));
  }

  err(name: string): string { return controlError(this.form.get(name)); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.users().subscribe({ next: (u) => { this.users.set(u); this.loading.set(false); }, error: () => { this.failed.set(true); this.loading.set(false); } });
  }

  open(user: UserSummary | null): void {
    this.editing.set(user);
    this.form.reset({ fullName: '', email: '', temporaryPassword: '', roleIds: user?.roleIds ?? [] });
    const invite = !user;
    for (const k of ['fullName', 'email'] as const) this.form.controls[k].setValidators(invite ? [Validators.required] : []);
    this.form.controls.email.addValidators(invite ? [Validators.email] : []);
    Object.values(this.form.controls).forEach((c) => c.updateValueAndValidity());
    this.drawerOpen.set(true);
  }

  toggleActive(user: UserSummary): void {
    this.api.setUserActive(user.id, !user.isActive).subscribe({
      next: () => { this.message.success(`User ${user.fullName} ${user.isActive ? 'deactivated' : 'reactivated'}`); this.load(); },
      error: (e) => { this.message.error(problemMessage(e, 'Could not change the user')); },
    });
  }

  save(): void {
    if (this.form.invalid) { Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const user = this.editing();
    this.saving.set(true);
    const call = user ? this.api.setUserRoles(user.id, v.roleIds)
      : this.api.inviteUser({ email: v.email, fullName: v.fullName, temporaryPassword: v.temporaryPassword || null, roleIds: v.roleIds, legalEntityIds: null });
    call.subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success(user ? `Roles updated for ${user.fullName}` : `User ${v.fullName} invited`); this.load(); },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(rest[0] ?? p?.detail ?? p?.title ?? 'Could not save the user');
      },
    });
  }
}
