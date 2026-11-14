import { DecimalPipe } from '@angular/common';
import { afterNextRender, ChangeDetectionStrategy, Component, computed, effect, ElementRef, HostListener, inject, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { ThermalReceiptComponent, ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { CanPipe } from '@shared/pipes/can.pipe';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { CashDialogShift, PosCashDialogComponent } from '../../components/pos-cash-dialog/pos-cash-dialog.component';
import { PosPaymentDialogComponent } from '../../components/pos-payment-dialog/pos-payment-dialog.component';
import { PosReturnDialogComponent } from '../../components/pos-return-dialog/pos-return-dialog.component';
import { PosShiftReportComponent } from '../../components/pos-shift-report/pos-shift-report.component';
import { AppliedPromo, cartSignature, CartLine, cartTotals, fromHeldOrder, lineAmounts, parseScan, repriceHeld, round2, Tender, toOrderLine, toPayments } from '../../pos-cart';
import { PosCatalog, ScanMatch } from '../../pos-catalog';
import { PosCustomerSearch } from '../../pos-customer-search';
import { ReceiptContext, ReceiptView, returnReceiptView, saleReceiptView } from '../../pos-receipt';
import {
  addToTicket,
  commonDiscount,
  discountLimit,
  hasPrice,
  heldSummary,
  openItemLine,
  outOfStockBlocked,
  pricedLines,
  promoFromQuote,
  WALK_IN,
  withDiscount,
  withLineEdit,
  withQuantity,
} from '../../pos-ticket';

const REGISTER_KEY = 'erp.pos.register';

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
    CanPipe,
    KeyboardClickDirective,
    StatePanelComponent,
    ThermalReceiptComponent,
    PosCashDialogComponent,
    PosPaymentDialogComponent,
    PosReturnDialogComponent,
    PosShiftReportComponent,
  ],
  providers: [PosCatalog, PosCustomerSearch],
  templateUrl: './pos-terminal.page.html',
  styleUrl: './pos-terminal.page.scss',
})
export class PosTerminalPage {
  private readonly terminalApi = inject(PosTerminalApi);
  private readonly sessionsApi = inject(PosSessionsApi);
  private readonly ordersApi = inject(PosOrdersApi);
  private readonly products = inject(PosCatalog);
  private readonly customerLookup = inject(PosCustomerSearch);
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

  /** A different ticket is a different sale; the same ticket keeps its key, so a retried payment cannot charge twice. */
  private idempotencyKey = crypto.randomUUID();
  /** The held sale this ticket was resumed from: selling or holding it again replaces that copy. */
  private parkedOrderId: string | null = null;

  readonly currency = this.currencyService.currency;
  readonly canSellOnAccount = computed(() => this.store.hasPermission('pos.orders.on-account'));
  readonly canOverridePrice = computed(() => this.store.hasPermission('pos.orders.override-price'));
  /** A return finds the sale by its receipt (orders read) and gives the money back (refund). */
  readonly canReturn = computed(() => this.store.hasPermission('pos.orders.refund') && this.store.hasPermission('pos.orders.read'));
  readonly canManageShift = computed(() => this.store.hasPermission('pos.sessions.manage'));

  readonly registers = signal<PosTerminalRegisterDto[]>([]);
  readonly settings = signal<PosTerminalSettingsDto | null>(null);
  readonly booted = signal(false);
  readonly bootFailed = signal(false);
  readonly registerId = signal('');
  readonly session = signal<PosSessionDto | null>(null);
  readonly shiftLoading = signal(false);
  readonly shiftFailed = signal(false);

  readonly catalog = this.products.items;
  readonly catalogTotal = this.products.total;
  readonly catalogLoading = this.products.loading;
  readonly catalogFailed = this.products.failed;
  readonly searchQuery = signal('');

  readonly cart = signal<CartLine[]>([]);
  readonly selected = signal(-1);
  readonly customerId = signal<string | null>(null);
  readonly customerName = signal(WALK_IN);
  /** The ticket as the server prices it once a promo code is applied: its share of the discount on each line, so the total matches. */
  readonly priced = computed(() => pricedLines(this.cart(), this.promo()));
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
  readonly customers = this.customerLookup.results;
  readonly customersLoading = this.customerLookup.loading;
  readonly customerSearch = signal('');
  readonly customerTyped = signal('');

  readonly discountOpen = signal(false);
  readonly discountValue = signal(0);
  readonly discountLimit = computed(() => discountLimit(this.settings(), this.store.hasPermission('pos.orders.discount-override')));
  readonly discountInvalid = computed(() => this.discountValue() > this.discountLimit());
  readonly discountChips = computed(() => [5, 10, 15, 20, 25].filter((d) => d <= this.discountLimit()));
  /**
   * The ticket holds something only the override permissions allow: a discount above this person's limit, a price below the list
   * price, or an item that is not in the catalogue. Access can change while the till is open (and a held ticket can come from someone
   * else), so the till stops at Pay instead of letting the server refuse the sale.
   */
  readonly needsOverride = computed(() => {
    const limit = this.discountLimit();
    const canPrice = this.canOverridePrice();
    return this.cart().some((l) => l.discountPercent > limit || (!canPrice && (l.itemId === null || (l.listPrice !== null && l.unitPrice < l.listPrice))));
  });

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
    this.boot();
    afterNextRender(() => this.focusScan());
  }

  // ---- start up ----

  boot(): void {
    this.bootFailed.set(false);
    this.terminalApi.bootstrap().subscribe({
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
    this.terminalApi.session(registerId).subscribe({
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
    this.terminalApi.held(id).subscribe({ next: (rows) => this.held.set(rows), error: () => this.held.set([]) });
  }

  loadCatalog(term: string): void {
    this.products.search(this.registerId(), term);
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
      F10: () => this.startCash(),
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
    const line = this.cart()[i];
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
        if (line) this.changeQty(i, line.quantity + 1);
        break;
      case '-':
        e.preventDefault();
        if (line) this.changeQty(i, line.quantity - 1);
        break;
      case 'Delete':
        e.preventDefault();
        if (line) this.removeLine(i);
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
    const hit = this.products.remembered(code);
    if (hit) {
      this.addItem(hit, quantity, false);
      return;
    }
    this.products.resolveScan(this.registerId(), code).subscribe({
      next: (match) => this.onScanMatch(match, code, quantity),
      error: () => this.message.error('The search failed. Try again.'),
    });
  }

  private onScanMatch(match: ScanMatch, code: string, quantity: number): void {
    if (match.kind === 'item') {
      this.addItem(match.item, quantity, false);
    } else if (match.kind === 'none') {
      this.notFound(code);
    } else {
      // Show the matches, unless the next code is already being typed.
      if (!this.searchQuery()) this.onSearch(code);
      this.message.info(`More than one product matches "${code}". Tap the one you want.`);
    }
  }

  /** Not a product: a receipt scanned here starts its return, for whoever may take one. */
  private notFound(code: string): void {
    const warn = () => this.message.warning(`No product matches "${code}".`);
    if (!this.session() || !this.canReturn()) {
      warn();
      return;
    }
    this.ordersApi.byNumber(code).subscribe({
      next: (order) => this.startReturn(order.number),
      error: warn,
    });
  }

  outOfStockBlocked(item: PosCatalogItemDto): boolean {
    return outOfStockBlocked(this.settings(), item);
  }

  /** A scan has already emptied the box, and by the time its answer comes back the next code may be in it. */
  private emptyScanBox(): void {
    this.searchQuery.set('');
    const box = this.scanBox()?.nativeElement;
    if (box) box.value = ''; // not waiting for the next render: a scanner can be faster than that
    this.loadCatalog('');
  }

  addItem(item: PosCatalogItemDto, quantity = 1, clearBox = true): void {
    if (!hasPrice(item)) {
      this.message.warning(`${item.name} has no sales price.`);
      return;
    }
    const { lines, at } = addToTicket(this.cart(), item, quantity);
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
    this.ordersApi.quote({ legalEntityId: s.legalEntityId, customerPartyId: this.customerId(), lines: lines.map(toOrderLine), promoCode: code }).subscribe({
      next: (q) => {
        this.promoBusy.set(false);
        const applied = promoFromQuote(q, sig);
        if (!applied) {
          this.promo.set(null);
          this.promoError.set(explicit ? 'That code does not give a discount on this ticket.' : 'The code no longer gives a discount on this ticket, so it was removed.');
          return;
        }
        this.promoInput.set('');
        this.promo.set(applied);
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
    if (quantity <= 0) {
      this.removeLine(index);
      return;
    }
    this.setCart(withQuantity(this.cart(), index, quantity));
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
    const edit = { quantity: this.editQty(), discountPercent: this.editDiscount(), unitPrice: this.editPrice() };
    this.setCart(withLineEdit(this.cart(), i, edit, this.canOverridePrice()));
    this.editIndex.set(null);
    this.focusScan();
  }

  startOpenItem(): void {
    if (!this.canOverridePrice()) return;
    this.openItemName.set('');
    this.openItemPrice.set(0);
    this.openItemTax.set(0);
    this.openItemOpen.set(true);
  }

  addOpenItem(): void {
    const name = this.openItemName().trim();
    if (!name || this.openItemPrice() <= 0 || !this.canOverridePrice()) return;
    const lines = [...this.cart(), openItemLine(name, this.openItemPrice(), this.openItemTax())];
    this.setCart(lines);
    this.selectLine(lines.length - 1);
    this.openItemOpen.set(false);
    this.focusScan();
  }

  startDiscount(): void {
    if (!this.cart().length || this.discountLimit() <= 0) return;
    this.discountValue.set(commonDiscount(this.cart()));
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
    this.setCart(withDiscount(this.cart(), this.discountValue()));
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
    this.customerLookup.search('');
  }

  focusCustomer(): void {
    setTimeout(() => this.customerBox()?.nativeElement.focus());
  }

  onCustomerSearch(value: string): void {
    this.customerSearch.set(value);
    this.customerLookup.search(value);
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
    this.ordersApi.park({
      sessionId: session.id,
      customerPartyId: this.customerId(),
      customerName: this.customerId() ? null : this.customerName(),
      lines: this.cart().map(toOrderLine),
    }).subscribe({
      next: () => {
        this.holding.set(false);
        this.replaceHeldCopy();
        this.clearCart();
        this.message.success('Sale held. Open Held to bring it back.');
      },
      error: (err) => {
        this.holding.set(false);
        this.message.error(problemMessage(err, 'The sale could not be held.'));
      },
    });
  }

  /** A resumed ticket held again replaces its old copy. */
  private replaceHeldCopy(): void {
    const previous = this.parkedOrderId;
    if (previous) this.ordersApi.discardParked(previous).subscribe({ next: () => this.loadHeld(), error: () => this.loadHeld() });
    else this.loadHeld();
  }

  startHeld(): void {
    this.loadHeld();
    this.heldOpen.set(true);
  }

  heldSummary(o: PosOrderDto): string {
    return heldSummary(o);
  }

  resumeHeld(o: PosOrderDto): void {
    if (this.cart().length) {
      this.message.warning('Finish or hold the current ticket first.');
      return;
    }
    const lines = fromHeldOrder(o);
    // Today's prices, tax and stock, because the server charges today's prices at payment.
    this.products.current(this.registerId(), lines).subscribe((items) => {
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
    this.ordersApi.discardParked(o.id).subscribe({
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
    if (!this.cart().length || !this.store.hasPermission('pos.orders.create') || this.needsOverride()) return;
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
    if (!s || this.paying() || this.needsOverride()) return;
    this.paying.set(true);
    this.ordersApi.sell({
      legalEntityId: s.legalEntityId,
      registerId: s.registerId,
      sessionId: s.id,
      customerPartyId: this.customerId(),
      customerName: this.customerId() ? null : this.customerName(),
      lines: this.cart().map(toOrderLine),
      payments: toPayments(this.totals().total, tenders),
      parkedOrderId: this.parkedOrderId,
      promoCode: this.promoStale() ? null : this.promo()?.code ?? null,
    }, this.idempotencyKey).subscribe({
      next: (order) => {
        this.paying.set(false);
        this.paymentOpen.set(false);
        this.showReceipt(saleReceiptView(order, this.receiptContext()));
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

  private receiptContext(): ReceiptContext {
    const register = this.registers().find((r) => r.id === this.registerId());
    return { business: this.store.me()?.workspace?.name, register, currency: this.currency() };
  }

  private showReceipt(view: ReceiptView): void {
    this.receiptTitle.set(view.title);
    this.receiptOrderId.set(view.orderId);
    this.receiptChange.set(view.change);
    this.receiptPrintUrl.set(view.printUrl);
    this.receipt.set(view.receipt);
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
    if (!this.canReturn()) return;
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
    this.showReceipt(returnReceiptView(ret, this.receiptContext()));
    this.loadSession(this.registerId());
    this.loadCatalog('');
  }

  // ---- cash and shifts ----

  private startCash(): void {
    if (this.session() && this.canManageShift()) this.cashOpen.set(true);
  }

  closeCash(): void {
    this.cashOpen.set(false);
    this.focusScan();
  }

  cashRecorded(): void {
    this.closeCash();
    this.loadSession(this.registerId());
  }

  startOpen(): void {
    if (!this.canManageShift()) return;
    this.openFloat.set(this.settings()?.enforceFloatAtOpen === false ? 0 : null);
    this.shiftNotes.set('');
    this.openShiftOpen.set(true);
  }

  submitOpenShift(): void {
    const reg = this.registers().find((r) => r.id === this.registerId());
    const float = this.openFloat();
    if (!reg || float === null || !this.canManageShift()) return;
    this.shiftBusy.set(true);
    this.sessionsApi.openSession({ legalEntityId: reg.legalEntityId, registerId: reg.id, openingCash: float, notes: this.shiftNotes() || null }).subscribe({
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
    if (!this.canManageShift()) return;
    this.closeCash_.set(null); // counted, not copied from what the system expects
    this.closeNotes.set('');
    this.loadHeld();
    // Other tills or the back office may have moved cash since this screen last looked.
    this.loadSession(this.registerId(), () => this.closeShiftOpen.set(true));
  }

  submitCloseShift(): void {
    const s = this.session();
    const counted = this.closeCash_();
    if (!s || counted === null || !this.canManageShift()) return;
    this.shiftBusy.set(true);
    this.sessionsApi.closeSession(s.id, { actualCash: counted, notes: this.closeNotes() || null }).subscribe({
      next: () => {
        this.shiftBusy.set(false);
        this.closeShiftOpen.set(false);
        this.session.set(null);
        this.message.success(`Shift closed on ${s.registerName}.`);
        this.sessionsApi.report(s.id).subscribe({
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
    if (!s || !this.store.hasPermission('pos.sessions.read')) return;
    this.sessionsApi.report(s.id).subscribe({
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
