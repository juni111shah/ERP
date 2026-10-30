import { registerLocaleData } from '@angular/common';
import localeAr from '@angular/common/locales/ar';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { en_US, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { authInterceptor } from './core/http/auth.interceptor';
import { entityScopeInterceptor } from './core/http/entity-scope.interceptor';
import { envelopeInterceptor } from './core/http/envelope.interceptor';
import { moduleDisabledInterceptor } from './core/http/module-disabled.interceptor';
import { APP_ICONS } from './icons';
import { I18nService } from './core/i18n/i18n.service';
import { TenantThemeService } from '../theme/tenant-theme.service';

// ng-zorro's date helper formats with Angular's formatDate under the ng-zorro locale id ('ar' for ar_EG); without this Arabic locale data
// date pickers and calendars throw "Missing locale data for the locale 'ar'" once Arabic is selected.
registerLocaleData(localeAr);

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor, entityScopeInterceptor, moduleDisabledInterceptor, envelopeInterceptor])),
    provideAnimationsAsync(),
    // Angular's number/date pipes (DecimalPipe, DatePipe, CurrencyPipe) read LOCALE_ID once, so the saved language decides it at startup;
    // switching language at runtime is handled reactively by I18nService.number()/date() (used by erp-stat-card and the shared pages).
    { provide: LOCALE_ID, useFactory: () => (inject(TenantThemeService).lang().toLowerCase().startsWith('ar') ? 'ar' : 'en-US') },
    provideNzI18n(en_US),
    provideNzIcons(APP_ICONS),
    // Restore the session from the refresh cookie before the first navigation.
    provideAppInitializer(() => { inject(I18nService); return firstValueFrom(inject(AuthService).restore()); }),
  ],
};
