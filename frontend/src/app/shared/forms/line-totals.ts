/** Totals of priced document lines (quotes, orders, bills...): net of discount, then tax on the net. */

/** The priced part of a document line. */
export interface PricedLine {
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
}

export interface DocumentTotals {
  subTotal: number;
  taxTotal: number;
  total: number;
}

/** Net of discount, before tax. */
function netOf(line: PricedLine): number {
  const gross = (line.quantity || 0) * (line.unitPrice || 0);
  const discount = gross * ((line.discountPercent || 0) / 100);
  return gross - discount;
}

/** One line: net of discount, plus tax. */
export function lineTotal(line: PricedLine): number {
  const net = netOf(line);
  return net + net * ((line.taxRate || 0) / 100);
}

/** All lines of a document. */
export function documentTotals(lines: PricedLine[]): DocumentTotals {
  let subTotal = 0;
  let taxTotal = 0;
  for (const line of lines) {
    const net = netOf(line);
    subTotal += net;
    taxTotal += net * ((line.taxRate || 0) / 100);
  }
  return { subTotal, taxTotal, total: subTotal + taxTotal };
}
