import { DecimalPipe, formatNumber } from '@angular/common';
import { RouterLink } from '@angular/router';
import { afterNextRender, ChangeDetectionStrategy, Component, computed, effect, ElementRef, HostListener, inject, OnDestroy, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { Subject, debounceTime, forkJoin, of, switchMap, catchError, map, tap } from 'rxjs';
import {
  PosCatalogItemDto,
  PosCustomerDto,
  PosOrderDto,
  PosOrdersApi,
  PosReturnDto,
  PosSessionDto,
  PosSessionsApi,
  PosShiftReportDto,
  PosTerminalApi,
  PosTerminalRegisterDto,
  PosTerminalSettingsDto,
} from '@core/api/pos.api';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { ThermalReceiptComponent, ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';
import { AppliedPromo, cartSignature, CartLine, cartTotals, fromHeldOrder, lineAmounts, parseScan, repriceHeld, round2, roundQty, Tender, toOrderLine, toPayments } from '../../pos-cart';
import { CashDialogShift, PosCashDialogComponent } from '../../components/pos-cash-dialog/pos-cash-dialog.component';
import { PosPaymentDialogComponent } from '../../components/pos-payment-dialog/pos-payment-dialog.component';
import { returnReceipt, saleReceipt } from '../../pos-receipt';
import { PosReturnDialogComponent } from '../../components/pos-return-dialog/pos-return-dialog.component';
import { PosShiftReportComponent } from '../../components/pos-shift-report/pos-shift-report.component';

const REGISTER_KEY = 'erp.pos.register';
const WALK_IN = 'Walk-in Customer';
const CATALOG_PAGE = 60;

const blurHidden = (el: HTMLElement | null): boolean => !el || el.offsetParent === null;

/** The till: keyboard and scanner first, big enough to use by touch, and always showing what is on the ticket and what it costs. */
@Component({
  selector: 'erp-pos-terminal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DecimalPipe,
    FormsModule,
    ErpDatePipe,
    NzAlertModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    NzSkeletonModule,
    NzToolTipModule,
    HasPermissionDirective,
    KeyboardClickDirective,
    StatePanelComponent,
    ThermalReceiptComponent,
    PosCashDialogComponent,
    PosPaymentDialogComponent,
    PosReturnDialogComponent,
    PosShiftReportComponent,
  ],
  templateUrl: './pos-terminal.page.html',
  styleUrl: './pos-terminal.page.scss',
})
export class PosTerminalPage implements OnDestroy {
  private readonly terminal = inject(PosTerminalApi);
  private readonly sessions = inject(PosSessionsApi);
  private readonly orders = inject(PosOrdersApi);
  private readonly store = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly scanBox = viewChild<ElementRef<HTMLInputElement>>('scanBox');
  private readonly customerBox = viewChild<ElementRef<HTMLInputElement>>('customerBox');
  private readonly discountBox = viewChild('discountBox', { read: ElementRef<HTMLElement> });
  private readonly linesBox = viewChild<ElementRef<HTMLElement>>('linesBox');
  private readonly newSale = viewChild('newSale', { read: ElementRef<HTMLElement> });

  private readonly searchChanges = new Subject<string>();
  private readonly customerChanges = new Subject<string>();
  /** Every item the till has seen, by barcode and SKU, so a repeat scan goes on the ticket without a round trip. */
  private readonly known = new Map<string, PosCatalogItemDto>();
  private idempotencyKey = crypto.randomUUID();
  private parkedOrderId: string | null = null;

  readonly currency = this.currencyService.currency;
  readonly canSellOnAccount = computed(() => this.store.hasPermission('pos.orders.on-account'));
  readonly canOverridePrice = computed(() => this.store.hasPermission('pos.orders.override-price'));

  readonly registers = signal<PosTerminalRegisterDto[]>([]);
  readonly settings = signal<PosTerminalSettingsDto | null>(null);
  readonly booted = signal(false);
  readonly bootFailed = signal(false);
  readonly registerId = signal('');
  readonly session = signal<PosSessionDto | null>(null);
  readonly shiftLoading = signal(false);
  readonly shiftFailed = signal(false);

  readonly catalog = signal<PosCatalogItemDto[]>([]);
  readonly catalogTotal = signal(0);
  readonly catalogLoading = signal(false);
  readonly catalogFailed = signal(false);
  readonly searchQuery = signal('');

  readonly cart = signal<CartLine[]>([]);
  readonly selected = signal(-1);
  readonly customerId = signal<string | null>(null);
  readonly customerName = signal(WALK_IN);
  /** The ticket as the server prices it once a promo code is applied: its share of the discount on each line, so the total matches. */
  readonly priced = computed(() => {
    const p = this.promo();
    const lines = this.cart();
    if (!p || p.sig !== cartSignature(lines)) return lines;
    return lines.map((l, i) => ({ ...l, ...(p.lines[i] ?? {}) }));
  });
  readonly totals = computed(() => cartTotals(this.priced()));
  readonly promoInput = signal('');
  readonly promo = signal<AppliedPromo | null>(null);
  readonly promoBusy = signal(false);
  readonly promoError = signal<string | null>(null);
  /** The ticket changed after the code was applied: the discount is worked out again before payment. */
  readonly promoStale = computed(() => { const p = this.promo(); return !!p && p.sig !== cartSignature(this.cart()); });
  private readonly promoSync = effect(() => {
    const p = this.promo();
    const lines = this.cart();
    if (!p) return;
    if (!lines.length) { untracked(() => this.promo.set(null)); return; }
    if (p.sig !== cartSignature(lines)) untracked(() => this.requote(p.code, false));
  });

  readonly paymentOpen = signal(false);
  readonly paying = signal(false);
  readonly holding = signal(false);

  readonly receipt = signal<ThermalReceiptData | null>(null);
  readonly receiptTitle = signal('Sale complete');
  /** The order behind the receipt, so the cashier can find it again. */
  readonly receiptOrderId = signal<string | null>(null);
  readonly receiptChange = signal(0);
  readonly receiptPrintUrl = signal('');

  readonly openShiftOpen = signal(false);
  readonly openFloat = signal<number | null>(null);
  readonly shiftNotes = signal('');
  readonly shiftBusy = signal(false);
  readonly closeShiftOpen = signal(false);
  readonly closeCash_ = signal<number | null>(null);
  readonly closeNotes = signal('');
  readonly closeDifference = computed(() => {
    const counted = this.closeCash_();
    return counted === null ? 0 : round2(counted - (this.session()?.expectedCash ?? 0));
  });
  readonly shiftReport = signal<PosShiftReportDto | null>(null);

  readonly cashOpen = signal(false);
  readonly cashShifts = computed<CashDialogShift[]>(() => {
    const s = this.session();
    return s ? [{ id: s.id, label: s.registerName, expectedCash: s.expectedCash }] : [];
  });

  readonly customerOpen = signal(false);
  readonly customers = signal<PosCustomerDto[]>([]);
  readonly customersLoading = signal(false);
  readonly customerSearch = signal('');
  readonly customerTyped = signal('');

  readonly discountOpen = signal(false);
  readonly discountValue = signal(0);
  readonly discountLimit = computed(() => {
    const s = this.settings();
    if (!s) return 100;
    const cashier = s.cashierDiscountPercent;
    return cashier !== null && cashier !== undefined && !this.store.hasPermission('pos.orders.discount-override') ? Math.min(cashier, s.maxDiscountPercent) : s.maxDiscountPercent;
  });
  readonly discountInvalid = computed(() => this.discountValue() > this.discountLimit());
  readonly discountChips = computed(() => [5, 10, 15, 20, 25].filter((d) => d <= this.discountLimit()));

  readonly editIndex = signal<number | null>(null);
  readonly editQty = signal(1);
  readonly editDiscount = signal(0);
  readonly editPrice = signal(0);
  readonly editing = computed(() => {
    const i = this.editIndex();
    return i === null ? null : this.cart()[i] ?? null;
  });
  readonly lineInvalid = computed(() => this.editDiscount() > this.discountLimit() || this.editDiscount() < 0 || this.editQty() < 0);

  readonly openItemOpen = signal(false);
  readonly openItemName = signal('');
  readonly openItemPrice = signal(0);
  readonly openItemTax = signal(0);

  readonly heldOpen = signal(false);
  readonly held = signal<PosOrderDto[]>([]);
  readonly heldCount = computed(() => this.held().length);

  readonly returnOpen = signal(false);
  readonly returnNumber = signal<string | null>(null);

  constructor() {
    this.searchChanges.pipe(
      debounceTime(180),
      switchMap((term) => this.fetchCatalog(term)),
    ).subscribe();
    this.customerChanges.pipe(
      debounceTime(220),
      switchMap((term) => {
        this.customersLoading.set(true);
        return this.terminal.customers(term).pipe(catchError(() => of([] as PosCustomerDto[])));
      }),
    ).subscribe((rows) => {
      this.customers.set(rows);
      this.customersLoading.set(false);
    });
    this.boot();
    afterNextRender(() => this.focusScan());
  }

  ngOnDestroy(): void {
    this.searchChanges.complete();
    this.customerChanges.complete();
  }

  // ---- start up ----

  boot(): void {
    this.bootFailed.set(false);
    this.terminal.bootstrap().subscribe({
      next: (t) => {
        this.booted.set(true);
        this.settings.set(t.settings);
        this.registers.set(t.registers);
        if (t.registers.length) {
          const remembered = localStorage.getItem(REGISTER_KEY);
          this.selectRegister(t.registers.find((r) => r.id === remembered)?.id ?? t.registers[0].id);
        }
      },
      error: () => this.bootFailed.set(true),
    });
  }

  selectRegister(id: string): void {
    this.registerId.set(id);
    try { localStorage.setItem(REGISTER_KEY, id); } catch { /* storage unavailable */ }
    this.loadSession(id, () => this.takeReturnFromLink());
    this.loadHeld();
    this.loadCatalog(this.searchQuery());
  }

  private loadSession(registerId: string, then?: () => void): void {
    this.shiftLoading.set(true);
    this.shiftFailed.set(false);
    this.terminal.session(registerId).subscribe({
      next: (s) => {
        this.session.set(s);
        this.shiftLoading.set(false);
        then?.();
      },
      error: () => {
        this.session.set(null);
        this.shiftLoading.set(false);
        this.shiftFailed.set(true);
      },
    });
  }

  private loadHeld(): void {
    const id = this.registerId();
    if (!id) return;
    this.terminal.held(id).subscribe({ next: (rows) => this.held.set(rows), error: () => this.held.set([]) });
  }

  loadCatalog(term: string): void {
    this.searchChanges.next(term);
  }

  private fetchCatalog(term: string) {
    this.catalogLoading.set(true);
    this.catalogFailed.set(false);
    return this.terminal.catalog({ registerId: this.registerId() || undefined, search: term.trim() || undefined, pageSize: CATALOG_PAGE }).pipe(
      tap((res) => {
        res.items.forEach((i) => this.remember(i));
        this.catalog.set(res.items);
        this.catalogTotal.set(res.total);
        this.catalogLoading.set(false);
      }),
      catchError(() => {
        this.catalogLoading.set(false);
        this.catalogFailed.set(true);
        return of(null);
      }),
    );
  }

  private remember(i: PosCatalogItemDto): void {
    this.known.set(i.sku.toLowerCase(), i);
    if (i.barcode) this.known.set(i.barcode.toLowerCase(), i);
  }

  // ---- keyboard ----

  private anyDialogOpen(): boolean {
    return this.paymentOpen() || !!this.receipt() || this.openShiftOpen() || this.closeShiftOpen() || !!this.shiftReport() || this.cashOpen() || this.customerOpen()
      || this.discountOpen() || this.editIndex() !== null || this.openItemOpen() || this.heldOpen() || this.returnOpen();
  }

  @HostListener('window:keydown', ['$event'])
  onWindowKey(e: KeyboardEvent): void {
    if (e.repeat && e.key.startsWith('F')) return;
    const fn: Record<string, () => void> = {
      F2: () => this.focusScan(),
      F3: () => this.startCustomer(),
      F4: () => this.startDiscount(),
      F6: () => this.holdSale(),
      F7: () => this.startHeld(),
      F8: () => this.startReturn(),
      F9: () => this.startPayment(),
      F10: () => { if (this.session() && this.store.hasPermission('pos.sessions.manage')) this.cashOpen.set(true); },
    };
    const action = fn[e.key];
    if (!action) return;
    e.preventDefault(); // these keys also mean something to the browser
    if (!this.anyDialogOpen()) action();
  }

  /** The scan box is where the keyboard lives: with it empty, arrows and + - work the ticket. */
  onScanKey(e: KeyboardEvent): void {
    if (e.key === 'Enter') {
      e.preventDefault();
      this.submitScan();
      return;
    }
    if (e.key === 'Escape') {
      this.clearSearch();
      return;
    }
    if (this.searchQuery()) return;
    const count = this.cart().length;
    const i = this.selected();
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        if (count) this.selectLine(Math.min(i + 1, count - 1));
        break;
      case 'ArrowUp':
        e.preventDefault();
        if (count) this.selectLine(Math.max(i - 1, 0));
        break;
      case '+':
        e.preventDefault();
        if (this.cart()[i]) this.changeQty(i, this.cart()[i].quantity + 1);
        break;
      case '-':
        e.preventDefault();
        if (this.cart()[i]) this.changeQty(i, this.cart()[i].quantity - 1);
        break;
      case 'Delete':
        e.preventDefault();
        if (this.cart()[i]) this.removeLine(i);
        break;
    }
  }

  private selectLine(i: number): void {
    this.selected.set(i);
    setTimeout(() => this.linesBox()?.nativeElement.querySelector('.line.selected')?.scrollIntoView({ block: 'nearest' }));
  }

  focusField(id: string): void {
    setTimeout(() => (document.getElementById(id) as HTMLInputElement | null)?.focus());
  }

  focusScan(): void {
    setTimeout(() => {
      const box = this.scanBox()?.nativeElement ?? null;
      if (!blurHidden(box)) box!.focus();
    });
  }

  // ---- scanning and searching ----

  onSearch(value: string): void {
    this.searchQuery.set(value);
    this.loadCatalog(value);
  }

  clearSearch(): void {
    if (this.searchQuery()) {
      this.searchQuery.set('');
      this.loadCatalog('');
    }
    this.focusScan();
  }

  /** The box empties at once, so a scanner can fire the next code before this one has come back and the two never run together. */
  private submitScan(): void {
    const { quantity, code } = parseScan(this.searchQuery());
    if (!code) return;
    this.emptyScanBox();
    const key = code.toLowerCase();
    const hit = this.known.get(key);
    if (hit) {
      this.addItem(hit, quantity, false);
      return;
    }
    this.terminal.catalog({ registerId: this.registerId() || undefined, code, pageSize: 2 }).subscribe({
      next: (exact) => {
        if (exact.items.length) {
          this.remember(exact.items[0]);
          this.addItem(exact.items[0], quantity, false);
          return;
        }
        // Not a code: the one product whose name matches is what was meant.
        this.terminal.catalog({ registerId: this.registerId() || undefined, search: code, pageSize: 2 }).subscribe({
          next: (found) => {
            if (found.items.length === 1) {
              this.remember(found.items[0]);
              this.addItem(found.items[0], quantity, false);
            } else if (found.items.length === 0) {
              this.notFound(code);
            } else {
              // Show the matches, unless the next code is already being typed.
              if (!this.searchQuery()) this.onSearch(code);
              this.message.info(`More than one product matches "${code}". Tap the one you want.`);
            }
          },
          error: () => this.message.error('The search failed. Try again.'),
        });
      },
      error: () => this.message.error('The search failed. Try again.'),
    });
  }

  /** Not a product: a receipt scanned here starts its return, for whoever may take one. */
  private notFound(code: string): void {
    const warn = () => this.message.warning(`No product matches "${code}".`);
    if (!this.session() || !this.store.hasPermission('pos.orders.refund') || !this.store.hasPermission('pos.orders.read')) {
      warn();
      return;
    }
    this.orders.byNumber(code).subscribe({
      next: (order) => this.startReturn(order.number),
      error: warn,
    });
  }

  outOfStockBlocked(item: PosCatalogItemDto): boolean {
    return this.settings()?.allowNegativeStock === false && item.onHand !== null && item.onHand <= 0;
  }

  /** A scan has already emptied the box, and by the time its answer comes back the next code may be in it. */
  private emptyScanBox(): void {
    this.searchQuery.set('');
    const box = this.scanBox()?.nativeElement;
    if (box) box.value = ''; // not waiting for the next render: a scanner can be faster than that
    this.loadCatalog('');
  }

  addItem(item: PosCatalogItemDto, quantity = 1, clearBox = true): void {
    if (item.price === null) {
      this.message.warning(`${item.name} has no sales price.`);
      return;
    }
    const lines = [...this.cart()];
    let at = lines.findIndex((l) => l.itemId === item.id);
    if (at >= 0) {
      lines[at] = { ...lines[at], quantity: roundQty(lines[at].quantity + quantity), onHand: item.onHand };
    } else {
      lines.push({ key: item.id, itemId: item.id, itemCode: item.sku, description: item.name, quantity, unitPrice: item.price, listPrice: item.price, discountPercent: 0, taxRate: item.taxRate, onHand: item.onHand });
      at = lines.length - 1;
    }
    this.setCart(lines);
    this.selectLine(at);
    if (clearBox && this.searchQuery()) this.emptyScanBox();
    this.focusScan();
  }

  // ---- the ticket ----

  private setCart(lines: CartLine[]): void {
    this.cart.set(lines);
    if (!lines.length) { this.promo.set(null); this.promoInput.set(''); this.promoError.set(null); }
    this.idempotencyKey = crypto.randomUUID(); // a different ticket is a different sale
    if (!lines.length) this.parkedOrderId = null; // the held copy stays on hold: it was not sold
  }

  /** Asks the server what the ticket costs with this code. Nothing is used up until the sale is paid. */
  applyPromo(): void {
    const code = this.promoInput().trim();
    if (code) this.requote(code, true);
  }

  removePromo(): void {
    this.promo.set(null);
    this.promoInput.set('');
    this.promoError.set(null);
  }

  /** `explicit` is the cashier typing a code: a refusal is shown. After a ticket change the code is simply tried again, and dropped with a note if it no longer applies. */
  private requote(code: string, explicit: boolean): void {
    const s = this.session();
    const lines = this.cart();
    if (!s || !lines.length) return;
    const sig = cartSignature(lines);
    this.promoBusy.set(true);
    this.promoError.set(null);
    this.orders.quote({ legalEntityId: s.legalEntityId, customerPartyId: this.customerId(), lines: lines.map(toOrderLine), promoCode: code }).subscribe({
      next: (q) => {
        this.promoBusy.set(false);
        if (!q.promoCode || !(q.promoDiscount > 0)) {
          this.promo.set(null);
          this.promoError.set(explicit ? 'That code does not give a discount on this ticket.' : 'The code no longer gives a discount on this ticket, so it was removed.');
          return;
        }
        this.promoInput.set('');
        this.promo.set({
          code: q.promoCode, name: q.promoName, discount: q.promoDiscount, sig,
          lines: q.lines.map((l) => ({ unitPrice: l.unitPrice, discountPercent: l.discountPercent, taxRate: l.taxRate, promoDiscount: l.promoDiscount })),
        });
      },
      error: (err) => {
        this.promoBusy.set(false);
        this.promo.set(null);
        this.promoError.set(problemMessage(err, 'The code could not be checked.', 'promoCode'));
      },
    });
  }

  amount(line: CartLine): number {
    return lineAmounts(line).total;
  }

  changeQty(index: number, quantity: number): void {
    const lines = [...this.cart()];
    if (quantity <= 0) {
      this.removeLine(index);
      return;
    }
    lines[index] = { ...lines[index], quantity: roundQty(quantity) };
    this.setCart(lines);
  }

  removeLine(index: number): void {
    const lines = this.cart().filter((_, i) => i !== index);
    this.setCart(lines);
    this.selected.set(lines.length ? Math.min(index, lines.length - 1) : -1);
    this.focusScan();
  }

  clearCart(): void {
    this.setCart([]);
    this.selected.set(-1);
    this.parkedOrderId = null;
    this.setWalkInQuietly();
    this.focusScan();
  }

  private setWalkInQuietly(): void {
    this.customerId.set(null);
    this.customerName.set(WALK_IN);
  }

  startLineEdit(index: number): void {
    const line = this.cart()[index];
    if (!line) return;
    this.selected.set(index);
    this.editQty.set(line.quantity);
    this.editDiscount.set(line.discountPercent);
    this.editPrice.set(line.unitPrice);
    this.editIndex.set(index);
  }

  applyLineEdit(): void {
    const i = this.editIndex();
    if (i === null || this.lineInvalid()) return;
    if (this.editQty() <= 0) {
      this.editIndex.set(null);
      this.removeLine(i);
      return;
    }
    const lines = [...this.cart()];
    lines[i] = { ...lines[i], quantity: roundQty(this.editQty()), discountPercent: this.editDiscount(), unitPrice: this.canOverridePrice() ? this.editPrice() : lines[i].unitPrice };
    this.setCart(lines);
    this.editIndex.set(null);
    this.focusScan();
  }

  startOpenItem(): void {
    this.openItemName.set('');
    this.openItemPrice.set(0);
    this.openItemTax.set(0);
    this.openItemOpen.set(true);
  }

  addOpenItem(): void {
    const name = this.openItemName().trim();
    if (!name || this.openItemPrice() <= 0) return;
    const key = `open-${crypto.randomUUID()}`;
    const lines = [...this.cart(), { key, itemId: null, itemCode: 'OTHER', description: name, quantity: 1, unitPrice: this.openItemPrice(), listPrice: null, discountPercent: 0, taxRate: this.openItemTax(), onHand: null }];
    this.setCart(lines);
    this.selectLine(lines.length - 1);
    this.openItemOpen.set(false);
    this.focusScan();
  }

  startDiscount(): void {
    if (!this.cart().length) return;
    const percents = new Set(this.cart().map((l) => l.discountPercent));
    this.discountValue.set(percents.size === 1 ? [...percents][0] : 0);
    this.discountOpen.set(true);
  }

  focusDiscount(): void {
    setTimeout(() => {
      const field = this.discountBox()?.nativeElement.querySelector('input');
      field?.focus();
      field?.select();
    });
  }

  applyDiscount(): void {
    if (this.discountInvalid()) return;
    this.setCart(this.cart().map((l) => ({ ...l, discountPercent: this.discountValue() })));
    this.closeDiscount();
  }

  closeDiscount(): void {
    this.discountOpen.set(false);
    this.focusScan();
  }

  // ---- customer ----

  startCustomer(): void {
    this.customerSearch.set('');
    this.customerTyped.set(this.customerId() || this.customerName() === WALK_IN ? '' : this.customerName());
    this.customerOpen.set(true);
    this.customerChanges.next('');
  }

  focusCustomer(): void {
    setTimeout(() => this.customerBox()?.nativeElement.focus());
  }

  onCustomerSearch(value: string): void {
    this.customerSearch.set(value);
    this.customerChanges.next(value);
  }

  pickFirstCustomer(e: Event): void {
    e.preventDefault();
    const first = this.customers()[0];
    if (first) this.chooseCustomer(first);
  }

  chooseCustomer(c: PosCustomerDto): void {
    this.customerId.set(c.id);
    this.customerName.set(c.name);
    this.closeCustomer();
  }

  useTypedCustomer(e?: Event): void {
    e?.preventDefault();
    const name = this.customerTyped().trim();
    if (!name) return;
    this.customerId.set(null);
    this.customerName.set(name);
    this.closeCustomer();
  }

  setWalkIn(): void {
    this.setWalkInQuietly();
    this.closeCustomer();
  }

  closeCustomer(): void {
    this.customerOpen.set(false);
    this.focusScan();
  }

  // ---- holding ----

  holdSale(): void {
    const session = this.session();
    if (!session || !this.cart().length || this.holding()) return;
    this.holding.set(true);
    this.orders.park({
      sessionId: session.id,
      customerPartyId: this.customerId(),
      customerName: this.customerId() ? null : this.customerName(),
      lines: this.cart().map(toOrderLine),
    }).subscribe({
      next: () => {
        this.holding.set(false);
        // A resumed ticket held again replaces its old copy.
        const previous = this.parkedOrderId;
        if (previous) this.orders.discardParked(previous).subscribe({ next: () => this.loadHeld(), error: () => this.loadHeld() });
        else this.loadHeld();
        this.clearCart();
        this.message.success('Sale held. Open Held to bring it back.');
      },
      error: (err) => {
        this.holding.set(false);
        this.message.error(problemMessage(err, 'The sale could not be held.'));
      },
    });
  }

  startHeld(): void {
    this.loadHeld();
    this.heldOpen.set(true);
  }

  heldSummary(o: PosOrderDto): string {
    return o.lines.map((l) => `${l.quantity} × ${l.description}`).join(', ');
  }

  resumeHeld(o: PosOrderDto): void {
    if (this.cart().length) {
      this.message.warning('Finish or hold the current ticket first.');
      return;
    }
    const lines = fromHeldOrder(o);
    // Today's prices, tax and stock, because the server charges today's prices at payment.
    const lookups = lines.map((l) => (l.itemId
      ? this.terminal.catalog({ registerId: this.registerId() || undefined, code: l.itemCode, pageSize: 2 }).pipe(map((r) => r.items.find((i) => i.id === l.itemId) ?? null), catchError(() => of(null)))
      : of(null)));
    forkJoin(lookups).subscribe((items) => {
      const canOverride = this.canOverridePrice();
      const fresh = lines.map((l, i) => repriceHeld(l, items[i], canOverride));
      if (fresh.some((l, i) => l.unitPrice !== lines[i].unitPrice)) this.message.info('Prices changed since this sale was held. The ticket shows the current prices.');
      this.setCart(fresh);
      this.parkedOrderId = o.id;
      this.customerId.set(o.customerPartyId ?? null);
      this.customerName.set(o.customerName);
      this.selected.set(fresh.length - 1);
      this.heldOpen.set(false);
      this.focusScan();
    });
  }

  discardHeld(o: PosOrderDto): void {
    this.orders.discardParked(o.id).subscribe({
      next: () => {
        if (this.parkedOrderId === o.id) this.parkedOrderId = null;
        this.loadHeld();
        this.message.success(`Held sale ${o.number} discarded.`);
      },
      error: (err) => this.message.error(problemMessage(err, 'The held sale could not be discarded.')),
    });
  }

  // ---- payment ----

  startPayment(): void {
    if (!this.cart().length) return;
    if (!this.session()) {
      this.message.warning('Open a shift before taking payment.');
      return;
    }
    this.paymentOpen.set(true);
  }

  closePayment(): void {
    this.paymentOpen.set(false);
    this.focusScan();
  }

  takePayment(tenders: Tender[]): void {
    const s = this.session();
    if (!s || this.paying()) return;
    this.paying.set(true);
    const total = this.totals().total;
    this.orders.sell({
      legalEntityId: s.legalEntityId,
      registerId: s.registerId,
      sessionId: s.id,
      customerPartyId: this.customerId(),
      customerName: this.customerId() ? null : this.customerName(),
      lines: this.cart().map(toOrderLine),
      payments: toPayments(total, tenders),
      parkedOrderId: this.parkedOrderId,
      promoCode: this.promoStale() ? null : this.promo()?.code ?? null,
    }, this.idempotencyKey).subscribe({
      next: (order) => {
        this.paying.set(false);
        this.paymentOpen.set(false);
        this.showSaleReceipt(order);
        this.parkedOrderId = null;
        this.setCart([]);
        this.selected.set(-1);
        this.setWalkInQuietly();
        this.loadSession(s.registerId);
        this.loadHeld();
        this.loadCatalog(''); // stock moved
      },
      error: (err) => {
        this.paying.set(false);
        this.message.error(problemMessage(err, 'The payment could not be saved.'));
      },
    });
  }

  private context() {
    const register = this.registers().find((r) => r.id === this.registerId());
    return { business: this.store.me()?.workspace?.name, register, currency: this.currency() };
  }

  private showSaleReceipt(order: PosOrderDto): void {
    this.receiptTitle.set(`Sale ${order.number} complete: ${this.currency()} ${formatNumber(order.totalAmount, 'en-US', '1.2-2')}`);
    this.receiptOrderId.set(order.id);
    this.receiptChange.set(order.payments.reduce((sum, p) => sum + p.changeAmount, 0));
    this.receiptPrintUrl.set(`/print/pos-receipt/${order.id}`);
    this.receipt.set(saleReceipt(order, this.context()));
  }

  focusNewSale(): void {
    setTimeout(() => this.newSale()?.nativeElement.focus());
  }

  finishReceipt(): void {
    this.receipt.set(null);
    this.receiptChange.set(0);
    this.focusScan();
  }

  // ---- returns ----

  startReturn(number: string | null = null): void {
    if (!this.session()) {
      this.message.warning('Open a shift to give money back.');
      return;
    }
    if (!this.store.hasPermission('pos.orders.refund')) return;
    this.returnNumber.set(number);
    this.returnOpen.set(true);
  }

  closeReturn(): void {
    this.returnOpen.set(false);
    this.returnNumber.set(null);
    this.focusScan();
  }

  returnDone(ret: PosReturnDto): void {
    this.returnOpen.set(false);
    this.returnNumber.set(null);
    this.receiptTitle.set(`Refund ${ret.number} complete`);
    this.receiptOrderId.set(null);
    this.receiptChange.set(0);
    this.receiptPrintUrl.set(`/print/pos-return/${ret.id}`);
    this.receipt.set(returnReceipt(ret, this.context()));
    this.loadSession(this.registerId());
    this.loadCatalog('');
  }

  // ---- cash and shifts ----

  closeCash(): void {
    this.cashOpen.set(false);
    this.focusScan();
  }

  cashRecorded(): void {
    this.closeCash();
    this.loadSession(this.registerId());
  }

  startOpen(): void {
    this.openFloat.set(this.settings()?.enforceFloatAtOpen === false ? 0 : null);
    this.shiftNotes.set('');
    this.openShiftOpen.set(true);
  }

  submitOpenShift(): void {
    const reg = this.registers().find((r) => r.id === this.registerId());
    const float = this.openFloat();
    if (!reg || float === null) return;
    this.shiftBusy.set(true);
    this.sessions.openSession({ legalEntityId: reg.legalEntityId, registerId: reg.id, openingCash: float, notes: this.shiftNotes() || null }).subscribe({
      next: (s) => {
        this.shiftBusy.set(false);
        this.session.set(s);
        this.openShiftOpen.set(false);
        this.message.success(`Shift opened on ${s.registerName}.`);
        this.focusScan();
        this.takeReturnFromLink();
      },
      error: (err) => {
        this.shiftBusy.set(false);
        this.message.error(problemMessage(err, 'The shift could not be opened.'));
      },
    });
  }

  startClose(): void {
    this.closeCash_.set(null); // counted, not copied from what the system expects
    this.closeNotes.set('');
    this.loadHeld();
    // Other tills or the back office may have moved cash since this screen last looked.
    this.loadSession(this.registerId(), () => this.closeShiftOpen.set(true));
  }

  submitCloseShift(): void {
    const s = this.session();
    const counted = this.closeCash_();
    if (!s || counted === null) return;
    this.shiftBusy.set(true);
    this.sessions.closeSession(s.id, { actualCash: counted, notes: this.closeNotes() || null }).subscribe({
      next: () => {
        this.shiftBusy.set(false);
        this.closeShiftOpen.set(false);
        this.session.set(null);
        this.message.success(`Shift closed on ${s.registerName}.`);
        this.sessions.report(s.id).subscribe({
          next: (r) => this.shiftReport.set(r),
          error: () => this.focusScan(), // no read permission: the close itself succeeded
        });
      },
      error: (err) => {
        this.shiftBusy.set(false);
        this.message.error(problemMessage(err, 'The shift could not be closed.'));
      },
    });
  }

  showShiftReport(): void {
    const s = this.session();
    if (!s) return;
    this.sessions.report(s.id).subscribe({
      next: (r) => this.shiftReport.set(r),
      error: (err) => this.message.error(problemMessage(err, 'The shift report could not be loaded.')),
    });
  }

  closeShiftReport(): void {
    this.shiftReport.set(null);
    this.focusScan();
  }

  /** The back office sends a sale here to be returned: /pos/terminal?return=POS-00012. */
  takeReturnFromLink(): void {
    const number = this.route.snapshot.queryParamMap.get('return');
    if (!number || !this.session()) return;
    this.startReturn(number);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }
}
