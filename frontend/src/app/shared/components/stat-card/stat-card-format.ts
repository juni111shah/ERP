/** The colours, number text and count-up maths of <erp-stat-card>. */

export type StatFormat = 'money' | 'number' | 'percent' | 'text';
export type StatTone = number | 'danger' | 'success' | 'warning' | 'neutral';
/** A card's stripe colour and value colour. */
export interface StatColors { stripe: string; text: string; }

/** Palette of the Overview "Executive Highlights" cards: stripe colour + value colour, picked by index. */
export const STAT_PALETTES: StatColors[] = [
  { stripe: '#10955d', text: 'var(--erp-color-stat-text-1)' }, // Emerald brand green
  { stripe: '#2563eb', text: 'var(--erp-color-stat-text-2)' }, // Indigo blue
  { stripe: '#0ea5e9', text: 'var(--erp-color-stat-text-3)' }, // Cyan
  { stripe: '#f59e0b', text: 'var(--erp-color-stat-text-4)' }, // Amber
  { stripe: '#8b5cf6', text: 'var(--erp-color-stat-text-5)' }, // Purple
  { stripe: '#ec4899', text: 'var(--erp-color-stat-text-6)' }, // Rose
];
const NAMED: Record<string, StatColors> = {
  danger: { stripe: '#ef4444', text: 'var(--erp-color-error-text)' },
  success: { stripe: '#10955d', text: 'var(--erp-color-stat-text-1)' },
  warning: { stripe: '#f59e0b', text: 'var(--erp-color-stat-text-4)' },
  neutral: { stripe: '#94a3b8', text: 'var(--erp-color-text-primary, #0f172a)' },
};

/** The colours of a tone: a palette entry by index (wrapping, negatives too) or a named tone. */
export function statColors(tone: StatTone): StatColors {
  return typeof tone === 'number' ? STAT_PALETTES[((tone % STAT_PALETTES.length) + STAT_PALETTES.length) % STAT_PALETTES.length] : NAMED[tone];
}

/** The count-up curve: fast at first, settling at the end. */
export const easeOutCubic = (t: number) => 1 - Math.pow(1 - t, 3);

/**
 * Interpolates all numbers inside a formatted string from 0 to target based on progress.
 * E.g. "AED 1,234.50" with progress 0.5 -> "AED 617.25"
 * E.g. "AED 1,200.00 · USD 500.00" -> "AED 600.00 · USD 250.00"
 */
export function formatAnimatedString(template: string, progress: number): string {
  if (progress >= 1) return template;
  return template.replace(/-?\d[\d,]*(\.\d+)?/g, (match) => {
    const target = Number(match.replace(/,/g, ''));
    if (isNaN(target) || target === 0) return match;

    const current = target * progress;
    const hasCommas = match.includes(',');
    const decIndex = match.indexOf('.');
    const decimals = decIndex !== -1 ? match.length - decIndex - 1 : 0;

    const formatted = decimals > 0
      ? current.toLocaleString('en-US', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
      : Math.round(current).toLocaleString('en-US');
    return hasCommas ? formatted : formatted.replace(/,/g, '');
  });
}
