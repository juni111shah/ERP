import { Dir } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
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
import { TenantThemeService } from '../../../theme/tenant-theme.service';
import { PlatformApi } from '@core/api/platform.api';
import { AuthService } from '@core/auth/auth.service';
import { I18nService } from '@core/i18n/i18n.service';
import { PageTitleService } from '@core/services/page-title.service';
import { SessionStore } from '@core/session/session.store';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';

interface ConsoleLink { path: string; label: string; icon: string; exact?: boolean; testId: string; badge?: boolean; }

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
  readonly initials = computed(() => (this.session.me()?.user.fullName ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase());

  readonly links: ConsoleLink[] = [
    { path: '/platform', label: 'Dashboard', icon: 'dashboard', exact: true, testId: 'platform-nav-dashboard' },
    { path: '/platform/clients', label: 'Clients', icon: 'team', testId: 'platform-nav-clients' },
    { path: '/platform/signups', label: 'Registrations', icon: 'user-add', testId: 'platform-nav-signups', badge: true },
    { path: '/platform/leads', label: 'Leads', icon: 'contacts', testId: 'platform-nav-leads' },
    { path: '/platform/plans', label: 'Plans and pricing', icon: 'tag', testId: 'platform-nav-plans' },
    { path: '/platform/invoices', label: 'Invoices', icon: 'file-text', testId: 'platform-nav-invoices' },
    { path: '/platform/activity', label: 'Activity', icon: 'history', testId: 'platform-nav-activity' },
    { path: '/platform/team', label: 'Team', icon: 'safety-certificate', testId: 'platform-nav-team' },
    { path: '/platform/assistant', label: 'AI assistant', icon: 'thunderbolt', testId: 'platform-nav-assistant' },
    { path: '/platform/settings', label: 'Settings', icon: 'setting', testId: 'platform-nav-settings' },
  ];

  constructor() {
    // The badge needs the count on every console page, not only the dashboard.
    this.api.dashboard().subscribe({ error: () => undefined });
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => this.drawerOpen.set(false));
    if (typeof window === 'undefined' || !window.matchMedia) return;
    const phone = window.matchMedia('(max-width: 767px)');
    const apply = () => {
      this.narrow.set(phone.matches);
      if (!phone.matches) this.drawerOpen.set(false);
    };
    apply();
    phone.addEventListener('change', apply);
    inject(DestroyRef).onDestroy(() => phone.removeEventListener('change', apply));
  }

  backToWorkspace(): void {
    this.session.setStayInWorkspace(true);
    void this.router.navigateByUrl('/');
  }
}
