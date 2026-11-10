import { OrderDto, OrderFulfillmentLineInput, OrderLineDto } from '@core/api/sales.api';

/** A line of the record-delivery dialog: what was ordered, what was delivered before, and the quantity delivered now. */
export interface FulfillmentDraftLine {
  lineId: string;
  description: string;
  quantity: number;
  alreadyDelivered: number;
  deliveredQuantity: number;
}

/** The dialog's lines for an order, each pre-filled with the full ordered quantity. */
export function toFulfillmentDraft(lines: OrderLineDto[]): FulfillmentDraftLine[] {
  return lines.map((l) => ({
    lineId: l.id,
    description: l.description,
    quantity: l.quantity,
    alreadyDelivered: l.deliveredQuantity,
    deliveredQuantity: l.quantity,
  }));
}

/** The dialog's lines as the API takes them. */
export const toFulfillmentInputs = (draft: FulfillmentDraftLine[]): OrderFulfillmentLineInput[] =>
  draft.map((d) => ({ lineId: d.lineId, deliveredQuantity: d.deliveredQuantity }));

/** Something has been delivered and not all of it has come back. */
export const hasNetDelivery = (order: OrderDto | null): boolean =>
  (order?.lines ?? []).some((l) => l.deliveredQuantity - (l.returnedQuantity ?? 0) > 0);

/** How much of the ordered quantity has been delivered, as a whole percentage (at most 100). */
export function fulfilledPercent(order: OrderDto | null): number {
  if (!order || order.lines.length === 0) return 0;
  const totalOrdered = order.lines.reduce((acc, l) => acc + l.quantity, 0);
  const totalDelivered = order.lines.reduce((acc, l) => acc + l.deliveredQuantity, 0);
  if (totalOrdered <= 0) return 0;
  return Math.min(100, Math.round((totalDelivered / totalOrdered) * 100));
}
