import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { BillingApi, BillingResult } from '@core/api/realestate.api';
import { toIsoDate } from '@core/format/dates';
import { displayDate } from '@core/format/display';
import { asProblem } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';

/** Rent billing: invoices every rent instalment that has fallen due by the chosen date. Safe to run again: invoiced instalments are skipped. */
@Component({
  selector: 'erp-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzAlertModule, NzModalModule, StatCardComponent, StatePanelComponent],
  styleUrl: './billing.page.scss',
  templateUrl: './billing.page.html',
})
export class BillingPage {
  private readonly billingApi = inject(BillingApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  asOf: Date | null = new Date();
  readonly busy = signal(false);
  readonly result = signal<BillingResult | null>(null);

  openLeases(): void { void this.router.navigateByUrl('/real-estate/leases'); }

  /** Asks first, then bills every instalment due up to the chosen date. */
  run(): void {
    if (!this.asOf) return;
    const asOf = toIsoDate(this.asOf);
    this.modal.confirm({
      nzTitle: `Bill rent due up to ${displayDate(asOf)}?`, nzContent: 'An invoice is issued and posted to the ledger for every instalment due by then.', nzOkText: 'Run billing',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.busy.set(true);
        this.billingApi.run(asOf).subscribe({
          next: (r) => { this.busy.set(false); this.result.set(r); resolve(); },
          error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Billing failed'); resolve(); },
        });
      }),
    });
  }
}
