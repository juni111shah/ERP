import { Dir } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { filter } from 'rxjs';
import { PlatformApi } from '@core/api/platform.api';
import { AuthService } from '@core/auth/auth.service';
import { I18nService } from '@core/i18n/i18n.service';
import { PageTitleService } from '@core/services/page-title.service';
import { SessionStore } from '@core/session/session.store';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { TenantThemeService } from '../../../theme/tenant-theme.service';
import { watchPhoneWidth } from '../phone-width';
import { userInitials } from '../user-initials';
import { CONSOLE_LINKS } from './console-links';

/** The founder's console: its own navigation, separate from the ERP shell a client works in. */
@Component({
  selector: 'erp-platform-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KeyboardClickDirective, Dir, RouterOutlet, RouterLink, RouterLinkActive, NzAvatarModule, NzBadgeModule, NzButtonModule, NzDropDownModule, NzIconModule, NzMenuModule, NzToolTipModule, BrandLogoComponent],
  styleUrl: './platform-shell.component.scss',
  templateUrl: './platform-shell.component.html',
})
export class PlatformShellComponent {
  readonly api = inject(PlatformApi);
  readonly auth = inject(AuthService);
  readonly session = inject(SessionStore);
  readonly i18n = inject(I18nService);
  readonly theme = inject(TenantThemeService);
  readonly pageTitle = inject(PageTitleService);
  private readonly router = inject(Router);

  readonly narrow = signal(false);
  readonly drawerOpen = signal(false);
  readonly initials = computed(() => userInitials(this.session.me()?.user.fullName));
  /** What this person may do here, so a hidden button is not a mystery. */
  readonly roleText = computed(() => { const r = this.session.me()?.platformRole; return r === 'owner' ? 'Platform owner' : r === 'manager' ? 'Manager' : 'Support (no suspend, plans, voids or settings)'; });
  readonly links = CONSOLE_LINKS;

  constructor() {
    // The badge needs the count on every console page, not only the dashboard.
    this.api.dashboard().subscribe({ error: () => undefined });
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => this.drawerOpen.set(false));
    watchPhoneWidth((phone) => {
      this.narrow.set(phone);
      if (!phone) this.drawerOpen.set(false);
    });
  }

  backToWorkspace(): void {
    this.session.setStayInWorkspace(true);
    void this.router.navigateByUrl('/');
  }
}
