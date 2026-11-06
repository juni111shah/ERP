import { DecimalPipe } from '@angular/common';
import { afterNextRender, ChangeDetectionStrategy, Component, computed, ElementRef, HostListener, inject, OnDestroy, signal, viewChild } from '@angular/core';
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
} from '../../core/api/pos.api';
import { problemMessage } from '../../core/http/problem-details';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { ErpDatePipe } from '../../shared/display.pipes';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { KeyboardClickDirective } from '../../shared/keyboard-click.directive';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { ThermalReceiptComponent, ThermalReceiptData } from '../../shared/thermal-receipt.component';
import { CartLine, cartTotals, fromHeldOrder, lineAmounts, parseScan, repriceHeld, round2, roundQty, Tender, toOrderLine, toPayments } from './pos-cart';
import { CashDialogShift, PosCashDialogComponent } from './pos-cash-dialog.component';
import { PosPaymentDialogComponent } from './pos-payment-dialog.component';
import { returnReceipt, saleReceipt } from './pos-receipt';
import { PosReturnDialogComponent } from './pos-return-dialog.component';
import { PosShiftReportComponent } from './pos-shift-report.component';

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
  template: `
    <div class="till" data-testid="pos-terminal">
      <header class="bar" data-testid="pos-header">
        <nz-select class="register" [ngModel]="registerId()" (ngModelChange)="selectRegister($event)" nzPlaceHolder="Choose a register" aria-label="Register" data-testid="pos-register">
          @for (r of registers(); track r.id) { <nz-option [nzValue]="r.id" [nzLabel]="r.name"></nz-option> }
        </nz-select>

        @if (session(); as s) {
          <span class="pill ok" data-testid="pos-shift-open" nz-tooltip [nzTooltipTitle]="'Shift open since ' + (s.openedAt | erpDate)"><span nz-icon nzType="check-circle"></span> {{ s.cashierName }}</span>
          <span class="spacer"></span>
          <div class="cash" nz-tooltip nzTooltipTitle="Cash the drawer should hold now">
            <small>In drawer</small>
            <strong data-testid="pos-expected-cash">{{ currency() }} {{ s.expectedCash | number:'1.2-2' }}</strong>
          </div>
          <ng-container *erpHasPermission="'pos.sessions.manage'">
            <button nz-button type="button" (click)="cashOpen.set(true)" data-testid="pos-cash-move"><span nz-icon nzType="swap"></span>Cash <kbd>F10</kbd></button>
          </ng-container>
          <ng-container *erpHasPermission="'pos.sessions.read'">
            <button nz-button type="button" (click)="showShiftReport()" data-testid="pos-x-report"><span nz-icon nzType="file-text"></span>Report</button>
          </ng-container>
          <ng-container *erpHasPermission="'pos.sessions.manage'">
            <button nz-button nzDanger type="button" (click)="startClose()" data-testid="pos-close-shift"><span nz-icon nzType="poweroff"></span>Close shift</button>
          </ng-container>
        } @else if (shiftFailed()) {
          <span class="pill warn"><span nz-icon nzType="warning"></span> Shift status unknown</span>
          <span class="spacer"></span>
          <button nz-button type="button" (click)="selectRegister(registerId())"><span nz-icon nzType="reload"></span>Try again</button>
        } @else if (registerId() && !shiftLoading()) {
          <span class="pill off" data-testid="pos-shift-none"><span nz-icon nzType="close-circle"></span> No open shift</span>
          <span class="spacer"></span>
          <button *erpHasPermission="'pos.sessions.manage'" nz-button nzType="primary" type="button" (click)="startOpen()" data-testid="pos-open-shift"><span nz-icon nzType="play-circle"></span>Open shift</button>
        }
      </header>

      @if (bootFailed()) {
        <div class="center"><erp-state-panel kind="error" title="The till could not start." hint="Check your connection and try again." actionLabel="Try again" (action)="boot()" /></div>
      } @else if (booted() && !registers().length) {
        <div class="center"><erp-state-panel title="No registers yet" hint="Add a register under Registers, then come back to start selling." icon="desktop" /></div>
      } @else {
        <div class="main">
          <section class="left">
            <div class="scan">
              <div class="scan-box">
                <span nz-icon nzType="barcode" class="scan-icon"></span>
                <input #scanBox class="scan-input" aria-label="Scan or search products" placeholder="Scan a barcode, or type a code or name" autocomplete="off" spellcheck="false" data-testid="pos-search"
                  [ngModel]="searchQuery()" (ngModelChange)="onSearch($event)" (keydown)="onScanKey($event)" />
                @if (searchQuery()) { <button type="button" class="scan-clear" aria-label="Clear search" (click)="clearSearch()"><span nz-icon nzType="close"></span></button> }
              </div>
              <button *erpHasPermission="'pos.orders.override-price'" nz-button nzSize="large" type="button" (click)="startOpenItem()" nz-tooltip nzTooltipTitle="Sell something that is not in the catalogue" data-testid="pos-open-item"><span nz-icon nzType="plus"></span>Other item</button>
            </div>

            <div class="catalog">
              @if (catalogFailed()) {
                <erp-state-panel kind="error" title="The products could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadCatalog('')" />
              } @else if (catalogLoading() && !catalog().length) {
                <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }" />
              } @else if (!catalog().length && !searchQuery().trim()) {
                <erp-state-panel title="No products to sell" hint="Add active items with a sales price under Master Data, then come back." icon="shop" />
              } @else {
                <div class="tiles" data-testid="pos-catalog">
                  @for (item of catalog(); track item.id) {
                    <button type="button" class="tile erp-lift" [disabled]="item.price === null || outOfStockBlocked(item)" [attr.title]="item.name" (click)="addItem(item)" [attr.data-testid]="'pos-item-' + item.sku">
                      <span class="tile-name">{{ item.name }}</span>
                      <span class="tile-code">{{ item.sku }}</span>
                      <span class="tile-foot">
                        @if (item.price !== null) {
                          <span class="tile-price"><small>{{ currency() }}</small> {{ item.price | number:'1.2-2' }}</span>
                        } @else {
                          <span class="tile-none">No price</span>
                        }
                        @if (item.onHand !== null) {
                          <span class="stock" [class.out]="item.onHand <= 0" [class.low]="item.onHand > 0 && item.onHand <= 5" [attr.data-testid]="'pos-stock-' + item.sku">{{ item.onHand <= 0 ? 'Out' : (item.onHand | number:'1.0-2') + ' left' }}</span>
                        }
                      </span>
                    </button>
                  } @empty {
                    <div class="none">
                      <span nz-icon nzType="search"></span>
                      <p>No product matches "{{ searchQuery() }}".</p>
                      <button nz-button type="button" (click)="clearSearch()">Clear search</button>
                    </div>
                  }
                </div>
                @if (catalogTotal() > catalog().length) {
                  <p class="more" data-testid="pos-catalog-note">Showing {{ catalog().length }} of {{ catalogTotal() }} products. Search to find the rest.</p>
                }
              }
            </div>

            <footer class="keys" aria-label="Keyboard shortcuts">
              <span><kbd>F2</kbd> Scan</span>
              <span><kbd>F3</kbd> Customer</span>
              <span><kbd>F4</kbd> Discount</span>
              <span><kbd>F6</kbd> Hold</span>
              <span><kbd>F7</kbd> Held sales</span>
              <span><kbd>F8</kbd> Return</span>
              <span><kbd>F9</kbd> Pay</span>
              <span><kbd>↑↓</kbd> Line</span>
              <span><kbd>+ −</kbd> Qty</span>
              <span><kbd>Del</kbd> Remove</span>
            </footer>
          </section>

          <section class="ticket">
            <div class="ticket-top">
              <button type="button" class="customer" (click)="startCustomer()" data-testid="pos-customer">
                <span nz-icon nzType="user"></span>
                <span class="customer-name">{{ customerName() }}</span>
                @if (customerId()) { <span class="customer-tag">Account</span> }
              </button>
              <button nz-button type="button" (click)="startHeld()" data-testid="pos-held" [disabled]="!registerId()">
                <span nz-icon nzType="pause-circle"></span>Held @if (heldCount()) { <b class="count" data-testid="pos-held-count">{{ heldCount() }}</b> }
              </button>
              <button *erpHasPermission="'pos.orders.refund'" nz-button type="button" (click)="startReturn()" [disabled]="!session()" data-testid="pos-return"><span nz-icon nzType="rollback"></span>Return</button>
            </div>

            <div class="lines" #linesBox>
              @for (line of cart(); track line.key; let i = $index) {
                <div class="line" [class.selected]="i === selected()" erpKeyboardClick erpKeyboardClickRole="" [attr.aria-label]="line.description" [attr.aria-current]="i === selected() ? 'true' : null" (click)="selected.set(i)" [attr.data-testid]="'pos-line-' + i">
                  <div class="line-main">
                    <span class="line-name" [attr.title]="line.description">{{ line.description }}</span>
                    <span class="line-meta">
                      {{ currency() }} {{ line.unitPrice | number:'1.2-2' }}
                      @if (line.discountPercent) { <span class="off">-{{ line.discountPercent }}%</span> }
                      @if (line.taxRate) { · Tax {{ line.taxRate }}% }
                      @if (line.onHand !== null && line.quantity > line.onHand) { <span class="off" data-testid="pos-line-short">· only {{ line.onHand | number:'1.0-2' }} in stock</span> }
                    </span>
                  </div>
                  <div class="stepper">
                    <button type="button" class="step" [attr.aria-label]="'One less ' + line.description" (click)="changeQty(i, line.quantity - 1); $event.stopPropagation()">−</button>
                    <span class="qty" [attr.data-testid]="'pos-line-qty-' + i">{{ line.quantity | number:'1.0-2' }}</span>
                    <button type="button" class="step" [attr.aria-label]="'One more ' + line.description" (click)="changeQty(i, line.quantity + 1); $event.stopPropagation()">+</button>
                  </div>
                  <strong class="line-total">{{ amount(line) | number:'1.2-2' }}</strong>
                  <button type="button" class="icon-btn" [attr.aria-label]="'Change ' + line.description" nz-tooltip nzTooltipTitle="Quantity, discount, price" (click)="startLineEdit(i); $event.stopPropagation()" [attr.data-testid]="'pos-line-edit-' + i"><span nz-icon nzType="edit"></span></button>
                  <button type="button" class="icon-btn danger" [attr.aria-label]="'Remove ' + line.description" nz-tooltip nzTooltipTitle="Remove" (click)="removeLine(i); $event.stopPropagation()"><span nz-icon nzType="delete"></span></button>
                </div>
              } @empty {
                <div class="empty">
                  <span nz-icon nzType="barcode"></span>
                  <p>The ticket is empty. Scan an item or tap a product to start the sale.</p>
                </div>
              }
            </div>

            <div class="sum">
              <div class="sum-row"><span>{{ totals().units | number:'1.0-2' }} {{ totals().units === 1 ? 'item' : 'items' }}</span>
                <button nz-button nzType="link" nzSize="small" nzDanger type="button" [disabled]="!cart().length" (click)="clearCart()" data-testid="pos-clear">Clear ticket</button></div>
              <div class="sum-row"><span>Subtotal</span><span class="num">{{ currency() }} {{ totals().gross | number:'1.2-2' }}</span></div>
              @if (totals().discount) { <div class="sum-row off" data-testid="pos-discount-total"><span>Discount</span><span class="num">-{{ currency() }} {{ totals().discount | number:'1.2-2' }}</span></div> }
              <div class="sum-row"><span>Tax</span><span class="num" data-testid="pos-tax-total">{{ currency() }} {{ totals().tax | number:'1.2-2' }}</span></div>
              <div class="grand"><span>Total</span><span class="num" data-testid="pos-total">{{ currency() }} {{ totals().total | number:'1.2-2' }}</span></div>
              <div class="sum-actions">
                <button nz-button nzSize="large" type="button" [disabled]="!cart().length" (click)="startDiscount()" data-testid="pos-discount"><span nz-icon nzType="percentage"></span>Discount</button>
                <button nz-button nzSize="large" type="button" [disabled]="!cart().length || !session() || holding()" (click)="holdSale()" data-testid="pos-hold"><span nz-icon nzType="pause-circle"></span>Hold</button>
              </div>
              <button *erpHasPermission="'pos.orders.create'" nz-button nzType="primary" nzBlock nzSize="large" class="pay" type="button" data-testid="pos-pay" [disabled]="!cart().length || !session()" (click)="startPayment()">
                <span nz-icon nzType="credit-card"></span> Pay {{ currency() }} {{ totals().total | number:'1.2-2' }} <kbd>F9</kbd>
              </button>
              @if (!session() && cart().length) { <p class="hint" data-testid="pos-pay-hint">Open a shift to take payment.</p> }
            </div>
          </section>
        </div>
      }

      <erp-pos-payment-dialog [open]="paymentOpen()" [total]="totals().total" [currency]="currency()" [customerName]="customerName()" [hasCustomer]="!!customerId()" [canUseAccount]="canSellOnAccount()" [busy]="paying()"
        (cancelled)="closePayment()" (completed)="takePayment($event)" />

      <erp-pos-return-dialog [open]="returnOpen()" [sessionId]="session()?.id ?? null" [currency]="currency()" [initialNumber]="returnNumber()"
        (closed)="closeReturn()" (done)="returnDone($event)" />

      <erp-pos-cash-dialog [open]="cashOpen()" [shifts]="cashShifts()" (closed)="closeCash()" (recorded)="cashRecorded()" />

      <!-- Receipt -->
      <nz-modal [nzVisible]="!!receipt()" [nzTitle]="receiptTitle()" [nzFooter]="receiptFooter" [nzWidth]="440" (nzOnCancel)="finishReceipt()" (nzAfterOpen)="focusNewSale()">
        <ng-container *nzModalContent>
          @if (receiptChange()) {
            <div class="change-banner" data-testid="pos-change"><span>Change to give</span><strong>{{ currency() }} {{ receiptChange() | number:'1.2-2' }}</strong></div>
          }
          @if (receipt(); as rc) { <erp-thermal-receipt [receipt]="rc" /> }
        </ng-container>
        <ng-template #receiptFooter>
          <a nz-button nzSize="large" [href]="receiptPrintUrl()" target="_blank" rel="noopener" data-testid="pos-print"><span nz-icon nzType="printer"></span>Print</a>
          <button #newSale nz-button nzType="primary" nzSize="large" (click)="finishReceipt()" data-testid="pos-new-sale">New sale</button>
        </ng-template>
      </nz-modal>

      <!-- Open shift -->
      <nz-modal [nzVisible]="openShiftOpen()" nzTitle="Open shift" nzOkText="Open shift" [nzOkLoading]="shiftBusy()" [nzOkDisabled]="openFloat() === null" (nzAfterOpen)="focusField('pos-open-float')" (nzOnCancel)="openShiftOpen.set(false)" (nzOnOk)="submitOpenShift()">
        <ng-container *nzModalContent>
          <div class="stack">
            <div>
              <label class="field" for="pos-open-float">Cash in the drawer now ({{ currency() }})</label>
              <nz-input-number nzId="pos-open-float" class="wide big" [ngModel]="openFloat()" (ngModelChange)="openFloat.set($event)" [nzMin]="0" (keydown.enter)="submitOpenShift()" data-testid="pos-open-float"></nz-input-number>
              @if (settings()?.enforceFloatAtOpen) { <small class="muted">Count the drawer and enter what is there, even if that is 0.</small> }
            </div>
            <div>
              <label class="field" for="pos-open-notes">Notes</label>
              <input id="pos-open-notes" nz-input aria-label="Notes" [ngModel]="shiftNotes()" (ngModelChange)="shiftNotes.set($event)" placeholder="Optional" />
            </div>
          </div>
        </ng-container>
      </nz-modal>

      <!-- Close shift -->
      <nz-modal [nzVisible]="closeShiftOpen()" nzTitle="Close shift" nzOkText="Close shift" nzOkDanger [nzOkLoading]="shiftBusy()" [nzOkDisabled]="closeCash_() === null" [nzWidth]="480" (nzAfterOpen)="focusField('pos-close-cash')" (nzOnCancel)="closeShiftOpen.set(false)" (nzOnOk)="submitCloseShift()">
        <ng-container *nzModalContent>
          @if (session(); as s) {
            <div class="stack">
              @if (heldCount()) { <nz-alert nzType="warning" nzShowIcon [nzMessage]="heldCount() + (heldCount() === 1 ? ' held sale stays' : ' held sales stay') + ' on hold. Nothing was charged for them.'" data-testid="pos-close-held"></nz-alert> }
              <div>
                <label class="field" for="pos-close-cash">Cash counted in the drawer ({{ currency() }})</label>
                <nz-input-number nzId="pos-close-cash" class="wide big" [ngModel]="closeCash_()" (ngModelChange)="closeCash_.set($event)" [nzMin]="0" data-testid="pos-close-cash"></nz-input-number>
              </div>
              <div class="variance">
                <div class="variance-row"><span>Expected</span><strong class="num">{{ currency() }} {{ s.expectedCash | number:'1.2-2' }}</strong></div>
                <div class="variance-row"><span>{{ closeDifference() < 0 ? 'Short' : closeDifference() > 0 ? 'Over' : 'Difference' }}</span>
                  <strong class="num" [class.short]="closeDifference() < 0" [class.over]="closeDifference() > 0" data-testid="pos-close-difference">{{ currency() }} {{ closeDifference() | number:'1.2-2' }}</strong></div>
                @if (closeDifference() !== 0 && closeCash_() !== null) { <small class="muted">The difference posts to the ledger as cash over or short.</small> }
              </div>
              <div>
                <label class="field" for="pos-close-notes">Notes</label>
                <input id="pos-close-notes" nz-input aria-label="Notes" [ngModel]="closeNotes()" (ngModelChange)="closeNotes.set($event)" placeholder="Optional" />
              </div>
            </div>
          }
        </ng-container>
      </nz-modal>

      <!-- Shift report -->
      <nz-modal [nzVisible]="!!shiftReport()" [nzTitle]="shiftReport()?.session?.status === 'closed' ? 'Shift closed' : 'Shift report'" [nzFooter]="shiftFooter" [nzWidth]="560" (nzOnCancel)="closeShiftReport()">
        <ng-container *nzModalContent>
          @if (shiftReport(); as rep) { <erp-pos-shift-report [report]="rep" [currency]="currency()" /> }
        </ng-container>
        <ng-template #shiftFooter>
          @if (shiftReport(); as rep) { <a nz-button [href]="'/print/pos-shift/' + rep.session.id" target="_blank" rel="noopener" data-testid="pos-print-shift"><span nz-icon nzType="printer"></span>Print</a> }
          <button nz-button nzType="primary" (click)="closeShiftReport()" data-testid="pos-shift-report-close">Close</button>
        </ng-template>
      </nz-modal>

      <!-- Customer -->
      <nz-modal [nzVisible]="customerOpen()" nzTitle="Customer" [nzFooter]="customerFooter" [nzWidth]="480" (nzAfterOpen)="focusCustomer()" (nzOnCancel)="closeCustomer()">
        <ng-container *nzModalContent>
          <div class="stack">
            <input #customerBox nz-input nzSize="large" aria-label="Search customers" placeholder="Search name, phone or code" autocomplete="off" [ngModel]="customerSearch()" (ngModelChange)="onCustomerSearch($event)" (keydown.enter)="pickFirstCustomer($event)" data-testid="pos-customer-search" />
            <div class="pick-list">
              @for (c of customers(); track c.id) {
                <button type="button" class="pick" (click)="chooseCustomer(c)" [attr.data-testid]="'pos-customer-' + $index">
                  <strong>{{ c.name }}</strong><small class="muted">{{ c.code }} @if (c.phone) { · {{ c.phone }} }</small>
                </button>
              } @empty {
                <p class="muted">{{ customersLoading() ? 'Searching…' : 'No customer found.' }}</p>
              }
            </div>
            <div>
              <label class="field" for="pos-customer-name">Or just a name on the receipt</label>
              <input id="pos-customer-name" nz-input aria-label="Name on the receipt" [ngModel]="customerTyped()" (ngModelChange)="customerTyped.set($event)" (keydown.enter)="useTypedCustomer($event)" placeholder="Name only, no account" />
            </div>
          </div>
        </ng-container>
        <ng-template #customerFooter>
          <button nz-button (click)="setWalkIn()" data-testid="pos-walk-in">Walk-in customer</button>
          <button nz-button nzType="primary" [disabled]="!customerTyped().trim()" (click)="useTypedCustomer()">Use this name</button>
        </ng-template>
      </nz-modal>

      <!-- Discount -->
      <nz-modal [nzVisible]="discountOpen()" nzTitle="Discount on the whole ticket" nzOkText="Apply" [nzOkDisabled]="discountInvalid()" (nzAfterOpen)="focusDiscount()" (nzOnCancel)="closeDiscount()" (nzOnOk)="applyDiscount()">
        <ng-container *nzModalContent>
          <div class="stack" (keydown.enter)="applyDiscount()">
            <div>
              <label class="field" for="pos-discount-input">Percent off every line</label>
              <nz-input-number #discountBox nzId="pos-discount-input" class="wide big" [ngModel]="discountValue()" (ngModelChange)="discountValue.set($event ?? 0)" [nzMin]="0" [nzMax]="100" data-testid="pos-discount-input"></nz-input-number>
              <small class="muted" data-testid="pos-discount-limit">Your limit is {{ discountLimit() }}%.</small>
            </div>
            <div class="chips">
              @for (d of discountChips(); track d) { <button nz-button type="button" (click)="discountValue.set(d)">{{ d }}%</button> }
            </div>
            @if (discountInvalid()) { <nz-alert nzType="error" nzShowIcon [nzMessage]="'Above your limit of ' + discountLimit() + '%.'"></nz-alert> }
          </div>
        </ng-container>
      </nz-modal>

      <!-- Line -->
      <nz-modal [nzVisible]="editIndex() !== null" [nzTitle]="editing()?.description ?? 'Line'" nzOkText="Apply" [nzOkDisabled]="lineInvalid()" (nzOnCancel)="editIndex.set(null); focusScan()" (nzOnOk)="applyLineEdit()">
        <ng-container *nzModalContent>
          <div class="stack" (keydown.enter)="applyLineEdit()">
            <div>
              <label class="field" for="pos-edit-qty">Quantity</label>
              <nz-input-number nzId="pos-edit-qty" class="wide big" [ngModel]="editQty()" (ngModelChange)="editQty.set($event ?? 0)" [nzMin]="0" data-testid="pos-edit-qty"></nz-input-number>
            </div>
            <div>
              <label class="field" for="pos-edit-discount">Discount %</label>
              <nz-input-number nzId="pos-edit-discount" class="wide" [ngModel]="editDiscount()" (ngModelChange)="editDiscount.set($event ?? 0)" [nzMin]="0" [nzMax]="100" data-testid="pos-edit-discount"></nz-input-number>
              <small class="muted">Your limit is {{ discountLimit() }}%.</small>
            </div>
            @if (canOverridePrice()) {
              <div>
                <label class="field" for="pos-edit-price">Price ({{ currency() }})</label>
                <nz-input-number nzId="pos-edit-price" class="wide" [ngModel]="editPrice()" (ngModelChange)="editPrice.set($event ?? 0)" [nzMin]="0" data-testid="pos-edit-price"></nz-input-number>
              </div>
            }
          </div>
        </ng-container>
      </nz-modal>

      <!-- Other item -->
      <nz-modal [nzVisible]="openItemOpen()" nzTitle="Other item" nzOkText="Add to ticket" [nzOkDisabled]="!openItemName().trim() || openItemPrice() <= 0" (nzOnCancel)="openItemOpen.set(false); focusScan()" (nzOnOk)="addOpenItem()">
        <ng-container *nzModalContent>
          <div class="stack">
            <div>
              <label class="field" for="pos-open-item-name">Description</label>
              <input id="pos-open-item-name" nz-input [ngModel]="openItemName()" (ngModelChange)="openItemName.set($event)" maxlength="200" data-testid="pos-open-item-name" />
            </div>
            <div>
              <label class="field" for="pos-open-item-price">Price ({{ currency() }})</label>
              <nz-input-number nzId="pos-open-item-price" class="wide" [ngModel]="openItemPrice()" (ngModelChange)="openItemPrice.set($event ?? 0)" [nzMin]="0" data-testid="pos-open-item-price"></nz-input-number>
            </div>
            <div>
              <label class="field" for="pos-open-item-tax">Tax %</label>
              <nz-input-number nzId="pos-open-item-tax" class="wide" [ngModel]="openItemTax()" (ngModelChange)="openItemTax.set($event ?? 0)" [nzMin]="0" [nzMax]="100"></nz-input-number>
            </div>
          </div>
        </ng-container>
      </nz-modal>

      <!-- Held sales -->
      <nz-modal [nzVisible]="heldOpen()" nzTitle="Held sales" [nzFooter]="null" [nzWidth]="600" (nzOnCancel)="heldOpen.set(false); focusScan()">
        <ng-container *nzModalContent>
          <div class="pick-list" data-testid="pos-held-list">
            @for (o of held(); track o.id) {
              <div class="held">
                <div class="held-main">
                  <strong>{{ o.customerName }}</strong>
                  <small class="muted">{{ o.orderDate | erpDate }} · {{ o.cashierName }} · {{ o.lines.length }} {{ o.lines.length === 1 ? 'line' : 'lines' }}</small>
                  <small class="muted held-items">{{ heldSummary(o) }}</small>
                </div>
                <strong class="num">{{ currency() }} {{ o.totalAmount | number:'1.2-2' }}</strong>
                <button nz-button nzType="primary" type="button" (click)="resumeHeld(o)" [attr.data-testid]="'pos-resume-' + $index">Resume</button>
                <button nz-button nzDanger type="button" (click)="discardHeld(o)" [attr.data-testid]="'pos-discard-' + $index">Discard</button>
              </div>
            } @empty {
              <erp-state-panel title="Nothing on hold" hint="Press F6 on a ticket to put it aside and serve the next customer." icon="pause-circle" />
            }
          </div>
        </ng-container>
      </nz-modal>
    </div>
  `,
  styles: [`
    /* Fills the space the shell leaves, so the Pay button is always on screen. */
    .till { display: flex; flex-direction: column; height: calc(100vh - 106px); min-height: 520px; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: var(--erp-radius-card, 8px); overflow: hidden; }
    kbd { display: inline-block; padding: 0 4px; font: 600 10px/1.5 inherit; color: inherit; background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-default); border-radius: 3px; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .muted { color: var(--erp-color-text-muted); }
    .center { flex: 1; display: grid; place-items: center; padding: 24px; }

    .bar { display: flex; align-items: center; flex-wrap: wrap; gap: 8px 10px; padding: 8px 12px; border-bottom: 1px solid var(--erp-color-border-subtle); }
    .register { width: 210px; }
    .spacer { flex: 1; }
    .pill { display: inline-flex; align-items: center; gap: 6px; padding: 3px 10px; font-size: 13px; font-weight: 600; border-radius: 999px; }
    .pill.ok { background: var(--erp-color-success-bg); color: var(--erp-color-success-text); }
    .pill.off { background: var(--erp-color-error-bg); color: var(--erp-color-error-text); }
    .pill.warn { background: var(--erp-color-warning-bg, var(--erp-color-bg-subtle)); color: var(--erp-color-warning-text, var(--erp-color-text-primary)); }
    .cash { display: flex; flex-direction: column; align-items: flex-end; line-height: 1.15; }
    .cash small { font-size: 11px; color: var(--erp-color-text-muted); }
    .cash strong { font-size: 15px; font-variant-numeric: tabular-nums; }

    .main { display: flex; flex: 1; min-height: 0; }
    .left { flex: 1; min-width: 0; display: flex; flex-direction: column; background: var(--erp-color-bg-subtle); border-inline-end: 1px solid var(--erp-color-border-subtle); }
    .scan { display: flex; align-items: center; gap: 8px; padding: 10px 12px; background: var(--erp-color-bg-surface); border-bottom: 1px solid var(--erp-color-border-subtle); }
    .scan-box { flex: 1; display: flex; align-items: center; gap: 8px; height: 48px; padding: 0 12px; background: var(--erp-color-bg-surface); border: 2px solid var(--erp-color-primary); border-radius: 10px; }
    .scan-icon { font-size: 20px; color: var(--erp-color-primary); }
    .scan-input { flex: 1; min-width: 0; height: 100%; font: inherit; font-size: 16px; color: var(--erp-color-text-primary); background: transparent; border: 0; outline: 0; }
    .scan-clear { width: 32px; height: 32px; color: var(--erp-color-text-muted); background: none; border: 0; border-radius: 6px; cursor: pointer; }
    .catalog { flex: 1; min-height: 0; overflow-y: auto; padding: 10px 12px; }
    .tiles { display: grid; grid-template-columns: repeat(auto-fill, minmax(142px, 1fr)); gap: 8px; align-content: start; }
    .tile { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); display: flex; flex-direction: column; gap: 2px; min-width: 0; min-height: 96px; padding: 9px 10px; font: inherit; color: inherit; text-align: start; border-radius: 10px; cursor: pointer; user-select: none; }
    .tile:active:not(:disabled) { background: var(--erp-color-primary-subtle); }
    .tile:focus-visible { outline: 2px solid var(--erp-color-primary); outline-offset: 1px; }
    .tile:disabled { cursor: not-allowed; opacity: .6; }
    .tile-name { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; font-size: 13px; font-weight: 600; line-height: 1.25; color: var(--erp-color-text-primary); }
    .tile-code { font-size: 11px; color: var(--erp-color-text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .tile-foot { display: flex; align-items: baseline; justify-content: space-between; gap: 6px; margin-top: auto; }
    .tile-price { font-size: 15px; font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; color: var(--erp-color-primary-text); }
    .tile-price small { font-size: 10.5px; font-weight: 600; }
    .tile-none { font-size: 12px; color: var(--erp-color-error-text); }
    .stock { font-size: 11px; white-space: nowrap; color: var(--erp-color-text-muted); }
    .stock.low { font-weight: 600; color: var(--erp-color-warning-text, var(--erp-color-text-primary)); }
    .stock.out { font-weight: 700; color: var(--erp-color-error-text); }
    .none { grid-column: 1 / -1; padding: 40px 16px; text-align: center; color: var(--erp-color-text-muted); }
    .none p { margin: 8px 0 12px; }
    .more { margin: 10px 0 0; text-align: center; color: var(--erp-color-text-muted); }
    .keys { display: flex; flex-wrap: wrap; gap: 4px 12px; padding: 6px 12px; font-size: 11px; color: var(--erp-color-text-muted); background: var(--erp-color-bg-surface); border-top: 1px solid var(--erp-color-border-subtle); }

    .ticket { flex: 0 0 440px; min-width: 0; display: flex; flex-direction: column; background: var(--erp-color-bg-surface); }
    .ticket-top { display: flex; align-items: center; gap: 6px; padding: 8px 10px; border-bottom: 1px solid var(--erp-color-border-subtle); }
    .customer { flex: 1; min-width: 0; display: flex; align-items: center; gap: 8px; height: 40px; padding: 0 10px; font: inherit; font-weight: 600; color: var(--erp-color-text-primary); background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-subtle); border-radius: 8px; cursor: pointer; }
    .customer:hover { border-color: var(--erp-color-primary); }
    .customer-name { flex: 1; min-width: 0; text-align: start; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
    .customer-tag { padding: 0 6px; font-size: 11px; color: var(--erp-color-primary-text); background: var(--erp-color-primary-subtle); border-radius: 4px; }
    .count { display: inline-block; min-width: 18px; margin-inline-start: 4px; padding: 0 5px; font-size: 11px; line-height: 18px; text-align: center; color: #fff; background: var(--erp-color-primary); border-radius: 9px; }

    .lines { flex: 1; min-height: 0; overflow-y: auto; padding: 8px 10px; display: flex; flex-direction: column; gap: 6px; }
    .line { display: grid; grid-template-columns: minmax(0, 1fr) auto 72px auto auto; align-items: center; gap: 6px; padding: 7px 8px; border: 1px solid var(--erp-color-border-subtle); border-radius: 10px; cursor: pointer; }
    .line.selected { border-color: var(--erp-color-primary); background: var(--erp-color-primary-subtle); }
    .line:focus-visible { outline: 2px solid var(--erp-color-primary); outline-offset: 1px; }
    .line-main { min-width: 0; }
    .line-name { display: block; font-size: 13.5px; font-weight: 600; line-height: 1.25; color: var(--erp-color-text-primary); overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
    .line-meta { font-size: 11.5px; color: var(--erp-color-text-muted); }
    .off { color: var(--erp-color-error-text); font-weight: 600; }
    .stepper { display: flex; align-items: center; gap: 3px; }
    .step { width: 36px; height: 36px; font-size: 18px; font-weight: 700; line-height: 1; color: var(--erp-color-text-primary); background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-default); border-radius: 8px; cursor: pointer; }
    .step:active { background: var(--erp-color-primary-subtle); }
    .qty { min-width: 26px; text-align: center; font-size: 14px; font-weight: 700; font-variant-numeric: tabular-nums; }
    .line-total { text-align: end; font-size: 14px; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .icon-btn { width: 30px; height: 36px; color: var(--erp-color-text-secondary); background: none; border: 0; border-radius: 6px; cursor: pointer; }
    .icon-btn:hover { background: var(--erp-color-bg-hover); }
    .icon-btn.danger { color: var(--erp-color-error-text); }
    .empty { padding: 48px 16px; text-align: center; color: var(--erp-color-text-muted); }
    .empty [nz-icon] { font-size: 34px; opacity: .6; }
    .empty p { margin: 10px 0 0; }

    .sum { padding: 8px 12px 12px; background: var(--erp-color-bg-subtle); border-top: 1px solid var(--erp-color-border-subtle); }
    .sum-row { display: flex; justify-content: space-between; align-items: center; min-height: 22px; font-size: 13px; color: var(--erp-color-text-secondary); }
    .sum-row.off { color: var(--erp-color-error-text); }
    .grand { display: flex; justify-content: space-between; align-items: baseline; margin: 4px 0 8px; padding-top: 6px; font-size: 26px; font-weight: 800; color: var(--erp-color-text-primary); border-top: 1px solid var(--erp-color-border-default); }
    .sum-actions { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-bottom: 8px; }
    .sum-actions button { height: 44px; }
    .pay { height: 56px; font-size: 18px; font-weight: 700; border-radius: 10px; }
    .pay kbd { margin-inline-start: 6px; color: var(--erp-color-text-primary); }
    .hint { margin: 6px 0 0; text-align: center; color: var(--erp-color-error-text); }

    .stack { display: flex; flex-direction: column; gap: 12px; }
    .field { display: block; margin-bottom: 4px; font-weight: 600; }
    .wide { width: 100%; }
    :host ::ng-deep .big input { height: 48px; font-size: 20px; font-variant-numeric: tabular-nums; }
    .chips { display: flex; flex-wrap: wrap; gap: 8px; }
    .change-banner { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 10px; padding: 12px 14px; border-radius: 10px; background: var(--erp-color-success-bg); color: var(--erp-color-success-text); }
    .change-banner strong { font-size: 28px; font-variant-numeric: tabular-nums; }
    .variance { display: flex; flex-direction: column; gap: 4px; padding: 10px 12px; border-radius: 8px; background: var(--erp-color-bg-subtle); }
    .variance-row { display: flex; justify-content: space-between; }
    .short { color: var(--erp-color-error-text); }
    .over { color: var(--erp-color-success-text); }
    .pick-list { display: flex; flex-direction: column; gap: 6px; max-height: 340px; overflow-y: auto; }
    .pick { display: flex; flex-direction: column; align-items: flex-start; gap: 1px; min-height: 48px; padding: 6px 10px; font: inherit; color: inherit; text-align: start; background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: 8px; cursor: pointer; }
    .pick:hover, .pick:focus-visible { border-color: var(--erp-color-primary); }
    .held { display: grid; grid-template-columns: minmax(0, 1fr) auto auto auto; align-items: center; gap: 10px; padding: 8px 10px; border: 1px solid var(--erp-color-border-subtle); border-radius: 8px; }
    .held-main { display: flex; flex-direction: column; min-width: 0; }
    .held-items { overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }

    @media (max-width: 1100px) {
      .ticket { flex-basis: 380px; }
      .line { grid-template-columns: minmax(0, 1fr) auto 64px auto; }
      .line .icon-btn:not(.danger) { display: none; }
    }
    @media (max-width: 860px) {
      .till { height: auto; overflow: visible; }
      .main { flex-direction: column; }
      .left { border-inline-end: 0; border-bottom: 1px solid var(--erp-color-border-subtle); }
      .catalog { max-height: 48vh; }
      .ticket { flex: none; }
      .lines { max-height: 42vh; }
      .keys { display: none; }
    }
  `],
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
  readonly totals = computed(() => cartTotals(this.cart()));

  readonly paymentOpen = signal(false);
  readonly paying = signal(false);
  readonly holding = signal(false);

  readonly receipt = signal<ThermalReceiptData | null>(null);
  readonly receiptTitle = signal('Sale complete');
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
    this.idempotencyKey = crypto.randomUUID(); // a different ticket is a different sale
    if (!lines.length) this.parkedOrderId = null; // the held copy stays on hold: it was not sold
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
        this.message.success('Sale held.');
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
        this.message.success('Held sale discarded.');
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
    this.receiptTitle.set('Sale complete');
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
    this.receiptTitle.set('Refund complete');
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
        this.message.success('Shift opened.');
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
        this.message.success('Shift closed.');
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
