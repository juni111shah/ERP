import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { DashboardChart } from '../../core/api/admin.api';
import { LifecycleResult, PlatformApi, PlatformDashboard } from '../../core/api/platform.api';
import { displayMoney } from '../../core/format/display';
import { problemMessage } from '../../core/http/problem-details';
import { ApexChartComponent } from '../../shared/apex-chart.component';
import { ErpDatePipe, ErpMoneyPipe } from '../../shared/display.pipes';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { PLATFORM_STYLES, statusWords } from './platform-ui';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** How the business stands today, and what needs doing first. */
@Component({
  selector: 'erp-platform-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzAlertModule, NzButtonModule, NzIconModule, NzModalModule, ApexChartComponent, ErpDatePipe, ErpMoneyPipe, StatCardComponent, StatePanelComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .todo { display: flex; align-items: center; justify-content: space-between; gap: 8px 12px; flex-wrap: wrap; padding: 12px 16px; margin-bottom: 12px; border-radius: 10px; background: var(--erp-color-warning-bg); border: 1px solid var(--erp-color-warning-border); color: var(--erp-color-text-primary); }
    .todo strong { font-weight: 600; }
    .kpis { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    .done { margin-bottom: 16px; }
    .done dl { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 4px 16px; margin: 8px 0 0; }
    .done dl div { display: flex; justify-content: space-between; gap: 8px; }
    .done dd { margin: 0; font-weight: 600; }
    .log time { color: var(--erp-color-text-muted); font-size: 12px; white-space: nowrap; }
    .log li { align-items: flex-start; flex-direction: column; gap: 2px; }
    .log .what { overflow-wrap: anywhere; }
    .act { margin-inline-end: 4px; }
  `],
  template: `
    @if (failed()) {
      <erp-state-panel kind="error" title="Could not load the dashboard" hint="The server did not answer." actionLabel="Try again" (action)="load()" />
    } @else {
      @let d = data();
      <div class="pf-head">
        <p>{{ d ? d.totalClients + (d.totalClients === 1 ? ' client' : ' clients') + ', ' + d.newClients30Days + ' new in the last 30 days.' : ' ' }}</p>
        <div class="pf-actions">
          <button nz-button type="button" [nzLoading]="running()" (click)="runBilling()" data-testid="platform-run-billing"><span nz-icon nzType="thunderbolt"></span>Run billing now</button>
        </div>
      </div>

      @if (d && d.pendingSignups > 0) {
        <div class="todo" data-testid="platform-todo-signups">
          <span><strong>{{ d.pendingSignups }} {{ d.pendingSignups === 1 ? 'registration is' : 'registrations are' }} waiting</strong> for your review.</span>
          <a nz-button nzType="primary" nzSize="small" routerLink="/platform/signups">Review</a>
        </div>
      }
      @if (d && d.planRequests > 0) {
        <div class="todo" data-testid="platform-todo-plans">
          <span><strong>{{ d.planRequests }} {{ d.planRequests === 1 ? 'client has' : 'clients have' }} asked</strong> for a plan change.</span>
          <a nz-button nzType="primary" nzSize="small" routerLink="/platform/clients" [queryParams]="{ status: 'planRequest' }">See them</a>
        </div>
      }
      @if (lastRun(); as r) {
        <nz-alert class="done" nzType="success" nzShowIcon nzCloseable nzMessage="Billing run finished" [nzDescription]="runResult" (nzOnClose)="lastRun.set(null)" data-testid="platform-run-result"></nz-alert>
        <ng-template #runResult>
          <dl>
            <div><dt>Trials ended</dt><dd>{{ r.trialsExpired }}</dd></div>
            <div><dt>Invoices issued</dt><dd>{{ r.invoicesIssued }}</dd></div>
            <div><dt>Marked overdue</dt><dd>{{ r.markedPastDue }}</dd></div>
            <div><dt>Suspended</dt><dd>{{ r.suspended }}</dd></div>
            <div><dt>Reminders sent</dt><dd>{{ r.remindersSent }}</dd></div>
            <div><dt>Registrations expired</dt><dd>{{ r.signupsExpired }}</dd></div>
          </dl>
        </ng-template>
      }

      <div class="erp-stat-grid kpis" data-testid="platform-kpis">
        <erp-stat-card label="Monthly recurring revenue" [value]="money(d?.monthlyRecurring)" caption="From paying clients" [tone]="0" [loading]="!d" testId="kpi-mrr" />
        <erp-stat-card label="Annual recurring revenue" [value]="money(d?.annualRecurring)" caption="Monthly times twelve" [tone]="1" [loading]="!d" testId="kpi-arr" />
        <erp-stat-card label="Active clients" [value]="d?.active ?? 0" format="number" caption="Paying or free of charge" [tone]="2" [loading]="!d" [link]="['/platform/clients']" testId="kpi-active" />
        <erp-stat-card label="On trial" [value]="d?.trialing ?? 0" format="number" caption="Not paying yet" [tone]="3" [loading]="!d" [link]="['/platform/clients']" testId="kpi-trial" />
        <erp-stat-card label="Overdue amount" [value]="money(d?.overdue)" [caption]="'All open: ' + money(d?.outstanding)" [tone]="(d?.overdue ?? 0) > 0 ? 'danger' : 'neutral'" [loading]="!d" [link]="['/platform/invoices']" testId="kpi-overdue" />
        <erp-stat-card label="Collected this month" [value]="money(d?.collectedThisMonth)" [caption]="'All time: ' + money(d?.collectedTotal)" tone="success" [loading]="!d" testId="kpi-collected" />
      </div>

      @if (d) {
        <div class="pf-grid">
          <section class="pf-card" data-testid="platform-collected-chart">
            <h2>Collected over the last 6 months</h2>
            <erp-apex-chart [chart]="chart()!" />
          </section>
          <section class="pf-card" data-testid="platform-by-status">
            <h2>Clients by status</h2>
            <ul class="pf-list">
              @for (s of statuses(); track s.key) {
                <li>
                  <a [routerLink]="['/platform/clients']" [queryParams]="{ status: s.key }"><erp-status [value]="s.tone" [label]="s.label" /></a>
                  <strong>{{ s.count }}</strong>
                </li>
              }
            </ul>
          </section>
        </div>
        <div class="pf-grid">
          <section class="pf-card" data-testid="platform-by-plan">
            <h2>Clients by plan</h2>
            @if (d.byPlan.length) {
              <div class="pf-scroll">
                <table class="pf-table">
                  <thead><tr><th>Plan</th><th class="num">Clients</th><th class="num">Monthly value</th></tr></thead>
                  <tbody>
                    @for (p of d.byPlan; track p.planId) {
                      <tr><td><a [routerLink]="['/platform/clients']" [queryParams]="{ planId: p.planId }">{{ p.planName }}</a></td><td class="num">{{ p.clients }}</td><td class="num">{{ p.monthlyValue | erpMoney: d.currency }}</td></tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else { <p class="pf-empty">No client is on a plan yet.</p> }
          </section>
          <section class="pf-card" data-testid="platform-trials">
            <h2>Trials ending soon</h2>
            @if (d.trialsEnding.length) {
              <ul class="pf-list">
                @for (t of d.trialsEnding; track t.orgId) {
                  <li>
                    <a [routerLink]="['/platform/clients', t.orgId]">{{ t.name }}</a>
                    <span class="pf-small pf-muted pf-nowrap">{{ t.trialEndsOn | erpDate }} ({{ t.daysLeft < 0 ? 'ended' : t.daysLeft === 0 ? 'today' : t.daysLeft === 1 ? '1 day' : t.daysLeft + ' days' }})</span>
                  </li>
                }
              </ul>
            } @else { <p class="pf-empty">No trial ends in the next 7 days.</p> }
          </section>
          <section class="pf-card" data-testid="platform-recent">
            <div class="pf-card-head"><h2>Recent activity</h2><a routerLink="/platform/activity" class="pf-small">See all</a></div>
            @if (d.recentActivity.length) {
              <ul class="pf-list log">
                @for (a of d.recentActivity; track a.id) {
                  <li>
                    <time>{{ a.at | erpDate }}</time>
                    <span class="what"><strong class="act">{{ a.action }}{{ a.clientName ? ':' : '' }}</strong>@if (a.clientName) { @if (a.orgId) { <a [routerLink]="['/platform/clients', a.orgId]">{{ a.clientName }}</a> } @else { {{ a.clientName }} } }</span>
                    @if (a.detail) { <span class="pf-small pf-muted what">{{ a.detail }}</span> }
                  </li>
                }
              </ul>
            } @else { <p class="pf-empty">Nothing has happened yet.</p> }
          </section>
        </div>
      }
    }`,
})
export class PlatformDashboardPage {
  private readonly api = inject(PlatformApi);
  private readonly modal = inject(NzModalService);
  private readonly message = inject(NzMessageService);

  readonly data = signal<PlatformDashboard | null>(null);
  readonly failed = signal(false);
  readonly running = signal(false);
  readonly lastRun = signal<LifecycleResult | null>(null);

  readonly chart = computed<DashboardChart | null>(() => {
    const d = this.data();
    if (!d) return null;
    return {
      key: 'platform-collected', title: 'Collected', kind: 'bar', order: 0, singleColor: true,
      categories: d.collected.map((m) => { const [y, mm] = m.month.split('-'); return `${MONTHS[+mm - 1]} ${y}`; }),
      series: [{ name: 'Collected', values: d.collected.map((m) => m.amount) }],
    };
  });
  readonly statuses = computed(() => {
    const d = this.data();
    if (!d) return [];
    const row = (key: 'trialing' | 'active' | 'pastDue' | 'expired' | 'suspended' | 'cancelled', count: number) => ({ key, tone: key, label: statusWords(key), count });
    return [row('active', d.active), row('trialing', d.trialing), row('pastDue', d.pastDue), row('expired', d.expired), row('suspended', d.suspended), row('cancelled', d.cancelled), { key: 'none', tone: 'inactive', label: 'No plan', count: d.withoutPlan }];
  });

  constructor() { this.load(); }

  money(v: number | undefined): string { return displayMoney(v ?? 0, this.data()?.currency); }

  load(): void {
    this.failed.set(false);
    this.api.dashboard().subscribe({ next: (d) => this.data.set(d), error: () => this.failed.set(true) });
  }

  runBilling(): void {
    this.modal.confirm({
      nzTitle: 'Run billing now?',
      nzContent: 'This does today\'s billing work straight away: ends lapsed trials, issues renewal invoices, marks overdue clients, suspends those long overdue and sends reminders. It normally runs by itself every day, and running it twice changes nothing.',
      nzOkText: 'Run billing',
      nzCancelText: 'Not now',
      nzOnOk: () => {
        this.running.set(true);
        this.api.runBilling().subscribe({
          next: (r) => { this.running.set(false); this.lastRun.set(r); this.message.success('Billing run finished'); this.load(); },
          error: (e) => { this.running.set(false); this.message.error(problemMessage(e, 'The billing run failed')); },
        });
      },
    });
  }
}
