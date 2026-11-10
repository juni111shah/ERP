import { PosCatalogItemDto, PosOrderDto, PosQuoteDto, PosTerminalSettingsDto } from '@core/api/pos.api';
import { AppliedPromo, CartLine, cartSignature, roundQty } from './pos-cart';

/** Ticket edits on the till as plain functions: each takes the lines and returns new ones, so the page only sets the result. */

/** The name on a ticket that has no customer. */
export const WALK_IN = 'Walk-in Customer';

/** A catalogue item that has a sales price, so it can go on the ticket. */
export type PricedCatalogItem = PosCatalogItemDto & { price: number };

/** True when the item has a sales price. */
export function hasPrice(item: PosCatalogItemDto): item is PricedCatalogItem {
  return item.price !== null;
}

/** What the cashier changes in the line dialog. */
export interface LineEdit {
  quantity: number;
  discountPercent: number;
  unitPrice: number;
}

/** A catalogue item as a new ticket line, at the catalogue price. */
export function catalogLine(item: PricedCatalogItem, quantity: number): CartLine {
  return {
    key: item.id,
    itemId: item.id,
    itemCode: item.sku,
    description: item.name,
    quantity,
    unitPrice: item.price,
    listPrice: item.price,
    discountPercent: 0,
    taxRate: item.taxRate,
    onHand: item.onHand,
  };
}

/** Adds an item: one more on its line when it is already on the ticket, else a new line. `at` is the line that changed. */
export function addToTicket(lines: readonly CartLine[], item: PricedCatalogItem, quantity: number): { lines: CartLine[]; at: number } {
  const next = [...lines];
  const at = next.findIndex((l) => l.itemId === item.id);
  if (at >= 0) {
    next[at] = { ...next[at], quantity: roundQty(next[at].quantity + quantity), onHand: item.onHand };
    return { lines: next, at };
  }
  next.push(catalogLine(item, quantity));
  return { lines: next, at: next.length - 1 };
}

/** Something sold that is not in the catalogue: one of it, at the price typed. */
export function openItemLine(name: string, unitPrice: number, taxRate: number): CartLine {
  return { key: `open-${crypto.randomUUID()}`, itemId: null, itemCode: 'OTHER', description: name, quantity: 1, unitPrice, listPrice: null, discountPercent: 0, taxRate, onHand: null };
}

/** The ticket with a new quantity on one line. */
export function withQuantity(lines: readonly CartLine[], index: number, quantity: number): CartLine[] {
  const next = [...lines];
  next[index] = { ...next[index], quantity: roundQty(quantity) };
  return next;
}

/** The ticket with the line dialog applied; the price changes only for someone who may override it. */
export function withLineEdit(lines: readonly CartLine[], index: number, edit: LineEdit, canOverridePrice: boolean): CartLine[] {
  const next = [...lines];
  const line = next[index];
  next[index] = { ...line, quantity: roundQty(edit.quantity), discountPercent: edit.discountPercent, unitPrice: canOverridePrice ? edit.unitPrice : line.unitPrice };
  return next;
}

/** The same percent off every line. */
export function withDiscount(lines: readonly CartLine[], percent: number): CartLine[] {
  return lines.map((l) => ({ ...l, discountPercent: percent }));
}

/** The discount every line shares, or 0 when they differ: what the whole-ticket discount dialog starts from. */
export function commonDiscount(lines: readonly CartLine[]): number {
  const percents = new Set(lines.map((l) => l.discountPercent));
  return percents.size === 1 ? [...percents][0] : 0;
}

/** The ticket as the server prices it with the promo applied, while the promo still matches the ticket. */
export function pricedLines(lines: CartLine[], promo: AppliedPromo | null): CartLine[] {
  if (!promo || promo.sig !== cartSignature(lines)) return lines;
  return lines.map((l, i) => ({ ...l, ...(promo.lines[i] ?? {}) }));
}

/** The promo from a server quote for the ticket with this signature, or null when the code gives no discount. */
export function promoFromQuote(quote: PosQuoteDto, signature: string): AppliedPromo | null {
  if (!quote.promoCode || !(quote.promoDiscount > 0)) return null;
  return {
    code: quote.promoCode,
    name: quote.promoName,
    discount: quote.promoDiscount,
    sig: signature,
    lines: quote.lines.map((l) => ({ unitPrice: l.unitPrice, discountPercent: l.discountPercent, taxRate: l.taxRate, promoDiscount: l.promoDiscount })),
  };
}

/** The highest discount this cashier may give: the cashier limit (unless they may override it) within the register maximum. */
export function discountLimit(settings: PosTerminalSettingsDto | null, mayOverride: boolean): number {
  if (!settings) return 100;
  const cashier = settings.cashierDiscountPercent;
  return cashier !== null && cashier !== undefined && !mayOverride ? Math.min(cashier, settings.maxDiscountPercent) : settings.maxDiscountPercent;
}

/** An item with no stock left that the register will not sell below zero. */
export function outOfStockBlocked(settings: PosTerminalSettingsDto | null, item: PosCatalogItemDto): boolean {
  return settings?.allowNegativeStock === false && item.onHand !== null && item.onHand <= 0;
}

/** A held sale in one line: "2 × Coffee, 1 × Cake". */
export function heldSummary(order: PosOrderDto): string {
  return order.lines.map((l) => `${l.quantity} × ${l.description}`).join(', ');
}
