import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, combineLatest, interval, map, of, startWith, switchMap } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import { TenantThemeService } from '../../theme/tenant-theme.service';
import { API_BASE_URL } from '../core/api/api-config';
import { AuthService } from '../core/auth/auth.service';
import { SessionStore } from '../core/session/session.store';
import { KNOWN_ICONS } from '../icons';
import { Dir } from '@angular/cdk/bidi';
import { I18nService } from '../core/i18n/i18n.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { PageTitleService } from '../core/services/page-title.service';
import { BrandLogoComponent } from '../shared/brand-logo.component';
import { GlobalSearchComponent } from './global-search.component';
import { NotificationDrawerComponent } from './notification-drawer.component';

interface MenuGroup { id: string; label: string; order: number; items: { id: string; label: string; route: string; icon?: string; module: string }[]; }

/** App shell: white sidebar with the server-driven menu, header with workspace/entity switchers, notifications, theme and user menu. */
@Component({
  selector: 'erp-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Dir, TranslatePipe, RouterOutlet, RouterLink, RouterLinkActive, FormsModule, NzLayoutModule, NzMenuModule, NzIconModule, NzButtonModule,
    NzDropDownModule, NzAvatarModule, NzBadgeModule, NzTagModule, NzSelectModule, NzInputModule, NzToolTipModule, GlobalSearchComponent, NotificationDrawerComponent, BrandLogoComponent],
  styles: [`
    :host { display: block; height: 100vh; }
    nz-layout.root { height: 100vh; }
    .sider {
      background: var(--erp-color-nav-bg);
      border-inline-end: 1px solid var(--erp-color-nav-border);
      overflow: visible !important;
      height: 100vh;
      position: relative;
      z-index: 100;
      transition: width 0.24s cubic-bezier(0.2, 0, 0, 1) !important;
    }
    .sider ::ng-deep .ant-layout-sider-children {
      display: flex;
      flex-direction: column;
      height: 100%;
      overflow: visible !important;
      position: relative;
    }
    .brand {
      height: 60px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 14px 0 16px;
      border-bottom: 1px solid var(--erp-color-border-subtle);
      position: relative;
      overflow: visible;
      flex-shrink: 0;
      transition: padding 0.24s cubic-bezier(0.2, 0, 0, 1);
    }
    .brand.collapsed {
      padding: 0;
      justify-content: center;
    }
    .brand erp-brand-logo {
      flex: 1;
      min-width: 0;
    }
    .brand.collapsed erp-brand-logo {
      width: 100%;
      display: flex;
      justify-content: center;
    }

    .sidebar-toggle-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 28px;
      padding: 0;
      margin: 0;
      border: 1px solid transparent;
      border-radius: 6px;
      background: transparent;
      color: var(--erp-color-text-secondary, #64748b);
      cursor: pointer;
      flex-shrink: 0;
      transition: all 0.2s cubic-bezier(0.2, 0, 0, 1);
      outline: none;
      z-index: 2;
    }
    .sidebar-toggle-btn:hover {
      background: rgba(16, 149, 93, 0.08);
      color: #10955d;
      border-color: rgba(16, 149, 93, 0.2);
    }
    .sidebar-toggle-btn:active {
      transform: scale(0.94);
    }
    .sidebar-toggle-btn .anticon {
      font-size: 13px;
      line-height: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }

    .sidebar-toggle-btn.collapsed {
      position: absolute;
      inset-inline-end: -13px;
      top: 50%;
      transform: translateY(-50%);
      width: 26px;
      height: 26px;
      border-radius: 50%;
      background: #ffffff;
      border: 1px solid var(--erp-color-border-subtle, #e2e8f0);
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.08), 0 1px 2px rgba(0, 0, 0, 0.04);
      color: var(--erp-color-text-secondary, #475569);
      z-index: 101;
    }
    .sidebar-toggle-btn.collapsed:hover {
      background: #ffffff;
      color: #10955d;
      border-color: rgba(16, 149, 93, 0.4);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.14), 0 2px 4px rgba(0, 0, 0, 0.06);
      transform: translateY(-50%) scale(1.08);
    }
    .sidebar-toggle-btn.collapsed:active {
      transform: translateY(-50%) scale(0.96);
    }
    .sidebar-toggle-btn.collapsed .anticon {
      font-size: 11px;
      line-height: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }

    :host-context([data-theme='dark']) .sidebar-toggle-btn.collapsed,
    :host-context(.dark) .sidebar-toggle-btn.collapsed {
      background: #1e293b;
      border-color: #334155;
      color: #94a3b8;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.35);
    }
    :host-context([data-theme='dark']) .sidebar-toggle-btn.collapsed:hover,
    :host-context(.dark) .sidebar-toggle-btn.collapsed:hover {
      background: #1e293b;
      color: #34d399;
      border-color: rgba(52, 211, 153, 0.4);
    }
    .nav-scroll-area {
      flex: 1;
      overflow-y: auto;
      overflow-x: hidden;
      min-height: 0;
      padding: 4px 0 24px;
      scrollbar-width: thin;
      scrollbar-color: transparent transparent;
      transition: scrollbar-color 0.2s ease;
    }
    .nav-scroll-area:hover {
      scrollbar-color: var(--erp-scrollbar-thumb, rgba(16, 149, 93, 0.35)) transparent;
    }
    .nav-scroll-area::-webkit-scrollbar {
      width: 4px;
    }
    .nav-scroll-area::-webkit-scrollbar-track {
      background: transparent;
    }
    .nav-scroll-area::-webkit-scrollbar-thumb {
      background: transparent;
      border-radius: 9999px;
      transition: background-color 0.2s ease;
    }
    .nav-scroll-area:hover::-webkit-scrollbar-thumb {
      background: var(--erp-scrollbar-thumb, rgba(16, 149, 93, 0.35));
    }
    .nav-scroll-area::-webkit-scrollbar-thumb:hover {
      background: var(--erp-scrollbar-thumb-hover, rgba(16, 149, 93, 0.65)) !important;
    }
    .header { background: var(--erp-color-bg-surface); border-bottom: 1px solid var(--erp-color-border-subtle); display: flex; align-items: center; gap: 12px; padding: 0 16px; height: 56px; line-height: 56px; }
    .spacer { flex: 1; }
    .search { max-width: 360px; }
    .content { padding: 24px; overflow: auto; background: var(--erp-color-bg-canvas); }
    .group-title {
      padding: 14px 18px 4px;
      font-size: 11px;
      font-weight: 600;
      color: var(--erp-color-text-tertiary);
      text-transform: uppercase;
      letter-spacing: .06em;
      white-space: nowrap;
      overflow: hidden;
      height: 32px;
      line-height: 20px;
      opacity: 1;
      transition: opacity 0.18s cubic-bezier(0.2, 0, 0, 1),
                  height 0.24s cubic-bezier(0.2, 0, 0, 1),
                  padding 0.24s cubic-bezier(0.2, 0, 0, 1);
    }
    .group-title.collapsed {
      height: 0;
      padding-top: 0;
      padding-bottom: 0;
      opacity: 0;
      pointer-events: none;
    }

    /* Polished Menu Items — perfectly centered collapsed pills and soft mint active background */
    .sider ::ng-deep {
      .ant-menu {
        background: transparent !important;
        border-inline-end: none !important;
        width: 100% !important;
      }

      /* Collapsed Menu Items: perfectly centered 44x44px pill with no left-padding offset */
      .ant-menu-inline-collapsed {
        width: 64px !important;

        > .ant-menu-item {
          width: 44px !important;
          height: 44px !important;
          line-height: 44px !important;
          margin: 6px auto !important;
          padding: 0 !important;
          padding-left: 0 !important;
          padding-right: 0 !important;
          padding-inline: 0 !important;
          border-radius: 12px !important;
          display: flex !important;
          align-items: center !important;
          justify-content: center !important;
          text-align: center !important;
          transition: background-color 0.2s ease, color 0.2s ease, border-color 0.2s ease !important;

          .ant-menu-title-content {
            width: 100% !important;
            height: 100% !important;
            margin: 0 !important;
            padding: 0 !important;
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
          }

          .nav-item-inner {
            width: 100% !important;
            height: 100% !important;
            margin: 0 !important;
            padding: 0 !important;
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
          }

          .nav-icon {
            width: 44px !important;
            height: 44px !important;
            margin: 0 !important;
            padding: 0 !important;
            display: inline-flex !important;
            align-items: center !important;
            justify-content: center !important;
            font-size: 20px !important;
            line-height: 1 !important;
            flex-shrink: 0 !important;

            .anticon {
              margin: 0 !important;
              display: inline-flex !important;
              align-items: center !important;
              justify-content: center !important;
            }
          }

          .nav-label {
            display: none !important;
            width: 0 !important;
            height: 0 !important;
            margin: 0 !important;
            padding: 0 !important;
            pointer-events: none !important;
          }

          &::after {
            display: none !important;
          }

          &:not(.ant-menu-item-selected):hover {
            background: rgba(16, 149, 93, 0.08) !important;
            color: #10955d !important;

            .nav-icon {
              color: #10955d !important;
              transform: scale(1.1);
            }
          }

          &.ant-menu-item-selected {
            background: #e6f7ef !important;
            color: #10955d !important;
            border: 1px solid rgba(16, 149, 93, 0.25) !important;

            .nav-icon {
              color: #10955d !important;
            }
          }
        }
      }

      /* Expanded Menu Items: comfortable rounded row */
      .ant-menu-inline:not(.ant-menu-inline-collapsed) {
        > .ant-menu-item {
          height: 42px !important;
          line-height: 42px !important;
          margin: 3px 10px !important;
          padding: 0 12px !important;
          padding-left: 12px !important;
          width: calc(100% - 20px) !important;
          border-radius: 9px !important;
          display: flex !important;
          align-items: center !important;
          transition: background-color 0.2s ease, color 0.2s ease, border-color 0.2s ease !important;

          .ant-menu-title-content {
            width: 100% !important;
            height: 100% !important;
            display: flex !important;
            align-items: center !important;
          }

          .nav-item-inner {
            display: flex !important;
            align-items: center !important;
            gap: 10px !important;
            width: 100% !important;
          }

          .nav-icon {
            width: 20px !important;
            min-width: 20px !important;
            height: 20px !important;
            font-size: 17px !important;
            display: inline-flex !important;
            align-items: center !important;
            justify-content: center !important;
          }

          .nav-label {
            display: inline-block !important;
            white-space: nowrap !important;
            overflow: hidden !important;
            text-overflow: ellipsis !important;
            font-size: 14px !important;
            opacity: 1 !important;
            max-width: 170px !important;
          }

          &::after {
            display: none !important;
          }

          &:not(.ant-menu-item-selected):hover {
            background: rgba(16, 149, 93, 0.08) !important;
            color: #10955d !important;

            .nav-icon {
              color: #10955d !important;
            }
          }

          &.ant-menu-item-selected {
            background: #e6f7ef !important;
            color: #10955d !important;
            border: 1px solid rgba(16, 149, 93, 0.25) !important;
            font-weight: 600 !important;

            .nav-icon {
              color: #10955d !important;
            }

            .nav-label {
              color: #10955d !important;
            }
          }
        }
      }
    }
  `],
  template: `
    <nz-layout class="root" [dir]="i18n.dir()">
      <nz-sider class="sider" nzCollapsible [nzCollapsed]="collapsed()" [nzTrigger]="null" nzWidth="248px" [nzCollapsedWidth]="64" nzTheme="light" data-testid="sidebar">
        <div class="brand" [class.collapsed]="collapsed()" data-testid="sidebar-brand">
          <erp-brand-logo [collapsed]="collapsed()" [subtitle]="workspaceName()"></erp-brand-logo>
          <button
            type="button"
            class="sidebar-toggle-btn"
            [class.collapsed]="collapsed()"
            (click)="collapsed.set(!collapsed())"
            [attr.aria-label]="collapsed() ? 'Expand sidebar' : 'Collapse sidebar'"
            [attr.title]="collapsed() ? 'Expand sidebar' : 'Collapse sidebar'"
            data-testid="nav-toggle">
            <span nz-icon [nzType]="collapsed() ? 'double-right' : 'double-left'"></span>
          </button>
        </div>
        <div class="nav-scroll-area">
          @for (g of menu(); track g.id) {
            <div class="group-title" [class.collapsed]="collapsed()">{{ i18n.t('menu.group.' + g.id, g.label) }}</div>
            <ul nz-menu nzMode="inline" [nzInlineCollapsed]="collapsed()" [nzInlineIndent]="collapsed() ? 0 : 16">
              @for (i of g.items; track i.id) {
                <li nz-menu-item nz-tooltip [nzTooltipTitle]="collapsed() ? i.label : null" nzTooltipPlacement="right"
                    [routerLink]="linkPath(i.route)" [queryParams]="linkQuery(i.route)" routerLinkActive="ant-menu-item-selected" [routerLinkActiveOptions]="{ exact: i.route === '/' }"
                    [attr.data-testid]="'nav-' + i.id">
                  <div class="nav-item-inner">
                    <span nz-icon [nzType]="icon(i.icon)" class="nav-icon"></span>
                    <span class="nav-label" [class.collapsed]="collapsed()">{{ i18n.t('menu.' + i.id, i.label) }}</span>
                  </div>
                </li>
              }
            </ul>
          }
        </div>
      </nz-sider>
      <nz-layout>
        <nz-header class="header">
          <div class="topbar-title-wrap" data-testid="topbar-title-wrap">
            @if (pageTitle.module(); as mod) {
              <span class="topbar-module-badge">{{ mod }}</span>
              <span class="topbar-breadcrumb-slash">/</span>
            }
            <h1 class="topbar-title" data-testid="topbar-title">{{ pageTitle.title() }}</h1>
            @if (pageTitle.badge(); as b) {
              <nz-tag [nzColor]="b.color ?? 'default'" class="topbar-badge">{{ b.text }}</nz-tag>
            }
            @if (pageTitle.subtitle(); as sub) {
              <span class="topbar-subtitle" nz-tooltip [nzTooltipTitle]="sub">{{ sub }}</span>
            }
          </div>
          <span class="spacer"></span>
          <erp-global-search />
          @if (session.me(); as me) {
            @if (me.legalEntities.length > 1 || me.allLegalEntities) {
              <nz-select style="width: 220px" [ngModel]="session.currentEntityId()" (ngModelChange)="selectEntity($event)" data-testid="entity-switcher">
                @if (me.allLegalEntities) { <nz-option [nzValue]="null" [nzLabel]="'shell.allEntities' | translate: 'All legal entities'"></nz-option> }
                @for (e of me.legalEntities; track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }
              </nz-select>
            }
            @if (me.workspaces.length > 1) {
              <button nz-button nz-dropdown [nzDropdownMenu]="wsMenu" data-testid="workspace-switcher"><span nz-icon nzType="swap"></span>{{ me.workspace.name }}</button>
              <nz-dropdown-menu #wsMenu="nzDropdownMenu">
                <ul nz-menu>
                  @for (w of me.workspaces; track w.id) {
                    <li nz-menu-item [nzDisabled]="w.id === me.workspace.id" (click)="switchWorkspace(w.id)" [attr.data-testid]="'workspace-option-' + w.code">{{ w.name }}</li>
                  }
                </ul>
              </nz-dropdown-menu>
            }
          }
          <button nz-button nzType="text" aria-label="Notifications" data-testid="notifications-bell" (click)="notificationsOpen.set(true)">
            <nz-badge [nzCount]="unread().unread" nzSize="small"><span nz-icon nzType="bell"></span></nz-badge>
          </button>
          <button nz-button nzType="text" (click)="i18n.toggle()" aria-label="Language" data-testid="lang-toggle">{{ i18n.lang() === 'ar' ? 'EN' : 'العربية' }}</button>
          <button nz-button nzType="text" (click)="theme.toggleTheme()" aria-label="Toggle dark mode" data-testid="theme-toggle"><span nz-icon nzType="bulb"></span></button>
          <button nz-button nzType="text" nz-dropdown [nzDropdownMenu]="userMenu" data-testid="user-menu">
            <nz-avatar nzSize="small" [nzText]="initials()"></nz-avatar>
          </button>
          <nz-dropdown-menu #userMenu="nzDropdownMenu">
            <ul nz-menu>
              <li nz-menu-item nzDisabled>{{ session.me()?.user?.email }}</li>
              <li nz-menu-item routerLink="/account/security" data-testid="account-security"><span nz-icon nzType="lock"></span> {{ 'shell.accountSecurity' | translate: 'Account security' }}</li>
              <li nz-menu-divider></li>
              <li nz-menu-item (click)="auth.logout()" data-testid="sign-out"><span nz-icon nzType="logout"></span> {{ 'shell.signOut' | translate: 'Sign out' }}</li>
            </ul>
          </nz-dropdown-menu>
        </nz-header>
        <nz-content class="content"><router-outlet /></nz-content>
        <erp-notification-drawer [open]="notificationsOpen()" (closed)="notificationsOpen.set(false)" (changed)="bump()" />
      </nz-layout>
    </nz-layout>`,
})
export class ShellComponent {
  readonly pageTitle = inject(PageTitleService);
  readonly session = inject(SessionStore);
  readonly i18n = inject(I18nService);
  readonly auth = inject(AuthService);
  readonly theme = inject(TenantThemeService);
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);

  readonly collapsed = signal(false);
  readonly notificationsOpen = signal(false);
  readonly refresh = signal(0);
  readonly workspaceName = computed(() => this.session.me()?.workspace.name ?? 'ERP');
  readonly initials = computed(() => (this.session.me()?.user.fullName ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase());

  // Menu reloads whenever the session changes (workspace switch, module enabled/disabled).
  readonly menu = toSignal(toObservable(this.session.me).pipe(
    switchMap((me) => (me ? this.http.get<MenuGroup[]>(`${this.base}/navigation/menu`).pipe(catchError(() => of([]))) : of([]))),
  ), { initialValue: [] as MenuGroup[] });

  // Unread count: on session change, after the drawer changes something, and every minute.
  readonly unread = toSignal(combineLatest([toObservable(this.session.me), toObservable(this.refresh), interval(60_000).pipe(startWith(0))]).pipe(
    map(([me]) => me),
    switchMap((me) => (me ? this.http.get<{ unread: number }>(`${this.base}/notifications`).pipe(catchError(() => of({ unread: 0 }))) : of({ unread: 0 }))),
  ), { initialValue: { unread: 0 } }) as () => { unread: number };

  /** Menu routes may carry a query string (e.g. /parties?type=customer); routerLink would encode the "?" and miss the route. */
  linkPath(route: string): string { return (route ?? '').split('?')[0] || '/'; }
  linkQuery(route: string): Record<string, string> | null {
    const q = (route ?? '').split('?')[1];
    if (!q) return null;
    const out: Record<string, string> = {};
    new URLSearchParams(q).forEach((v, k) => (out[k] = v));
    return out;
  }

  bump(): void { this.refresh.update((n) => n + 1); }

  icon(name?: string): string {
    return name && KNOWN_ICONS.has(name) ? name : 'appstore';
  }

  selectEntity(id: string | null): void {
    this.session.setEntity(id);
    // Reload the current screen so lists re-query in the new scope.
    const url = this.router.url;
    void this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => this.router.navigateByUrl(url));
  }

  switchWorkspace(id: string): void {
    this.auth.switchWorkspace(id).subscribe(() => void this.router.navigateByUrl('/'));
  }
}
