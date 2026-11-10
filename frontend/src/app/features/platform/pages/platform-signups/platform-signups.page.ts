import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { createPagedList } from '@core/api/paged-list';
import { IndustryOption, PlatformApi, SignupRow, SignupStatus } from '@core/api/platform.api';
import { displayLabel } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { askReason } from '@shared/utils/reason-prompt';
import { PLATFORM_STYLES, cycleWords } from '../../platform-ui';

/** One tab of registrations: the status it lists and what it says. */
interface SignupTab { status: SignupStatus; label: string; empty: string; hint: string; }

const TABS: SignupTab[] = [
  { status: 'pendingReview', label: 'Waiting for review', empty: 'No registrations are waiting for you', hint: 'Approving creates the workspace and starts its trial. They sign in with the password they chose.' },
  { status: 'pendingEmail', label: 'Email not confirmed', empty: 'Nobody is waiting on a confirmation email', hint: 'They have not opened the link yet. You can approve anyway if you know them.' },
  { status: 'provisioned', label: 'Created', empty: 'No workspace has been created from a registration yet', hint: '' },
  { status: 'rejected', label: 'Rejected', empty: 'Nothing has been rejected', hint: '' },
  { status: 'expired', label: 'Expired', empty: 'Nothing has expired', hint: 'The confirmation link ran out, or they registered again.' },
];

/** People who asked for a workspace: approve, reject, and a link to the client once it exists. */
@Component({
  selector: 'erp-platform-signups',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzModalModule, NzTabsModule, ErpDatePipe, PagedTableComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .pf-tabs { margin-bottom: 8px; }
    .stack { display: flex; flex-direction: column; min-width: 0; }
    .row-actions { display: flex; gap: 6px; }
    .hint { margin: 0 0 12px; color: var(--erp-color-text-muted); }
  `],
  templateUrl: './platform-signups.page.html',
})
export class PlatformSignupsPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly tabs = TABS;
  readonly index = signal(0);
  readonly list = createPagedList<SignupRow>((q) => this.api.signups(q), { extra: { status: TABS[0].status } });
  readonly columns: TableColumn<SignupRow>[] = [
    { key: 'business', label: 'Business', width: '170px', value: (r) => r.businessName },
    { key: 'contact', label: 'Contact', width: '220px', value: (r) => r.contactName },
    { key: 'where', label: 'Industry and country', width: '160px', value: (r) => r.industry },
    { key: 'plan', label: 'Plan', width: '110px', value: (r) => r.planName },
    { key: 'createdAt', label: 'Registered', width: '150px', value: (r) => r.createdAt },
    { key: 'email', label: 'Email', width: '120px', value: (r) => r.emailVerifiedAt },
    { key: 'rowActions', label: '', width: '170px' },
  ];

  cycleText = cycleWords;
  private readonly industries = signal<IndustryOption[]>([]);

  constructor() {
    loadInto(this.industries, this.api.industries());
  }

  industryName(key: string): string { return this.industries().find((i) => i.key === key)?.label ?? displayLabel(key); }

  select(index: number): void {
    this.index.set(index);
    this.list.setExtra({ status: TABS[index].status });
  }

  /** After a decision: say so, reload the list and refresh the menu's waiting count (which the dashboard call updates). */
  private changed(text: string): void {
    this.message.success(text);
    this.list.reload();
    this.api.dashboard().subscribe({ error: () => undefined });
  }

  approve(r: SignupRow): void {
    this.modal.confirm({
      nzTitle: `Approve ${r.businessName}?`,
      nzContent: `This creates the workspace "${r.signInKey}" on the ${r.planName} plan and starts its trial. ${r.email} can then sign in.`,
      nzOkText: 'Approve',
      nzCancelText: 'Not now',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.api.approveSignup(r.id).subscribe({
          next: () => { this.changed(`${r.businessName} approved`); resolve(); },
          error: (e) => { this.message.error(problemMessage(e, 'Could not approve this registration')); resolve(); },
        });
      }),
    });
  }

  async reject(r: SignupRow): Promise<void> {
    const reason = await askReason(this.modal, `Reject ${r.businessName}?`, 'Reject', 'Nothing is created, and they are told by email when email is on. Any reason you give is included.', false);
    if (reason === null) return;
    this.api.rejectSignup(r.id, reason || null).subscribe({
      next: () => this.changed(`${r.businessName} rejected`),
      error: (e) => this.message.error(problemMessage(e, 'Could not reject this registration')),
    });
  }
}
