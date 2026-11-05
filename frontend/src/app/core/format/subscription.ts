import { SubscriptionSummary } from '../session/session.store';

/** Pure helpers for plans and subscriptions, kept free of Angular so they can be read and tested on their own. */

/** What paying annually saves against twelve months, or null when it saves nothing. */
export function yearlySaving(monthlyPrice: number, annualPrice: number): { amount: number; percent: number } | null {
  const fullYear = monthlyPrice * 12;
  const amount = Math.round((fullYear - annualPrice) * 100) / 100;
  if (fullYear <= 0 || amount <= 0) return null;
  return { amount, percent: Math.round((amount / fullYear) * 100) };
}

const STATUS_WORDS: Record<NonNullable<SubscriptionSummary['status']>, string> = {
  trialing: 'On trial', active: 'Active', pastDue: 'Payment overdue', expired: 'Trial ended', suspended: 'Suspended', cancelled: 'Cancelled',
};

/** A subscription's status in plain words. No status means no plan. */
export function statusWords(status: SubscriptionSummary['status'] | undefined): string {
  return status ? STATUS_WORDS[status] : 'No plan';
}

/** A workspace code from a business name: lower-case letters, digits and single dashes ("Acme & Sons Ltd." becomes "acme-sons-ltd"). */
export function suggestWorkspaceCode(name: string | null | undefined): string {
  const code = (name ?? '')
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
    .replace(/-+$/g, '');
  return code.length >= 2 ? code : '';
}

/** "Up to 5 users", "No limit on users". */
export function limitText(max: number | null | undefined, one: string, many: string): string {
  if (max === null || max === undefined) return `No limit on ${many}`;
  return max === 1 ? `1 ${one}` : `Up to ${max} ${many}`;
}

const RESTATES_LIMIT = /^(up to \d+|\d+|unlimited|no limit on) (users?|legal entit(y|ies))$/i;

/** A plan's selling points, without those that only repeat its limits, which the card already shows. */
export function sellingPoints(features: readonly string[]): string[] {
  return features.filter((f) => !RESTATES_LIMIT.test(f.trim()));
}

/** How full a limit is, 0 to 100. No limit reads as 0. */
export function usagePercent(used: number, max: number | null | undefined): number {
  if (!max || max <= 0) return 0;
  return Math.min(100, Math.round((used / max) * 100));
}

export interface SubscriptionBanner { tone: 'info' | 'warning' | 'error'; text: string; }

/** The notice at the top of the workspace, or null when there is nothing to say. A blocked workspace gets the full-page state instead. */
export function subscriptionBanner(s: SubscriptionSummary | null | undefined): SubscriptionBanner | null {
  if (!s || !s.hasSubscription || s.access === 'blocked') return null;
  if (s.access === 'readOnly') {
    const why = s.status === 'pastDue' ? 'A payment is overdue.' : s.status === 'trialing' || s.status === 'expired' ? 'Your trial has ended.' : 'Your subscription needs attention.';
    return { tone: 'error', text: `${why} Changes are paused and your data can still be viewed.` };
  }
  if (s.isComplimentary) return null;
  if (s.status === 'pastDue') return { tone: 'warning', text: 'A payment is overdue. Please settle it to avoid changes being paused.' };
  if (s.status === 'trialing' && s.trialDaysLeft !== null && s.trialDaysLeft <= 7) {
    const when = s.trialDaysLeft <= 0 ? 'today' : s.trialDaysLeft === 1 ? 'tomorrow' : `in ${s.trialDaysLeft} days`;
    return { tone: s.trialDaysLeft <= 3 ? 'warning' : 'info', text: `Your trial ends ${when}.` };
  }
  return null;
}

/** The platform's own team, in the platform's own workspace, starts on the console. Any other workspace keeps its usual home page. */
export function landsOnConsole(me: { platformAdmin?: boolean; isInternalWorkspace?: boolean } | null | undefined, stayInWorkspace: boolean): boolean {
  return !!me?.platformAdmin && !!me.isInternalWorkspace && !stayInWorkspace;
}

const BLOCKED_PATH = /^\/admin\/billing(\/|$|\?)/;

/** Blocked workspaces can still reach their billing page, so they can see why and put it right. */
export function blockedPageApplies(access: SubscriptionSummary['access'] | undefined, url: string): boolean {
  return access === 'blocked' && !BLOCKED_PATH.test(url);
}
