import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { InvoiceDocument } from '@core/api/platform.api';
import { ErpDatePipe, ErpMoneyPipe } from '../../pipes/display.pipes';

/** A subscription invoice as a clean sheet: the founder's console and a client's billing page print the same document. */
@Component({
  selector: 'erp-subscription-invoice',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NzButtonModule, NzIconModule, ErpDatePipe, ErpMoneyPipe],
  styles: [`
    :host {
      --ink: #0f172a; --muted: #64748b; --rule: #e2e8f0; --accent: #10955d; --accent-subtle: #ecfdf5; --accent-ink: #064e3b;
      display: block; background: #eef0f3; min-height: 100vh; padding: 24px 12px 48px; box-sizing: border-box;
      font-family: 'Segoe UI', system-ui, -apple-system, Roboto, Arial, sans-serif; color: var(--ink);
    }
    .toolbar { display: flex; justify-content: center; flex-wrap: wrap; gap: 8px; margin-bottom: 16px; }
    .sheet { position: relative; width: 100%; max-width: 210mm; min-height: 297mm; margin: 0 auto; background: #fff; box-shadow: 0 4px 20px rgba(0,0,0,.08); padding: 14mm; box-sizing: border-box; font-size: 12px; line-height: 1.5; border-top: 5px solid var(--accent); overflow: hidden; }
    .head { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 16px 24px; padding-bottom: 14px; border-bottom: 2.5px solid var(--accent); }
    .company { font-size: 21px; font-weight: 800; color: var(--accent-ink); margin: 0 0 4px; overflow-wrap: anywhere; }
    .line { font-size: 11.5px; color: var(--muted); white-space: pre-line; }
    .id { text-align: end; }
    .title { font-size: 22px; font-weight: 800; letter-spacing: 2px; text-transform: uppercase; color: var(--accent); margin: 2px 0 6px; }
    .no { font-size: 12px; font-weight: 700; }
    .parties { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 16px 32px; margin: 20px 0 24px; }
    .label { font-size: 10px; letter-spacing: 1.2px; text-transform: uppercase; color: var(--muted); font-weight: 700; margin-bottom: 4px; }
    .name { font-weight: 700; font-size: 13.5px; }
    table.meta { border-collapse: collapse; }
    table.meta td { padding: 3px 0; vertical-align: top; }
    table.meta td:first-child { padding-inline-end: 18px; color: var(--muted); font-size: 11.5px; }
    table.lines { width: 100%; border-collapse: collapse; margin: 10px 0 16px; }
    table.lines th { font-size: 10.5px; font-weight: 700; letter-spacing: .6px; text-transform: uppercase; text-align: start; padding: 9px 8px; background: var(--accent-subtle); color: var(--accent-ink); border-bottom: 2px solid var(--accent); }
    table.lines td { padding: 9px 8px; border-bottom: 1px solid var(--rule); vertical-align: top; }
    .num { text-align: end !important; white-space: nowrap; font-variant-numeric: tabular-nums; }
    .totals { margin-inline-start: auto; width: 84mm; max-width: 100%; margin-top: 12px; break-inside: avoid; }
    .totals .row { display: flex; justify-content: space-between; gap: 12px; padding: 6px 8px; border-bottom: 1px solid var(--rule); }
    .totals .grand { background: var(--accent-subtle); border-inline-start: 4px solid var(--accent); border-top: 2px solid var(--accent); border-bottom: 2px solid var(--accent); margin-top: 6px; padding: 8px 10px; font-size: 14px; font-weight: 800; color: var(--accent-ink); }
    .box { margin-top: 20px; padding: 10px 14px; background: #f8fafc; border: 1px solid var(--rule); border-radius: 6px; white-space: pre-line; break-inside: avoid; }
    .box-title { font-size: 10.5px; font-weight: 700; text-transform: uppercase; color: var(--muted); margin-bottom: 4px; }
    .foot { border-top: 1px solid var(--rule); margin-top: 24px; padding-top: 10px; font-size: 10.5px; color: var(--muted); break-inside: avoid; }
    .stamp { position: absolute; top: 95mm; left: 50%; transform: translateX(-50%) rotate(-18deg); font-size: 60px; font-weight: 800; letter-spacing: 8px; text-transform: uppercase; pointer-events: none; padding: 4px 28px; border-radius: 8px; }
    .stamp.paid { color: rgba(16,149,93,.2); border: 5px solid rgba(16,149,93,.2); }
    .stamp.void { color: rgba(220,38,38,.2); border: 5px solid rgba(220,38,38,.2); }
    @media (max-width: 767px) {
      :host { padding: 12px 8px 32px; }
      .sheet { min-height: 0; padding: 20px 16px; }
      .stamp { font-size: 40px; top: 50%; }
      .id { text-align: start; }
    }
    @page { size: A4 portrait; margin: 12mm; }
    @media print {
      :host { background: #fff; padding: 0; min-height: 0; }
      .toolbar { display: none !important; }
      .sheet { width: auto; max-width: none; min-height: 0; margin: 0; padding: 0; box-shadow: none; }
      * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    }
  `],
  template: `
    @let d = doc();
    <div class="toolbar">
      <button nz-button nzType="primary" type="button" (click)="print()" data-testid="invoice-print"><span nz-icon nzType="printer"></span>Print</button>
      <a nz-button [routerLink]="backLink()" data-testid="invoice-back"><span nz-icon nzType="arrow-left"></span>{{ backLabel() }}</a>
    </div>
    <div class="sheet" data-testid="invoice-sheet">
      @if (d.status === 'paid') { <div class="stamp paid">Paid</div> }
      @if (d.status === 'void') { <div class="stamp void">Void</div> }
      <div class="head">
        <div>
          <div class="company">{{ d.companyName }}</div>
          @if (d.companyAddress) { <div class="line">{{ d.companyAddress }}</div> }
          @if (d.companyTaxNumber) { <div class="line">{{ d.taxLabel }} number: <strong>{{ d.companyTaxNumber }}</strong></div> }
          @if (d.supportEmail) { <div class="line">{{ d.supportEmail }}</div> }
        </div>
        <div class="id">
          <div class="title">Invoice</div>
          <div class="no" data-testid="invoice-number">{{ d.number }}</div>
        </div>
      </div>
      <div class="parties">
        <div>
          <div class="label">Bill to</div>
          <div class="name">{{ d.clientName }}</div>
          <div class="line">Workspace code: {{ d.clientCode }}</div>
          @if (d.billingEmail) { <div class="line">{{ d.billingEmail }}</div> }
        </div>
        <table class="meta">
          <tr><td>Date of issue</td><td>{{ d.issuedOn | erpDate }}</td></tr>
          <tr><td>Due date</td><td>{{ d.dueOn | erpDate }}</td></tr>
          @if (d.periodStart && d.periodEnd) { <tr><td>Period</td><td>{{ d.periodStart | erpDate }} to {{ d.periodEnd | erpDate }}</td></tr> }
          <tr><td>Currency</td><td>{{ d.currency }}</td></tr>
        </table>
      </div>
      <table class="lines">
        <thead><tr><th>Description</th><th class="num">Amount</th></tr></thead>
        <tbody><tr><td>{{ d.description }}</td><td class="num">{{ d.subtotal | erpMoney: d.currency }}</td></tr></tbody>
      </table>
      <div class="totals">
        <div class="row"><span>Subtotal</span><span>{{ d.subtotal | erpMoney: d.currency }}</span></div>
        <div class="row"><span>{{ d.taxLabel }} ({{ d.taxPercent }}%)</span><span>{{ d.taxAmount | erpMoney: d.currency }}</span></div>
        <div class="row grand"><span>Total</span><span data-testid="invoice-total">{{ d.total | erpMoney: d.currency }}</span></div>
      </div>
      @if (d.status === 'paid') {
        <div class="box" data-testid="invoice-paid">
          <div class="box-title">Payment received</div>
          {{ d.paidOn | erpDate }}{{ d.paymentMethod ? ' by ' + d.paymentMethod : '' }}{{ d.paymentReference ? ', reference ' + d.paymentReference : '' }}
        </div>
      } @else if (d.status === 'void') {
        <div class="box" data-testid="invoice-void">
          <div class="box-title">This invoice was voided</div>
          {{ d.voidReason || 'Nothing is owed on this invoice.' }}
        </div>
      } @else if (d.paymentInstructions) {
        <div class="box" data-testid="invoice-instructions">
          <div class="box-title">How to pay</div>
          {{ d.paymentInstructions }}
        </div>
      }
      <div class="foot">Please quote the invoice number with your payment. Thank you for your business.</div>
    </div>`,
})
export class SubscriptionInvoiceComponent {
  readonly doc = input.required<InvoiceDocument>();
  readonly backLink = input.required<string | unknown[]>();
  readonly backLabel = input('Back');
  print(): void { window.print(); }
}
