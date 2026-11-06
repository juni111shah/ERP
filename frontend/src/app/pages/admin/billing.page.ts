import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { ProblemDetails } from '../../core/api/api.models';
import { BillingCycle, BillingOverview, PlatformApi, PublicPlan } from '../../core/api/platform.api';
import { statusWords } from '../../core/format/subscription';
import { asProblem, problemMessage } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';
import { ErpDatePipe, ErpMoneyPipe } from '../../shared/display.pipes';
import { PlanCardComponent } from '../../shared/plan-card.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { UsageBarComponent } from '../../shared/usage-bar.component';

/** A workspace's own plan: where it stands, what is owed, and a way to ask for a different plan. */
@Component({
  selector: 'erp-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzAlertModule, NzButtonModule, NzModalModule, NzRadioModule, NzSpinModule, ErpDatePipe, ErpMoneyPipe, PlanCardComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent, UsageBarComponent],
  styles: [`
    :host { display: block; min-width: 0; }
    .grid { display: grid; gap: 16px; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); margin-bottom: 16px; align-items: start; }
    .card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius: 12px; padding: 16px 18px; min-width: 0; box-sizing: border-box; }
    .card h2 { font-size: 15px; font-weight: 600; margin: 0 0 12px; color: var(--erp-color-text-primary); }
    .head { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
    .head h2 { margin: 0; }
    .plan-name { font-size: 20px; font-weight: 700; color: var(--erp-color-text-primary); }
    dl { display: grid; grid-template-columns: minmax(110px, auto) minmax(0, 1fr); gap: 8px 16px; margin: 12px 0 0; }
    dt { color: var(--erp-color-text-muted); font-size: 13px; }
    dd { margin: 0; color: var(--erp-color-text-primary); overflow-wrap: anywhere; }
    .bars { display: flex; flex-direction: column; gap: 14px; }
    .request { background: var(--erp-color-info-bg); border-color: var(--erp-color-info-border); margin-bottom: 16px; }
    .request p { margin: 0 0 12px; color: var(--erp-color-text-primary); }
    .plans { display: grid; gap: 20px; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); align-items: stretch; padding-top: 12px; }
    .explain { margin: 0; color: var(--erp-color-text-muted); max-width: 640px; }
    .scroll { overflow-x: auto; margin: 0 -18px; padding: 0 18px; -webkit-overflow-scrolling: touch; }
    table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    th { text-align: start; font-size: 12px; font-weight: 600; color: var(--erp-color-text-muted); padding: 6px 12px 6px 0; border-bottom: 1px solid var(--erp-color-border-subtle); white-space: nowrap; }
    td { padding: 8px 12px 8px 0; border-bottom: 1px solid var(--erp-color-border-subtle); color: var(--erp-color-text-primary); vertical-align: top; }
    tr:last-child td { border-bottom: 0; }
    .num { text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums; }
    .nowrap { white-space: nowrap; }
    .instructions { white-space: pre-line; overflow-wrap: anywhere; margin: 0 0 8px; color: var(--erp-color-text-primary); }
    .empty { margin: 0; color: var(--erp-color-text-muted); }
    @media (max-width: 767px) {
      .grid { grid-template-columns: minmax(0, 1fr); }
      .card { padding: 14px; }
      .scroll { margin: 0 -14px; padding: 0 14px; }
    }
  `],
  template: `
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load your subscription" [hint]="failed()" actionLabel="Try again" (action)="load()" />
    } @else if (!data()) {
      <div style="padding:64px;text-align:center"><nz-spin nzSimple></nz-spin></div>
    } @else if (!data()!.summary.hasSubscription) {
      <erp-state-panel icon="credit-card" title="No subscription on this workspace" hint="This workspace is not billed through a plan, so there is nothing to manage here." data-testid="billing-none" />
    } @else {
      @let d = data()!;
      @let s = d.summary;
      @if (s.access === 'blocked') {
        <nz-alert nzType="error" nzShowIcon style="margin-bottom:16px" [nzMessage]="s.status === 'cancelled' ? 'This subscription is cancelled' : 'This workspace is suspended'"
                  [nzDescription]="(s.suspendedReason ? s.suspendedReason + ' ' : '') + 'Your data is kept. Please contact us to restore access.'" data-testid="billing-blocked"></nz-alert>
      }
      <div class="grid">
        <section class="card" data-testid="billing-plan">
          <div class="head">
            <span class="plan-name" data-testid="billing-plan-name">{{ s.planName }}</span>
            <erp-status [value]="s.status" [label]="statusText()" data-testid="billing-status" />
          </div>
          <dl>
            @if (s.isComplimentary) {
              <dt>Price</dt><dd>Free of charge</dd>
            } @else if (d.cyclePrice !== null) {
              <dt>Price</dt><dd>{{ d.cyclePrice | erpMoney: d.currency }} {{ d.cycle === 'annual' ? 'a year' : 'a month' }}</dd>
            }
            @if (s.status === 'trialing' && s.trialEndsOn) {
              <dt>Trial ends</dt><dd data-testid="billing-trial">{{ s.trialEndsOn | erpDate }} ({{ trialLeft() }})</dd>
            } @else if (s.currentPeriodEnd) {
              <dt>{{ s.status === 'active' ? 'Next renewal' : 'Paid until' }}</dt><dd data-testid="billing-renewal">{{ s.currentPeriodEnd | erpDate }}</dd>
            }
            @if (s.access === 'readOnly') { <dt>Access</dt><dd>View only: changes are paused</dd> }
          </dl>
        </section>
        <section class="card" data-testid="billing-usage">
          <h2>Usage</h2>
          <div class="bars">
            <erp-usage-bar label="Active users" [used]="d.activeUsers" [max]="d.maxUsers" />
            <erp-usage-bar label="Legal entities" [used]="d.legalEntities" [max]="d.maxLegalEntities" />
          </div>
        </section>
        @if (d.paymentInstructions || d.supportEmail) {
          <section class="card" data-testid="billing-howto">
            <h2>How to pay</h2>
            @if (d.paymentInstructions) { <p class="instructions" data-testid="billing-instructions">{{ d.paymentInstructions }}</p> }
            @if (d.supportEmail) { <p class="empty">Questions? Write to <a [href]="'mailto:' + d.supportEmail" data-testid="billing-support">{{ d.supportEmail }}</a>.</p> }
          </section>
        }
      </div>

      @if (d.requested; as r) {
        <section class="card request" data-testid="billing-request">
          <h2>You asked to change your plan</h2>
          <p>{{ r.planName }}, {{ r.cycle === 'annual' ? 'annual' : 'monthly' }}, on {{ r.requestedAt | erpDate }}. Our team will confirm it and let you know.@if (r.note) { <br />Your note: "{{ r.note }}" }</p>
          @if (canManage()) { <button nz-button nzSize="small" type="button" (click)="withdraw()" data-testid="billing-withdraw">Withdraw request</button> }
        </section>
      }

      <section class="card" style="margin-bottom:16px" data-testid="billing-invoices">
        <h2>Invoices</h2>
        @if (d.invoices.length) {
          <div class="scroll">
            <table>
              <thead><tr><th>Number</th><th>Description</th><th class="num">Total</th><th>Due</th><th>Status</th></tr></thead>
              <tbody>
                @for (i of d.invoices; track i.id) {
                  <tr>
                    <td class="nowrap"><a [routerLink]="['/admin/billing/invoices', i.id]" [attr.data-testid]="'billing-invoice-' + i.number">{{ i.number }}</a></td>
                    <td style="min-width:180px">{{ i.description }}</td>
                    <td class="num">{{ i.total | erpMoney: i.currency }}</td>
                    <td class="nowrap">{{ i.dueOn | erpDate }}</td>
                    <td><erp-status [value]="i.isOverdue ? 'overdue' : i.status" /></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        } @else { <p class="empty">No invoices yet.</p> }
      </section>

      @if (canManage() && d.plans.length) {
        <section class="card" data-testid="billing-plans">
          <div class="head">
            <h2>Change your plan</h2>
            <nz-radio-group [ngModel]="cycle()" (ngModelChange)="cycle.set($event)" nzButtonStyle="solid" nzSize="small" data-testid="billing-cycle">
              <label nz-radio-button nzValue="monthly">Monthly</label>
              <label nz-radio-button nzValue="annual">Annual</label>
            </nz-radio-group>
          </div>
          <p class="explain">Ask for the plan you want and our team confirms the change with you. Nothing changes until they do, and we invoice you for any difference.</p>
          <div class="plans">
            @for (p of d.plans; track p.id) {
              <erp-plan-card [plan]="p" [cycle]="cycle()" [selected]="p.id === d.planId" [disabled]="p.id === d.planId && cycle() === d.cycle" [actionLabel]="p.id === d.planId && cycle() === d.cycle ? 'Your current plan' : 'Ask for ' + p.name" (choose)="ask($event)" />
            }
          </div>
        </section>
      }
    }
    <erp-record-drawer [title]="asking() ? 'Ask for ' + asking()!.name : ''" [open]="!!asking()" [fields]="fields" [value]="askValue()" [saving]="saving()" [errors]="errors()" submitLabel="Send request" testId="billing-request-form"
                       (submitted)="send($event)" (closed)="asking.set(null)">
      <p drawerActions style="margin:0 0 16px;color:var(--erp-color-text-muted)">Our team confirms the change with you. Nothing changes until they do.</p>
    </erp-record-drawer>`,
})
export class BillingPage {
  private readonly api = inject(PlatformApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);

  readonly data = signal<BillingOverview | null>(null);
  readonly failed = signal<string | null>(null);
  readonly cycle = signal<BillingCycle>('monthly');
  readonly asking = signal<PublicPlan | null>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly canManage = computed(() => this.session.hasPermission('core.billing.manage'));
  readonly statusText = computed(() => statusWords(this.data()?.summary.status));
  readonly trialLeft = computed(() => {
    const days = this.data()?.summary.trialDaysLeft ?? 0;
    return days <= 0 ? 'today' : days === 1 ? '1 day left' : `${days} days left`;
  });
  readonly askValue = computed<Record<string, unknown>>(() => ({ cycle: this.cycle(), note: null }));
  readonly fields: FieldSpec[] = [
    { key: 'cycle', label: 'Billing cycle', type: 'select', required: true, options: [{ value: 'monthly', label: 'Monthly' }, { value: 'annual', label: 'Annual' }] },
    { key: 'note', label: 'Note for our team (optional)', type: 'textarea', maxLength: 400 },
  ];

  constructor() { this.load(); }

  private show(d: BillingOverview): void {
    this.data.set(d);
    this.failed.set(null);
    if (d.cycle) this.cycle.set(d.cycle);
  }

  load(): void {
    this.failed.set(null);
    this.api.billing().subscribe({ next: (d) => this.show(d), error: (e) => this.failed.set(problemMessage(e, 'The server did not answer.')) });
  }

  ask(plan: PublicPlan): void {
    this.errors.set(null);
    this.asking.set(plan);
  }

  send(v: Record<string, unknown>): void {
    const plan = this.asking();
    if (!plan) return;
    this.saving.set(true);
    this.errors.set(null);
    this.api.requestPlanChange({ planId: plan.id, cycle: v['cycle'] as BillingCycle, note: v['note'] as string | null }).subscribe({
      next: (d) => { this.saving.set(false); this.asking.set(null); this.show(d); this.message.success(`Plan change request for ${plan.name} sent. Our team will be in touch.`); },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        this.errors.set(problem);
        if (!problem?.errors) this.message.error(problemMessage(e, 'Could not send the request'));
      },
    });
  }

  withdraw(): void {
    this.modal.confirm({
      nzTitle: 'Withdraw this request?',
      nzContent: 'You stay on your current plan.',
      nzOkText: 'Withdraw',
      nzCancelText: 'Keep it',
      nzOnOk: () => this.api.withdrawPlanChange().subscribe({
        next: (d) => { this.show(d); this.message.success('Plan change request withdrawn'); },
        error: (e) => this.message.error(problemMessage(e, 'Could not withdraw the request')),
      }),
    });
  }
}
