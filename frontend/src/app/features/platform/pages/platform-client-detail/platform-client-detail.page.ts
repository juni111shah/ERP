import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { Observable } from 'rxjs';
import { ProblemDetails } from '@core/api/api.models';
import { BillingCycle, ClientDetail, InvoiceStatus, PlanView, PlatformApi, PlatformModule, WorkspaceMember } from '@core/api/platform.api';
import { displayLabel } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { PageTitleService } from '@core/services/page-title.service';
import { SessionStore } from '@core/session/session.store';
import { RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { UsageBarComponent } from '@shared/components/usage-bar/usage-bar.component';
import { showDrawerSaveError } from '@shared/forms/save-errors';
import { ErpDatePipe, ErpMoneyPipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { askReason } from '@shared/utils/reason-prompt';
import { InvoiceActionsComponent } from '../../components/invoice-actions/invoice-actions.component';
import { OneTimePasswordComponent, OneTimeSecret } from '../../components/one-time-password/one-time-password.component';
import { ClientDialogKind, ClientDialogSpec, canActivateClient, canExtendTrial, clientDialogSpec } from '../../platform-client-dialogs';
import { DrawerValue, toAssignPlanInput, toChangePlanInput, toClientNotesInput, toIssueInvoiceInput } from '../../platform-forms';
import { ACCESS_WORDS, PLATFORM_STYLES, cycleWords, invoiceTone, statusWords } from '../../platform-ui';

/** One client: how it stands, what can be done about it, and everything that has happened to it. */
@Component({
  selector: 'erp-platform-client-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, FormsModule, RouterLink, NzAlertModule, NzButtonModule, NzIconModule, NzModalModule, NzSpinModule, NzSwitchModule, ErpDatePipe, ErpMoneyPipe, InvoiceActionsComponent, OneTimePasswordComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent, UsageBarComponent],
  styles: [PLATFORM_STYLES, `
    .back { display: inline-flex; align-items: center; gap: 6px; margin-bottom: 12px; font-size: 13px; }
    .header { margin-bottom: 16px; }
    .head-row { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px 16px; flex-wrap: wrap; margin-bottom: 12px; }
    .head-row h2 { font-size: 20px; font-weight: 700; margin: 0; overflow-wrap: anywhere; color: var(--erp-color-text-primary); }
    .tags { display: flex; flex-wrap: wrap; gap: 4px; }
    .request { background: var(--erp-color-warning-bg); border-color: var(--erp-color-warning-border); margin-bottom: 16px; }
    .request p { margin: 0 0 12px; color: var(--erp-color-text-primary); }
    .bars { display: flex; flex-direction: column; gap: 14px; }
    .note { white-space: pre-line; overflow-wrap: anywhere; }
    .module { align-items: flex-start !important; }
    .module .about { min-width: 0; }
    .module .about small { display: block; color: var(--erp-color-text-muted); font-size: 12.5px; line-height: 1.4; }
    .log time { color: var(--erp-color-text-muted); font-size: 12px; white-space: nowrap; }
    .log li { align-items: flex-start; flex-direction: column; gap: 2px; }
    .log .what { overflow-wrap: anywhere; }
    .assign { margin-bottom: 16px; }
  `],
  templateUrl: './platform-client-detail.page.html',
})
export class PlatformClientDetailPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly pageTitle = inject(PageTitleService);
  /** Suspend, cancel, reactivate, activate, plans, modules and password resets are for managers; the API refuses them to the rest of the team. */
  readonly canManage = inject(SessionStore).platformManager;
  readonly orgId = inject(ActivatedRoute).snapshot.paramMap.get('orgId')!;

  readonly data = signal<ClientDetail | null>(null);
  readonly failed = signal<string | null>(null);
  readonly modules = signal<PlatformModule[]>([]);
  readonly modulesFailed = signal(false);
  readonly plans = signal<PlanView[]>([]);
  readonly countries = signal<{ code: string; name: string }[]>([]);
  readonly industries = signal<{ key: string; label: string }[]>([]);
  readonly busy = signal<string | null>(null);
  readonly dialog = signal<ClientDialogKind | null>(null);
  readonly prefill = signal<DrawerValue | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly secret = signal<OneTimeSecret | null>(null);
  /** Prices come in the platform's billing currency, which the plans and invoices carry. */
  readonly currency = computed(() => this.plans()[0]?.currency ?? this.data()?.invoices[0]?.currency ?? '');

  readonly statusText = computed(() => statusWords(this.data()?.client.status));
  readonly accessText = computed(() => ACCESS_WORDS[this.data()?.client.access ?? 'full']);
  readonly countryName = computed(() => { const code = this.data()?.client.countryCode; return this.countries().find((c) => c.code === code)?.name ?? code ?? ''; });
  readonly industryText = computed(() => { const key = this.data()?.client.industryCode; return this.industries().find((i) => i.key === key)?.label ?? displayLabel(key); });
  readonly canExtend = computed(() => canExtendTrial(this.data()));
  readonly canActivate = computed(() => canActivateClient(this.data()));
  readonly dialogSpec = computed((): ClientDialogSpec => clientDialogSpec(this.dialog(), this.data(), { plans: this.plans, prefill: this.prefill, currency: this.currency }));

  constructor() {
    this.load();
    loadInto(this.plans, this.api.plans());
    this.api.modules().subscribe({ next: (m) => this.modules.set(m.filter((x) => !x.isCore)), error: () => this.modulesFailed.set(true) });
    loadInto(this.countries, this.api.countries());
    loadInto(this.industries, this.api.industries());
  }

  private show(d: ClientDetail): void {
    this.data.set(d);
    this.failed.set(null);
    this.pageTitle.setTitle(d.client.name, null, null, 'Clients');
  }

  load(): void {
    this.api.client(this.orgId).subscribe({ next: (d) => this.show(d), error: (e) => this.failed.set(problemMessage(e, 'The server did not answer.')) });
  }

  cycleText(cycle: BillingCycle | null): string { return cycleWords(cycle) || '—'; }
  tone(i: { status: InvoiceStatus; isOverdue: boolean }): string { return invoiceTone(i); }
  isEnabled(key: string): boolean { return this.data()?.enabledModules.includes(key) ?? false; }
  notInPlan(key: string): boolean { const allowed = this.data()?.planModules; return !!allowed?.length && !allowed.includes(key); }

  openDialog(kind: ClientDialogKind): void {
    this.errors.set(null);
    this.prefill.set(null);
    this.dialog.set(kind);
  }

  /** Opens the plan dialog filled in with what the client asked for. */
  applyRequest(): void {
    const d = this.data();
    if (!d?.requested) return;
    this.errors.set(null);
    this.prefill.set({ planId: d.requested.planId, cycle: d.requested.cycle, customPrice: null, isComplimentary: false });
    this.dialog.set('plan');
  }

  submitDialog(v: DrawerValue): void {
    const kind = this.dialog();
    this.saving.set(true);
    this.errors.set(null);
    switch (kind) {
      case 'assign': return this.saveClient(this.api.setClientPlan(this.orgId, toAssignPlanInput(v)), (n) => `Plan assigned to ${n}`);
      case 'plan': return this.saveClient(this.api.setClientPlan(this.orgId, toChangePlanInput(v)), (n) => `Plan changed for ${n}`);
      case 'trial': return this.saveClient(this.api.extendTrial(this.orgId, v['days'] as number), (n) => `Trial extended for ${n}`);
      case 'activate': return this.saveClient(this.api.activateClient(this.orgId, !!v['issueInvoice']), (n) => `Client ${n} activated`);
      case 'invoice':
        this.api.issueInvoice(this.orgId, toIssueInvoiceInput(v)).subscribe({ next: (i) => { this.finish(`Invoice ${i.number} issued`); this.load(); }, error: (e) => this.dialogFailed(e) });
        return;
      case 'details': return this.saveClient(this.api.updateClientDetails(this.orgId, toClientNotesInput(v)), (n) => `Billing details saved for ${n}`);
      default:
        this.saving.set(false);
    }
  }

  /** Runs a dialog's call that answers with the updated client, then shows it and says what was done. */
  private saveClient(call: Observable<ClientDetail>, text: (client: string) => string): void {
    call.subscribe({ next: (d) => { this.show(d); this.finish(text(d.client.name)); }, error: (e) => this.dialogFailed(e) });
  }

  private dialogFailed(e: unknown): void {
    this.saving.set(false);
    showDrawerSaveError(e, this.errors, this.message, 'That did not work');
  }

  private finish(text: string): void {
    this.saving.set(false);
    this.dialog.set(null);
    this.message.success(text);
  }

  async suspend(): Promise<void> {
    const name = this.data()?.client.name ?? '';
    const reason = await askReason(this.modal, `Suspend ${name}?`, 'Suspend', 'Everyone in this workspace is shut out until you reactivate it. Their data is kept. They see the reason you give here.', false);
    if (reason === null) return;
    this.api.suspendClient(this.orgId, reason || null).subscribe({ next: (d) => { this.show(d); this.message.success(`Client ${name} suspended`); }, error: (e) => this.message.error(problemMessage(e, 'Could not suspend the client')) });
  }

  async cancel(): Promise<void> {
    const name = this.data()?.client.name ?? '';
    const reason = await askReason(this.modal, `Cancel the subscription of ${name}?`, 'Cancel subscription', 'Everyone in this workspace is shut out and billing stops. Their data is kept, and you can reactivate later.', false);
    if (reason === null) return;
    this.api.cancelClient(this.orgId, reason || null).subscribe({ next: (d) => { this.show(d); this.message.success(`Subscription of ${name} cancelled`); }, error: (e) => this.message.error(problemMessage(e, 'Could not cancel the subscription')) });
  }

  reactivate(): void {
    const name = this.data()?.client.name ?? '';
    this.modal.confirm({
      nzTitle: `Reactivate ${name}?`,
      nzContent: 'They can sign in and work again. If an invoice is still overdue, the client goes back to overdue rather than active.',
      nzOkText: 'Reactivate',
      nzCancelText: 'Not now',
      nzOnOk: () => {
        this.busy.set('reactivate');
        this.api.reactivateClient(this.orgId).subscribe({
          next: (d) => { this.busy.set(null); this.show(d); this.message.success(`Client ${name} reactivated`); },
          error: (e) => { this.busy.set(null); this.message.error(problemMessage(e, 'Could not reactivate the client')); },
        });
      },
    });
  }

  decline(): void {
    this.modal.confirm({
      nzTitle: 'Decline this plan change?',
      nzContent: 'The request is removed and the client stays on the current plan. They are not told automatically, so let them know.',
      nzOkText: 'Decline',
      nzOkDanger: true,
      nzCancelText: 'Keep it',
      nzOnOk: () => this.api.declinePlanRequest(this.orgId).subscribe({ next: (d) => { this.show(d); this.message.success(`Plan change request from ${this.data()?.client.name ?? 'the client'} declined`); }, error: (e) => this.message.error(problemMessage(e, 'Could not decline the request')) }),
    });
  }

  issuePassword(member: WorkspaceMember): void {
    this.modal.confirm({
      nzTitle: `Issue a temporary password for ${member.fullName}?`,
      nzContent: 'Their current password stops working at once, and they must choose a new one when they sign in.',
      nzOkText: 'Issue password',
      nzOkDanger: true,
      nzCancelText: 'Not now',
      nzOnOk: () => this.api.issueTemporaryPassword(this.orgId, member.userId).subscribe({
        next: (r) => this.secret.set({ title: 'Temporary password', password: r.password, facts: [{ label: 'User', value: member.fullName }, { label: 'Email', value: member.email }] }),
        error: (e) => this.message.error(problemMessage(e, 'Could not issue a password')),
      }),
    });
  }

  toggleModule(m: PlatformModule): void {
    if (this.busy() || !this.canManage()) return;
    const enable = !this.isEnabled(m.key);
    this.busy.set(m.key);
    this.api.setClientModule(this.orgId, m.key, enable).subscribe({
      next: () => { this.busy.set(null); this.message.success(`${m.name} ${enable ? 'switched on' : 'switched off'}`); this.load(); },
      error: (e) => { this.busy.set(null); this.message.error(problemMessage(e, `Could not change ${m.name}`)); },
    });
  }
}
