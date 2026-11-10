import { DashboardChart, KpiCard } from '@core/api/admin.api';
import { addDays, isRealDate, parseIso } from './dashboard-range';

/** The overview's figures in the user's language: dates, the period caption, KPI captions and chart axis labels. */

/** Latin digits in both languages (matches the rest of the app's figures). */
export const latn = (locale: string) => `${locale}-u-nu-latn`;

const FSI = '⁨', PDI = '⁩';
/** Bidi-isolates a date inside a translated sentence. */
export const isolate = (text: string) => FSI + text + PDI;

/** A yyyy-MM-dd date in `locale` ("30 Oct 2026" by default); anything else is shown as it is. */
export function formatIsoDate(iso: string, locale: string, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }): string {
  return isRealDate(iso) ? new Intl.DateTimeFormat(locale, options).format(parseIso(iso)) : iso;
}

/** One piece of the period caption; `bdi` pieces are dates. */
export interface CaptionPart { text: string; bdi: boolean; }

/** The `{from} – {to}` caption template split into text and (bidi-isolated) date pieces. */
export function captionParts(template: string, from: string, to: string, locale: string): CaptionPart[] {
  const values: Record<string, string> = { '{from}': formatIsoDate(from, locale), '{to}': formatIsoDate(to, locale) };
  return template.split(/(\{from\}|\{to\})/).filter((x) => x !== '')
    .map((x) => (x in values ? { text: values[x], bdi: true } : { text: x, bdi: false }));
}

/** A caption the API sends as a key with {name} args (e.g. the cash card's "at {date}"), filled in; dates in the user's locale. */
export function localizeKpi(k: KpiCard, locale: string, translate: (key: string) => string): KpiCard {
  if (!k.captionKey) return k;
  const template = translate(k.captionKey);
  if (!template) return k;
  const caption = template.replace(/\{(\w+)\}/g, (m: string, name: string) => {
    const v = k.captionArgs?.[name];
    if (v == null) return m;
    return isRealDate(v) ? isolate(formatIsoDate(v, locale)) : v;
  });
  return { ...k, caption };
}

/** Axis labels for time-series charts from their buckets: weeks by start day, months by name, quarters as "Nov – Jan 26". */
export function localizeChart(c: DashboardChart, locale: string): DashboardChart {
  if (!c.buckets?.length || c.buckets.length !== c.categories.length) return c;
  const categories = c.buckets.map((b) => {
    const from = parseIso(b.from), to = parseIso(b.to);
    if (c.granularity === 'week') return formatIsoDate(b.from, locale, { day: 'numeric', month: 'short' });
    if (c.granularity === 'quarter') {
      return `${new Intl.DateTimeFormat(locale, { month: 'short' }).format(addDays(from, 14))} – ${new Intl.DateTimeFormat(locale, { month: 'short', year: '2-digit' }).format(addDays(to, -14))}`;
    }
    const mid = addDays(from, Math.floor((to.getTime() - from.getTime()) / 86_400_000 / 2));
    return new Intl.DateTimeFormat(locale, { month: 'short', year: '2-digit' }).format(mid);
  });
  return { ...c, categories };
}

/** The browser's time zone as "GMT+04:00 (Asia/Dubai)". */
export function browserTimezone(): string {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const offsetMin = -new Date().getTimezoneOffset();
    const sign = offsetMin >= 0 ? '+' : '-';
    const hours = String(Math.floor(Math.abs(offsetMin) / 60)).padStart(2, '0');
    const mins = String(Math.abs(offsetMin) % 60).padStart(2, '0');
    return `GMT${sign}${hours}:${mins} (${tz.replace('_', ' ')})`;
  } catch {
    return 'GMT+03:00 (Standard Time)';
  }
}
