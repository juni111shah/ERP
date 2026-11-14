import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { CountDetail, CountedQuantityInput, CountLine, CountsApi } from '@core/api/inventory.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** The counted quantity entered per item id; null or missing is not counted yet. */
type CountedMap = Record<string, number | null | undefined>;

/** How many lines have a count, and how many of those differ from the system quantity. */
function countProgress(lines: CountLine[], counted: CountedMap): { counted: number; differences: number } {
  const entered = lines.filter((l) => counted[l.itemId] !== null && counted[l.itemId] !== undefined);
  return { counted: entered.length, differences: entered.filter((l) => counted[l.itemId] !== l.systemQuantity).length };
}

/** Count sheet: enter what was found next to the system quantity, see the differences, then post them as adjustments. */
@Component({
  selector: 'erp-count-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, FormsModule, RouterLink, HasPermissionDirective, StatePanelComponent, StatusTagComponent, NzButtonModule, NzInputNumberModule, NzModalModule, NzTableModule],
  styleUrl: './count-detail.page.scss',
  templateUrl: './count-detail.page.html',
})
export class CountDetailPage {
  readonly id = input.required<string>();
  private readonly countsApi = inject(CountsApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  readonly count = signal<CountDetail | null>(null);
  readonly counted = signal<CountedMap>({});
  readonly busy = signal(false);
  readonly failed = signal(false);
  /** The sheet can be filled in (inputs, save, post, cancel) while it is open and by someone who may manage counts. */
  readonly editable = computed(() => this.session.hasPermission('inventory.counts.manage') && ['draft', 'counting'].includes(this.count()?.status ?? ''));
  readonly progress = computed(() => countProgress(this.count()?.lines ?? [], this.counted()));

  constructor() { effect(() => { this.fetch(this.id()); }); }

  reload(): void { this.fetch(this.id()); }

  private fetch(id: string): void {
    this.failed.set(false);
    this.countsApi.detail(id).subscribe({ next: (c) => this.apply(c), error: () => this.failed.set(true) });
  }

  private apply(c: CountDetail): void {
    this.count.set(c);
    this.counted.set(Object.fromEntries(c.lines.map((l) => [l.itemId, l.countedQuantity ?? null])));
  }

  setCounted(itemId: string, value: number | null): void { this.counted.update((m) => ({ ...m, [itemId]: value })); }
  difference(l: CountLine): number { return (this.counted()[l.itemId] ?? 0) - l.systemQuantity; }

  private fail(e: unknown, fallback: string): void { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? fallback); }

  private body(): CountedQuantityInput[] {
    return Object.entries(this.counted()).map(([itemId, countedQuantity]) => ({ itemId, countedQuantity: countedQuantity ?? null }));
  }

  save(after?: () => void): void {
    this.busy.set(true);
    this.countsApi.saveLines(this.id(), this.body()).subscribe({
      next: (c) => {
        this.busy.set(false);
        this.apply(c);
        this.message.success(`Counts saved for ${c.number}`);
        after?.();
      },
      error: (e) => this.fail(e, 'Could not save the counts'),
    });
  }

  post(): void {
    this.modal.confirm({
      nzTitle: 'Post the differences?', nzContent: 'One stock adjustment is booked for every item whose counted quantity differs from the system. This cannot be undone.', nzOkText: 'Post',
      nzOnOk: () => new Promise<void>((resolve) => this.saveAndPost(resolve)),
    });
  }

  /** The counts on screen are saved first, so what is posted is what the person sees. */
  private saveAndPost(resolve: () => void): void {
    this.countsApi.saveLines(this.id(), this.body()).subscribe({
      next: () => this.countsApi.post(this.id()).subscribe({
        next: (c) => { this.apply(c); this.message.success(doneText('posted', 'Stock count', c.number)); resolve(); },
        error: (e) => { this.fail(e, 'Could not post the count'); resolve(); },
      }),
      error: (e) => { this.fail(e, 'Could not save the counts'); resolve(); },
    });
  }

  cancel(): void {
    this.modal.confirm({
      nzTitle: 'Cancel this count?', nzContent: 'The counted figures are kept on the sheet, but nothing is posted to stock.', nzOkText: 'Cancel count', nzOkDanger: true, nzCancelText: 'Keep counting',
      nzOnOk: () => new Promise<void>((resolve) => this.countsApi.cancel(this.id()).subscribe({
        next: (c) => { this.apply(c); this.message.success(doneText('cancelled', 'Stock count', c.number)); resolve(); },
        error: (e) => { this.fail(e, 'Could not cancel'); resolve(); },
      })),
    });
  }
}
