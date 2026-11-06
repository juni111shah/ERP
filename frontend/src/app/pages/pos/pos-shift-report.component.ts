import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { displayLabel } from '../../core/format/display';
import { PosShiftReportDto } from '../../core/api/pos.api';
import { ErpDatePipe } from '../../shared/display.pipes';
import { TENDER_LABELS } from './pos-cart';

/** A shift on paper: an X report while it is open, the Z report once closed. Used on screen and on the print page. */
@Component({
  selector: 'erp-pos-shift-report',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, ErpDatePipe],
  template: `
    @let r = report();
    @let s = r.session;
    <div class="report" data-testid="pos-shift-report">
      <header class="head">
        <strong class="kind">{{ s.status === 'closed' ? 'Z report' : 'X report' }}</strong>
        <span>{{ s.registerName }} · {{ s.cashierName }}</span>
        <span class="muted">{{ s.openedAt | erpDate }} @if (s.closedAt) { to {{ s.closedAt | erpDate }} } @else { (still open) }</span>
      </header>

      <section>
        <h4>Sales</h4>
        <div class="row"><span>Gross sales</span><span class="amt">{{ money(r.grossSales) }}</span></div>
        <div class="row"><span>Discounts</span><span class="amt">-{{ money(r.discounts) }}</span></div>
        <div class="row"><span>Net sales</span><span class="amt">{{ money(r.netSales) }}</span></div>
        <div class="row"><span>Tax</span><span class="amt">{{ money(r.tax) }}</span></div>
        <div class="row strong"><span>Sales total ({{ r.saleCount }})</span><span class="amt" data-testid="shift-sales-total">{{ money(r.salesTotal) }}</span></div>
        <div class="row"><span>Returns ({{ r.returnCount }})</span><span class="amt">-{{ money(r.returns) }}</span></div>
        <div class="row strong total"><span>Net takings</span><span class="amt" data-testid="shift-net">{{ money(r.netTakings) }}</span></div>
        <div class="row muted"><span>Voided, not counted above ({{ r.voidCount }})</span><span class="amt">{{ money(r.voids) }}</span></div>
        <div class="row muted"><span>Items sold · average ticket</span><span class="amt">{{ r.itemsSold | number:'1.0-2' }} · {{ money(r.averageTicket) }}</span></div>
      </section>

      @if (r.tenders.length) {
        <section>
          <h4>By payment</h4>
          <table>
            <thead><tr><th>Method</th><th class="amt">Taken</th><th class="amt">Refunded</th><th class="amt">Net</th></tr></thead>
            <tbody>
              @for (t of r.tenders; track t.method) {
                <tr><td>{{ tenderLabel(t.method) }}</td><td class="amt">{{ money(t.sales) }}</td><td class="amt">{{ money(t.refunds) }}</td><td class="amt strong">{{ money(t.net) }}</td></tr>
              }
            </tbody>
          </table>
        </section>
      }

      <section>
        <h4>Cash drawer</h4>
        <div class="row"><span>Opening float</span><span class="amt">{{ money(s.openingCash) }}</span></div>
        <div class="row"><span>Cash sales</span><span class="amt">{{ money(r.cashSales) }}</span></div>
        @if (r.cashRefunds) { <div class="row"><span>Cash refunds</span><span class="amt">-{{ money(r.cashRefunds) }}</span></div> }
        @if (s.totalPayIn) { <div class="row"><span>Cash added</span><span class="amt">{{ money(s.totalPayIn) }}</span></div> }
        @if (s.totalPayOut) { <div class="row"><span>Cash taken out</span><span class="amt">-{{ money(s.totalPayOut) }}</span></div> }
        <div class="row strong total"><span>Expected in drawer</span><span class="amt" data-testid="shift-expected">{{ money(s.expectedCash) }}</span></div>
        @if (s.status === 'closed') {
          <div class="row"><span>Counted</span><span class="amt" data-testid="shift-counted">{{ money(s.actualCash) }}</span></div>
          <div class="row strong" [class.short]="s.cashDifference < 0" [class.over]="s.cashDifference > 0">
            <span>{{ s.cashDifference < 0 ? 'Short' : s.cashDifference > 0 ? 'Over' : 'Difference' }}</span>
            <span class="amt" data-testid="shift-difference">{{ money(s.cashDifference) }}</span>
          </div>
        }
      </section>

      @if (moves().length) {
        <section>
          <h4>Drawer movements</h4>
          <table>
            <tbody>
              @for (m of moves(); track m.id) {
                <tr>
                  <td class="nowrap">{{ m.timestamp | erpDate }}</td>
                  <td><strong>{{ moveLabel(m.type) }}</strong> {{ m.reason }} @if (m.performedByName) { <span class="muted">({{ m.performedByName }})</span> }</td>
                  <td class="amt">{{ m.type === 'payOut' || m.type === 'dropOut' ? '-' : '' }}{{ money(m.amount) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </section>
      }

      @if (r.topItems.length) {
        <section>
          <h4>Top items</h4>
          <table>
            <tbody>
              @for (i of r.topItems; track i.itemCode + i.description) {
                <tr><td>{{ i.description }}</td><td class="amt nowrap">{{ i.quantity | number:'1.0-2' }} ×</td><td class="amt">{{ money(i.revenue) }}</td></tr>
              }
            </tbody>
          </table>
        </section>
      }

      @if (r.heldCount) { <p class="note" data-testid="shift-held">{{ r.heldCount }} held {{ r.heldCount === 1 ? 'sale stays' : 'sales stay' }} on hold on this register. Nothing has been charged for them.</p> }
      @if (s.notes) { <p class="note">{{ s.notes }}</p> }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .report { display: flex; flex-direction: column; gap: 14px; font-size: 13px; }
    .head { display: flex; flex-direction: column; gap: 2px; padding-bottom: 10px; border-bottom: 1px solid var(--erp-color-border-subtle, #ddd); }
    .kind { font-size: 16px; }
    h4 { margin: 0 0 6px; font-size: 11px; font-weight: 700; letter-spacing: .05em; text-transform: uppercase; color: var(--erp-color-text-muted, #666); }
    .row { display: flex; justify-content: space-between; gap: 16px; padding: 2px 0; }
    .row.strong, td.strong { font-weight: 700; }
    .row.total { margin-top: 3px; padding-top: 5px; font-size: 14px; border-top: 1px solid var(--erp-color-border-subtle, #ddd); }
    .muted { color: var(--erp-color-text-muted, #666); font-weight: 400; }
    .amt { font-variant-numeric: tabular-nums; text-align: end; white-space: nowrap; }
    .nowrap { white-space: nowrap; }
    .short { color: var(--erp-color-error-text, #b00); }
    .over { color: var(--erp-color-success-text, #070); }
    table { width: 100%; border-collapse: collapse; }
    th { font-size: 11px; font-weight: 600; text-align: start; color: var(--erp-color-text-muted, #666); }
    th.amt { text-align: end; }
    td, th { padding: 3px 6px 3px 0; vertical-align: top; }
    .note { margin: 0; padding: 8px 10px; border-radius: 6px; background: var(--erp-color-bg-subtle, #f5f5f5); }
  `],
})
export class PosShiftReportComponent {
  readonly report = input.required<PosShiftReportDto>();
  readonly currency = input('');

  readonly moves = computed(() => this.report().session.drawerTransactions.filter((t) => t.reason !== 'Opening cash float'));

  money(n: number): string {
    const text = (n ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return this.currency() ? `${this.currency()} ${text}` : text;
  }

  tenderLabel(method: string): string {
    return TENDER_LABELS[method as keyof typeof TENDER_LABELS] ?? displayLabel(method);
  }

  moveLabel(type: string): string {
    return { floatIn: 'Float in', payIn: 'Pay-in', payOut: 'Pay-out', dropOut: 'Safe drop' }[type] ?? displayLabel(type);
  }
}
