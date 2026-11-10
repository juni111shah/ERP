import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Title } from '@angular/platform-browser';
import { HttpClient } from '@angular/common/http';
import { forkJoin, map, of, catchError, switchMap } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { API_BASE_URL } from '@core/api/api-config';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { I18nService } from '@core/i18n/i18n.service';
import { displayNote } from '@core/i18n/translate-now';
import { SessionStore } from '@core/session/session.store';
import { TenantThemeService } from '../../../../../theme/tenant-theme.service';
import { ThermalReceiptComponent, ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';
import { PosOrderDto, PosReturnDto, PosShiftReportDto } from '@core/api/pos.api';
import { returnReceipt, saleReceipt } from '@features/pos/pos-receipt';
import { PosShiftReportComponent } from '@features/pos/components/pos-shift-report/pos-shift-report.component';

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
  register?: { receiptHeader?: string | null; receiptFooter?: string | null } | null;
  posOrder: {
    id: string; number: string; customerName: string; cashierName: string; orderDate: string; subTotal: number;
    taxTotal: number; totalAmount: number; status: string; notes?: string | null;
    lines: { itemCode: string; description: string; quantity: number; unitPrice: number; lineTotal: number }[];
    payments?: { method: string; amount: number; changeAmount: number }[];
  };
}

export interface PrintPosReturnData {
  header: PrintHeader;
  register?: { name?: string; receiptHeader?: string | null; receiptFooter?: string | null } | null;
  posReturn: PosReturnDto;
}

export interface PrintPosShiftData {
  header: PrintHeader;
  posShift: PosShiftReportDto;
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
  | PrintPosReceiptData
  | PrintPosReturnData
  | PrintPosShiftData;

const TAX_NAME: Record<string, string> = { AE: 'VAT', SA: 'VAT', GB: 'VAT', IN: 'GST', PK: 'Sales tax', US: 'Sales tax' };

const MONTH_NAMES = ['', 'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

@Component({
  selector: 'erp-print',
  standalone: true,
  imports: [TranslatePipe, CommonModule, DatePipe, DecimalPipe, NzButtonModule, NzIconModule, NzToolTipModule, ThermalReceiptComponent, PosShiftReportComponent],
  styleUrl: './print.page.scss',
  templateUrl: './print.page.html',
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
    return saleReceipt(pr.posOrder as unknown as PosOrderDto, {
      business: d.header.organizationName || undefined,
      register: pr.register,
      address: d.header.legalEntityAddress || undefined,
      currency: d.header.baseCurrency || '',
    });
  });

  readonly posReturnPayload = computed<ThermalReceiptData | null>(() => {
    const d = this.data();
    if (!d || !('posReturn' in d)) return null;
    const pr = d as PrintPosReturnData;
    return returnReceipt(pr.posReturn, {
      business: d.header.organizationName || undefined,
      register: pr.register,
      address: d.header.legalEntityAddress || undefined,
      currency: d.header.baseCurrency || '',
    });
  });

  readonly posShiftData = computed(() => { const d = this.data(); return d && 'posShift' in d ? (d as PrintPosShiftData) : null; });

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
        method: r.receipt.method || 'cash',
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
      return j === 'draft' ? 'draft' : j === 'reversed' ? 'Reversed' : '';
    }
    if ('lease' in d) {
      const s = String(d.lease.status ?? '').toLowerCase();
      return s === 'active' ? 'active' : s === 'draft' ? 'draft' : s === 'terminated' ? 'terminated' : '';
    }
    if ('quote' in d) {
      const s = String(d.quote.status ?? '').toLowerCase();
      return s === 'accepted' ? 'accepted' : s === 'declined' ? 'declined' : s === 'sent' ? 'sent' : s === 'draft' ? 'draft' : '';
    }
    if ('po' in d) {
      const s = String(d.po.status ?? '').toLowerCase();
      return s === 'approved' ? 'approved' : s === 'received' ? 'received' : s === 'draft' ? 'draft' : '';
    }
    if ('order' in d) {
      const s = String(d.order.status ?? '').toLowerCase();
      return s === 'confirmed' ? 'confirmed' : s === 'delivered' ? 'delivered' : s === 'draft' ? 'draft' : '';
    }
    if ('payroll' in d) {
      const s = String(d.payroll.status ?? '').toLowerCase();
      return s === 'paid' ? 'paid' : s === 'posted' ? 'Posted' : 'Draft';
    }
    if ('payrollRun' in d) {
      const s = String(d.payrollRun.status ?? '').toLowerCase();
      return s === 'paid' ? 'paid' : '';
    }
    if ('grn' in d) return 'Received';
    if ('job' in d) return d.job.status;
    if ('manifest' in d) return d.manifest.status;
    if ('inspection' in d) return d.inspection.status;
    if ('posOrder' in d) return d.posOrder.status === 'completed' ? 'paid' : d.posOrder.status;

    if ('wo' in d) {
      const s = String(d.wo.status ?? '').toLowerCase();
      return s === 'completed' ? 'completed' : s === 'cancelled' ? 'cancelled' : '';
    }
    const s = ('invoice' in d ? d.invoice.status : 'bill' in d ? d.bill.status : 'receipt' in d ? d.receipt.status : 'payment' in d ? d.payment.status : '') ?? '';
    const k = String(s).toLowerCase();
    return k === 'paid' ? 'paid' : k === 'draft' ? 'draft' : k === 'voided' ? 'Void' : k === 'pendingapproval' ? 'pending' : '';
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
    } else if (type === 'purchase-order' || type === 'po') {
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
    } else if (type === 'work-order' || type === 'workorder') {
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
    } else if (type === 'pos-return') {
      this.http.get<PosReturnDto>(`${this.apiBase}/m/pos/returns/${id}`).pipe(
        switchMap((posReturn) => forkJoin({
          posReturn: of(posReturn),
          header: this.loadHeader(),
          register: this.http.get<any>(`${this.apiBase}/m/pos/registers/${posReturn.registerId}`).pipe(catchError(() => of(null))),
        })),
      ).subscribe({
        next: ({ posReturn, header, register }) => this.setLoaded({ header, posReturn, register } as PrintPosReturnData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'pos-shift') {
      forkJoin({
        posShift: this.http.get<PosShiftReportDto>(`${this.apiBase}/m/pos/sessions/${id}/report`),
        header: this.loadHeader(),
      }).subscribe({
        next: ({ posShift, header }) => this.setLoaded({ header, posShift } as PrintPosShiftData),
        error: (err) => this.setError(err),
      });
    } else if (type === 'pos-receipt') {
      this.http.get<any>(`${this.apiBase}/m/pos/orders/${id}`).pipe(
        switchMap((order) => forkJoin({
          order: of(order),
          header: this.loadHeader(),
          // a register that cannot be read must not stop the receipt from printing
          register: this.http.get<any>(`${this.apiBase}/m/pos/registers/${order.registerId}`).pipe(catchError(() => of(null))),
        })),
      ).subscribe({
        next: ({ order, header, register }) => this.setLoaded({ header, posOrder: order, register } as PrintPosReceiptData),
        error: (err) => this.setError(err),
      });
    } else {
      this.error.set(`Unsupported document type: ${type}`);
      this.loading.set(false);
    }
  }

  private loadHeader() {
    return this.http.get<any>(`${this.apiBase}/legal-entities?pageSize=10`).pipe(
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
          baseCurrency: entity.baseCurrency || ws?.baseCurrency || '',
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
          baseCurrency: ws?.baseCurrency || '',
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

  /** Documents the server can render as a real PDF file (GET /print/{kind}/{id}/pdf). */
  readonly pdfKinds = ['invoice', 'bill', 'receipt', 'payment', 'journal'];
  readonly pdfBusy = signal(false);
  readonly pdfError = signal<string | null>(null);

  downloadPdf(): void {
    const type = this.docType();
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || !this.pdfKinds.includes(type)) return;
    this.pdfBusy.set(true);
    this.pdfError.set(null);
    this.http.get(`${this.apiBase}/print/${type}/${id}/pdf`, { responseType: 'blob', observe: 'response' }).subscribe({
      next: (res) => {
        const header = res.headers.get('content-disposition') ?? '';
        const name = /filename="?([^";]+)"?/i.exec(header)?.[1] ?? `${type}-${id}.pdf`;
        const url = URL.createObjectURL(res.body as Blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        this.pdfBusy.set(false);
      },
      error: () => {
        this.pdfBusy.set(false);
        this.pdfError.set('Could not download the PDF. Try again.');
      },
    });
  }
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
