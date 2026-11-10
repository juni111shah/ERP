import { displayLabel } from '@core/format/display';
import { PosOrderDto, PosReturnDto } from '@core/api/pos.api';
import { ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';

export interface ReceiptContext {
  business?: string;
  /** The register's own wording; the name is left out when it cannot be read. */
  register?: { name?: string; receiptHeader?: string | null; receiptFooter?: string | null } | null;
  address?: string;
  currency: string;
}

/** One receipt, whether it is shown on the till, in the back office or on the print page. */
export function saleReceipt(o: PosOrderDto, c: ReceiptContext): ThermalReceiptData {
  const taxed = o.taxTotal > 0;
  return {
    organizationName: c.business,
    organizationNameAr: c.business,
    branchName: c.register?.name,
    branchAddress: c.address,
    headerNote: c.register?.receiptHeader,
    footerNote: c.register?.receiptFooter,
    title: taxed ? 'Tax Invoice / Receipt' : 'Sales Receipt',
    titleAr: taxed ? 'فاتورة ضريبية / إيصال مبيعات' : 'إيصال مبيعات',
    receiptNumber: o.number,
    date: o.orderDate,
    status: displayLabel(o.status),
    cashierName: o.cashierName,
    customerName: o.customerName,
    currency: c.currency,
    lines: (o.lines || []).map((l) => ({
      code: l.itemCode,
      description: l.description,
      quantity: l.quantity,
      unitPrice: l.unitPrice,
      discountPercent: l.discountPercent,
      lineTotal: l.lineTotal,
    })),
    subTotal: o.subTotal,
    discountTotal: o.discountTotal,
    taxTotal: o.taxTotal,
    totalAmount: o.totalAmount,
    payments: (o.payments || []).map((p) => ({
      method: displayLabel(p.method === 'account' ? 'onAccount' : p.method),
      amount: p.amount,
      tendered: p.tenderedAmount,
      changeAmount: p.changeAmount,
      reference: p.reference,
    })),
    referenceNo: o.invoiceNumber ? `${o.number} / ${o.invoiceNumber}` : undefined,
    notes: o.notes,
  };
}

export function returnReceipt(r: PosReturnDto, c: ReceiptContext): ThermalReceiptData {
  return {
    organizationName: c.business,
    organizationNameAr: c.business,
    branchName: c.register?.name,
    branchAddress: c.address,
    headerNote: c.register?.receiptHeader,
    footerNote: c.register?.receiptFooter,
    title: 'Refund Receipt',
    titleAr: 'إيصال استرداد',
    receiptNumber: r.number,
    date: r.returnDate,
    status: 'Refunded',
    cashierName: r.cashierName,
    customerName: r.customerName,
    currency: c.currency,
    againstNumber: r.originalNumber,
    lines: (r.lines || []).map((l) => ({
      code: l.itemCode,
      description: l.description + (l.restock ? '' : ' (not restocked)'),
      quantity: l.quantity,
      unitPrice: l.unitPrice,
      discountPercent: l.discountPercent,
      lineTotal: l.lineTotal,
    })),
    subTotal: r.subTotal,
    taxTotal: r.taxTotal,
    totalAmount: r.totalAmount,
    payments: (r.refunds || []).map((f) => ({
      method: 'Refund: ' + displayLabel(f.method === 'account' ? 'onAccount' : f.method),
      amount: f.amount,
      reference: f.reference,
    })),
    referenceNo: r.creditNoteNumber ? `${r.number} / ${r.creditNoteNumber}` : undefined,
    notes: r.reason,
  };
}
