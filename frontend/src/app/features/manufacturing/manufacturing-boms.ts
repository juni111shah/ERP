import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { BomComponentDto, BomComponentInput, BomDto, BomInput } from '@core/api/manufacturing.api';

/** Legal entity, code and product cannot change on an existing BOM, so they are not validated when editing. */
export const BOM_CREATE_ONLY_FIELDS = ['legalEntityId', 'code', 'producedItemId'];

/** The bill-of-materials form's raw value (`getRawValue()`, so the locked create-only fields are included). */
export interface BomFormValue {
  legalEntityId: string;
  code: string;
  producedItemId: string | null;
  producedItemCode: string;
  producedItemName: string;
  name: string;
  outputQuantity: number;
  uom: string;
  version: string;
  laborCostPerUnit: number | null;
  overheadCostPerUnit: number | null;
  isActive: boolean;
  description: string;
  components: BomComponentInput[];
}

/** A component row, empty or filled from an existing component. */
export function bomComponentGroup(fb: FormBuilder, c?: Partial<BomComponentDto>): FormGroup {
  return fb.group({
    componentItemId: [c?.componentItemId ?? null, Validators.required],
    componentItemCode: [c?.componentItemCode ?? ''],
    componentItemName: [c?.componentItemName ?? ''],
    quantityRequired: [c?.quantityRequired ?? 1, [Validators.required, Validators.min(0.0001)]],
    uom: [c?.uom ?? 'PCS'],
    scrapPercentage: [c?.scrapPercentage ?? 0, [Validators.min(0), Validators.max(100)]],
    unitCost: [c?.unitCost ?? 0, [Validators.required, Validators.min(0)]],
    notes: [c?.notes ?? null],
  });
}

/** The header fields of a new BOM (the components are added separately). */
export function emptyBomForm(legalEntityId: string): Omit<BomFormValue, 'components'> {
  return {
    legalEntityId,
    code: '',
    producedItemId: null,
    producedItemCode: '',
    producedItemName: '',
    name: '',
    outputQuantity: 1,
    uom: 'PCS',
    version: '1.0',
    laborCostPerUnit: 0,
    overheadCostPerUnit: 0,
    isActive: true,
    description: '',
  };
}

/** The editable header fields of an existing BOM. */
export function bomEditValues(bom: BomDto): Partial<BomFormValue> {
  return {
    name: bom.name,
    outputQuantity: bom.outputQuantity,
    uom: bom.uom,
    version: bom.version,
    laborCostPerUnit: bom.laborCostPerUnit,
    overheadCostPerUnit: bom.overheadCostPerUnit,
    isActive: bom.isActive,
    description: bom.description ?? '',
  };
}

/** The BOM as the API takes it; an edit keeps the record's custom fields. */
export function toBomInput(v: BomFormValue, editing: BomDto | null): BomInput {
  return {
    legalEntityId: v.legalEntityId,
    code: v.code,
    name: v.name,
    producedItemId: v.producedItemId as string,
    producedItemCode: v.producedItemCode,
    producedItemName: v.producedItemName,
    outputQuantity: v.outputQuantity,
    uom: v.uom,
    version: v.version,
    laborCostPerUnit: v.laborCostPerUnit ?? 0,
    overheadCostPerUnit: v.overheadCostPerUnit ?? 0,
    isActive: v.isActive,
    description: v.description || null,
    components: v.components.map((c) => ({ ...c, scrapPercentage: c.scrapPercentage ?? 0 })),
    customFields: editing?.customFields ?? null,
  };
}

/** Marks every leaf control of the form, nested component rows included, dirty and re-validates it on its own. */
export function revealLeafErrors(group: FormGroup | FormArray): void {
  Object.values(group.controls).forEach((c) => {
    if (c instanceof FormGroup || c instanceof FormArray) revealLeafErrors(c);
    else { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); }
  });
}
