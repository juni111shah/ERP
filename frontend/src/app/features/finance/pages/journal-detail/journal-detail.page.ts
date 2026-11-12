import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { JournalsApi } from '@core/api/admin.api';
import { FinanceDocumentsApi, JournalDetailDto } from '@core/api/finance-documents.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { loadRecord } from '@shared/utils/load-record';
import { askReason } from '@shared/utils/reason-prompt';
import { journalSourceLink, printDocument } from '../../finance-documents';
import { formatMoney } from '../../finance-format';

/** A journal entry: its lines and totals; a manual entry can be posted, edited, deleted or reversed here, a document's journal links to its source. */
@Component({
  selector: 'erp-journal-detail',
  standalone: true,
  imports: [CommonModule, NzModalModule, NzSpinModule, NzTableModule, TranslatePipe, DetailPageComponent, StatePanelComponent],
  templateUrl: './journal-detail.page.html',
})
export class JournalDetailPage implements OnInit {
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly api = inject(JournalsApi);
  private readonly documentsApi = inject(FinanceDocumentsApi);
  private readonly modal = inject(NzModalService);

  loading = signal(true);
  error = signal<string | null>(null);
  journal = signal<JournalDetailDto | null>(null);

  totalDebit = computed(() => this.journal()?.lines.reduce((sum, line) => sum + line.debit, 0) ?? 0);
  totalCredit = computed(() => this.journal()?.lines.reduce((sum, line) => sum + line.credit, 0) ?? 0);

  actions = computed((): ActionButton[] => { const jrn = this.journal(); return jrn ? this.buildActions(jrn) : []; });

  kpiCards = computed((): KpiCard[] => {
    const jrn = this.journal();
    if (!jrn) return [];
    return [
      { title: 'Total Amount', value: jrn.amount, suffix: jrn.currencyCode },
      { title: 'Total Debit', value: this.totalDebit(), suffix: jrn.currencyCode },
      { title: 'Total Credit', value: this.totalCredit(), suffix: jrn.currencyCode },
      { title: 'Lines', value: jrn.lines.length },
    ];
  });

  descriptions = computed((): DescriptionItem[] => { const jrn = this.journal(); return jrn ? journalDescriptions(jrn) : []; });

  money(v: number): string { return formatMoney(v, this.journal()?.currencyCode); }

  retry(): void { this.loading.set(true); this.error.set(null); this.ngOnInit(); }

  ngOnInit(): void {
    loadRecord(this.route.snapshot.paramMap.get('id'), (id) => this.documentsApi.journal(id), { loading: this.loading, error: this.error, record: this.journal },
      { invalid: 'Invalid journal ID', failed: 'Failed to load journal entry' });
  }

  postJournal(): void {
    const jrn = this.journal();
    if (!jrn) return;
    this.api.post(jrn.id).subscribe({
      next: (res) => { this.journal.set(res as unknown as JournalDetailDto); this.message.success(`${this.i18n.t('ui.msg.postedAs', 'Posted as')} ${res.number}`); },
      error: (err) => this.fail(err, 'Failed to post journal'),
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
        next: () => { this.message.success(doneText('deleted', 'Draft journal', jrn.number)); this.router.navigate(['/finance/journals']); },
        error: (err) => this.fail(err, 'Failed to delete journal'),
      }),
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
          this.api.detail(jrn.id).subscribe((j) => this.journal.set(j as unknown as JournalDetailDto));
        },
        error: (err) => this.fail(err, 'Failed to reverse journal'),
      });
    });
  }

  viewSource(sourceType: string, sourceId: string): void {
    const link = journalSourceLink(sourceType, sourceId);
    if (link) this.router.navigate(link);
    else this.message.warning('Cannot navigate to source document');
  }

  private buildActions(jrn: JournalDetailDto): ActionButton[] {
    // Only manual entries can be edited, deleted or reversed here; document journals follow their document.
    const manual = jrn.sourceType === 'manual';
    const draft = jrn.status === 'draft' && manual;
    return [
      { label: 'Post', icon: 'check', type: 'primary', permission: 'finance.journals.post', visible: draft, handler: () => this.postJournal() },
      { label: 'Edit', icon: 'edit', permission: 'finance.journals.write', visible: draft, handler: () => this.router.navigate(['/finance/journals'], { queryParams: { open: jrn.id } }) },
      { label: 'Delete', icon: 'delete', danger: true, permission: 'finance.journals.write', visible: draft, handler: () => this.deleteJournal() },
      { label: 'Reverse', icon: 'rollback', danger: true, permission: 'finance.journals.reverse', visible: jrn.status === 'posted' && manual, handler: () => this.reverseJournal() },
      { label: 'Print', icon: 'printer', handler: () => printDocument('journal', jrn.id) },
      { label: 'View Source', icon: 'link', visible: !!jrn.sourceType && !!jrn.sourceId, handler: () => this.viewSource(jrn.sourceType!, jrn.sourceId!) },
    ];
  }

  /** The server's message when it sent one, else `fallback`. */
  private fail(err: { error?: { message?: string } } | null | undefined, fallback: string): void { this.message.error(problemMessage(err, err?.error?.message || fallback, 'memo')); }
}

/** Date, currency and posting date; reference and memo when there are any. */
function journalDescriptions(jrn: JournalDetailDto): DescriptionItem[] {
  const items: DescriptionItem[] = [
    { label: 'Date', value: displayDate(jrn.date) },
    { label: 'Currency', value: jrn.currencyCode },
    { label: 'Posted', value: jrn.postedAt ? displayDate(jrn.postedAt) : 'Not posted' },
  ];
  if (jrn.reference) items.push({ label: 'Reference', value: jrn.reference });
  if (jrn.memo) items.push({ label: 'Memo', value: jrn.memo, span: 2 });
  return items;
}
