import { Directionality } from '@angular/cdk/bidi';
import { Injectable, computed, effect, inject } from '@angular/core';
import { NzI18nService, ar_EG, en_US } from 'ng-zorro-antd/i18n';
import { TenantThemeService } from '../../../theme/tenant-theme.service';
import { DICTIONARIES, Lang } from './dictionaries';
import { DomTranslator } from './dom-translator';

/**
 * UI language. The language itself (persisted in localStorage, with try/catch) and the resulting `<html lang dir>` belong to
 * TenantThemeService; this service adds the dictionaries, `t()` and the matching ng-zorro locale.
 * `t(key, fallback)` returns the translation, else the English fallback, else the key.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly theme = inject(TenantThemeService);
  private readonly nz = inject(NzI18nService, { optional: true });
  /** Root CDK direction: read once at startup, so overlays (modals, messages, notifications) and pages outside the shell stayed LTR after a switch. */
  private readonly rootDir = inject(Directionality, { optional: true });

  readonly lang = computed<Lang>(() => (this.theme.lang().toLowerCase().startsWith('ar') ? 'ar' : 'en'));
  readonly dir = this.theme.dir;
  /** BCP-47 locale for number/date formatting. */
  readonly locale = computed(() => (this.lang() === 'ar' ? 'ar-AE' : 'en-US'));

  private readonly dom = new DomTranslator();

  constructor() {
    effect(() => this.nz?.setLocale(this.lang() === 'ar' ? ar_EG : en_US));
    effect(() => {
      const dir = this.dir();
      if (this.rootDir && this.rootDir.value !== dir) {
        (this.rootDir as { value: 'ltr' | 'rtl' }).value = dir; // typed readonly, but a plain field the CDK reads on each access
        this.rootDir.change.emit(dir);
      }
    });
    // Arabic: translate the English UI text still present in templates, columns, labels and reports (see phrases.ar.ts).
    effect(() => {
      const arabic = this.lang() === 'ar';
      if (typeof document === 'undefined') return;
      if (arabic) setTimeout(() => this.dom.start(document.body));
      else this.dom.stop(true);
    });
  }

  t(key: string, fallback?: string): string { return DICTIONARIES[this.lang()][key] ?? fallback ?? key; }

  setLang(lang: Lang): void { this.theme.setLanguage(lang); }

  toggle(): void { this.setLang(this.lang() === 'ar' ? 'en' : 'ar'); }

  /** Formats a number for the active language (grouping and digits). */
  number(value: number, options?: Intl.NumberFormatOptions): string { return new Intl.NumberFormat(this.locale(), options).format(value); }
}
