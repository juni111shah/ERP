import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ConvertToBillInput, GoodsReceiptLineInput, PurchaseOrderDto, PurchaseOrderLineDto } from '@core/api/purchasing.api';

/** One row of the receive-goods form. */
export interface ReceiptLineValue extends GoodsReceiptLineInput {
  orderedQuantity: number;
  alreadyReceived: number;
}

/** The receive-goods form's raw value. */
export interface ReceiptFormValue {
  receivedBy: string;
  deliveryNoteNumber: string;
  warehouseId: string | null;
  lines: ReceiptLineValue[];
}

/** One row of the convert-to-bill dialog: what the order line holds and what is billed now, at which price. */
export interface BillDraftRow {
  lineId: string;
  description: string;
  ordered: number;
  received: number;
  billed: number;
  orderPrice: number;
  quantity: number;
  price: number;
}

/** Statuses from which an order can be billed. */
const BILLABLE_STATUSES: PurchaseOrderDto['status'][] = ['approved', 'partiallyReceived', 'received', 'billed'];

/** Something has been received and not all of it has gone back. */
export function hasReceived(order: PurchaseOrderDto | null): boolean {
  return (order?.lines ?? []).some((l) => l.receivedQuantity - (l.returnedQuantity ?? 0) > 0);
}

/** Share of the ordered quantity received so far, 0 to 100. */
export function receivingPercentage(order: PurchaseOrderDto | null): number {
  if (!order || order.lines.length === 0) return 0;
  const totalOrdered = order.lines.reduce((acc, l) => acc + l.quantity, 0);
  const totalReceived = order.lines.reduce((acc, l) => acc + l.receivedQuantity, 0);
  if (totalOrdered <= 0) return 0;
  return Math.min(100, Math.round((totalReceived / totalOrdered) * 100));
}

/** More can be billed: something is not yet billed and the order is past approval. */
export function canBill(order: PurchaseOrderDto | null): boolean {
  return !!order && BILLABLE_STATUSES.includes(order.status) && order.lines.some((l) => l.billedQuantity < l.quantity);
}

/** A receive-goods row for an order line, proposing what is still outstanding. */
export function newReceiptLine(fb: FormBuilder, line: PurchaseOrderLineDto): FormGroup {
  const remaining = Math.max(0, line.quantity - line.receivedQuantity);
  return fb.group({
    purchaseOrderLineId: [line.id],
    itemId: [line.itemId],
    itemCode: [line.itemCode],
    description: [line.description],
    orderedQuantity: [line.quantity],
    alreadyReceived: [line.receivedQuantity],
    quantityReceived: [remaining, [Validators.required, Validators.min(0)]],
    unitOfMeasure: [line.unitOfMeasure],
  });
}

/** The rows with a quantity, as the API takes them (it refuses a line with nothing received). */
export function toReceiptLineInputs(lines: ReceiptLineValue[]): GoodsReceiptLineInput[] {
  return lines
    .filter((l) => l.quantityReceived > 0)
    .map((l) => ({
      purchaseOrderLineId: l.purchaseOrderLineId,
      itemId: l.itemId,
      itemCode: l.itemCode,
      description: l.description,
      quantityReceived: l.quantityReceived,
      unitOfMeasure: l.unitOfMeasure,
    }));
}

/** The bill dialog's rows: what is billable is received and not yet billed, at the order price. */
export function toBillDraft(lines: PurchaseOrderLineDto[]): BillDraftRow[] {
  return lines.map((l) => ({
    lineId: l.id,
    description: l.description,
    ordered: l.quantity,
    received: l.receivedQuantity,
    billed: l.billedQuantity,
    orderPrice: l.unitPrice,
    // Nothing is offered for goods that have not arrived unless that is chosen in the dialog.
    quantity: Math.max(0, l.receivedQuantity - l.billedQuantity),
    price: l.unitPrice,
  }));
}

/** The convert-to-bill body: only lines with a quantity, only prices that differ from the order. */
export function toBillRequest(rows: BillDraftRow[], allowUnreceived: boolean, reference: string): ConvertToBillInput {
  const overrides: Record<string, number> = {};
  for (const r of rows) if (r.quantity > 0 && r.price !== r.orderPrice) overrides[r.lineId] = r.price;
  const body: ConvertToBillInput = { lines: rows.filter((r) => r.quantity > 0).map((r) => ({ lineId: r.lineId, quantity: r.quantity })), allowUnreceived };
  if (Object.keys(overrides).length) body.unitPriceOverrides = overrides;
  if (reference.trim()) body.vendorReference = reference.trim();
  return body;
}
