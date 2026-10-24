import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Title } from '@angular/platform-browser';
import { HttpClient } from '@angular/common/http';
import { forkJoin, map, of, catchError } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { API_BASE_URL } from '../core/api/api-config';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { I18nService } from '../core/i18n/i18n.service';
import { displayNote } from '../core/i18n/translate-now';
import { SessionStore } from '../core/session/session.store';
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { ThermalReceiptComponent, ThermalReceiptData } from '../shared/thermal-receipt.component';

export interface PrintHeader {
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

export interface DocLine { seq: number; description: string; quantity: number; price: number; taxAmount: number; net: number; }

export interface PrintInvoiceData {
  header: PrintHeader;
  invoice: {
    id: string; number: string; isCreditNote: boolean; invoiceDate: string; dueDate: string; partyName?: string | null;
    currencyCode: string; net: number; tax: number; total: number; paid: number; status: string; sourceType?: string | null; notes?: string | null; lines: DocLine[];
  };
  partyAddress?: string | null;
  partyTaxNumber?: string | null;
  partyPhone?: string | null;
  partyEmail?: string | null;
  originalInvoiceId?: string | null;
  originalInvoiceNumber?: string | null;
}

export interface PrintBillData {
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

export interface PrintReceiptData {
  header: PrintHeader;
  receipt: { id: string; number: string; date: string; method: string; currencyCode: string; amount: number; unallocated: number; reference?: string | null; status: string };
  partyName: string;
  partyAddress?: string | null;
  partyTaxNumber?: string | null;
  partyPhone?: string | null;
  partyEmail?: string | null;
  allocations: { invoiceNumber: string; amount: number; label?: string | null }[];
  instrumentKind?: string | null;
  chequeNumber?: string | null;
  chequeDate?: string | null;
  chequeStatus?: string | null;
  chequeBankName?: string | null;
}

export interface PrintPaymentData {
  header: PrintHeader;
  payment: { id: string; number: string; date: string; method: string; currencyCode: string; amount: number; unallocated: number; reference?: string | null; status: string };
  partyName: string;
  partyAddress?: string | null;
  partyTaxNumber?: string | null;
  partyPhone?: string | null;
  partyEmail?: string | null;
  allocations: { billNumber: string; amount: number; label?: string | null }[];
  instrumentKind?: string | null;
  chequeNumber?: string | null;
  chequeDate?: string | null;
  chequeStatus?: string | null;
  chequeBankName?: string | null;
}

export interface PrintJournalData {
  header: PrintHeader;
  journal: {
    id: string; number: string; date: string; memo?: string | null; reference?: string | null; sourceType?: string | null; currencyCode?: string | null; status?: string | null;
    lines: { seq: number; accountCode: string; accountName: string; description?: string | null; debit: number; credit: number }[];
  };
}

export interface PrintQuoteData {
  header: PrintHeader;
  quote: {
    id: string; number: string; quoteDate: string; validUntil?: string | null; customerName: string; customerEmail?: string | null; customerPhone?: string | null;
    currency: string; subTotal: number; taxTotal: number; discountTotal: number; totalAmount: number; status: string; notes?: string | null; terms?: string | null;
    lines: { itemCode: string; description: string; quantity: number; unitOfMeasure: string; unitPrice: number; discountPercent: number; taxRate: number; lineTotal: number }[];
  };
}

export interface PrintOrderData {
  header: PrintHeader;
  order: {
    id: string; number: string; orderDate: string; expectedDeliveryDate?: string | null; customerName: string; customerReference?: string | null;
    shippingAddress?: string | null; currency: string; subTotal: number; taxTotal: number; totalAmount: number; status: string; notes?: string | null; paymentTerms?: string | null;
    lines: { itemCode: string; description: string; quantity: number; deliveredQuantity: number; unitOfMeasure: string; unitPrice: number; discountPercent: number; taxRate: number; lineTotal: number }[];
  };
}

export interface PrintPurchaseOrderData {
  header: PrintHeader;
  po: {
    id: string; number: string; orderDate: string; expectedDeliveryDate?: string | null; vendorName: string; vendorContact?: string | null; vendorReference?: string | null;
    deliveryAddress?: string | null; currency: string; subTotal: number; taxTotal: number; totalAmount: number; status: string; notes?: string | null; paymentTerms?: string | null;
    lines: { itemCode: string; description: string; quantity: number; unitOfMeasure: string; unitPrice: number; discountPercent: number; taxRate: number; lineTotal: number }[];
  };
}

export interface PrintLeaseData {
  header: PrintHeader;
  lease: {
    id: string; number?: string; unitNumber: string; propertyName?: string; tenantName: string; startDate: string; endDate: string;
    annualRent: number; currencyCode: string; paymentFrequency: number; securityDepositAmount: number; rentForTerm: number;
    graceDays: number; noticePeriodDays: number; status: string; notes?: string | null;
    schedule: { sequence: number; periodFrom: string; periodTo: string; dueDate: string; amount: number; status: string }[];
  };
}

export interface PrintWorkOrderData {
  header: PrintHeader;
  wo: {
    id: string; number?: string; propertyName?: string; unitNumber?: string; issue: string; category?: string; priority: string;
    status: string; reportedOn: string; reportedBy?: string; technician?: string; scheduledFor?: string; completedOn?: string;
    estimatedCost: number; actualCost?: number; details?: string; resolutionNotes?: string;
  };
}

export interface PrintPayrollData {
  header: PrintHeader;
  payroll: {
    id: string; number: string; periodYear: number; periodMonth: number; employees: number; grossEarnings: number; deductions: number; netPay: number;
    status: string; paidOn?: string | null;
    lines: { employeeId: string; employeeName: string; employeeNumber: string; basic: number; allowances: number; overtime: number; absence: number; adjustment: number; grossEarnings: number; deductions: number; netPay: number }[];
  };
}

export interface PrintPayslipData {
  header: PrintHeader;
  payrollRun: { number: string; periodYear: number; periodMonth: number; status: string; paidOn?: string | null };
  line: { employeeId: string; employeeName: string; employeeNumber: string; basic: number; allowances: number; overtime: number; absence: number; adjustment: number; grossEarnings: number; deductions: number; netPay: number };
}

export interface PrintGrnData {
  header: PrintHeader;
  grn: {
    id: string; number: string; purchaseOrderNumber: string; vendorName: string; receiptDate: string; receivedBy: string;
    deliveryNoteNumber?: string | null; notes?: string | null;
    lines: { itemCode: string; description: string; quantityReceived: number; unitOfMeasure?: string }[];
  };
}

export interface PrintJobCardData {
  header: PrintHeader;
  job: {
    id: string; workOrderNumber: string; bomCode: string; producedItemName: string; plannedQuantity: number;
    completedQuantity: number; uom: string; status: string; dueDate?: string | null; totalPlannedCost: number; notes?: string | null;
  };
}

export interface PrintManifestData {
  header: PrintHeader;
  manifest: {
    id: string; tripNumber: string; routeName: string; driverName: string; vehicleNumber: string; departureDate: string;
    status: string; totalSalesAmount: number; cashCollected?: number; notes?: string | null;
  };
}

export interface PrintInspectionData {
  header: PrintHeader;
  inspection: {
    id: string; propertyName: string; unitNumber?: string; kind: string; inspectionDate: string; inspector?: string;
    status: string; damageCost: number; notes?: string | null;
    items: { area: string; condition: string; notes?: string; repairCost: number }[];
  };
}

export interface PrintPosReceiptData {
  header: PrintHeader;
  posOrder: {
    id: string; number: string; customerName: string; cashierName: string; orderDate: string; subTotal: number;
    taxTotal: number; totalAmount: number; status: string; notes?: string | null;
    lines: { itemCode: string; description: string; quantity: number; unitPrice: number; lineTotal: number }[];
    payments?: { method: string; amount: number; changeAmount: number }[];
  };
}

export type PrintData =
  | PrintInvoiceData
  | PrintBillData
  | PrintReceiptData
  | PrintPaymentData
  | PrintJournalData
  | PrintQuoteData
  | PrintOrderData
  | PrintPurchaseOrderData
  | PrintLeaseData
  | PrintWorkOrderData
  | PrintPayrollData
  | PrintPayslipData
  | PrintGrnData
  | PrintJobCardData
  | PrintManifestData
  | PrintInspectionData
  | PrintPosReceiptData;

const TAX_NAME: Record<string, string> = { AE: 'VAT', SA: 'VAT', GB: 'VAT', IN: 'GST', PK: 'Sales tax', US: 'Sales tax' };

const MONTH_NAMES = ['', 'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

@Component({
  selector: 'erp-print',
  standalone: true,
  imports: [TranslatePipe, CommonModule, DatePipe, DecimalPipe, NzButtonModule, NzIconModule, ThermalReceiptComponent],
  styles: [`
    :host {
      --ink: #0f172a;
      --muted: #64748b;
      --rule: #e2e8f0;
      --accent: var(--erp-brand-solid-light, var(--erp-brand-600, #10955d));
      --accent-subtle: var(--erp-brand-subtle-light, var(--erp-brand-50, #ecfdf5));
      --accent-border: var(--erp-brand-border-light, var(--erp-brand-200, #a7f3d0));
      --accent-ink: var(--erp-brand-ink, #064e3b);
      --link: var(--accent);
      display: block; background: #eef0f3; min-height: 100vh; padding: 24px 0 48px;
      font-family: 'Segoe UI', system-ui, -apple-system, Roboto, Arial, sans-serif; color: var(--ink);
    }
    .toolbar { display: flex; justify-content: center; gap: 8px; margin-bottom: 16px; }
    .sheet {
      position: relative; width: 210mm; min-height: 297mm; margin: 0 auto; background: #fff;
      box-shadow: 0 4px 20px rgba(0,0,0,.08); padding: 14mm 14mm 16mm; box-sizing: border-box; font-size: 12px; line-height: 1.5;
      border-top: 5px solid var(--accent);
    }
    .head { display: flex; justify-content: space-between; align-items: flex-start; gap: 24px; padding-bottom: 14px; border-bottom: 2.5px solid var(--accent); }
    .company-name { font-size: 21px; font-weight: 800; color: var(--accent-ink); margin: 0 0 4px; }
    .company-line { font-size: 11.5px; color: var(--muted); }
    .trn strong { font-weight: 700; color: var(--ink); }
    .doc-id { text-align: right; white-space: nowrap; }
    .doc-title { font-size: 22px; font-weight: 800; letter-spacing: 2px; text-transform: uppercase; color: var(--accent); margin: 2px 0 6px; }
    .doc-no { font-size: 12px; font-weight: 700; color: var(--ink); }
    .parties { display: flex; justify-content: space-between; gap: 32px; margin: 20px 0 24px; }
    .label { font-size: 10px; letter-spacing: 1.2px; text-transform: uppercase; color: var(--muted); font-weight: 700; margin-bottom: 4px; }
    .party-name { font-weight: 700; font-size: 13.5px; color: var(--ink); }
    .party-address { color: var(--muted); white-space: pre-line; }
    .meta { border-collapse: collapse; }
    .meta td { padding: 3px 0; vertical-align: top; }
    .meta td:first-child { padding-right: 18px; color: var(--muted); font-size: 11.5px; }
    .meta td.strong { font-weight: 700; color: var(--ink); }
    .pdc { display: inline-block; margin-left: 8px; padding: 0 6px; border: 1.5px solid var(--accent); color: var(--accent); font-size: 10px; font-weight: 700; letter-spacing: 1px; line-height: 1.5; border-radius: 3px; }
    
    table.lines { width: 100%; border-collapse: collapse; margin-bottom: 16px; margin-top: 10px; }
    table.lines thead { display: table-header-group; }
    table.lines th {
      font-size: 10.5px; font-weight: 700; letter-spacing: .6px; text-transform: uppercase; text-align: left;
      padding: 9px 8px; background: var(--accent-subtle); color: var(--accent-ink); border-top: 1px solid var(--accent-border); border-bottom: 2px solid var(--accent);
    }
    table.lines td { padding: 9px 8px; border-bottom: 1px solid var(--rule); vertical-align: top; }
    table.lines tr { break-inside: avoid; page-break-inside: avoid; }
    .num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }
    
    .totals { margin-left: auto; width: 84mm; break-inside: avoid; page-break-inside: avoid; margin-top: 12px; }
    .totals .row { display: flex; justify-content: space-between; padding: 6px 8px; border-bottom: 1px solid var(--rule); }
    .totals .row.grand {
      background: var(--accent-subtle); border-left: 4px solid var(--accent); border-top: 2px solid var(--accent); border-bottom: 2px solid var(--accent);
      margin-top: 6px; padding: 8px 10px; font-size: 14px; font-weight: 800; color: var(--accent-ink);
    }
    .totals .row.due { font-weight: 700; border-bottom: none; }
    
    .notes { color: var(--ink); margin: 20px 0 0; white-space: pre-line; break-inside: avoid; padding: 10px 14px; background: #f8fafc; border: 1px solid var(--rule); border-radius: 6px; }
    .notes-title { font-size: 10.5px; font-weight: 700; text-transform: uppercase; color: var(--muted); margin-bottom: 4px; }
    .terms { border-top: 1px solid var(--rule); margin-top: 24px; padding-top: 10px; font-size: 10.5px; color: var(--muted); break-inside: avoid; }
    .terms p { margin: 0 0 5px; }

    .signatures { display: flex; justify-content: space-between; gap: 48px; margin-top: 40px; break-inside: avoid; }
    .sig-block { flex: 1; border-top: 1px solid var(--ink); padding-top: 8px; text-align: center; }
    .sig-label { font-weight: 700; font-size: 11px; text-transform: uppercase; letter-spacing: 0.8px; color: var(--muted); }

    .stamp {
      position: absolute; top: 95mm; left: 50%; transform: translateX(-50%) rotate(-18deg);
      font-size: 60px; font-weight: 800; letter-spacing: 8px; text-transform: uppercase; pointer-events: none;
      padding: 4px 28px; border-radius: 8px;
    }
    .stamp.paid, .stamp.active, .stamp.approved, .stamp.sent, .stamp.received {
      color: rgba(16, 149, 93, .22); border: 5px solid rgba(16, 149, 93, .22);
    }
    .stamp.draft, .stamp.pending {
      color: rgba(100, 116, 139, .22); border: 5px solid rgba(100, 116, 139, .22);
    }
    .stamp.void, .stamp.cancelled, .stamp.terminated {
      color: rgba(220, 38, 38, .22); border: 5px solid rgba(220, 38, 38, .22);
    }

    .state { text-align: center; padding: 64px; color: var(--muted); }
    .state.err { color: #dc2626; }
    .pos-receipt-print-wrapper {
      display: flex;
      justify-content: center;
      padding: 30px 10px;
    }

    @page { size: A4 portrait; margin: 12mm; }
    @media print {
      :host { background: #fff; padding: 0; min-height: 0; }
      .toolbar { display: none !important; }
      .sheet { width: auto; min-height: 0; margin: 0; padding: 0; box-shadow: none; border-top: 4px solid var(--accent); }
      .pos-receipt-print-wrapper { padding: 0; }
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
        @if (docType() === 'receipt') {
          <button nz-button (click)="toggleSlipMode()">
            <span nz-icon nzType="file-text"></span> {{ receiptSlipMode() ? 'Switch to Full A4' : 'Switch to Thermal Slip' }}
          </button>
        }
        <button nz-button (click)="close()">Close</button>
      </div>

      @if (docType() === 'pos-receipt') {
        @if (posReceiptPayload(); as rc) {
          <div class="pos-receipt-print-wrapper">
            <erp-thermal-receipt [receipt]="rc" />
          </div>
        }
      } @else if (docType() === 'receipt' && receiptSlipMode()) {
        @if (receiptSlipPayload(); as rc) {
          <div class="pos-receipt-print-wrapper">
            <erp-thermal-receipt [receipt]="rc" />
          </div>
        }
      } @else {
      <div class="sheet">
        @if (stamp()) { <div class="stamp" [ngClass]="stampClass()">{{ stamp() }}</div> }

        <div class="head">
          <div>
            <div class="company-name">{{ d.header.legalEntityName || d.header.organizationName }}</div>
            @if (d.header.legalEntityAddress) { <div class="company-line">{{ d.header.legalEntityAddress }}</div> }
            @if (contactLine()) { <div class="company-line">{{ contactLine() }}</div> }
            @if (d.header.taxRegistrationNo) { <div class="company-line trn">{{ taxRegLabel() }}: <strong>{{ d.header.taxRegistrationNo }}</strong></div> }
            @if (d.header.registrationNo) { <div class="company-line">{{ 'ui.print.regNo' | translate: 'Reg. no.' }}: {{ d.header.registrationNo }}</div> }
          </div>
          <div class="doc-id">
            <div class="doc-title">{{ title() }}</div>
            <div class="doc-no">{{ docNumber() }}</div>
          </div>
        </div>

        <!-- 1. INVOICE -->
        @if (invoiceData(); as inv) {
          <div class="parties">
            <div>
              <div class="label">Bill to</div>
              <div class="party-name">{{ inv.invoice.partyName || 'Unknown' }}</div>
              @if (inv.partyAddress) { <div class="party-address">{{ inv.partyAddress }}</div> }
              @if (partyContact(inv)) { <div class="party-address">{{ partyContact(inv) }}</div> }
              @if (inv.partyTaxNumber) { <div>{{ taxRegLabel() }}: <strong>{{ inv.partyTaxNumber }}</strong></div> }
            </div>
            <table class="meta">
              <tr><td>{{ inv.invoice.isCreditNote ? 'Credit note no.' : 'Invoice no.' }}</td><td class="strong">{{ inv.invoice.number }}</td></tr>
              <tr><td>Date of issue</td><td>{{ inv.invoice.invoiceDate | date:'dd MMM yyyy' }}</td></tr>
              @if (inv.invoice.isCreditNote && inv.originalInvoiceNumber) { <tr><td>Original invoice no.</td><td>{{ inv.originalInvoiceNumber }}</td></tr> }
              @if (!inv.invoice.isCreditNote) { <tr><td>Due date</td><td>{{ inv.invoice.dueDate | date:'dd MMM yyyy' }}</td></tr> }
              @if (inv.invoice.sourceType && inv.invoice.sourceType.toLowerCase() !== 'manual') { <tr><td>Source</td><td>{{ humanize(inv.invoice.sourceType) }}</td></tr> }
              <tr><td>Currency</td><td>{{ inv.invoice.currencyCode }}</td></tr>
            </table>
          </div>
          <ng-container *ngTemplateOutlet="linesTpl; context: { $implicit: inv.invoice.lines }"></ng-container>
          <div class="totals">
            <div class="row"><span>{{ 'ui.print.totalExcl' | translate: 'Total excluding' }} {{ taxName() }}</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.net | number:'1.2-2' }}</span></div>
            <div class="row"><span>{{ taxName() }}</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.tax | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>{{ 'common.total' | translate: 'Total' }} ({{ inv.invoice.currencyCode }})</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.total | number:'1.2-2' }}</span></div>
            @if (inv.invoice.paid > 0) {
              <div class="row"><span>Amount paid</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.paid | number:'1.2-2' }}</span></div>
              <div class="row due"><span>Balance due</span><span>{{ inv.invoice.currencyCode }} {{ inv.invoice.total - inv.invoice.paid | number:'1.2-2' }}</span></div>
            }
          </div>
          @if (inv.invoice.notes) { <div class="notes"><div class="notes-title">Notes</div>{{ noteText(inv.invoice.notes) }}</div> }
          <div class="terms">
            <p>{{ inv.invoice.isCreditNote ? ('ui.print.quoteCn' | translate: 'Please quote the credit note number with your payment. Thank you for your business.') : ('ui.print.quoteInv' | translate: 'Please quote the invoice number with your payment. Thank you for your business.') }}</p>
            <p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p>
          </div>
        }

        <!-- 2. BILL -->
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
            <div class="row"><span>{{ 'ui.print.totalExcl' | translate: 'Total excluding' }} {{ taxName() }}</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.net | number:'1.2-2' }}</span></div>
            <div class="row"><span>{{ taxName() }}</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.tax | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>{{ 'common.total' | translate: 'Total' }} ({{ bil.bill.currencyCode }})</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.total | number:'1.2-2' }}</span></div>
            @if (bil.bill.paid > 0) {
              <div class="row"><span>Amount paid</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.paid | number:'1.2-2' }}</span></div>
              <div class="row due"><span>Balance due</span><span>{{ bil.bill.currencyCode }} {{ bil.bill.total - bil.bill.paid | number:'1.2-2' }}</span></div>
            }
          </div>
          @if (bil.bill.notes) { <div class="notes"><div class="notes-title">Notes</div>{{ bil.bill.notes }}</div> }
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 3. RECEIPT -->
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
              @if (rec.instrumentKind || rec.chequeNumber) {
                <tr><td>Instrument</td><td>{{ humanize(rec.instrumentKind) || 'Cheque' }}@if (isPdc(rec.instrumentKind, rec.chequeDate, rec.receipt.date)) {<span class="pdc" data-testid="print-pdc">PDC</span>}</td></tr>
                @if (rec.chequeNumber) { <tr><td>Cheque no.</td><td class="strong">{{ rec.chequeNumber }}</td></tr> }
                @if (rec.chequeDate) { <tr><td>Cheque date</td><td>{{ rec.chequeDate | date:'dd MMM yyyy' }}</td></tr> }
                @if (rec.chequeBankName) { <tr><td>Bank</td><td>{{ rec.chequeBankName }}</td></tr> }
                @if (rec.chequeStatus) { <tr><td>Cheque status</td><td>{{ humanize(rec.chequeStatus) }}</td></tr> }
              }
              <tr><td>Currency</td><td>{{ rec.receipt.currencyCode }}</td></tr>
            </table>
          </div>
          @if (rec.allocations.length) {
            <table class="lines">
              <thead><tr><th style="width:28px">#</th><th>Invoice no.</th><th class="num">Amount applied</th></tr></thead>
              <tbody>
                @for (a of rec.allocations; track $index) {
                  <tr><td>{{ $index + 1 }}</td><td>{{ a.invoiceNumber || a.label }}</td><td class="num">{{ a.amount | number:'1.2-2' }}</td></tr>
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
          <div class="terms"><p>Thank you for your payment.</p><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 4. PAYMENT VOUCHER -->
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
              @if (pay.instrumentKind || pay.chequeNumber) {
                <tr><td>Instrument</td><td>{{ humanize(pay.instrumentKind) || 'Cheque' }}@if (isPdc(pay.instrumentKind, pay.chequeDate, pay.payment.date)) {<span class="pdc" data-testid="print-pdc">PDC</span>}</td></tr>
                @if (pay.chequeNumber) { <tr><td>Cheque no.</td><td class="strong">{{ pay.chequeNumber }}</td></tr> }
                @if (pay.chequeDate) { <tr><td>Cheque date</td><td>{{ pay.chequeDate | date:'dd MMM yyyy' }}</td></tr> }
                @if (pay.chequeBankName) { <tr><td>Bank</td><td>{{ pay.chequeBankName }}</td></tr> }
                @if (pay.chequeStatus) { <tr><td>Cheque status</td><td>{{ humanize(pay.chequeStatus) }}</td></tr> }
              }
              <tr><td>Currency</td><td>{{ pay.payment.currencyCode }}</td></tr>
            </table>
          </div>
          @if (pay.allocations.length) {
            <table class="lines">
              <thead><tr><th style="width:28px">#</th><th>Bill no.</th><th class="num">Amount applied</th></tr></thead>
              <tbody>
                @for (a of pay.allocations; track $index) {
                  <tr><td>{{ $index + 1 }}</td><td>{{ a.billNumber || a.label }}</td><td class="num">{{ a.amount | number:'1.2-2' }}</td></tr>
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
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 5. JOURNAL ENTRY -->
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
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 6. SALES QUOTATION -->
        @if (quoteData(); as q) {
          <div class="parties">
            <div>
              <div class="label">Quotation Prepared For</div>
              <div class="party-name">{{ q.quote.customerName }}</div>
              @if (q.quote.customerEmail) { <div class="party-address">{{ q.quote.customerEmail }}</div> }
              @if (q.quote.customerPhone) { <div class="party-address">{{ q.quote.customerPhone }}</div> }
            </div>
            <table class="meta">
              <tr><td>Quotation no.</td><td class="strong">{{ q.quote.number }}</td></tr>
              <tr><td>Date of issue</td><td>{{ q.quote.quoteDate | date:'dd MMM yyyy' }}</td></tr>
              @if (q.quote.validUntil) { <tr><td>Valid until</td><td class="strong">{{ q.quote.validUntil | date:'dd MMM yyyy' }}</td></tr> }
              <tr><td>Currency</td><td>{{ q.quote.currency }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr>
                <th style="width:28px">#</th>
                <th style="width:100px">Code</th>
                <th>Description</th>
                <th class="num">Qty</th>
                <th class="num">Unit price</th>
                @if (hasQuoteDiscounts(q)) { <th class="num">Disc %</th> }
                <th class="num">Tax %</th>
                <th class="num">Total</th>
              </tr>
            </thead>
            <tbody>
              @for (l of q.quote.lines; track $index) {
                <tr>
                  <td>{{ $index + 1 }}</td>
                  <td>{{ l.itemCode }}</td>
                  <td>{{ l.description }}</td>
                  <td class="num">{{ l.quantity | number:'1.0-2' }} {{ l.unitOfMeasure }}</td>
                  <td class="num">{{ l.unitPrice | number:'1.2-2' }}</td>
                  @if (hasQuoteDiscounts(q)) { <td class="num">{{ l.discountPercent ? (l.discountPercent + '%') : '—' }}</td> }
                  <td class="num">{{ l.taxRate ? (l.taxRate + '%') : '0%' }}</td>
                  <td class="num">{{ l.lineTotal | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div class="row"><span>Subtotal</span><span>{{ q.quote.currency }} {{ q.quote.subTotal | number:'1.2-2' }}</span></div>
            @if (q.quote.discountTotal > 0) { <div class="row"><span>Discount</span><span>-{{ q.quote.currency }} {{ q.quote.discountTotal | number:'1.2-2' }}</span></div> }
            <div class="row"><span>{{ taxName() }}</span><span>{{ q.quote.currency }} {{ q.quote.taxTotal | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>Total Amount</span><span>{{ q.quote.currency }} {{ q.quote.totalAmount | number:'1.2-2' }}</span></div>
          </div>
          @if (q.quote.notes) { <div class="notes"><div class="notes-title">Notes</div>{{ q.quote.notes }}</div> }
          @if (q.quote.terms) { <div class="notes"><div class="notes-title">Terms & Conditions</div>{{ q.quote.terms }}</div> }
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Authorized Representative</div></div>
            <div class="sig-block"><div class="sig-label">Client Acceptance & Date</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 7. SALES ORDER -->
        @if (orderData(); as o) {
          <div class="parties">
            <div>
              <div class="label">Customer</div>
              <div class="party-name">{{ o.order.customerName }}</div>
              @if (o.order.shippingAddress) { <div class="party-address"><strong>Ship To:</strong><br>{{ o.order.shippingAddress }}</div> }
            </div>
            <table class="meta">
              <tr><td>Order no.</td><td class="strong">{{ o.order.number }}</td></tr>
              <tr><td>Order date</td><td>{{ o.order.orderDate | date:'dd MMM yyyy' }}</td></tr>
              @if (o.order.expectedDeliveryDate) { <tr><td>Delivery date</td><td>{{ o.order.expectedDeliveryDate | date:'dd MMM yyyy' }}</td></tr> }
              @if (o.order.customerReference) { <tr><td>Customer ref.</td><td>{{ o.order.customerReference }}</td></tr> }
              @if (o.order.paymentTerms) { <tr><td>Payment terms</td><td>{{ o.order.paymentTerms }}</td></tr> }
              <tr><td>Currency</td><td>{{ o.order.currency }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr>
                <th style="width:28px">#</th>
                <th style="width:100px">Code</th>
                <th>Description</th>
                <th class="num">Ordered</th>
                <th class="num">Delivered</th>
                <th class="num">Unit price</th>
                <th class="num">Total</th>
              </tr>
            </thead>
            <tbody>
              @for (l of o.order.lines; track $index) {
                <tr>
                  <td>{{ $index + 1 }}</td>
                  <td>{{ l.itemCode }}</td>
                  <td>{{ l.description }}</td>
                  <td class="num">{{ l.quantity | number:'1.0-2' }} {{ l.unitOfMeasure }}</td>
                  <td class="num">{{ l.deliveredQuantity | number:'1.0-2' }}</td>
                  <td class="num">{{ l.unitPrice | number:'1.2-2' }}</td>
                  <td class="num">{{ l.lineTotal | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div class="row"><span>Subtotal</span><span>{{ o.order.currency }} {{ o.order.subTotal | number:'1.2-2' }}</span></div>
            <div class="row"><span>{{ taxName() }}</span><span>{{ o.order.currency }} {{ o.order.taxTotal | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>Total ({{ o.order.currency }})</span><span>{{ o.order.currency }} {{ o.order.totalAmount | number:'1.2-2' }}</span></div>
          </div>
          @if (o.order.notes) { <div class="notes"><div class="notes-title">Instructions</div>{{ o.order.notes }}</div> }
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 8. PURCHASE ORDER -->
        @if (purchaseOrderData(); as po) {
          <div class="parties">
            <div>
              <div class="label">Supplier / Vendor</div>
              <div class="party-name">{{ po.po.vendorName }}</div>
              @if (po.po.vendorContact) { <div class="party-address">{{ po.po.vendorContact }}</div> }
              @if (po.po.deliveryAddress) { <div class="party-address" style="margin-top:6px"><strong>Deliver To:</strong><br>{{ po.po.deliveryAddress }}</div> }
            </div>
            <table class="meta">
              <tr><td>Purchase order no.</td><td class="strong">{{ po.po.number }}</td></tr>
              <tr><td>PO date</td><td>{{ po.po.orderDate | date:'dd MMM yyyy' }}</td></tr>
              @if (po.po.expectedDeliveryDate) { <tr><td>Delivery date</td><td>{{ po.po.expectedDeliveryDate | date:'dd MMM yyyy' }}</td></tr> }
              @if (po.po.vendorReference) { <tr><td>Vendor quotation</td><td>{{ po.po.vendorReference }}</td></tr> }
              @if (po.po.paymentTerms) { <tr><td>Payment terms</td><td>{{ po.po.paymentTerms }}</td></tr> }
              <tr><td>Currency</td><td>{{ po.po.currency }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr>
                <th style="width:28px">#</th>
                <th style="width:100px">Item code</th>
                <th>Description</th>
                <th class="num">Qty</th>
                <th class="num">Unit price</th>
                <th class="num">Total</th>
              </tr>
            </thead>
            <tbody>
              @for (l of po.po.lines; track $index) {
                <tr>
                  <td>{{ $index + 1 }}</td>
                  <td>{{ l.itemCode }}</td>
                  <td>{{ l.description }}</td>
                  <td class="num">{{ l.quantity | number:'1.0-2' }} {{ l.unitOfMeasure }}</td>
                  <td class="num">{{ l.unitPrice | number:'1.2-2' }}</td>
                  <td class="num">{{ l.lineTotal | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div class="row"><span>Subtotal</span><span>{{ po.po.currency }} {{ po.po.subTotal | number:'1.2-2' }}</span></div>
            <div class="row"><span>{{ taxName() }}</span><span>{{ po.po.currency }} {{ po.po.taxTotal | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>Total PO Amount</span><span>{{ po.po.currency }} {{ po.po.totalAmount | number:'1.2-2' }}</span></div>
          </div>
          @if (po.po.notes) { <div class="notes"><div class="notes-title">Special Conditions</div>{{ po.po.notes }}</div> }
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Authorized Procurement Officer</div></div>
            <div class="sig-block"><div class="sig-label">Supplier Confirmation</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 9. REAL ESTATE TENANCY CONTRACT -->
        @if (leaseData(); as ls) {
          <div class="parties">
            <div>
              <div class="label">Tenant (Lessee)</div>
              <div class="party-name">{{ ls.lease.tenantName }}</div>
              <div class="party-address">Premises: <strong>Unit {{ ls.lease.unitNumber }}</strong>@if (ls.lease.propertyName) {, {{ ls.lease.propertyName }} }</div>
              @if (ls.lease.graceDays) { <div class="party-address">Grace period: {{ ls.lease.graceDays }} days</div> }
            </div>
            <table class="meta">
              <tr><td>Contract no.</td><td class="strong">{{ ls.lease.number || 'DRAFT-LEASE' }}</td></tr>
              <tr><td>Tenancy start</td><td class="strong">{{ ls.lease.startDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Tenancy end</td><td class="strong">{{ ls.lease.endDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Notice period</td><td>{{ ls.lease.noticePeriodDays }} days</td></tr>
              <tr><td>Payment plan</td><td>{{ ls.lease.paymentFrequency }} cheques / instalments</td></tr>
            </table>
          </div>
          <div style="margin: 16px 0; padding: 12px 14px; background: var(--accent-subtle); border-radius: 6px; border: 1px solid var(--accent-border); display: flex; justify-content: space-around;">
            <div><small style="color:var(--muted)">ANNUAL RENT</small><div style="font-size:16px; font-weight:800; color:var(--accent-ink)">{{ ls.lease.currencyCode }} {{ ls.lease.annualRent | number:'1.2-2' }}</div></div>
            <div><small style="color:var(--muted)">RENT FOR TERM</small><div style="font-size:16px; font-weight:800; color:var(--accent-ink)">{{ ls.lease.currencyCode }} {{ ls.lease.rentForTerm | number:'1.2-2' }}</div></div>
            <div><small style="color:var(--muted)">SECURITY DEPOSIT</small><div style="font-size:16px; font-weight:800; color:var(--accent-ink)">{{ ls.lease.currencyCode }} {{ ls.lease.securityDepositAmount | number:'1.2-2' }}</div></div>
          </div>
          @if (ls.lease.schedule.length) {
            <div class="notes-title" style="margin-top:16px">Rent Payment Schedule</div>
            <table class="lines">
              <thead><tr><th style="width:28px">#</th><th>Period</th><th>Due date</th><th class="num">Instalment amount</th><th>Status</th></tr></thead>
              <tbody>
                @for (s of ls.lease.schedule; track s.sequence) {
                  <tr>
                    <td>{{ s.sequence }}</td>
                    <td>{{ s.periodFrom }} → {{ s.periodTo }}</td>
                    <td>{{ s.dueDate | date:'dd MMM yyyy' }}</td>
                    <td class="num">{{ ls.lease.currencyCode }} {{ s.amount | number:'1.2-2' }}</td>
                    <td>{{ humanize(s.status) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
          @if (ls.lease.notes) { <div class="notes"><div class="notes-title">Special Clauses</div>{{ ls.lease.notes }}</div> }
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Landlord (Lessor) Signature & Stamp</div></div>
            <div class="sig-block"><div class="sig-label">Tenant (Lessee) Signature</div></div>
          </div>
          <div class="terms"><p>This Tenancy Agreement is legally binding between Lessor and Lessee.</p><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 10. REAL ESTATE WORK ORDER -->
        @if (workOrderData(); as wo) {
          <div class="parties">
            <div>
              <div class="label">Premises & Issue</div>
              <div class="party-name">{{ wo.wo.issue }}</div>
              <div class="party-address">Unit: <strong>{{ wo.wo.unitNumber || 'Common Area' }}</strong>@if (wo.wo.propertyName) { · {{ wo.wo.propertyName }} }</div>
              @if (wo.wo.reportedBy) { <div class="party-address">Reported by: {{ wo.wo.reportedBy }}</div> }
            </div>
            <table class="meta">
              <tr><td>Work order no.</td><td class="strong">{{ wo.wo.number || 'WO-PENDING' }}</td></tr>
              <tr><td>Reported date</td><td>{{ wo.wo.reportedOn | date:'dd MMM yyyy' }}</td></tr>
              @if (wo.wo.scheduledFor) { <tr><td>Scheduled for</td><td>{{ wo.wo.scheduledFor | date:'dd MMM yyyy' }}</td></tr> }
              <tr><td>Priority</td><td><strong [style.color]="wo.wo.priority === 'emergency' ? '#dc2626' : 'inherit'">{{ humanize(wo.wo.priority) }}</strong></td></tr>
              @if (wo.wo.technician) { <tr><td>Technician</td><td>{{ wo.wo.technician }}</td></tr> }
            </table>
          </div>
          <div class="notes" style="margin-bottom:16px">
            <div class="notes-title">Scope of Work & Issue Details</div>
            {{ wo.wo.details || wo.wo.issue }}
          </div>
          @if (wo.wo.resolutionNotes) {
            <div class="notes" style="margin-bottom:16px">
              <div class="notes-title">Resolution & Work Performed</div>
              {{ wo.wo.resolutionNotes }}
            </div>
          }
          <div class="totals">
            <div class="row"><span>Estimated Cost</span><span>{{ d.header.baseCurrency }} {{ wo.wo.estimatedCost | number:'1.2-2' }}</span></div>
            @if (wo.wo.actualCost != null) {
              <div class="row grand"><span>Actual Cost</span><span>{{ d.header.baseCurrency }} {{ wo.wo.actualCost | number:'1.2-2' }}</span></div>
            }
          </div>
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Technician Completion Sign-off</div></div>
            <div class="sig-block"><div class="sig-label">Tenant / Occupant Satisfaction Sign-off</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 11. EMPLOYEE PAYSLIP -->
        @if (payslipData(); as ps) {
          <div class="parties">
            <div>
              <div class="label">Employee</div>
              <div class="party-name">{{ ps.line.employeeName }}</div>
              <div class="party-address">Employee ID: <strong>{{ ps.line.employeeNumber }}</strong></div>
            </div>
            <table class="meta">
              <tr><td>Pay period</td><td class="strong">{{ monthName(ps.payrollRun.periodMonth) }} {{ ps.payrollRun.periodYear }}</td></tr>
              <tr><td>Payroll run no.</td><td>{{ ps.payrollRun.number }}</td></tr>
              @if (ps.payrollRun.paidOn) { <tr><td>Paid on</td><td>{{ ps.payrollRun.paidOn | date:'dd MMM yyyy' }}</td></tr> }
              <tr><td>Currency</td><td>{{ d.header.baseCurrency }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr><th>Earnings Component</th><th class="num">Amount ({{ d.header.baseCurrency }})</th><th>Deductions</th><th class="num">Amount ({{ d.header.baseCurrency }})</th></tr>
            </thead>
            <tbody>
              <tr>
                <td>Basic Salary</td>
                <td class="num">{{ ps.line.basic | number:'1.2-2' }}</td>
                <td>Absence Deductions</td>
                <td class="num">{{ ps.line.absence ? (ps.line.absence | number:'1.2-2') : '0.00' }}</td>
              </tr>
              <tr>
                <td>Allowances</td>
                <td class="num">{{ ps.line.allowances | number:'1.2-2' }}</td>
                <td>Other Recoveries / Loans</td>
                <td class="num">{{ ps.line.deductions ? (ps.line.deductions | number:'1.2-2') : '0.00' }}</td>
              </tr>
              <tr>
                <td>Overtime</td>
                <td class="num">{{ ps.line.overtime ? (ps.line.overtime | number:'1.2-2') : '0.00' }}</td>
                <td></td>
                <td></td>
              </tr>
              @if (ps.line.adjustment) {
                <tr>
                  <td>Adjustments</td>
                  <td class="num">{{ ps.line.adjustment | number:'1.2-2' }}</td>
                  <td></td>
                  <td></td>
                </tr>
              }
              <tr style="font-weight:700; background:#f8fafc">
                <td>Total Gross Earnings</td>
                <td class="num">{{ ps.line.grossEarnings | number:'1.2-2' }}</td>
                <td>Total Deductions</td>
                <td class="num">{{ ps.line.deductions | number:'1.2-2' }}</td>
              </tr>
            </tbody>
          </table>
          <div class="totals" style="width:100%; margin-top:20px">
            <div class="row grand" style="font-size:16px; justify-content:space-between; display:flex;">
              <span>NET SALARY PAYABLE</span>
              <span>{{ d.header.baseCurrency }} {{ ps.line.netPay | number:'1.2-2' }}</span>
            </div>
          </div>
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">HR / Payroll Manager</div></div>
            <div class="sig-block"><div class="sig-label">Employee Signature & Date</div></div>
          </div>
          <div class="terms"><p>Confidential document for the named employee only.</p><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 12. PAYROLL SUMMARY -->
        @if (payrollData(); as pr) {
          <div class="parties">
            <div>
              <div class="label">Payroll Period</div>
              <div class="party-name">{{ monthName(pr.payroll.periodMonth) }} {{ pr.payroll.periodYear }}</div>
              <div class="party-address">Employees: <strong>{{ pr.payroll.employees }}</strong></div>
            </div>
            <table class="meta">
              <tr><td>Payroll run</td><td class="strong">{{ pr.payroll.number }}</td></tr>
              <tr><td>Status</td><td>{{ humanize(pr.payroll.status) }}</td></tr>
              @if (pr.payroll.paidOn) { <tr><td>Paid on</td><td>{{ pr.payroll.paidOn | date:'dd MMM yyyy' }}</td></tr> }
              <tr><td>Currency</td><td>{{ d.header.baseCurrency }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr>
                <th style="width:28px">#</th>
                <th>Employee</th>
                <th class="num">Basic</th>
                <th class="num">Allowances</th>
                <th class="num">Overtime</th>
                <th class="num">Gross</th>
                <th class="num">Deductions</th>
                <th class="num">Net pay</th>
              </tr>
            </thead>
            <tbody>
              @for (l of pr.payroll.lines; track l.employeeId; let idx = $index) {
                <tr>
                  <td>{{ idx + 1 }}</td>
                  <td>{{ l.employeeName }}<div style="font-size:10px; color:var(--muted)">{{ l.employeeNumber }}</div></td>
                  <td class="num">{{ l.basic | number:'1.2-2' }}</td>
                  <td class="num">{{ l.allowances | number:'1.2-2' }}</td>
                  <td class="num">{{ l.overtime | number:'1.2-2' }}</td>
                  <td class="num">{{ l.grossEarnings | number:'1.2-2' }}</td>
                  <td class="num">{{ l.deductions | number:'1.2-2' }}</td>
                  <td class="num"><strong>{{ l.netPay | number:'1.2-2' }}</strong></td>
                </tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div class="row"><span>Gross Earnings</span><span>{{ d.header.baseCurrency }} {{ pr.payroll.grossEarnings | number:'1.2-2' }}</span></div>
            <div class="row"><span>Total Deductions</span><span>{{ d.header.baseCurrency }} {{ pr.payroll.deductions | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>Total Net Pay</span><span>{{ d.header.baseCurrency }} {{ pr.payroll.netPay | number:'1.2-2' }}</span></div>
          </div>
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Prepared By</div></div>
            <div class="sig-block"><div class="sig-label">Approved By Management</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 13. GOODS RECEIPT NOTE (GRN) -->
        @if (grnData(); as gn) {
          <div class="parties">
            <div>
              <div class="label">Supplier / Vendor</div>
              <div class="party-name">{{ gn.grn.vendorName }}</div>
              @if (gn.grn.deliveryNoteNumber) { <div class="party-address">Supplier Delivery Note: <strong>{{ gn.grn.deliveryNoteNumber }}</strong></div> }
            </div>
            <table class="meta">
              <tr><td>GRN no.</td><td class="strong">{{ gn.grn.number }}</td></tr>
              <tr><td>Purchase order</td><td class="strong">{{ gn.grn.purchaseOrderNumber }}</td></tr>
              <tr><td>Receipt date</td><td>{{ gn.grn.receiptDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Received by</td><td>{{ gn.grn.receivedBy }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr>
                <th style="width:28px">#</th>
                <th style="width:120px">Item code</th>
                <th>Description</th>
                <th class="num" style="width:140px">Quantity received</th>
              </tr>
            </thead>
            <tbody>
              @for (l of gn.grn.lines; track $index) {
                <tr>
                  <td>{{ $index + 1 }}</td>
                  <td><strong>{{ l.itemCode }}</strong></td>
                  <td>{{ l.description }}</td>
                  <td class="num"><strong>{{ l.quantityReceived | number:'1.0-2' }}</strong> {{ l.unitOfMeasure || 'PCS' }}</td>
                </tr>
              }
            </tbody>
          </table>
          @if (gn.grn.notes) { <div class="notes"><div class="notes-title">Receiving Remarks</div>{{ gn.grn.notes }}</div> }
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Warehouse Storekeeper Signature</div></div>
            <div class="sig-block"><div class="sig-label">Supplier / Courier Delivery Signature</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 14. PRODUCTION JOB CARD & TRAVELER -->
        @if (jobCardData(); as jc) {
          <div class="parties">
            <div>
              <div class="label">Manufacturing Product</div>
              <div class="party-name">{{ jc.job.producedItemName }}</div>
              <div class="party-address">BOM Reference: <strong>{{ jc.job.bomCode }}</strong></div>
            </div>
            <table class="meta">
              <tr><td>Work order no.</td><td class="strong">{{ jc.job.workOrderNumber }}</td></tr>
              <tr><td>Due date</td><td class="strong">{{ jc.job.dueDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Planned quantity</td><td><strong>{{ jc.job.plannedQuantity }}</strong> {{ jc.job.uom }}</td></tr>
              <tr><td>Completed quantity</td><td><strong>{{ jc.job.completedQuantity }}</strong> {{ jc.job.uom }}</td></tr>
              <tr><td>Status</td><td>{{ jc.job.status }}</td></tr>
            </table>
          </div>
          <div class="notes-title" style="margin-top:16px">Shop-Floor Operations Checklist</div>
          <table class="lines">
            <thead>
              <tr><th style="width:28px">Seq</th><th>Operation / Work Station</th><th>Required Specification</th><th style="width:100px">Operator Sign</th><th style="width:100px">QC Passed</th></tr>
            </thead>
            <tbody>
              <tr><td>010</td><td>Material Staging & Weighing</td><td>Verify raw materials per BOM {{ jc.job.bomCode }}</td><td></td><td></td></tr>
              <tr><td>020</td><td>Primary Processing / Fabrication</td><td>Follow SOP for {{ jc.job.producedItemName }}</td><td></td><td></td></tr>
              <tr><td>030</td><td>Assembly / Finishing</td><td>Inspect dimensions and tolerances</td><td></td><td></td></tr>
              <tr><td>040</td><td>Packaging & Barcoding</td><td>Box and palletize standard batch</td><td></td><td></td></tr>
            </tbody>
          </table>
          <div class="totals">
            <div class="row grand"><span>Total Planned Cost</span><span>{{ d.header.baseCurrency }} {{ jc.job.totalPlannedCost | number:'1.2-2' }}</span></div>
          </div>
          @if (jc.job.notes) { <div class="notes"><div class="notes-title">Shop Floor Notes</div>{{ jc.job.notes }}</div> }
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Production Supervisor</div></div>
            <div class="sig-block"><div class="sig-label">Quality Assurance Inspector</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 15. VAN SALES TRIP MANIFEST -->
        @if (manifestData(); as mf) {
          <div class="parties">
            <div>
              <div class="label">Trip Details</div>
              <div class="party-name">{{ mf.manifest.routeName }}</div>
              <div class="party-address">Assigned Driver: <strong>{{ mf.manifest.driverName }}</strong></div>
              <div class="party-address">Vehicle Plate: <strong>{{ mf.manifest.vehicleNumber }}</strong></div>
            </div>
            <table class="meta">
              <tr><td>Trip manifest no.</td><td class="strong">{{ mf.manifest.tripNumber }}</td></tr>
              <tr><td>Departure date</td><td>{{ mf.manifest.departureDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Status</td><td>{{ mf.manifest.status }}</td></tr>
            </table>
          </div>
          <div style="margin: 16px 0; padding: 12px 14px; background: var(--accent-subtle); border-radius: 6px; border: 1px solid var(--accent-border); display: flex; justify-content: space-around;">
            <div><small style="color:var(--muted)">SALES TARGET / REVENUE</small><div style="font-size:16px; font-weight:800; color:var(--accent-ink)">{{ d.header.baseCurrency }} {{ mf.manifest.totalSalesAmount | number:'1.2-2' }}</div></div>
            <div><small style="color:var(--muted)">CASH SETTLEMENT</small><div style="font-size:16px; font-weight:800; color:var(--accent-ink)">{{ d.header.baseCurrency }} {{ (mf.manifest.cashCollected ?? mf.manifest.totalSalesAmount) | number:'1.2-2' }}</div></div>
          </div>
          @if (mf.manifest.notes) { <div class="notes"><div class="notes-title">Route Dispatch Instructions</div>{{ mf.manifest.notes }}</div> }
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Fleet Dispatcher Signature</div></div>
            <div class="sig-block"><div class="sig-label">Van Driver Acknowledgment</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 16. PROPERTY INSPECTION REPORT -->
        @if (inspectionData(); as ip) {
          <div class="parties">
            <div>
              <div class="label">Property & Unit</div>
              <div class="party-name">{{ ip.inspection.propertyName }}</div>
              <div class="party-address">Unit: <strong>{{ ip.inspection.unitNumber || 'Whole Building' }}</strong></div>
              <div class="party-address">Type: <strong>{{ humanize(ip.inspection.kind) }} Inspection</strong></div>
            </div>
            <table class="meta">
              <tr><td>Inspection date</td><td class="strong">{{ ip.inspection.inspectionDate | date:'dd MMM yyyy' }}</td></tr>
              <tr><td>Inspector</td><td>{{ ip.inspection.inspector || 'Staff' }}</td></tr>
              <tr><td>Status</td><td>{{ humanize(ip.inspection.status) }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr>
                <th style="width:28px">#</th>
                <th style="width:160px">Area / Room</th>
                <th style="width:100px">Condition</th>
                <th>Inspector Remarks</th>
                <th class="num" style="width:120px">Repair cost</th>
              </tr>
            </thead>
            <tbody>
              @for (item of ip.inspection.items; track $index) {
                <tr>
                  <td>{{ $index + 1 }}</td>
                  <td><strong>{{ item.area }}</strong></td>
                  <td>{{ humanize(item.condition) }}</td>
                  <td>{{ item.notes || 'Normal wear & tear' }}</td>
                  <td class="num">{{ d.header.baseCurrency }} {{ item.repairCost | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div class="row grand"><span>Total Damage Cost</span><span>{{ d.header.baseCurrency }} {{ ip.inspection.damageCost | number:'1.2-2' }}</span></div>
          </div>
          @if (ip.inspection.notes) { <div class="notes"><div class="notes-title">General Notes</div>{{ ip.inspection.notes }}</div> }
          <div class="signatures">
            <div class="sig-block"><div class="sig-label">Inspector Signature</div></div>
            <div class="sig-block"><div class="sig-label">Tenant Acceptance & Date</div></div>
          </div>
          <div class="terms"><p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} {{ d.header.organizationName }}.</p></div>
        }

        <!-- 17. POS SALES RECEIPT -->
        @if (posReceiptData(); as pr) {
          <div class="parties">
            <div>
              <div class="label">POS Official Receipt</div>
              <div class="party-name">{{ pr.posOrder.customerName || 'Walk-in Customer' }}</div>
              <div class="party-address">Terminal Cashier: <strong>{{ pr.posOrder.cashierName }}</strong></div>
            </div>
            <table class="meta">
              <tr><td>Receipt no.</td><td class="strong">{{ pr.posOrder.number }}</td></tr>
              <tr><td>Date & time</td><td>{{ pr.posOrder.orderDate | date:'medium' }}</td></tr>
              <tr><td>Status</td><td>{{ pr.posOrder.status }}</td></tr>
            </table>
          </div>
          <table class="lines">
            <thead>
              <tr>
                <th style="width:28px">#</th>
                <th>Item description</th>
                <th class="num" style="width:60px">Qty</th>
                <th class="num" style="width:100px">Price</th>
                <th class="num" style="width:100px">Total</th>
              </tr>
            </thead>
            <tbody>
              @for (l of pr.posOrder.lines; track $index) {
                <tr>
                  <td>{{ $index + 1 }}</td>
                  <td><strong>{{ l.description }}</strong><div style="font-size:10px; color:var(--muted)">{{ l.itemCode }}</div></td>
                  <td class="num">{{ l.quantity }}</td>
                  <td class="num">{{ d.header.baseCurrency }} {{ l.unitPrice | number:'1.2-2' }}</td>
                  <td class="num"><strong>{{ d.header.baseCurrency }} {{ l.lineTotal | number:'1.2-2' }}</strong></td>
                </tr>
              }
            </tbody>
          </table>
          <div class="totals">
            <div class="row"><span>Subtotal</span><span>{{ d.header.baseCurrency }} {{ pr.posOrder.subTotal | number:'1.2-2' }}</span></div>
            <div class="row"><span>{{ taxName() }}</span><span>{{ d.header.baseCurrency }} {{ pr.posOrder.taxTotal | number:'1.2-2' }}</span></div>
            <div class="row grand"><span>TOTAL AMOUNT</span><span>{{ d.header.baseCurrency }} {{ pr.posOrder.totalAmount | number:'1.2-2' }}</span></div>
            @for (p of pr.posOrder.payments; track $index) {
              <div class="row" style="font-size:11px">
                <span>{{ p.method }} Paid:</span>
                <span>{{ d.header.baseCurrency }} {{ p.amount | number:'1.2-2' }}@if (p.changeAmount > 0) { (Change: {{ d.header.baseCurrency }} {{ p.changeAmount | number:'1.2-2' }}) }</span>
              </div>
            }
          </div>
          <div class="terms" style="text-align:center">
            <p style="font-weight:700">Thank you for shopping with {{ d.header.organizationName }}!</p>
            <p>{{ 'ui.print.generatedBy' | translate: 'This document was generated by' }} HigherSquare 360 ERP.</p>
          </div>
        }

      </div>
      }
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
  private readonly i18n = inject(I18nService);
  private readonly session = inject(SessionStore);
  readonly theme = inject(TenantThemeService);

  noteText(note: string | null | undefined): string { return displayNote(note, this.i18n.lang()); }

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<PrintData | null>(null);
  readonly docType = signal('');

  readonly invoiceData = computed(() => { const d = this.data(); return d && 'invoice' in d ? (d as PrintInvoiceData) : null; });
  readonly billData = computed(() => { const d = this.data(); return d && 'bill' in d ? (d as PrintBillData) : null; });
  readonly receiptData = computed(() => { const d = this.data(); return d && 'receipt' in d ? (d as PrintReceiptData) : null; });
  readonly paymentData = computed(() => { const d = this.data(); return d && 'payment' in d ? (d as PrintPaymentData) : null; });
  readonly journalData = computed(() => { const d = this.data(); return d && 'journal' in d ? (d as PrintJournalData) : null; });
  readonly quoteData = computed(() => { const d = this.data(); return d && 'quote' in d ? (d as PrintQuoteData) : null; });
  readonly orderData = computed(() => { const d = this.data(); return d && 'order' in d ? (d as PrintOrderData) : null; });
  readonly purchaseOrderData = computed(() => { const d = this.data(); return d && 'po' in d ? (d as PrintPurchaseOrderData) : null; });
  readonly leaseData = computed(() => { const d = this.data(); return d && 'lease' in d ? (d as PrintLeaseData) : null; });
  readonly workOrderData = computed(() => { const d = this.data(); return d && 'wo' in d ? (d as PrintWorkOrderData) : null; });
  readonly payrollData = computed(() => { const d = this.data(); return d && 'payroll' in d ? (d as PrintPayrollData) : null; });
  readonly payslipData = computed(() => { const d = this.data(); return d && 'payrollRun' in d ? (d as PrintPayslipData) : null; });
  readonly grnData = computed(() => { const d = this.data(); return d && 'grn' in d ? (d as PrintGrnData) : null; });
  readonly jobCardData = computed(() => { const d = this.data(); return d && 'job' in d ? (d as PrintJobCardData) : null; });
  readonly manifestData = computed(() => { const d = this.data(); return d && 'manifest' in d ? (d as PrintManifestData) : null; });
  readonly inspectionData = computed(() => { const d = this.data(); return d && 'inspection' in d ? (d as PrintInspectionData) : null; });
  readonly posReceiptData = computed(() => { const d = this.data(); return d && 'posOrder' in d ? (d as PrintPosReceiptData) : null; });

  readonly receiptSlipMode = signal(false);
  toggleSlipMode(): void { this.receiptSlipMode.update((v) => !v); }

  readonly posReceiptPayload = computed<ThermalReceiptData | null>(() => {
    const pr = this.posReceiptData();
    const d = this.data();
    if (!pr || !d) return null;
    return {
      organizationName: d.header.organizationName || 'HigherSquare 360 ERP',
      organizationNameAr: 'هاير سكوير 360',
      branchAddress: d.header.legalEntityAddress || undefined,
      title: 'Tax Invoice / Cash Receipt',
      titleAr: 'فاتورة ضريبية / إيصال مبيعات',
      receiptNumber: pr.posOrder.number,
      date: pr.posOrder.orderDate,
      status: pr.posOrder.status,
      cashierName: pr.posOrder.cashierName,
      customerName: pr.posOrder.customerName,
      currency: d.header.baseCurrency || 'AED',
      lines: (pr.posOrder.lines || []).map((l: any) => ({
        code: l.itemCode,
        description: l.description,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        lineTotal: l.lineTotal,
      })),
      subTotal: pr.posOrder.subTotal,
      taxTotal: pr.posOrder.taxTotal,
      totalAmount: pr.posOrder.totalAmount,
      payments: (pr.posOrder.payments || []).map((p: any) => ({
        method: p.method,
        amount: p.amount,
        changeAmount: p.changeAmount,
      })),
      notes: pr.posOrder.notes,
    };
  });

  readonly receiptSlipPayload = computed<ThermalReceiptData | null>(() => {
    const r = this.receiptData();
    const d = this.data();
    if (!r || !d) return null;
    return {
      organizationName: d.header.organizationName || 'HigherSquare 360 ERP',
      organizationNameAr: 'هاير سكوير 360',
      branchName: d.header.legalEntityName || 'Main Cash Counter',
      branchAddress: d.header.legalEntityAddress || undefined,
      title: 'Cash Receipt Voucher',
      titleAr: 'سند استلام نقدي',
      receiptNumber: r.receipt.number,
      date: r.receipt.date,
      status: r.receipt.status || 'Successful',
      customerName: r.partyName || 'Customer',
      referenceNo: r.receipt.reference || r.receipt.number,
      currency: r.receipt.currencyCode || d.header.baseCurrency,
      subTotal: r.receipt.amount,
      totalAmount: r.receipt.amount,
      payments: [{
        method: r.receipt.method || 'Cash',
        amount: r.receipt.amount,
      }],
      notes: r.receipt.reference,
    };
  });

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
    if ('receipt' in d) return 'Payment receipt';
    if ('payment' in d) return 'Payment voucher';
    if ('journal' in d) return 'Journal entry voucher';
    if ('quote' in d) return 'Sales quotation';
    if ('order' in d) return 'Sales order';
    if ('po' in d) return 'Purchase order';
    if ('lease' in d) return 'Tenancy agreement';
    if ('wo' in d) return 'Work order';
    if ('payroll' in d) return 'Payroll summary';
    if ('payrollRun' in d) return 'Salary payslip';
    if ('grn' in d) return 'Goods receipt note';
    if ('job' in d) return 'Production job card';
    if ('manifest' in d) return 'Trip manifest';
    if ('inspection' in d) return 'Inspection report';
    if ('posOrder' in d) return 'Sales receipt';
    return 'Document';
  });

  readonly docNumber = computed(() => {
    const d = this.data();
    if (!d) return '';
    if ('invoice' in d) return d.invoice.number;
    if ('bill' in d) return d.bill.number;
    if ('receipt' in d) return d.receipt.number;
    if ('payment' in d) return d.payment.number;
    if ('journal' in d) return d.journal.number;
    if ('quote' in d) return d.quote.number;
    if ('order' in d) return d.order.number;
    if ('po' in d) return d.po.number;
    if ('lease' in d) return d.lease.number || 'LEASE';
    if ('wo' in d) return d.wo.number || 'WO';
    if ('payroll' in d) return d.payroll.number;
    if ('payrollRun' in d) return d.payrollRun.number;
    if ('grn' in d) return d.grn.number;
    if ('job' in d) return d.job.workOrderNumber;
    if ('manifest' in d) return d.manifest.tripNumber;
    if ('inspection' in d) return 'INSP';
    if ('posOrder' in d) return d.posOrder.number;
    return '';
  });

  readonly stamp = computed(() => {
    const d = this.data();
    if (!d) return '';
    if ('journal' in d) {
      const j = String(d.journal.status ?? '').toLowerCase();
      return j === 'draft' ? 'Draft' : j === 'reversed' ? 'Reversed' : '';
    }
    if ('lease' in d) {
      const s = String(d.lease.status ?? '').toLowerCase();
      return s === 'active' ? 'Active' : s === 'draft' ? 'Draft' : s === 'terminated' ? 'Terminated' : '';
    }
    if ('quote' in d) {
      const s = String(d.quote.status ?? '').toLowerCase();
      return s === 'accepted' ? 'Accepted' : s === 'declined' ? 'Declined' : s === 'sent' ? 'Sent' : s === 'draft' ? 'Draft' : '';
    }
    if ('po' in d) {
      const s = String(d.po.status ?? '').toLowerCase();
      return s === 'approved' ? 'Approved' : s === 'received' ? 'Received' : s === 'draft' ? 'Draft' : '';
    }
    if ('order' in d) {
      const s = String(d.order.status ?? '').toLowerCase();
      return s === 'confirmed' ? 'Confirmed' : s === 'delivered' ? 'Delivered' : s === 'draft' ? 'Draft' : '';
    }
    if ('payroll' in d) {
      const s = String(d.payroll.status ?? '').toLowerCase();
      return s === 'paid' ? 'Paid' : s === 'posted' ? 'Posted' : 'Draft';
    }
    if ('payrollRun' in d) {
      const s = String(d.payrollRun.status ?? '').toLowerCase();
      return s === 'paid' ? 'Paid' : '';
    }
    if ('grn' in d) return 'Received';
    if ('job' in d) return d.job.status;
    if ('manifest' in d) return d.manifest.status;
    if ('inspection' in d) return d.inspection.status;
    if ('posOrder' in d) return d.posOrder.status === 'Completed' ? 'Paid' : d.posOrder.status;

    if ('wo' in d) {
      const s = String(d.wo.status ?? '').toLowerCase();
      return s === 'completed' ? 'Completed' : s === 'cancelled' ? 'Cancelled' : '';
    }
    const s = ('invoice' in d ? d.invoice.status : 'bill' in d ? d.bill.status : 'receipt' in d ? d.receipt.status : 'payment' in d ? d.payment.status : '') ?? '';
    const k = String(s).toLowerCase();
    return k === 'paid' ? 'Paid' : k === 'draft' ? 'Draft' : k === 'voided' ? 'Void' : k === 'pendingapproval' ? 'Pending' : '';
  });

  readonly stampClass = computed(() => {
    const s = this.stamp().toLowerCase();
    if (['paid', 'active', 'approved', 'accepted', 'sent', 'confirmed', 'delivered', 'received'].includes(s)) return 'paid';
    if (['void', 'cancelled', 'terminated', 'declined'].includes(s)) return 'void';
    return 'draft';
  });

  monthName(m: number): string { return MONTH_NAMES[m] || `Month ${m}`; }

  hasQuoteDiscounts(q: PrintQuoteData): boolean {
    return q.quote.lines.some((l) => (l.discountPercent ?? 0) > 0);
  }

  totalDebit(): number {
    const j = this.journalData();
    return j ? j.journal.lines.reduce((s, l) => s + (l.debit || 0), 0) : 0;
  }

  totalCredit(): number {
    const j = this.journalData();
    return j ? j.journal.lines.reduce((s, l) => s + (l.credit || 0), 0) : 0;
  }

  ngOnInit(): void {
    const type = this.route.snapshot.paramMap.get('type');
    const id = this.route.snapshot.paramMap.get('id');
    if (!type || !id) {
      this.error.set('Invalid document type or ID');
      this.loading.set(false);
      return;
    }
    this.docType.set(type);

    if (['invoice', 'bill', 'receipt', 'payment', 'journal'].includes(type)) {
      this.http.get<PrintData>(`${this.apiBase}/print/${type}/${id}`).subscribe({
        next: (res) => this.setLoaded(res),
        error: (err) => this.setError(err),
      });
    } else if (type === 'quote') {
      forkJoin({
        quote: this.http.get<any>(`${this.apiBase}/m/sales/quotes/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ quote, header }) => this.setLoaded({ header, quote } as PrintQuoteData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'order') {
      forkJoin({
        order: this.http.get<any>(`${this.apiBase}/m/sales/orders/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ order, header }) => this.setLoaded({ header, order } as PrintOrderData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'purchase-order') {
      forkJoin({
        po: this.http.get<any>(`${this.apiBase}/m/purchasing/orders/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ po, header }) => this.setLoaded({ header, po } as PrintPurchaseOrderData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'lease') {
      forkJoin({
        lease: this.http.get<any>(`${this.apiBase}/m/real-estate/leases/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ lease, header }) => this.setLoaded({ header, lease } as PrintLeaseData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'work-order') {
      forkJoin({
        wo: this.http.get<any>(`${this.apiBase}/m/real-estate/work-orders/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ wo, header }) => this.setLoaded({ header, wo } as PrintWorkOrderData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'payroll') {
      forkJoin({
        payroll: this.http.get<any>(`${this.apiBase}/m/hr/payroll/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ payroll, header }) => this.setLoaded({ header, payroll } as PrintPayrollData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'payslip') {
      const employeeId = this.route.snapshot.queryParamMap.get('employeeId');
      forkJoin({
        payroll: this.http.get<any>(`${this.apiBase}/m/hr/payroll/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ payroll, header }) => {
          const lines: any[] = payroll.lines || [];
          const line = (employeeId ? lines.find((l) => l.employeeId === employeeId) : lines[0]) || lines[0];
          if (!line) {
            this.error.set('Employee record not found in this payroll run');
            this.loading.set(false);
            return;
          }
          this.setLoaded({
            header,
            payrollRun: { number: payroll.number, periodYear: payroll.periodYear, periodMonth: payroll.periodMonth, status: payroll.status, paidOn: payroll.paidOn },
            line,
          } as PrintPayslipData);
        },
        error: (err) => this.setError(err),
      });
    } else if (type === 'grn' || type === 'goods-receipt') {
      forkJoin({
        grn: this.http.get<any>(`${this.apiBase}/m/purchasing/receipts/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ grn, header }) => this.setLoaded({ header, grn: {
          id: grn.id,
          number: grn.number,
          purchaseOrderNumber: grn.PurchaseOrderNumber || grn.purchaseOrderNumber,
          vendorName: grn.vendorName,
          receiptDate: grn.receiptDate,
          receivedBy: grn.receivedBy,
          deliveryNoteNumber: grn.deliveryNoteNumber,
          notes: grn.notes,
          lines: grn.lines || []
        } } as PrintGrnData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'job-card' || type === 'mfg-order') {
      forkJoin({
        job: this.http.get<any>(`${this.apiBase}/m/manufacturing/workorders/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ job, header }) => this.setLoaded({ header, job: job.data || job } as PrintJobCardData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'manifest' || type === 'trip') {
      forkJoin({
        manifest: this.http.get<any>(`${this.apiBase}/m/distribution/trips/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ manifest, header }) => this.setLoaded({ header, manifest: manifest.data || manifest } as PrintManifestData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'inspection') {
      forkJoin({
        inspection: this.http.get<any>(`${this.apiBase}/m/real-estate/inspections/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ inspection, header }) => this.setLoaded({ header, inspection: inspection.data || inspection } as PrintInspectionData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'pos-receipt') {
      forkJoin({
        order: this.http.get<any>(`${this.apiBase}/m/pos/orders/${id}`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ order, header }) => this.setLoaded({ header, posOrder: order.data || order } as PrintPosReceiptData),
        error: (err) => this.setError(err),
      });
    } else {
      this.error.set(`Unsupported document type: ${type}`);
      this.loading.set(false);
    }
  }

  private loadHeader() {
    return this.http.get<any>(`${this.apiBase}/admin/legal-entities?pageSize=10`).pipe(
      map((res) => {
        const items = res?.data || res?.items || [];
        const entity = items[0] || {};
        const ws = this.session.me()?.workspace;
        return {
          organizationName: ws?.name || entity.name || 'HigherSquare 360 ERP',
          legalEntityName: entity.legalName || entity.name || ws?.name || 'HigherSquare 360 ERP',
          legalEntityAddress: entity.address || '',
          taxRegistrationNo: entity.taxRegistrationNo || '',
          registrationNo: entity.registrationNo || '',
          phone: entity.phone || '',
          email: entity.email || '',
          baseCurrency: entity.baseCurrency || ws?.baseCurrency || 'AED',
          countryCode: entity.countryCode || ws?.countryCode || 'AE',
        } as PrintHeader;
      }),
      catchError(() => {
        const ws = this.session.me()?.workspace;
        return of({
          organizationName: ws?.name || 'HigherSquare 360 ERP',
          legalEntityName: ws?.name || 'HigherSquare 360 ERP',
          legalEntityAddress: '',
          taxRegistrationNo: '',
          registrationNo: '',
          phone: '',
          email: '',
          baseCurrency: ws?.baseCurrency || 'AED',
          countryCode: ws?.countryCode || 'AE',
        } as PrintHeader);
      })
    );
  }

  private setLoaded(res: PrintData): void {
    this.data.set(res);
    this.loading.set(false);
    this.titleService.setTitle(`${this.title()} ${this.docNumber()}`.trim());
  }

  private setError(err: any): void {
    this.error.set(err?.error?.message || err?.error?.detail || 'Failed to load document');
    this.loading.set(false);
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

  isPdc(kind: string | null | undefined, chequeDate: string | null | undefined, docDate: string): boolean {
    if (String(kind ?? '').toLowerCase() === 'postdatedcheque') return true;
    return !!chequeDate && chequeDate.slice(0, 10) > String(docDate).slice(0, 10);
  }

  humanize(v: string | null | undefined): string {
    if (!v) return '';
    const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2');
    return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
  }
}
