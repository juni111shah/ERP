/** How values from the API are shown to people: one place for labels, dates and status colours, across every page. */

export type StatusTone = 'success' | 'neutral' | 'warning' | 'error' | 'info' | 'orange' | 'plain';

const MACHINE_VALUE = /^[a-z]+(?:[A-Z][a-z0-9]*)*$|^[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+$|^[a-z]+(?:[_-][a-z0-9]+)+$/;

/** Turns a value the API sends ("closedWon", "partly_paid") into words ("Closed won"). Text already written for people is left alone. */
export function displayLabel(value: unknown): string {
  if (value === null || value === undefined) return '';
  const text = String(value);
  if (!MACHINE_VALUE.test(text)) return text;
  const words = text.replace(/[_-]+/g, ' ').replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase();
  return words[0].toUpperCase() + words.slice(1);
}

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const ISO_DATE_TIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/;
const pad = (n: number) => String(n).padStart(2, '0');

/** "2026-11-05" becomes "05 Nov 2026", and a timestamp also gets its time. Anything that is not an ISO date is returned as it came. */
export function displayDate(value: unknown): string {
  if (value === null || value === undefined || value === '') return '';
  const text = String(value);
  const lang = typeof document !== 'undefined' ? document.documentElement.lang || 'en' : 'en';
  // Western digits for financial data in every language (design system, RTL section).
  const locale = lang.startsWith('en') ? 'en-US' : `${lang}-u-nu-latn`;
  const date = { day: '2-digit', month: 'short', year: 'numeric' } as const;
  // dd MMM yyyy whatever order the locale prefers.
  const write = (d: Date, timeZone?: string) => {
    const parts = new Intl.DateTimeFormat(locale, { ...date, timeZone }).formatToParts(d);
    const part = (type: string) => parts.find((p) => p.type === type)?.value ?? '';
    return `${part('day')} ${part('month')} ${part('year')}`;
  };
  if (ISO_DATE.test(text)) {
    // A date without a time is the same calendar day everywhere, so it must not shift with the time zone.
    return write(new Date(`${text}T00:00:00Z`), 'UTC');
  }
  if (ISO_DATE_TIME.test(text)) {
    const d = new Date(text);
    if (Number.isNaN(d.getTime())) return text;
    const day = write(d);
    // A timestamp at exactly midnight is a date the server stored as a time.
    return d.getHours() === 0 && d.getMinutes() === 0 ? day : `${day}, ${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
  return text;
}

/** "USD 1,234.50": an amount with its currency from the API, always two decimals. */
export function displayMoney(value: number | null | undefined, currency?: string | null): string {
  const amount = (value ?? 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return currency ? `${currency} ${amount}` : amount;
}

export function isIsoDate(value: unknown): boolean {
  return typeof value === 'string' && (ISO_DATE.test(value) || ISO_DATE_TIME.test(value));
}

const TONES: Record<string, StatusTone> = {};
const tone = (t: StatusTone, ...names: string[]) => names.forEach((n) => { TONES[n] = t; });
// Meaning of each tone: docs/team/uiux-advanced/design-system.md, status-tag map.
tone('success', 'active', 'approved', 'completed', 'done', 'paid', 'posted', 'delivered', 'accepted', 'closedwon', 'won', 'received', 'achieved', 'converted', 'cleared', 'matched', 'published', 'present', 'operational', 'qualified', 'hired', 'occupied', 'settled', 'closed', 'ontrack', 'yes', 'provisioned', 'full');
tone('info', 'confirmed', 'sent', 'submitted', 'issued', 'processing', 'inprogress', 'released', 'open', 'contacted', 'proposal', 'negotiation', 'qualification', 'scheduled', 'planned', 'readytoinvoice', 'invoiced', 'billed', 'review', 'new', 'normal', 'reserved', 'ordered', 'trialing');
tone('warning', 'draft', 'pending', 'pendingapproval', 'partlypaid', 'partiallypaid', 'partiallyreceived', 'partial', 'onhold', 'paused', 'parked', 'atrisk', 'holdingover', 'todo', 'expiring', 'medium', 'pastdue', 'expired', 'pendingemail', 'pendingreview', 'readonly');
tone('orange', 'high', 'overbudget', 'expiringsoon', 'reversed');
tone('error', 'overdue', 'cancelled', 'canceled', 'closedlost', 'lost', 'void', 'voided', 'rejected', 'declined', 'terminated', 'failed', 'offtrack', 'critical', 'urgent', 'unqualified', 'refunded', 'bounced', 'absent', 'no', 'suspended', 'blocked');
tone('plain', 'inactive', 'low', 'archived', 'unavailable', 'vacant', 'writtenoff');

/** The colour family for a status. Unknown values are neutral, so a new status still reads as a plain tag. */
export function statusTone(value: unknown): StatusTone {
  return TONES[String(value ?? '').replace(/[\s_-]+/g, '').toLowerCase()] ?? 'neutral';
}
