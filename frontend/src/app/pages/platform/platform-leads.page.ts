import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { createPagedList } from '../../core/api/paged-list';
import { BrandConversationRow, BrandLeadRow, BrandLeadStatus, BrandMessageRow, PlatformApi } from '../../core/api/platform.api';
import { problemMessage } from '../../core/http/problem-details';
import { AssistantTextComponent } from '../../shared/assistant-text.component';
import { ErpDatePipe } from '../../shared/display.pipes';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { PLATFORM_STYLES } from './platform-ui';

const CHATS_SHOWN = 100;
const STATUSES: { value: BrandLeadStatus; label: string }[] = [{ value: 'new', label: 'New' }, { value: 'contacted', label: 'Contacted' }, { value: 'won', label: 'Won' }, { value: 'lost', label: 'Lost' }];
const TABS: { status: BrandLeadStatus | null; label: string; empty: string }[] = [
  { status: null, label: 'All', empty: 'No leads found' },
  ...STATUSES.map((s) => ({ status: s.value, label: s.label, empty: `No leads marked ${s.label.toLowerCase()}` })),
];

/** People who asked the public assistant to be contacted, and the public chats themselves. */
@Component({
  selector: 'erp-platform-leads',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzButtonModule, NzCheckboxModule, NzModalModule, NzSelectModule, NzSpinModule, NzTabsModule, AssistantTextComponent, ErpDatePipe, PagedTableComponent, StatePanelComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .group { margin-bottom: 16px; }
    .pf-tabs { margin-bottom: 8px; }
    .stack { display: flex; flex-direction: column; align-items: flex-start; min-width: 0; }
    .need { display: block; max-width: 360px; white-space: pre-wrap; overflow-wrap: anywhere; }
    nz-select { width: 130px; }
    .row-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 6px; }
    .said { max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .turn { padding: 8px 12px; border-radius: 10px; margin-bottom: 8px; max-width: 90%; }
    .turn.visitor { margin-inline-start: auto; background: var(--erp-color-primary-subtle); white-space: pre-wrap; overflow-wrap: anywhere; }
    .turn.assistant { background: var(--erp-color-bg-subtle); }
  `],
  template: `
    <div class="pf-head"><p>People who asked the public assistant to be contacted. Public chats are deleted after 30 days. Leads stay until you delete them.</p></div>
    <section class="group" data-testid="leads-list">
      <nz-tabset class="pf-tabs" [nzSelectedIndex]="index()" (nzSelectedIndexChange)="select($event)" nzSize="small" data-testid="leads-tabs">
        @for (t of tabs; track t.label) { <nz-tab [nzTitle]="t.label"></nz-tab> }
      </nz-tabset>
      <erp-paged-table [list]="leads" [columns]="columns" searchPlaceholder="Search by name, email or company" [emptyText]="tabs[index()].empty" [filterable]="false" [configurable]="false" [exportable]="false" [clickable]="false"
                       tableName="leads" scrollX="1040px">
        <ng-template #cell let-l let-col="column">
          @switch (col.key) {
            @case ('createdAt') { <span class="pf-nowrap">{{ l.createdAt | erpDate }}</span> }
            @case ('person') {
              <div class="stack" [attr.data-testid]="'lead-' + l.email">
                <strong>{{ l.name }}</strong><a [href]="'mailto:' + l.email">{{ l.email }}</a>
                @if (l.phone) { <span class="pf-small">{{ l.phone }}</span> }
                @if (l.company) { <span class="pf-muted pf-small">{{ l.company }}</span> }
                @if (l.hasRegistered) { <erp-status value="active" label="Has registered" /> }
              </div>
            }
            @case ('need') { <span class="need" dir="auto" data-no-i18n>{{ l.need }}</span> }
            @case ('status') {
              <nz-select [ngModel]="l.status" (ngModelChange)="setStatus(l, $event)" [attr.data-testid]="'lead-status-' + l.email">
                @for (s of statuses; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option> }
              </nz-select>
            }
            @case ('rowActions') {
              <div class="row-actions">
                @if (l.conversationId) { <button nz-button nzSize="small" type="button" (click)="openChat(l.conversationId)" [attr.data-testid]="'lead-chat-' + l.email">Chat</button> }
                <button nz-button nzSize="small" nzDanger type="button" (click)="remove(l)" [attr.data-testid]="'lead-delete-' + l.email">Delete</button>
              </div>
            }
          }
        </ng-template>
      </erp-paged-table>
    </section>
    <section class="pf-card" data-testid="conversations-list">
      <h2>Public chats</h2>
      <p class="pf-muted pf-small" style="margin:-4px 0 12px">Chats where a reply quoted a price that matches no plan are marked and listed first, so you can check them. Chats where a visitor marked an answer as not helpful are marked too.</p>
      <label nz-checkbox style="margin-bottom:12px" [ngModel]="onlyNotHelpful()" (ngModelChange)="filterNotHelpful($event)" data-testid="conversations-not-helpful">Only chats with an answer marked not helpful</label>
      @if (failed()) {
        <erp-state-panel kind="error" title="Could not load the public chats" hint="The server did not answer." actionLabel="Try again" (action)="load()" />
      } @else if (loading()) {
        <div style="padding:32px;text-align:center"><nz-spin nzSimple></nz-spin></div>
      } @else if (!conversations().length) {
        <p class="pf-empty">{{ onlyNotHelpful() ? 'No chats with an answer marked not helpful.' : 'No chats yet.' }}</p>
      } @else {
        <div class="pf-scroll">
          <table class="pf-table">
            <thead><tr><th>Last message</th><th>First question</th><th class="num">Questions</th><th></th><th></th></tr></thead>
            <tbody>
              @for (c of conversations(); track c.id) {
                <tr data-testid="conversation-row">
                  <td class="pf-nowrap">{{ c.lastMessageAt | erpDate }}</td>
                  <td><div class="said" dir="auto" data-no-i18n>{{ c.firstMessage }}</div></td>
                  <td class="num">{{ c.visitorMessages }}</td>
                  <td>
                    @if (c.needsReview) { <erp-status value="overdue" label="Check the prices" /> }
                    @if (c.notHelpful) { <erp-status value="overdue" label="Not helpful" data-testid="conversation-not-helpful" /> }
                  </td>
                  <td><div class="row-actions">
                    <button nz-button nzSize="small" type="button" (click)="openChat(c.id)" data-testid="conversation-open">Read</button>
                    @if (canManage()) {
                      @if (c.needsReview) { <button nz-button nzSize="small" type="button" (click)="markReviewed(c)" data-testid="conversation-reviewed">Mark reviewed</button> }
                      <button nz-button nzSize="small" nzDanger type="button" (click)="removeChat(c)" data-testid="conversation-delete">Delete</button>
                    }
                  </div></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </section>
    <nz-modal [nzVisible]="chat() !== null" nzTitle="Public chat" [nzFooter]="null" (nzOnCancel)="chat.set(null)">
      <ng-container *nzModalContent>
        <div data-no-i18n data-testid="conversation-transcript">
          @for (m of chat() ?? []; track $index) {
            <div class="turn" [class.visitor]="m.fromVisitor" [class.assistant]="!m.fromVisitor">
              @if (m.fromVisitor) { <span dir="auto">{{ m.text }}</span> } @else { <erp-assistant-text [text]="m.text" /> }
              @if (m.helpful === false) { <erp-status value="overdue" label="Not helpful" data-testid="transcript-not-helpful" /> }
              @if (m.helpful === true) { <erp-status value="active" label="Helpful" data-testid="transcript-helpful" /> }
            </div>
          } @empty { <p class="pf-empty">This chat has been deleted.</p> }
        </div>
      </ng-container>
    </nz-modal>`,
})
export class PlatformLeadsPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly tabs = TABS;
  readonly statuses = STATUSES;
  readonly index = signal(0);
  readonly leads = createPagedList<BrandLeadRow>((q) => this.api.leads(q));
  readonly columns: TableColumn<BrandLeadRow>[] = [
    { key: 'createdAt', label: 'Received', width: '150px', value: (l) => l.createdAt },
    { key: 'person', label: 'Person', width: '240px', value: (l) => l.name },
    { key: 'need', label: 'What they need', width: '320px', value: (l) => l.need },
    { key: 'status', label: 'Status', width: '150px', value: (l) => l.status },
    { key: 'rowActions', label: '', width: '150px' },
  ];
  readonly conversations = signal<BrandConversationRow[]>([]);
  readonly chat = signal<BrandMessageRow[] | null>(null);
  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly onlyNotHelpful = signal(false);
  /** Only the owner can change a chat. The server decides; this just hides buttons that would be refused. */
  readonly canManage = signal(false);

  constructor() {
    this.load();
    this.api.assistant().subscribe({ next: (v) => this.canManage.set(v.canManage), error: () => undefined });
  }

  load(): void {
    this.failed.set(false);
    this.api.brandConversations({ page: 1, pageSize: CHATS_SHOWN, extra: this.onlyNotHelpful() ? { notHelpful: true } : {} }).subscribe({
      next: (r) => { this.conversations.set(r.items); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  filterNotHelpful(on: boolean): void {
    this.onlyNotHelpful.set(on);
    this.load();
  }

  select(index: number): void {
    this.index.set(index);
    const status = TABS[index].status;
    this.leads.setExtra(status ? { status } : {});
  }

  /** The list and the new-lead badge both depend on what just changed. */
  private leadsChanged(): void {
    this.leads.reload();
    this.api.dashboard().subscribe({ error: () => undefined });
  }

  setStatus(lead: BrandLeadRow, status: BrandLeadStatus): void {
    this.api.setLeadStatus(lead.id, status).subscribe({
      next: () => this.leadsChanged(),
      error: (e) => { this.message.error(problemMessage(e, 'Could not change the status')); this.leads.reload(); },
    });
  }

  remove(lead: BrandLeadRow): void {
    this.modal.confirm({
      nzTitle: `Delete the lead from ${lead.name}?`,
      nzContent: 'Their contact details are permanently removed from this system.',
      nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.deleteLead(lead.id).subscribe({
        next: () => this.leadsChanged(),
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

  markReviewed(chat: BrandConversationRow): void {
    this.api.markConversationReviewed(chat.id).subscribe({
      next: () => this.load(),
      error: (e) => this.message.error(problemMessage(e, 'Could not mark this chat as reviewed')),
    });
  }

  removeChat(chat: BrandConversationRow): void {
    this.modal.confirm({
      nzTitle: 'Delete this public chat?',
      nzContent: 'The whole conversation is permanently removed from this system. A lead that came from it stays.',
      nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.deleteConversation(chat.id).subscribe({
        next: () => this.conversations.update((list) => list.filter((c) => c.id !== chat.id)),
        error: (e) => this.message.error(problemMessage(e, 'Could not delete this chat')),
      }),
    });
  }
}
