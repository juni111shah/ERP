import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ItemDto, TaxCodeDto } from '@core/api/admin.api';
import { PricedLine } from '@shared/forms/line-totals';

/** A quote or sales-order line form row. */
export interface SalesLineValue extends PricedLine {
  itemId: string | null;
  itemCode: string;
  description: string;
  unitOfMeasure: string;
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
