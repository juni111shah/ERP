import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '../../core/api/api-config';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';

interface JournalLine {
  id: string;
  seq: number;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  description: string | null;
  partyId: string | null;
}

interface Journal {
  id: string;
  number: string;
  legalEntityId: string;
  date: string;
  memo: string | null;
  reference: string | null;
  sourceType: string | null;
  sourceId: string | null;
  currencyCode: string;
  status: string;
  amount: number;
  reversalOfId: string | null;
  postedAt: string | null;
  lines: JournalLine[];
}

@Component({
  selector: 'erp-journal-detail',
  standalone: true,
  imports: [CommonModule, DatePipe, CurrencyPipe, NzTableModule, DetailPageComponent],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (journal()) {
      <erp-detail-page
        [backLink]="['/finance/journals']"
        [backLabel]="'Back to Journal Entries'"
        [title]="'Journal Entry #' + journal()!.number"
        [status]="journal()!.status"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="journal()!.date"
        [entityName]="'Journal Entry'">
        
        <div content style="margin-top: 16px;">
          <h3>Journal Lines</h3>
          <nz-table #linesTable [nzData]="journal()!.lines" [nzFrontPagination]="false" nzSize="small">
            <thead>
              <tr>
                <th style="width: 40px;">#</th>
                <th style="width: 120px;">Account Code</th>
                <th>Account Name</th>
                <th>Description</th>
                <th style="width: 140px; text-align: right;">Debit</th>
                <th style="width: 140px; text-align: right;">Credit</th>
              </tr>
            </thead>
            <tbody>
              @for (line of linesTable.data; track line.id) {
                <tr>
                  <td>{{ line.seq }}</td>
                  <td>{{ line.accountCode }}</td>
                  <td>{{ line.accountName }}</td>
                  <td>{{ line.description || '—' }}</td>
                  <td style="text-align: right;">
                    {{ line.debit > 0 ? (line.debit | currency:journal()!.currencyCode) : '' }}
                  </td>
                  <td style="text-align: right;">
                    {{ line.credit > 0 ? (line.credit | currency:journal()!.currencyCode) : '' }}
                  </td>
                </tr>
              }
              <tr style="font-weight: 600; border-top: 2px solid #1890ff;">
                <td colspan="4" style="text-align: right;">Total:</td>
                <td style="text-align: right;">{{ totalDebit() | currency:journal()!.currencyCode }}</td>
                <td style="text-align: right;">{{ totalCredit() | currency:journal()!.currencyCode }}</td>
              </tr>
            </tbody>
          </nz-table>
        </div>
      </erp-detail-page>
    }
  `
})
export class JournalDetailPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly apiBase = inject(API_BASE_URL);

  loading = signal(true);
  error = signal<string | null>(null);
  journal = signal<Journal | null>(null);

  totalDebit = computed(() => {
    const jrn = this.journal();
    return jrn ? jrn.lines.reduce((sum, line) => sum + line.debit, 0) : 0;
  });

  totalCredit = computed(() => {
    const jrn = this.journal();
    return jrn ? jrn.lines.reduce((sum, line) => sum + line.credit, 0) : 0;
  });

  actions = computed((): ActionButton[] => {
    const jrn = this.journal();
    if (!jrn) return [];
    
    return [
      {
        label: 'Print',
        icon: 'printer',
        handler: () => window.open(`/print/journal/${jrn.id}`, '_blank')
      },
      {
        label: 'View Source',
        icon: 'link',
        visible: !!jrn.sourceType && !!jrn.sourceId,
        handler: () => this.viewSource(jrn.sourceType!, jrn.sourceId!)
      }
    ];
  });

  kpiCards = computed((): KpiCard[] => {
    const jrn = this.journal();
    if (!jrn) return [];
    
    return [
      {
        title: 'Total Amount',
        value: jrn.amount,
        suffix: jrn.currencyCode
      },
      {
        title: 'Total Debit',
        value: this.totalDebit(),
        suffix: jrn.currencyCode
      },
      {
        title: 'Total Credit',
        value: this.totalCredit(),
        suffix: jrn.currencyCode
      },
      {
        title: 'Lines',
        value: jrn.lines.length
      }
    ];
  });

  descriptions = computed((): DescriptionItem[] => {
    const jrn = this.journal();
    if (!jrn) return [];
    
    const items: DescriptionItem[] = [
      { label: 'Date', value: new Date(jrn.date).toLocaleDateString('en-US', { day: '2-digit', month: 'short', year: 'numeric' }) },
      { label: 'Currency', value: jrn.currencyCode },
      { label: 'Posted', value: jrn.postedAt ? new Date(jrn.postedAt).toLocaleString('en-US', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : 'Not posted' }
    ];

    if (jrn.reference) {
      items.push({ label: 'Reference', value: jrn.reference });
    }

    if (jrn.memo) {
      items.push({ label: 'Memo', value: jrn.memo, span: 2 });
    }

    return items;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Invalid journal ID');
      this.loading.set(false);
      return;
    }

    this.http.get<any>(`${this.apiBase}/finance/journals/${id}`).subscribe({
      next: (response) => {
        this.journal.set(response);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load journal entry');
        this.loading.set(false);
      }
    });
  }

  viewSource(sourceType: string, sourceId: string): void {
    const routes: Record<string, string> = {
      'invoice': '/finance/invoices',
      'receipt': '/finance/receipts',
      'bill': '/finance/bills',
      'payment': '/finance/payments'
    };
    const basePath = routes[(sourceType ?? '').toLowerCase()];
    if (basePath) {
      this.router.navigate([basePath, sourceId]);
    } else {
      this.message.warning('Cannot navigate to source document');
    }
  }
}
