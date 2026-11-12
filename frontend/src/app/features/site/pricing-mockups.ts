/** Helpers of the pricing page's drawings of the app: the phone screens, the carousel and sample month labels. */

/** A screen of the mobile app a phone drawing can show. */
export type PhoneScreen = 'overview' | 'invoices' | 'hr' | 'approvals' | 'alerts';

/** Where a phone sits in the carousel: 0 is at the front, then +1, -1 and so on, always the short way round. */
export function carouselOffset(index: number, active: number, count: number): number {
  let offset = (index - active) % count;
  if (offset > count / 2) offset -= count;
  if (offset < -count / 2) offset += count;
  return offset;
}

/** The last few months, oldest first, as the app's charts label them. */
export function lastMonths(count: number, withYear = false, now = new Date()): string[] {
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - (count - i), 1);
    const name = d.toLocaleString('en-US', { month: 'short' });
    return withYear ? `${name} ${String(d.getFullYear()).slice(2)}` : name;
  });
}
