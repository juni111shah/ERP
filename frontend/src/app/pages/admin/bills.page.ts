import { ActivatedRoute, Router } from '@angular/router';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '../../shared/reason-prompt';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { AccountDto, AccountsApi, BillDto, BillSummary, BillsApi, LegalEntitiesApi, LegalEntityDto, TaxCodeDto, TaxCodesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { asProblem, controlError } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { PartiesApi } from '../parties.page';

const STATUS_COLORS: Record<string, string> = { draft: 'default', pendingApproval: 'purple', issued: 'blue', partlyPaid: 'gold', paid: 'green', voided: 'red' };
const STATUS_LABELS: Record<string, string> = { partlyPaid: 'Part paid', pendingApproval: 'Needs approval', issued: 'Posted' };

/** Vendor bills: draft with a line editor, post to the ledger (after approval when a rule applies), void. */
@Component({
  selector: 'erp-bills',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzModalModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule, NzInputNumberModule, NzSelectModule, NzTagModule],
  styles: [`.line { display:grid; grid-template-columns: 2fr 1.6fr .7fr .9fr 1.1fr auto; gap:8px; margin-bottom:8px; align-items:center } .totals { display:flex; gap:24px; justify-content:flex-end; margin:8px 0 }`],
  template: `
    <h1 class="erp-page-title">Bills</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, vendor or reference" emptyText="No bills yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <nz-tag [nzColor]="color(row.status)">{{ label(row.status) }}</nz-tag> } @else { {{ value }} }
      </ng-template>
      <button toolbar *erpHasPermission="'finance.bills.write'" nz-button nzType="primary" (click)="open(null)" data-testid="bill-new"><span nz-icon nzType="plus"></span>New bill</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="940" [nzTitle]="bill() ? 'Bill ' + bill()!.number : 'New bill'" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="bill-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="bill-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Vendor</nz-form-label><nz-form-control [nzErrorTip]="err('partyId')">
            <nz-select formControlName="partyId" nzShowSearch nzServerSearch (nzOnSearch)="searchParties($event)" data-testid="bill-party">@for (o of partyOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Vendor reference</nz-form-label><nz-form-control [nzErrorTip]="err('vendorReference')"><input nz-input formControlName="vendorReference" data-testid="bill-ref" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Bill date</nz-form-label><nz-form-control [nzErrorTip]="err('billDate')"><nz-date-picker formControlName="billDate" style="width:100%" data-testid="bill-date"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Due date</nz-form-label><nz-form-control [nzErrorTip]="err('dueDate')" nzExtra="Leave empty to use the vendor's payment terms."><nz-date-picker formControlName="dueDate" style="width:100%"></nz-date-picker></nz-form-control></nz-form-item>
          <h4>Lines</h4>
          <div formArrayName="lines">
            @for (l of lines.controls; track $index) {
              <div class="line" [formGroupName]="$index">
                <input nz-input formControlName="description" placeholder="Description" [attr.data-testid]="'bill-desc-' + $index" />
                <nz-select formControlName="accountId" nzShowSearch nzPlaceHolder="Expense account" [attr.data-testid]="'bill-account-' + $index">@for (a of accounts(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
                <nz-input-number formControlName="quantity" [nzMin]="0.0001" [nzPrecision]="2"></nz-input-number>
                <nz-input-number formControlName="price" [nzMin]="0" [nzPrecision]="2" [attr.data-testid]="'bill-price-' + $index"></nz-input-number>
                <nz-select formControlName="taxCodeId" nzAllowClear nzPlaceHolder="No tax">@for (t of taxes(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.code + ' ' + t.rate + '%'"></nz-option> }</nz-select>
                @if (editable()) { <button nz-button nzType="text" type="button" (click)="removeLine($index)"><span nz-icon nzType="delete"></span></button> }
              </div>
            }
          </div>
          @if (editable()) { <button nz-button type="button" (click)="addLine()" data-testid="bill-add-line"><span nz-icon nzType="plus"></span>Add line</button> }
          <div class="totals"><span>Net <b>{{ totals().net.toFixed(2) }}</b></span><span>Tax <b>{{ totals().tax.toFixed(2) }}</b></span><span>Total <b>{{ (totals().net + totals().tax).toFixed(2) }}</b></span></div>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Close</button>
      @if (bill()?.id) { <button nz-button (click)="openDetails(bill()!.id)" style="margin-inline-start:8px" data-testid="bill-details"><span nz-icon nzType="file-text"></span>Details</button><button nz-button (click)="printDoc(bill()!.id)" style="margin-inline-start:8px" data-testid="bill-print"><span nz-icon nzType="printer"></span>Print / PDF</button> }
      @if (bill()?.status === 'issued' && bill()!.paid === 0) { <button *erpHasPermission="'finance.bills.post'" nz-button nzDanger (click)="void()" style="margin-inline-start:8px" data-testid="bill-void">Void</button> }
      @if (editable()) {
        @if (bill()?.id) { <button *erpHasPermission="'finance.bills.write'" nz-button nzDanger (click)="remove()" style="margin-inline-start:8px" data-testid="bill-delete">Delete</button> }
        <button *erpHasPermission="'finance.bills.write'" nz-button [nzLoading]="saving()" (click)="save(false)" style="margin-inline-start:8px" data-testid="bill-save">Save draft</button>
        <button *erpHasPermission="'finance.bills.post'" nz-button nzType="primary" [nzLoading]="saving()" (click)="save(true)" style="margin-inline-start:8px" data-testid="bill-post">Save and post</button>
      }
    </ng-template>`,
})
export class BillsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(BillsApi);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly taxPage = toSignal(inject(TaxCodesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as TaxCodeDto[] }))), { initialValue: { items: [] as TaxCodeDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly taxes = computed(() => this.taxPage().items.filter((t) => t.isActive));
  readonly accounts = computed(() => this.accountPage().items.filter((a) => a.isPostable && a.isActive && (a.type === 'expense' || a.type === 'asset')));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-billDate' });
  readonly columns: TableColumn<BillSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'partyName', label: 'Vendor' },
    { key: 'vendorReference', label: 'Reference', width: '140px' },
    { key: 'billDate', label: 'Date', sortField: 'billDate', width: '120px' },
    { key: 'dueDate', label: 'Due', sortField: 'dueDate', width: '120px' },
    { key: 'total', label: 'Total', sortField: 'total', width: '110px' },
    { key: 'outstanding', label: 'Outstanding', width: '110px' },
    { key: 'status', label: 'Status', width: '130px' },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly bill = signal<BillDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], vendorReference: [''], billDate: [null as Date | null, Validators.required], dueDate: [null as Date | null],
    lines: this.fb.array<unknown>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  readonly totals = computed(() => {
    const lines = (this.value().lines ?? []) as { quantity?: number | null; price?: number | null; taxCodeId?: string | null }[];
    const rate = (id?: string | null) => this.taxes().find((t) => t.id === id)?.rate ?? 0;
    const net = lines.reduce((s, l) => s + Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100, 0);
    const tax = lines.reduce((s, l) => s + Math.round(Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100 * rate(l.taxCodeId)) / 100, 0);
    return { net, tax };
  });
  readonly editable = computed(() => !this.bill() || this.bill()!.status === 'draft' || this.bill()!.status === 'pendingApproval');

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  color(s: string): string { return STATUS_COLORS[s] ?? 'default'; }
  label(s: string): string { return STATUS_LABELS[s] ?? s.charAt(0).toUpperCase() + s.slice(1); }
  err(name: string): string { return controlError(this.form.get(name)); }

  private newLine(l?: Partial<{ description: string; accountId: string; quantity: number; price: number; taxCodeId: string }>) {
    return this.fb.group({ description: [l?.description ?? '', Validators.required], accountId: [l?.accountId ?? '', Validators.required], quantity: [l?.quantity ?? 1], price: [l?.price ?? 0], taxCodeId: [l?.taxCodeId ?? null as string | null] });
  }
  addLine(): void { this.lines.push(this.newLine() as never); }
  removeLine(i: number): void { if (this.lines.length > 1) this.lines.removeAt(i); }

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => this.partyOptions.set(r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))));
  }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions). */
  ngOnInit(): void {
    const id = this.route.snapshot.queryParamMap.get('open');
    if (id) this.open({ id } as never);
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/bills', id]); }
  printDoc(id: string): void { window.open(`/print/bill/${id}`, '_blank'); }

  open(row: BillSummary | null): void {
    if (!row) { this.show(null); return; }
    this.api.detail(row.id).subscribe((b) => this.show(b));
  }

  private show(b: BillDto | null): void {
    this.bill.set(b);
    this.lines.clear();
    for (const l of b?.lines ?? [{}]) this.lines.push(this.newLine(l) as never);
    const entities = this.entities();
    this.form.patchValue({
      legalEntityId: b?.legalEntityId ?? (entities.length === 1 ? entities[0].id : ''), partyId: b?.partyId ?? '', vendorReference: b?.vendorReference ?? '',
      billDate: b ? new Date(b.billDate + 'T00:00:00') : new Date(), dueDate: b ? new Date(b.dueDate + 'T00:00:00') : null,
    });
    this.partyOptions.set(b ? [{ value: b.partyId, label: b.partyName ?? b.partyId }] : []);
    if (!b) this.searchParties('');
    this.editable() ? this.form.enable() : this.form.disable();
    this.drawerOpen.set(true);
  }

  save(post: boolean): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, partyId: v.partyId, vendorReference: v.vendorReference || null, billDate: toIsoDate(v.billDate!), dueDate: v.dueDate ? toIsoDate(v.dueDate) : null,
      lines: (v.lines as { description: string; accountId: string; quantity: number; price: number; taxCodeId: string | null }[]).map((l) => ({ ...l, taxCodeId: l.taxCodeId || null })),
    };
    this.saving.set(true);
    this.api.save(this.bill()?.id ?? null, body).subscribe({
      next: (saved) => {
        if (!post) { this.done('Draft saved'); return; }
        this.api.post(saved.id).subscribe({
          next: (b) => this.done(b.status === 'pendingApproval' ? 'Sent for approval. Post it again once it is approved.' : `Posted as ${b.number}`),
          error: (e) => { this.saving.set(false); this.bill.set(saved); this.list.reload(); this.fail(e); },
        });
      },
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  void(): void {
    const b = this.bill();
    if (!b) return;
    // Same required-reason prompt as the detail page; the void endpoint takes no body, so the reason is not sent.
    void askReason(this.modal, 'Void this bill?', 'Void Bill', 'This cannot be undone. The bill will be marked as voided.').then((reason) => {
      if (reason) this.api.void(b.id).subscribe({ next: () => this.done('Bill voided'), error: (e) => this.fail(e) });
    });
  }

  remove(): void {
    const b = this.bill();
    if (!b) return;
    this.modal.confirm({
      nzTitle: 'Delete this draft bill?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(b.id).subscribe({ next: () => this.done('Draft deleted'), error: (e) => this.fail(e) }),
    });
  }

  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.list.reload(); }
  private fail(e: unknown): void { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save the bill'); }
}
