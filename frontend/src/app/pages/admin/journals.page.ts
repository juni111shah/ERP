import { ActivatedRoute, Router } from '@angular/router';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, forkJoin, of } from 'rxjs';
import { ListQuery } from '../../core/api/api.models';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { countRows } from './finance-kpis';
import { AccountDto, AccountsApi, JournalDto, JournalSummary, JournalsApi, LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem, controlError, problemMessage } from '../../core/http/problem-details';
import { askReason } from '../../shared/reason-prompt';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { formatMoney } from './invoice-sheet.component';
import { I18nService } from '../../core/i18n/i18n.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

import { StatCardComponent } from '../../shared/stat-card.component';
import { ErpLabelPipe } from '../../shared/display.pipes';
import { ErpFormModule } from '../../core/forms/erp-form.module';
import { showFormErrors } from '../../core/forms/form-errors';

/** Journals: list, draft entry with a balanced-lines editor, post, reverse; plus closing the books of a legal entity. */
@Component({
  selector: 'erp-journals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, StatCardComponent, StatusTagComponent, TranslatePipe, NzToolTipModule, RevealDirective, ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, ErpFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule],
  styles: [`.line { display:grid; grid-template-columns: 2fr 1fr 1fr 1.4fr 32px; gap:8px; margin-bottom:8px; align-items:center } .line nz-input-number { width:100% }
    .line-head { margin-bottom:4px; font-size:12px; font-weight:600; color: var(--erp-color-text-muted) }
    .totals { display:flex; gap:24px; justify-content:flex-end; margin:8px 0; font-variant-numeric: tabular-nums } .bad { color: var(--erp-color-error, #cf1322) }`],
  template: `
    <h1 class="erp-page-title">Journals</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="journal-stats">
      <erp-stat-card label="Posted" [value]="stats()?.posted" format="number" [loading]="statsLoading()" [caption]="stats() ? 'In the general ledger' : statsError()" [tone]="1" />
      <erp-stat-card label="Drafts" [value]="stats()?.drafts" format="number" [loading]="statsLoading()" [caption]="stats() ? 'Not posted yet' : statsError()" [tone]="2" />
      <erp-stat-card label="Reversed" [value]="stats()?.reversed" format="number" [loading]="statsLoading()" [caption]="stats() ? 'Undone by a later entry' : statsError()" [tone]="3" />
    </div>

    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, memo or reference" emptyText="No journals yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('number') {
            <span class="code-pill">{{ row.number }}</span>
          }
          @case ('sourceType') {
            <nz-tag>{{ row.sourceType | erpLabel }}</nz-tag>
          }
          @case ('amount') {
            <b>{{ value }}</b>
          }
          @case ('status') {
            <erp-status [value]="row.status" />
          }
          @default {
            {{ value }}
          }
        }
      </ng-template>
      <button toolbar *erpHasPermission="'core.entities.write'" nz-button (click)="lockOpen.set(true)" data-testid="lock-books"><span nz-icon nzType="lock"></span>Close books</button>
      <button toolbar *erpHasPermission="'finance.journals.write'" nz-button nzType="primary" (click)="open(null)" data-testid="journal-new"><span nz-icon nzType="plus"></span>New journal</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="820" [nzTitle]="journal() ? journal()!.number : 'New journal'" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="journal-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="j-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Date</nz-form-label><nz-form-control [nzErrorTip]="err('date')"><nz-date-picker nzFormat="dd MMM yyyy" formControlName="date" style="width:100%" data-testid="j-date"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Memo</nz-form-label><nz-form-control><input nz-input formControlName="memo" data-testid="j-memo" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Reference</nz-form-label><nz-form-control><input nz-input formControlName="reference" /></nz-form-control></nz-form-item>
          <h4>Lines</h4>
          <div formArrayName="lines">
            <div class="line line-head" aria-hidden="true" data-testid="j-line-head"><span>Account</span><span>Debit</span><span>Credit</span><span>Description</span><span></span></div>
            @for (l of lines.controls; track $index) {
              <div class="line" [formGroupName]="$index">
                <nz-select [nzStatus]="lineStatus(l, 'accountId')" formControlName="accountId" nzShowSearch nzPlaceHolder="Account" [attr.data-testid]="'j-account-' + $index">@for (a of accounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
                <nz-input-number formControlName="debit" [nzMin]="0" [nzPrecision]="2" nzPlaceHolder="Debit" [attr.data-testid]="'j-debit-' + $index"></nz-input-number>
                <nz-input-number formControlName="credit" [nzMin]="0" [nzPrecision]="2" nzPlaceHolder="Credit" [attr.data-testid]="'j-credit-' + $index"></nz-input-number>
                <input aria-label="Description" nz-input formControlName="description" />
                @if (editable()) { <button aria-label="Remove line" nz-button nzType="text" type="button" (click)="removeLine($index)"><span nz-icon nzType="delete"></span></button> }
              </div>
            }
          </div>
          @if (editable()) { <button nz-button nzType="dashed" type="button" (click)="addLine()" data-testid="j-add-line"><span nz-icon nzType="plus"></span> Add line</button> }
          <div class="totals" data-testid="j-totals">
            <span>Debit <b>{{ money(totals().debit) }}</b></span><span>Credit <b>{{ money(totals().credit) }}</b></span>
            <span [class.bad]="totals().debit !== totals().credit">Difference <b>{{ money(totals().debit - totals().credit) }}</b></span>
          </div>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Close</button>
      @if (journal()?.id) { <button nz-button (click)="openDetails(journal()!.id)" style="margin-inline-start:8px" data-testid="journal-details"><span nz-icon nzType="file-text"></span>Details</button><button nz-button (click)="printDoc(journal()!.id)" style="margin-inline-start:8px" data-testid="journal-print"><span nz-icon nzType="printer"></span>Print / PDF</button> }
      @if (journal()?.status === 'posted' && journal()!.sourceType === 'manual') {
        <button *erpHasPermission="'finance.journals.reverse'" nz-button nzDanger (click)="reverse()" style="margin-inline-start:8px" data-testid="j-reverse">Reverse</button>
      }
      @if (editable()) {
        @if (journal()?.id) { <button *erpHasPermission="'finance.journals.write'" nz-button nzDanger (click)="remove()" style="margin-inline-start:8px" data-testid="j-delete">Delete</button> }
        <button *erpHasPermission="'finance.journals.write'" nz-button [nzLoading]="saving()" (click)="save(false)" style="margin-inline-start:8px" data-testid="j-save">Save draft</button>
        <button *erpHasPermission="'finance.journals.post'" nz-button nzType="primary" [nzLoading]="saving()" (click)="save(true)" style="margin-inline-start:8px" data-testid="j-post">Save and post</button>
      }
    </ng-template>

    <nz-modal [nzVisible]="lockOpen()" nzTitle="Close books" (nzOnCancel)="lockOpen.set(false)" (nzOnOk)="lock()" nzOkText="Save" [nzOkLoading]="saving()">
      <ng-container *nzModalContent>
        <p>Nothing can be posted on or before the closing date. Clear the date to reopen the books.</p>
        <nz-select [(ngModel)]="lockEntity" style="width:100%;margin-bottom:12px" nzPlaceHolder="Legal entity" data-testid="lock-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name + (e.booksLockedTo ? ' (' + ('ui.journal.closedTo' | translate: 'closed to') + ' ' + e.booksLockedTo + ')' : '')"></nz-option> }</nz-select>
        <nz-date-picker nzFormat="dd MMM yyyy" [(ngModel)]="lockDate" style="width:100%" data-testid="lock-date"></nz-date-picker>
      </ng-container>
    </nz-modal>`,
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
    if (this.form.invalid) { showFormErrors(this.form); this.message.warning('Fill in the fields marked in red'); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, date: toIsoDate(v.date!), memo: v.memo || null, reference: v.reference || null,
      lines: (v.lines as { accountId: string; debit: number; credit: number; description: string }[]).map((l) => ({ ...l, debit: l.debit ?? 0, credit: l.credit ?? 0, description: l.description || null })),
    };
    this.saving.set(true);
    this.api.save(this.journal()?.id ?? null, body).subscribe({
      next: (saved) => {
        if (!post) return this.done(saved, 'Draft saved');
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
      nzOnOk: () => this.api.delete(j.id).subscribe({ next: () => { this.drawerOpen.set(false); this.message.success('Draft deleted'); this.reload(); }, error: (e) => this.fail(e) }),
    });
  }

  lock(): void {
    if (!this.lockEntity) return;
    this.saving.set(true);
    this.api.lockBooks(this.lockEntity, this.lockDate ? toIsoDate(this.lockDate) : null).subscribe({
      next: () => { this.saving.set(false); this.lockOpen.set(false); this.message.success('Books updated'); },
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
