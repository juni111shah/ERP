import { DICTIONARIES, Lang } from './dictionaries';
import { PHRASES_AR } from './phrases.ar';

/**
 * Translation for plain functions and shared classes that run outside an injection context (validation helpers, report
 * definitions, MasterDataState). The language is read from `<html lang>`, which TenantThemeService keeps in sync, so the
 * result matches I18nService.t() at the moment of the call. English output is the fallback, unchanged.
 */
export function currentLang(): Lang {
  const lang = typeof document !== 'undefined' ? document.documentElement.getAttribute('lang') ?? '' : '';
  return lang.toLowerCase().startsWith('ar') ? 'ar' : 'en';
}

/** Dictionary lookup with `{name}` placeholders, e.g. tNow('ui.valid.max', 'Must be ≤ {n}', { n: 5 }). */
export function tNow(key: string, fallback: string, params?: Record<string, string | number>): string {
  let text = DICTIONARIES[currentLang()][key] ?? fallback;
  for (const [name, value] of Object.entries(params ?? {})) text = text.split(`{${name}}`).join(String(value));
  return text;
}

/** An English UI phrase (an entity or source label) in the active language; unknown phrases come back unchanged. */
export function phraseNow(english: string): string {
  return currentLang() === 'ar' ? PHRASES_AR[english] ?? english : english;
}

const CREDIT_NOTE_FOR = /^Credit note for invoice (.+)$/;

/**
 * Display text for a stored note. The default credit-note note is saved in English ("Credit note for invoice INV-1"), so the
 * stored data stays the same for every user; in Arabic it is shown translated. Any other note is shown as typed.
 */
export function displayNote(note: string | null | undefined, lang: Lang = currentLang()): string {
  const text = note ?? '';
  const m = lang === 'ar' ? CREDIT_NOTE_FOR.exec(text.trim()) : null;
  return m ? (DICTIONARIES.ar['ui.cn.noteForInvoice'] ?? 'Credit note for invoice {n}').split('{n}').join(m[1]) : text;
}
