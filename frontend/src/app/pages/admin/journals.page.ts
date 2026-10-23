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
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi, JournalDto, JournalSummary, JournalsApi, LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { asProblem, controlError, problemMessage } from '../../core/http/problem-details';
import { askReason } from '../../shared/reason-prompt';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

const STATUS_COLORS: Record<string, string> = { draft: 'default', posted: 'green', reversed: 'orange' };

/** Journals: list, draft entry with a balanced-lines editor, post, reverse; plus closing the books of a legal entity. */
@Component({
  selector: 'erp-journals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule],
  styles: [`.line { display:grid; grid-template-columns: 2fr 1fr 1fr 1.4fr auto; gap:8px; margin-bottom:8px; align-items:center } .totals { display:flex; gap:24px; justify-content:flex-end; margin:8px 0 } .bad { color: var(--erp-color-error, #cf1322) }`],
  template: `
    <h1 class="erp-page-title">Journals</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, memo or reference" emptyText="No journals yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <nz-tag [nzColor]="color(row.status)">{{ row.status }}</nz-tag> } @else { {{ value }} }
      </ng-template>
      <button toolbar *erpHasPermission="'core.entities.write'" nz-button (click)="lockOpen.set(true)" data-testid="lock-books"><span nz-icon nzType="lock"></span>Close books</button>
      <button toolbar *erpHasPermission="'finance.journals.write'" nz-button nzType="primary" (click)="open(null)" data-testid="journal-new"><span nz-icon nzType="plus"></span>New journal</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="820" [nzTitle]="journal() ? journal()!.number : 'New journal'" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="journal-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="j-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Date</nz-form-label><nz-form-control [nzErrorTip]="err('date')"><nz-date-picker formControlName="date" style="width:100%" data-testid="j-date"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Memo</nz-form-label><nz-form-control><input nz-input formControlName="memo" data-testid="j-memo" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Reference</nz-form-label><nz-form-control><input nz-input formControlName="reference" /></nz-form-control></nz-form-item>
          <h4>Lines</h4>
          <div formArrayName="lines">
            @for (l of lines.controls; track $index) {
              <div class="line" [formGroupName]="$index">
                <nz-select formControlName="accountId" nzShowSearch nzPlaceHolder="Account" [attr.data-testid]="'j-account-' + $index">@for (a of accounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
                <nz-input-number formControlName="debit" [nzMin]="0" [nzPrecision]="2" nzPlaceHolder="Debit" [attr.data-testid]="'j-debit-' + $index"></nz-input-number>
                <nz-input-number formControlName="credit" [nzMin]="0" [nzPrecision]="2" nzPlaceHolder="Credit" [attr.data-testid]="'j-credit-' + $index"></nz-input-number>
                <input nz-input formControlName="description" placeholder="Description" />
                @if (editable()) { <button nz-button nzType="text" type="button" (click)="removeLine($index)"><span nz-icon nzType="delete"></span></button> }
              </div>
            }
          </div>
          @if (editable()) { <button nz-button type="button" (click)="addLine()" data-testid="j-add-line"><span nz-icon nzType="plus"></span>Add line</button> }
          <div class="totals" data-testid="j-totals">
            <span>Debit <b>{{ totals().debit.toFixed(2) }}</b></span><span>Credit <b>{{ totals().credit.toFixed(2) }}</b></span>
            <span [class.bad]="totals().debit !== totals().credit">Difference <b>{{ (totals().debit - totals().credit).toFixed(2) }}</b></span>
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
        <nz-select [(ngModel)]="lockEntity" style="width:100%;margin-bottom:12px" nzPlaceHolder="Legal entity" data-testid="lock-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name + (e.booksLockedTo ? ' (closed to ' + e.booksLockedTo + ')' : '')"></nz-option> }</nz-select>
        <nz-date-picker [(ngModel)]="lockDate" style="width:100%" data-testid="lock-date"></nz-date-picker>
      </ng-container>
    </nz-modal>`,
})
export class JournalsPage {
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
  readonly columns: TableColumn<JournalSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'date', label: 'Date', sortField: 'date', width: '120px' },
    { key: 'memo', label: 'Memo' },
    { key: 'sourceType', label: 'Source', width: '110px' },
    { key: 'amount', label: 'Amount', sortField: 'amount', width: '130px' },
    { key: 'status', label: 'Status', width: '110px' },
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
  color(status: string): string { return STATUS_COLORS[status] ?? 'default'; }
  err(name: string): string { return controlError(this.form.get(name)); }

  private newLine(l?: { accountId?: string; debit?: number; credit?: number; description?: string }) {
    return this.fb.group({ accountId: [l?.accountId ?? '', Validators.required], debit: [l?.debit ?? 0], credit: [l?.credit ?? 0], description: [l?.description ?? ''] });
  }
  addLine(): void { this.lines.push(this.newLine() as never); }
  removeLine(i: number): void { if (this.lines.length > 2) this.lines.removeAt(i); }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions). */
  ngOnInit(): void {
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
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, date: toIsoDate(v.date!), memo: v.memo || null, reference: v.reference || null,
      lines: (v.lines as { accountId: string; debit: number; credit: number; description: string }[]).map((l) => ({ ...l, debit: l.debit ?? 0, credit: l.credit ?? 0, description: l.description || null })),
    };
    this.saving.set(true);
    this.api.save(this.journal()?.id ?? null, body).subscribe({
      next: (saved) => {
        if (!post) return this.done(saved, 'Draft saved');
        this.api.post(saved.id).subscribe({ next: (p) => this.done(p, `Posted as ${p.number}`), error: (e) => { this.saving.set(false); this.fail(e, saved); } });
      },
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  reverse(): void {
    const j = this.journal();
    if (!j) return;
    void askReason(this.modal, 'Reverse this journal?', 'Reverse', 'A reversing entry is posted today (or on the original date if that is later). The reason becomes its memo.', false).then((reason) => {
      if (reason === null) return;
      this.api.reverse(j.id, reason || undefined).subscribe({ next: (r) => { this.drawerOpen.set(false); this.message.success(`Reversed by ${r.number}`); this.list.reload(); }, error: (e) => this.fail(e) });
    });
  }

  remove(): void {
    const j = this.journal();
    if (!j) return;
    this.modal.confirm({
      nzTitle: 'Delete this draft journal?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(j.id).subscribe({ next: () => { this.drawerOpen.set(false); this.message.success('Draft deleted'); this.list.reload(); }, error: (e) => this.fail(e) }),
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
    this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.list.reload();
  }

  private fail(e: unknown, saved?: JournalDto): void {
    const p = asProblem(e);
    if (saved) this.journal.set(saved); // saved as a draft but could not post; keep it open for correction
    this.message.error(problemMessage(e, p?.detail ?? p?.title ?? 'Could not save the journal', 'memo'));
    if (saved) this.list.reload();
  }
}
