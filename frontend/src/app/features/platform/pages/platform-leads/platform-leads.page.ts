import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { forkJoin } from 'rxjs';
import { BrandConversationRow, BrandLeadRow, BrandLeadStatus, BrandMessageRow, PlatformApi } from '@core/api/platform.api';
import { problemMessage } from '@core/http/problem-details';
import { AssistantTextComponent } from '@shared/components/assistant-text/assistant-text.component';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { PLATFORM_STYLES } from '../../platform-ui';

const PAGE_SIZE = 100;
const LEAD_STATUSES: { value: BrandLeadStatus; label: string }[] = [{ value: 'new', label: 'New' }, { value: 'contacted', label: 'Contacted' }, { value: 'won', label: 'Won' }, { value: 'lost', label: 'Lost' }];

/** People who asked the public assistant to be contacted, and the public chats themselves. */
@Component({
  selector: 'erp-platform-leads',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, FormsModule, NzButtonModule, NzModalModule, NzSelectModule, NzSpinModule, AssistantTextComponent, ErpDatePipe, StatePanelComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .group { margin-bottom: 16px; }
    .need { display: block; max-width: 360px; white-space: pre-wrap; overflow-wrap: anywhere; }
    nz-select { width: 130px; }
    .row-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
    .said { max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .turn { padding: 8px 12px; border-radius: 10px; margin-bottom: 8px; max-width: 90%; }
    .turn.visitor { margin-inline-start: auto; background: var(--erp-color-primary-subtle); white-space: pre-wrap; overflow-wrap: anywhere; }
    .turn.assistant { background: var(--erp-color-bg-subtle); }
  `],
  templateUrl: './platform-leads.page.html',
})
export class PlatformLeadsPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly statuses = LEAD_STATUSES;
  readonly leads = signal<BrandLeadRow[]>([]);
  readonly conversations = signal<BrandConversationRow[]>([]);
  readonly chat = signal<BrandMessageRow[] | null>(null);
  readonly loading = signal(true);
  readonly failed = signal(false);

  constructor() { this.load(); }

  load(): void {
    this.failed.set(false);
    forkJoin({ leads: this.api.leads({ page: 1, pageSize: PAGE_SIZE }), conversations: this.api.brandConversations({ page: 1, pageSize: PAGE_SIZE }) }).subscribe({
      next: (r) => { this.leads.set(r.leads.items); this.conversations.set(r.conversations.items); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  setStatus(lead: BrandLeadRow, status: BrandLeadStatus): void {
    this.api.setLeadStatus(lead.id, status).subscribe({
      next: (row) => this.leads.update((list) => list.map((l) => (l.id === row.id ? row : l))),
      error: (e) => this.message.error(problemMessage(e, 'Could not change the status')),
    });
  }

  remove(lead: BrandLeadRow): void {
    this.modal.confirm({
      nzTitle: `Delete the lead from ${lead.name}?`,
      nzContent: 'Their contact details are permanently removed from this system.',
      nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.deleteLead(lead.id).subscribe({
        next: () => this.leads.update((list) => list.filter((l) => l.id !== lead.id)),
        error: (e) => this.message.error(problemMessage(e, 'Could not delete this lead')),
      }),
    });
  }

  openChat(id: string): void {
    this.api.brandConversation(id).subscribe({
      next: (messages) => this.chat.set(messages),
      error: (e) => this.message.error(problemMessage(e, 'Could not load this chat')),
    });
  }
}
