import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '../../core/api/api-config';
import { ReceiptsApi } from '../../core/api/admin.api';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '../../shared/reason-prompt';
import { problemMessage } from '../../core/http/problem-details';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface Allocation {
  id: string;
  invoiceId: string;
  invoiceNumber: string;
  amount: number;
}

interface Receipt {
  id: string;
  number: string;
  legalEntityId: string;
  partyId: string;
  partyName: string;
  date: string;
  method: string;
  bankAccountId: string | null;
  currencyCode: string;
  amount: number;
  unallocated: number;
  reference: string | null;
  status: string;
  journalEntryId: string | null;
  allocations: Allocation[];
}

@Component({
  selector: 'erp-receipt-detail',
  standalone: true,
  imports: [TranslatePipe, CommonModule, DatePipe, CurrencyPipe, NzTableModule, NzModalModule, DetailPageComponent],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (receipt()) {
      <erp-detail-page
        [backLink]="['/finance/receipts']"
        [backLabel]="'Back to Receipts'"
        [title]="('ui.title.receiptNo' | translate: 'Receipt #') + receipt()!.number"
        [status]="receipt()!.status"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="receipt()!.date"
        [entityName]="'Receipt'"
        recordType="Settlement" [recordId]="receipt()!.id">
        
        @if (receipt()!.allocations.length > 0) {
          <div content style="margin-top: 16px;">
            <h3>Applied to Invoices</h3>
            <nz-table #allocTable [nzData]="receipt()!.allocations" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
              <thead>
                <tr>
                  <th>Invoice Number</th>
                  <th style="width: 150px; text-align: right;">Amount Applied</th>
                </tr>
              </thead>
              <tbody>
                @for (alloc of allocTable.data; track alloc.id) {
                  <tr>
                    <td>{{ alloc.invoiceNumber }}</td>
                    <td style="text-align: right;">{{ alloc.amount | currency:receipt()!.currencyCode }}</td>
                  </tr>
                }
              </tbody>
            </nz-table>
          </div>
        }
      </erp-detail-page>
    }
  `
})
export class ReceiptDetailPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly api = inject(ReceiptsApi);
  private readonly modal = inject(NzModalService);

  loading = signal(true);
  error = signal<string | null>(null);
  receipt = signal<Receipt | null>(null);

  actions = computed((): ActionButton[] => {
    const rec = this.receipt();
    if (!rec) return [];
    
    return [
      {
        label: 'Allocate',
        icon: 'link',
        type: 'primary',
        permission: 'finance.receipts.write',
        visible: rec.status === 'issued' && rec.unallocated > 0,
        handler: () => this.router.navigate(['/finance/receipts'], { queryParams: { open: rec.id } })
      },
      {
        label: 'Void',
        icon: 'stop',
        danger: true,
        permission: 'finance.receipts.write',
        visible: rec.status === 'issued',
        handler: () => this.voidReceipt()
      },
      {
        label: 'View Journal',
        icon: 'book',
        permission: 'finance.journals.read',
        visible: !!rec.journalEntryId,
        handler: () => this.router.navigate(['/finance/journals', rec.journalEntryId])
      },
      {
        label: 'Print',
        icon: 'printer',
        handler: () => window.open(`/print/receipt/${rec.id}`, '_blank')
      },
      {
        label: 'Email',
        icon: 'mail',
        handler: () => this.message.info('Email functionality will be implemented')
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const rec = this.receipt();
    if (!rec) return [];
    
    return [
      {
        title: 'Total Amount',
        value: rec.amount,
        suffix: rec.currencyCode
      },
      {
        title: 'Allocated',
        value: rec.amount - rec.unallocated,
        suffix: rec.currencyCode
      },
      {
        title: 'Unallocated',
        value: rec.unallocated,
        suffix: rec.currencyCode,
        valueStyle: { color: rec.unallocated > 0 ? '#fa8c16' : '#52c41a' }
      },
      {
        title: 'Invoices Applied',
        value: rec.allocations.length
      }
    ];
  });

  descriptions = computed((): DescriptionItem[] => {
    const rec = this.receipt();
    if (!rec) return [];
    
    return [
      { label: 'Customer', value: rec.partyName, routerLink: ['/parties', rec.partyId] },
      { label: 'Date', value: new Date(rec.date).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) },
      { label: 'Payment Method', value: rec.method },
      { label: 'Currency', value: rec.currencyCode },
      { label: 'Reference', value: rec.reference, span: 2 }
    ];
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid receipt ID');
      this.loading.set(false);
      return;
    }

    this.http.get<any>(`${this.apiBase}/finance/receipts/${id}`).subscribe({
      next: (response) => {
        this.receipt.set(response);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load receipt');
        this.loading.set(false);
      }
    });
  }

  voidReceipt(): void {
    const rec = this.receipt();
    if (!rec) return;
    askReason(this.modal, 'Void this receipt?', 'Void Receipt', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (!reason) return;
      this.api.void(rec.id, reason).subscribe({
        next: (res) => { this.receipt.set(res as unknown as Receipt); this.message.success('Receipt voided'); },
        error: (err) => this.message.error(problemMessage(err, 'Failed to void receipt', 'reason'))
      });
    });
  }
}
