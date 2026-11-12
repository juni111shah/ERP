import { AbstractControl, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ItemDto, TaxCodeDto } from '@core/api/admin.api';
import { PricedLine } from '@shared/forms/line-totals';

/** A purchase-order line form row. */
export interface PurchaseLineValue extends PricedLine {
  itemId: string | null;
  itemCode: string;
  description: string;
  unitOfMeasure: string;
}

/** A requisition line form row: an estimate, no discount or tax. */
export interface RequisitionLineValue {
  itemId: string | null;
  itemCode: string;
  description: string;
  quantity: number;
  unitOfMeasure: string;
  estimatedUnitPrice: number;
}

/** A new, empty purchase-order line with the default tax rate. */
export function newPurchaseLine(fb: FormBuilder, taxRate: number): FormGroup {
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

/** Fills a purchase-order line from the picked item: code, name, unit, purchase price (else sales price) and the item's tax rate (else the default). */
export function fillPurchaseLineFromItem(row: AbstractControl, item: ItemDto, taxCodes: TaxCodeDto[], defaultTaxRate: number): void {
  let taxRate = defaultTaxRate;
  if (item.taxCodeId) {
    const code = taxCodes.find((t) => t.id === item.taxCodeId);
    if (code && typeof code.rate === 'number') taxRate = code.rate;
  }
  row.patchValue({
    itemCode: item.sku,
    description: item.name,
    unitOfMeasure: item.uom || 'EA',
    unitPrice: item.purchasePrice || item.salesPrice || 0,
    taxRate,
  });
}

/** A new, empty requisition line. */
export function newRequisitionLine(fb: FormBuilder): FormGroup {
  return fb.group({
    itemId: [null],
    itemCode: [''],
    description: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(0.01)]],
    unitOfMeasure: ['EA'],
    estimatedUnitPrice: [0, [Validators.required, Validators.min(0)]],
  });
}

/** Fills a requisition line from the picked item (its purchase price as the estimate); no item clears the code. */
export function fillRequisitionLineFromItem(line: AbstractControl, item: ItemDto | undefined): void {
  if (!item) {
    line.patchValue({ itemCode: '' });
    return;
  }
  line.patchValue({
    itemCode: item.sku,
    description: item.name,
    unitOfMeasure: item.uom || 'EA',
    estimatedUnitPrice: item.purchasePrice ?? line.value.estimatedUnitPrice ?? 0,
  });
}
