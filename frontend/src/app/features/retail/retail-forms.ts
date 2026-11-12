import { AbstractControl, FormGroup, ValidationErrors, ValidatorFn } from '@angular/forms';
import { toIsoDate } from '@core/format/dates';
import { PriceListType, PromotionType, RetailPriceListItemInput } from '@core/api/retail.api';

/** Form helpers shared by the retail stores, price lists and promotions drawers. */

/** The store form's raw value (`form.getRawValue()`, so the disabled legal entity is included). */
export interface StoreFormValue {
  legalEntityId: string;
  code: string;
  name: string;
  warehouseId: string | null;
  storeManagerName: string;
  phone: string;
  email: string;
  address: string;
  isActive: boolean;
}

/** The price list form's raw value. */
export interface PriceListFormValue {
  legalEntityId: string;
  code: string;
  name: string;
  type: PriceListType;
  currency: string;
  validFrom: Date | null;
  validTo: Date | null;
  isDefault: boolean;
  isActive: boolean;
  description: string;
  items: RetailPriceListItemInput[];
}

/** The promotion form's raw value. */
export interface PromotionFormValue {
  legalEntityId: string;
  code: string;
  name: string;
  type: PromotionType;
  discountValue: number;
  minimumOrderAmount: number | null;
  startDate: Date;
  endDate: Date;
  usageLimit: number | null;
  isActive: boolean;
  description: string;
}

/** The end date field must not fall before the start date field of the same form. */
export const notBeforeStart = (startKey: string): ValidatorFn => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

/** Shows the errors of the invalid top-level controls only: each is marked dirty and re-validated on its own. */
export function revealInvalid(form: FormGroup): void {
  Object.values(form.controls).forEach((c) => {
    if (c.invalid) {
      c.markAsDirty();
      c.updateValueAndValidity({ onlySelf: true });
    }
  });
}

/** A "yyyy-MM-dd" from the API as that day's local midnight, for a date picker. */
export function localDay(isoDate: string): Date {
  return new Date(`${isoDate}T00:00:00`);
}

/** The entity chosen in the header when it is one of the list, else the first one, else none. */
export function defaultEntityId(entities: { id: string }[], currentId: string | null | undefined): string {
  return entities.find((e) => e.id === currentId)?.id || entities[0]?.id || '';
}
