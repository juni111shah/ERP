import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { I18nService } from '../core/i18n/i18n.service';

/** Language and theme for pages with no profile menu to hold them: everything a visitor sees before signing in. */
@Component({
  selector: 'erp-preference-switches',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzIconModule],
  styles: [`
    :host { display: inline-flex; align-items: center; gap: 4px; flex-shrink: 0; }
    :host(.floating) { position: fixed; inset-block-start: 12px; inset-inline-end: 12px; z-index: 20; }
    button { display: inline-flex; align-items: center; justify-content: center; min-width: 36px; height: 36px; padding: 0 8px; border: 1px solid transparent; border-radius: 8px; background: none; color: var(--erp-color-text-secondary); font: inherit; font-size: 13px; font-weight: 600; cursor: pointer; }
    button:hover { background: var(--erp-color-nav-item-hover-bg, rgba(16, 149, 93, .08)); color: var(--erp-color-primary-text); }
    button:focus-visible { outline: 2px solid var(--erp-color-focus-ring); outline-offset: 1px; }
    .icon { font-size: 16px; }
  `],
  template: `
    <button type="button" (click)="i18n.toggle()" [attr.aria-label]="languageHint()" [title]="languageHint()" data-no-i18n data-testid="public-lang-toggle">{{ languageName() }}</button>
    <button type="button" (click)="theme.toggleTheme()" [attr.aria-pressed]="theme.resolvedTheme() === 'dark'" [attr.aria-label]="themeHint()" [title]="themeHint()" data-testid="public-theme-toggle">
      <span nz-icon nzType="bulb" class="icon" aria-hidden="true" [style.color]="theme.resolvedTheme() === 'dark' ? '#fbbf24' : 'inherit'"></span>
    </button>`,
})
export class PreferenceSwitchesComponent {
  readonly i18n = inject(I18nService);
  readonly theme = inject(TenantThemeService);
  readonly languageName = computed(() => (this.i18n.lang() === 'ar' ? 'English' : 'العربية'));
  readonly languageHint = computed(() => (this.i18n.lang() === 'ar' ? 'Switch to English' : 'التحويل إلى العربية'));
  readonly themeHint = computed(() => (this.theme.resolvedTheme() === 'dark' ? this.i18n.t('shell.lightMode', 'Light mode') : this.i18n.t('shell.darkMode', 'Dark mode')));
}
