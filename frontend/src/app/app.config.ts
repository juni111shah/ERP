import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
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

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor, entityScopeInterceptor, moduleDisabledInterceptor, envelopeInterceptor])),
    provideAnimationsAsync(),
    provideNzI18n(en_US),
    provideNzIcons(APP_ICONS),
    // Restore the session from the refresh cookie before the first navigation.
    provideAppInitializer(() => firstValueFrom(inject(AuthService).restore())),
  ],
};
