import { doneText } from '@core/format/done-text';
import { ActivatedRoute, Router } from '@angular/router';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, forkJoin, of } from 'rxjs';
import { ListQuery } from '@core/api/api.models';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { countRows } from '../../finance-kpis';
import { AccountDto, AccountsApi, JournalDto, JournalSummary, JournalsApi, LegalEntitiesApi, LegalEntityDto } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { asProblem, controlError, problemMessage } from '@core/http/problem-details';
import { askReason } from '@shared/utils/reason-prompt';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { formatMoney } from '../../components/invoice-sheet/invoice-sheet.component';
import { I18nService } from '@core/i18n/i18n.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';

import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';

/** Journals: list, draft entry with a balanced-lines editor, post, reverse; plus closing the books of a legal entity. */
@Component({
  selector: 'erp-journals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, StatCardComponent, StatusTagComponent, TranslatePipe, NzToolTipModule, RevealDirective, ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule],
  styleUrl: './journals.page.scss',
  templateUrl: './journals.page.html',
})
export class JournalsPage {
  private readonly i18n = inject(I18nService);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(JournalsApi);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly accounts = computed(() => this.accountPage().items.filter((a) => a.isPostable && a.isActive));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });

  /** Counted by the server for the whole workspace, not the page on screen. */
  readonly stats = signal<{ posted: number; drafts: number; reversed: number } | null>(null);
  readonly statsLoading = signal(true);
  /** Caption of the cards when their figures could not be read. */
  readonly statsError = computed(() => (this.statsLoading() ? null : 'Could not load this figure'));
  reload(): void { this.list.reload(); this.loadStats(); }
  private loadStats(): void {
    const count = (status: string) => countRows((q: ListQuery) => this.api.list(q), [{ field: 'status', op: 'eq', value: status }]);
    forkJoin({ posted: count('posted'), drafts: count('draft'), reversed: count('reversed') }).subscribe({
      next: (s) => { this.stats.set(s); this.statsLoading.set(false); },
      error: () => { this.stats.set(null); this.statsLoading.set(false); },
    });
  }
  readonly columns: TableColumn<JournalSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'date', label: 'Date', sortField: 'date', width: '120px', filterField: 'date' },
    { key: 'memo', label: 'Memo' },
    { key: 'sourceType', label: 'Source', width: '130px' },
    { key: 'amount', label: 'Amount', sortField: 'amount', width: '130px', align: 'right', value: (r) => formatMoney(r.amount, r.currencyCode) },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: [{ value: 'draft', label: 'Draft' }, { value: 'posted', label: 'Posted' }, { value: 'reversed', label: 'Reversed' }] },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly journal = signal<JournalDto | null>(null);
  readonly lockOpen = signal(false);
  lockEntity: string | null = null;
  lockDate: Date | null = null;
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], date: [null as Date | null, Validators.required], memo: [''], reference: [''],
    lines: this.fb.array<unknown>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  readonly totals = computed(() => {
    const lines = (this.value().lines ?? []) as { debit?: number | null; credit?: number | null }[];
    return { debit: lines.reduce((s, l) => s + (l.debit ?? 0), 0), credit: lines.reduce((s, l) => s + (l.credit ?? 0), 0) };
  });
  readonly editable = computed(() => !this.journal() || (this.journal()!.status === 'draft' && this.journal()!.sourceType === 'manual'));

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  /** A line field the user has tried to save empty; these sit outside nz-form-control, so they need their own red state. */
  lineStatus(line: unknown, name: string): 'error' | '' { const c = (line as { get(n: string): { invalid: boolean; touched: boolean } | null }).get(name); return c?.invalid && c.touched ? 'error' : ''; }
  readonly money = formatMoney;
  err(name: string): string { return controlError(this.form.get(name)); }

  private newLine(l?: { accountId?: string; debit?: number; credit?: number; description?: string }) {
    return this.fb.group({ accountId: [l?.accountId ?? '', Validators.required], debit: [l?.debit ?? 0], credit: [l?.credit ?? 0], description: [l?.description ?? ''] });
  }
  addLine(): void { this.lines.push(this.newLine() as never); }
  removeLine(i: number): void { if (this.lines.length > 2) this.lines.removeAt(i); }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions). */
  ngOnInit(): void {
    this.loadStats();
    const id = this.route.snapshot.queryParamMap.get('open');
    if (id) this.open({ id } as never);
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/journals', id]); }
  printDoc(id: string): void { window.open(`/print/journal/${id}`, '_blank'); }

  open(row: JournalSummary | null): void {
    if (!row) { this.show(null); return; }
    this.api.detail(row.id).subscribe((j) => this.show(j));
  }

  private show(j: JournalDto | null): void {
    this.journal.set(j);
    this.lines.clear();
    for (const l of j?.lines ?? [{}, {}]) this.lines.push(this.newLine(l) as never);
    const entities = this.entities();
    this.form.patchValue({
      legalEntityId: j?.legalEntityId ?? (entities.length === 1 ? entities[0].id : ''), memo: j?.memo ?? '', reference: j?.reference ?? '',
      date: j ? new Date(j.date + 'T00:00:00') : new Date(),
    });
    this.editable() ? this.form.enable() : this.form.disable();
    this.drawerOpen.set(true);
  }

  save(post: boolean): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, date: toIsoDate(v.date!), memo: v.memo || null, reference: v.reference || null,
      lines: (v.lines as { accountId: string; debit: number; credit: number; description: string }[]).map((l) => ({ ...l, debit: l.debit ?? 0, credit: l.credit ?? 0, description: l.description || null })),
    };
    this.saving.set(true);
    this.api.save(this.journal()?.id ?? null, body).subscribe({
      next: (saved) => {
        if (!post) return this.done(saved, doneText('saved', 'Draft journal', saved.number));
        this.api.post(saved.id).subscribe({ next: (p) => this.done(p, `${this.i18n.t('ui.msg.postedAs', 'Posted as')} ${p.number}`), error: (e) => { this.saving.set(false); this.fail(e, saved); } });
      },
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  reverse(): void {
    const j = this.journal();
    if (!j) return;
    void askReason(this.modal, 'Reverse this journal?', 'Reverse', 'A reversing entry is posted today (or on the original date if that is later). The reason becomes its memo.', false).then((reason) => {
      if (reason === null) return;
      this.api.reverse(j.id, reason || undefined).subscribe({ next: (r) => { this.drawerOpen.set(false); this.message.success(`${this.i18n.t('ui.msg.reversedBy', 'Reversed by')} ${r.number}`); this.reload(); }, error: (e) => this.fail(e) });
    });
  }

  remove(): void {
    const j = this.journal();
    if (!j) return;
    this.modal.confirm({
      nzTitle: 'Delete this draft journal?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(j.id).subscribe({ next: () => { this.drawerOpen.set(false); this.message.success(doneText('deleted', 'Draft journal', j.number)); this.reload(); }, error: (e) => this.fail(e) }),
    });
  }

  lock(): void {
    if (!this.lockEntity) return;
    this.saving.set(true);
    this.api.lockBooks(this.lockEntity, this.lockDate ? toIsoDate(this.lockDate) : null).subscribe({
      next: () => { this.saving.set(false); this.lockOpen.set(false); this.message.success(this.lockDate ? `Books locked up to ${toIsoDate(this.lockDate)}` : 'Books unlocked'); },
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  private done(j: JournalDto, text: string): void {
    this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.reload();
  }

  private fail(e: unknown, saved?: JournalDto): void {
    const p = asProblem(e);
    if (saved) this.journal.set(saved); // saved as a draft but could not post; keep it open for correction
    this.message.error(problemMessage(e, p?.detail ?? p?.title ?? 'Could not save the journal', 'memo'));
    if (saved) this.reload();
  }
}
