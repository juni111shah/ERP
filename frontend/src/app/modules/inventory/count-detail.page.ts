import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { CountDetail, CountLine, CountsApi } from './inventory.api';

/** Count sheet: enter what was found next to the system quantity, see the differences, then post them as adjustments. */
@Component({
  selector: 'erp-count-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterLink, HasPermissionDirective, NzButtonModule, NzInputNumberModule, NzTableModule, NzTagModule],
  template: `
    <a routerLink="/inventory/counts">&larr; Stock counts</a>
    @if (count(); as c) {
      <div style="display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin:8px 0 16px">
        <h1 class="erp-page-title" style="margin:0">{{ c.number }}</h1>
        <nz-tag [nzColor]="c.status === 'posted' ? 'green' : c.status === 'cancelled' ? 'default' : 'blue'" data-testid="count-status">{{ c.status }}</nz-tag>
        <span>{{ c.warehouse }} &middot; {{ c.countDate }}</span>
        <span style="flex:1"></span>
        @if (editable()) {
          <ng-container *erpHasPermission="'inventory.counts.manage'">
            <button nz-button (click)="save()" [nzLoading]="busy()" data-testid="count-save">Save counts</button>
            <button nz-button nzType="primary" (click)="post()" [nzLoading]="busy()" data-testid="count-post">Post differences</button>
            <button nz-button nzDanger (click)="cancel()" data-testid="count-cancel">Cancel count</button>
          </ng-container>
        }
      </div>
      <nz-table [nzData]="c.lines" nzSize="small" [nzShowPagination]="false" data-testid="count-lines">
        <thead><tr><th>SKU</th><th>Item</th><th style="text-align:right">System</th><th style="text-align:right">Counted</th><th style="text-align:right">Difference</th></tr></thead>
        <tbody>
          @for (l of c.lines; track l.itemId) {
            <tr>
              <td>{{ l.sku }}</td><td>{{ l.item }}</td>
              <td style="text-align:right">{{ l.systemQuantity | number: '1.0-4' }} {{ l.uom }}</td>
              <td style="text-align:right">
                @if (editable()) { <nz-input-number [ngModel]="counted()[l.itemId]" (ngModelChange)="setCounted(l.itemId, $event)" [nzMin]="0" [nzPrecision]="4" [attr.data-testid]="'counted-' + l.sku"></nz-input-number> }
                @else { {{ l.countedQuantity === null || l.countedQuantity === undefined ? '' : (l.countedQuantity | number: '1.0-4') }} }
              </td>
              <td style="text-align:right" [style.color]="difference(l) < 0 ? '#cf1322' : difference(l) > 0 ? '#389e0d' : ''">{{ counted()[l.itemId] === null || counted()[l.itemId] === undefined ? '' : (difference(l) | number: '1.0-4') }}</td>
            </tr>
          } @empty { <tr><td colspan="5">This warehouse holds no stock. Use "Save counts" after adding an item below.</td></tr> }
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
  readonly editable = computed(() => ['draft', 'counting'].includes(this.count()?.status ?? ''));

  constructor() { effect(() => { const id = this.id(); this.api.detail(id).subscribe((c) => this.apply(c)); }); }

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
    this.api.saveLines(this.id(), this.body()).subscribe({ next: (c) => { this.busy.set(false); this.apply(c); this.message.success('Counts saved'); after?.(); }, error: (e) => this.fail(e, 'Could not save the counts') });
  }

  post(): void {
    this.modal.confirm({
      nzTitle: 'Post the differences?', nzContent: 'One stock adjustment is booked for every item whose counted quantity differs from the system. This cannot be undone.', nzOkText: 'Post',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.api.saveLines(this.id(), this.body()).subscribe({
          next: () => this.api.post(this.id()).subscribe({ next: (c) => { this.apply(c); this.message.success('Count posted'); resolve(); }, error: (e) => { this.fail(e, 'Could not post the count'); resolve(); } }),
          error: (e) => { this.fail(e, 'Could not save the counts'); resolve(); },
        });
      }),
    });
  }

  cancel(): void { this.api.cancel(this.id()).subscribe({ next: (c) => { this.apply(c); this.message.success('Count cancelled'); }, error: (e) => this.fail(e, 'Could not cancel') }); }
}
