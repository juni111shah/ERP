import { AfterViewChecked, DestroyRef, Directive, ElementRef, NgModule, inject } from '@angular/core';
import { NzTableComponent, NzTableModule } from 'ng-zorro-antd/table';

const MAX_ROWS = 8;
const CELL_WIDTHS = ['72%', '54%', '86%', '40%', '63%'];

/**
 * A table that is loading draws rows of its own columns instead of ng-zorro's spinner (hidden in theme/_skeleton.scss). A table that
 * already has rows keeps them, dimmed, until the new ones arrive, so nothing jumps.
 */
@Directive({ selector: 'nz-table[nzLoading]', standalone: true })
export class TableSkeletonDirective implements AfterViewChecked {
  private readonly table = inject(NzTableComponent, { self: true });
  private readonly el = inject(ElementRef<HTMLElement>).nativeElement as HTMLElement;
  private rows: HTMLElement[] = [];

  constructor() {
    inject(DestroyRef).onDestroy(() => this.clear());
  }

  ngAfterViewChecked(): void {
    const loading = !!this.table.nzLoading;
    this.el.setAttribute('aria-busy', String(loading));
    const empty = loading && !this.table.nzData?.length;
    this.el.classList.toggle('erp-table-skeleton', empty);
    if (!empty) { this.clear(); return; }
    if (this.rows.some((r) => r.isConnected)) return;
    this.draw();
  }

  private draw(): void {
    this.clear();
    const body = this.el.querySelector<HTMLElement>('.ant-table-tbody');
    const headers = Array.from(this.el.querySelectorAll<HTMLElement>('.ant-table-thead > tr:last-child > th'));
    if (!body || !headers.length) return;
    const count = Math.min(this.table.nzPageSize || MAX_ROWS, MAX_ROWS);
    for (let r = 0; r < count; r++) {
      const tr = document.createElement('tr');
      tr.className = 'ant-table-row erp-table-skeleton__row';
      tr.setAttribute('aria-hidden', 'true');
      headers.forEach((th, c) => {
        const td = document.createElement('td');
        td.className = 'ant-table-cell';
        td.style.textAlign = th.style.textAlign;
        const bar = document.createElement('span');
        bar.className = 'erp-skel erp-skel--cell';
        bar.style.width = CELL_WIDTHS[(r + c) % CELL_WIDTHS.length];
        td.appendChild(bar);
        tr.appendChild(td);
      });
      body.appendChild(tr);
      this.rows.push(tr);
    }
  }

  private clear(): void {
    for (const row of this.rows) row.remove();
    this.rows = [];
  }
}

/** NzTableModule plus loading rows. Pages with a loading table import this one. */
@NgModule({
  imports: [NzTableModule, TableSkeletonDirective],
  exports: [NzTableModule, TableSkeletonDirective],
})
export class ErpTableModule {}
