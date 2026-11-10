import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { asProblem } from '@core/http/problem-details';
import { displayDate } from '@core/format/display';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { BillingApi, BillingResult } from '@core/api/realestate.api';

/** Rent billing: invoices every rent instalment that has fallen due by the chosen date. Safe to run again: invoiced instalments are skipped. */
@Component({
  selector: 'erp-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzAlertModule, NzModalModule, StatCardComponent, StatePanelComponent],
  styles: [`
    .run-card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); padding: 20px; margin-bottom: 16px; border-radius: var(--erp-radius-card, 8px); }
    .run-card p { margin: 0 0 16px; color: var(--erp-color-text-secondary); max-width: 720px; }
    .run-row { display: flex; align-items: flex-end; gap: 12px; flex-wrap: wrap; }
    .run-row label { display: block; margin-bottom: 4px; font-size: 13px; color: var(--erp-color-text-secondary); }
    .links { display: flex; gap: 8px; margin-top: 12px; }
  `],
  template: `
    <h1 class="erp-page-title">Rent billing</h1>
    <div class="run-card">
      <p>Creates and issues one invoice for each rent instalment due on or before the date, for active leases. Invoices post to the ledger. It is safe to run again: instalments already invoiced are skipped.</p>
      <div class="run-row">
        <div><label for="billing-date">Bill rent due up to</label><nz-date-picker nzId="billing-date" [nzAllowClear]="false" [(ngModel)]="asOf" data-testid="billing-date"></nz-date-picker></div>
        <button nz-button nzType="primary" [nzLoading]="busy()" [disabled]="!asOf" (click)="run()" data-testid="billing-run">Run billing</button>
      </div>
    </div>
    @if (result(); as r) {
      @if (r.invoiced) {
        <nz-alert nzType="success" nzShowIcon data-testid="billing-result" [nzMessage]="r.invoiced + (r.invoiced === 1 ? ' invoice issued.' : ' invoices issued.')"></nz-alert>
        <div class="erp-stat-grid erp-stat-grid--auto" style="margin-top:16px">
          <erp-stat-card label="Invoices issued" [value]="r.invoiced" format="number" [tone]="0" />
          <erp-stat-card label="Total billed" [value]="r.totalBilled" format="money" [shortenMillions]="false" caption="excluding what was billed before" [tone]="2" />
        </div>
        <div class="links"><a nz-button routerLink="/finance/invoices" data-testid="billing-open-invoices">Open invoices</a></div>
      } @else {
        <div data-testid="billing-result"><erp-state-panel title="Nothing is due for billing on that date" hint="Every instalment due by then is already invoiced, or no active lease has one due. Choose a later date, or check the rent schedule on the lease." icon="check-circle" actionLabel="Open leases" (action)="openLeases()" /></div>
      }
    }`,
})
export class BillingPage {
  private readonly api = inject(BillingApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  asOf: Date | null = new Date();
  readonly busy = signal(false);
  readonly result = signal<BillingResult | null>(null);

  openLeases(): void { void this.router.navigateByUrl('/real-estate/leases'); }

  run(): void {
    if (!this.asOf) return;
    const asOf = toIsoDate(this.asOf);
    this.modal.confirm({
      nzTitle: `Bill rent due up to ${displayDate(asOf)}?`, nzContent: 'An invoice is issued and posted to the ledger for every instalment due by then.', nzOkText: 'Run billing',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.busy.set(true);
        this.api.run(asOf).subscribe({
          next: (r) => { this.busy.set(false); this.result.set(r); resolve(); },
          error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Billing failed'); resolve(); },
        });
      }),
    });
  }
}
