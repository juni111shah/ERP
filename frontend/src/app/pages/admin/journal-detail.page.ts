import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, CurrencyPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { API_BASE_URL } from '../../core/api/api-config';
import { JournalsApi } from '../../core/api/admin.api';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '../../shared/reason-prompt';
import { problemMessage } from '../../core/http/problem-details';
import { DetailPageComponent, ActionButton, KpiCard, DescriptionItem } from '../../shared/detail-page.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';

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
  imports: [TranslatePipe, CommonModule, DatePipe, CurrencyPipe, NzTableModule, NzModalModule, DetailPageComponent],
  template: `
    @if (loading()) {
      <div style="text-align: center; padding: 48px;">Loading...</div>
    } @else if (error()) {
      <div style="text-align: center; padding: 48px; color: #ff4d4f;">{{ error() }}</div>
    } @else if (journal()) {
      <erp-detail-page
        [backLink]="['/finance/journals']"
        [backLabel]="'Back to Journal Entries'"
        [title]="('ui.title.journalNo' | translate: 'Journal Entry #') + journal()!.number"
        [status]="journal()!.status"
        [actions]="actions()"
        [kpiCards]="kpiCards()"
        [descriptions]="descriptions()"
        [createdAt]="journal()!.date"
        [entityName]="'Journal Entry'"
        recordType="JournalEntry" [recordId]="journal()!.id">
        
        <div content style="margin-top: 16px;">
          <h3>Journal Lines</h3>
          <nz-table #linesTable [nzData]="journal()!.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
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
  private readonly i18n = inject(I18nService);
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly api = inject(JournalsApi);
  private readonly modal = inject(NzModalService);

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
    
    // Only manual entries can be edited, deleted or reversed here; document journals follow their document.
    const manual = jrn.sourceType === 'manual';
    return [
      {
        label: 'Post',
        icon: 'check',
        type: 'primary',
        permission: 'finance.journals.post',
        visible: jrn.status === 'draft' && manual,
        handler: () => this.postJournal()
      },
      {
        label: 'Edit',
        icon: 'edit',
        permission: 'finance.journals.write',
        visible: jrn.status === 'draft' && manual,
        handler: () => this.router.navigate(['/finance/journals'], { queryParams: { open: jrn.id } })
      },
      {
        label: 'Delete',
        icon: 'delete',
        danger: true,
        permission: 'finance.journals.write',
        visible: jrn.status === 'draft' && manual,
        handler: () => this.deleteJournal()
      },
      {
        label: 'Reverse',
        icon: 'rollback',
        danger: true,
        permission: 'finance.journals.reverse',
        visible: jrn.status === 'posted' && manual,
        handler: () => this.reverseJournal()
      },
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

  private fail(err: any, fallback: string): void { this.message.error(problemMessage(err, err?.error?.message || fallback, 'memo')); }

  postJournal(): void {
    const jrn = this.journal();
    if (!jrn) return;
    this.api.post(jrn.id).subscribe({
      next: (res) => { this.journal.set(res as unknown as Journal); this.message.success(`${this.i18n.t('ui.msg.postedAs', 'Posted as')} ${res.number}`); },
      error: (err) => this.fail(err, 'Failed to post journal')
    });
  }

  deleteJournal(): void {
    const jrn = this.journal();
    if (!jrn) return;
    this.modal.confirm({
      nzTitle: 'Delete this draft journal?',
      nzOkText: 'Delete',
      nzOkDanger: true,
      nzOnOk: () => this.api.delete(jrn.id).subscribe({
        next: () => { this.message.success('Draft deleted'); this.router.navigate(['/finance/journals']); },
        error: (err) => this.fail(err, 'Failed to delete journal')
      })
    });
  }

  reverseJournal(): void {
    const jrn = this.journal();
    if (!jrn) return;
    // The optional reason is sent as the reversal memo (max 400 characters).
    askReason(this.modal, 'Reverse this journal?', 'Reverse', 'A reversing entry is posted today (or on the original date if that is later).', false).then((reason) => {
      if (reason === null) return;
      this.api.reverse(jrn.id, reason || undefined).subscribe({
        next: (rev) => {
          this.message.success(`${this.i18n.t('ui.msg.reversedBy', 'Reversed by')} ${rev.number}`);
          this.api.detail(jrn.id).subscribe((j) => this.journal.set(j as unknown as Journal));
        },
        error: (err) => this.fail(err, 'Failed to reverse journal')
      });
    });
  }

  viewSource(sourceType: string, sourceId: string): void {
    const type = (sourceType ?? '').toLowerCase().trim();
    const routes: Record<string, string> = {
      'invoice': '/finance/invoices',
      'receipt': '/finance/receipts',
      'bill': '/finance/bills',
      'payment': '/finance/payments',
      'cheque': '/finance/cheques',
      'pos': '/pos/orders',
      'posorder': '/pos/orders',
      'pos-order': '/pos/orders',
      'order': '/sales/orders',
      'sales': '/sales/orders',
      'salesorder': '/sales/orders',
      'quote': '/sales/quotes',
      'purchase': '/purchasing/orders',
      'purchaseorder': '/purchasing/orders',
      'purchasereceipt': '/purchasing/receipts',
      'movement': '/inventory/movements',
      'stock': '/inventory/stock',
      'workorder': '/manufacturing/workorders',
      'payroll': '/hr/payroll',
      'lease': '/real-estate/leases'
    };
    const basePath = routes[type];
    if (basePath) {
      if (sourceId && sourceId !== '00000000-0000-0000-0000-000000000000') {
        this.router.navigate([basePath, sourceId]);
      } else {
        this.router.navigate([basePath]);
      }
    } else {
      this.message.warning('Cannot navigate to source document');
    }
  }
}
