import { CommonModule, CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
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

interface CartItem extends PosOrderLineInput {
  lineTotal: number;
}

@Component({
  selector: 'erp-pos-terminal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    CurrencyPipe,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzTagModule,
    NzModalModule,
    NzRadioModule,
    NzDividerModule,
    NzSpaceModule,
    NzAlertModule,
    NzEmptyModule,
    ThermalReceiptComponent,
  ],
  template: `
    <div class="pos-terminal-layout">
      <!-- Top Status Bar -->
      <header class="terminal-header">
        <div class="terminal-brand">
          <span nz-icon nzType="shop" class="terminal-logo"></span>
          <div>
            <h2 class="terminal-title">POS Counter Terminal</h2>
            <span class="terminal-sub">Express Retail & Counter Sales</span>
          </div>
        </div>

        <div class="terminal-status-controls">
          <div class="register-select-box">
            <span class="label">Register:</span>
            <nz-select [ngModel]="selectedRegisterId()" (ngModelChange)="onRegisterChange($event)" nzSize="small" style="width: 180px;">
              @for (r of registers(); track r.id) {
                <nz-option [nzValue]="r.id" [nzLabel]="r.name"></nz-option>
              }
            </nz-select>
          </div>

          @if (activeSession(); as session) {
            <nz-tag nzColor="success">
              <span nz-icon nzType="check-circle"></span> Shift Open · {{ session.cashierName }}
            </nz-tag>
            <div class="drawer-cash-badge">
              <span class="label">Expected Cash:</span>
              <strong>AED {{ session.expectedCash | number:'1.2-2' }}</strong>
            </div>
            <button nz-button nzDanger nzSize="small" (click)="openCloseShiftModal()">
              <span nz-icon nzType="poweroff"></span>Close Shift
            </button>
          } @else {
            <nz-tag nzColor="error">
              <span nz-icon nzType="close-circle"></span> No Open Shift
            </nz-tag>
            <button nz-button nzType="primary" nzSize="small" (click)="openOpenShiftModal()">
              <span nz-icon nzType="play-circle"></span>Open Shift
            </button>
          }
        </div>
      </header>

      <!-- Terminal Body: Catalog (Left) + Cart & Payment (Right) -->
      <div class="terminal-main">
        <!-- Item Catalog Panel -->
        <section class="catalog-section">
          <div class="catalog-search-bar">
            <nz-input-group [nzPrefix]="searchPrefix">
              <input
                nz-input
                placeholder="Scan barcode, SKU or search item title..."
                [ngModel]="searchQuery()"
                (ngModelChange)="searchQuery.set($event)"
              />
            </nz-input-group>
            <ng-template #searchPrefix><span nz-icon nzType="search"></span></ng-template>
          </div>

          <div class="catalog-grid">
            @for (item of filteredCatalog(); track item.id) {
              <div class="item-tile" (click)="addToCart(item)">
                <div class="item-tile-code">{{ item.code }}</div>
                <div class="item-tile-title">{{ item.name }}</div>
                <div class="item-tile-price">AED {{ item.price | number:'1.2-2' }}</div>
              </div>
            } @empty {
              <div class="empty-catalog">
                <nz-empty nzNotFoundImage="simple" nzNotFoundContent="No products match search criteria."></nz-empty>
              </div>
            }
          </div>
        </section>

        <!-- Current Ticket / Cart Panel -->
        <section class="ticket-section">
          <div class="ticket-header">
            <div class="ticket-title">
              <span nz-icon nzType="shopping-cart"></span> Current Ticket
              <nz-tag nzColor="blue">{{ cart().length }} items</nz-tag>
            </div>
            <button nz-button nzType="link" nzDanger (click)="clearCart()" [disabled]="cart().length === 0">
              Clear
            </button>
          </div>

          <div class="customer-bar">
            <span nz-icon nzType="user" style="color: #64748b;"></span>
            <input nz-input nzSize="small" placeholder="Walk-in Customer" [ngModel]="customerName()" (ngModelChange)="customerName.set($event)" />
          </div>

          <!-- Cart Line Items -->
          <div class="cart-lines-container">
            @for (line of cart(); track line.itemCode; let i = $index) {
              <div class="cart-line-card">
                <div class="line-info">
                  <span class="line-name">{{ line.description }}</span>
                  <span class="line-meta">AED {{ line.unitPrice | number:'1.2-2' }} · Tax {{ line.taxRate }}%</span>
                </div>

                <div class="line-qty-controls">
                  <button class="qty-btn" (click)="updateQuantity(i, line.quantity - 1)">-</button>
                  <span class="qty-val">{{ line.quantity }}</span>
                  <button class="qty-btn" (click)="updateQuantity(i, line.quantity + 1)">+</button>
                </div>

                <div class="line-total-col">
                  <strong>AED {{ line.lineTotal | number:'1.2-2' }}</strong>
                  <span nz-icon nzType="delete" class="line-delete-btn" (click)="removeLine(i)"></span>
                </div>
              </div>
            } @empty {
              <div class="empty-cart-state">
                <span nz-icon nzType="barcode" class="empty-icon"></span>
                <p>Ticket is empty. Scan an item or tap a product on the left to start sale.</p>
              </div>
            }
          </div>

          <!-- Cart Totals & Checkout Actions -->
          <div class="ticket-footer">
            <div class="totals-breakdown">
              <div class="summary-row">
                <span>Subtotal:</span>
                <span>AED {{ cartSubTotal() | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span>VAT / Sales Tax (5%):</span>
                <span>AED {{ cartTaxTotal() | number:'1.2-2' }}</span>
              </div>
              <nz-divider style="margin: 8px 0;"></nz-divider>
              <div class="summary-row grand-total">
                <span>Grand Total:</span>
                <span>AED {{ cartTotal() | number:'1.2-2' }}</span>
              </div>
            </div>

            <button
              nz-button
              nzType="primary"
              nzBlock
              nzSize="large"
              class="checkout-btn"
              [disabled]="cart().length === 0 || !activeSession()"
              (click)="openPaymentModal()"
            >
              <span nz-icon nzType="credit-card"></span> Pay AED {{ cartTotal() | number:'1.2-2' }}
            </button>
          </div>
        </section>
      </div>

      <!-- Payment / Tender Modal -->
      <nz-modal
        [(nzVisible)]="paymentModalVisible"
        nzTitle="Complete POS Payment Tender"
        [nzFooter]="paymentModalFooter"
        (nzOnCancel)="paymentModalVisible = false"
      >
        <ng-container *nzModalContent>
          <div class="payment-modal-body">
            <div class="payment-due-banner">
              <span class="due-label">Total Amount Due</span>
              <span class="due-amount">AED {{ cartTotal() | number:'1.2-2' }}</span>
            </div>

            <div class="tender-options" style="margin-bottom: 16px;">
              <nz-radio-group [(ngModel)]="paymentMethod" nzButtonStyle="solid">
                <label nz-radio-button nzValue="Cash"><span nz-icon nzType="dollar"></span> Cash</label>
                <label nz-radio-button nzValue="Card"><span nz-icon nzType="credit-card"></span> Credit / Debit Card</label>
              </nz-radio-group>
            </div>

            @if (paymentMethod === 'Cash') {
              <div class="cash-tender-section">
                <label style="font-weight: 600; display: block; margin-bottom: 6px;">Cash Tendered by Customer</label>
                <nz-input-number
                  [(ngModel)]="tenderedCash"
                  [nzMin]="cartTotal()"
                  [nzStep]="10"
                  style="width: 100%; font-size: 18px;"
                ></nz-input-number>

                <div class="quick-cash-row">
                  <button nz-button nzSize="small" (click)="tenderedCash = cartTotal()">Exact (AED {{ cartTotal() | number:'1.0-0' }})</button>
                  <button nz-button nzSize="small" (click)="quickCash(50)">50</button>
                  <button nz-button nzSize="small" (click)="quickCash(100)">100</button>
                  <button nz-button nzSize="small" (click)="quickCash(200)">200</button>
                  <button nz-button nzSize="small" (click)="quickCash(500)">500</button>
                </div>

                <div class="change-display">
                  <span>Change Due to Customer:</span>
                  <strong class="change-amount">AED {{ cashChange() | number:'1.2-2' }}</strong>
                </div>
              </div>
            } @else {
              <div class="card-tender-section">
                <label style="font-weight: 600; display: block; margin-bottom: 6px;">Card Transaction Authorization / Ref</label>
                <input nz-input placeholder="e.g. AUTH-88492" [(ngModel)]="cardReference" />
                <p style="margin-top: 6px; font-size: 12px; color: #64748b;">
                  Swipe / tap on payment terminal, then confirm receipt approval.
                </p>
              </div>
            }
          </div>
        </ng-container>

        <ng-template #paymentModalFooter>
          <button nz-button (click)="paymentModalVisible = false">Cancel</button>
          <button nz-button nzType="primary" (click)="processPayment()" [nzLoading]="isProcessingPayment">
            <span nz-icon nzType="check"></span> Confirm & Print Receipt
          </button>
        </ng-template>
      </nz-modal>

      <!-- Completed Receipt Modal -->
      <nz-modal
        [(nzVisible)]="receiptModalVisible"
        nzTitle="Sale Completed · Official Thermal Receipt"
        [nzFooter]="receiptFooterTpl"
        [nzWidth]="420"
        (nzOnCancel)="receiptModalVisible = false"
      >
        <ng-container *nzModalContent>
          @if (thermalReceiptData(); as rc) {
            <erp-thermal-receipt [receipt]="rc" />
          }
        </ng-container>
        <ng-template #receiptFooterTpl>
          @if (completedOrder(); as ord) {
            <a [href]="'/print/pos-receipt/' + ord.id" target="_blank" nz-button nzType="primary">
              <span nz-icon nzType="printer"></span> Print Official Receipt
            </a>
          }
          <button nz-button (click)="receiptModalVisible = false">
            Done
          </button>
        </ng-template>
      </nz-modal>

      <!-- Open Shift Modal -->
      <nz-modal
        [(nzVisible)]="openShiftModalVisible"
        nzTitle="Open POS Register Shift"
        (nzOnCancel)="openShiftModalVisible = false"
        (nzOnOk)="submitOpenShift()"
      >
        <ng-container *nzModalContent>
          <div style="display: flex; flex-direction: column; gap: 14px;">
            <div>
              <label style="font-weight: 600;">Opening Float Cash (AED)</label>
              <nz-input-number [(ngModel)]="openFloatCash" [nzMin]="0" style="width: 100%;"></nz-input-number>
            </div>
            <div>
              <label style="font-weight: 600;">Shift Opening Notes</label>
              <input nz-input [(ngModel)]="shiftNotes" placeholder="Morning / afternoon shift notes..." />
            </div>
          </div>
        </ng-container>
      </nz-modal>

      <!-- Close Shift Modal -->
      <nz-modal
        [(nzVisible)]="closeShiftModalVisible"
        nzTitle="Close & Reconcile POS Shift"
        (nzOnCancel)="closeShiftModalVisible = false"
        (nzOnOk)="submitCloseShift()"
      >
        <ng-container *nzModalContent>
          @if (activeSession(); as s) {
            <div style="display: flex; flex-direction: column; gap: 14px;">
              <nz-alert
                nzType="info"
                [nzMessage]="'Expected Drawer Cash: AED ' + (s.expectedCash | number:'1.2-2')"
                nzShowIcon
              ></nz-alert>
              <div>
                <label style="font-weight: 600;">Actual Physical Cash Counted (AED)</label>
                <nz-input-number [(ngModel)]="actualCloseCash" [nzMin]="0" style="width: 100%; font-size: 16px;"></nz-input-number>
              </div>
              <div class="variance-box">
                <span>Variance (Difference):</span>
                <strong [style.color]="(actualCloseCash - s.expectedCash) < 0 ? '#dc2626' : '#16a34a'">
                  AED {{ (actualCloseCash - s.expectedCash) | number:'1.2-2' }}
                </strong>
              </div>
              <div>
                <label style="font-weight: 600;">Closing Notes</label>
                <input nz-input [(ngModel)]="closeNotes" placeholder="Shift handover / cash drop comments..." />
              </div>
            </div>
          }
        </ng-container>
      </nz-modal>
    </div>
  `,
  styles: [`
    .pos-terminal-layout {
      display: flex;
      flex-direction: column;
      height: calc(100vh - 64px);
      background: #f8fafc;
      overflow: hidden;
    }
    .terminal-header {
      background: #ffffff;
      border-bottom: 1px solid #e2e8f0;
      padding: 12px 20px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-shrink: 0;
    }
    .terminal-brand { display: flex; align-items: center; gap: 12px; }
    .terminal-logo { font-size: 26px; color: #2563eb; background: #eff6ff; padding: 8px; border-radius: 8px; }
    .terminal-title { margin: 0; font-size: 18px; font-weight: 700; color: #0f172a; }
    .terminal-sub { font-size: 12px; color: #64748b; }
    .terminal-status-controls { display: flex; align-items: center; gap: 14px; }
    .register-select-box { display: flex; align-items: center; gap: 8px; font-size: 13px; font-weight: 600; }
    .drawer-cash-badge { background: #f1f5f9; padding: 4px 10px; border-radius: 6px; font-size: 13px; }

    .terminal-main {
      display: flex;
      flex: 1;
      overflow: hidden;
    }
    .catalog-section {
      flex: 3;
      display: flex;
      flex-direction: column;
      border-right: 1px solid #e2e8f0;
      background: #f8fafc;
      overflow: hidden;
    }
    .catalog-search-bar { padding: 14px 18px; background: white; border-bottom: 1px solid #e2e8f0; }
    .catalog-grid {
      padding: 16px;
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      gap: 12px;
      overflow-y: auto;
      flex: 1;
    }
    .item-tile {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      padding: 12px;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      min-height: 105px;
      transition: all 0.15s ease-in-out;
      user-select: none;
    }
    .item-tile:hover {
      border-color: #3b82f6;
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(59,130,246,0.1);
    }
    .item-tile-code { font-size: 11px; font-weight: 700; color: #64748b; text-transform: uppercase; }
    .item-tile-title { font-size: 13px; font-weight: 600; color: #1e293b; margin: 4px 0; line-height: 1.3; }
    .item-tile-price { font-size: 14px; font-weight: 700; color: #2563eb; }

    .ticket-section {
      flex: 2;
      min-width: 380px;
      max-width: 460px;
      background: white;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .ticket-header {
      padding: 14px 18px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid #e2e8f0;
    }
    .ticket-title { font-size: 15px; font-weight: 700; color: #0f172a; display: flex; align-items: center; gap: 8px; }
    .customer-bar { padding: 8px 18px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; align-items: center; gap: 8px; }

    .cart-lines-container {
      flex: 1;
      overflow-y: auto;
      padding: 12px 18px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .cart-line-card {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 10px 12px;
      background: #ffffff;
      border: 1px solid #f1f5f9;
      border-radius: 8px;
    }
    .line-info { flex: 1; min-width: 0; padding-right: 8px; }
    .line-name { font-size: 13px; font-weight: 600; color: #0f172a; display: block; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .line-meta { font-size: 11px; color: #64748b; }
    .line-qty-controls { display: flex; align-items: center; gap: 6px; margin: 0 10px; }
    .qty-btn {
      width: 24px;
      height: 24px;
      border-radius: 4px;
      border: 1px solid #cbd5e1;
      background: #f8fafc;
      font-weight: 700;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .qty-val { font-size: 13px; font-weight: 700; min-width: 20px; text-align: center; }
    .line-total-col { text-align: right; display: flex; align-items: center; gap: 8px; font-size: 13px; }
    .line-delete-btn { color: #ef4444; cursor: pointer; font-size: 14px; }

    .empty-cart-state { text-align: center; color: #94a3b8; padding: 48px 16px; }
    .empty-icon { font-size: 36px; margin-bottom: 12px; opacity: 0.6; }

    .ticket-footer {
      padding: 16px 18px;
      background: #f8fafc;
      border-top: 1px solid #e2e8f0;
    }
    .totals-breakdown { margin-bottom: 14px; }
    .summary-row { display: flex; justify-content: space-between; font-size: 13px; color: #475569; margin-bottom: 4px; }
    .grand-total { font-size: 18px; font-weight: 800; color: #0f172a; }
    .checkout-btn { font-size: 16px; font-weight: 700; height: 48px; border-radius: 8px; }

    .payment-due-banner { text-align: center; background: #eff6ff; padding: 14px; border-radius: 8px; margin-bottom: 16px; }
    .due-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #3b82f6; display: block; }
    .due-amount { font-size: 28px; font-weight: 800; color: #1e3a8a; }
    .quick-cash-row { display: flex; gap: 6px; margin-top: 8px; flex-wrap: wrap; }
    .change-display { margin-top: 14px; padding: 12px; background: #ecfdf5; border-radius: 6px; display: flex; justify-content: space-between; align-items: center; }
    .change-amount { font-size: 18px; color: #065f46; }

    .thermal-receipt {
      font-family: 'Courier New', Courier, monospace;
      padding: 12px;
      background: #fafafa;
      border: 1px dashed #ccc;
      color: #111;
    }
    .receipt-header-center { text-align: center; }
    .receipt-header-center h3 { margin: 0; font-size: 16px; font-weight: bold; }
    .receipt-header-center p { margin: 2px 0; font-size: 12px; }
    .receipt-line { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 4px; }
    .receipt-total-row { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 2px; }
    .receipt-grand { font-size: 14px; font-weight: bold; }
    .receipt-tender-row { display: flex; justify-content: space-between; font-size: 11px; color: #555; }
    .receipt-footer-center { text-align: center; margin-top: 14px; font-size: 11px; }
    .variance-box { display: flex; justify-content: space-between; padding: 8px 12px; background: #f8fafc; border-radius: 6px; font-size: 14px; }
  `],
})
export class PosTerminalPage {
  private readonly registersApi = inject(PosRegistersApi);
  private readonly sessionsApi = inject(PosSessionsApi);
  private readonly ordersApi = inject(PosOrdersApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);

  readonly registers = signal<PosRegisterDto[]>([]);
  readonly selectedRegisterId = signal<string>('');
  readonly activeSession = signal<PosSessionDto | null>(null);

  readonly catalog = signal<{ id: string; code: string; name: string; price: number }[]>([
    { id: '1', code: 'BEV-01', name: 'Espresso Roast Coffee', price: 18.00 },
    { id: '2', code: 'BEV-02', name: 'Matcha Green Latte', price: 24.00 },
    { id: '3', code: 'BAK-01', name: 'Fresh Butter Croissant', price: 14.50 },
    { id: '4', code: 'BAK-02', name: 'Artisan Sourdough Loaf', price: 22.00 },
    { id: '5', code: 'SNK-01', name: 'Roasted Almond Bar', price: 9.50 },
    { id: '6', code: 'SNK-02', name: 'Organic Mineral Water 500ml', price: 6.00 },
    { id: '7', code: 'MER-01', name: 'Ceramic Reusable Tumbler', price: 65.00 },
    { id: '8', code: 'MER-02', name: 'Specialty Whole Beans 250g', price: 55.00 },
  ]);

  readonly searchQuery = signal<string>('');
  readonly cart = signal<CartItem[]>([]);
  readonly customerName = signal<string>('Walk-in Customer');

  // Modals & Payment State
  paymentModalVisible = false;
  receiptModalVisible = false;
  openShiftModalVisible = false;
  closeShiftModalVisible = false;

  paymentMethod: PosPaymentMethod = 'Cash';
  tenderedCash = 0;
  cardReference = '';
  isProcessingPayment = false;

  openFloatCash = 200;
  shiftNotes = '';
  actualCloseCash = 0;
  closeNotes = '';

  readonly completedOrder = signal<PosOrderDto | null>(null);

  readonly thermalReceiptData = computed<ThermalReceiptData | null>(() => {
    const ord = this.completedOrder();
    if (!ord) return null;
    return {
      organizationName: 'HigherSquare 360 ERP',
      organizationNameAr: 'هاير سكوير 360',
      title: 'Tax Invoice / Cash Receipt',
      titleAr: 'فاتورة ضريبية / إيصال مبيعات',
      receiptNumber: ord.number,
      date: ord.orderDate,
      status: ord.status,
      cashierName: ord.cashierName,
      customerName: ord.customerName,
      currency: 'AED',
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

  readonly cartSubTotal = computed(() => {
    return this.cart().reduce((sum, item) => sum + (item.quantity * item.unitPrice), 0);
  });

  readonly cartTaxTotal = computed(() => {
    return Math.round(this.cartSubTotal() * 0.05 * 100) / 100;
  });

  readonly cartTotal = computed(() => {
    return this.cartSubTotal() + this.cartTaxTotal();
  });

  readonly cashChange = computed(() => {
    const diff = this.tenderedCash - this.cartTotal();
    return diff > 0 ? diff : 0;
  });

  constructor() {
    this.loadRegisters();
    this.loadItemsCatalog();
  }

  loadRegisters(): void {
    this.registersApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res: any) => {
        const items = res.items || [];
        this.registers.set(items);
        if (items.length > 0 && !this.selectedRegisterId()) {
          this.onRegisterChange(items[0].id);
        }
      },
    });
  }

  loadItemsCatalog(): void {
    this.itemsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res: any) => {
        if (res.items && res.items.length > 0) {
          const mapped = res.items.map((i: any) => ({
            id: i.id,
            code: i.sku || i.code || i.name.substring(0, 4).toUpperCase(),
            name: i.name,
            price: i.salesPrice || 25.0,
          }));
          this.catalog.set(mapped);
        }
      },
    });
  }

  onRegisterChange(regId: string): void {
    this.selectedRegisterId.set(regId);
    this.sessionsApi.getCurrent(regId).subscribe({
      next: (sess) => this.activeSession.set(sess),
      error: () => this.activeSession.set(null),
    });
  }

  addToCart(item: { id: string; code: string; name: string; price: number }): void {
    const current = [...this.cart()];
    const existingIndex = current.findIndex((c) => c.itemCode === item.code);

    if (existingIndex >= 0) {
      const existing = current[existingIndex];
      const newQty = existing.quantity + 1;
      current[existingIndex] = {
        ...existing,
        quantity: newQty,
        lineTotal: newQty * existing.unitPrice * 1.05,
      };
    } else {
      current.push({
        itemId: item.id,
        itemCode: item.code,
        description: item.name,
        quantity: 1,
        unitPrice: item.price,
        discountPercent: 0,
        taxRate: 5,
        lineTotal: item.price * 1.05,
      });
    }

    this.cart.set(current);
  }

  updateQuantity(index: number, newQty: number): void {
    const current = [...this.cart()];
    if (newQty <= 0) {
      current.splice(index, 1);
    } else {
      const line = current[index];
      current[index] = {
        ...line,
        quantity: newQty,
        lineTotal: newQty * line.unitPrice * 1.05,
      };
    }
    this.cart.set(current);
  }

  removeLine(index: number): void {
    const current = [...this.cart()];
    current.splice(index, 1);
    this.cart.set(current);
  }

  clearCart(): void {
    this.cart.set([]);
  }

  openPaymentModal(): void {
    if (!this.activeSession()) {
      this.message.warning('Please open a shift before processing payments.');
      return;
    }
    this.tenderedCash = this.cartTotal();
    this.paymentMethod = 'Cash';
    this.paymentModalVisible = true;
  }

  quickCash(amount: number): void {
    this.tenderedCash = amount;
  }

  processPayment(): void {
    const session = this.activeSession();
    if (!session) return;

    this.isProcessingPayment = true;
    const total = this.cartTotal();

    const cmd: CreatePosOrderCommand = {
      legalEntityId: session.legalEntityId,
      registerId: session.registerId,
      sessionId: session.id,
      customerName: this.customerName(),
      cashierUserId: session.openedByUserId,
      cashierName: session.cashierName,
      lines: this.cart().map((l) => ({
        itemId: l.itemId,
        itemCode: l.itemCode,
        description: l.description,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent,
        taxRate: l.taxRate,
      })),
      payments: [
        {
          method: this.paymentMethod,
          amount: total,
          tenderedAmount: this.paymentMethod === 'Cash' ? this.tenderedCash : total,
          changeAmount: this.paymentMethod === 'Cash' ? this.cashChange() : 0,
          reference: this.cardReference || null,
        },
      ],
    };

    this.ordersApi.create(cmd).subscribe({
      next: (order) => {
        this.isProcessingPayment = false;
        this.paymentModalVisible = false;
        this.completedOrder.set(order as unknown as PosOrderDto);
        this.receiptModalVisible = true;
        this.clearCart();
        this.message.success(`Order ${order.number} placed successfully!`);
        // Refresh active session cash
        this.onRegisterChange(session.registerId);
      },
      error: (err) => {
        this.isProcessingPayment = false;
        this.message.error(err?.error?.message || 'Payment processing failed.');
      },
    });
  }

  openOpenShiftModal(): void {
    this.openShiftModalVisible = true;
  }

  submitOpenShift(): void {
    const regId = this.selectedRegisterId();
    const reg = this.registers().find((r) => r.id === regId);
    if (!reg) return;

    this.sessionsApi
      .openSession({
        legalEntityId: reg.legalEntityId,
        registerId: reg.id,
        openingCash: this.openFloatCash,
        cashierUserId: '00000000-0000-0000-0000-000000000001',
        cashierName: 'POS Cashier',
        notes: this.shiftNotes,
      })
      .subscribe({
        next: (s) => {
          this.activeSession.set(s);
          this.openShiftModalVisible = false;
          this.message.success('Shift opened successfully.');
        },
        error: (err) => this.message.error(err?.error?.message || 'Failed to open shift.'),
      });
  }

  openCloseShiftModal(): void {
    const s = this.activeSession();
    if (s) {
      this.actualCloseCash = s.expectedCash;
    }
    this.closeShiftModalVisible = true;
  }

  submitCloseShift(): void {
    const s = this.activeSession();
    if (!s) return;

    this.sessionsApi
      .closeSession(s.id, {
        actualCash: this.actualCloseCash,
        closedByUserId: '00000000-0000-0000-0000-000000000001',
        notes: this.closeNotes,
      })
      .subscribe({
        next: () => {
          this.activeSession.set(null);
          this.closeShiftModalVisible = false;
          this.message.success('Shift closed and cash reconciled.');
        },
        error: (err) => this.message.error(err?.error?.message || 'Failed to close shift.'),
      });
  }
}
