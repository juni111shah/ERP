import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nService } from '../core/i18n/i18n.service';

export interface ThermalReceiptLine {
  code?: string;
  description: string;
  quantity: number;
  unitPrice?: number;
  lineTotal: number;
}

export interface ThermalReceiptPayment {
  method: string;
  amount: number;
  changeAmount?: number;
}

export interface ThermalReceiptData {
  organizationName?: string;
  organizationNameAr?: string;
  branchName?: string;
  branchAddress?: string;
  title?: string;
  titleAr?: string;
  receiptNumber: string;
  date: string | Date;
  status?: string;
  cashierName?: string;
  customerName?: string;
  account?: string;
  referenceNo?: string;
  currency?: string;
  lines?: ThermalReceiptLine[];
  subTotal?: number;
  taxTotal?: number;
  totalAmount: number;
  payments?: ThermalReceiptPayment[];
  helplinePhone?: string;
  website?: string;
  notes?: string | null;
}

/**
 * Cut-style Thermal Paper Receipt with realistic serrated / scalloped blade cut edge.
 * Follows the user's active language:
 * - English when English is selected (no Arabic text shown)
 * - Arabic when Arabic is selected
 * Real ERP branding and order details without fake mockup data.
 */
@Component({
  selector: 'erp-thermal-receipt',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div class="thermal-receipt-wrapper">
      <div class="thermal-paper" [attr.dir]="isAr() ? 'rtl' : 'ltr'" [class.is-arabic]="isAr()">
        <!-- Top Receipt Header -->
        <div class="receipt-header">
          <div class="brand-emblem-wrap">
            <img src="highersquare-emblem.png" class="brand-emblem-img" alt="HigherSquare Logo" />
            <div class="brand-titles">
              <div class="brand-name">
                {{ isAr() ? (receipt().organizationNameAr || 'هاير سكوير 360') : (receipt().organizationName || 'HigherSquare 360 ERP') }}
              </div>
            </div>
          </div>

          <div class="receipt-doc-title">
            {{ isAr() ? (receipt().titleAr || 'فاتورة ضريبية / إيصال مبيعات') : (receipt().title || 'TAX INVOICE / CASH RECEIPT') }}
          </div>

          @if (receipt().branchName || receipt().branchAddress) {
            <div class="receipt-location">
              @if (receipt().branchName) { <div>{{ receipt().branchName }}</div> }
              @if (receipt().branchAddress) { <div>{{ receipt().branchAddress }}</div> }
            </div>
          }
        </div>

        <div class="dashed-divider"></div>

        <!-- Meta Information Rows -->
        <div class="receipt-kv-list">
          <div class="kv-row">
            <span class="kv-label">{{ isAr() ? 'الحالة:' : 'Status:' }}</span>
            <span class="kv-val status-badge">{{ statusLabel() }}</span>
          </div>
          <div class="kv-row">
            <span class="kv-label">{{ isAr() ? 'رقم الإيصال:' : 'Receipt #:' }}</span>
            <span class="kv-val font-mono num-val">{{ receipt().referenceNo || receipt().receiptNumber }}</span>
          </div>
          <div class="kv-row">
            <span class="kv-label">{{ isAr() ? 'التاريخ / الوقت:' : 'Date / Time:' }}</span>
            <span class="kv-val font-mono num-val">{{ formattedDate() }}</span>
          </div>
          @if (receipt().branchName) {
            <div class="kv-row">
              <span class="kv-label">{{ isAr() ? 'الفرع / الصندوق:' : 'Register / Branch:' }}</span>
              <span class="kv-val">{{ receipt().branchName }}</span>
            </div>
          }
          @if (receipt().customerName || receipt().account) {
            <div class="kv-row">
              <span class="kv-label">{{ isAr() ? 'العميل:' : 'Customer:' }}</span>
              <span class="kv-val">{{ customerLabel() }}</span>
            </div>
          }
          @if (receipt().cashierName) {
            <div class="kv-row">
              <span class="kv-label">{{ isAr() ? 'الكاشير:' : 'Cashier:' }}</span>
              <span class="kv-val">{{ receipt().cashierName }}</span>
            </div>
          }
        </div>

        <!-- Line Items (if present) -->
        @if (receipt().lines && receipt().lines!.length > 0) {
          <div class="dashed-divider"></div>
          <div class="items-table">
            <div class="items-head">
              <span>{{ isAr() ? 'الصنف / الوصف' : 'ITEM / DESCRIPTION' }}</span>
              <span class="val-align">{{ isAr() ? 'الإجمالي' : 'TOTAL' }}</span>
            </div>
            @for (l of receipt().lines; track $index) {
              <div class="item-row">
                <div class="item-info">
                  <div class="item-desc">{{ l.description }}</div>
                  @if (l.quantity > 1 || l.unitPrice) {
                    <div class="item-sub font-mono num-val">
                      {{ l.quantity }} &times; {{ curr() }} {{ (l.unitPrice ?? (l.lineTotal / l.quantity)) | number:'1.2-2' }}
                    </div>
                  }
                </div>
                <div class="item-amount font-mono num-val val-align">
                  {{ curr() }} {{ l.lineTotal | number:'1.2-2' }}
                </div>
              </div>
            }
          </div>
        }

        <div class="dashed-divider"></div>

        <!-- Financial Totals -->
        <div class="receipt-totals-list">
          @if (receipt().subTotal !== undefined) {
            <div class="kv-row">
              <span class="kv-label">{{ isAr() ? 'المبلغ الصافي:' : 'Subtotal:' }}</span>
              <span class="kv-val font-mono num-val">{{ curr() }} {{ receipt().subTotal | number:'1.2-2' }}</span>
            </div>
          }
          @if (receipt().taxTotal !== undefined && receipt().taxTotal! > 0) {
            <div class="kv-row">
              <span class="kv-label">{{ isAr() ? 'ضريبة القيمة المضافة:' : 'VAT / Tax:' }}</span>
              <span class="kv-val font-mono num-val">{{ curr() }} {{ receipt().taxTotal | number:'1.2-2' }}</span>
            </div>
          }

          <div class="dashed-divider subtle"></div>

          <div class="kv-row total-highlight">
            <span class="kv-label font-bold">{{ isAr() ? 'المجموع الإجمالي:' : 'Total:' }}</span>
            <span class="kv-val font-bold total-figure font-mono num-val">{{ curr() }} {{ receipt().totalAmount | number:'1.2-2' }}</span>
          </div>

          <div class="dashed-divider subtle"></div>

          @if (receipt().payments && receipt().payments!.length > 0) {
            @for (p of receipt().payments; track $index) {
              <div class="kv-row payment-row">
                <span class="kv-label">{{ paymentMethodLabel(p.method) }}:</span>
                <span class="kv-val font-mono num-val">{{ curr() }} {{ p.amount | number:'1.2-2' }}</span>
              </div>
              @if (p.changeAmount !== undefined && p.changeAmount > 0) {
                <div class="kv-row change-row">
                  <span class="kv-label">{{ isAr() ? 'المبلغ المتبقي:' : 'Change:' }}</span>
                  <span class="kv-val font-mono num-val">{{ curr() }} {{ p.changeAmount | number:'1.2-2' }}</span>
                </div>
              }
            }
          }
        </div>

        <div class="dashed-divider"></div>

        <!-- Footer Notice -->
        <div class="receipt-footer">
          @if (receipt().helplinePhone) {
            <div class="help-text">
              {{ isAr() ? ('للمساعدة: ' + receipt().helplinePhone) : ('Helpline: ' + receipt().helplinePhone) }}
            </div>
          }

          <div class="thankyou-text">
            {{ isAr() ? 'شكراً لتعاملكم معنا' : 'Thank you for your business' }}
          </div>

          @if (receipt().website) {
            <div class="website font-mono num-val">{{ receipt().website }}</div>
          }

          <!-- Thermal Barcode with Order Number -->
          <div class="thermal-barcode" aria-hidden="true">
            <div class="barcode-lines"></div>
            <div class="barcode-num font-mono">*{{ receipt().receiptNumber }}*</div>
          </div>
        </div>

        <!-- SERRATED / SCALLOP CUT EDGE (Bottom paper tear blade teeth) -->
        <div class="cut-edge-teeth" aria-hidden="true"></div>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }

    .thermal-receipt-wrapper {
      display: flex;
      justify-content: center;
      padding: 8px 8px 24px;
      background: transparent;
    }

    .thermal-paper {
      position: relative;
      width: 330px;
      max-width: 100%;
      background-color: #edf2f7;
      color: #1e293b;
      padding: 14px 16px 18px;
      border-radius: 4px 4px 0 0;
      filter: drop-shadow(0 10px 20px rgba(15, 23, 42, 0.16)) drop-shadow(0 2px 4px rgba(15, 23, 42, 0.08));
      font-family: 'SF Mono', 'Roboto Mono', 'Courier New', Courier, monospace;
      font-size: 11.5px;
      line-height: 1.25;
      box-sizing: border-box;
      text-align: left;
    }

    .thermal-paper.is-arabic {
      text-align: right;
      font-family: 'Cairo', 'Segoe UI', system-ui, 'SF Mono', 'Roboto Mono', monospace;
    }

    /* Serrated Scalloped Cut Blade Teeth at Bottom Edge */
    .cut-edge-teeth {
      position: absolute;
      left: 0;
      right: 0;
      bottom: -9px;
      height: 9px;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='9' viewBox='0 0 14 9'%3E%3Cpath d='M0 0 C 2.5 0, 3.5 7.5, 7 7.5 C 10.5 7.5, 11.5 0, 14 0 Z' fill='%23edf2f7'/%3E%3C/svg%3E");
      background-repeat: repeat-x;
      background-size: 14px 9px;
      background-position: left top;
    }

    .receipt-header {
      text-align: center;
    }

    .brand-emblem-wrap {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      margin-bottom: 4px;
    }

    .brand-emblem-img {
      width: 26px;
      height: 26px;
      object-fit: contain;
      flex-shrink: 0;
    }

    .brand-name {
      font-size: 13.5px;
      font-weight: 800;
      color: #0f172a;
      letter-spacing: -0.01em;
      line-height: 1.2;
    }

    .receipt-doc-title {
      margin: 2px 0 2px;
      text-align: center;
      font-size: 11.5px;
      font-weight: 800;
      color: #0f172a;
      text-transform: uppercase;
      letter-spacing: 0.03em;
      line-height: 1.2;
    }

    .receipt-location {
      font-size: 10.5px;
      color: #475569;
      margin-top: 2px;
      line-height: 1.2;
      text-align: center;
    }

    /* Dashed dividers matching authentic thermal slips */
    .dashed-divider {
      border-top: 1.5px dashed #94a3b8;
      margin: 5px 0;
      width: 100%;
    }
    .dashed-divider.subtle {
      border-top-color: #cbd5e1;
      margin: 3px 0;
    }

    .receipt-kv-list,
    .receipt-totals-list {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .kv-row {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      font-size: 11.5px;
      line-height: 1.25;
    }

    .kv-label {
      color: #334155;
      font-weight: 600;
      white-space: nowrap;
    }

    .kv-val {
      font-weight: 600;
      color: #0f172a;
    }

    .status-badge {
      font-weight: 700;
      color: #047857;
    }

    /* Numbers maintain LTR order even in Arabic */
    .num-val {
      direction: ltr;
      display: inline-block;
      unicode-bidi: embed;
    }

    .val-align {
      text-align: right;
    }
    .is-arabic .val-align {
      text-align: left;
    }

    .items-table {
      margin: 3px 0;
    }

    .items-head {
      display: flex;
      justify-content: space-between;
      font-size: 10px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: #64748b;
      margin-bottom: 3px;
      line-height: 1.2;
    }

    .item-row {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 3px;
      font-size: 11.5px;
      line-height: 1.25;
    }

    .item-info {
      flex: 1;
      padding-inline-end: 8px;
    }

    .item-desc {
      font-weight: 600;
      color: #0f172a;
      line-height: 1.2;
    }

    .item-sub {
      font-size: 10px;
      color: #64748b;
      margin-top: 1px;
      line-height: 1.15;
    }

    .item-amount {
      font-weight: 700;
      white-space: nowrap;
      line-height: 1.2;
    }

    .total-highlight {
      font-size: 13.5px;
      padding: 1px 0;
      line-height: 1.2;
    }

    .total-figure {
      font-size: 14.5px;
      font-weight: 900;
      color: #0f172a;
    }

    .payment-row {
      font-size: 11px;
      color: #475569;
      line-height: 1.2;
    }

    .change-row {
      font-size: 11px;
      color: #047857;
      font-weight: 600;
      line-height: 1.2;
    }

    .receipt-footer {
      text-align: center;
      margin-top: 4px;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .help-text {
      font-size: 11px;
      font-weight: 600;
      color: #334155;
      line-height: 1.2;
    }

    .thankyou-text {
      font-size: 11.5px;
      font-weight: 700;
      color: #0f172a;
      letter-spacing: 0.01em;
      line-height: 1.2;
    }

    .website {
      font-size: 10.5px;
      color: #475569;
      margin-top: 2px;
      line-height: 1.2;
    }

    /* Barcode styling */
    .thermal-barcode {
      margin-top: 6px;
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .barcode-lines {
      height: 22px;
      width: 170px;
      background: repeating-linear-gradient(
        90deg,
        #0f172a 0px,
        #0f172a 2px,
        transparent 2px,
        transparent 4px,
        #0f172a 4px,
        #0f172a 5px,
        transparent 5px,
        transparent 8px,
        #0f172a 8px,
        #0f172a 11px,
        transparent 11px,
        transparent 13px,
        #0f172a 13px,
        #0f172a 14px,
        transparent 14px,
        transparent 17px,
        #0f172a 17px,
        #0f172a 20px,
        transparent 20px,
        transparent 22px
      );
    }

    .barcode-num {
      font-size: 9px;
      color: #64748b;
      letter-spacing: 0.1em;
      margin-top: 1px;
      direction: ltr;
    }

    /* Print media styling */
    @media print {
      .thermal-receipt-wrapper {
        padding: 0;
      }
      .thermal-paper {
        box-shadow: none;
        filter: none;
        margin: 0 auto;
        width: 78mm;
        padding: 4mm 4mm 8mm;
      }
    }
  `],
})
export class ThermalReceiptComponent {
  private readonly i18n = inject(I18nService);

  readonly receipt = input.required<ThermalReceiptData>();
  readonly isAr = computed(() => this.i18n.lang() === 'ar');
  readonly curr = computed(() => this.receipt().currency || 'AED');

  readonly statusLabel = computed(() => {
    const raw = this.receipt().status || 'Completed';
    if (!this.isAr()) return raw;
    switch (raw.toLowerCase()) {
      case 'completed': return 'مكتمل';
      case 'voided': return 'ملغى';
      case 'draft': return 'مسودة';
      case 'successful': return 'ناجح';
      case 'paid': return 'مدفوع';
      default: return raw;
    }
  });

  readonly customerLabel = computed(() => {
    const cust = this.receipt().customerName || this.receipt().account;
    if (cust && cust.toLowerCase() !== 'walk-in' && cust.toLowerCase() !== 'walk-in customer') {
      return cust;
    }
    return this.isAr() ? 'عميل مباشر' : 'Walk-in Customer';
  });

  readonly formattedDate = computed(() => {
    const raw = this.receipt().date;
    if (!raw) return '';
    const d = new Date(raw);
    if (isNaN(d.getTime())) return String(raw);
    const pad = (n: number) => String(n).padStart(2, '0');
    const day = pad(d.getDate());
    const mo = pad(d.getMonth() + 1);
    const yr = d.getFullYear();
    let hrs = d.getHours();
    const isPm = hrs >= 12;
    hrs = hrs % 12 || 12;
    const min = pad(d.getMinutes());
    const sec = pad(d.getSeconds());
    const ampm = this.isAr() ? (isPm ? 'م' : 'ص') : (isPm ? 'PM' : 'AM');
    return `${day}/${mo}/${yr}  ${pad(hrs)}:${min}:${sec} ${ampm}`;
  });

  paymentMethodLabel(method: string): string {
    if (!this.isAr()) return method;
    switch ((method || '').toLowerCase()) {
      case 'cash': return 'نقداً';
      case 'card': return 'بطاقة مصرفية';
      case 'credit card': return 'بطاقة ائتمان';
      case 'bank transfer': return 'تحويل بنكي';
      default: return method;
    }
  }
}
