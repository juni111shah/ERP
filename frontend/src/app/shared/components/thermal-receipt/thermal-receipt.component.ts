import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { code39 } from '@core/format/code39';
import { I18nService } from '@core/i18n/i18n.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

export interface ThermalReceiptLine {
  code?: string;
  description: string;
  quantity: number;
  unitPrice?: number;
  discountPercent?: number;
  lineTotal: number;
}

export interface ThermalReceiptPayment {
  method: string;
  amount: number;
  /** Cash handed over, shown when it differs from the amount applied. */
  tendered?: number;
  changeAmount?: number;
  reference?: string | null;
}

export interface ThermalReceiptData {
  /** the register's receipt header and footer text */
  headerNote?: string | null;
  footerNote?: string | null;
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
  /** On a refund: the sale it is against. */
  againstNumber?: string;
  currency?: string;
  lines?: ThermalReceiptLine[];
  subTotal?: number;
  discountTotal?: number;
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
  templateUrl: './thermal-receipt.component.html',
  styleUrl: './thermal-receipt.component.scss',
})
export class ThermalReceiptComponent {
  private readonly i18n = inject(I18nService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly receipt = input.required<ThermalReceiptData>();
  readonly isAr = computed(() => this.i18n.lang() === 'ar');
  readonly curr = computed(() => this.receipt().currency || this.currencyService.currency());
  readonly barcode = computed(() => code39(this.receipt().receiptNumber));

  readonly statusLabel = computed(() => {
    const raw = this.receipt().status || 'Completed';
    if (!this.isAr()) return raw;
    switch (raw.toLowerCase()) {
      case 'completed': return 'مكتمل';
      case 'refunded': return 'مسترد';
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
