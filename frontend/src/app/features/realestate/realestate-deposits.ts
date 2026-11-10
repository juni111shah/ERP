import { DepositAllocation, DepositSettlementInput } from '@core/api/realestate.api';

/**
 * Spreads `amount` of unpaid rent over the tenant's open invoices, oldest first. `shortfall` is what the invoices could not
 * take (more was entered than is open).
 */
export function allocateArrears(open: { id: string; outstanding: number }[], amount: number): { allocations: DepositAllocation[]; shortfall: number } {
  let left = amount;
  const allocations: DepositAllocation[] = [];
  for (const i of open) {
    if (left <= 0) break;
    const take = Math.min(left, i.outstanding);
    allocations.push({ invoiceId: i.id, amount: take });
    left -= take;
  }
  return { allocations, shortfall: left };
}

/** The settle drawer's values as the API takes them, with the unpaid rent already spread over invoices. */
export function depositSettlement(v: Record<string, unknown>, arrears: DepositAllocation[]): DepositSettlementInput {
  return {
    date: v['date'] as string, damages: (v['damages'] as number | undefined) ?? 0, damagesDescription: (v['damagesDescription'] as string | undefined) ?? null, arrears,
    refund: (v['refund'] as number | undefined) ?? 0, refundBankAccountId: (v['refundBankAccountId'] as string | undefined) ?? null,
  };
}
