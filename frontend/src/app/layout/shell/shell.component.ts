import { WorkspaceAssistantComponent, WorkspaceAssistantState } from '../workspace-assistant/workspace-assistant.component';
import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, afterRenderEffect, computed, inject, linkedSignal, signal, viewChild, DestroyRef } from '@angular/core';
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
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, combineLatest, filter, interval, map, of, startWith, switchMap } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import { TenantThemeService } from '../../../theme/tenant-theme.service';
import { API_BASE_URL } from '@core/api/api-config';
import { AuthService } from '@core/auth/auth.service';
import { SessionStore } from '@core/session/session.store';
import { KNOWN_ICONS } from '../../icons';
import { Dir } from '@angular/cdk/bidi';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { PageTitleService } from '@core/services/page-title.service';
import { BrandLogoComponent } from '@shared/components/brand-logo/brand-logo.component';
import { GlobalSearchComponent } from '../global-search/global-search.component';
import { NotificationDrawerComponent } from '../notification-drawer/notification-drawer.component';
import { SubscriptionBannerComponent } from '../subscription-banner/subscription-banner.component';
import { WorkspaceBlockedComponent } from '../workspace-blocked/workspace-blocked.component';
import { blockedPageApplies } from '@core/format/subscription';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { visibleQuickCreate } from '../quick-create';
import { NavGroup, currentGroupId, groupIcon, nextFocusIndex, resolveOpenGroup, scrollDeltaToReveal } from '../sidebar-nav';

/** The category whose menu is showing beside the collapsed sidebar. A pinned flyout (click or keyboard) ignores the pointer leaving. */
interface NavFlyout { id: string; origin: CdkOverlayOrigin; pinned: boolean; }

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
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);

  readonly collapsed = signal(false);
  /** Phone width: the sidebar is a drawer over the page rather than a column beside it. */
  readonly narrow = signal(false);
  readonly drawerOpen = signal(false);

  constructor() {
    afterRenderEffect(() => this.revealActiveNav());
    if (typeof window === 'undefined' || !window.matchMedia) return;
    const phone = window.matchMedia('(max-width: 767px)');
    const apply = () => {
      this.narrow.set(phone.matches);
      if (phone.matches) this.collapsed.set(false);
      else this.drawerOpen.set(false);
    };
    apply();
    // Tablets start with the icon-only sidebar, to leave room for tables.
    if (!phone.matches && window.matchMedia('(max-width: 1100px)').matches) this.collapsed.set(true);
    phone.addEventListener('change', apply);
    inject(DestroyRef).onDestroy(() => phone.removeEventListener('change', apply));
    // Choosing a page closes the drawer and the flyout.
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => {
      this.drawerOpen.set(false);
      this.closeFlyout();
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.flyoutTimer));
  }
  readonly notificationsOpen = signal(false);
  readonly refresh = signal(0);
  readonly workspaceName = computed(() => this.session.me()?.workspace.name ?? 'ERP');
  /** The workspace holds sample data: say so on every screen, so nobody mistakes it for real figures. */
  readonly isDemo = computed(() => !!this.session.me()?.workspace.isDemo);
  /** The New menu: only what this workspace has switched on and this person may create. */
  readonly quickCreate = computed(() => visibleQuickCreate((m) => this.session.isModuleEnabled(m), (p) => this.session.hasPermission(p)));
  readonly initials = computed(() => (this.session.me()?.user.fullName ?? '?').split(' ').map((p) => p[0]).slice(0, 2).join('').toUpperCase());
  readonly toggleIcon = computed(() => this.i18n.dir() === 'rtl' ? (this.collapsed() ? 'double-left' : 'double-right') : (this.collapsed() ? 'double-right' : 'double-left'));

  toggleSidebar(): void {
    if (this.narrow()) { this.drawerOpen.set(false); return; }
    this.collapsed.set(!this.collapsed());
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new Event('resize'));
      setTimeout(() => window.dispatchEvent(new Event('resize')), 150);
      setTimeout(() => window.dispatchEvent(new Event('resize')), 320);
    }
  }

  readonly menuSearch = signal('');

  // ---------- sidebar groups: one open at a time, the choice is remembered, the group of the current page always opens ----------
  private readonly openGroupKey = 'erp.nav.openGroup';
  private readonly currentUrl = toSignal(this.router.events.pipe(
    filter((e): e is NavigationEnd => e instanceof NavigationEnd),
    map((e) => e.urlAfterRedirects),
    startWith(this.router.url),
  ), { initialValue: '/' });
  private readonly currentPath = computed(() => this.currentUrl().split('?')[0]);

  /** A suspended or cancelled workspace sees the blocked page everywhere but its billing page. */
  readonly blocked = computed(() => blockedPageApplies(this.session.me()?.subscription?.access, this.currentPath()));

  readonly groupIcon = groupIcon;
  /** The group holding the current page, marked on the icon rail. */
  readonly currentGroup = computed(() => currentGroupId(this.rawMenu(), this.currentUrl()));
  /** Worked out again on every navigation and menu load; between those, toggleGroup sets it. */
  private readonly openGroupId = linkedSignal<{ url: string; groups: NavGroup[] }, string | null | undefined>({
    source: () => ({ url: this.currentUrl(), groups: this.rawMenu() }),
    computation: ({ url, groups }, previous) => resolveOpenGroup(groups, url, previous ? previous.value : this.readOpenGroup()),
  });
  private readonly navScroll = viewChild<ElementRef<HTMLElement>>('navScroll');

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

  /** A search shows the items of every matching group. */
  isGroupOpen(g: NavGroup): boolean {
    return !!this.menuSearch().trim() || this.openGroupId() === g.id;
  }

  toggleGroup(g: NavGroup): void {
    if (this.menuSearch().trim()) return;
    const next = this.openGroupId() === g.id ? null : g.id;
    this.openGroupId.set(next);
    try {
      if (next) localStorage.setItem(this.openGroupKey, next);
      else localStorage.removeItem(this.openGroupKey);
    } catch { /* storage unavailable */ }
  }

  private readOpenGroup(): string | undefined {
    try { return localStorage.getItem(this.openGroupKey) ?? undefined; } catch { return undefined; }
  }

  // ---------- icon rail: hovering or clicking a category shows its menu beside the collapsed sidebar ----------
  /** Start and end follow the language, so in Arabic the flyout opens on the left. */
  readonly flyoutPositions: ConnectedPosition[] = [
    { originX: 'end', originY: 'top', overlayX: 'start', overlayY: 'top' },
    { originX: 'end', originY: 'bottom', overlayX: 'start', overlayY: 'bottom' },
  ];
  /** Back to closed whenever the sidebar is collapsed or expanded. */
  readonly flyout = linkedSignal<boolean, NavFlyout | null>({ source: this.collapsed, computation: () => null });
  readonly flyoutGroup = computed(() => {
    const open = this.flyout();
    return open && this.collapsed() ? this.rawMenu().find((g) => g.id === open.id) ?? null : null;
  });
  private readonly flyoutOverlay = viewChild(CdkConnectedOverlay);
  private readonly flyoutPanel = viewChild<ElementRef<HTMLElement>>('flyoutPanel');
  private flyoutTimer?: ReturnType<typeof setTimeout>;

  onRailEnter(g: NavGroup, origin: CdkOverlayOrigin): void {
    this.keepFlyout();
    if (this.flyout()?.id === g.id) return;
    this.flyoutTimer = setTimeout(() => this.showFlyout({ id: g.id, origin, pinned: false }), 100);
  }

  /** The first click pins a menu the pointer opened; a click on a pinned menu closes it. A click from the keyboard moves into the menu. */
  onRailClick(g: NavGroup, origin: CdkOverlayOrigin, event: MouseEvent): void {
    this.keepFlyout();
    const open = this.flyout();
    if (open?.id === g.id && open.pinned) { this.closeFlyout(); return; }
    this.showFlyout({ id: g.id, origin, pinned: true }, event.detail === 0);
  }

  /** Cancels a pending open or close. */
  keepFlyout(): void { clearTimeout(this.flyoutTimer); }

  scheduleFlyoutClose(): void {
    this.keepFlyout();
    const open = this.flyout();
    if (open && !open.pinned) this.flyoutTimer = setTimeout(() => this.closeFlyout(), 200);
  }

  /** Focus inside the menu goes back to its category button, so it is not lost with the menu. */
  closeFlyout(): void {
    this.keepFlyout();
    const open = this.flyout();
    if (!open) return;
    if (this.flyoutPanel()?.nativeElement.contains(document.activeElement)) (open.origin.elementRef.nativeElement as HTMLElement).focus();
    this.flyout.set(null);
  }

  onFlyoutOverlayKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') this.closeFlyout();
  }

  /** A click on another category switches the menu instead of closing it first. */
  onFlyoutOutsideClick(event: MouseEvent): void {
    if (!(event.target as Element | null)?.closest?.('.nav-rail')) this.closeFlyout();
  }

  /** The panel sits at the end of the page, so tabbing past either end returns to the category button. */
  onFlyoutKeydown(event: KeyboardEvent): void {
    const links = Array.from((event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('a'));
    const at = links.indexOf(document.activeElement as HTMLElement);
    if (event.key === 'Tab') {
      if (event.shiftKey ? at <= 0 : at === links.length - 1) { event.preventDefault(); this.closeFlyout(); }
      return;
    }
    const next = nextFocusIndex(event.key, at, links.length);
    if (next !== null) { event.preventDefault(); links[next].focus(); }
  }

  private showFlyout(next: NavFlyout, moveFocus = false): void {
    this.flyout.set(next);
    afterNextRender(() => {
      this.flyoutOverlay()?.overlayRef?.updatePosition(); // the height changes with the category
      if (!moveFocus) return;
      // routerLinkActive marks the current page's link in a microtask.
      queueMicrotask(() => {
        const panel = this.flyoutPanel()?.nativeElement;
        (panel?.querySelector<HTMLElement>('a.is-active') ?? panel?.querySelector<HTMLElement>('a'))?.focus();
      });
    }, { injector: this.injector });
  }

  onSearchInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.menuSearch.set(target.value);
  }

  clearSearch(): void {
    this.menuSearch.set('');
  }

  expandAndSearch(): void {
    this.collapsed.set(false);
    setTimeout(() => {
      const input = document.querySelector('[data-testid="sidebar-search-input"]') as HTMLInputElement;
      input?.focus();
    }, 120);
  }

  // Menu reloads whenever the session changes (workspace switch, module enabled/disabled).
  readonly rawMenu = toSignal(toObservable(this.session.me).pipe(
    switchMap((me) => (me ? this.http.get<NavGroup[]>(`${this.base}/navigation/menu`).pipe(catchError(() => of([]))) : of([]))),
  ), { initialValue: [] as NavGroup[] });

  // Filtered menu based on menuSearch signal
  readonly menu = computed(() => {
    const term = this.menuSearch().trim().toLowerCase();
    const groups = this.rawMenu();
    if (!term) return groups;

    return groups.map((g) => {
      const groupLabel = (g.label || '').toLowerCase();
      const groupTranslated = (this.i18n.t('menu.group.' + g.id, g.label) || '').toLowerCase();
      const groupMatches = groupLabel.includes(term) || groupTranslated.includes(term);

      const filteredItems = g.items.filter((item) => {
        if (groupMatches) return true;
        const label = (item.label || '').toLowerCase();
        const translated = (this.i18n.t('menu.' + item.id, item.label) || '').toLowerCase();
        const route = (item.route || '').toLowerCase();
        return label.includes(term) || translated.includes(term) || route.includes(term);
      });

      return { ...g, items: filteredItems };
    }).filter((g) => g.items.length > 0);
  });

  // Unread count: on session change, after the drawer changes something, and every minute.
  readonly unread = toSignal(combineLatest([toObservable(this.session.me), toObservable(this.refresh), interval(60_000).pipe(startWith(0))]).pipe(
    map(([me]) => me),
    // A blocked workspace answers 402 to everything but its billing page, so there is nothing to ask.
    switchMap((me) => (me && me.subscription?.access !== 'blocked' ? this.http.get<{ unread: number }>(`${this.base}/notifications`).pipe(catchError(() => of({ unread: 0 }))) : of({ unread: 0 }))),
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
