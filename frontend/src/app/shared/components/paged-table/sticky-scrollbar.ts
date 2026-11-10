import { NgZone, signal } from '@angular/core';

/**
 * The sticky horizontal scrollbar under a wide <erp-paged-table>: a proxy bar kept in step with nz-table's scrolling body, so the
 * table can be scrolled sideways without first scrolling to its end. Call `attach` after every render and `detach` on destroy.
 */
export class StickyScrollbar {
  /** The body scrolls sideways (the sticky scrollbar is shown). */
  readonly overflowX = signal(false);
  readonly scrollWidth = signal(0);
  /** Negative padding-bottom of the page's scroll container, so the sticky footer sits flush with the viewport edge. */
  readonly stickyBottom = signal(0);
  private body: HTMLElement | null = null;
  private proxy: HTMLElement | null = null;
  private resize?: ResizeObserver;
  private readonly onBodyScroll = () => this.sync(this.body, this.proxy);
  private readonly onProxyScroll = () => this.sync(this.proxy, this.body);

  constructor(private readonly zone: NgZone) {}

  /** Finds nz-table's scrolling element under `root` (it is re-created when the scroll config changes) and binds the proxy bar to it. */
  attach(root: HTMLElement | undefined, proxy: HTMLElement | null): void {
    const body = root?.querySelector<HTMLElement>('.ant-table-body, .ant-table-content') ?? null;
    if (body === this.body && proxy === this.proxy) { this.measure(); return; }
    this.detach();
    this.body = body; this.proxy = proxy;
    if (!body || !proxy) return;
    this.stickyBottom.set(-scrollerPadding(root!));
    this.zone.runOutsideAngular(() => {
      body.addEventListener('scroll', this.onBodyScroll, { passive: true });
      proxy.addEventListener('scroll', this.onProxyScroll, { passive: true });
      if (typeof ResizeObserver !== 'undefined') {
        this.resize = new ResizeObserver(() => this.zone.run(() => this.measure()));
        this.resize.observe(body);
        const table = body.querySelector('table');
        if (table) this.resize.observe(table);
        if (proxy) this.resize.observe(proxy);
      }
    });
    this.measure();
  }

  detach(): void {
    this.body?.removeEventListener('scroll', this.onBodyScroll);
    this.proxy?.removeEventListener('scroll', this.onProxyScroll);
    this.resize?.disconnect();
    this.resize = undefined;
    this.body = this.proxy = null;
  }

  /** Scrolls the table body sideways by `left` pixels; false when there is no body yet. */
  scrollBy(left: number): boolean {
    if (!this.body) return false;
    this.body.scrollBy({ left, behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    return true;
  }

  private measure(): void {
    const b = this.body;
    const over = !!b && b.scrollWidth - b.clientWidth > 1;
    if (over !== this.overflowX()) this.overflowX.set(over);
    // the proxy can be narrower than the body (e.g. a vertical scrollbar), so match the scroll range, not the raw width
    const w = b ? b.scrollWidth - b.clientWidth + (this.proxy?.clientWidth || b.clientWidth) : 0;
    if (w !== this.scrollWidth()) this.scrollWidth.set(w);
    if (over) this.sync(b, this.proxy);
  }

  /** Copies the raw scrollLeft (both elements share the same direction, so this is RTL-safe); equal values stop the echo. */
  private sync(from: HTMLElement | null, to: HTMLElement | null): void {
    if (!from || !to || Math.abs(from.scrollLeft - to.scrollLeft) < 1) return;
    to.scrollLeft = from.scrollLeft;
  }
}

/** The bottom padding of the nearest scrolling ancestor. */
function scrollerPadding(el: HTMLElement): number {
  for (let e = el.parentElement; e; e = e.parentElement) {
    const cs = getComputedStyle(e);
    if (/(auto|scroll)/.test(cs.overflowY)) return parseFloat(cs.paddingBottom) || 0;
  }
  return 0;
}
