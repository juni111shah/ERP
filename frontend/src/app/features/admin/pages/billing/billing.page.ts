import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { ProblemDetails } from '@core/api/api.models';
import { BillingCycle, BillingOverview, PlatformApi, PublicPlan } from '@core/api/platform.api';
import { statusWords } from '@core/format/subscription';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe, ErpMoneyPipe } from '@shared/pipes/display.pipes';
import { PlanCardComponent } from '@shared/components/plan-card/plan-card.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { UsageBarComponent } from '@shared/components/usage-bar/usage-bar.component';

/** A workspace's own plan: where it stands, what is owed, and a way to ask for a different plan. */
@Component({
  selector: 'erp-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzAlertModule, NzButtonModule, NzModalModule, NzRadioModule, NzSpinModule, ErpDatePipe, ErpMoneyPipe, PlanCardComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent, UsageBarComponent],
  styleUrl: './billing.page.scss',
  templateUrl: './billing.page.html',
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
