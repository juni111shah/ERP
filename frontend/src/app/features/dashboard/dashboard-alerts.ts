import { DashboardAlert } from '@core/api/admin.api';
import { DomainCategory } from './dashboard-categories';

/** The overview's alerts: their wording, urgency tag and which domain tab shows them. */

export interface UrgencyTag { label: string; tone: string; }

const URGENCY: Record<string, UrgencyTag> = {
  action: { label: 'Action needed', tone: 'error' },
  upcoming: { label: 'Upcoming', tone: 'warning' },
  info: { label: 'For information', tone: 'info' },
};

/** The tag of an alert's urgency; unknown ones are "For information". */
export const urgencyTag = (u: string): UrgencyTag => URGENCY[u] ?? URGENCY['info'];

/** Words in an alert's category that place it in a domain tab. */
const ALERT_WORDS: Partial<Record<DomainCategory, string[]>> = {
  'real-estate': ['lease', 'maintenance', 'unit', 'property'],
  finance: ['cheque', 'bill', 'invoice', 'payment'],
  'sales-crm': ['sales', 'lead', 'deal', 'order'],
  'supply-operations': ['inventory', 'stock', 'purchase', 'production'],
  'projects-services': ['project', 'task', 'service'],
  hr: ['hr', 'payroll', 'employee'],
};

/** The alerts a domain tab shows; `all` shows every alert. */
export function alertsFor(alerts: DashboardAlert[], cat: DomainCategory): DashboardAlert[] {
  if (cat === 'all') return alerts;
  const words = ALERT_WORDS[cat];
  return alerts.filter((a) => {
    if (!words) return true;
    const c = a.category.toLowerCase();
    return words.some((w) => c.includes(w));
  });
}

/**
 * Alerts arrive from the API as "1 employee document(s) have expired." This writes them as a person would:
 * "1 employee document has expired." and "3 employee documents have expired."
 */
export function readableAlert(message: string): string {
  const match = /(\d[\d,]*)\s+[^()]*?\(s\)/.exec(message);
  if (!match) return message;
  const end = match.index + match[0].length;
  if (match[1] !== '1') return message.replace('(s)', 's');
  const singular: Record<string, string> = { have: 'has', are: 'is', were: 'was', expire: 'expires', need: 'needs', require: 'requires' };
  const rest = message.slice(end).replace(/\b(have|are|were|expire|need|require)\b/, (verb) => singular[verb]);
  return message.slice(0, end).replace('(s)', '') + rest;
}
