import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../core/api/api-config';

interface PrintHeader {
  organizationName: string;
  legalEntityName: string;
  legalEntityAddress?: string;
  taxRegistrationNo?: string;
  registrationNo?: string;
  phone?: string;
  email?: string;
  baseCurrency: string;
  countryCode: string;
}

interface InvoiceLine {
  seq: number;
  description: string;
  quantity: number;
  price: number;
  taxAmount: number;
  net: number;
}

interface PrintInvoiceData {
  header: PrintHeader;
  invoice: {
    id: string;
    number: string;
    isCreditNote: boolean;
    invoiceDate: string;
    dueDate: string;
    partyName: string;
    net: number;
    tax: number;
    total: number;
    notes?: string;
    lines: InvoiceLine[];
  };
  partyAddress: string;
}

interface Allocation {
  invoiceNumber: string;
  amount: number;
}

interface PrintReceiptData {
  header: PrintHeader;
  receipt: {
    number: string;
    date: string;
    partyName: string;
    amount: number;
    method: string;
    reference?: string;
  };
  partyName: string;
  partyAddress?: string;
  allocations: Allocation[];
}

interface BillLine {
  seq: number;
  description: string;
  quantity: number;
  price: number;
  taxAmount: number;
  net: number;
}

interface PrintBillData {
  header: PrintHeader;
  bill: {
    number: string;
    billDate: string;
    dueDate: string;
    vendorReference?: string;
    net: number;
    tax: number;
    total: number;
    notes?: string;
    lines: BillLine[];
  };
  partyName: string;
  partyAddress?: string;
}

interface BillAllocation {
  billNumber: string;
  amount: number;
}

interface PrintPaymentData {
  header: PrintHeader;
  payment: {
    number: string;
    date: string;
    amount: number;
    method: string;
    reference?: string;
  };
  partyName: string;
  allocations: BillAllocation[];
}

interface JournalLine {
  seq: number;
  accountCode: string;
  accountName: string;
  description?: string;
  debit: number;
  credit: number;
}

interface PrintJournalData {
  header: PrintHeader;
  journal: {
    number: string;
    date: string;
    memo?: string;
    reference?: string;
    lines: JournalLine[];
  };
}

@Component({
  selector: 'erp-print',
  standalone: true,
  imports: [CommonModule, DatePipe, CurrencyPipe, NzButtonModule, NzIconModule, NzTableModule],
  styles: [`
    @media screen {
      :host {
        display: block;
        padding: 24px;
        background: #f5f5f5;
      }
      .print-container {
        max-width: 210mm;
        margin: 0 auto;
        background: white;
        box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        padding: 20mm;
      }
      .no-print {
        text-align: center;
        margin-bottom: 16px;
      }
    }
    @media print {
      :host {
        display: block;
      }
      .print-container {
        max-width: none;
        padding: 0;
      }
      .no-print {
        display: none;
      }
    }
    .letterhead {
      border-bottom: 2px solid #1890ff;
      padding-bottom: 16px;
      margin-bottom: 24px;
    }
    .org-name {
      font-size: 20px;
      font-weight: 600;
      color: #1890ff;
      margin: 0 0 4px;
    }
    .entity-name {
      font-size: 16px;
      font-weight: 500;
      margin: 0 0 8px;
    }
    .contact-info {
      font-size: 12px;
      color: #666;
      line-height: 1.5;
    }
    .doc-title {
      font-size: 24px;
      font-weight: 600;
      text-align: center;
      margin: 0 0 24px;
      text-transform: uppercase;
    }
    .doc-meta {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
      margin-bottom: 24px;
    }
    .meta-group {
      padding: 12px;
      background: #fafafa;
      border-radius: 4px;
    }
    .meta-group label {
      display: block;
      font-size: 11px;
      text-transform: uppercase;
      color: #999;
      margin-bottom: 4px;
    }
    .meta-group .value {
      font-size: 14px;
      font-weight: 500;
      color: #333;
    }
    .lines-table {
      width: 100%;
      border-collapse: collapse;
      margin: 24px 0;
    }
    .lines-table th {
      background: #f0f0f0;
      padding: 10px 8px;
      text-align: left;
      font-size: 12px;
      font-weight: 600;
      border-bottom: 2px solid #d9d9d9;
    }
    .lines-table td {
      padding: 8px;
      border-bottom: 1px solid #f0f0f0;
      font-size: 13px;
    }
    .lines-table .number {
      text-align: right;
    }
    .totals {
      margin-left: auto;
      width: 300px;
      margin-top: 16px;
    }
    .total-row {
      display: flex;
      justify-content: space-between;
      padding: 6px 0;
      font-size: 14px;
    }
    .total-row.grand {
      border-top: 2px solid #1890ff;
      padding-top: 12px;
      margin-top: 8px;
      font-size: 16px;
      font-weight: 600;
    }
    .notes {
      margin-top: 24px;
      padding: 12px;
      background: #fafafa;
      border-left: 3px solid #1890ff;
      font-size: 13px;
    }
    .footer {
      margin-top: 48px;
      padding-top: 16px;
      border-top: 1px solid #d9d9d9;
      font-size: 11px;
      color: #999;
      text-align: center;
    }
  `],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (data()) {
      <div class="no-print">
        <button nz-button nzType="primary" (click)="print()">
          <span nz-icon nzType="printer"></span> Print / Save as PDF
        </button>
      </div>
      <div class="print-container">
        @if (header()) {
          <div class="letterhead">
            <div class="org-name">{{ header()!.organizationName }}</div>
            <div class="entity-name">{{ header()!.legalEntityName }}</div>
            <div class="contact-info">
              @if (header()!.legalEntityAddress) {
                <div>{{ header()!.legalEntityAddress }}</div>
              }
              <div>
                @if (header()!.phone) { Phone: {{ header()!.phone }} }
                @if (header()!.email) { | Email: {{ header()!.email }} }
              </div>
              @if (header()!.taxRegistrationNo) {
                <div>Tax Reg. No.: {{ header()!.taxRegistrationNo }}</div>
              }
              @if (header()!.registrationNo) {
                <div>Reg. No.: {{ header()!.registrationNo }}</div>
              }
            </div>
          </div>
        }

        @switch (docType()) {
          @case ('invoice') {
            @if (invoiceData(); as inv) {
              <div class="doc-title">{{ inv.invoice.isCreditNote ? 'Credit Note' : 'Tax Invoice' }}</div>
              <div class="doc-meta">
                <div class="meta-group">
                  <label>Document Number</label>
                  <div class="value">{{ inv.invoice.number }}</div>
                </div>
                <div class="meta-group">
                  <label>Date</label>
                  <div class="value">{{ inv.invoice.invoiceDate | date:'dd MMM yyyy' }}</div>
                </div>
                <div class="meta-group">
                  <label>Customer</label>
                  <div class="value">{{ inv.invoice.partyName }}</div>
                  @if (inv.partyAddress) {
                    <div style="font-size: 12px; color: #666;">{{ inv.partyAddress }}</div>
                  }
                </div>
                <div class="meta-group">
                  <label>Due Date</label>
                  <div class="value">{{ inv.invoice.dueDate | date:'dd MMM yyyy' }}</div>
                </div>
              </div>
              
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 40px;">#</th>
                    <th>Description</th>
                    <th class="number" style="width: 80px;">Qty</th>
                    <th class="number" style="width: 100px;">Price</th>
                    <th class="number" style="width: 100px;">Tax</th>
                    <th class="number" style="width: 120px;">Net</th>
                  </tr>
                </thead>
                <tbody>
                  @for (line of inv.invoice.lines; track line.seq) {
                    <tr>
                      <td>{{ line.seq }}</td>
                      <td>{{ line.description }}</td>
                      <td class="number">{{ line.quantity }}</td>
                      <td class="number">{{ line.price | currency:inv.header.baseCurrency }}</td>
                      <td class="number">{{ line.taxAmount | currency:inv.header.baseCurrency }}</td>
                      <td class="number">{{ line.net | currency:inv.header.baseCurrency }}</td>
                    </tr>
                  }
                </tbody>
              </table>
              
              <div class="totals">
                <div class="total-row">
                  <span>Subtotal:</span>
                  <span>{{ inv.invoice.net | currency:inv.header.baseCurrency }}</span>
                </div>
                <div class="total-row">
                  <span>Tax:</span>
                  <span>{{ inv.invoice.tax | currency:inv.header.baseCurrency }}</span>
                </div>
                <div class="total-row grand">
                  <span>Total:</span>
                  <span>{{ inv.invoice.total | currency:inv.header.baseCurrency }}</span>
                </div>
              </div>
              
              @if (inv.invoice.notes) {
                <div class="notes">
                  <strong>Notes:</strong> {{ inv.invoice.notes }}
                </div>
              }
            }
          }

          @case ('receipt') {
            @if (receiptData(); as rec) {
              <div class="doc-title">Receipt</div>
              <div class="doc-meta">
                <div class="meta-group">
                  <label>Receipt Number</label>
                  <div class="value">{{ rec.receipt.number }}</div>
                </div>
                <div class="meta-group">
                  <label>Date</label>
                  <div class="value">{{ rec.receipt.date | date:'dd MMM yyyy' }}</div>
                </div>
                <div class="meta-group">
                  <label>Received From</label>
                  <div class="value">{{ rec.partyName }}</div>
                </div>
                <div class="meta-group">
                  <label>Payment Method</label>
                  <div class="value">{{ rec.receipt.method }}</div>
                </div>
                <div class="meta-group">
                  <label>Amount</label>
                  <div class="value">{{ rec.receipt.amount | currency:rec.header.baseCurrency }}</div>
                </div>
                @if (rec.receipt.reference) {
                  <div class="meta-group">
                    <label>Reference</label>
                    <div class="value">{{ rec.receipt.reference }}</div>
                  </div>
                }
              </div>
              
              @if (rec.allocations.length > 0) {
                <table class="lines-table">
                  <thead>
                    <tr>
                      <th>Invoice Number</th>
                      <th class="number">Amount Applied</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (alloc of rec.allocations; track alloc.invoiceNumber) {
                      <tr>
                        <td>{{ alloc.invoiceNumber }}</td>
                        <td class="number">{{ alloc.amount | currency:rec.header.baseCurrency }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            }
          }

          @case ('bill') {
            @if (billData(); as bil) {
              <div class="doc-title">Purchase Bill</div>
              <div class="doc-meta">
                <div class="meta-group">
                  <label>Bill Number</label>
                  <div class="value">{{ bil.bill.number }}</div>
                </div>
                <div class="meta-group">
                  <label>Bill Date</label>
                  <div class="value">{{ bil.bill.billDate | date:'dd MMM yyyy' }}</div>
                </div>
                <div class="meta-group">
                  <label>Vendor</label>
                  <div class="value">{{ bil.partyName }}</div>
                </div>
                <div class="meta-group">
                  <label>Due Date</label>
                  <div class="value">{{ bil.bill.dueDate | date:'dd MMM yyyy' }}</div>
                </div>
                @if (bil.bill.vendorReference) {
                  <div class="meta-group">
                    <label>Vendor Reference</label>
                    <div class="value">{{ bil.bill.vendorReference }}</div>
                  </div>
                }
              </div>
              
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 40px;">#</th>
                    <th>Description</th>
                    <th class="number" style="width: 80px;">Qty</th>
                    <th class="number" style="width: 100px;">Price</th>
                    <th class="number" style="width: 100px;">Tax</th>
                    <th class="number" style="width: 120px;">Net</th>
                  </tr>
                </thead>
                <tbody>
                  @for (line of bil.bill.lines; track line.seq) {
                    <tr>
                      <td>{{ line.seq }}</td>
                      <td>{{ line.description }}</td>
                      <td class="number">{{ line.quantity }}</td>
                      <td class="number">{{ line.price | currency:bil.header.baseCurrency }}</td>
                      <td class="number">{{ line.taxAmount | currency:bil.header.baseCurrency }}</td>
                      <td class="number">{{ line.net | currency:bil.header.baseCurrency }}</td>
                    </tr>
                  }
                </tbody>
              </table>
              
              <div class="totals">
                <div class="total-row">
                  <span>Subtotal:</span>
                  <span>{{ bil.bill.net | currency:bil.header.baseCurrency }}</span>
                </div>
                <div class="total-row">
                  <span>Tax:</span>
                  <span>{{ bil.bill.tax | currency:bil.header.baseCurrency }}</span>
                </div>
                <div class="total-row grand">
                  <span>Total:</span>
                  <span>{{ bil.bill.total | currency:bil.header.baseCurrency }}</span>
                </div>
              </div>
              
              @if (bil.bill.notes) {
                <div class="notes">
                  <strong>Notes:</strong> {{ bil.bill.notes }}
                </div>
              }
            }
          }

          @case ('payment') {
            @if (paymentData(); as pay) {
              <div class="doc-title">Payment Voucher</div>
              <div class="doc-meta">
                <div class="meta-group">
                  <label>Payment Number</label>
                  <div class="value">{{ pay.payment.number }}</div>
                </div>
                <div class="meta-group">
                  <label>Date</label>
                  <div class="value">{{ pay.payment.date | date:'dd MMM yyyy' }}</div>
                </div>
                <div class="meta-group">
                  <label>Paid To</label>
                  <div class="value">{{ pay.partyName }}</div>
                </div>
                <div class="meta-group">
                  <label>Payment Method</label>
                  <div class="value">{{ pay.payment.method }}</div>
                </div>
                <div class="meta-group">
                  <label>Amount</label>
                  <div class="value">{{ pay.payment.amount | currency:pay.header.baseCurrency }}</div>
                </div>
                @if (pay.payment.reference) {
                  <div class="meta-group">
                    <label>Reference</label>
                    <div class="value">{{ pay.payment.reference }}</div>
                  </div>
                }
              </div>
              
              @if (pay.allocations.length > 0) {
                <table class="lines-table">
                  <thead>
                    <tr>
                      <th>Bill Number</th>
                      <th class="number">Amount Applied</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (alloc of pay.allocations; track alloc.billNumber) {
                      <tr>
                        <td>{{ alloc.billNumber }}</td>
                        <td class="number">{{ alloc.amount | currency:pay.header.baseCurrency }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              }
            }
          }

          @case ('journal') {
            @if (journalData(); as jrn) {
              <div class="doc-title">Journal Entry</div>
              <div class="doc-meta">
                <div class="meta-group">
                  <label>Journal Number</label>
                  <div class="value">{{ jrn.journal.number }}</div>
                </div>
                <div class="meta-group">
                  <label>Date</label>
                  <div class="value">{{ jrn.journal.date | date:'dd MMM yyyy' }}</div>
                </div>
                @if (jrn.journal.reference) {
                  <div class="meta-group">
                    <label>Reference</label>
                    <div class="value">{{ jrn.journal.reference }}</div>
                  </div>
                }
                @if (jrn.journal.memo) {
                  <div class="meta-group">
                    <label>Memo</label>
                    <div class="value">{{ jrn.journal.memo }}</div>
                  </div>
                }
              </div>
              
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 40px;">#</th>
                    <th style="width: 120px;">Account Code</th>
                    <th>Account Name</th>
                    <th>Description</th>
                    <th class="number" style="width: 120px;">Debit</th>
                    <th class="number" style="width: 120px;">Credit</th>
                  </tr>
                </thead>
                <tbody>
                  @for (line of jrn.journal.lines; track line.seq) {
                    <tr>
                      <td>{{ line.seq }}</td>
                      <td>{{ line.accountCode }}</td>
                      <td>{{ line.accountName }}</td>
                      <td>{{ line.description || '-' }}</td>
                      <td class="number">{{ line.debit > 0 ? (line.debit | currency:jrn.header.baseCurrency) : '' }}</td>
                      <td class="number">{{ line.credit > 0 ? (line.credit | currency:jrn.header.baseCurrency) : '' }}</td>
                    </tr>
                  }
                  <tr style="font-weight: 600; border-top: 2px solid #1890ff;">
                    <td colspan="4" style="text-align: right;">Total:</td>
                    <td class="number">{{ totalDebit() | currency:jrn.header.baseCurrency }}</td>
                    <td class="number">{{ totalCredit() | currency:jrn.header.baseCurrency }}</td>
                  </tr>
                </tbody>
              </table>
            }
          }
        }

        @if (header()) {
          <div class="footer">
            This document was generated by {{ header()!.organizationName }} ERP system
          </div>
        }
      </div>
    }
  `,
})
export class PrintPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly apiBase = inject(API_BASE_URL);

  loading = signal(true);
  error = signal<string | null>(null);
  data = signal<PrintInvoiceData | PrintReceiptData | PrintBillData | PrintPaymentData | PrintJournalData | null>(null);
  docType = signal<string>('');

  header = computed(() => this.data()?.header ?? null);
  invoiceData = computed(() => {
    const d = this.data();
    return d && 'invoice' in d ? d as PrintInvoiceData : null;
  });
  receiptData = computed(() => {
    const d = this.data();
    return d && 'receipt' in d ? d as PrintReceiptData : null;
  });
  billData = computed(() => {
    const d = this.data();
    return d && 'bill' in d ? d as PrintBillData : null;
  });
  paymentData = computed(() => {
    const d = this.data();
    return d && 'payment' in d ? d as PrintPaymentData : null;
  });
  journalData = computed(() => {
    const d = this.data();
    return d && 'journal' in d ? d as PrintJournalData : null;
  });

  ngOnInit(): void {
    const type = this.route.snapshot.paramMap.get('type');
    const id = this.route.snapshot.paramMap.get('id');
    
    if (!type || !id) {
      this.error.set('Invalid document type or ID');
      this.loading.set(false);
      return;
    }

    this.docType.set(type);
    
    this.http.get<any>(`${this.apiBase}/print/${type}/${id}`).subscribe({
      next: (response) => {
        this.data.set(response.data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load document');
        this.loading.set(false);
      }
    });
  }

  print(): void {
    window.print();
  }

  totalDebit(): number {
    const jrn = this.journalData();
    return jrn ? jrn.journal.lines.reduce((sum, line) => sum + line.debit, 0) : 0;
  }

  totalCredit(): number {
    const jrn = this.journalData();
    return jrn ? jrn.journal.lines.reduce((sum, line) => sum + line.credit, 0) : 0;
  }
}
