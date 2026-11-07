import { PosCatalogItemDto, PosOrderDto, PosOrderLineDto, PosOrderLineInput, PosPaymentInput, PosPaymentMethod, PosRefundInput } from '../../core/api/pos.api';

/**
 * To cents, halves away from zero, as the server's decimal arithmetic does. Binary floats hold 14.50 x 15% as 2.17499..., so the
 * product is cleaned of that dust before rounding; otherwise the till asks for a cent less than the server charges and the payment is refused.
 */
export const round2 = (n: number): number => {
  const cents = Math.round(Number((Math.abs(n) * 100).toFixed(6)));
  return (n < 0 ? -cents : cents) / 100 || 0;
};

/** Quantities keep the four decimals the database holds. */
export const roundQty = (n: number): number => Math.round((n + Number.EPSILON) * 10000) / 10000;

export interface CartLine {
  /** Stable key on the ticket: the item id, or a generated one for a typed-in item. */
  key: string;
  itemId: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  /** The price the catalogue gave. A different unitPrice is an override the server checks. */
  listPrice: number | null;
  discountPercent: number;
  taxRate: number;
  onHand: number | null;
  /** Part of a promo code's discount shared onto this line by the server quote. It comes off before tax, after the manual discount. */
  promoDiscount?: number;
}

export interface LineAmounts {
  /** the line's share of a promo code discount */
  promo: number;
  gross: number;
  discount: number;
  net: number;
  tax: number;
  total: number;
}

/** Rounded exactly as the server does it, line by line, so the amount sent always equals the order total. */
export function lineAmounts(l: Pick<CartLine, 'quantity' | 'unitPrice' | 'discountPercent' | 'taxRate'> & { promoDiscount?: number }): LineAmounts {
  const gross = round2(l.quantity * l.unitPrice);
  const discount = round2(gross * (l.discountPercent / 100));
  const promo = round2(l.promoDiscount ?? 0);
  const net = round2(gross - discount - promo);
  const tax = round2(net * (l.taxRate / 100));
  return { gross, discount, promo, net, tax, total: round2(net + tax) };
}

export interface CartTotals {
  promo: number;
  gross: number;
  discount: number;
  net: number;
  tax: number;
  total: number;
  units: number;
}

export function cartTotals(lines: readonly CartLine[]): CartTotals {
  const t: CartTotals = { gross: 0, discount: 0, promo: 0, net: 0, tax: 0, total: 0, units: 0 };
  for (const l of lines) {
    const a = lineAmounts(l);
    t.gross += a.gross;
    t.discount += a.discount;
    t.promo += a.promo;
    t.net += a.net;
    t.tax += a.tax;
    t.units += l.quantity;
  }
  t.gross = round2(t.gross);
  t.discount = round2(t.discount);
  t.promo = round2(t.promo);
  t.net = round2(t.net);
  t.tax = round2(t.tax);
  t.total = round2(t.net + t.tax);
  t.units = round2(t.units);
  return t;
}

/** What the till sends. The price goes only when it differs from the catalogue, so the server prices everything else itself. */
export function toOrderLine(l: CartLine): PosOrderLineInput {
  if (!l.itemId) {
    return { itemId: null, itemCode: l.itemCode, description: l.description, quantity: l.quantity, unitPrice: l.unitPrice, discountPercent: l.discountPercent, taxRate: l.taxRate };
  }
  return { itemId: l.itemId, quantity: l.quantity, discountPercent: l.discountPercent, ...(l.unitPrice !== l.listPrice ? { unitPrice: l.unitPrice } : {}) };
}

/** A held ticket back on the till, at the prices it was held at. */
export function fromHeldOrder(order: PosOrderDto): CartLine[] {
  return order.lines.map((l) => ({
    key: l.itemId ?? `open-${l.id}`,
    itemId: l.itemId ?? null,
    itemCode: l.itemCode,
    description: l.description,
    quantity: l.quantity,
    unitPrice: l.unitPrice,
    listPrice: null, // not known until repriceHeld has looked it up; until then the held price is sent as it is
    discountPercent: l.discountPercent,
    taxRate: l.taxRate,
    onHand: null,
  }));
}

/**
 * Brings a held line up to today's catalogue. A price set by hand stays for someone who may set prices; for anyone else the
 * server would charge the catalogue price anyway, so the ticket shows that. An item no longer on sale stays as it is and the server explains at payment.
 */
export function repriceHeld(line: CartLine, item: PosCatalogItemDto | null, canOverridePrice: boolean): CartLine {
  if (!item) return line;
  return {
    ...line,
    listPrice: item.price,
    unitPrice: canOverridePrice || item.price === null ? line.unitPrice : item.price,
    taxRate: item.taxRate,
    onHand: item.onHand,
  };
}

/**
 * What a return of this quantity gives back: its running share of what the line charged, less what earlier returns took.
 * The same arithmetic as the server (ReturnFeatures), so the refund it asks for is always the one the server accepts.
 */
export function returnAmount(line: Pick<PosOrderLineDto, 'quantity' | 'unitPrice' | 'discountPercent' | 'taxRate' | 'lineTotal' | 'returnedQuantity' | 'returnedAmount'> & { promoDiscount?: number }, quantity: number): number {
  if (quantity <= 0) return 0;
  const net = lineAmounts(line).net;
  const tax = round2(line.lineTotal - net);
  const soFar = line.returnedQuantity + quantity;
  return round2(round2((net * soFar) / line.quantity) + round2((tax * soFar) / line.quantity) - line.returnedAmount);
}

/** Money goes back the way it came: what the chosen way cannot take goes the other way, so a sale paid part on account can be returned in full. */
export function splitRefund(total: number, method: PosPaymentMethod, moneyLeft: number, onAccountLeft: number, moneyMethod: PosPaymentMethod, reference: string | null): PosRefundInput[] {
  if (onAccountLeft <= 0) return [{ method, amount: total, reference }];
  const account = method === 'account' ? Math.min(total, onAccountLeft) : round2(total - Math.min(total, moneyLeft));
  const money = round2(total - account);
  const refunds: PosRefundInput[] = [];
  if (money > 0) refunds.push({ method: method === 'account' ? moneyMethod : method, amount: money, reference });
  if (account > 0) refunds.push({ method: 'account', amount: account, reference: null });
  return refunds;
}

export interface ScanInput {
  quantity: number;
  code: string;
}

/** "3*ABC" sells three of ABC. Anything else is one of what was typed or scanned. */
export function parseScan(text: string): ScanInput {
  const value = text.trim();
  const match = /^(\d+(?:\.\d+)?)\s*[*x×]\s*(.+)$/i.exec(value);
  if (match) {
    const quantity = roundQty(Number(match[1]));
    if (quantity > 0) return { quantity, code: match[2].trim() };
  }
  return { quantity: 1, code: value };
}

/** The next round amounts a customer is likely to hand over. */
export function quickCash(due: number): number[] {
  if (due <= 0) return [];
  const rounded = [5, 10, 50, 100, 500, 1000].map((step) => Math.ceil(due / step - 1e-9) * step).filter((n) => n > due);
  return [...new Set(rounded)].sort((a, b) => a - b).slice(0, 4);
}

/** One tender as the cashier entered it. Cash may be more than is due: the difference is change. */
export interface Tender {
  method: PosPaymentMethod;
  amount: number;
  reference?: string;
}

export function tenderedTotal(tenders: readonly Tender[]): number {
  return round2(tenders.reduce((sum, t) => sum + t.amount, 0));
}

export function remainingDue(total: number, tenders: readonly Tender[]): number {
  return Math.max(0, round2(total - tenderedTotal(tenders)));
}

/** Only cash can give change. A card or account amount above what is due is a mistake, not change. */
export function changeDue(total: number, tenders: readonly Tender[]): number {
  const over = round2(tenderedTotal(tenders) - total);
  if (over <= 0) return 0;
  const cash = tenders.filter((t) => t.method === 'cash').reduce((sum, t) => sum + t.amount, 0);
  return round2(Math.min(over, cash));
}

/** True when the tenders cover the total and nothing but cash goes over it. */
export function tendersSettle(total: number, tenders: readonly Tender[]): boolean {
  if (tenders.length === 0) return false;
  const over = round2(tenderedTotal(tenders) - total);
  return over >= 0 && over === changeDue(total, tenders);
}

/** Payments for the server. Each records the amount applied, so change comes off the cash and the sum equals the total. */
export function toPayments(total: number, tenders: readonly Tender[]): PosPaymentInput[] {
  let change = changeDue(total, tenders);
  return tenders.map((t): PosPaymentInput => {
    if (t.method !== 'cash') return { method: t.method, amount: t.amount, tenderedAmount: t.amount, changeAmount: 0, reference: t.reference || null };
    const given = Math.min(change, t.amount);
    change = round2(change - given);
    return { method: 'cash', amount: round2(t.amount - given), tenderedAmount: t.amount, changeAmount: given, reference: null };
  }).filter((p) => p.amount > 0);
}

/** A promo code the server accepted for the ticket as it was when asked: what to put on each line so the totals match its quote. */
export interface AppliedPromo {
  code: string;
  name: string | null;
  discount: number;
  /** the ticket the quote was for; when the ticket changes the quote is out of date */
  sig: string;
  lines: Pick<CartLine, 'unitPrice' | 'discountPercent' | 'taxRate' | 'promoDiscount'>[];
}

/** Identifies a ticket by what the server prices: item, quantity, price and manual discount of each line, in order. */
export function cartSignature(lines: readonly CartLine[]): string {
  return lines.map((l) => `${l.key}|${l.quantity}|${l.unitPrice}|${l.discountPercent}`).join(';');
}

export const TENDER_LABELS: Record<PosPaymentMethod, string> = {
  cash: 'Cash',
  card: 'Card',
  other: 'Other',
  account: 'On account',
  giftCard: 'Gift card',
};
