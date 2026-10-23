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
import { AdminApi, RoleDto, RolesApi, UserSummary } from '../../core/api/admin.api';
import { applyProblemDetailsToForm, asProblem, controlError } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { RevealDirective } from '../../shared/reveal.directive';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Users: invite, change roles, deactivate / reactivate. The server refuses to deactivate yourself or the last administrator. */
@Component({
  selector: 'erp-users',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, ReactiveFormsModule, FormsModule, HasPermissionDirective, RevealDirective, TableToolsComponent, NzToolTipModule, NzTableModule, NzButtonModule, NzDrawerModule, NzFormModule, NzInputModule, NzSelectModule, NzTagModule, NzIconModule],
  styles: [`.bar { display:flex; margin-bottom:12px } .spacer { flex:1 }`],
  template: `
    <h1 class="erp-page-title">Users</h1>
    <div class="bar"><span class="spacer"></span>
      <button *erpHasPermission="'core.users.write'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.inviteUser' | translate: 'Invite user'" nz-tooltip [nzTooltipTitle]="'ui.add.inviteUser' | translate: 'Invite user'" nzType="primary" (click)="open(null)" data-testid="user-new"><span nz-icon nzType="plus"></span>Invite user</button>
    </div>
    <erp-table-tools [(search)]="q" (refresh)="load()" [loading]="loading()" testId="users">
      <nz-select [ngModel]="status()" (ngModelChange)="status.set($event)" nzAllowClear [nzPlaceHolder]="'ui.common.filterStatus' | translate: 'Filter by status'" style="width:160px" data-testid="users-status-filter">
        <nz-option nzValue="active" [nzLabel]="'ui.common.active' | translate: 'Active'"></nz-option><nz-option nzValue="inactive" [nzLabel]="'ui.common.inactive' | translate: 'Inactive'"></nz-option>
      </nz-select>
    </erp-table-tools>
    <nz-table [nzData]="shown()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" data-testid="users-table">
      <thead><tr><th>Name</th><th>Email</th><th>Roles</th><th>Status</th><th>Last sign-in</th><th></th></tr></thead>
      <tbody>
        @for (u of shown(); track u.id) {
          <tr [attr.data-testid]="'user-' + u.email">
            <td>{{ u.fullName }}</td><td>{{ u.email }}</td><td>{{ u.roles.join(', ') }}</td>
            <td><nz-tag [nzColor]="u.isActive ? 'green' : 'default'">{{ u.isActive ? 'Active' : 'Inactive' }}</nz-tag></td>
            <td>{{ u.lastSignInAt ? (u.lastSignInAt.slice(0, 16).replace('T', ' ')) : '—' }}</td>
            <td *erpHasPermission="'core.users.write'">
              <a (click)="open(u)" data-testid="user-edit">Roles</a> ·
              <a (click)="toggleActive(u)" data-testid="user-toggle">{{ u.isActive ? 'Deactivate' : 'Reactivate' }}</a>
            </td>
          </tr>
        }
      </tbody>
    </nz-table>
    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="480" [nzTitle]="editing() ? editing()!.fullName : 'Invite user'" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="user-form">
          @if (!editing()) {
            <nz-form-item><nz-form-label nzRequired>Full name</nz-form-label><nz-form-control [nzErrorTip]="err('fullName')"><input nz-input formControlName="fullName" data-testid="user-name" /></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label nzRequired>Email</nz-form-label><nz-form-control [nzErrorTip]="err('email')"><input nz-input formControlName="email" data-testid="user-email" /></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label>Temporary password</nz-form-label><nz-form-control [nzErrorTip]="err('temporaryPassword')" nzExtra="Leave empty to generate one."><input nz-input type="password" formControlName="temporaryPassword" autocomplete="new-password" /></nz-form-control></nz-form-item>
          }
          <nz-form-item><nz-form-label nzRequired>Roles</nz-form-label><nz-form-control [nzErrorTip]="err('roleIds') || 'Choose at least one role'">
            <nz-select formControlName="roleIds" nzMode="multiple" data-testid="user-roles">@for (r of roles(); track r.id) { <nz-option [nzValue]="r.id" [nzLabel]="r.name"></nz-option> }</nz-select>
          </nz-form-control></nz-form-item>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Cancel</button>
      <button nz-button nzType="primary" [nzLoading]="saving()" (click)="save()" style="margin-inline-start:8px" data-testid="user-save">Save</button>
    </ng-template>`,
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
  readonly loading = signal(false);
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
    this.api.users().subscribe({ next: (u) => { this.users.set(u); this.loading.set(false); }, error: () => this.loading.set(false) });
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
      next: () => { this.message.success(user.isActive ? 'User deactivated' : 'User reactivated'); this.load(); },
      error: (e) => { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not change the user'); },
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
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success(user ? 'Roles updated' : 'User invited'); this.load(); },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(rest[0] ?? p?.detail ?? p?.title ?? 'Could not save the user');
      },
    });
  }
}
