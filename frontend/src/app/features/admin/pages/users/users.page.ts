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
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { AdminApi, RoleDto, RolesApi, UserSummary } from '@core/api/admin.api';
import { controlError, problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { TableToolsComponent, matchText } from '@shared/components/table-tools/table-tools.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { showFormSaveError } from '@shared/forms/save-errors';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { userInitials, userStats } from '../../admin-access';

/** Users: invite, change roles, deactivate / reactivate. The server refuses to deactivate yourself or the last administrator. */
@Component({
  selector: 'erp-users',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatePanelComponent, StatCardComponent, TranslatePipe, ReactiveFormsModule, FormsModule, HasPermissionDirective, TableToolsComponent, NzToolTipModule, NzTableModule, NzButtonModule, NzDrawerModule, NzFormModule, NzInputModule, NzSelectModule, NzTagModule, NzIconModule],
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
  readonly stats = computed(() => userStats(this.users()));

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

  initials(name?: string): string { return userInitials(name); }
  err(name: string): string { return controlError(this.form.get(name)); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.users().subscribe({ next: (u) => { this.users.set(u); this.loading.set(false); }, error: () => { this.failed.set(true); this.loading.set(false); } });
  }

  /** Opens the drawer: inviting needs a name and a valid email, editing only changes the roles. */
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
    if (this.form.invalid) { revealErrors(this.form); return; }
    const v = this.form.getRawValue();
    const user = this.editing();
    this.saving.set(true);
    const call = user ? this.api.setUserRoles(user.id, v.roleIds)
      : this.api.inviteUser({ email: v.email, fullName: v.fullName, temporaryPassword: v.temporaryPassword || null, roleIds: v.roleIds, legalEntityIds: null });
    call.subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success(user ? `Roles updated for ${user.fullName}` : `User ${v.fullName} invited`); this.load(); },
      error: (e) => {
        this.saving.set(false);
        showFormSaveError(this.form, e, this.message, 'Could not save the user');
      },
    });
  }
}
