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
  templateUrl: './pos-payment-dialog.component.html',
  styleUrl: './pos-payment-dialog.component.scss',
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
