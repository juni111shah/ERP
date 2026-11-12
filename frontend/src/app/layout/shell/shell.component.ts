import { Dir } from '@angular/cdk/bidi';
import { CdkConnectedOverlay, CdkOverlayOrigin, OverlayModule } from '@angular/cdk/overlay';
import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, linkedSignal, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzLayoutModule } from 'ng-zorro-antd/layout';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { filter, map, startWith } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { blockedPageApplies } from '@core/format/subscription';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { PageTitleService } from '@core/services/page-title.service';
import { SessionStore } from '@core/session/session.store';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { TenantThemeService } from '../../../theme/tenant-theme.service';
import { GlobalSearchComponent } from '../global-search/global-search.component';
import { NotificationDrawerComponent } from '../notification-drawer/notification-drawer.component';
import { TABLET_MEDIA, watchPhoneWidth } from '../phone-width';
import { visibleQuickCreate } from '../quick-create';
import { NavGroup, currentGroupId, filterMenu, groupIcon, menuIcon, menuLinkPath, menuLinkQuery, readOpenGroup, resolveOpenGroup, saveOpenGroup, scrollDeltaToReveal } from '../sidebar-nav';
import { SubscriptionBannerComponent } from '../subscription-banner/subscription-banner.component';
import { userInitials } from '../user-initials';
import { WorkspaceAssistantComponent, WorkspaceAssistantState } from '../workspace-assistant/workspace-assistant.component';
import { WorkspaceBlockedComponent } from '../workspace-blocked/workspace-blocked.component';
import { WorkspaceSwitcher } from '../workspace-switcher';
import { NAV_FLYOUT_POSITIONS, NavFlyoutMenu } from './nav-flyout';
import { sidebarMenu, unreadNotifications } from './shell-data';

/** App shell: white sidebar with the server-driven menu, header with workspace/entity switchers, notifications, theme and user menu. */
@Component({
  selector: 'erp-shell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [WorkspaceAssistantComponent, KeyboardClickDirective, Dir, OverlayModule, TranslatePipe, RouterOutlet, RouterLink, RouterLinkActive, FormsModule, NzLayoutModule, NzMenuModule, NzIconModule, NzButtonModule,
    NzDropDownModule, NzAvatarModule, NzBadgeModule, NzTagModule, NzSelectModule, NzInputModule, NzToolTipModule, GlobalSearchComponent, NotificationDrawerComponent, BrandLogoComponent, SubscriptionBannerComponent, WorkspaceBlockedComponent],
  styleUrl: './shell.component.scss',
  templateUrl: './shell.component.html',
})
export class ShellComponent {
  readonly assistant = inject(WorkspaceAssistantState);
  readonly pageTitle = inject(PageTitleService);
  readonly session = inject(SessionStore);
  readonly i18n = inject(I18nService);
  readonly auth = inject(AuthService);
  readonly theme = inject(TenantThemeService);
  private readonly router = inject(Router);
  private readonly switcher = inject(WorkspaceSwitcher);

  // ---------- sidebar: column, icon rail (collapsed) or drawer (phone) ----------
  readonly collapsed = signal(false);
  /** Phone width: the sidebar is a drawer over the page rather than a column beside it. */
  readonly narrow = signal(false);
  readonly drawerOpen = signal(false);
  readonly toggleIcon = computed(() => this.i18n.dir() === 'rtl' ? (this.collapsed() ? 'double-left' : 'double-right') : (this.collapsed() ? 'double-right' : 'double-left'));

  // ---------- header ----------
  readonly notificationsOpen = signal(false);
  /** Bumped when the notification drawer changes something, so the unread count is asked again. */
  readonly refresh = signal(0);
  readonly unread = unreadNotifications(this.refresh);
  readonly workspaceName = computed(() => this.session.me()?.workspace.name ?? 'ERP');
  /** The workspace holds sample data: say so on every screen, so nobody mistakes it for real figures. */
  readonly isDemo = computed(() => !!this.session.me()?.workspace.isDemo);
  /** The New menu: only what this workspace has switched on and this person may create. */
  readonly quickCreate = computed(() => visibleQuickCreate((m) => this.session.isModuleEnabled(m), (p) => this.session.hasPermission(p)));
  readonly initials = computed(() => userInitials(this.session.me()?.user.fullName));

  // ---------- menu: loaded per session, narrowed by the sidebar search ----------
  readonly rawMenu = sidebarMenu();
  readonly menuSearch = signal('');
  readonly menu = computed(() => filterMenu(this.rawMenu(), this.menuSearch(), (key, fallback) => this.i18n.t(key, fallback)));
  readonly groupIcon = groupIcon;

  // ---------- sidebar groups: one open at a time, the choice is remembered, the group of the current page always opens ----------
  private readonly currentUrl = toSignal(this.router.events.pipe(
    filter((e): e is NavigationEnd => e instanceof NavigationEnd),
    map((e) => e.urlAfterRedirects),
    startWith(this.router.url),
  ), { initialValue: '/' });
  private readonly currentPath = computed(() => this.currentUrl().split('?')[0]);
  /** A suspended or cancelled workspace sees the blocked page everywhere but its billing page. */
  readonly blocked = computed(() => blockedPageApplies(this.session.me()?.subscription?.access, this.currentPath()));
  /** The group holding the current page, marked on the icon rail. */
  readonly currentGroup = computed(() => currentGroupId(this.rawMenu(), this.currentUrl()));
  /** Worked out again on every navigation and menu load; between those, toggleGroup sets it. */
  private readonly openGroupId = linkedSignal<{ url: string; groups: NavGroup[] }, string | null | undefined>({
    source: () => ({ url: this.currentUrl(), groups: this.rawMenu() }),
    computation: ({ url, groups }, previous) => resolveOpenGroup(groups, url, previous ? previous.value : readOpenGroup()),
  });
  private readonly navScroll = viewChild<ElementRef<HTMLElement>>('navScroll');

  // ---------- icon rail: hovering or clicking a category shows its menu beside the collapsed sidebar ----------
  readonly flyoutPositions = NAV_FLYOUT_POSITIONS;
  private readonly flyoutOverlay = viewChild(CdkConnectedOverlay);
  private readonly flyoutPanel = viewChild<ElementRef<HTMLElement>>('flyoutPanel');
  private readonly flyoutMenu = new NavFlyoutMenu({ collapsed: this.collapsed, menu: this.rawMenu, overlay: this.flyoutOverlay, panel: this.flyoutPanel });
  readonly flyout = this.flyoutMenu.current;
  readonly flyoutGroup = this.flyoutMenu.group;

  constructor() {
    afterRenderEffect(() => this.revealActiveNav());
    const watched = watchPhoneWidth((phone) => {
      this.narrow.set(phone);
      if (phone) this.collapsed.set(false);
      else this.drawerOpen.set(false);
    });
    if (!watched) return;
    // Tablets start with the icon-only sidebar, to leave room for tables.
    if (!this.narrow() && window.matchMedia(TABLET_MEDIA).matches) this.collapsed.set(true);
    // Choosing a page closes the drawer and the flyout.
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => {
      this.drawerOpen.set(false);
      this.closeFlyout();
    });
  }

  toggleSidebar(): void {
    if (this.narrow()) { this.drawerOpen.set(false); return; }
    this.collapsed.set(!this.collapsed());
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new Event('resize'));
      setTimeout(() => window.dispatchEvent(new Event('resize')), 150);
      setTimeout(() => window.dispatchEvent(new Event('resize')), 320);
    }
  }

  /** A search shows the items of every matching group. */
  isGroupOpen(g: NavGroup): boolean {
    return !!this.menuSearch().trim() || this.openGroupId() === g.id;
  }

  toggleGroup(g: NavGroup): void {
    if (this.menuSearch().trim()) return;
    const next = this.openGroupId() === g.id ? null : g.id;
    this.openGroupId.set(next);
    saveOpenGroup(next);
  }

  onRailEnter(g: NavGroup, origin: CdkOverlayOrigin): void { this.flyoutMenu.hover(g, origin); }
  onRailClick(g: NavGroup, origin: CdkOverlayOrigin, event: MouseEvent): void { this.flyoutMenu.click(g, origin, event); }
  /** Cancels a pending open or close. */
  keepFlyout(): void { this.flyoutMenu.keep(); }
  scheduleFlyoutClose(): void { this.flyoutMenu.scheduleClose(); }
  closeFlyout(): void { this.flyoutMenu.close(); }
  onFlyoutOverlayKeydown(event: KeyboardEvent): void { if (event.key === 'Escape') this.closeFlyout(); }
  onFlyoutOutsideClick(event: MouseEvent): void { this.flyoutMenu.outsideClick(event); }
  onFlyoutKeydown(event: KeyboardEvent): void { this.flyoutMenu.keydown(event); }

  onSearchInput(event: Event): void { this.menuSearch.set((event.target as HTMLInputElement).value); }
  clearSearch(): void { this.menuSearch.set(''); }

  expandAndSearch(): void {
    this.collapsed.set(false);
    setTimeout(() => document.querySelector<HTMLInputElement>('[data-testid="sidebar-search-input"]')?.focus(), 120);
  }

  linkPath(route: string): string { return menuLinkPath(route); }
  linkQuery(route: string): Record<string, string> | null { return menuLinkQuery(route); }
  icon(name?: string): string { return menuIcon(name); }
  bump(): void { this.refresh.update((n) => n + 1); }

  selectEntity(id: string | null): void { this.switcher.selectEntity(id); }
  switchWorkspace(id: string): void { this.switcher.switchWorkspace(id); }

  /** Keeps the open group in view (it can land past the end of the sidebar as the one above closes), or the current page's category on the rail. */
  private revealActiveNav(): void {
    const collapsed = this.collapsed();
    const id = collapsed ? this.currentGroup() : this.openGroupId();
    const area = this.navScroll()?.nativeElement;
    this.rawMenu(); // nothing is drawn until the menu has loaded
    if (!id || !area || (!collapsed && this.menuSearch().trim())) return;
    const first = area.querySelector(`[data-nav-group="${CSS.escape(id)}"]`);
    const last = collapsed ? first : first?.nextElementSibling;
    if (!first || !last) return;
    const view = area.getBoundingClientRect();
    area.scrollTop += scrollDeltaToReveal(view.top, view.bottom, first.getBoundingClientRect().top, last.getBoundingClientRect().bottom);
  }
}
