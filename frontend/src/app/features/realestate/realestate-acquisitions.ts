import { AcquisitionDto, AcquisitionInput } from '@core/api/realestate.api';
import { toIsoDate } from '@core/format/dates';

/** One funding source row of the new-acquisition form. */
export interface FundingFormValue { type: string; lender: string; principal: number; annualRatePercent: number; termMonths: number; }

/** The new-acquisition form's raw value (the required fields are filled once the form is valid). */
export interface AcquisitionFormValue {
  legalEntityId: string; propertyId: string; sellerName: string; purchaseDate: Date; contractReference: string; purchasePrice: number | null;
  downPayment: number | null; transferFees: number | null; otherCosts: number | null; payFromAccountId: string | null; notes: string; funding: FundingFormValue[];
}

/** The new draft as the API takes it: each funding source's first instalment falls due a month after the purchase. */
export function acquisitionInput(v: AcquisitionFormValue): AcquisitionInput {
  const firstDue = new Date(v.purchaseDate); firstDue.setMonth(firstDue.getMonth() + 1);
  return {
    legalEntityId: v.legalEntityId, propertyId: v.propertyId, sellerName: v.sellerName, purchaseDate: toIsoDate(v.purchaseDate), contractReference: v.contractReference || null, purchasePrice: v.purchasePrice,
    downPayment: v.downPayment ?? 0, transferFees: v.transferFees ?? 0, otherCosts: v.otherCosts ?? 0, payFromAccountId: v.payFromAccountId, notes: v.notes || null,
    funding: v.funding.map((f) => ({ ...f, firstDueDate: toIsoDate(firstDue) })),
  };
}

/** A saved draft sent back unchanged except for the account its cash is paid from. */
export function acquisitionWithBank(a: AcquisitionDto, payFromAccountId: string): AcquisitionInput {
  return {
    legalEntityId: a.legalEntityId, propertyId: a.propertyId, sellerName: a.sellerName, purchaseDate: a.purchaseDate, contractReference: a.contractReference ?? null, purchasePrice: a.purchasePrice, downPayment: a.downPayment, transferFees: a.transferFees, otherCosts: a.otherCosts, payFromAccountId, notes: a.notes ?? null,
    funding: a.funding.map((f) => ({ type: f.type, lender: f.lender, principal: f.principal, annualRatePercent: f.annualRatePercent, termMonths: f.termMonths, firstDueDate: f.firstDueDate })),
  };
}

/** What the price leaves unfunded: price - down payment - the funding sources' principal. */
export function unfundedAmount(v: { purchasePrice?: number | null; downPayment?: number | null; funding?: unknown[] }): number {
  const funding = ((v.funding ?? []) as { principal?: number | null }[]).reduce((s, f) => s + (f.principal ?? 0), 0);
  return (v.purchasePrice ?? 0) - (v.downPayment ?? 0) - funding;
}
