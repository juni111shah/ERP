import { DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
import { afterNextRender, ChangeDetectionStrategy, Component, computed, ElementRef, inject, OnDestroy, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ItemDto, ItemsApi, TaxCodesApi } from '../../core/api/admin.api';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { ThermalReceiptComponent, ThermalReceiptData } from '../../shared/thermal-receipt.component';
import {
  CreatePosOrderCommand,
  PosOrderDto,
  PosOrderLineInput,
  PosOrdersApi,
  PosPaymentMethod,
  PosRegisterDto,
  PosRegistersApi,
  PosSessionDto,
  PosSessionsApi,
} from '../../core/api/pos.api';

interface CatalogItem {
  id: string;
  code: string;
  name: string;
  /** Null when the item has no sales price: it cannot be sold from the till until it has one. */
  price: number | null;
  taxCodeId?: string | null;
}

interface CartItem extends PosOrderLineInput {
  itemCode: string;
  unitPrice: number;
  taxRate: number;
  discountPercent: number;
  lineNet: number;
  lineTax: number;
  lineTotal: number;
}

const REGISTER_KEY = 'erp.pos.register';
const CATALOG_PAGE = 200;
const ACTIVE_ONLY = [{ field: 'isActive', op: 'eq' as const, value: true }];
const round2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100;

/** The till: scan or tap products onto the ticket, take payment, print the receipt. */
@Component({
  selector: 'erp-pos-terminal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzSkeletonModule,
    NzTagModule,
    NzToolTipModule,
    NzModalModule,
    NzRadioModule,
    NzDividerModule,
    NzAlertModule,
    HasPermissionDirective,
    StatePanelComponent,
    ThermalReceiptComponent,
  ],
  template: `
    <div class="pos-terminal-layout">
      <header class="terminal-header" data-testid="pos-header">
        <label class="register-select-box">
          <span>Register</span>
          <nz-select [ngModel]="selectedRegisterId()" (ngModelChange)="onRegisterChange($event)" nzPlaceHolder="Choose a register" style="width: 200px;" aria-label="Register" data-testid="pos-register">
            @for (r of registers(); track r.id) {
              <nz-option [nzValue]="r.id" [nzLabel]="r.name"></nz-option>
            }
          </nz-select>
        </label>

        @if (activeSession(); as session) {
          <nz-tag nzColor="success" data-testid="pos-shift-open">
            <span nz-icon nzType="check-circle"></span> Shift open · {{ session.cashierName }}
          </nz-tag>
          <span class="header-spacer"></span>
          <div class="drawer-cash-badge">
            <span>Expected cash</span>
            <strong>{{ currency() }} {{ session.expectedCash | number:'1.2-2' }}</strong>
          </div>
          <button *erpHasPermission="'pos.sessions.manage'" nz-button nzDanger (click)="openCloseShiftModal()" data-testid="pos-close-shift">
            <span nz-icon nzType="poweroff"></span>Close Shift
          </button>
        } @else if (shiftFailed()) {
          <nz-tag nzColor="warning"><span nz-icon nzType="warning"></span> Shift status unknown</nz-tag>
          <span class="header-spacer"></span>
          <button nz-button (click)="onRegisterChange(selectedRegisterId())"><span nz-icon nzType="reload"></span>Try again</button>
        } @else if (selectedRegisterId() && !shiftLoading()) {
          <nz-tag nzColor="error" data-testid="pos-shift-none">
            <span nz-icon nzType="close-circle"></span> No open shift
          </nz-tag>
          <span class="header-spacer"></span>
          <button *erpHasPermission="'pos.sessions.manage'" nz-button nzType="primary" (click)="openShiftModalVisible.set(true)" data-testid="pos-open-shift">
            <span nz-icon nzType="play-circle"></span>Open Shift
          </button>
        }
      </header>

      <div class="terminal-main">
        <section class="catalog-section">
          <div class="catalog-search-bar">
            <nz-input-group [nzPrefix]="searchPrefix" nzSize="large">
              <input #searchInput
                nz-input
                nzSize="large"
                aria-label="Scan or search products"
                placeholder="Scan or type a SKU or product name, then press Enter"
                autocomplete="off"
                data-testid="pos-search"
                [ngModel]="searchQuery()"
                (ngModelChange)="onSearch($event)"
                (keydown.enter)="onSearchEnter($event)"
              />
            </nz-input-group>
            <ng-template #searchPrefix><span nz-icon nzType="barcode"></span></ng-template>
          </div>

          <div class="catalog-scroll">
            @if (catalogFailed()) {
              <erp-state-panel kind="error" title="The products could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadItemsCatalog()" />
            } @else if (catalogLoading() && !catalog().length) {
              <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }" />
            } @else if (!catalog().length) {
              <erp-state-panel title="No products to sell" hint="Add active items with a sales price under Master Data, then come back." icon="shop" />
            } @else {
              <div class="catalog-grid" data-testid="pos-catalog">
                @for (item of filteredCatalog(); track item.id) {
                  <button type="button" class="item-tile" [disabled]="item.price === null" [attr.title]="item.name" (click)="addToCart(item)" [attr.data-testid]="'pos-item-' + item.code">
                    <span class="item-tile-title">{{ item.name }}</span>
                    <span class="item-tile-code">{{ item.code }}</span>
                    @if (item.price !== null) {
                      <span class="item-tile-price"><small>{{ currency() }}</small> {{ item.price | number:'1.2-2' }}</span>
                    } @else {
                      <span class="item-tile-noprice">No price set</span>
                    }
                  </button>
                } @empty {
                  <div class="empty-catalog">
                    <span nz-icon nzType="search"></span>
                    <p>No product matches "{{ searchQuery() }}".</p>
                    <button nz-button type="button" (click)="clearSearch()">Clear search</button>
                  </div>
                }
              </div>
              @if (catalogTotal() > catalog().length && !searchQuery().trim()) {
                <p class="catalog-note" data-testid="pos-catalog-note">Showing {{ catalog().length }} of {{ catalogTotal() }} products. Search to find the rest.</p>
              }
            }
          </div>
        </section>

        <section class="ticket-section">
          <div class="ticket-header">
            <div class="ticket-title">
              <span nz-icon nzType="shopping-cart"></span> Current Ticket
              <span class="ticket-count" data-testid="pos-ticket-count">{{ itemCount() }} {{ itemCount() === 1 ? 'item' : 'items' }}</span>
            </div>
            <button nz-button nzType="link" nzDanger (click)="clearCart()" [disabled]="cart().length === 0">
              Clear
            </button>
          </div>

          <div class="customer-bar">
            <span nz-icon nzType="user" class="muted"></span>
            <input aria-label="Customer name" nz-input nzSize="small" placeholder="Walk-in Customer" [ngModel]="customerName()" (ngModelChange)="customerName.set($event)" />
          </div>

          <div class="cart-lines-container">
            @for (line of cart(); track line.itemCode; let i = $index) {
              <div class="cart-line-card">
                <div class="line-info">
                  <span class="line-name" [attr.title]="line.description">{{ line.description }}</span>
                  <span class="line-meta">{{ currency() }} {{ line.unitPrice | number:'1.2-2' }} · Tax {{ line.taxRate }}%</span>
                </div>

                <div class="line-qty-controls">
                  <button type="button" class="qty-btn" [attr.aria-label]="'One less ' + line.description" (click)="updateQuantity(i, line.quantity - 1)">-</button>
                  <span class="qty-val">{{ line.quantity }}</span>
                  <button type="button" class="qty-btn" [attr.aria-label]="'One more ' + line.description" (click)="updateQuantity(i, line.quantity + 1)">+</button>
                </div>

                <div class="line-total-col">
                  <strong>{{ line.lineTotal | number:'1.2-2' }}</strong>
                  <button type="button" class="line-delete-btn" [attr.aria-label]="'Remove ' + line.description" nz-tooltip nzTooltipTitle="Remove" (click)="removeLine(i)">
                    <span nz-icon nzType="delete"></span>
                  </button>
                </div>
              </div>
            } @empty {
              <div class="empty-cart-state">
                <span nz-icon nzType="barcode" class="empty-icon"></span>
                <p>The ticket is empty. Scan an item or tap a product to start the sale.</p>
              </div>
            }
          </div>

          <div class="ticket-footer">
            <div class="totals-breakdown">
              <div class="summary-row">
                <span>Subtotal</span>
                <span>{{ currency() }} {{ cartSubTotal() | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span>Tax</span>
                <span>{{ currency() }} {{ cartTaxTotal() | number:'1.2-2' }}</span>
              </div>
              <nz-divider style="margin: 6px 0;"></nz-divider>
              <div class="summary-row grand-total">
                <span>Total</span>
                <span>{{ currency() }} {{ cartTotal() | number:'1.2-2' }}</span>
              </div>
            </div>

            <button
              *erpHasPermission="'pos.orders.create'"
              nz-button
              nzType="primary"
              nzBlock
              nzSize="large"
              class="checkout-btn"
              data-testid="pos-pay"
              [disabled]="cart().length === 0 || !activeSession()"
              (click)="openPaymentModal()"
            >
              <span nz-icon nzType="credit-card"></span> Pay {{ currency() }} {{ cartTotal() | number:'1.2-2' }}
            </button>
            @if (!activeSession() && cart().length > 0) {
              <p class="pay-hint" data-testid="pos-pay-hint">Open a shift to take payment.</p>
            }
          </div>
        </section>
      </div>

      <!-- Payment -->
      <nz-modal
        [(nzVisible)]="paymentModalVisible"
        nzTitle="Take Payment"
        [nzFooter]="paymentModalFooter"
        (nzAfterOpen)="focusTender()"
        (nzOnCancel)="paymentModalVisible.set(false)"
      >
        <ng-container *nzModalContent>
          <div class="payment-modal-body">
            <div class="payment-due-banner">
              <span class="due-label">Amount due</span>
              <span class="due-amount">{{ currency() }} {{ cartTotal() | number:'1.2-2' }}</span>
            </div>

            <div class="tender-options">
              <nz-radio-group [(ngModel)]="paymentMethod" nzButtonStyle="solid" aria-label="Payment method">
                <label nz-radio-button nzValue="cash"><span nz-icon nzType="dollar"></span> Cash</label>
                <label nz-radio-button nzValue="card"><span nz-icon nzType="credit-card"></span> Card</label>
              </nz-radio-group>
            </div>

            @if (paymentMethod() === 'cash') {
              <div>
                <label class="field-label">Cash received</label>
                <nz-input-number
                  #tender
                  [(ngModel)]="tenderedCash"
                  [nzMin]="0"
                  [nzStep]="10"
                  aria-label="Cash received"
                  style="width: 100%; font-size: 18px;"
                  (keydown.enter)="onTenderEnter($event)"
                  data-testid="pos-tendered"
                ></nz-input-number>

                <div class="quick-cash-row">
                  <button nz-button nzSize="small" type="button" (click)="tenderedCash.set(cartTotal())">Exact</button>
                  @for (amount of quickCashOptions(); track amount) {
                    <button nz-button nzSize="small" type="button" (click)="tenderedCash.set(amount)">{{ amount | number:'1.0-2' }}</button>
                  }
                </div>

                @if (tenderedCash() < cartTotal()) {
                  <div class="change-display short">
                    <span>Still to pay</span>
                    <strong class="change-amount">{{ currency() }} {{ cartTotal() - tenderedCash() | number:'1.2-2' }}</strong>
                  </div>
                } @else {
                  <div class="change-display" data-testid="pos-change">
                    <span>Change to give</span>
                    <strong class="change-amount">{{ currency() }} {{ cashChange() | number:'1.2-2' }}</strong>
                  </div>
                }
              </div>
            } @else {
              <div>
                <label class="field-label">Card approval reference</label>
                <input aria-label="Card approval reference" nz-input placeholder="Optional" [(ngModel)]="cardReference" />
                <p class="pay-note">Take the payment on the card machine, then confirm here.</p>
              </div>
            }
          </div>
        </ng-container>

        <ng-template #paymentModalFooter>
          <button nz-button (click)="paymentModalVisible.set(false)">Cancel</button>
          <button nz-button nzType="primary" (click)="processPayment()" [nzLoading]="isProcessingPayment()" [disabled]="!canConfirmPayment()" data-testid="pos-confirm-payment">
            <span nz-icon nzType="check"></span> Confirm Payment
          </button>
        </ng-template>
      </nz-modal>

      <!-- Receipt -->
      <nz-modal
        [(nzVisible)]="receiptModalVisible"
        nzTitle="Sale Completed"
        [nzFooter]="receiptFooterTpl"
        [nzWidth]="420"
        (nzOnCancel)="finishSale()"
      >
        <ng-container *nzModalContent>
          @if (thermalReceiptData(); as rc) {
            <erp-thermal-receipt [receipt]="rc" />
          }
        </ng-container>
        <ng-template #receiptFooterTpl>
          @if (completedOrder(); as ord) {
            <a [href]="'/print/pos-receipt/' + ord.id" target="_blank" nz-button>
              <span nz-icon nzType="printer"></span> Print Receipt
            </a>
          }
          <button nz-button nzType="primary" (click)="finishSale()" data-testid="pos-new-sale">
            New Sale
          </button>
        </ng-template>
      </nz-modal>

      <!-- Open shift -->
      <nz-modal
        [(nzVisible)]="openShiftModalVisible"
        nzTitle="Open Shift"
        nzOkText="Open Shift"
        [nzOkLoading]="shiftBusy()"
        (nzOnCancel)="openShiftModalVisible.set(false)"
        (nzOnOk)="submitOpenShift()"
      >
        <ng-container *nzModalContent>
          <div class="modal-stack">
            <div>
              <label class="field-label">Opening cash in the drawer ({{ currency() }})</label>
              <nz-input-number [(ngModel)]="openFloatCash" [nzMin]="0" aria-label="Opening cash in the drawer" style="width: 100%;" data-testid="pos-open-float"></nz-input-number>
            </div>
            <div>
              <label class="field-label">Notes</label>
              <input aria-label="Notes" nz-input [(ngModel)]="shiftNotes" placeholder="Optional" />
            </div>
          </div>
        </ng-container>
      </nz-modal>

      <!-- Close shift -->
      <nz-modal
        [(nzVisible)]="closeShiftModalVisible"
        nzTitle="Close Shift"
        nzOkText="Close Shift"
        [nzOkLoading]="shiftBusy()"
        (nzOnCancel)="closeShiftModalVisible.set(false)"
        (nzOnOk)="submitCloseShift()"
      >
        <ng-container *nzModalContent>
          @if (activeSession(); as s) {
            <div class="modal-stack">
              <nz-alert
                nzType="info"
                [nzMessage]="'Expected cash in the drawer: ' + currency() + ' ' + (s.expectedCash | number:'1.2-2')"
                nzShowIcon
              ></nz-alert>
              <div>
                <label class="field-label">Cash counted ({{ currency() }})</label>
                <nz-input-number [(ngModel)]="actualCloseCash" [nzMin]="0" aria-label="Cash counted" style="width: 100%; font-size: 16px;" data-testid="pos-close-cash"></nz-input-number>
              </div>
              <div class="variance-box">
                <span>Difference</span>
                <strong [class.short]="actualCloseCash() < s.expectedCash" [class.over]="actualCloseCash() > s.expectedCash">
                  {{ currency() }} {{ actualCloseCash() - s.expectedCash | number:'1.2-2' }}
                </strong>
              </div>
              <div>
                <label class="field-label">Notes</label>
                <input aria-label="Notes" nz-input [(ngModel)]="closeNotes" placeholder="Optional" />
              </div>
            </div>
          }
        </ng-container>
      </nz-modal>
    </div>
  `,
  styles: [`
    /* Fills the space the shell leaves (its top bar and page padding), so the Pay button is always on screen. */
    .pos-terminal-layout {
      display: flex;
      flex-direction: column;
      height: calc(100vh - 106px);
      min-height: 480px;
      background: var(--erp-color-bg-surface);
      border: 1px solid var(--erp-color-border-subtle);
      border-radius: var(--erp-radius-card, 8px);
      overflow: hidden;
    }
    .terminal-header {
      padding: 8px 16px;
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 8px 12px;
      flex-shrink: 0;
      border-bottom: 1px solid var(--erp-color-border-subtle);
    }
    .header-spacer { flex: 1; }
    .register-select-box { display: inline-flex; align-items: center; gap: 8px; font-weight: 600; color: var(--erp-color-text-primary); }
    .drawer-cash-badge { display: inline-flex; align-items: baseline; gap: 8px; padding: 4px 10px; border-radius: 6px; background: var(--erp-color-bg-subtle); color: var(--erp-color-text-secondary); white-space: nowrap; }
    .drawer-cash-badge strong { color: var(--erp-color-text-primary); font-variant-numeric: tabular-nums; }

    .terminal-main { display: flex; flex: 1; min-height: 0; }
    .catalog-section { flex: 1; min-width: 0; display: flex; flex-direction: column; border-inline-end: 1px solid var(--erp-color-border-subtle); background: var(--erp-color-bg-subtle); }
    .catalog-search-bar { padding: 10px 12px; background: var(--erp-color-bg-surface); border-bottom: 1px solid var(--erp-color-border-subtle); }
    .catalog-scroll { flex: 1; min-height: 0; overflow-y: auto; padding: 10px 12px; }
    .catalog-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(128px, 1fr)); gap: 8px; align-content: start; }
    .catalog-note { margin: 10px 0 0; text-align: center; color: var(--erp-color-text-muted); }
    .item-tile {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
      padding: 7px 9px;
      text-align: start;
      font: inherit;
      color: inherit;
      background: var(--erp-color-bg-surface);
      border: 1px solid var(--erp-color-border-subtle);
      border-radius: 8px;
      cursor: pointer;
      user-select: none;
    }
    .item-tile:hover:not(:disabled) { border-color: var(--erp-color-primary); background: var(--erp-color-bg-hover); }
    .item-tile:active:not(:disabled) { background: var(--erp-color-primary-subtle); }
    .item-tile:focus-visible { outline: 2px solid var(--erp-color-primary); outline-offset: 1px; }
    .item-tile:disabled { cursor: not-allowed; opacity: .65; }
    .item-tile-title { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; min-height: 2.5em; font-size: 12.5px; font-weight: 600; line-height: 1.25; color: var(--erp-color-text-primary); }
    .item-tile-code { font-size: 10.5px; color: var(--erp-color-text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .item-tile-price { font-size: 15px; font-weight: 700; line-height: 1.3; font-variant-numeric: tabular-nums; white-space: nowrap; color: var(--erp-color-primary-text); }
    .item-tile-price small { font-size: 10.5px; font-weight: 600; }
    .item-tile-noprice { font-size: 12px; line-height: 1.6; color: var(--erp-color-error-text); }
    .empty-catalog { grid-column: 1 / -1; padding: 40px 16px; text-align: center; color: var(--erp-color-text-muted); }
    .empty-catalog p { margin: 8px 0 12px; }

    .ticket-section { flex: 0 0 400px; min-width: 0; display: flex; flex-direction: column; background: var(--erp-color-bg-surface); }
    .ticket-header { padding: 6px 12px 6px 16px; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--erp-color-border-subtle); }
    .ticket-title { display: flex; align-items: center; gap: 8px; font-size: 15px; font-weight: 700; color: var(--erp-color-text-primary); }
    .ticket-count { font-size: 12.5px; font-weight: 400; color: var(--erp-color-text-muted); }
    .customer-bar { padding: 6px 16px; display: flex; align-items: center; gap: 8px; background: var(--erp-color-bg-subtle); border-bottom: 1px solid var(--erp-color-border-subtle); }
    .muted { color: var(--erp-color-text-muted); }

    .cart-lines-container { flex: 1; min-height: 0; overflow-y: auto; padding: 8px 12px; display: flex; flex-direction: column; gap: 6px; }
    .cart-line-card { display: flex; justify-content: space-between; align-items: center; gap: 8px; padding: 6px 8px; border: 1px solid var(--erp-color-border-subtle); border-radius: 8px; }
    .line-info { flex: 1; min-width: 0; }
    .line-name { display: block; font-size: 13px; font-weight: 600; color: var(--erp-color-text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .line-meta { font-size: 11px; color: var(--erp-color-text-muted); white-space: nowrap; }
    .line-qty-controls { display: flex; align-items: center; gap: 4px; }
    .qty-btn { width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; font-weight: 700; color: var(--erp-color-text-primary); background: var(--erp-color-bg-subtle); border: 1px solid var(--erp-color-border-default); border-radius: 4px; cursor: pointer; }
    .qty-val { min-width: 22px; text-align: center; font-size: 13px; font-weight: 700; font-variant-numeric: tabular-nums; }
    .line-total-col { display: flex; align-items: center; gap: 4px; font-size: 13px; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .line-delete-btn { padding: 4px; line-height: 1; background: none; border: 0; border-radius: 4px; color: var(--erp-color-error-text); cursor: pointer; }

    .empty-cart-state { padding: 40px 16px; text-align: center; color: var(--erp-color-text-muted); }
    .empty-icon { font-size: 32px; margin-bottom: 10px; opacity: .6; }

    .ticket-footer { padding: 10px 16px 12px; background: var(--erp-color-bg-subtle); border-top: 1px solid var(--erp-color-border-subtle); }
    .totals-breakdown { margin-bottom: 8px; }
    .summary-row { display: flex; justify-content: space-between; margin-bottom: 2px; font-size: 13px; font-variant-numeric: tabular-nums; color: var(--erp-color-text-secondary); }
    .grand-total { font-size: 18px; font-weight: 800; color: var(--erp-color-text-primary); }
    .checkout-btn { height: 46px; font-size: 16px; font-weight: 700; border-radius: 8px; }
    .pay-hint { margin: 6px 0 0; text-align: center; color: var(--erp-color-error-text); }

    .payment-due-banner { margin-bottom: 16px; padding: 14px; text-align: center; border-radius: 8px; background: var(--erp-color-primary-subtle); }
    .due-label { display: block; font-size: 12px; font-weight: 600; color: var(--erp-color-text-secondary); }
    .due-amount { font-size: 28px; font-weight: 800; font-variant-numeric: tabular-nums; color: var(--erp-color-text-primary); }
    .tender-options { margin-bottom: 16px; }
    .field-label { display: block; margin-bottom: 6px; font-weight: 600; }
    .pay-note { margin: 6px 0 0; font-size: 12px; color: var(--erp-color-text-muted); }
    .quick-cash-row { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 8px; }
    .change-display { display: flex; justify-content: space-between; align-items: center; margin-top: 14px; padding: 12px; border-radius: 6px; background: var(--erp-color-success-bg); color: var(--erp-color-success-text); }
    .change-display.short { background: var(--erp-color-error-bg); color: var(--erp-color-error-text); }
    .change-amount { font-size: 18px; font-variant-numeric: tabular-nums; }
    .modal-stack { display: flex; flex-direction: column; gap: 14px; }
    .variance-box { display: flex; justify-content: space-between; padding: 8px 12px; font-size: 14px; border-radius: 6px; background: var(--erp-color-bg-subtle); }
    .variance-box strong { font-variant-numeric: tabular-nums; }
    .variance-box .short { color: var(--erp-color-error-text); }
    .variance-box .over { color: var(--erp-color-success-text); }

    @media (max-width: 1000px) {
      .pos-terminal-layout { height: auto; overflow: visible; }
      .terminal-main { flex-direction: column; }
      .catalog-section { border-inline-end: 0; border-bottom: 1px solid var(--erp-color-border-subtle); }
      .catalog-scroll { max-height: 50vh; }
      .ticket-section { flex: none; }
      .cart-lines-container { max-height: 40vh; }
    }
  `],
})
export class PosTerminalPage implements OnDestroy {
  private readonly registersApi = inject(PosRegistersApi);
  private readonly sessionsApi = inject(PosSessionsApi);
  private readonly ordersApi = inject(PosOrdersApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly taxCodesApi = inject(TaxCodesApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');
  private readonly tender = viewChild('tender', { read: ElementRef<HTMLElement> });
  private searchTimer?: ReturnType<typeof setTimeout>;
  private readonly taxRates = new Map<string, number>();

  readonly registers = signal<PosRegisterDto[]>([]);
  readonly selectedRegisterId = signal<string>('');
  readonly activeSession = signal<PosSessionDto | null>(null);
  readonly shiftLoading = signal(false);
  readonly shiftFailed = signal(false);
  readonly currency = this.currencyService.currency;

  readonly catalog = signal<CatalogItem[]>([]);
  readonly catalogTotal = signal(0);
  readonly catalogLoading = signal(false);
  readonly catalogFailed = signal(false);

  readonly searchQuery = signal<string>('');
  readonly cart = signal<CartItem[]>([]);
  readonly customerName = signal<string>('Walk-in Customer');

  readonly paymentModalVisible = signal(false);
  readonly receiptModalVisible = signal(false);
  readonly openShiftModalVisible = signal(false);
  readonly closeShiftModalVisible = signal(false);

  readonly paymentMethod = signal<PosPaymentMethod>('cash');
  readonly tenderedCash = signal(0);
  readonly cardReference = signal('');
  readonly isProcessingPayment = signal(false);

  readonly openFloatCash = signal(0);
  readonly shiftNotes = signal('');
  readonly actualCloseCash = signal(0);
  readonly closeNotes = signal('');
  readonly shiftBusy = signal(false);

  readonly completedOrder = signal<PosOrderDto | null>(null);

  readonly thermalReceiptData = computed<ThermalReceiptData | null>(() => {
    const ord = this.completedOrder();
    if (!ord) return null;
    const business = this.sessionStore.me()?.workspace?.name;
    const register = this.registers().find((r) => r.id === ord.registerId);
    return {
      organizationName: business,
      organizationNameAr: business,
      branchName: register?.name,
      headerNote: register?.receiptHeader,
      footerNote: register?.receiptFooter,
      title: 'Tax Invoice / Cash Receipt',
      titleAr: 'فاتورة ضريبية / إيصال مبيعات',
      receiptNumber: ord.number,
      date: ord.orderDate,
      status: ord.status,
      cashierName: ord.cashierName,
      customerName: ord.customerName,
      currency: this.currency(),
      lines: (ord.lines || []).map((l) => ({
        code: l.itemCode,
        description: l.description,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        lineTotal: l.lineTotal,
      })),
      subTotal: ord.subTotal,
      taxTotal: ord.taxTotal,
      totalAmount: ord.totalAmount,
      payments: (ord.payments || []).map((p) => ({
        method: p.method,
        amount: p.amount,
        changeAmount: p.changeAmount,
      })),
    };
  });

  readonly filteredCatalog = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.catalog();
    return this.catalog().filter((i) => i.name.toLowerCase().includes(q) || i.code.toLowerCase().includes(q));
  });

  readonly itemCount = computed(() => this.cart().reduce((sum, l) => sum + l.quantity, 0));
  // Worked out line by line and rounded the way the server does, so the amount sent always equals the order total.
  readonly cartSubTotal = computed(() => round2(this.cart().reduce((sum, l) => sum + l.lineNet, 0)));
  readonly cartTaxTotal = computed(() => round2(this.cart().reduce((sum, l) => sum + l.lineTax, 0)));
  readonly cartTotal = computed(() => round2(this.cartSubTotal() + this.cartTaxTotal()));
  readonly cashChange = computed(() => Math.max(0, round2(this.tenderedCash() - this.cartTotal())));

  /** The next round amounts a customer is likely to hand over. */
  readonly quickCashOptions = computed(() => {
    const total = this.cartTotal();
    const rounded = [10, 50, 100, 500, 1000].map((step) => Math.ceil(total / step) * step).filter((n) => n > total);
    return [...new Set(rounded)].sort((a, b) => a - b).slice(0, 4);
  });

  readonly canConfirmPayment = computed(() =>
    !this.isProcessingPayment() && (this.paymentMethod() !== 'cash' || this.tenderedCash() >= this.cartTotal()));

  constructor() {
    this.loadRegisters();
    this.loadTaxCodes();
    this.loadItemsCatalog();
    afterNextRender(() => this.focusSearch());
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
  }

  private focusSearch(): void {
    this.searchInput()?.nativeElement.focus();
  }

  loadRegisters(): void {
    this.registersApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => {
        const active = res.items.filter((r) => r.isActive);
        this.registers.set(active);
        if (!this.selectedRegisterId() && active.length > 0) {
          const remembered = localStorage.getItem(REGISTER_KEY);
          this.onRegisterChange(active.find((r) => r.id === remembered)?.id ?? active[0].id);
        }
      },
      error: () => this.message.error('The registers could not be loaded.'),
    });
  }

  private loadTaxCodes(): void {
    this.taxCodesApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => res.items.forEach((t) => this.taxRates.set(t.id, t.rate)),
      error: () => this.message.error('The tax rates could not be loaded. Totals may change at payment.'),
    });
  }

  loadItemsCatalog(): void {
    this.catalogLoading.set(true);
    this.catalogFailed.set(false);
    this.itemsApi.list({ page: 1, pageSize: CATALOG_PAGE, filters: ACTIVE_ONLY }).subscribe({
      next: (res) => {
        this.catalog.set(res.items.map(toCatalogItem));
        this.catalogTotal.set(res.total);
        this.catalogLoading.set(false);
      },
      error: () => {
        this.catalogLoading.set(false);
        this.catalogFailed.set(true);
      },
    });
  }

  onSearch(value: string): void {
    this.searchQuery.set(value);
    clearTimeout(this.searchTimer);
    if (value.trim() && this.catalogTotal() > this.catalog().length) {
      this.searchTimer = setTimeout(() => this.searchServer(value.trim()), 250);
    }
  }

  /** The till holds the first page of products, so a big catalogue is also searched on the server. */
  private searchServer(term: string, then?: () => void): void {
    this.itemsApi.list({ page: 1, pageSize: 50, search: term, filters: ACTIVE_ONLY }).subscribe({
      next: (res) => {
        const known = new Set(this.catalog().map((i) => i.id));
        const found = res.items.filter((i) => !known.has(i.id)).map(toCatalogItem);
        if (found.length) this.catalog.update((all) => [...all, ...found]);
        then?.();
      },
      error: () => then?.(),
    });
  }

  /** Enter from the keyboard or a scanner: an exact code, or the only match, goes straight onto the ticket. */
  onSearchEnter(event: Event): void {
    event.preventDefault();
    const term = this.searchQuery().trim();
    if (!term) return;
    if (this.addExactMatch(term)) return;
    if (this.catalogTotal() > this.catalog().length) {
      clearTimeout(this.searchTimer);
      this.searchServer(term, () => {
        if (!this.addExactMatch(term)) this.warnIfNoMatch(term);
      });
      return;
    }
    this.warnIfNoMatch(term);
  }

  private addExactMatch(term: string): boolean {
    const code = term.toLowerCase();
    const matches = this.filteredCatalog();
    const hit = matches.find((i) => i.code.toLowerCase() === code) ?? (matches.length === 1 ? matches[0] : null);
    if (!hit) return false;
    if (hit.price === null) {
      this.message.warning(`${hit.name} has no sales price.`);
      return true;
    }
    this.addToCart(hit);
    return true;
  }

  private warnIfNoMatch(term: string): void {
    if (!this.filteredCatalog().length) this.message.warning(`No product matches "${term}".`);
    this.searchInput()?.nativeElement.select();
  }

  clearSearch(): void {
    this.searchQuery.set('');
    this.focusSearch();
  }

  onRegisterChange(regId: string): void {
    this.selectedRegisterId.set(regId);
    try { localStorage.setItem(REGISTER_KEY, regId); } catch { /* storage unavailable */ }
    this.shiftLoading.set(true);
    this.shiftFailed.set(false);
    this.sessionsApi.getCurrent(regId).subscribe({
      next: (sess) => {
        this.activeSession.set(sess);
        this.shiftLoading.set(false);
      },
      error: () => {
        this.activeSession.set(null);
        this.shiftLoading.set(false);
        this.shiftFailed.set(true);
      },
    });
  }

  addToCart(item: CatalogItem): void {
    if (item.price === null) return;
    const current = [...this.cart()];
    const existingIndex = current.findIndex((c) => c.itemId === item.id);
    if (existingIndex >= 0) {
      current[existingIndex] = withQuantity(current[existingIndex], current[existingIndex].quantity + 1);
    } else {
      const line: CartItem = {
        itemId: item.id,
        itemCode: item.code,
        description: item.name,
        quantity: 1,
        unitPrice: item.price,
        discountPercent: 0,
        // The server charges the rate of the item's own tax code, and nothing when it has none.
        taxRate: (item.taxCodeId && this.taxRates.get(item.taxCodeId)) || 0,
        lineNet: 0,
        lineTax: 0,
        lineTotal: 0,
      };
      current.push(withQuantity(line, 1));
    }
    this.cart.set(current);
    // Ready for the next scan.
    this.clearSearch();
  }

  updateQuantity(index: number, newQty: number): void {
    const current = [...this.cart()];
    if (newQty <= 0) current.splice(index, 1);
    else current[index] = withQuantity(current[index], newQty);
    this.cart.set(current);
  }

  removeLine(index: number): void {
    const current = [...this.cart()];
    current.splice(index, 1);
    this.cart.set(current);
    this.focusSearch();
  }

  clearCart(): void {
    this.cart.set([]);
    this.focusSearch();
  }

  openPaymentModal(): void {
    if (!this.activeSession()) {
      this.message.warning('Open a shift before taking payment.');
      return;
    }
    this.tenderedCash.set(this.cartTotal());
    this.paymentMethod.set('cash');
    this.cardReference.set('');
    this.paymentModalVisible.set(true);
  }

  processPayment(): void {
    const session = this.activeSession();
    if (!session || !this.canConfirmPayment()) return;

    this.isProcessingPayment.set(true);
    const total = this.cartTotal();
    const cash = this.paymentMethod() === 'cash';

    const cmd: CreatePosOrderCommand = {
      legalEntityId: session.legalEntityId,
      registerId: session.registerId,
      sessionId: session.id,
      customerName: this.customerName(),
      lines: this.cart().map((l) => ({
        itemId: l.itemId,
        quantity: l.quantity,
        discountPercent: l.discountPercent,
      })),
      payments: [
        {
          method: this.paymentMethod(),
          amount: total,
          tenderedAmount: cash ? this.tenderedCash() : total,
          changeAmount: cash ? this.cashChange() : 0,
          reference: cash ? null : this.cardReference() || null,
        },
      ],
    };

    this.ordersApi.create(cmd).subscribe({
      next: (order) => {
        this.isProcessingPayment.set(false);
        this.paymentModalVisible.set(false);
        this.completedOrder.set(order as unknown as PosOrderDto);
        this.receiptModalVisible.set(true);
        this.cart.set([]);
        this.customerName.set('Walk-in Customer');
        // The drawer now holds more cash.
        this.onRegisterChange(session.registerId);
      },
      error: (err) => {
        this.isProcessingPayment.set(false);
        this.message.error(problemMessage(err, 'The payment could not be saved.'));
      },
    });
  }

  /** The dialog otherwise focuses its close button, and Enter would close it instead of taking the payment. */
  focusTender(): void {
    const field = this.tender()?.nativeElement.querySelector('input');
    field?.focus();
    field?.select();
  }

  onTenderEnter(event: Event): void {
    event.preventDefault();
    if (!(event as KeyboardEvent).repeat) this.processPayment();
  }

  finishSale(): void {
    this.receiptModalVisible.set(false);
    this.focusSearch();
  }

  submitOpenShift(): void {
    const reg = this.registers().find((r) => r.id === this.selectedRegisterId());
    if (!reg) return;
    this.shiftBusy.set(true);
    this.sessionsApi
      .openSession({
        legalEntityId: reg.legalEntityId,
        registerId: reg.id,
        openingCash: this.openFloatCash(),
        notes: this.shiftNotes(),
      })
      .subscribe({
        next: (s) => {
          this.shiftBusy.set(false);
          this.activeSession.set(s);
          this.openShiftModalVisible.set(false);
          this.shiftNotes.set('');
          this.message.success('Shift opened.');
          this.focusSearch();
        },
        error: (err) => {
          this.shiftBusy.set(false);
          this.message.error(problemMessage(err, 'The shift could not be opened.'));
        },
      });
  }

  openCloseShiftModal(): void {
    const s = this.activeSession();
    if (s) this.actualCloseCash.set(s.expectedCash);
    this.closeNotes.set('');
    this.closeShiftModalVisible.set(true);
  }

  submitCloseShift(): void {
    const s = this.activeSession();
    if (!s) return;
    this.shiftBusy.set(true);
    this.sessionsApi
      .closeSession(s.id, {
        actualCash: this.actualCloseCash(),
        notes: this.closeNotes(),
      })
      .subscribe({
        next: () => {
          this.shiftBusy.set(false);
          this.activeSession.set(null);
          this.closeShiftModalVisible.set(false);
          this.message.success('Shift closed.');
        },
        error: (err) => {
          this.shiftBusy.set(false);
          this.message.error(problemMessage(err, 'The shift could not be closed.'));
        },
      });
  }
}

function toCatalogItem(i: ItemDto): CatalogItem {
  return { id: i.id, code: i.sku, name: i.name, price: i.salesPrice ?? null, taxCodeId: i.taxCodeId };
}

function withQuantity(line: CartItem, quantity: number): CartItem {
  const lineNet = round2(quantity * line.unitPrice);
  const lineTax = round2(lineNet * (line.taxRate / 100));
  return { ...line, quantity, lineNet, lineTax, lineTotal: round2(lineNet + lineTax) };
}
