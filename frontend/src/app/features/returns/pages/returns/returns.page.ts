import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTableModule } from 'ng-zorro-antd/table';
import { Observable, map } from 'rxjs';
import { BillsApi, InvoicesApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { PurchaseOrdersApi } from '@core/api/purchasing.api';
import {
  PurchaseReturnDto, PurchaseReturnInput, PurchaseReturnsApi, PurchaseReturnSummaryDto, ReturnDetail, ReturnKind, ReturnSummary, SalesReturnDto, SalesReturnInput, SalesReturnsApi, SalesReturnSummaryDto,
} from '@core/api/returns.api';
import { SalesOrdersApi } from '@core/api/sales.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WarehousesApi } from '@core/api/inventory.api';
import { ActionFormState } from '@shared/utils/action-form-state';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedList } from '@core/api/paged-list';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { askReason } from '@shared/utils/reason-prompt';
import { FieldSpec, RecordDrawerComponent, SelectOption } from '@shared/components/record-drawer/record-drawer.component';
import { ReturnFormComponent, ReturnFormLine, ReturnFormValue } from '@shared/components/return-form/return-form.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

/** The document a return is raised against, with the lines that can still come back. */
export interface ReturnSource { type: 'order' | 'invoice' | 'bill'; id: string; label: string; lines: ReturnFormLine[]; }

type SourceType = ReturnSource['type'];
const manage = (kind: ReturnKind) => (kind === 'sales' ? 'sales.returns.manage' : 'purchasing.returns.manage');

/**
 * Sales returns (goods a customer sends back) and purchase returns (goods sent back to a vendor). One screen for both: the route says which.
 * Entry points on the order, invoice and bill pages open it with ?order=, ?invoice= or ?bill= and the form starts at once.
 */
@Component({
  selector: 'erp-returns',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, NzModalModule, NzPopconfirmModule, NzTableModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, ReturnFormComponent, StatusTagComponent],
  styleUrl: './returns.page.scss',
  templateUrl: './returns.page.html',
})
export class ReturnsPage {
  readonly kind: ReturnKind = inject(ActivatedRoute).snapshot.data['kind'] === 'purchase' ? 'purchase' : 'sales';
  readonly manageKey = manage(this.kind);
  readonly displayDate = displayDate;
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly salesApi = inject(SalesReturnsApi);
  private readonly purchaseApi = inject(PurchaseReturnsApi);
  private readonly salesOrders = inject(SalesOrdersApi);
  private readonly purchaseOrders = inject(PurchaseOrdersApi);
  private readonly invoices = inject(InvoicesApi);
  private readonly bills = inject(BillsApi);

  readonly list: PagedList<ReturnSummary> = createPagedList<ReturnSummary>((q) => this.api().list(q) as Observable<never>, { sort: '-returnDate' });
  readonly detail = signal<ReturnDetail | null>(null);
  readonly source = signal<ReturnSource | null>(null);
  readonly formOpen = signal(false);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly warehouses = signal<SelectOption[]>([]);
  private pickedType: SourceType = 'order';

  readonly columns: TableColumn<ReturnSummary>[] = this.kind === 'sales'
    ? [
      { key: 'number', label: 'Number', sortField: 'number', filter: false },
      { key: 'returnDate', label: 'Date', sortField: 'returnDate', filter: false, value: (r) => displayDate(r.returnDate) },
      { key: 'against', label: 'Against', filter: false, value: (r) => { const s = r as SalesReturnSummaryDto; return s.orderNumber ?? s.invoiceNumber ?? ''; } },
      { key: 'customerName', label: 'Customer', filter: false },
      { key: 'totalAmount', label: 'Amount', align: 'right', filter: false },
      { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['draft', 'posted', 'voided'] },
      { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
    ]
    : [
      { key: 'number', label: 'Number', sortField: 'number', filter: false },
      { key: 'returnDate', label: 'Date', sortField: 'returnDate', filter: false, value: (r) => displayDate(r.returnDate) },
      { key: 'against', label: 'Against', filter: false, value: (r) => (r as PurchaseReturnSummaryDto).purchaseOrderNumber ?? 'Bill' },
      { key: 'vendorName', label: 'Vendor', filter: false },
      { key: 'totalAmount', label: 'Amount', align: 'right', filter: false },
      { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['draft', 'posted', 'voided'] },
      { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
    ];

  readonly startFields: FieldSpec[] = [
    {
      key: 'sourceType', label: 'Return against', type: 'select', required: true, default: 'order',
      options: this.kind === 'sales' ? [{ value: 'order', label: 'Sales order' }, { value: 'invoice', label: 'Invoice' }] : [{ value: 'order', label: 'Purchase order' }, { value: 'bill', label: 'Vendor bill' }],
      onChange: (value) => { this.pickedType = (value as SourceType) ?? 'order'; return null; },
    },
    { key: 'sourceId', label: 'Document', type: 'lookup', required: true, lookup: (term) => this.lookupSource(term) },
  ];
  readonly start = new ActionFormState({
    run: (p) => this.loadSource((p['sourceType'] as SourceType) ?? 'order', String(p['sourceId'])),
    success: () => 'Choose what comes back',
    done: (s) => this.openForm(s as ReturnSource), message: this.message, failure: 'Could not load the document',
  });

  constructor() {
    const q = this.route.snapshot.queryParamMap;
    const target: [SourceType, string | null][] = [['order', q.get('order')], ['invoice', q.get('invoice')], ['bill', q.get('bill')]];
    const picked = target.find(([, id]) => !!id);
    if (picked) this.loadSource(picked[0], picked[1]!).subscribe({ next: (s) => this.openForm(s), error: (e) => this.message.error(problemMessage(e, 'Could not load the document')) });
    if (this.kind === 'purchase') {
      inject(WarehousesApi).list({ page: 1, pageSize: 100, sort: 'code' }).subscribe({ next: (r) => this.warehouses.set(r.items.filter((w) => w.isActive).map((w) => ({ value: w.id, label: `${w.code} · ${w.name}` }))), error: () => undefined });
    }
  }

  private api() { return this.kind === 'sales' ? this.salesApi : this.purchaseApi; }

  private lookupSource(term: string) {
    const label = (n: string, p?: string | null) => (p ? `${n} · ${p}` : n);
    const page = { page: 1, pageSize: 20, search: term };
    if (this.kind === 'sales') {
      return this.pickedType === 'invoice'
        ? this.invoices.list(page).pipe(map((r) => r.items.filter((i) => !i.isCreditNote).map((i) => ({ value: i.id, label: label(i.number, i.partyName) }))))
        : this.salesOrders.list(page).pipe(map((r) => r.items.map((o) => ({ value: o.id, label: label(o.number, o.customerName) }))));
    }
    return this.pickedType === 'bill'
      ? this.bills.list(page).pipe(map((r) => r.items.map((b) => ({ value: b.id, label: label(b.number, b.partyName) }))))
      : this.purchaseOrders.list(page).pipe(map((r) => r.items.map((o) => ({ value: o.id, label: label(o.number, o.vendorName) }))));
  }

  /** Loads the document and works out how much of each line can still come back. */
  loadSource(type: SourceType, id: string): Observable<ReturnSource> {
    const left = (n: number) => Math.max(0, Math.round(n * 10000) / 10000);
    if (this.kind === 'sales' && type === 'order') {
      return this.salesOrders.get(id).pipe(map((o) => ({ type, id, label: o.number, lines: o.lines.map((l) => ({ key: l.id, label: l.description, max: left(l.deliveredQuantity - (l.returnedQuantity ?? 0)), unit: l.unitOfMeasure })).filter((l) => l.max > 0) })));
    }
    if (this.kind === 'sales') {
      return this.invoices.detail(id).pipe(map((i) => ({ type: 'invoice' as const, id, label: i.number, lines: i.lines.filter((l) => !!l.id).map((l) => ({ key: l.id!, label: l.description, max: left(l.quantity) })) })));
    }
    if (type === 'order') {
      return this.purchaseOrders.get(id).pipe(map((o) => ({ type, id, label: o.number, lines: o.lines.map((l) => ({ key: l.id, label: l.description, max: left(l.receivedQuantity - (l.returnedQuantity ?? 0)), unit: l.unitOfMeasure })).filter((l) => l.max > 0) })));
    }
    return this.bills.detail(id).pipe(map((b) => ({ type: 'bill' as const, id, label: b.number, lines: b.lines.filter((l) => !!l.id).map((l) => ({ key: l.id!, label: l.description, max: left(l.quantity) })) })));
  }

  private openForm(s: ReturnSource): void {
    if (!s.lines.length) { this.message.warning('Nothing on this document can be returned.'); return; }
    this.formError.set(null);
    this.source.set(s);
    this.formOpen.set(true);
  }

  create(v: ReturnFormValue): void {
    const s = this.source();
    if (!s) return;
    this.saving.set(true);
    this.formError.set(null);
    let call: Observable<ReturnSummary>;
    if (this.kind === 'sales') {
      const body: SalesReturnInput = {
        salesOrderId: s.type === 'order' ? s.id : null, invoiceId: s.type === 'invoice' ? s.id : null, returnDate: v.returnDate, reason: v.reason, notes: v.notes,
        lines: v.lines.map((l) => ({ orderLineId: s.type === 'order' ? l.key : null, invoiceLineId: s.type === 'invoice' ? l.key : null, quantity: l.quantity, restock: l.restock })),
      };
      call = this.salesApi.create(body);
    } else {
      const body: PurchaseReturnInput = {
        purchaseOrderId: s.type === 'order' ? s.id : null, billId: s.type === 'bill' ? s.id : null, returnDate: v.returnDate, reason: v.reason, notes: v.notes, warehouseId: v.warehouseId,
        lines: v.lines.map((l) => ({ orderLineId: s.type === 'order' ? l.key : null, billLineId: s.type === 'bill' ? l.key : null, quantity: l.quantity })),
      };
      call = this.purchaseApi.create(body);
    }
    call.subscribe({
      next: (r) => { this.saving.set(false); this.formOpen.set(false); this.message.success(doneText('created', this.kind === 'sales' ? 'Sales return' : 'Purchase return', r.number)); this.list.reload(); },
      error: (e) => { this.saving.set(false); this.formError.set(problemMessage(e, 'Could not create the return')); },
    });
  }

  show(row: ReturnSummary): void { (this.api().detail(row.id) as Observable<ReturnDetail>).subscribe({ next: (d) => this.detail.set(d), error: (e) => this.message.error(problemMessage(e, 'Could not load the return')) }); }

  post(row: ReturnSummary): void {
    (this.api().post(row.id) as Observable<ReturnDetail>).subscribe({
      next: () => { this.message.success(doneText('posted', this.kind === 'sales' ? 'Sales return' : 'Purchase return', row.number)); this.list.reload(); },
      error: (e) => this.message.error(problemMessage(e, 'Could not post the return')),
    });
  }

  voidIt(row: ReturnSummary): void {
    askReason(this.modal, 'Void this return?', 'Void return', 'This reverses its entries and puts stock and balances back as they were. It is refused once the credit note is applied or the goods have been used.').then((reason) => {
      if (!reason) return;
      (this.api().void(row.id, reason) as Observable<ReturnDetail>).subscribe({
        next: () => { this.message.success(doneText('voided', this.kind === 'sales' ? 'Sales return' : 'Purchase return', row.number)); this.list.reload(); },
        error: (e) => this.message.error(problemMessage(e, 'Could not void the return', 'reason')),
      });
    });
  }

  remove(row: ReturnSummary): void {
    this.api().delete(row.id).subscribe({
      next: () => { this.message.success(doneText('deleted', this.kind === 'sales' ? 'Sales return' : 'Purchase return', row.number)); this.list.reload(); },
      error: (e) => this.message.error(problemMessage(e, 'Could not delete the return')),
    });
  }

  partyName(d: ReturnDetail): string { return 'customerName' in d ? d.customerName : d.vendorName; }
  salesDetail(d: ReturnDetail): SalesReturnDto | null { return 'customerName' in d ? d : null; }
  purchaseDetail(d: ReturnDetail): PurchaseReturnDto | null { return 'vendorName' in d ? d : null; }
}
