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
import { PosOrderDto, PosOrderLineDto, PosOrdersApi, PosPaymentMethod, PosReturnDto, PosReturnsApi } from '../../core/api/pos.api';
import { problemMessage } from '../../core/http/problem-details';
import { ErpDatePipe } from '../../shared/display.pipes';
import { SessionStore } from '../../core/session/session.store';
import { returnAmount, round2, roundQty, splitRefund, TENDER_LABELS } from './pos-cart';

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
  template: `
    <nz-modal [nzVisible]="open()" nzTitle="Return against a receipt" [nzWidth]="720" [nzFooter]="footer" [nzMaskClosable]="false" (nzAfterOpen)="focusSearch()" (nzOnCancel)="closed.emit()">
      <ng-container *nzModalContent>
        <div class="stack">
          <form class="find" (ngSubmit)="find()">
            <span nz-icon nzType="barcode" class="find-icon"></span>
            <input #numberBox nz-input nzSize="large" name="receipt" aria-label="Receipt number" placeholder="Scan the receipt or type its number" autocomplete="off"
              [ngModel]="number()" (ngModelChange)="number.set($event)" data-testid="pos-return-number" />
            <button nz-button nzSize="large" type="submit" [nzLoading]="finding()" data-testid="pos-return-find">Find</button>
          </form>

          @if (error(); as e) { <nz-alert nzType="error" nzShowIcon [nzMessage]="e" data-testid="pos-return-error"></nz-alert> }

          @if (order(); as o) {
            <div class="sale">
              <strong>{{ o.number }}</strong>
              <span>{{ o.orderDate | erpDate }} · {{ o.customerName }} · {{ currency() }} {{ o.totalAmount | number:'1.2-2' }}</span>
              @if (o.returnedTotal > 0) { <span class="muted">Already returned: {{ currency() }} {{ o.returnedTotal | number:'1.2-2' }}</span> }
            </div>

            @if (o.status !== 'completed') {
              <nz-alert nzType="warning" nzShowIcon nzMessage="Only a completed sale can be returned."></nz-alert>
            } @else if (!rows().length || nothingLeft()) {
              <nz-alert nzType="info" nzShowIcon nzMessage="Everything on this sale has already been returned."></nz-alert>
            } @else {
              <div class="rows">
                @for (r of rows(); track r.line.id; let i = $index) {
                  <div class="row" [class.off]="r.left === 0">
                    <div class="what">
                      <span class="name">{{ r.line.description }}</span>
                      <small class="muted">{{ currency() }} {{ r.line.unitPrice | number:'1.2-2' }} @if (r.line.discountPercent) { · -{{ r.line.discountPercent }}% } · {{ r.left | number:'1.0-2' }} of {{ r.line.quantity | number:'1.0-2' }} can be returned</small>
                    </div>
                    <div class="qty">
                      <button type="button" class="step" [disabled]="r.quantity <= 0" [attr.aria-label]="'One less ' + r.line.description" (click)="setQty(i, r.quantity - 1)">−</button>
                      <span class="qty-val" [attr.data-testid]="'pos-return-qty-' + i">{{ r.quantity | number:'1.0-2' }}</span>
                      <button type="button" class="step" [disabled]="r.quantity >= r.left" [attr.aria-label]="'One more ' + r.line.description" (click)="setQty(i, r.quantity + 1)" [attr.data-testid]="'pos-return-more-' + i">+</button>
                    </div>
                    <label class="restock"><nz-switch nzSize="small" [ngModel]="r.restock" (ngModelChange)="setRestock(i, $event)" [nzDisabled]="!r.line.itemId"></nz-switch> Back on shelf</label>
                    <strong class="amt">{{ amountOf(r) | number:'1.2-2' }}</strong>
                  </div>
                }
              </div>

              <div>
                <label class="field" for="pos-return-reason">Why did it come back?</label>
                <input id="pos-return-reason" nz-input aria-label="Why did it come back?" [ngModel]="reason()" (ngModelChange)="reason.set($event)" maxlength="500" placeholder="e.g. Wrong size" data-testid="pos-return-reason" />
              </div>

              <div>
                <span class="field">Give the money back as</span>
                <p class="split" data-testid="pos-return-tenders">
                  This sale was paid with
                  @for (t of tenders(); track t.method; let last = $last) { {{ label(t.method) }} {{ currency() }} {{ t.amount | number:'1.2-2' }}@if (!last) { , } }.
                  Money goes back the way it came: each way of paying can be refunded what it took, less earlier refunds.
                  @if (!canOverride()) { Sending it another way needs a manager's refund override. }
                </p>
                <div class="methods" role="group" aria-label="Refund method">
                  @for (m of methods(); track m) {
                    <button type="button" class="method" [class.on]="method() === m" [attr.aria-pressed]="method() === m" [attr.data-testid]="'pos-refund-' + m" (click)="method.set(m)">{{ label(m) }}</button>
                  }
                </div>
                @if (method() === 'card' || method() === 'other') {
                  <input class="ref" nz-input placeholder="Reference (optional)" aria-label="Refund reference" [ngModel]="reference()" (ngModelChange)="reference.set($event)" />
                }
              </div>

              @if (overTender() !== null) {
                <nz-alert nzType="warning" nzShowIcon data-testid="pos-return-over" [nzMessage]="label(method()) + ' took only ' + currency() + ' ' + (overTender()! | number:'1.2-2') + ' on this sale, so it cannot be refunded more than that without a manager. Return less, or ask a manager.'"></nz-alert>
              }
              <div class="total" data-testid="pos-return-total">
                <span>To give back</span>
                <strong>{{ currency() }} {{ total() | number:'1.2-2' }}</strong>
              </div>
              @if (refunds().length > 1) {
                <p class="split" data-testid="pos-return-split">
                  This sale was paid part on account, so it goes back the same way:
                  @for (f of refunds(); track f.method; let last = $last) { {{ label(f.method) }} {{ currency() }} {{ f.amount | number:'1.2-2' }}@if (!last) { and } }
                </p>
              }
            }
          }
        </div>
      </ng-container>
      <ng-template #footer>
        <button nz-button nzSize="large" (click)="closed.emit()">Cancel</button>
        <button nz-button nzType="primary" nzDanger nzSize="large" [nzLoading]="busy()" [disabled]="!canSubmit()" (click)="submit()" data-testid="pos-return-confirm">
          <span nz-icon nzType="rollback"></span>Refund
        </button>
      </ng-template>
    </nz-modal>
  `,
  styles: [`
    .stack { display: flex; flex-direction: column; gap: 14px; }
    .find { display: flex; align-items: center; gap: 8px; }
    .find-icon { font-size: 22px; color: var(--erp-color-text-muted); }
    .sale { display: flex; flex-direction: column; padding: 8px 12px; border-radius: 8px; background: var(--erp-color-bg-subtle); }
    .muted { color: var(--erp-color-text-muted); }
    .rows { display: flex; flex-direction: column; gap: 6px; }
    .row { display: grid; grid-template-columns: 1fr auto auto 76px; align-items: center; gap: 12px; padding: 8px 10px; border: 1px solid var(--erp-color-border-subtle); border-radius: 8px; }
    .row.off { opacity: .55; }
    .what { display: flex; flex-direction: column; min-width: 0; }
    .name { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .qty { display: flex; align-items: center; gap: 6px; }
    .step { width: 40px; height: 40px; font-size: 18px; font-weight: 700; color: var(--erp-color-text-primary); background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-default); border-radius: 8px; cursor: pointer; }
    .step:disabled { opacity: .4; cursor: not-allowed; }
    .qty-val { min-width: 26px; text-align: center; font-weight: 700; font-variant-numeric: tabular-nums; }
    .restock { display: flex; align-items: center; gap: 6px; font-size: 12px; white-space: nowrap; }
    .amt { text-align: end; font-variant-numeric: tabular-nums; }
    .field { display: block; margin-bottom: 4px; font-weight: 600; }
    .methods { display: flex; flex-wrap: wrap; gap: 8px; }
    .method { flex: 1 1 90px; min-height: 46px; font: inherit; font-weight: 600; color: var(--erp-color-text-primary); background: var(--erp-color-bg-surface); border: 2px solid var(--erp-color-border-default); border-radius: 8px; cursor: pointer; }
    .method.on { border-color: var(--erp-color-primary); background: var(--erp-color-primary-subtle); color: var(--erp-color-primary-text); }
    .ref { margin-top: 8px; }
    .total { display: flex; justify-content: space-between; align-items: baseline; padding: 10px 12px; border-radius: 8px; background: var(--erp-color-error-bg); color: var(--erp-color-error-text); }
    .total strong { font-size: 24px; font-variant-numeric: tabular-nums; }
    .split { margin: 0; font-size: 13px; color: var(--erp-color-text-secondary); }
    @media (max-width: 640px) { .row { grid-template-columns: 1fr auto; } }
  `],
})
export class PosReturnDialogComponent {
  private readonly orders = inject(PosOrdersApi);
  private readonly returns = inject(PosReturnsApi);
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
  readonly canSubmit = computed(() => !this.busy() && !!this.sessionId() && this.total() > 0 && !!this.reason().trim() && this.order()?.status === 'completed' && this.overTender() === null);

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
    this.orders.byNumber(number).subscribe({
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
    this.returns.submit({
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
