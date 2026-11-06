import { doneText } from '../../core/format/done-text';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { catchError, map, of } from 'rxjs';
import { AccountsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import { API_BASE_URL } from '../../core/api/api-config';
import { createPagedList } from '../../core/api/paged-list';
import { EMPTY_GUID, PosRegisterDto, PosRegisterInput, PosRegistersApi } from '../../core/api/pos.api';
import { problemMessage } from '../../core/http/problem-details';
import { ErpDatePipe } from '../../shared/display.pipes';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

interface Option { value: string; label: string; }

/** The tills: where each one sells from, which accounts its money goes to, and the wording on its receipts. */
@Component({
  selector: 'erp-pos-registers',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No registers yet. Add one to start selling." exportFilename="pos-registers" (rowClick)="onRow($event)">
      <button toolbar *erpHasPermission="'pos.registers.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="pos-register-new"><span nz-icon nzType="plus"></span>New register</button>
      <ng-template #cell let-reg let-column="column" let-value="value">
        @if (column.key === 'code') {
          <strong class="nowrap">{{ reg.code }}</strong>
        } @else if (column.key === 'warehouse') {
          {{ warehouseName(reg.warehouseId) || 'Default' }}
        } @else if (column.key === 'receiptHeader') {
          <span class="preview" [title]="reg.receiptHeader || ''">{{ reg.receiptHeader || '-' }}</span>
        } @else if (column.key === 'isActive') {
          <erp-status [value]="reg.isActive ? 'active' : 'inactive'" />
        } @else if (column.key === 'createdAt') {
          <span class="nowrap">{{ reg.createdAt | erpDate }}</span>
        } @else if (column.key === 'actions') {
          <button *erpHasPermission="'pos.registers.manage'" nz-button nzType="link" nzDanger nzSize="small" (click)="remove(reg); $event.stopPropagation()" [attr.data-testid]="'pos-register-delete-' + reg.code">Delete</button>
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>

    <erp-record-drawer [title]="state.editing() ? 'Edit register' : 'New register'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
      [saving]="state.saving()" [errors]="state.errors()" testId="pos-register-form" (submitted)="save($event)" (closed)="state.close()" />
  `,
  styles: [`
    .nowrap { white-space: nowrap; }
    .preview { display: block; max-width: 280px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  `],
})
export class PosRegistersPage {
  private readonly api = inject(PosRegistersApi);
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly list = createPagedList<PosRegisterDto>((q: any) => this.api.list(q));

  private readonly entities = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 50 }).pipe(
    map((r: any) => (r.items as { id: string; name: string }[]).map((e) => ({ value: e.id, label: e.name }))),
    catchError(() => of([] as Option[])),
  ), { initialValue: [] as Option[] });

  // Whoever may not read warehouses or accounts still gets the rest of the form.
  private readonly warehouses = toSignal(this.http.get<{ items: { id: string; code: string; name: string; isActive: boolean }[] }>(`${this.base}/m/inventory/warehouses`, { params: { pageSize: 200 } }).pipe(
    map((r) => r.items.filter((w) => w.isActive).map((w) => ({ value: w.id, label: `${w.code} · ${w.name}` }))),
    catchError(() => of([] as Option[])),
  ), { initialValue: [] as Option[] });

  private readonly accounts = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200 }).pipe(
    map((r) => r.items.filter((a) => a.isPostable && a.isActive).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}`, type: a.type }))),
    catchError(() => of([] as (Option & { type: string })[])),
  ), { initialValue: [] as (Option & { type: string })[] });

  readonly columns: TableColumn<PosRegisterDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'warehouse', label: 'Warehouse', filter: false, value: (r) => this.warehouseName(r.warehouseId) },
    { key: 'receiptHeader', label: 'Receipt header', filter: false },
    { key: 'isActive', label: 'Status', filterField: 'isActive', filter: false, value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'createdAt', label: 'Created', sortField: 'createdAt' },
    { key: 'actions', label: 'Actions', filter: false },
  ];

  readonly fields = computed<FieldSpec[]>(() => {
    const assets = this.accounts().filter((a) => a.type?.toLowerCase() === 'asset');
    const all = this.accounts();
    return [
      { key: 'code', label: 'Register code', type: 'text', required: true, maxLength: 50, readonlyOnEdit: true },
      { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150, hint: 'What cashiers see, such as Front counter 1.' },
      { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: this.entities(), default: this.entities()[0]?.value, readonlyOnEdit: true },
      { key: 'warehouseId', label: 'Warehouse', type: 'select', searchable: true, options: this.warehouses(), hint: 'Sales take stock from here. Empty uses the first warehouse of the legal entity.' },
      { key: 'defaultCashAccountId', label: 'Cash account', type: 'select', searchable: true, options: assets, hint: 'Empty uses Cash on hand.' },
      { key: 'cardAccountId', label: 'Card takings account', type: 'select', searchable: true, options: assets, hint: 'Empty uses the bank account.' },
      { key: 'varianceAccountId', label: 'Cash over and short account', type: 'select', searchable: true, options: all, hint: 'Takes what is over or short when a shift closes. Empty uses general expenses.' },
      { key: 'expenseAccountId', label: 'Pay-out account', type: 'select', searchable: true, options: all, hint: 'The other side of cash paid out of the drawer. Empty uses general expenses.' },
      { key: 'receiptHeader', label: 'Receipt header', type: 'textarea', maxLength: 500 },
      { key: 'receiptFooter', label: 'Receipt footer', type: 'textarea', maxLength: 500 },
      { key: 'isActive', label: 'Active', type: 'switch', default: true },
    ];
  });

  readonly state = new MasterDataState<PosRegisterDto>({
    save: (id, body) => (id ? this.api.update(id, body as PosRegisterInput) : this.api.create(body as PosRegisterInput)),
    reload: () => this.list.reload(),
    message: this.message,
    entityName: 'Register',
  });

  warehouseName(id?: string | null): string {
    return (id && this.warehouses().find((w) => w.value === id)?.label.split(' · ')[0]) || '';
  }

  onRow(reg: PosRegisterDto): void {
    this.state.edit(reg);
  }

  /** On the server an account left out keeps its value, so clearing one here sends the empty GUID. */
  save(values: Record<string, unknown>): void {
    const body = { ...values };
    for (const key of ['cardAccountId', 'varianceAccountId', 'expenseAccountId']) {
      if (!body[key] && this.state.editing()) body[key] = EMPTY_GUID;
    }
    this.state.submit(body);
  }

  remove(reg: PosRegisterDto): void {
    this.modal.confirm({
      nzTitle: `Delete ${reg.name}?`,
      nzContent: 'Its past shifts and sales stay on record. A register with an open shift cannot be deleted.',
      nzOkText: 'Delete',
      nzOkDanger: true,
      nzOnOk: () => this.api.delete(reg.id).subscribe({
        next: () => {
          this.list.reload();
          this.message.success(doneText('deleted', 'Register', reg.name));
        },
        error: (err) => this.message.error(problemMessage(err, 'The register could not be deleted.')),
      }),
    });
  }
}
