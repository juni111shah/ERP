import { DocLine, PrintData } from '@core/api/print.api';

/** The wording of a printed document: its title, number, status stamp, tax labels and small formatting helpers. */

const TAX_NAME: Record<string, string> = { AE: 'VAT', SA: 'VAT', GB: 'VAT', IN: 'GST', PK: 'Sales tax', US: 'Sales tax' };

const MONTH_NAMES = ['', 'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** The tax's name in the issuer's country ("VAT", "GST"...). */
export const taxNameFor = (countryCode: string | null | undefined): string => TAX_NAME[(countryCode ?? '').toUpperCase()] ?? 'Tax';

/** The label of the issuer's tax registration number in its country ("TRN", "GSTIN"...). */
export function taxRegLabelFor(countryCode: string | null | undefined): string {
  const c = (countryCode ?? '').toUpperCase();
  return c === 'AE' || c === 'SA' ? 'TRN' : c === 'IN' ? 'GSTIN' : c === 'PK' ? 'NTN' : c === 'GB' ? 'VAT no.' : 'Tax reg. no.';
}

/** Phone and email on one line, the empty ones left out. */
export const contactText = (phone: string | null | undefined, email: string | null | undefined): string =>
  [phone, email].filter((x) => !!x).join(' · ');

/** The document's title ("Tax invoice", "Sales quotation"...). */
export function documentTitle(d: PrintData): string {
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
}

/** The document's number, or a placeholder for records without one. */
export function documentNumber(d: PrintData): string {
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
}

const lower = (s: string | null | undefined) => String(s ?? '').toLowerCase();

/** The status stamp across the document ("paid", "draft", "Void"...), or '' for none. */
export function documentStamp(d: PrintData): string {
  if ('journal' in d) {
    const j = lower(d.journal.status);
    return j === 'draft' ? 'draft' : j === 'reversed' ? 'Reversed' : '';
  }
  if ('lease' in d) {
    const s = lower(d.lease.status);
    return s === 'active' ? 'active' : s === 'draft' ? 'draft' : s === 'terminated' ? 'terminated' : '';
  }
  if ('quote' in d) {
    const s = lower(d.quote.status);
    return s === 'accepted' ? 'accepted' : s === 'declined' ? 'declined' : s === 'sent' ? 'sent' : s === 'draft' ? 'draft' : '';
  }
  if ('po' in d) {
    const s = lower(d.po.status);
    return s === 'approved' ? 'approved' : s === 'received' ? 'received' : s === 'draft' ? 'draft' : '';
  }
  if ('order' in d) {
    const s = lower(d.order.status);
    return s === 'confirmed' ? 'confirmed' : s === 'delivered' ? 'delivered' : s === 'draft' ? 'draft' : '';
  }
  if ('payroll' in d) {
    const s = lower(d.payroll.status);
    return s === 'paid' ? 'paid' : s === 'posted' ? 'Posted' : 'Draft';
  }
  if ('payrollRun' in d) {
    const s = lower(d.payrollRun.status);
    return s === 'paid' ? 'paid' : '';
  }
  if ('grn' in d) return 'Received';
  if ('job' in d) return d.job.status;
  if ('manifest' in d) return d.manifest.status;
  if ('inspection' in d) return d.inspection.status;
  if ('posOrder' in d) return d.posOrder.status === 'completed' ? 'paid' : d.posOrder.status;

  if ('wo' in d) {
    const s = lower(d.wo.status);
    return s === 'completed' ? 'completed' : s === 'cancelled' ? 'cancelled' : '';
  }
  const s = ('invoice' in d ? d.invoice.status : 'bill' in d ? d.bill.status : 'receipt' in d ? d.receipt.status : 'payment' in d ? d.payment.status : '') ?? '';
  const k = lower(s);
  return k === 'paid' ? 'paid' : k === 'draft' ? 'draft' : k === 'voided' ? 'Void' : k === 'pendingapproval' ? 'pending' : '';
}

/** The stamp's colour: green for done, red for void, grey otherwise. */
export function stampTone(stamp: string): 'paid' | 'void' | 'draft' {
  const s = stamp.toLowerCase();
  if (['paid', 'active', 'approved', 'accepted', 'sent', 'confirmed', 'delivered', 'received'].includes(s)) return 'paid';
  if (['void', 'cancelled', 'terminated', 'declined'].includes(s)) return 'void';
  return 'draft';
}

export const monthNameOf = (m: number): string => MONTH_NAMES[m] || `Month ${m}`;

/** A line's tax as a percentage of its net amount ("5%"), or '' without a net amount. */
export function lineTaxRate(l: DocLine): string {
  if (!l.net) return '';
  const r = (l.taxAmount / l.net) * 100;
  return `${Math.round(r * 100) / 100}%`;
}

/** A post-dated cheque: marked as one, or dated after the document. */
export function isPostDated(kind: string | null | undefined, chequeDate: string | null | undefined, docDate: string): boolean {
  if (String(kind ?? '').toLowerCase() === 'postdatedcheque') return true;
  return !!chequeDate && chequeDate.slice(0, 10) > String(docDate).slice(0, 10);
}

/** "pendingApproval" as "Pending approval". */
export function humanizeCode(v: string | null | undefined): string {
  if (!v) return '';
  const s = String(v).replace(/([a-z])([A-Z])/g, '$1 $2');
  return s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();
}
