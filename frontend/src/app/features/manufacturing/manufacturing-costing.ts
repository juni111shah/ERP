/** How an actual cost compares with the standard: a dearer actual cost is unfavourable, a cheaper one favourable. */
export function varianceLabel(amount: number): 'On standard' | 'Over standard' | 'Under standard' {
  return amount === 0 ? 'On standard' : amount > 0 ? 'Over standard' : 'Under standard';
}

/** The status shown for a variance amount. */
export function varianceTone(amount: number): 'normal' | 'overbudget' | 'ontrack' {
  return amount === 0 ? 'normal' : amount > 0 ? 'overbudget' : 'ontrack';
}
