import { Injectable, computed, effect, inject } from '@angular/core';
import { NzI18nService, ar_EG, en_US } from 'ng-zorro-antd/i18n';
import { TenantThemeService } from '../../../theme/tenant-theme.service';
import { DICTIONARIES, Lang } from './dictionaries';

/**
 * UI language. The language itself (persisted in localStorage, with try/catch) and the resulting `<html lang dir>` belong to
 * TenantThemeService; this service adds the dictionaries, `t()` and the matching ng-zorro locale.
 * `t(key, fallback)` returns the translation, else the English fallback, else the key.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly theme = inject(TenantThemeService);
  private readonly nz = inject(NzI18nService, { optional: true });

  readonly lang = computed<Lang>(() => (this.theme.lang().toLowerCase().startsWith('ar') ? 'ar' : 'en'));
  readonly dir = this.theme.dir;
  /** BCP-47 locale for number/date formatting. */
  readonly locale = computed(() => (this.lang() === 'ar' ? 'ar-AE' : 'en-US'));

  constructor() {
    effect(() => this.nz?.setLocale(this.lang() === 'ar' ? ar_EG : en_US));
  }

  t(key: string, fallback?: string): string { return DICTIONARIES[this.lang()][key] ?? fallback ?? key; }

  setLang(lang: Lang): void { this.theme.setLanguage(lang); }

  toggle(): void { this.setLang(this.lang() === 'ar' ? 'en' : 'ar'); }

  /** Formats a number for the active language (grouping and digits). */
  number(value: number, options?: Intl.NumberFormatOptions): string { return new Intl.NumberFormat(this.locale(), options).format(value); }
}
