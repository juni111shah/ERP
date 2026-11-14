import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { PosReturnDto, PosShiftReportDto } from './pos.api';

/** The print page's documents: the server-rendered finance documents and the module records it prints. */

/** The issuer block at the top of every printed document. */
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

/** One employee's line of a payroll run. */
export interface PrintPayrollLine {
  employeeId: string; employeeName: string; employeeNumber: string; basic: number; allowances: number; overtime: number; absence: number; adjustment: number;
  grossEarnings: number; deductions: number; netPay: number;
}

export interface PrintPayrollData {
  header: PrintHeader;
  payroll: {
    id: string; number: string; periodYear: number; periodMonth: number; employees: number; grossEarnings: number; deductions: number; netPay: number;
    status: string; paidOn?: string | null;
    lines: PrintPayrollLine[];
  };
}

export interface PrintPayslipData {
  header: PrintHeader;
  payrollRun: { number: string; periodYear: number; periodMonth: number; status: string; paidOn?: string | null };
  line: PrintPayrollLine;
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

/** A register's own receipt wording. */
export interface PrintRegister { name?: string; receiptHeader?: string | null; receiptFooter?: string | null; }

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
  register?: PrintRegister | null;
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

/** A record some module APIs return wrapped in `data`. */
export type MaybeWrapped<T> = T & { data?: T };

/** A goods receipt as the purchasing API returns it (older servers send `PurchaseOrderNumber`). */
export type GoodsReceiptRecord = Omit<PrintGrnData['grn'], 'lines'> & { PurchaseOrderNumber?: string; lines?: PrintGrnData['grn']['lines'] };

/** A POS sale as the POS API returns it. */
export type PosOrderRecord = PrintPosReceiptData['posOrder'] & { registerId: string };

/** A legal entity's printable details, as the legal-entities list returns them. */
export interface PrintEntityRow {
  name?: string; legalName?: string; address?: string; taxRegistrationNo?: string; registrationNo?: string;
  phone?: string; email?: string; baseCurrency?: string; countryCode?: string;
}

/** The legal-entities list, either enveloped (`data`) or paged (`items`). */
export interface PrintEntityList { data?: PrintEntityRow[]; items?: PrintEntityRow[]; }

/** Reads everything the print page prints. */
@Injectable({ providedIn: 'root' })
export class PrintApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  /** A finance document (invoice, bill, receipt, payment, journal) as the server prepares it for printing. */
  document(kind: string, id: string): Observable<PrintData> {
    return this.http.get<PrintData>(`${this.base}/print/${kind}/${id}`);
  }

  /** The server-rendered PDF of a finance document, with its headers (for the file name). */
  pdf(kind: string, id: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.base}/print/${kind}/${id}/pdf`, { responseType: 'blob', observe: 'response' });
  }

  /** The first legal entities, for the issuer block. */
  legalEntities(): Observable<PrintEntityList | null> {
    return this.http.get<PrintEntityList | null>(`${this.base}/legal-entities?pageSize=10`);
  }

  quote(id: string): Observable<PrintQuoteData['quote']> {
    return this.http.get<PrintQuoteData['quote']>(`${this.base}/m/sales/quotes/${id}`);
  }

  salesOrder(id: string): Observable<PrintOrderData['order']> {
    return this.http.get<PrintOrderData['order']>(`${this.base}/m/sales/orders/${id}`);
  }

  purchaseOrder(id: string): Observable<PrintPurchaseOrderData['po']> {
    return this.http.get<PrintPurchaseOrderData['po']>(`${this.base}/m/purchasing/orders/${id}`);
  }

  lease(id: string): Observable<PrintLeaseData['lease']> {
    return this.http.get<PrintLeaseData['lease']>(`${this.base}/m/realestate/leases/${id}`);
  }

  workOrder(id: string): Observable<PrintWorkOrderData['wo']> {
    return this.http.get<PrintWorkOrderData['wo']>(`${this.base}/m/realestate/work-orders/${id}`);
  }

  payroll(id: string): Observable<PrintPayrollData['payroll']> {
    return this.http.get<PrintPayrollData['payroll']>(`${this.base}/m/hr/payroll/${id}`);
  }

  goodsReceipt(id: string): Observable<GoodsReceiptRecord> {
    return this.http.get<GoodsReceiptRecord>(`${this.base}/m/purchasing/receipts/${id}`);
  }

  jobCard(id: string): Observable<MaybeWrapped<PrintJobCardData['job']>> {
    return this.http.get<MaybeWrapped<PrintJobCardData['job']>>(`${this.base}/m/manufacturing/workorders/${id}`);
  }

  tripManifest(id: string): Observable<MaybeWrapped<PrintManifestData['manifest']>> {
    return this.http.get<MaybeWrapped<PrintManifestData['manifest']>>(`${this.base}/m/distribution/trips/${id}`);
  }

  inspection(id: string): Observable<MaybeWrapped<PrintInspectionData['inspection']>> {
    return this.http.get<MaybeWrapped<PrintInspectionData['inspection']>>(`${this.base}/m/realestate/inspections/${id}`);
  }

  posOrder(id: string): Observable<PosOrderRecord> {
    return this.http.get<PosOrderRecord>(`${this.base}/m/pos/orders/${id}`);
  }

  posReturn(id: string): Observable<PosReturnDto> {
    return this.http.get<PosReturnDto>(`${this.base}/m/pos/returns/${id}`);
  }

  posRegister(id: string): Observable<PrintRegister> {
    return this.http.get<PrintRegister>(`${this.base}/m/pos/registers/${id}`);
  }

  posShiftReport(sessionId: string): Observable<PosShiftReportDto> {
    return this.http.get<PosShiftReportDto>(`${this.base}/m/pos/sessions/${sessionId}/report`);
  }
}
