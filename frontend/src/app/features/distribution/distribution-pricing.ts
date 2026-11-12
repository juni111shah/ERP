import { CreateWholesalePriceInput, WholesalePriceMatrixDto, WholesaleTier } from '@core/api/distribution.api';

/** A catalog item a price rule can be set for. */
export interface PricingCatalogItem {
  id: string;
  name: string;
  sku: string;
  salesPrice?: number;
}

/** The price-rule form's raw value (`getRawValue()`, so the locked entity and item are included). */
export interface WholesaleRuleFormValue {
  legalEntityId: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  tier: WholesaleTier;
  minQuantity: number;
  maxQuantity: number | null;
  discountPercentage: number | null;
  wholesaleUnitPrice: number | null;
  isActive: boolean;
  notes: string;
}

/** The tag colour of each tier; the standard tier is blue. */
const TIER_COLORS: Partial<Record<WholesaleTier, string>> = { bronze: 'orange', silver: 'default', gold: 'gold', platinum: 'purple' };

export function tierColor(tier: WholesaleTier): string {
  return TIER_COLORS[tier] ?? 'blue';
}

/** The rule's quantity bracket in words ("10 to 50", "100 and up"). */
export function bracketLabel(row: WholesalePriceMatrixDto): string {
  return row.maxQuantity ? `${row.minQuantity} to ${row.maxQuantity}` : `${row.minQuantity} and up`;
}

/** Rounded to cents. */
export function roundMoney(value: number): number {
  return Math.round(value * 100) / 100;
}

/** The price-rule form's values for a new rule. */
export function emptyRuleForm(legalEntityId: string): WholesaleRuleFormValue {
  return {
    legalEntityId,
    itemId: '',
    itemCode: '',
    itemName: '',
    tier: 'standard',
    minQuantity: 1,
    maxQuantity: null,
    discountPercentage: 0,
    wholesaleUnitPrice: null,
    isActive: true,
    notes: '',
  };
}

/** The price-rule form's values for an existing rule. */
export function ruleFormOf(row: WholesalePriceMatrixDto): WholesaleRuleFormValue {
  return {
    legalEntityId: row.legalEntityId,
    itemId: row.itemId,
    itemCode: row.itemCode,
    itemName: row.itemName,
    tier: row.tier,
    minQuantity: row.minQuantity,
    maxQuantity: row.maxQuantity ?? null,
    discountPercentage: row.discountPercentage,
    wholesaleUnitPrice: row.wholesaleUnitPrice,
    isActive: row.isActive,
    notes: row.notes ?? '',
  };
}

/** The rule as the API takes it (the form only submits with a unit price). */
export function toWholesaleInput(val: WholesaleRuleFormValue): CreateWholesalePriceInput {
  return {
    legalEntityId: val.legalEntityId,
    itemId: val.itemId,
    itemCode: val.itemCode,
    itemName: val.itemName,
    tier: val.tier,
    minQuantity: val.minQuantity,
    maxQuantity: val.maxQuantity,
    discountPercentage: val.discountPercentage ?? 0,
    wholesaleUnitPrice: val.wholesaleUnitPrice as number,
    isActive: val.isActive,
    notes: val.notes,
  };
}
