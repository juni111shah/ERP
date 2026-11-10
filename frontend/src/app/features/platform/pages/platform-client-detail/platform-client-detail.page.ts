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
import { ProblemDetails } from '@core/api/api.models';
import { BillingCycle, ClientDetail, ClientStartMode, PlanView, PlatformApi, PlatformModule, WorkspaceMember } from '@core/api/platform.api';
import { displayDate, displayLabel, displayMoney } from '@core/format/display';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { PageTitleService } from '@core/services/page-title.service';
import { ErpDatePipe, ErpMoneyPipe } from '@shared/pipes/display.pipes';
import { askReason } from '@shared/utils/reason-prompt';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { UsageBarComponent } from '@shared/components/usage-bar/usage-bar.component';
import { InvoiceActionsComponent } from '../../components/invoice-actions/invoice-actions.component';
import { ACCESS_WORDS, CYCLE_OPTIONS, OneTimePasswordComponent, OneTimeSecret, PLATFORM_STYLES, START_OPTIONS, cycleWords, invoiceTone, planOptions, statusWords } from '../../platform-ui';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';

type DialogKind = 'assign' | 'plan' | 'trial' | 'activate' | 'invoice' | 'details';
const todayIso = (): string => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };

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
  readonly orgId = inject(ActivatedRoute).snapshot.paramMap.get('orgId')!;

  readonly data = signal<ClientDetail | null>(null);
  readonly failed = signal<string | null>(null);
  readonly modules = signal<PlatformModule[]>([]);
  readonly modulesFailed = signal(false);
  readonly plans = signal<PlanView[]>([]);
  readonly countries = signal<{ code: string; name: string }[]>([]);
  readonly industries = signal<{ key: string; label: string }[]>([]);
  readonly busy = signal<string | null>(null);
  readonly dialog = signal<DialogKind | null>(null);
  readonly prefill = signal<Record<string, unknown> | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly secret = signal<OneTimeSecret | null>(null);
  /** Prices come in the platform's billing currency, which the plans and invoices carry. */
  readonly currency = computed(() => this.plans()[0]?.currency ?? this.data()?.invoices[0]?.currency ?? '');

  readonly statusText = computed(() => statusWords(this.data()?.client.status));
  readonly accessText = computed(() => ACCESS_WORDS[this.data()?.client.access ?? 'full']);
  readonly countryName = computed(() => { const code = this.data()?.client.countryCode; return this.countries().find((c) => c.code === code)?.name ?? code ?? ''; });
  readonly industryText = computed(() => { const key = this.data()?.client.industryCode; return this.industries().find((i) => i.key === key)?.label ?? displayLabel(key); });
  readonly canExtend = computed(() => {
    const d = this.data();
    if (!d) return false;
    const s = d.client.status;
    return s === 'trialing' || s === 'expired' || (s === 'suspended' && !d.suspendedByFounder);
  });
  readonly canActivate = computed(() => {
    const d = this.data();
    if (!d || d.client.isComplimentary) return false;
    const s = d.client.status;
    if (s === 'trialing' || s === 'expired') return true;
    if (s === 'suspended') return !d.suspendedByFounder;
    return s === 'active' && !!d.client.currentPeriodEnd && d.client.currentPeriodEnd < todayIso();
  });

  readonly dialogSpec = computed((): { title: string; submit: string; fields: FieldSpec[]; value: Record<string, unknown> | null; warning?: string; intro?: string } => {
    const kind = this.dialog();
    const d = this.data();
    const planField = (keep?: string | null): FieldSpec => ({ key: 'planId', label: 'Plan', type: 'select', required: true, options: planOptions(this.plans(), keep) });
    const cycleField: FieldSpec = { key: 'cycle', label: 'Billing cycle', type: 'select', required: true, default: 'monthly', options: CYCLE_OPTIONS };
    const priceField: FieldSpec = { key: 'customPrice', label: 'Custom price per cycle', type: 'number', min: 0, hint: 'Leave empty to use the plan\'s price.' };
    switch (kind) {
      case 'assign':
        return {
          title: `Assign a plan to ${d?.client.name ?? ''}`, submit: 'Assign plan', value: null, warning: 'Modules outside the plan are switched off. Their data is kept.',
          fields: [
            planField(), cycleField,
            { key: 'startMode', label: 'How it starts', type: 'select', required: true, default: 'trial', options: START_OPTIONS, hint: 'Starting as active does not issue an invoice. Use Issue invoice afterwards.' },
            { key: 'trialDays', label: 'Trial days', type: 'number', min: 1, max: 365, precision: 0, hint: 'Leave empty to use the plan\'s trial.', hidden: (v) => v['startMode'] !== 'trial' },
            { ...priceField, hidden: (v) => v['startMode'] === 'complimentary' },
          ],
        };
      case 'plan':
        return {
          title: `Change plan for ${d?.client.name ?? ''}`, submit: 'Change plan', warning: 'Modules outside the new plan are switched off straight away. Their data is kept.',
          value: this.prefill() ?? { planId: d?.client.planId, cycle: d?.client.cycle, customPrice: d?.customPrice, isComplimentary: d?.client.isComplimentary },
          fields: [planField(d?.client.planId), cycleField, priceField, { key: 'isComplimentary', label: 'Free of charge', type: 'switch', hint: 'No invoices are issued and the subscription never lapses.' }],
        };
      case 'trial':
        return {
          title: 'Extend trial', submit: 'Extend trial', value: null, intro: d?.client.trialEndsOn ? `The trial ends on ${displayDate(d.client.trialEndsOn)}. A lapsed trial starts running again from today.` : 'The trial starts running from today.',
          fields: [{ key: 'days', label: 'More days', type: 'number', required: true, min: 1, max: 365, precision: 0, default: 14 }],
        };
      case 'activate':
        return {
          title: `Activate ${d?.client.name ?? ''}`, submit: 'Activate', value: null, intro: `Starts a paid period from today at ${displayMoney(d?.client.cyclePrice, this.currency())} per cycle.`,
          fields: [{ key: 'issueInvoice', label: 'Issue the first invoice', type: 'switch', default: true, hint: 'Turn this off when the period has already been paid for.' }],
        };
      case 'invoice':
        return {
          title: 'Issue invoice', submit: 'Issue invoice', value: null,
          fields: [
            { key: 'kind', label: 'What to bill', type: 'select', required: true, default: 'period', options: [{ value: 'period', label: 'The next billing period' }, { value: 'oneoff', label: 'A one-off amount' }] },
            { key: 'description', label: 'Description', type: 'text', required: true, maxLength: 300, hidden: (v) => v['kind'] !== 'oneoff' },
            { key: 'amount', label: 'Amount before tax', type: 'number', required: true, min: 0.01, hidden: (v) => v['kind'] !== 'oneoff' },
            { key: 'dueOn', label: 'Due date', type: 'date', hint: 'Leave empty to use the usual payment terms.' },
          ],
        };
      case 'details':
        return {
          title: 'Contact and notes', submit: 'Save', value: { billingEmail: d?.billingEmail, contactPhone: d?.contactPhone, notes: d?.notes },
          fields: [
            { key: 'billingEmail', label: 'Billing email', type: 'text', maxLength: 256 },
            { key: 'contactPhone', label: 'Phone', type: 'text', maxLength: 40 },
            { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000, hint: 'For your team only.' },
          ],
        };
      default:
        return { title: '', submit: 'Save', fields: [], value: null };
    }
  });

  constructor() {
    this.load();
    this.api.plans().subscribe({ next: (p) => this.plans.set(p), error: () => undefined });
    this.api.modules().subscribe({ next: (m) => this.modules.set(m.filter((x) => !x.isCore)), error: () => this.modulesFailed.set(true) });
    this.api.countries().subscribe({ next: (c) => this.countries.set(c), error: () => undefined });
    this.api.industries().subscribe({ next: (i) => this.industries.set(i), error: () => undefined });
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
  tone(i: { status: 'open' | 'paid' | 'void'; isOverdue: boolean }): string { return invoiceTone(i); }
  isEnabled(key: string): boolean { return this.data()?.enabledModules.includes(key) ?? false; }
  notInPlan(key: string): boolean { const allowed = this.data()?.planModules; return !!allowed?.length && !allowed.includes(key); }

  openDialog(kind: DialogKind): void {
    this.errors.set(null);
    this.prefill.set(null);
    this.dialog.set(kind);
  }

  applyRequest(): void {
    const d = this.data();
    if (!d?.requested) return;
    this.errors.set(null);
    this.prefill.set({ planId: d.requested.planId, cycle: d.requested.cycle, customPrice: null, isComplimentary: false });
    this.dialog.set('plan');
  }

  submitDialog(v: Record<string, unknown>): void {
    const kind = this.dialog();
    const done = (text: (client: string) => string) => (d: ClientDetail) => { this.show(d); this.finish(text(d.client.name)); };
    const fail = (e: unknown) => {
      this.saving.set(false);
      const problem = asProblem(e);
      this.errors.set(problem);
      if (!problem?.errors) this.message.error(problemMessage(e, 'That did not work'));
    };
    this.saving.set(true);
    this.errors.set(null);
    switch (kind) {
      case 'assign': {
        const startMode = v['startMode'] as ClientStartMode;
        this.api.setClientPlan(this.orgId, {
          planId: v['planId'] as string, cycle: v['cycle'] as BillingCycle, customPrice: startMode === 'complimentary' ? null : (v['customPrice'] as number | null) ?? null,
          isComplimentary: startMode === 'complimentary', startMode, trialDays: startMode === 'trial' ? (v['trialDays'] as number | null) ?? null : null,
        }).subscribe({ next: done((n) => `Plan assigned to ${n}`), error: fail });
        break;
      }
      case 'plan':
        this.api.setClientPlan(this.orgId, { planId: v['planId'] as string, cycle: v['cycle'] as BillingCycle, customPrice: (v['customPrice'] as number | null) ?? null, isComplimentary: !!v['isComplimentary'] }).subscribe({ next: done((n) => `Plan changed for ${n}`), error: fail });
        break;
      case 'trial':
        this.api.extendTrial(this.orgId, v['days'] as number).subscribe({ next: done((n) => `Trial extended for ${n}`), error: fail });
        break;
      case 'activate':
        this.api.activateClient(this.orgId, !!v['issueInvoice']).subscribe({ next: done((n) => `Client ${n} activated`), error: fail });
        break;
      case 'invoice':
        this.api.issueInvoice(this.orgId, {
          description: v['kind'] === 'oneoff' ? (v['description'] as string) : null, amount: v['kind'] === 'oneoff' ? (v['amount'] as number) : null, dueOn: (v['dueOn'] as string | null) ?? null,
        }).subscribe({ next: (i) => { this.finish(`Invoice ${i.number} issued`); this.load(); }, error: fail });
        break;
      case 'details':
        this.api.updateClientDetails(this.orgId, { billingEmail: v['billingEmail'] as string | null, contactPhone: v['contactPhone'] as string | null, notes: v['notes'] as string | null }).subscribe({ next: done((n) => `Billing details saved for ${n}`), error: fail });
        break;
      default:
        this.saving.set(false);
    }
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
    if (this.busy()) return;
    const enable = !this.isEnabled(m.key);
    this.busy.set(m.key);
    this.api.setClientModule(this.orgId, m.key, enable).subscribe({
      next: () => { this.busy.set(null); this.message.success(`${m.name} ${enable ? 'switched on' : 'switched off'}`); this.load(); },
      error: (e) => { this.busy.set(null); this.message.error(problemMessage(e, `Could not change ${m.name}`)); },
    });
  }
}
