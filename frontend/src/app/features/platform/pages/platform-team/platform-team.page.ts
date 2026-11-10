import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { ProblemDetails } from '@core/api/api.models';
import { PlatformApi, PlatformTeamMember } from '@core/api/platform.api';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { OneTimePasswordComponent, OneTimeSecret, PLATFORM_STYLES } from '../../platform-ui';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';

/** Who can open this console. */
@Component({
  selector: 'erp-platform-team',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, NzButtonModule, NzIconModule, NzModalModule, NzSpinModule, ErpDatePipe, OneTimePasswordComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent],
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
  readonly fields: FieldSpec[] = [
    { key: 'fullName', label: 'Name', type: 'text', required: true, maxLength: 200 },
    { key: 'email', label: 'Email', type: 'text', required: true, maxLength: 256 },
  ];

  constructor() { this.load(); }

  load(): void {
    this.failed.set(false);
    this.api.team().subscribe({
      next: (t) => { this.team.set(t); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  add(v: Record<string, unknown>): void {
    this.saving.set(true);
    this.errors.set(null);
    this.api.addTeamMember({ fullName: v['fullName'] as string, email: v['email'] as string }).subscribe({
      next: (r) => {
        this.saving.set(false);
        this.open.set(false);
        this.message.success(`${r.member.fullName} added`);
        this.secret.set({ title: 'Team member added', password: r.temporaryPassword, facts: [{ label: 'Name', value: r.member.fullName }, { label: 'Email', value: r.member.email }] });
        this.load();
      },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        if (!problem?.errors) this.message.error(problemMessage(e, 'Could not add this person'));
      },
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
