import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ProblemDetails } from '../../core/api/api.models';
import { PlatformApi, PlatformTeamMember } from '../../core/api/platform.api';
import { asProblem, problemMessage } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';
import { ErpDatePipe } from '../../shared/display.pipes';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { OneTimePasswordComponent, OneTimeSecret, PLATFORM_STYLES } from './platform-ui';
import { SkeletonComponent } from '../../shared/skeleton.component';

/** Who can open this console. */
@Component({
  selector: 'erp-platform-team',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule, NzModalModule, SkeletonComponent, ErpDatePipe, OneTimePasswordComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES],
  template: `
    <div class="pf-head">
      <p>Everyone here sees every client and can change plans, invoices and settings. Add only people who run the business.</p>
      <div class="pf-actions"><button nz-button nzType="primary" type="button" (click)="errors.set(null); open.set(true)" data-testid="team-add"><span nz-icon nzType="user-add"></span>Add team member</button></div>
    </div>
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the team" hint="The server did not answer." actionLabel="Try again" (action)="load()" />
    } @else if (loading()) {
      <erp-skeleton kind="list" [rows]="4" />
    } @else {
      <section class="pf-card" data-testid="team-list">
        <div class="pf-scroll">
          <table class="pf-table">
            <thead><tr><th>Name</th><th>Email</th><th>Last sign-in</th><th>Two-step sign-in</th><th></th></tr></thead>
            <tbody>
              @for (m of team(); track m.userId) {
                <tr [attr.data-testid]="'team-' + m.email">
                  <td class="pf-nowrap">{{ m.fullName }}@if (m.userId === myId()) { <span class="pf-muted pf-small"> (you)</span> }</td>
                  <td>{{ m.email }}</td>
                  <td class="pf-nowrap">{{ m.lastSignInAt ? (m.lastSignInAt | erpDate) : 'Never' }}</td>
                  <td><erp-status [value]="m.twoFactorEnabled ? 'active' : 'inactive'" [label]="m.twoFactorEnabled ? 'On' : 'Off'" /></td>
                  <td class="num"><button nz-button nzSize="small" nzDanger type="button" (click)="remove(m)" [attr.data-testid]="'team-remove-' + m.email">Remove</button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }
    <erp-record-drawer title="Add team member" [open]="open()" [fields]="fields" [saving]="saving()" [errors]="errors()" submitLabel="Add" testId="team-form" (submitted)="add($event)" (closed)="open.set(false)" />
    <erp-one-time-password [secret]="secret()" (closed)="secret.set(null)" />`,
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
