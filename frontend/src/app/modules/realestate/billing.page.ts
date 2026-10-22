import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzMessageService } from 'ng-zorro-antd/message';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem } from '../../core/http/problem-details';
import { BillingApi, BillingResult } from './realestate.api';

/** Rent billing: invoices every rent instalment that has fallen due by the chosen date. Safe to run again: invoiced instalments are skipped. */
@Component({
  selector: 'erp-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzAlertModule],
  template: `
    <h1 class="erp-page-title">Rent billing</h1>
    <p>Creates and issues one invoice for each rent instalment due on or before the date, for active leases. Invoices post to the ledger.</p>
    <div style="display:flex;gap:12px;margin:16px 0">
      <nz-date-picker [(ngModel)]="asOf" data-testid="billing-date"></nz-date-picker>
      <button nz-button nzType="primary" [nzLoading]="busy()" (click)="run()" data-testid="billing-run">Run billing</button>
    </div>
    @if (result(); as r) {
      <nz-alert [nzType]="r.invoiced ? 'success' : 'info'" nzShowIcon data-testid="billing-result"
        [nzMessage]="r.invoiced ? r.invoiced + ' invoice(s) issued, total ' + r.totalBilled.toFixed(2) : 'Nothing is due for billing on that date.'">
      </nz-alert>
      @if (r.invoiced) { <p style="margin-top:12px"><a routerLink="/finance/invoices">Open invoices</a></p> }
    }`,
})
export class BillingPage {
  private readonly api = inject(BillingApi);
  private readonly message = inject(NzMessageService);
  asOf: Date | null = new Date();
  readonly busy = signal(false);
  readonly result = signal<BillingResult | null>(null);

  run(): void {
    if (!this.asOf) return;
    this.busy.set(true);
    this.api.run(toIsoDate(this.asOf)).subscribe({
      next: (r) => { this.busy.set(false); this.result.set(r); },
      error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Billing failed'); },
    });
  }
}
