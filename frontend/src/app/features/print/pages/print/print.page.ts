import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, Signal, computed, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import {
  DocLine, PrintApi, PrintBillData, PrintData, PrintGrnData, PrintInspectionData, PrintInvoiceData, PrintJobCardData, PrintJournalData,
  PrintLeaseData, PrintManifestData, PrintOrderData, PrintPaymentData, PrintPayrollData, PrintPayslipData, PrintPosReceiptData,
  PrintPosReturnData, PrintPosShiftData, PrintPurchaseOrderData, PrintQuoteData, PrintReceiptData, PrintWorkOrderData,
} from '@core/api/print.api';
import { I18nService } from '@core/i18n/i18n.service';
import { displayNote } from '@core/i18n/translate-now';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ThermalReceiptComponent, ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';
import { saveBlob } from '@shared/utils/save-blob';
import { PosShiftReportComponent } from '@features/pos/components/pos-shift-report/pos-shift-report.component';
import { TenantThemeService } from '../../../../../theme/tenant-theme.service';
import {
  contactText, documentNumber, documentStamp, documentTitle, humanizeCode, isPostDated, lineTaxRate, monthNameOf, stampTone, taxNameFor,
  taxRegLabelFor,
} from '../../print-labels';
import { PrintLoader, loadErrorText } from '../../print-loader';
import { cashReceiptSlip, posReturnSlip, posSaleSlip } from '../../print-slips';

/** The data of one kind of document (the kind is told apart by its key, e.g. `invoice`), or null when another kind is shown. */
type KindOf<T> = Signal<T | null>;

/** A document to print (or save as PDF): finance documents, sales and purchasing records, leases, payroll, POS slips... */
@Component({
  selector: 'erp-print',
  standalone: true,
  imports: [TranslatePipe, CommonModule, DatePipe, DecimalPipe, NzButtonModule, NzIconModule, NzToolTipModule, ThermalReceiptComponent, PosShiftReportComponent],
  providers: [PrintLoader],
  styleUrl: './print.page.scss',
  templateUrl: './print.page.html',
})
export class PrintPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly titleService = inject(Title);
  private readonly i18n = inject(I18nService);
  private readonly printApi = inject(PrintApi);
  private readonly loader = inject(PrintLoader);
  readonly theme = inject(TenantThemeService);

  noteText(note: string | null | undefined): string { return displayNote(note, this.i18n.lang()); }

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly data = signal<PrintData | null>(null);
  readonly docType = signal('');

  readonly invoiceData: KindOf<PrintInvoiceData> = this.kind('invoice');
  readonly billData: KindOf<PrintBillData> = this.kind('bill');
  readonly receiptData: KindOf<PrintReceiptData> = this.kind('receipt');
  readonly paymentData: KindOf<PrintPaymentData> = this.kind('payment');
  readonly journalData: KindOf<PrintJournalData> = this.kind('journal');
  readonly quoteData: KindOf<PrintQuoteData> = this.kind('quote');
  readonly orderData: KindOf<PrintOrderData> = this.kind('order');
  readonly purchaseOrderData: KindOf<PrintPurchaseOrderData> = this.kind('po');
  readonly leaseData: KindOf<PrintLeaseData> = this.kind('lease');
  readonly workOrderData: KindOf<PrintWorkOrderData> = this.kind('wo');
  readonly payrollData: KindOf<PrintPayrollData> = this.kind('payroll');
  readonly payslipData: KindOf<PrintPayslipData> = this.kind('payrollRun');
  readonly grnData: KindOf<PrintGrnData> = this.kind('grn');
  readonly jobCardData: KindOf<PrintJobCardData> = this.kind('job');
  readonly manifestData: KindOf<PrintManifestData> = this.kind('manifest');
  readonly inspectionData: KindOf<PrintInspectionData> = this.kind('inspection');
  readonly posReceiptData: KindOf<PrintPosReceiptData> = this.kind('posOrder');
  readonly posShiftData: KindOf<PrintPosShiftData> = this.kind('posShift');
  private readonly posReturnData: KindOf<PrintPosReturnData> = this.kind('posReturn');

  readonly receiptSlipMode = signal(false);
  toggleSlipMode(): void { this.receiptSlipMode.update((v) => !v); }

  readonly posReceiptPayload = computed<ThermalReceiptData | null>(() => { const pr = this.posReceiptData(); return pr ? posSaleSlip(pr) : null; });
  readonly posReturnPayload = computed<ThermalReceiptData | null>(() => { const pr = this.posReturnData(); return pr ? posReturnSlip(pr) : null; });
  readonly receiptSlipPayload = computed<ThermalReceiptData | null>(() => { const r = this.receiptData(); return r ? cashReceiptSlip(r) : null; });

  readonly taxName = computed(() => taxNameFor(this.data()?.header.countryCode));
  readonly taxRegLabel = computed(() => taxRegLabelFor(this.data()?.header.countryCode));
  readonly contactLine = computed(() => {
    const h = this.data()?.header;
    return h ? contactText(h.phone, h.email) : '';
  });

  readonly title = computed(() => { const d = this.data(); return d ? documentTitle(d) : ''; });
  readonly docNumber = computed(() => { const d = this.data(); return d ? documentNumber(d) : ''; });
  readonly stamp = computed(() => { const d = this.data(); return d ? documentStamp(d) : ''; });
  readonly stampClass = computed(() => stampTone(this.stamp()));

  /** Documents the server can render as a real PDF file (GET /print/{kind}/{id}/pdf). */
  readonly pdfKinds = ['invoice', 'bill', 'receipt', 'payment', 'journal'];
  readonly pdfBusy = signal(false);
  readonly pdfError = signal<string | null>(null);

  monthName(m: number): string { return monthNameOf(m); }

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
    const params = this.route.snapshot.paramMap;
    const type = params.get('type');
    const id = params.get('id');
    if (!type || !id) {
      this.fail('Invalid document type or ID');
      return;
    }
    this.docType.set(type);

    const document$ = this.loader.load(type, id, this.route.snapshot.queryParamMap.get('employeeId'));
    if (!document$) {
      this.fail(`Unsupported document type: ${type}`);
      return;
    }
    document$.subscribe({
      next: (res) => this.setLoaded(res),
      error: (err) => this.fail(loadErrorText(err)),
    });
  }

  partyContact(p: { partyPhone?: string | null; partyEmail?: string | null }): string {
    return contactText(p.partyPhone, p.partyEmail);
  }

  print(): void { window.print(); }

  downloadPdf(): void {
    const type = this.docType();
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || !this.pdfKinds.includes(type)) return;
    this.pdfBusy.set(true);
    this.pdfError.set(null);
    this.printApi.pdf(type, id).subscribe({
      next: (res) => {
        const header = res.headers.get('content-disposition') ?? '';
        const name = /filename="?([^";]+)"?/i.exec(header)?.[1] ?? `${type}-${id}.pdf`;
        saveBlob(res.body as Blob, name);
        this.pdfBusy.set(false);
      },
      error: () => {
        this.pdfBusy.set(false);
        this.pdfError.set('Could not download the PDF. Try again.');
      },
    });
  }

  close(): void { window.close(); }

  taxRate(l: DocLine): string { return lineTaxRate(l); }

  isPdc(kind: string | null | undefined, chequeDate: string | null | undefined, docDate: string): boolean {
    return isPostDated(kind, chequeDate, docDate);
  }

  humanize(v: string | null | undefined): string { return humanizeCode(v); }

  /** The document as one kind (told apart by its `key`), or null. */
  private kind<T extends PrintData>(key: string): KindOf<T> {
    return computed(() => { const d = this.data(); return d && key in d ? (d as T) : null; });
  }

  private setLoaded(res: PrintData): void {
    this.data.set(res);
    this.loading.set(false);
    this.titleService.setTitle(`${this.title()} ${this.docNumber()}`.trim());
  }

  private fail(message: string): void {
    this.error.set(message);
    this.loading.set(false);
  }
}
