import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { PosOrderDto, PosOrderLineDto, PosOrdersApi, PosPaymentMethod, PosReturnDto, PosReturnsApi } from '@core/api/pos.api';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { returnAmount, round2, roundQty, splitRefund, TENDER_LABELS } from '../../pos-cart';

/** One line of the sale being returned: how much is left to return, how much comes back now and whether it goes back on the shelf. */
interface ReturnRow {
  line: PosOrderLineDto;
  left: number;
  quantity: number;
  restock: boolean;
}

/** A customer brings goods back: find the sale by its receipt (scanned or typed), choose what came back, give the money back. */
@Component({
  selector: 'erp-pos-return-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, ErpDatePipe, NzAlertModule, NzButtonModule, NzIconModule, NzInputModule, NzModalModule, NzSwitchModule],
  templateUrl: './pos-return-dialog.component.html',
  styleUrl: './pos-return-dialog.component.scss',
})
export class PosReturnDialogComponent {
  private readonly ordersApi = inject(PosOrdersApi);
  private readonly returnsApi = inject(PosReturnsApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  /** Without this permission money goes back only the way it came: each tender refunds what it took, less earlier refunds. */
  readonly canOverride = computed(() => this.session.hasPermission('pos.orders.refund-override'));
  private readonly numberBox = viewChild<ElementRef<HTMLInputElement>>('numberBox');

  readonly open = input(false);
  readonly sessionId = input<string | null>(null);
  readonly currency = input('');
  /** Opens straight on this receipt. */
  readonly initialNumber = input<string | null>(null);
  readonly closed = output<void>();
  readonly done = output<PosReturnDto>();

  readonly number = signal('');
  readonly order = signal<PosOrderDto | null>(null);
  readonly rows = signal<ReturnRow[]>([]);
  readonly reason = signal('');
  readonly method = signal<PosPaymentMethod>('cash');
  readonly reference = signal('');
  readonly finding = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  private idempotencyKey = crypto.randomUUID();

  readonly nothingLeft = computed(() => this.rows().every((r) => r.left <= 0));
  readonly total = computed(() => round2(this.rows().reduce((sum, r) => sum + this.amountOf(r), 0)));
  /** What each way of paying took on this sale (change given back already taken off the cash). */
  readonly tenders = computed(() => {
    const taken = new Map<PosPaymentMethod, number>();
    for (const p of this.order()?.payments ?? []) taken.set(p.method, round2((taken.get(p.method) ?? 0) + p.amount));
    return [...taken].map(([method, amount]) => ({ method, amount }));
  });
  /** The ways this sale was paid, plus (for someone with the override) cash and card. Anyone else can only send money back the way it came. */
  readonly methods = computed<PosPaymentMethod[]>(() => {
    const o = this.order();
    const paid = (o?.payments ?? []).map((p) => p.method).filter((m) => m !== 'giftCard');
    const set = new Set<PosPaymentMethod>(paid);
    if (this.canOverride()) { set.add('cash'); set.add('card'); }
    if (!set.size) set.add('cash');
    if (o?.invoiceId) set.add('account');
    else set.delete('account');
    return [...set];
  });
  /** The chosen way took less than is being refunded, and the person cannot override: the server would refuse with 403. */
  readonly overTender = computed(() => {
    if (this.canOverride() || this.method() === 'account') return null;
    const took = this.tenders().find((t) => t.method === this.method())?.amount ?? 0;
    return this.total() > took ? took : null;
  });
  /** The first way this sale was paid other than on account, for the part of a refund the account cannot take. */
  private readonly moneyMethod = computed<PosPaymentMethod>(() => {
    const paid = (this.order()?.payments ?? []).map((p) => p.method).find((m) => m !== 'account' && m !== 'giftCard');
    return paid ?? 'cash';
  });
  readonly refunds = computed(() => {
    const o = this.order();
    if (!o || this.total() <= 0) return [];
    return splitRefund(this.total(), this.method(), o.moneyLeftToRefund, o.invoiceId ? o.onAccountLeft : 0, this.moneyMethod(), this.reference().trim() || null);
  });
  readonly canSubmit = computed(() => this.session.hasPermission('pos.orders.refund') && !this.busy() && !!this.sessionId() && this.total() > 0 && !!this.reason().trim() && this.order()?.status === 'completed' && this.overTender() === null);

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const initial = this.initialNumber();
      untracked(() => {
        this.reset();
        if (initial) {
          this.number.set(initial);
          this.find();
        }
      });
    });
  }

  private reset(): void {
    this.number.set('');
    this.order.set(null);
    this.rows.set([]);
    this.reason.set('');
    this.reference.set('');
    this.error.set(null);
    this.method.set('cash');
    this.idempotencyKey = crypto.randomUUID();
  }

  focusSearch(): void {
    if (!this.order()) setTimeout(() => this.numberBox()?.nativeElement.focus());
  }

  find(): void {
    const number = this.number().trim();
    if (!number) return;
    this.finding.set(true);
    this.error.set(null);
    this.ordersApi.byNumber(number).subscribe({
      next: (o) => {
        this.finding.set(false);
        this.order.set(o);
        this.rows.set(o.lines.map((line) => {
          const left = roundQty(line.quantity - line.returnedQuantity);
          return { line, left, quantity: 0, restock: !!line.itemId };
        }));
        // Back the way it was paid: on account when it was, else the first tender.
        const first = o.payments[0]?.method;
        this.method.set(o.invoiceId ? 'account' : first && first !== 'giftCard' ? first : 'cash');
      },
      error: (err) => {
        this.finding.set(false);
        this.order.set(null);
        this.rows.set([]);
        this.error.set(problemMessage(err, 'That receipt could not be found.'));
      },
    });
  }

  amountOf(r: ReturnRow): number {
    return returnAmount(r.line, r.quantity);
  }

  setQty(index: number, quantity: number): void {
    this.rows.update((all) => all.map((r, i) => (i === index ? { ...r, quantity: Math.min(r.left, Math.max(0, quantity)) } : r)));
  }

  setRestock(index: number, restock: boolean): void {
    this.rows.update((all) => all.map((r, i) => (i === index ? { ...r, restock } : r)));
  }

  label(method: PosPaymentMethod): string {
    return TENDER_LABELS[method];
  }

  submit(): void {
    const o = this.order();
    const session = this.sessionId();
    if (!o || !session || !this.canSubmit()) return;
    this.busy.set(true);
    this.error.set(null);
    this.returnsApi.submit({
      orderId: o.id,
      sessionId: session,
      reason: this.reason().trim(),
      lines: this.rows().filter((r) => r.quantity > 0).map((r) => ({ orderLineId: r.line.id, quantity: r.quantity, restock: r.restock })),
      refunds: this.refunds(),
    }, this.idempotencyKey).subscribe({
      next: (ret) => {
        this.busy.set(false);
        this.done.emit(ret);
      },
      error: (err) => {
        this.busy.set(false);
        const text = problemMessage(err, 'The return could not be saved.');
        this.error.set(text);
        this.message.error(text);
      },
    });
  }
}
