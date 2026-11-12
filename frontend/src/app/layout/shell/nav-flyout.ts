import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { DestroyRef, ElementRef, Injector, Signal, WritableSignal, afterNextRender, computed, inject, linkedSignal } from '@angular/core';
import { NavGroup, nextFocusIndex } from '../sidebar-nav';

/** The category whose menu is showing beside the collapsed sidebar. A pinned flyout (click or keyboard) ignores the pointer leaving. */
export interface NavFlyout { id: string; origin: CdkOverlayOrigin; pinned: boolean; }

/** What the flyout needs from the shell: the sidebar state, the menu, and the overlay and panel the template draws. */
export interface NavFlyoutHost {
  collapsed: Signal<boolean>;
  menu: Signal<NavGroup[]>;
  overlay: Signal<CdkConnectedOverlay | undefined>;
  panel: Signal<ElementRef<HTMLElement> | undefined>;
}

/** Start and end follow the language, so in Arabic the flyout opens on the left. */
export const NAV_FLYOUT_POSITIONS: ConnectedPosition[] = [
  { originX: 'end', originY: 'top', overlayX: 'start', overlayY: 'top' },
  { originX: 'end', originY: 'bottom', overlayX: 'start', overlayY: 'bottom' },
];

/**
 * The icon rail's flyout menus: hovering or clicking a category shows its links beside the collapsed sidebar, with a short delay
 * to open and to close, and keyboard moves inside. Create it in an injection context; its timer stops with the host.
 */
export class NavFlyoutMenu {
  /** Back to closed whenever the sidebar is collapsed or expanded. */
  readonly current: WritableSignal<NavFlyout | null>;
  /** The group whose links the flyout lists, only while the sidebar is collapsed. */
  readonly group: Signal<NavGroup | null>;
  private readonly injector = inject(Injector);
  private timer?: ReturnType<typeof setTimeout>;

  constructor(private readonly host: NavFlyoutHost) {
    this.current = linkedSignal<boolean, NavFlyout | null>({ source: host.collapsed, computation: () => null });
    this.group = computed(() => {
      const open = this.current();
      return open && host.collapsed() ? host.menu().find((g) => g.id === open.id) ?? null : null;
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  /** The pointer reached a category: its menu opens after a moment. */
  hover(g: NavGroup, origin: CdkOverlayOrigin): void {
    this.keep();
    if (this.current()?.id === g.id) return;
    this.timer = setTimeout(() => this.show({ id: g.id, origin, pinned: false }), 100);
  }

  /** The first click pins a menu the pointer opened; a click on a pinned menu closes it. A click from the keyboard moves into the menu. */
  click(g: NavGroup, origin: CdkOverlayOrigin, event: MouseEvent): void {
    this.keep();
    const open = this.current();
    if (open?.id === g.id && open.pinned) { this.close(); return; }
    this.show({ id: g.id, origin, pinned: true }, event.detail === 0);
  }

  /** Cancels a pending open or close. */
  keep(): void { clearTimeout(this.timer); }

  /** The pointer left: an unpinned menu closes after a moment. */
  scheduleClose(): void {
    this.keep();
    const open = this.current();
    if (open && !open.pinned) this.timer = setTimeout(() => this.close(), 200);
  }

  /** Focus inside the menu goes back to its category button, so it is not lost with the menu. */
  close(): void {
    this.keep();
    const open = this.current();
    if (!open) return;
    if (this.host.panel()?.nativeElement.contains(document.activeElement)) (open.origin.elementRef.nativeElement as HTMLElement).focus();
    this.current.set(null);
  }

  /** A click on another category switches the menu instead of closing it first. */
  outsideClick(event: MouseEvent): void {
    if (!(event.target as Element | null)?.closest?.('.nav-rail')) this.close();
  }

  /** The panel sits at the end of the page, so tabbing past either end returns to the category button. */
  keydown(event: KeyboardEvent): void {
    const links = Array.from((event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('a'));
    const at = links.indexOf(document.activeElement as HTMLElement);
    if (event.key === 'Tab') {
      if (event.shiftKey ? at <= 0 : at === links.length - 1) { event.preventDefault(); this.close(); }
      return;
    }
    const next = nextFocusIndex(event.key, at, links.length);
    if (next !== null) { event.preventDefault(); links[next].focus(); }
  }

  private show(next: NavFlyout, moveFocus = false): void {
    this.current.set(next);
    afterNextRender(() => {
      this.host.overlay()?.overlayRef?.updatePosition(); // the height changes with the category
      if (!moveFocus) return;
      // routerLinkActive marks the current page's link in a microtask.
      queueMicrotask(() => {
        const panel = this.host.panel()?.nativeElement;
        (panel?.querySelector<HTMLElement>('a.is-active') ?? panel?.querySelector<HTMLElement>('a'))?.focus();
      });
    }, { injector: this.injector });
  }
}
