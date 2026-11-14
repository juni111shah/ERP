import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { ProblemDetails } from '@core/api/api.models';
import { PlatformApi, PlatformTeamMember } from '@core/api/platform.api';
import { problemMessage } from '@core/http/problem-details';
import { PlatformRole, SessionStore } from '@core/session/session.store';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { showDrawerSaveError } from '@shared/forms/save-errors';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { OneTimePasswordComponent, OneTimeSecret } from '../../components/one-time-password/one-time-password.component';
import { DrawerValue } from '../../platform-forms';
import { PLATFORM_STYLES } from '../../platform-ui';

type TeamRole = Exclude<PlatformRole, 'owner'>;

const ROLE_TEXT: Record<PlatformRole, string> = { owner: 'Owner', manager: 'Manager', support: 'Support' };

/** Who can open this console, and what each may do there. Managers add and remove members; only the owner makes managers. */
@Component({
  selector: 'erp-platform-team',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzSelectModule, RowActionsComponent, NzButtonModule, NzIconModule, NzModalModule, NzSpinModule, ErpDatePipe, OneTimePasswordComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES],
  templateUrl: './platform-team.page.html',
})
export class PlatformTeamPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);

  readonly team = signal<PlatformTeamMember[]>([]);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly open = signal(false);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly secret = signal<OneTimeSecret | null>(null);
  readonly myId = computed(() => this.session.me()?.user.id);
  readonly canManage = this.session.platformManager;
  readonly isOwner = this.session.platformOwner;
  /** The member whose role is being changed. */
  readonly busy = signal<string | null>(null);
  /** The owner also picks a new member's role; a manager adds support members. */
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'fullName', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'email', label: 'Email', type: 'text', required: true, maxLength: 256 },
    ...(this.isOwner()
      ? [{ key: 'role', label: 'Role', type: 'select', required: true, default: 'support', options: [{ value: 'support', label: 'Support' }, { value: 'manager', label: 'Manager' }] } as FieldSpec]
      : []),
  ]);

  constructor() { this.load(); }

  roleText(role: PlatformRole | undefined): string { return ROLE_TEXT[role ?? 'support']; }

  /** A manager removes support members; the owner removes anyone but themself. The API holds the same rules. */
  canRemove(m: PlatformTeamMember): boolean {
    if (!this.canManage() || m.userId === this.myId() || m.role === 'owner') return false;
    return m.role !== 'manager' || this.isOwner();
  }

  load(): void {
    this.failed.set(false);
    this.api.team().subscribe({
      next: (t) => { this.team.set(t); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  /** Adds a member and shows their temporary password once. */
  add(v: DrawerValue): void {
    this.saving.set(true);
    this.errors.set(null);
    const role: TeamRole = v['role'] === 'manager' ? 'manager' : 'support';
    this.api.addTeamMember({ fullName: v['fullName'] as string, email: v['email'] as string, role }).subscribe({
      next: (r) => {
        this.saving.set(false);
        this.open.set(false);
        this.message.success(`${r.member.fullName} added`);
        this.secret.set({ title: 'Team member added', password: r.temporaryPassword, facts: [{ label: 'Name', value: r.member.fullName }, { label: 'Email', value: r.member.email }, { label: 'Role', value: this.roleText(r.member.role) }] });
        this.load();
      },
      error: (e) => {
        this.saving.set(false);
        showDrawerSaveError(e, this.errors, this.message, 'Could not add this person');
      },
    });
  }

  /** Owner only. The member's open console shows or hides the manager actions at once (the server tells their screens). */
  setRole(m: PlatformTeamMember, role: TeamRole): void {
    if (role === m.role || !this.isOwner()) return;
    this.busy.set(m.userId);
    this.api.setTeamRole(m.userId, role).subscribe({
      next: (r) => { this.busy.set(null); this.message.success(`${r.fullName} is now ${role === 'manager' ? 'a manager' : 'support'}`); this.load(); },
      error: (e) => { this.busy.set(null); this.message.error(problemMessage(e, 'Could not change the role')); this.load(); },
    });
  }

  remove(m: PlatformTeamMember): void {
    this.modal.confirm({
      nzTitle: `Remove ${m.fullName} from the team?`,
      nzContent: 'They lose access to this console at once. Their sign-in and any client workspaces they belong to are not touched.',
      nzOkText: 'Remove',
      nzOkDanger: true,
      nzCancelText: 'Keep',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.api.removeTeamMember(m.userId).subscribe({
          next: () => { this.message.success(`${m.fullName} removed`); this.load(); resolve(); },
          error: (e) => { this.message.error(problemMessage(e, 'Could not remove this person')); resolve(); },
        });
      }),
    });
  }
}
