import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { forkJoin } from 'rxjs';
import { BrandConversationRow, BrandLeadRow, BrandLeadStatus, BrandMessageRow, PlatformApi } from '../../core/api/platform.api';
import { problemMessage } from '../../core/http/problem-details';
import { AssistantTextComponent } from '../../shared/assistant-text.component';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { PLATFORM_STYLES } from './platform-ui';
import { RowActionsComponent } from '../../shared/row-actions.component';

const PAGE_SIZE = 100;

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
  template: `
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load leads" hint="The server did not answer." actionLabel="Try again" (action)="load()" />
    } @else if (loading()) {
      <div style="padding:64px;text-align:center"><nz-spin nzSimple></nz-spin></div>
    } @else {
      <div class="pf-head"><p>People who asked the public assistant to be contacted. Public chats are deleted after 30 days. Leads stay until you delete them.</p></div>
      <section class="pf-card group" data-testid="leads-list">
        <h2>Leads</h2>
        @if (!leads().length) {
          <p class="pf-empty" data-testid="leads-empty">No leads yet.</p>
        } @else {
          <div class="pf-scroll">
            <table class="pf-table">
              <thead><tr><th>Received</th><th>Person</th><th>What they need</th><th>Status</th><th></th></tr></thead>
              <tbody>
                @for (l of leads(); track l.id) {
                  <tr [attr.data-testid]="'lead-' + l.email">
                    <td class="pf-nowrap">{{ l.createdAt | erpDate }}</td>
                    <td><strong>{{ l.name }}</strong><br /><a [href]="'mailto:' + l.email">{{ l.email }}</a>
                      @if (l.phone) { <br /><span class="pf-small">{{ l.phone }}</span> }
                      @if (l.company) { <br /><span class="pf-muted pf-small">{{ l.company }}</span> }
                      @if (l.hasRegistered) { <br /><erp-status value="active" label="Has registered" /> }
                    </td>
                    <td><span class="need" dir="auto" data-no-i18n>{{ l.need }}</span></td>
                    <td><nz-select [ngModel]="l.status" (ngModelChange)="setStatus(l, $event)" [attr.data-testid]="'lead-status-' + l.email">
                      @for (s of statuses; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option> }
                    </nz-select></td>
                    <td><div class="row-actions">
                      @if (l.conversationId) { <button nz-button nzSize="small" type="button" (click)="openChat(l.conversationId)" [attr.data-testid]="'lead-chat-' + l.email">Chat</button> }
                      <button nz-button nzSize="small" nzDanger type="button" (click)="remove(l)" [attr.data-testid]="'lead-delete-' + l.email">Delete</button>
                    </div></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>
      <section class="pf-card" data-testid="conversations-list">
        <h2>Public chats</h2>
        <p class="pf-muted pf-small" style="margin:-4px 0 12px">Chats where a reply quoted a price that matches no plan are marked and listed first, so you can check them.</p>
        @if (!conversations().length) {
          <p class="pf-empty">No chats yet.</p>
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
                    <td>@if (c.needsReview) { <erp-status value="overdue" label="Check the prices" /> }</td>
                    <td class="erp-cell-actions num"><erp-row-actions><button nz-button nzSize="small" type="button" (click)="openChat(c.id)" data-testid="conversation-open">Read</button></erp-row-actions></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>
    }
    <nz-modal [nzVisible]="chat() !== null" nzTitle="Public chat" [nzFooter]="null" (nzOnCancel)="chat.set(null)">
      <ng-container *nzModalContent>
        <div data-no-i18n data-testid="conversation-transcript">
          @for (m of chat() ?? []; track $index) {
            <div class="turn" [class.visitor]="m.fromVisitor" [class.assistant]="!m.fromVisitor">
              @if (m.fromVisitor) { <span dir="auto">{{ m.text }}</span> } @else { <erp-assistant-text [text]="m.text" /> }
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

  readonly statuses: { value: BrandLeadStatus; label: string }[] = [{ value: 'new', label: 'New' }, { value: 'contacted', label: 'Contacted' }, { value: 'won', label: 'Won' }, { value: 'lost', label: 'Lost' }];
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
