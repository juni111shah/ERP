import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import {
  GoodsReceiptRecord, PrintApi, PrintData, PrintEntityList, PrintGrnData, PrintHeader, PrintPayrollData, PrintPayslipData, PrintRegister,
} from '@core/api/print.api';
import { MeResponse, SessionStore } from '@core/session/session.store';

/** A load failure with a message of its own, shown as it is (not the server's error text). */
export class PrintLoadError extends Error {}

/** Shown when nothing better is known about the issuer. */
const FALLBACK_NAME = 'HigherSquare 360 ERP';

type PrintWorkspace = MeResponse['workspace'] | undefined;

/** The issuer block from the first legal entity, filled in from the workspace. */
export function headerFrom(res: PrintEntityList | null, ws: PrintWorkspace): PrintHeader {
  const items = res?.data || res?.items || [];
  const entity = items[0] || {};
  return {
    organizationName: ws?.name || entity.name || FALLBACK_NAME,
    legalEntityName: entity.legalName || entity.name || ws?.name || FALLBACK_NAME,
    legalEntityAddress: entity.address || '',
    taxRegistrationNo: entity.taxRegistrationNo || '',
    registrationNo: entity.registrationNo || '',
    phone: entity.phone || '',
    email: entity.email || '',
    baseCurrency: entity.baseCurrency || ws?.baseCurrency || '',
    countryCode: entity.countryCode || ws?.countryCode || 'AE',
  };
}

/** The issuer block when the legal entities cannot be read: the workspace only. */
export function workspaceHeader(ws: PrintWorkspace): PrintHeader {
  return {
    organizationName: ws?.name || FALLBACK_NAME,
    legalEntityName: ws?.name || FALLBACK_NAME,
    legalEntityAddress: '',
    taxRegistrationNo: '',
    registrationNo: '',
    phone: '',
    email: '',
    baseCurrency: ws?.baseCurrency || '',
    countryCode: ws?.countryCode || 'AE',
  };
}

/** The goods receipt as the print page shows it. */
function grnOf(grn: GoodsReceiptRecord): PrintGrnData['grn'] {
  return {
    id: grn.id,
    number: grn.number,
    purchaseOrderNumber: grn.PurchaseOrderNumber || grn.purchaseOrderNumber,
    vendorName: grn.vendorName,
    receiptDate: grn.receiptDate,
    receivedBy: grn.receivedBy,
    deliveryNoteNumber: grn.deliveryNoteNumber,
    notes: grn.notes,
    lines: grn.lines || [],
  };
}

/** One employee's payslip from a payroll run: the employee asked for, else the first line; an error when the run has no lines. */
function payslipOf(payroll: PrintPayrollData['payroll'], header: PrintHeader, employeeId: string | null): PrintPayslipData {
  const lines = payroll.lines || [];
  const line = (employeeId ? lines.find((l) => l.employeeId === employeeId) : lines[0]) || lines[0];
  if (!line) throw new PrintLoadError('Employee record not found in this payroll run');
  return {
    header,
    payrollRun: { number: payroll.number, periodYear: payroll.periodYear, periodMonth: payroll.periodMonth, status: payroll.status, paidOn: payroll.paidOn },
    line,
  };
}

/**
 * Loads a document to print by its route kind (`invoice`, `quote`, `pos-receipt`...): finance documents come ready from the server,
 * module records are read from their module and combined with the issuer block. Provided by the print page.
 */
@Injectable()
export class PrintLoader {
  private readonly printApi = inject(PrintApi);
  private readonly session = inject(SessionStore);

  /** The document, or null for a kind the page cannot print. */
  load(type: string, id: string, employeeId: string | null): Observable<PrintData> | null {
    const api = this.printApi;
    const header = () => this.header();
    switch (type) {
      case 'invoice': case 'bill': case 'receipt': case 'payment': case 'journal':
        return api.document(type, id);
      case 'quote':
        return forkJoin({ quote: api.quote(id), header: header() });
      case 'order':
        return forkJoin({ order: api.salesOrder(id), header: header() });
      case 'purchase-order': case 'po':
        return forkJoin({ po: api.purchaseOrder(id), header: header() });
      case 'lease':
        return forkJoin({ lease: api.lease(id), header: header() });
      case 'work-order': case 'workorder':
        return forkJoin({ wo: api.workOrder(id), header: header() });
      case 'payroll':
        return forkJoin({ payroll: api.payroll(id), header: header() });
      case 'payslip':
        return forkJoin({ payroll: api.payroll(id), header: header() }).pipe(map(({ payroll, header }) => payslipOf(payroll, header, employeeId)));
      case 'grn': case 'goods-receipt':
        return forkJoin({ grn: api.goodsReceipt(id), header: header() }).pipe(map(({ grn, header }) => ({ header, grn: grnOf(grn) })));
      case 'job-card': case 'mfg-order':
        return forkJoin({ job: api.jobCard(id), header: header() }).pipe(map(({ job, header }) => ({ header, job: job.data || job })));
      case 'manifest': case 'trip':
        return forkJoin({ manifest: api.tripManifest(id), header: header() }).pipe(map(({ manifest, header }) => ({ header, manifest: manifest.data || manifest })));
      case 'inspection':
        return forkJoin({ inspection: api.inspection(id), header: header() }).pipe(map(({ inspection, header }) => ({ header, inspection: inspection.data || inspection })));
      case 'pos-return':
        return api.posReturn(id).pipe(
          switchMap((posReturn) => forkJoin({ posReturn: of(posReturn), header: header(), register: this.register(posReturn.registerId) })),
        );
      case 'pos-shift':
        return forkJoin({ posShift: api.posShiftReport(id), header: header() });
      case 'pos-receipt':
        return api.posOrder(id).pipe(
          switchMap((order) => forkJoin({ header: header(), register: this.register(order.registerId) }).pipe(
            map(({ header, register }) => ({ header, posOrder: order, register })),
          )),
        );
      default:
        return null;
    }
  }

  /** The issuer block; the workspace's details when the legal entities cannot be read. */
  private header(): Observable<PrintHeader> {
    return this.printApi.legalEntities().pipe(
      map((res) => headerFrom(res, this.session.me()?.workspace)),
      catchError(() => of(workspaceHeader(this.session.me()?.workspace))),
    );
  }

  /** The register's receipt wording; a register that cannot be read must not stop the receipt from printing. */
  private register(registerId: string): Observable<PrintRegister | null> {
    return this.printApi.posRegister(registerId).pipe(catchError(() => of(null)));
  }
}

/** The message shown when a document cannot be loaded. */
export function loadErrorText(err: unknown): string {
  if (err instanceof PrintLoadError) return err.message;
  const body = (err as { error?: { message?: string; detail?: string } } | null)?.error;
  return body?.message || body?.detail || 'Failed to load document';
}
