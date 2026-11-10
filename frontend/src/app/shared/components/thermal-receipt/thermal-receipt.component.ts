import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { code39 } from '@core/format/code39';
import { I18nService } from '@core/i18n/i18n.service';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { receiptCustomerLabel, receiptDateTime, receiptPaymentLabel, receiptStatusLabel } from './thermal-receipt-labels';

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

  readonly statusLabel = computed(() => receiptStatusLabel(this.receipt().status, this.isAr()));
  readonly customerLabel = computed(() => receiptCustomerLabel(this.receipt().customerName || this.receipt().account, this.isAr()));
  readonly formattedDate = computed(() => receiptDateTime(this.receipt().date, this.isAr()));

  paymentMethodLabel(method: string): string { return receiptPaymentLabel(method, this.isAr()); }
}
