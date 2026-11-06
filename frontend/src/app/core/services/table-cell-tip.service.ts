import { DOCUMENT, Injectable, NgZone, inject } from '@angular/core';

const CELL = '.ant-table-tbody > tr > td, .erp-truncate';
const SHOW_DELAY_MS = 150;
const GAP_PX = 6;
const EDGE_PX = 8;

/**
 * Shows the full text when the pointer rests on a table cell that was cut short with an ellipsis, in any table. One listener on the
 * document does this, so rows do not each carry a tooltip. It also marks a table that has been scrolled sideways, so the pinned first
 * column can draw its edge.
 */
@Injectable({ providedIn: 'root' })
export class TableCellTipService {
  private readonly doc = inject(DOCUMENT);
  private readonly zone = inject(NgZone);
  private tip: HTMLElement | null = null;
  private current: Element | null = null;
  private timer?: ReturnType<typeof setTimeout>;
  private started = false;

  start(): void {
    if (this.started) return;
    this.started = true;
    this.zone.runOutsideAngular(() => {
      this.doc.addEventListener('mouseover', this.onOver, { passive: true });
      this.doc.addEventListener('scroll', this.onScroll, { passive: true, capture: true });
      this.doc.addEventListener('click', this.hide, { passive: true, capture: true });
    });
  }

  private readonly onOver = (e: Event): void => {
    const cell = e.target instanceof Element ? e.target.closest(CELL) : null;
    if (cell === this.current) return;
    this.hide();
    if (!cell || cell.scrollWidth - cell.clientWidth < 1) return;
    this.current = cell;
    this.timer = setTimeout(() => this.show(cell), SHOW_DELAY_MS);
  };

  private readonly onScroll = (e: Event): void => {
    this.hide();
    const el = e.target;
    if (el instanceof HTMLElement && el.classList.contains('ant-table-content')) el.classList.toggle('erp-scrolled', Math.abs(el.scrollLeft) > 0);
  };

  private readonly hide = (): void => {
    clearTimeout(this.timer);
    this.current = null;
    if (this.tip) this.tip.hidden = true;
  };

  private show(cell: Element): void {
    const text = cell.textContent?.trim();
    if (!text) return;
    const tip = this.tip ?? this.create();
    tip.textContent = text;
    tip.hidden = false;
    const box = cell.getBoundingClientRect();
    const width = tip.offsetWidth;
    const height = tip.offsetHeight;
    const viewport = this.doc.documentElement.clientWidth;
    const rtl = this.doc.documentElement.dir === 'rtl';
    const preferred = rtl ? box.right - width : box.left;
    tip.style.left = `${Math.max(EDGE_PX, Math.min(preferred, viewport - width - EDGE_PX))}px`;
    tip.style.top = `${box.top - height - GAP_PX >= EDGE_PX ? box.top - height - GAP_PX : box.bottom + GAP_PX}px`;
  }

  private create(): HTMLElement {
    const tip = this.doc.createElement('div');
    tip.className = 'erp-cell-tip';
    tip.setAttribute('role', 'tooltip');
    tip.setAttribute('data-testid', 'table-cell-tip');
    tip.hidden = true;
    this.doc.body.appendChild(tip);
    this.tip = tip;
    return tip;
  }
}
