import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ItemDto, TaxCodeDto } from '@core/api/admin.api';

/** The priced part of a quote or sales-order line, as the line form holds it. */
export interface PricedLine {
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxRate: number;
}

/** A quote or sales-order line form row. */
export interface SalesLineValue extends PricedLine {
  itemId: string | null;
  itemCode: string;
  description: string;
  unitOfMeasure: string;
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

/** A new, empty line row with the default tax rate. */
export function newSalesLine(fb: FormBuilder, taxRate: number): FormGroup {
  return fb.group({
    itemId: [null],
    itemCode: [''],
    description: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(0.01)]],
    unitOfMeasure: ['EA'],
    unitPrice: [0, [Validators.required, Validators.min(0)]],
    discountPercent: [0, [Validators.min(0), Validators.max(100)]],
    taxRate: [taxRate, [Validators.min(0), Validators.max(100)]],
  });
}

/** Fills a line from the picked item: code, name, unit, sales price and the item's tax rate (else the default rate). */
export function fillLineFromItem(row: FormGroup, item: ItemDto, taxCodes: TaxCodeDto[], defaultTaxRate: number): void {
  let taxRate = defaultTaxRate;
  if (item.taxCodeId) {
    const code = taxCodes.find((t) => t.id === item.taxCodeId);
    if (code && typeof code.rate === 'number') taxRate = code.rate;
  }
  row.patchValue({
    itemCode: item.sku,
    description: item.name,
    unitOfMeasure: item.uom || 'EA',
    unitPrice: item.salesPrice ?? row.value.unitPrice ?? 0,
    taxRate,
  });
}

/** The lines as the API takes them. */
export function toLineInputs(lines: SalesLineValue[]) {
  return lines.map((l) => ({
    itemId: l.itemId || null,
    itemCode: l.itemCode,
    description: l.description,
    quantity: l.quantity,
    unitOfMeasure: l.unitOfMeasure || 'EA',
    unitPrice: l.unitPrice,
    discountPercent: l.discountPercent || 0,
    taxRate: l.taxRate || 0,
  }));
}
