import { PosOrderDto } from '@core/api/pos.api';
import { PrintHeader, PrintPosReceiptData, PrintPosReturnData, PrintReceiptData } from '@core/api/print.api';
import { ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';
import { ReceiptContext, returnReceipt, saleReceipt } from '@features/pos/pos-receipt';

/** The print page's thermal slips: POS sales and returns, and a customer receipt printed as a cash voucher. */

/** The business, register wording, address and currency printed on a POS slip. */
function slipContext(header: PrintHeader, register: ReceiptContext['register']): ReceiptContext {
  return {
    business: header.organizationName || undefined,
    register,
    address: header.legalEntityAddress || undefined,
    currency: header.baseCurrency || '',
  };
}

/** A POS sale's receipt. */
export const posSaleSlip = (pr: PrintPosReceiptData): ThermalReceiptData =>
  saleReceipt(pr.posOrder as unknown as PosOrderDto, slipContext(pr.header, pr.register));

/** A POS return's refund slip. */
export const posReturnSlip = (pr: PrintPosReturnData): ThermalReceiptData => returnReceipt(pr.posReturn, slipContext(pr.header, pr.register));

/** A customer receipt as a cash receipt voucher slip. */
export function cashReceiptSlip(r: PrintReceiptData): ThermalReceiptData {
  const h = r.header;
  return {
    organizationName: h.organizationName || 'HigherSquare 360 ERP',
    organizationNameAr: 'هاير سكوير 360',
    branchName: h.legalEntityName || 'Main Cash Counter',
    branchAddress: h.legalEntityAddress || undefined,
    title: 'Cash Receipt Voucher',
    titleAr: 'سند استلام نقدي',
    receiptNumber: r.receipt.number,
    date: r.receipt.date,
    status: r.receipt.status || 'Successful',
    customerName: r.partyName || 'Customer',
    referenceNo: r.receipt.reference || r.receipt.number,
    currency: r.receipt.currencyCode || h.baseCurrency,
    subTotal: r.receipt.amount,
    totalAmount: r.receipt.amount,
    payments: [{
      method: r.receipt.method || 'cash',
      amount: r.receipt.amount,
    }],
    notes: r.receipt.reference,
  };
}
