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
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { PlatformApi } from '../core/api/platform.api';
import { AuthService } from '../core/auth/auth.service';
import { I18nService } from '../core/i18n/i18n.service';
import { PageTitleService } from '../core/services/page-title.service';
import { SessionStore } from '../core/session/session.store';
import { BrandLogoComponent } from '../shared/brand-logo.component';

interface ConsoleLink { path: string; label: string; icon: string; exact?: boolean; testId: string; badge?: boolean; }

/** The founder's console: its own navigation, separate from the ERP shell a client works in. */
@Component({
  selector: 'erp-platform-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Dir, RouterOutlet, RouterLink, RouterLinkActive, NzAvatarModule, NzBadgeModule, NzButtonModule, NzDropDownModule, NzIconModule, NzMenuModule, NzToolTipModule, BrandLogoComponent],
  styles: [`
    :host { display: block; height: 100vh; }
    .root { display: flex; height: 100vh; overflow: hidden; background: var(--erp-color-bg-canvas); }
    .side { width: 248px; flex-shrink: 0; height: 100vh; display: flex; flex-direction: column; background: var(--erp-color-nav-bg); border-inline-end: 1px solid var(--erp-color-nav-border); z-index: 100; }
    .side.as-drawer { position: fixed; inset-block: 0; inset-inline-start: 0; z-index: 1000; transform: translateX(calc(-100% * var(--erp-direction-sign, 1))); transition: transform .25s cubic-bezier(.4,0,.2,1); }
    .side.as-drawer.is-open { transform: none; box-shadow: var(--erp-shadow-3); }
    .backdrop { position: fixed; inset: 0; z-index: 999; background: var(--erp-color-bg-overlay); }
    .brand { height: 60px; display: flex; align-items: center; padding: 0 15px; border-bottom: 1px solid var(--erp-color-border-subtle); flex-shrink: 0; }
    .brand erp-brand-logo { flex: 1; min-width: 0; }
    nav { flex: 1; min-height: 0; overflow-y: auto; padding: 8px 10px 16px; }
    nav a { display: flex; align-items: center; gap: 10px; height: 42px; margin: 3px 0; padding: 0 12px; border-radius: 9px; border: 1px solid transparent; color: var(--erp-color-nav-text, var(--erp-color-text-secondary)); text-decoration: none; font-size: 14px; white-space: nowrap; transition: background-color .2s ease, color .2s ease; }
    nav a:hover { background: var(--erp-color-nav-item-hover-bg, rgba(16,149,93,.08)); color: var(--erp-color-primary-text); }
    nav a.active { background: var(--erp-color-primary-subtle); color: var(--erp-color-primary-text); border-color: var(--erp-color-primary-border); font-weight: 600; }
    nav a:focus-visible, .foot button:focus-visible, .foot a:focus-visible { outline: 2px solid var(--erp-color-focus-ring); outline-offset: -2px; }
    nav a .icon { font-size: 17px; width: 20px; display: inline-flex; justify-content: center; flex-shrink: 0; }
    nav a .label { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; }
    .foot { flex-shrink: 0; padding: 8px 10px 12px; border-top: 1px solid var(--erp-color-border-subtle); display: flex; flex-direction: column; gap: 2px; }
    .foot a, .foot button { display: flex; align-items: center; gap: 10px; width: 100%; height: 38px; padding: 0 12px; border: 0; border-radius: 9px; background: none; font: inherit; font-size: 13.5px; color: var(--erp-color-text-secondary); cursor: pointer; text-decoration: none; text-align: start; }
    .foot a:hover, .foot button:hover { background: var(--erp-color-nav-item-hover-bg, rgba(16,149,93,.08)); color: var(--erp-color-primary-text); }
    .main { flex: 1 1 auto; min-width: 0; display: flex; flex-direction: column; height: 100vh; overflow: hidden; }
    .bar { height: 58px; flex-shrink: 0; display: flex; align-items: center; gap: 10px; padding: 0 20px; background: var(--erp-color-bg-surface); border-bottom: 1px solid var(--erp-color-border-subtle); min-width: 0; }
    .bar-title { display: flex; align-items: center; gap: 8px; min-width: 0; flex: 1; overflow: hidden; }
    .menu-btn, .tool-btn { width: 36px; height: 36px; flex-shrink: 0; display: inline-flex; align-items: center; justify-content: center; border: 1px solid transparent; border-radius: 8px; background: none; color: var(--erp-color-text-secondary); cursor: pointer; font-size: 16px; padding: 0; }
    .menu-btn:hover, .tool-btn:hover { background: var(--erp-color-nav-item-hover-bg, rgba(16,149,93,.08)); color: var(--erp-color-primary-text); }
    .who { color: var(--erp-color-text-muted); font-size: 13px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 240px; }
    .content { flex: 1; min-height: 0; min-width: 0; overflow-y: auto; overflow-x: hidden; padding: 24px; }
    @media (max-width: 767px) {
      .bar { padding: 0 12px; }
      .who { display: none; }
      .content { padding: 14px 12px; }
    }
  `],
  template: `
    <div class="root" [dir]="i18n.dir()">
      <aside class="side" [class.as-drawer]="narrow()" [class.is-open]="drawerOpen()" (keydown.escape)="drawerOpen.set(false)" data-testid="platform-sidebar" aria-label="Console navigation">
        <div class="brand"><erp-brand-logo subtitle="Platform console"></erp-brand-logo></div>
        <nav>
          @for (l of links; track l.path) {
            <a [routerLink]="l.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: !!l.exact }" [attr.data-testid]="l.testId" (click)="drawerOpen.set(false)">
              <span class="icon" nz-icon [nzType]="l.icon" aria-hidden="true"></span>
              <span class="label">{{ l.label }}</span>
              @if (l.badge && api.pendingSignups() > 0) { <nz-badge [nzCount]="api.pendingSignups()" [attr.aria-label]="api.pendingSignups() + ' waiting'" data-testid="platform-signups-badge"></nz-badge> }
            </a>
          }
        </nav>
        <div class="foot">
          <button type="button" (click)="backToWorkspace()" data-testid="platform-back-to-workspace"><span nz-icon nzType="arrow-left" aria-hidden="true"></span>Back to workspace</button>
          <button type="button" (click)="auth.logout()" data-testid="platform-sign-out"><span nz-icon nzType="logout" aria-hidden="true"></span>Sign out</button>
        </div>
      </aside>
      @if (narrow() && drawerOpen()) { <div class="backdrop" (click)="drawerOpen.set(false)" data-testid="platform-backdrop"></div> }
      <div class="main">
        <header class="bar">
          @if (narrow()) {
            <button type="button" class="menu-btn" (click)="drawerOpen.set(true)" [attr.aria-expanded]="drawerOpen()" aria-label="Open menu" data-testid="platform-nav-open"><span nz-icon nzType="menu-unfold"></span></button>
          }
          <div class="bar-title">
            @if (pageTitle.module(); as mod) {
              <span class="topbar-module-badge"><span class="badge-dot"></span><span>{{ mod }}</span></span>
              <span class="topbar-breadcrumb-slash">/</span>
            }
            <h1 class="topbar-title" data-testid="platform-title">{{ pageTitle.title() }}</h1>
          </div>
          <span class="who">{{ session.me()?.user?.fullName }}</span>
          <button type="button" class="tool-btn" (click)="theme.toggleTheme()" aria-label="Toggle dark mode" nz-tooltip [nzTooltipTitle]="theme.resolvedTheme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'" data-testid="platform-theme-toggle">
            <span nz-icon nzType="bulb" [style.color]="theme.resolvedTheme() === 'dark' ? '#fbbf24' : 'inherit'"></span>
          </button>
          <button type="button" class="tool-btn" nz-dropdown [nzDropdownMenu]="userMenu" nzPlacement="bottomRight" [attr.aria-label]="session.me()?.user?.fullName || 'User menu'" data-testid="platform-user-menu">
            <nz-avatar nzSize="small" [nzText]="initials()"></nz-avatar>
          </button>
          <nz-dropdown-menu #userMenu="nzDropdownMenu">
            <ul nz-menu>
              <li nz-menu-item nzDisabled>{{ session.me()?.user?.email }}</li>
              <li nz-menu-item routerLink="/account/security"><span nz-icon nzType="lock"></span> Account security</li>
              <li nz-menu-divider></li>
              <li nz-menu-item (click)="auth.logout()"><span nz-icon nzType="logout"></span> Sign out</li>
            </ul>
          </nz-dropdown-menu>
        </header>
        <main class="content"><router-outlet /></main>
      </div>
    </div>`,
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
    { path: '/platform/plans', label: 'Plans and pricing', icon: 'tag', testId: 'platform-nav-plans' },
    { path: '/platform/invoices', label: 'Invoices', icon: 'file-text', testId: 'platform-nav-invoices' },
    { path: '/platform/activity', label: 'Activity', icon: 'history', testId: 'platform-nav-activity' },
    { path: '/platform/team', label: 'Team', icon: 'safety-certificate', testId: 'platform-nav-team' },
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
