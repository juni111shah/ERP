import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Title } from '@angular/platform-browser';
import { HttpClient } from '@angular/common/http';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { API_BASE_URL } from '../core/api/api-config';

interface PrintHeader {
  organizationName: string;
  legalEntityName: string;
  legalEntityAddress?: string | null;
  taxRegistrationNo?: string | null;
  registrationNo?: string | null;
  phone?: string | null;
  email?: string | null;
  baseCurrency: string;
  countryCode: string;
}

interface DocLine { seq: number; description: string; quantity: number; price: number; taxAmount: number; net: number; }

interface PrintInvoiceData {
  header: PrintHeader;
  invoice: {
    id: string; number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; partyName?: string | null;
    currencyCode: string; net: number; tax: number; total: number; paid: number; status: string; sourceType?: string | null; notes?: string | null; lines: DocLine[];
  };
  partyAddress?: string | null;
  partyTaxNumber?: string | null;
  partyPhone?: string | null;
  partyEmail?: string | null;
}

interface PrintBillData {
  header: PrintHeader;
  bill: {
    id: string; number: string; billDate: string; dueDate: string; vendorReference?: string | null; currencyCode: string;
    net: number; tax: number; total: number; paid: number; status: string; notes?: string | null; lines: DocLine[];
  };
  partyName: string;
  partyAddress?: string | null;
  partyTaxNumber?: string | null;
  partyPhone?: string | null;
  partyEmail?: string | null;
}

interface PrintReceiptData {
  header: PrintHeader;
  receipt: { id: string; number: string; date: string; method: string; currencyCode: string; amount: number; unallocated: number; reference?: string | null; status: string };
  partyName: string;
  partyAddress?: string | null;
  partyTaxNumber?: string | null;
  partyPhone?: string | null;
  partyEmail?: string | null;
  allocations: { invoiceNumber: string; amount: number }[];
}

interface PrintPaymentData {
  header: PrintHeader;
  payment: { id: string; number: string; date: string; method: string; currencyCode: string; amount: number; unallocated: number; reference?: string | null; status: string };
  partyName: string;
  partyAddress?: string | null;
  partyTaxNumber?: string | null;
  partyPhone?: string | null;
  partyEmail?: string | null;
  allocations: { billNumber: string; amount: number }[];
}

interface PrintJournalData {
  header: PrintHeader;
  journal: {
    id: string; number: string; date: string; memo?: string | null; reference?: string | null; sourceType?: string | null; currencyCode?: string | null;
    lines: { seq: number; accountCode: string; accountName: string; description?: string | null; debit: number; credit: number }[];
  };
}

type PrintData = PrintInvoiceData | PrintBillData | PrintReceiptData | PrintPaymentData | PrintJournalData;

/** Tax name per country pack; the core stays generic. */
const TAX_NAME: Record<string, string> = { AE: 'VAT', SA: 'VAT', GB: 'VAT', IN: 'GST', PK: 'Sales tax', US: 'Sales tax' };

@Component({
  selector: 'erp-print',
  standalone: true,
  imports: [CommonModule, DatePipe, DecimalPipe, NzButtonModule, NzIconModule],
  styles: [`
    :host {
      --ink: #1f2937; --muted: #6b7280; --rule: #e5e7eb; --accent: #b89b5e; --link: #3b5b7a;
      display: block; background: #eef0f3; min-height: 100vh; padding: 24px 0 48px;
      font-family: 'Segoe UI', 'Open Sans', Roboto, Arial, sans-serif; color: var(--ink);
    }
    .toolbar { display: flex; justify-content: center; gap: 8px; margin-bottom: 16px; }
    .sheet {
      position: relative; width: 210mm; min-height: 297mm; margin: 0 auto; background: #fff;
      box-shadow: 0 2px 12px rgba(0,0,0,.12); padding: 14mm 14mm 16mm; box-sizing: border-box; font-size: 12px; line-height: 1.5;
    }
    .head { display: flex; justify-content: space-between; align-items: flex-start; gap: 24px; padding-bottom: 14px; border-bottom: 3px solid var(--accent); }
    .company-name { font-size: 20px; font-weight: 700; margin: 0 0 4px; }
    .company-line { font-size: 11.5px; }
    .trn strong { font-weight: 700; }
    .doc-id { text-align: right; white-space: nowrap; }
    .doc-title { font-size: 21px; font-weight: 700; letter-spacing: 2.5px; text-transform: uppercase; margin: 2px 0 10px; }
    .doc-no { font-size: 11.5px; font-weight: 600; }
    .parties { display: flex; justify-content: space-between; gap: 32px; margin: 22px 0 26px; }
    .label { font-size: 10px; letter-spacing: 1.2px; text-transform: uppercase; color: var(--muted); margin-bottom: 4px; }
    .party-name { font-weight: 700; font-size: 12.5px; }
    .party-address { color: var(--muted); white-space: pre-line; }
    .meta { border-collapse: collapse; }
    .meta td { padding: 3px 0; vertical-align: top; }
    .meta td:first-child { padding-right: 18px; color: var(--ink); }
    .meta td.strong { font-weight: 700; }
    table.lines { width: 100%; border-collapse: collapse; margin-bottom: 16px; }
    table.lines thead { display: table-header-group; }
    table.lines th {
      font-size: 10px; font-weight: 700; letter-spacing: .6px; text-transform: uppercase; text-align: left;
      padding: 8px 6px; border-bottom: 1px solid var(--ink);
    }
    table.lines td { padding: 9px 6px; border-bottom: 1px solid var(--rule); vertical-align: top; }
    table.lines tr { break-inside: avoid; page-break-inside: avoid; }
    .num { text-align: right; white-space: nowrap; }
    .totals { margin-left: auto; width: 76mm; break-inside: avoid; page-break-inside: avoid; }
    .totals .row { display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px solid var(--rule); }
    .totals .row.grand { border-top: 2px solid var(--ink); border-bottom: none; margin-top: 4px; padding-top: 8px; font-size: 14px; font-weight: 700; }
    .totals .row.due { font-weight: 700; border-bottom: none; }
    .notes { color: var(--link); margin: 22px 0 0; white-space: pre-line; break-inside: avoid; }
    .terms { border-top: 1px solid var(--rule); margin-top: 26px; padding-top: 10px; font-size: 10.5px; break-inside: avoid; }
    .terms p { margin: 0 0 6px; }
    .stamp {
      position: absolute; top: 95mm; left: 50%; transform: translateX(-50%) rotate(-18deg);
      font-size: 64px; font-weight: 800; letter-spacing: 8px; color: rgba(220, 38, 38, .16); border: 6px solid rgba(220, 38, 38, .16);
      padding: 4px 24px; text-transform: uppercase; pointer-events: none;
    }
    .state { text-align: center; padding: 64px; color: var(--muted); }
    .state.err { color: #dc2626; }
    @page { size: A4 portrait; margin: 12mm; }
    @media print {
      :host { background: #fff; padding: 0; min-height: 0; }
      .toolbar { display: none !important; }
      .sheet { width: auto; min-height: 0; margin: 0; padding: 0; box-shadow: none; }
      * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    }
  `],
  template: `
    @if (loading()) {
      <div class="state">Loading document…</div>
    } @else if (error()) {
      <div class="state err">{{ error() }}</div>
    } @else {
      @if (data(); as d) {
      <div class="toolbar">
        <button nz-button nzType="primary" (click)="print()" data-testid="print-btn"><span nz-icon nzType="printer"></span>Print / Save as PDF</button>
        <button nz-button (click)="close()">Close</button>
      </div>

      <div class="sheet">
        @if (stamp()) { <div class="stamp">{{ stamp() }}</div> }

        <div class="head">
          <div>
            <div class="company-name">{{ d.header.legalEntityName || d.header.organizationName }}</div>
            @if (d.header.legalEntityAddress) { <div class="company-line">{{ d.header.legalEntityAddress }}</div> }
            @if (contactLine()) { <div class="company-line">{{ contactLine() }}</div> }
            @if (d.header.taxRegistrationNo) { <div class="company-line trn">{{ taxRegLabel() }}: <strong>{{ d.header.taxRegistrationNo }}</strong></div> }
            @if (d.header.registrationNo) { <div class="company-line">Reg. no.: {{ d.header.registrationNo }}</div> }
          </div>
          <div class="doc-id">
            <div class="doc-title">{{ title() }}</div>
            <div class="doc-no">{{ docNumber() }}</div>
          </div>
        </div>

        @if (invoiceData(); as inv) {
          <div class="parties">
            <div>
              <div class="label">Bill to</div>
              <div class="party-name">{{ inv.invoice.partyName }}</div>
              @if (inv.partyAddress) { <div class="party-address">{{ inv.partyAddress }}</div> }
              @if (partyContact(inv)) { <div class="party-address">{{ partyContact(inv) }}</div> }
              @if (inv.partyTaxNumber) { <div>{{ taxRegLabel() }}: <strong>{{ inv.partyTaxNumber }}</strong></div> }
            </div>
            <table class="meta">
              <tr><td>{{ inv.invoice.isCreditNote ? 'Credit note no.' : 'Invoice no.' }}</td><td class="strong">{{ inv.invoice.number }}</td></tr>
              <tr><td>Date of issue</td><td>{{ inv.invoice.invoiceDate | date:'dd MMM yyyy' }}</td></tr>
              @if (!inv.invoice.isCreditNote) { <tr><td>Due date</td><td>{{ inv.invoice.dueDate | date:'dd MMM yyyy' }}</td></tr> }
              @if (inv.invoice.sourceType && inv.invoice.sourceType.toLowerCase() !== 'manual') { <tr><td>Source</td><td>{{ humanize(inv.invoice.sourceType) }}</td></tr> }
              <tr><td>Currency</td><td>{{ inv.invoice.currencyCode }}</td></tr>
            </table>
          </div>
          <ng-container *ngTemplateOutlet="linesTpl; context: { $implicit: inv.invoice.lines }"></ng-container>
          <div class="totals">
            <div class="row"><span>Total excluding {{ taxName() }}</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.net | number:'1.2-2' }}</span></div>
            <div class="row"><span>{{ taxName() }}</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.tax | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>Total ({{ inv.invoice.currencyCode }})</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.total | number:'1.2-2' }}</span></div>
            @if (inv.invoice.paid > 0) {
              <div class="row"><span>Amount paid</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.paid | number:'1.2-2' }}</span></div>
              <div class="row due"><span>Balance due</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.total - inv.invoice.paid | number:'1.2-2' }}</span></div>
            }
          </div>
          @if (inv.invoice.notes) { <p class="notes">{{ inv.invoice.notes }}</p> }
          <div class="terms">
            <p>Please quote the {{ inv.invoice.isCreditNote ? 'credit note' : 'invoice' }} number with your payment. Thank you for your business.</p>
            <p>This document was generated by {{ d.header.organizationName }}.</p>
          </div>
        }

        @if (billData(); as bil) {
          <div class="parties">
            <div>
              <div class="label">Vendor</div>
              <div class="party-name">{{ bil.partyName }}</div>
              @if (bil.partyAddress) { <div class="party-address">{{ bil.partyAddress }}</div> }
              @if (partyContact(bil)) { <div class="party-address">{{ partyContact(bil) }}</div> }
              @if (bil.partyTaxNumber) { <div>{{ taxRegLabel() }}: <strong>{{ bil.partyTaxNumber }}</strong></div> }
            </div>
            <table class="meta">
              <tr><td>Bill no.</td><td class="strong">{{ bil.bill.number }}</td></tr>
              @if (bil.bill.vendorReference) { <tr><td>Vendor ref.</td><td>{{ bil.bill.vendorReference }}</td></tr> }
              <tr><td>Bill date</td><td>{{ bil.bill.billDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Due date</td><td>{{ bil.bill.dueDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Currency</td><td>{{ bil.bill.currencyCode }}</td></tr>
            </table>
          </div>
          <ng-container *ngTemplateOutlet="linesTpl; context: { $implicit: bil.bill.lines }"></ng-container>
          <div class="totals">
            <div class="row"><span>Total excluding {{ taxName() }}</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.net | number:'1.2-2' }}</span></div>
            <div class="row"><span>{{ taxName() }}</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.tax | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>Total ({{ bil.bill.currencyCode }})</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.total | number:'1.2-2' }}</span></div>
            @if (bil.bill.paid > 0) {
              <div class="row"><span>Amount paid</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.paid | number:'1.2-2' }}</span></div>
              <div class="row due"><span>Balance due</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.total - bil.bill.paid | number:'1.2-2' }}</span></div>
            }
          </div>
          @if (bil.bill.notes) { <p class="notes">{{ bil.bill.notes }}</p> }
          <div class="terms"><p>This document was generated by {{ d.header.organizationName }}.</p></div>
        }

        @if (receiptData(); as rec) {
          <div class="parties">
            <div>
              <div class="label">Received from</div>
              <div class="party-name">{{ rec.partyName }}</div>
              @if (rec.partyAddress) { <div class="party-address">{{ rec.partyAddress }}</div> }
              @if (partyContact(rec)) { <div class="party-address">{{ partyContact(rec) }}</div> }
              @if (rec.partyTaxNumber) { <div>{{ taxRegLabel() }}: <strong>{{ rec.partyTaxNumber }}</strong></div> }
            </div>
            <table class="meta">
              <tr><td>Receipt no.</td><td class="strong">{{ rec.receipt.number }}</td></tr>
              <tr><td>Date</td><td>{{ rec.receipt.date | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Method</td><td>{{ humanize(rec.receipt.method) }}</td></tr>
              @if (rec.receipt.reference) { <tr><td>Reference</td><td>{{ rec.receipt.reference }}</td></tr> }
              <tr><td>Currency</td><td>{{ rec.receipt.currencyCode }}</td></tr>
            </table>
          </div>
          @if (rec.allocations.length) {
            <table class="lines">
              <thead><tr><th style="width:28px">#</th><th>Invoice no.</th><th class="num">Amount applied</th></tr></thead>
              <tbody>
                @for (a of rec.allocations; track $index) {
                  <tr><td>{{ $index + 1 }}</td><td>{{ a.invoiceNumber }}</td><td class="num">{{ a.amount | number:'1.2-2' }}</td></tr>
                }
              </tbody>
            </table>
          }
          <div class="totals">
            @if (rec.receipt.unallocated > 0) {
              <div class="row"><span>Unallocated</span><span>{{ rec.receipt.currencyCode }} {{ rec.receipt.unallocated | number:'1.2-2' }}</span></div>
            }
            <div class="row grand"><span>Amount received</span><span>{{ rec.receipt.currencyCode }} {{ rec.receipt.amount | number:'1.2-2' }}</span></div>
          </div>
          <div class="terms"><p>Thank you for your payment.</p><p>This document was generated by {{ d.header.organizationName }}.</p></div>
        }

        @if (paymentData(); as pay) {
          <div class="parties">
            <div>
              <div class="label">Paid to</div>
              <div class="party-name">{{ pay.partyName }}</div>
              @if (pay.partyAddress) { <div class="party-address">{{ pay.partyAddress }}</div> }
              @if (partyContact(pay)) { <div class="party-address">{{ partyContact(pay) }}</div> }
              @if (pay.partyTaxNumber) { <div>{{ taxRegLabel() }}: <strong>{{ pay.partyTaxNumber }}</strong></div> }
            </div>
            <table class="meta">
              <tr><td>Voucher no.</td><td class="strong">{{ pay.payment.number }}</td></tr>
              <tr><td>Date</td><td>{{ pay.payment.date | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Method</td><td>{{ humanize(pay.payment.method) }}</td></tr>
              @if (pay.payment.reference) { <tr><td>Reference</td><td>{{ pay.payment.reference }}</td></tr> }
              <tr><td>Currency</td><td>{{ pay.payment.currencyCode }}</td></tr>
            </table>
          </div>
          @if (pay.allocations.length) {
            <table class="lines">
              <thead><tr><th style="width:28px">#</th><th>Bill no.</th><th class="num">Amount applied</th></tr></thead>
              <tbody>
                @for (a of pay.allocations; track $index) {
                  <tr><td>{{ $index + 1 }}</td><td>{{ a.billNumber }}</td><td class="num">{{ a.amount | number:'1.2-2' }}</td></tr>
                }
              </tbody>
            </table>
          }
          <div class="totals">
            @if (pay.payment.unallocated > 0) {
              <div class="row"><span>Unallocated</span><span>{{ pay.payment.currencyCode }} {{ pay.payment.unallocated | number:'1.2-2' }}</span></div>
            }
            <div class="row grand"><span>Amount paid</span><span>{{ pay.payment.currencyCode }} {{ pay.payment.amount | number:'1.2-2' }}</span></div>
          </div>
          <div class="terms"><p>This document was generated by {{ d.header.organizationName }}.</p></div>
        }

        @if (journalData(); as jrn) {
          <div class="parties">
            <div>
              @if (jrn.journal.memo) { <div class="label">Memo</div><div class="party-name">{{ jrn.journal.memo }}</div> }
            </div>
            <table class="meta">
              <tr><td>Journal no.</td><td class="strong">{{ jrn.journal.number }}</td></tr>
              <tr><td>Date</td><td>{{ jrn.journal.date | date:'dd MMM yyyy' }}</td></tr>
              @if (jrn.journal.reference) { <tr><td>Reference</td><td>{{ jrn.journal.reference }}</td></tr> }
              @if (jrn.journal.sourceType) { <tr><td>Source</td><td>{{ humanize(jrn.journal.sourceType) }}</td></tr> }
              <tr><td>Currency</td><td>{{ jrn.journal.currencyCode || jrn.header.baseCurrency }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead><tr><th style="width:28px">#</th><th style="width:80px">Account</th><th>Account name</th><th>Description</th><th class="num">Debit</th><th class="num">Credit</th></tr></thead>
            <tbody>
              @for (l of jrn.journal.lines; track l.seq) {
                <tr>
                  <td>{{ l.seq }}</td><td>{{ l.accountCode }}</td><td>{{ l.accountName }}</td><td>{{ l.description || '' }}</td>
                  <td class="num">{{ l.debit ? (l.debit | number:'1.2-2') : '' }}</td>
                  <td class="num">{{ l.credit ? (l.credit | number:'1.2-2') : '' }}</td>
                </tr>
              }
              <tr style="font-weight:700">
                <td colspan="4" class="num">Total</td>
                <td class="num" style="border-top:2px solid var(--ink)">{{ totalDebit() | number:'1.2-2' }}</td>
                <td class="num" style="border-top:2px solid var(--ink)">{{ totalCredit() | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
          <div class="terms"><p>This document was generated by {{ d.header.organizationName }}.</p></div>
        }
      </div>
      }
    }

    <ng-template #linesTpl let-lines>
      <table class="lines">
        <thead>
          <tr>
            <th style="width:28px">#</th>
            <th>Description</th>
            <th class="num">Qty</th>
            <th class="num">Unit price</th>
            <th class="num">Taxable amount</th>
            <th class="num">{{ taxName() }} %</th>
            <th class="num">{{ taxName() }}</th>
            <th class="num">Total</th>
          </tr>
        </thead>
        <tbody>
          @for (l of lines; track l.seq) {
            <tr>
              <td>{{ l.seq }}</td>
              <td>{{ l.description }}</td>
              <td class="num">{{ l.quantity | number:'1.0-4' }}</td>
              <td class="num">{{ l.price | number:'1.2-2' }}</td>
              <td class="num">{{ l.net | number:'1.2-2' }}</td>
              <td class="num">{{ taxRate(l) }}</td>
              <td class="num">{{ l.taxAmount | number:'1.2-2' }}</td>
              <td class="num">{{ l.net + l.taxAmount | number:'1.2-2' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </ng-template>
  `,
})
export class PrintPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly titleService = inject(Title);
  private readonly apiBase = inject(API_BASE_URL);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<PrintData | null>(null);
  readonly docType = signal('');

  readonly invoiceData = computed(() => { const d = this.data(); return d && 'invoice' in d ? d : null; });
  readonly billData = computed(() => { const d = this.data(); return d && 'bill' in d ? d : null; });
  readonly receiptData = computed(() => { const d = this.data(); return d && 'receipt' in d ? d : null; });
  readonly paymentData = computed(() => { const d = this.data(); return d && 'payment' in d ? d : null; });
  readonly journalData = computed(() => { const d = this.data(); return d && 'journal' in d ? d : null; });

  readonly taxName = computed(() => TAX_NAME[(this.data()?.header.countryCode ?? '').toUpperCase()] ?? 'Tax');
  readonly taxRegLabel = computed(() => {
    const c = (this.data()?.header.countryCode ?? '').toUpperCase();
    return c === 'AE' || c === 'SA' ? 'TRN' : c === 'IN' ? 'GSTIN' : c === 'PK' ? 'NTN' : c === 'GB' ? 'VAT no.' : 'Tax reg. no.';
  });
  readonly contactLine = computed(() => {
    const h = this.data()?.header;
    return h ? [h.phone, h.email].filter((x) => !!x).join(' · ') : '';
  });

  readonly title = computed(() => {
    const d = this.data();
    if (!d) return '';
    if ('invoice' in d) return d.invoice.isCreditNote ? 'Credit note' : d.header.taxRegistrationNo ? 'Tax invoice' : 'Invoice';
    if ('bill' in d) return 'Purchase bill';
    if ('receipt' in d) return 'Receipt';
    if ('payment' in d) return 'Payment voucher';
    return 'Journal entry';
  });
  readonly docNumber = computed(() => {
    const d = this.data();
    if (!d) return '';
    if ('invoice' in d) return d.invoice.number;
    if ('bill' in d) return d.bill.number;
    if ('receipt' in d) return d.receipt.number;
    if ('payment' in d) return d.payment.number;
    return d.journal.number;
  });
  readonly stamp = computed(() => {
    const d = this.data();
    if (!d || 'journal' in d) return '';
    const s = ('invoice' in d ? d.invoice.status : 'bill' in d ? d.bill.status : 'receipt' in d ? d.receipt.status : d.payment.status) ?? '';
    const k = String(s).toLowerCase();
    return k === 'draft' ? 'Draft' : k === 'voided' ? 'Void' : k === 'pendingapproval' ? 'Pending' : '';
  });

  ngOnInit(): void {
    const type = this.route.snapshot.paramMap.get('type');
    const id = this.route.snapshot.paramMap.get('id');
    if (!type || !id) {
      this.error.set('Invalid document type or ID');
      this.loading.set(false);
      return;
    }
    this.docType.set(type);
    this.http.get<PrintData>(`${this.apiBase}/print/${type}/${id}`).subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
        // The browser uses the page title as the default PDF file name and print header.
        this.titleService.setTitle(`${this.title()} ${this.docNumber()}`.trim());
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.error?.detail || 'Failed to load document');
        this.loading.set(false);
      },
    });
  }

  partyContact(p: { partyPhone?: string | null; partyEmail?: string | null }): string {
    return [p.partyPhone, p.partyEmail].filter((x) => !!x).join(' · ');
  }

  print(): void { window.print(); }
  close(): void { window.close(); }

  taxRate(l: DocLine): string {
    if (!l.net) return '';
    const r = (l.taxAmount / l.net) * 100;
    return `${Math.round(r * 100) / 100}%`;
  }

  humanize(v: string | null | undefined): string {
    if (!v) return '';
    const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2');
    return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
  }

  totalDebit(): number { return this.journalData()?.journal.lines.reduce((a, l) => a + l.debit, 0) ?? 0; }
  totalCredit(): number { return this.journalData()?.journal.lines.reduce((a, l) => a + l.credit, 0) ?? 0; }
}
