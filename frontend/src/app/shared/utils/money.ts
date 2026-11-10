/** "AED 1,234.50": two decimals, Western digits, the currency code in front when there is one. */
export function formatMoney(v: number | null | undefined, currency?: string | null): string {
  const n = (v ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return currency ? `${currency} ${n}` : n;
}
