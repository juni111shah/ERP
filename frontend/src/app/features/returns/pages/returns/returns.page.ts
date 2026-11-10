import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTableModule } from 'ng-zorro-antd/table';
import { Observable } from 'rxjs';
import { WarehousesApi } from '@core/api/inventory.api';
import { PagedList, createPagedList } from '@core/api/paged-list';
import {
  PurchaseReturnDto, PurchaseReturnSummaryDto, PurchaseReturnsApi, ReturnDetail, ReturnKind, ReturnSummary, ReturnsApiLike, SalesReturnDto, SalesReturnSummaryDto, SalesReturnsApi,
} from '@core/api/returns.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent, SelectOption } from '@shared/components/record-drawer/record-drawer.component';
import { ReturnFormComponent, ReturnFormValue } from '@shared/components/return-form/return-form.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ActionFormState } from '@shared/utils/action-form-state';
import { loadInto } from '@shared/utils/load-into';
import { askReason } from '@shared/utils/reason-prompt';
import { ReturnSource, ReturnSourceType, ReturnSources, purchaseReturnBody, salesReturnBody } from '../../returns-sources';

export type { ReturnSource } from '../../returns-sources';

const manage = (kind: ReturnKind) => (kind === 'sales' ? 'sales.returns.manage' : 'purchasing.returns.manage');

/** The sales returns table. */
const salesColumns = (): TableColumn<ReturnSummary>[] => [
  { key: 'number', label: 'Number', sortField: 'number', filter: false },
  { key: 'returnDate', label: 'Date', sortField: 'returnDate', filter: false, value: (r) => displayDate(r.returnDate) },
  { key: 'against', label: 'Against', filter: false, value: (r) => { const s = r as SalesReturnSummaryDto; return s.orderNumber ?? s.invoiceNumber ?? ''; } },
  { key: 'customerName', label: 'Customer', filter: false },
  { key: 'totalAmount', label: 'Amount', align: 'right', filter: false },
  { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['draft', 'posted', 'voided'] },
  { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
];

/** The purchase returns table. */
const purchaseColumns = (): TableColumn<ReturnSummary>[] => [
  { key: 'number', label: 'Number', sortField: 'number', filter: false },
  { key: 'returnDate', label: 'Date', sortField: 'returnDate', filter: false, value: (r) => displayDate(r.returnDate) },
  { key: 'against', label: 'Against', filter: false, value: (r) => (r as PurchaseReturnSummaryDto).purchaseOrderNumber ?? 'Bill' },
  { key: 'vendorName', label: 'Vendor', filter: false },
  { key: 'totalAmount', label: 'Amount', align: 'right', filter: false },
  { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['draft', 'posted', 'voided'] },
  { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
];

/**
 * Sales returns (goods a customer sends back) and purchase returns (goods sent back to a vendor). One screen for both: the route says which.
 * Entry points on the order, invoice and bill pages open it with ?order=, ?invoice= or ?bill= and the form starts at once.
 */
@Component({
  selector: 'erp-returns',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, NzModalModule, NzPopconfirmModule, NzTableModule, PagedTableComponent, RecordDrawerComponent, ReturnFormComponent, StatusTagComponent, HasPermissionDirective],
  providers: [ReturnSources],
  styleUrl: './returns.page.scss',
  templateUrl: './returns.page.html',
})
export class ReturnsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly salesApi = inject(SalesReturnsApi);
  private readonly purchaseApi = inject(PurchaseReturnsApi);
  private readonly sources = inject(ReturnSources);
  private pickedType: ReturnSourceType = 'order';

  readonly kind: ReturnKind = this.route.snapshot.data['kind'] === 'purchase' ? 'purchase' : 'sales';
  readonly manageKey = manage(this.kind);
  readonly displayDate = displayDate;
  readonly list: PagedList<ReturnSummary> = createPagedList<ReturnSummary>((q) => this.api().list(q), { sort: '-returnDate' });
  readonly detail = signal<ReturnDetail | null>(null);
  readonly source = signal<ReturnSource | null>(null);
  readonly formOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly warehouses = signal<SelectOption[]>([]);
  readonly columns: TableColumn<ReturnSummary>[] = this.kind === 'sales' ? salesColumns() : purchaseColumns();

  readonly startFields: FieldSpec[] = [
    {
      key: 'sourceType', label: 'Return against', type: 'select', required: true, default: 'order',
      options: this.kind === 'sales' ? [{ value: 'order', label: 'Sales order' }, { value: 'invoice', label: 'Invoice' }] : [{ value: 'order', label: 'Purchase order' }, { value: 'bill', label: 'Vendor bill' }],
      onChange: (value) => { this.pickedType = (value as ReturnSourceType) ?? 'order'; return null; },
    },
    { key: 'sourceId', label: 'Document', type: 'lookup', required: true, lookup: (term) => this.sources.search(this.kind, this.pickedType, term) },
  ];
  readonly start = new ActionFormState({
    run: (p) => this.loadSource((p['sourceType'] as ReturnSourceType) ?? 'order', String(p['sourceId'])),
    success: () => 'Choose what comes back',
    done: (s) => this.openForm(s as ReturnSource), message: this.message, failure: 'Could not load the document',
  });

  constructor() {
    const q = this.route.snapshot.queryParamMap;
    const target: [ReturnSourceType, string | null][] = [['order', q.get('order')], ['invoice', q.get('invoice')], ['bill', q.get('bill')]];
    const picked = target.find(([, id]) => !!id);
    if (picked) this.loadSource(picked[0], picked[1]!).subscribe({ next: (s) => this.openForm(s), error: (e) => this.message.error(problemMessage(e, 'Could not load the document')) });
    if (this.kind === 'purchase') {
      loadInto(this.warehouses, inject(WarehousesApi).list({ page: 1, pageSize: 100, sort: 'code' }), (r) => r.items.filter((w) => w.isActive).map((w) => ({ value: w.id, label: `${w.code} · ${w.name}` })));
    }
  }

  /** Loads the document and works out how much of each line can still come back. */
  loadSource(type: ReturnSourceType, id: string): Observable<ReturnSource> { return this.sources.load(this.kind, type, id); }

  create(v: ReturnFormValue): void {
    const s = this.source();
    if (!s) return;
    this.saving.set(true);
    this.formError.set(null);
    const call: Observable<ReturnSummary> = this.kind === 'sales' ? this.salesApi.create(salesReturnBody(s, v)) : this.purchaseApi.create(purchaseReturnBody(s, v));
    call.subscribe({
      next: (r) => { this.saving.set(false); this.formOpen.set(false); this.message.success(doneText('created', this.noun(), r.number)); this.list.reload(); },
      error: (e) => { this.saving.set(false); this.formError.set(problemMessage(e, 'Could not create the return')); },
    });
  }

  show(row: ReturnSummary): void { this.api().detail(row.id).subscribe({ next: (d) => this.detail.set(d), error: (e) => this.message.error(problemMessage(e, 'Could not load the return')) }); }

  post(row: ReturnSummary): void {
    this.api().post(row.id).subscribe({
      next: () => { this.message.success(doneText('posted', this.noun(), row.number)); this.list.reload(); },
      error: (e) => this.message.error(problemMessage(e, 'Could not post the return')),
    });
  }

  voidIt(row: ReturnSummary): void {
    askReason(this.modal, 'Void this return?', 'Void return', 'This reverses its entries and puts stock and balances back as they were. It is refused once the credit note is applied or the goods have been used.').then((reason) => {
      if (!reason) return;
      this.api().void(row.id, reason).subscribe({
        next: () => { this.message.success(doneText('voided', this.noun(), row.number)); this.list.reload(); },
        error: (e) => this.message.error(problemMessage(e, 'Could not void the return', 'reason')),
      });
    });
  }

  remove(row: ReturnSummary): void {
    this.api().delete(row.id).subscribe({
      next: () => { this.message.success(doneText('deleted', this.noun(), row.number)); this.list.reload(); },
      error: (e) => this.message.error(problemMessage(e, 'Could not delete the return')),
    });
  }

  partyName(d: ReturnDetail): string { return 'customerName' in d ? d.customerName : d.vendorName; }
  salesDetail(d: ReturnDetail): SalesReturnDto | null { return 'customerName' in d ? d : null; }
  purchaseDetail(d: ReturnDetail): PurchaseReturnDto | null { return 'vendorName' in d ? d : null; }

  private api(): ReturnsApiLike { return this.kind === 'sales' ? this.salesApi : this.purchaseApi; }
  private noun(): string { return this.kind === 'sales' ? 'Sales return' : 'Purchase return'; }

  private openForm(s: ReturnSource): void {
    if (!s.lines.length) { this.message.warning('Nothing on this document can be returned.'); return; }
    this.formError.set(null);
    this.source.set(s);
    this.formOpen.set(true);
  }
}
