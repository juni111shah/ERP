import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '../../core/format/done-text';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { CountDetail, CountLine, CountsApi } from './inventory.api';
import { ErpDatePipe } from '../../shared/display.pipes';

/** Count sheet: enter what was found next to the system quantity, see the differences, then post them as adjustments. */
@Component({
  selector: 'erp-count-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, FormsModule, RouterLink, HasPermissionDirective, StatePanelComponent, StatusTagComponent, NzButtonModule, NzInputNumberModule, NzModalModule, NzTableModule],
  styles: [`.num { text-align: end; white-space: nowrap; } .less { color: var(--erp-color-error-text); } .more { color: var(--erp-color-success-text); } .progress { color: var(--erp-color-text-secondary); margin: -8px 0 12px; } .sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }`],
  template: `
    <a routerLink="/inventory/counts">&larr; Stock counts</a>
    @if (failed()) {
      <erp-state-panel kind="error" title="This count could not be loaded" hint="It may have been removed, or your connection dropped." actionLabel="Try again" (action)="reload()" />
    }
    @if (count(); as c) {
      <div style="display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:8px 0 16px">
        <h1 style="margin:0;font-size:20px;font-weight:600">{{ c.number }}</h1>
        <erp-status [value]="c.status" data-testid="count-status" />
        <span>{{ c.warehouse }} &middot; {{ c.countDate | erpDate }}</span>
        <span style="flex:1"></span>
        @if (editable()) {
          <ng-container *erpHasPermission="'inventory.counts.manage'">
            <button nz-button (click)="save()" [nzLoading]="busy()" data-testid="count-save">Save counts</button>
            <button nz-button nzType="primary" (click)="post()" [nzLoading]="busy()" data-testid="count-post">Post differences</button>
            <button nz-button nzDanger (click)="cancel()" data-testid="count-cancel">Cancel count</button>
          </ng-container>
        }
      </div>
      @if (c.lines.length) { <p class="progress" data-testid="count-progress">{{ progress().counted }} of {{ c.lines.length }} items counted · {{ progress().differences }} with a difference</p> }
      <nz-table [nzData]="c.lines" nzSize="small" [nzFrontPagination]="false" [nzShowPagination]="false" nzNoResult="This warehouse holds no stock, so there is nothing to count." data-testid="count-lines">
        <thead><tr><th>SKU</th><th>Item</th><th class="num">System</th><th class="num">Counted</th><th class="num">Difference</th></tr></thead>
        <tbody>
          @for (l of c.lines; track l.itemId) {
            <tr>
              <td><span class="code-pill">{{ l.sku }}</span></td><td>{{ l.item }}</td>
              <td class="num">{{ l.systemQuantity | number: '1.0-4' }} {{ l.uom }}</td>
              <td class="num">
                @if (editable()) {
                  <label class="sr" [for]="'counted-' + l.sku">Counted, {{ l.item }}</label>
                  <nz-input-number [nzId]="'counted-' + l.sku" [ngModel]="counted()[l.itemId]" (ngModelChange)="setCounted(l.itemId, $event)" [nzMin]="0" [attr.data-testid]="'counted-' + l.sku"></nz-input-number>
                }
                @else { {{ l.countedQuantity === null || l.countedQuantity === undefined ? '' : (l.countedQuantity | number: '1.0-4') }} }
              </td>
              <td class="num" [class.less]="difference(l) < 0" [class.more]="difference(l) > 0">{{ counted()[l.itemId] === null || counted()[l.itemId] === undefined ? '' : (difference(l) | number: '1.0-4') }}</td>
            </tr>
          }
        </tbody>
      </nz-table>
    }`,
})
export class CountDetailPage {
  readonly id = input.required<string>();
  private readonly api = inject(CountsApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly count = signal<CountDetail | null>(null);
  readonly counted = signal<Record<string, number | null | undefined>>({});
  readonly busy = signal(false);
  readonly failed = signal(false);
  readonly editable = computed(() => ['draft', 'counting'].includes(this.count()?.status ?? ''));
  readonly progress = computed(() => {
    const lines = this.count()?.lines ?? [];
    const entered = lines.filter((l) => this.counted()[l.itemId] !== null && this.counted()[l.itemId] !== undefined);
    return { counted: entered.length, differences: entered.filter((l) => this.counted()[l.itemId] !== l.systemQuantity).length };
  });

  constructor() { effect(() => { this.fetch(this.id()); }); }

  reload(): void { this.fetch(this.id()); }
  private fetch(id: string): void { this.failed.set(false); this.api.detail(id).subscribe({ next: (c) => this.apply(c), error: () => this.failed.set(true) }); }

  private apply(c: CountDetail): void {
    this.count.set(c);
    this.counted.set(Object.fromEntries(c.lines.map((l) => [l.itemId, l.countedQuantity ?? null])));
  }

  setCounted(itemId: string, value: number | null): void { this.counted.update((m) => ({ ...m, [itemId]: value })); }
  difference(l: CountLine): number { return (this.counted()[l.itemId] ?? 0) - l.systemQuantity; }

  private fail(e: unknown, fallback: string): void { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? fallback); }

  private body(): { itemId: string; countedQuantity: number | null }[] {
    return Object.entries(this.counted()).map(([itemId, countedQuantity]) => ({ itemId, countedQuantity: countedQuantity ?? null }));
  }

  save(after?: () => void): void {
    this.busy.set(true);
    this.api.saveLines(this.id(), this.body()).subscribe({ next: (c) => { this.busy.set(false); this.apply(c); this.message.success(`Counts saved for ${c.number}`); after?.(); }, error: (e) => this.fail(e, 'Could not save the counts') });
  }

  post(): void {
    this.modal.confirm({
      nzTitle: 'Post the differences?', nzContent: 'One stock adjustment is booked for every item whose counted quantity differs from the system. This cannot be undone.', nzOkText: 'Post',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.api.saveLines(this.id(), this.body()).subscribe({
          next: () => this.api.post(this.id()).subscribe({ next: (c) => { this.apply(c); this.message.success(doneText('posted', 'Stock count', c.number)); resolve(); }, error: (e) => { this.fail(e, 'Could not post the count'); resolve(); } }),
          error: (e) => { this.fail(e, 'Could not save the counts'); resolve(); },
        });
      }),
    });
  }

  cancel(): void {
    this.modal.confirm({
      nzTitle: 'Cancel this count?', nzContent: 'The counted figures are kept on the sheet, but nothing is posted to stock.', nzOkText: 'Cancel count', nzOkDanger: true, nzCancelText: 'Keep counting',
      nzOnOk: () => new Promise<void>((resolve) => this.api.cancel(this.id()).subscribe({ next: (c) => { this.apply(c); this.message.success(doneText('cancelled', 'Stock count', c.number)); resolve(); }, error: (e) => { this.fail(e, 'Could not cancel'); resolve(); } })),
    });
  }
}
