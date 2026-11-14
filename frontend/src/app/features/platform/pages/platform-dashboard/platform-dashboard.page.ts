import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { DashboardChart } from '@core/api/admin.api';
import { SessionStore } from '@core/session/session.store';
import { LifecycleResult, PlatformApi, PlatformDashboard } from '@core/api/platform.api';
import { displayMoney } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { ApexChartComponent } from '@shared/components/apex-chart/apex-chart.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ErpDatePipe, ErpMoneyPipe } from '@shared/pipes/display.pipes';
import { StatusCount, collectedChart, statusCounts } from '../../platform-dashboard-view';
import { PLATFORM_STYLES } from '../../platform-ui';

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
  templateUrl: './platform-dashboard.page.html',
})
export class PlatformDashboardPage {
  private readonly api = inject(PlatformApi);
  /** Running the billing run is for managers; the API refuses it to the rest of the team. */
  readonly canManage = inject(SessionStore).platformManager;
  private readonly modal = inject(NzModalService);
  private readonly message = inject(NzMessageService);

  readonly data = signal<PlatformDashboard | null>(null);
  readonly failed = signal(false);
  readonly running = signal(false);
  readonly lastRun = signal<LifecycleResult | null>(null);

  readonly chart = computed<DashboardChart | null>(() => {
    const d = this.data();
    return d ? collectedChart(d) : null;
  });
  readonly statuses = computed((): StatusCount[] => {
    const d = this.data();
    return d ? statusCounts(d) : [];
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
          next: (r) => { this.running.set(false); this.lastRun.set(r); this.message.success(`Billing run finished: ${r.invoicesIssued} invoice(s) issued, ${r.trialsExpired} trial(s) ended, ${r.suspended} suspended`); this.load(); },
          error: (e) => { this.running.set(false); this.message.error(problemMessage(e, 'The billing run failed')); },
        });
      },
    });
  }
}
