import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { PosPaymentMethod } from '@core/api/pos.api';
import { changeDue, quickCash, remainingDue, round2, Tender, TENDER_LABELS, tendersSettle } from '../../pos-cart';

interface MethodButton {
  method: PosPaymentMethod;
  icon: string;
  key: string;
}

const METHODS: MethodButton[] = [
  { method: 'cash', icon: 'dollar', key: 'c' },
  { method: 'card', icon: 'credit-card', key: 'k' },
  { method: 'other', icon: 'swap', key: 'o' },
  { method: 'account', icon: 'book', key: 'a' },
];

/**
 * Taking the money. The fast path is one key: Enter takes the amount due in cash. Typing a bigger number and pressing Enter works out the
 * change; typing a smaller one adds that tender and asks for the rest, so a ticket can be split across cash, card and account.
 */
@Component({
  selector: 'erp-pos-payment-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzButtonModule, NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule],
  template: `
    <nz-modal [nzVisible]="open()" nzTitle="Take payment" [nzWidth]="620" [nzFooter]="footer" [nzMaskClosable]="false" (nzAfterOpen)="focusAmount()" (nzOnCancel)="cancelled.emit()">
      <ng-container *nzModalContent>
        <div class="pay" (keydown)="onKey($event)">
          <div class="due" [class.done]="remaining() === 0" data-testid="pos-due">
            <span>{{ remaining() === 0 ? (change() > 0 ? 'Change to give' : 'Paid in full') : (tenders().length ? 'Still to pay' : 'Amount due') }}</span>
            <strong>{{ currency() }} {{ (remaining() === 0 ? change() : remaining()) | number:'1.2-2' }}</strong>
            @if (tenders().length) { <small>of {{ currency() }} {{ total() | number:'1.2-2' }}</small> }
          </div>

          <div class="methods" role="group" aria-label="Payment method">
            @for (m of methods(); track m.method) {
              <button type="button" class="method" [class.on]="method() === m.method" [attr.aria-pressed]="method() === m.method" [attr.data-testid]="'pos-tender-' + m.method" (click)="choose(m.method)">
                <span nz-icon [nzType]="m.icon"></span>
                <span>{{ label(m.method) }}</span>
              </button>
            }
          </div>

          @if (remaining() > 0) {
            <div class="entry">
              <label class="field" for="pos-tender-amount">{{ method() === 'cash' ? 'Cash received' : label(method()) + ' amount' }}</label>
              <nz-input-number #amountBox nzId="pos-tender-amount" class="amount" [ngModel]="amount()" (ngModelChange)="amount.set($event ?? 0)" [nzMin]="0" [nzStep]="1" [nzPrecision]="2"
                (keydown.enter)="onEnter($event)" data-testid="pos-tendered"></nz-input-number>

              @if (method() === 'cash') {
                <div class="quick">
                  <button nz-button type="button" nzSize="large" (click)="takeCash(remaining())" data-testid="pos-exact">Exact</button>
                  @for (q of quick(); track q) { <button nz-button type="button" nzSize="large" (click)="takeCash(q)">{{ q | number:'1.0-2' }}</button> }
                </div>
              } @else if (method() === 'card' || method() === 'other') {
                <input class="ref" nz-input nzSize="large" [placeholder]="method() === 'card' ? 'Card approval reference (optional)' : 'Reference (optional)'" [ngModel]="reference()" (ngModelChange)="reference.set($event)" (keydown.enter)="onEnter($event)" aria-label="Reference" data-testid="pos-reference" />
                @if (method() === 'card') { <p class="note">Take the payment on the card machine first, then confirm here.</p> }
              } @else if (method() === 'account') {
                <p class="note" data-testid="pos-account-note">Goes on the account of <strong>{{ customerName() }}</strong> as an invoice.</p>
              }
              @if (blocked(); as why) { <p class="note warn" data-testid="pos-tender-blocked">{{ why }}</p> }
            </div>
          }

          @if (tenders().length) {
            <ul class="tenders" data-testid="pos-tenders">
              @for (t of tenders(); track $index) {
                <li>
                  <span>{{ label(t.method) }} @if (t.reference) { <small>{{ t.reference }}</small> }</span>
                  <span class="amt">{{ currency() }} {{ t.amount | number:'1.2-2' }}</span>
                  <button type="button" class="remove" [attr.aria-label]="'Remove ' + label(t.method)" (click)="removeTender($index)"><span nz-icon nzType="close"></span></button>
                </li>
              }
            </ul>
          }
        </div>
      </ng-container>
      <ng-template #footer>
        <span class="keys">Enter confirms · C cash · K card · O other @if (canUseAccount()) { · A account }</span>
        <button nz-button nzSize="large" (click)="cancelled.emit()">Cancel</button>
        <button nz-button nzType="primary" nzSize="large" [nzLoading]="busy()" [disabled]="!canConfirm()" (click)="confirm()" data-testid="pos-confirm-payment">
          <span nz-icon nzType="check"></span>Complete sale
        </button>
      </ng-template>
    </nz-modal>
  `,
  styles: [`
    .pay { display: flex; flex-direction: column; gap: 14px; }
    .due { display: flex; flex-direction: column; align-items: center; padding: 14px; border-radius: 10px; background: var(--erp-color-primary-subtle); }
    .due.done { background: var(--erp-color-success-bg); color: var(--erp-color-success-text); }
    .due span { font-size: 13px; font-weight: 600; }
    .due strong { font-size: 34px; font-weight: 800; line-height: 1.15; font-variant-numeric: tabular-nums; }
    .due small { color: var(--erp-color-text-muted); }
    .methods { display: grid; grid-template-columns: repeat(auto-fit, minmax(110px, 1fr)); gap: 8px; }
    .method { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px; min-height: 64px; font: inherit; font-weight: 600; color: var(--erp-color-text-primary);
      background: var(--erp-color-bg-surface); border: 2px solid var(--erp-color-border-default); border-radius: 10px; cursor: pointer; }
    .method [nz-icon] { font-size: 20px; }
    .method:hover { border-color: var(--erp-color-primary); }
    .method:focus-visible { outline: 2px solid var(--erp-color-primary); outline-offset: 2px; }
    .method.on { border-color: var(--erp-color-primary); background: var(--erp-color-primary-subtle); color: var(--erp-color-primary-text); }
    .entry { display: flex; flex-direction: column; gap: 8px; }
    .field { font-weight: 600; }
    .amount { width: 100%; font-size: 22px; }
    :host ::ng-deep .amount input { height: 52px; font-size: 22px; font-variant-numeric: tabular-nums; }
    .quick { display: flex; flex-wrap: wrap; gap: 8px; }
    .quick button { flex: 1 1 70px; min-height: 46px; font-variant-numeric: tabular-nums; }
    .note { margin: 0; font-size: 13px; color: var(--erp-color-text-muted); }
    .note.warn { color: var(--erp-color-error-text); }
    .tenders { display: flex; flex-direction: column; gap: 4px; margin: 0; padding: 0; list-style: none; }
    .tenders li { display: flex; align-items: center; gap: 12px; padding: 8px 10px; border: 1px solid var(--erp-color-border-subtle); border-radius: 8px; font-weight: 600; }
    .tenders li > span:first-child { flex: 1; }
    .tenders small { font-weight: 400; color: var(--erp-color-text-muted); }
    .amt { font-variant-numeric: tabular-nums; }
    .remove { width: 32px; height: 32px; color: var(--erp-color-error-text); background: none; border: 0; border-radius: 6px; cursor: pointer; }
    .keys { float: inline-start; margin-top: 10px; font-size: 12px; color: var(--erp-color-text-muted); }
    @media (max-width: 640px) { .keys { display: none; } }
  `],
})
export class PosPaymentDialogComponent {
  readonly open = input(false);
  readonly total = input(0);
  readonly currency = input('');
  readonly customerName = input('');
  readonly hasCustomer = input(false);
  readonly canUseAccount = input(false);
  readonly busy = input(false);
  readonly cancelled = output<void>();
  readonly completed = output<Tender[]>();

  private readonly amountBox = viewChild('amountBox', { read: ElementRef<HTMLElement> });

  readonly method = signal<PosPaymentMethod>('cash');
  readonly amount = signal(0);
  readonly reference = signal('');
  readonly tenders = signal<Tender[]>([]);

  readonly methods = computed(() => METHODS.filter((m) => m.method !== 'account' || this.canUseAccount()));
  readonly remaining = computed(() => remainingDue(this.total(), this.tenders()));
  readonly change = computed(() => changeDue(this.total(), this.tenders()));
  readonly quick = computed(() => quickCash(this.remaining()));

  /** What the amount typed now would mean, for the one thing it cannot do. */
  readonly blocked = computed(() => {
    if (this.method() === 'account' && !this.hasCustomer()) return 'Choose a customer on the ticket first.';
    if (this.method() !== 'cash' && this.amount() > this.remaining()) return 'Only cash can be more than what is due.';
    return null;
  });

  readonly canConfirm = computed(() => {
    if (this.busy()) return false;
    if (tendersSettle(this.total(), this.tenders())) return true;
    // Nothing added yet, or the rest on the box: what is typed must finish the sale.
    return !this.blocked() && this.amount() > 0 && tendersSettle(this.total(), this.withEntry());
  });

  private withEntry(): Tender[] {
    return [...this.tenders(), { method: this.method(), amount: round2(this.amount()), reference: this.reference().trim() || undefined }];
  }

  constructor() {
    effect(() => {
      if (!this.open()) return;
      this.tenders.set([]);
      this.reference.set('');
      this.method.set('cash');
      this.amount.set(this.total());
    });
  }

  label(method: PosPaymentMethod): string {
    return TENDER_LABELS[method];
  }

  focusAmount(): void {
    setTimeout(() => {
      const field = this.amountBox()?.nativeElement.querySelector('input');
      field?.focus();
      field?.select();
    });
  }

  choose(method: PosPaymentMethod): void {
    this.method.set(method);
    this.amount.set(this.remaining());
    this.reference.set('');
    this.focusAmount();
  }

  onKey(event: KeyboardEvent): void {
    if (event.ctrlKey || event.altKey || event.metaKey) return;
    if ((event.target as HTMLElement).getAttribute('aria-label') === 'Reference') return;
    const hit = this.methods().find((m) => m.key === event.key.toLowerCase());
    if (hit) {
      event.preventDefault();
      this.choose(hit.method);
    }
  }

  /** Quick-cash buttons finish the sale straight away, so one tap settles the common case. */
  takeCash(amount: number): void {
    this.method.set('cash');
    this.amount.set(amount);
    this.advance();
  }

  onEnter(event: Event): void {
    event.preventDefault();
    if (!(event as KeyboardEvent).repeat) this.advance();
  }

  private advance(): void {
    if (this.busy() || this.blocked()) return;
    if (this.amount() <= 0) {
      if (tendersSettle(this.total(), this.tenders())) this.confirm();
      return;
    }
    const next = this.withEntry();
    if (tendersSettle(this.total(), next)) {
      this.tenders.set(next);
      this.confirm();
      return;
    }
    // A part payment: keep it and ask for the rest.
    this.tenders.set(next);
    this.reference.set('');
    this.amount.set(this.remaining());
    this.focusAmount();
  }

  confirm(): void {
    if (this.busy()) return;
    const settled = tendersSettle(this.total(), this.tenders()) ? this.tenders() : this.withEntry();
    if (!tendersSettle(this.total(), settled)) return;
    this.tenders.set(settled);
    this.completed.emit(settled);
  }

  removeTender(index: number): void {
    this.tenders.update((all) => all.filter((_, i) => i !== index));
    this.amount.set(this.remaining());
    this.focusAmount();
  }
}
