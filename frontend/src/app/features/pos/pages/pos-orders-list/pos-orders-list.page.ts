import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { catchError, forkJoin, of } from 'rxjs';
import { createPagedList } from '@core/api/paged-list';
import { PosOrderDto, PosOrdersApi, PosOrderSummaryDto, PosRegisterDto, PosRegistersApi, PosReturnsApi, PosReturnSummaryDto } from '@core/api/pos.api';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { askReason } from '@shared/utils/reason-prompt';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ThermalReceiptComponent, ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';
import { TENDER_LABELS } from '../../pos-cart';
import { saleReceipt } from '../../pos-receipt';

@Component({
  selector: 'erp-pos-orders-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatusTagComponent, DecimalPipe, RouterModule, NzAlertModule, NzButtonModule, NzDrawerModule, NzIconModule, NzModalModule, PagedTableComponent, HasPermissionDirective, ThermalReceiptComponent],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search receipt number, customer, cashier" emptyText="No counter sales yet. Open the terminal to make one." exportFilename="pos-orders" (rowClick)="open($event.id)">
      <a toolbar *erpHasPermission="'pos.orders.create'" routerLink="/pos/terminal" nz-button nzType="primary" data-testid="pos-open-terminal"><span nz-icon nzType="shop"></span>Open Terminal</a>
      <ng-template #cell let-ord let-column="column" let-value="value">
        @if (column.key === 'number') {
          <strong class="nowrap">{{ ord.number }}</strong>
        } @else if (column.key === 'orderDate') {
          <span class="nowrap">{{ ord.orderDate | erpDate }}</span>
        } @else if (column.key === 'lineCount') {
          {{ ord.lineCount }} {{ ord.lineCount === 1 ? 'item' : 'items' }}
        } @else if (column.key === 'totalAmount') {
          <strong class="amount">{{ currency() }} {{ ord.totalAmount | number:'1.2-2' }}</strong>
        } @else if (column.key === 'returnedTotal') {
          @if (ord.returnedTotal > 0) { <span class="amount returned">-{{ currency() }} {{ ord.returnedTotal | number:'1.2-2' }}</span> }
        } @else if (column.key === 'status') {
          <erp-status [value]="ord.status" />
        } @else if (column.key === 'paymentStatus') {
          <erp-status [value]="ord.paymentStatus" />
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzTitle]="order()?.number ?? 'Sale'" [nzWidth]="480" (nzOnClose)="close()" [nzFooter]="drawerFooter">
      <ng-container *nzDrawerContent>
        @if (loadFailed()) {
          <nz-alert nzType="error" nzShowIcon nzMessage="This sale could not be loaded."></nz-alert>
        } @else if (order(); as o) {
          <div class="tags" data-testid="pos-order-detail">
            <erp-status [value]="o.status" />
            <erp-status [value]="o.paymentStatus" />
            @if (o.invoiceNumber) { <a class="doc" [routerLink]="['/finance/invoices', o.invoiceId]" data-testid="pos-order-invoice">Invoice {{ o.invoiceNumber }}</a> }
          </div>
          @if (o.returnedTotal > 0) {
            <nz-alert nzType="info" nzShowIcon [nzMessage]="'Returned so far: ' + currency() + ' ' + (o.returnedTotal | number:'1.2-2')" class="block"></nz-alert>
          }
          @if (o.notes) { <nz-alert nzType="warning" [nzMessage]="o.notes" class="block" data-testid="pos-order-notes"></nz-alert> }
          @if (receiptData(); as rc) { <erp-thermal-receipt [receipt]="rc" /> }
          @if (returnsOfOrder().length) {
            <h4>Returns</h4>
            @for (r of returnsOfOrder(); track r.id) {
              <div class="ret">
                <a [href]="'/print/pos-return/' + r.id" target="_blank" rel="noopener"><strong>{{ r.number }}</strong></a>
                <span class="muted">{{ r.returnDate | erpDate }} · {{ r.reason }}</span>
                <strong class="amount">-{{ currency() }} {{ r.totalAmount | number:'1.2-2' }}</strong>
              </div>
            }
          }
          @if (o.lines.length && hasPayments()) {
            <h4>Payment</h4>
            @for (p of o.payments; track p.id) {
              <div class="ret">
                <span>{{ tender(p.method) }} @if (p.reference) { <small class="muted">{{ p.reference }}</small> }</span>
                <strong class="amount">{{ currency() }} {{ p.amount | number:'1.2-2' }}</strong>
              </div>
            }
          }
        }
      </ng-container>
      <ng-template #drawerFooter>
        @if (order(); as o) {
          <div class="foot">
            <a [href]="'/print/pos-receipt/' + o.id" target="_blank" rel="noopener" nz-button data-testid="pos-order-print"><span nz-icon nzType="printer"></span>Receipt</a>
            @if (canReturn()) {
              <a *erpHasPermission="'pos.orders.refund'" nz-button [routerLink]="['/pos/terminal']" [queryParams]="{ return: o.number }" data-testid="pos-order-return"><span nz-icon nzType="rollback"></span>Return</a>
            }
            @if (o.status === 'completed' && !o.returnedTotal) {
              <button *erpHasPermission="'pos.orders.void'" nz-button nzDanger type="button" [nzLoading]="voiding()" (click)="voidOrder()" data-testid="pos-order-void"><span nz-icon nzType="stop"></span>Void</button>
            }
          </div>
        }
      </ng-template>
    </nz-drawer>
  `,
  styles: [`
    .nowrap { white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .returned { color: var(--erp-color-error-text); }
    .tags { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-bottom: 12px; }
    .doc { font-weight: 600; }
    .block { display: block; margin-bottom: 12px; }
    h4 { margin: 12px 0 6px; font-size: 13px; font-weight: 700; }
    .ret { display: flex; align-items: baseline; gap: 10px; padding: 5px 0; border-top: 1px solid var(--erp-color-border-subtle); }
    .ret > :first-child { flex-shrink: 0; }
    .ret .muted { flex: 1; min-width: 0; }
    .ret .amount { margin-inline-start: auto; }
    .muted { color: var(--erp-color-text-muted); }
    .foot { display: flex; flex-wrap: wrap; gap: 8px; }
  `],
})
export class PosOrdersListPage {
  private readonly api = inject(PosOrdersApi);
  private readonly returnsApi = inject(PosReturnsApi);
  private readonly registersApi = inject(PosRegistersApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosOrderSummaryDto>((q: any) => this.api.list(q), { sort: '-orderDate' });

  readonly columns: TableColumn<PosOrderSummaryDto>[] = [
    { key: 'number', label: 'Receipt', sortField: 'number' },
    { key: 'customerName', label: 'Customer', sortField: 'customerName' },
    { key: 'cashierName', label: 'Cashier' },
    { key: 'orderDate', label: 'Date', sortField: 'orderDate', filterField: 'orderDate' },
    { key: 'lineCount', label: 'Lines', align: 'right', filter: false },
    { key: 'totalAmount', label: 'Total', sortField: 'totalAmount', align: 'right', filter: false },
    { key: 'returnedTotal', label: 'Returned', align: 'right', filter: false },
    { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['completed', 'parked', 'voided'] },
    { key: 'paymentStatus', label: 'Payment', filterField: 'paymentStatus', filterOptions: ['paid', 'onAccount', 'partiallyPaid', 'partiallyRefunded', 'refunded', 'unpaid'] },
  ];

  readonly drawerOpen = signal(false);
  readonly loadFailed = signal(false);
  readonly voiding = signal(false);
  readonly order = signal<PosOrderDto | null>(null);
  readonly register = signal<PosRegisterDto | null>(null);
  readonly returnsOfOrder = signal<PosReturnSummaryDto[]>([]);

  readonly receiptData = computed<ThermalReceiptData | null>(() => {
    const o = this.order();
    if (!o) return null;
    return saleReceipt(o, { business: this.sessionStore.me()?.workspace?.name, register: this.register(), currency: this.currency() });
  });
  readonly hasPayments = computed(() => (this.order()?.payments.length ?? 0) > 0);
  readonly canReturn = computed(() => {
    const o = this.order();
    return !!o && o.status === 'completed' && o.lines.some((l) => l.returnedQuantity < l.quantity);
  });

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.open(id);
  }

  tender(method: string): string {
    return TENDER_LABELS[method as keyof typeof TENDER_LABELS] ?? method;
  }

  open(id: string): void {
    this.drawerOpen.set(true);
    this.loadFailed.set(false);
    this.order.set(null);
    this.returnsOfOrder.set([]);
    // The receipt still shows if the register's wording or the returns cannot be loaded.
    forkJoin({
      order: this.api.get(id),
      returns: this.returnsApi.list({ page: 1, pageSize: 50, filters: [{ field: 'originalOrderId', op: 'eq', value: id }] }).pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ order, returns }) => {
        this.order.set(order);
        this.returnsOfOrder.set(returns?.items ?? []);
        this.registersApi.get(order.registerId).pipe(catchError(() => of(null))).subscribe((r) => this.register.set(r));
      },
      error: () => this.loadFailed.set(true),
    });
  }

  close(): void {
    this.drawerOpen.set(false);
    if (this.route.snapshot.paramMap.get('id')) void this.router.navigate(['/pos/orders'], { replaceUrl: true });
  }

  async voidOrder(): Promise<void> {
    const o = this.order();
    if (!o) return;
    const reason = await askReason(this.modal, `Void ${o.number}?`, 'Void sale', 'This reverses the money, tax and stock of the whole sale. It only works while its shift is still open. After that, use Return.');
    if (!reason) return;
    this.voiding.set(true);
    this.api.voidOrder(o.id, reason).subscribe({
      next: (updated) => {
        this.voiding.set(false);
        this.order.set(updated);
        this.list.reload();
        this.message.success(`${o.number} voided.`);
      },
      error: (err) => {
        this.voiding.set(false);
        this.message.error(problemMessage(err, 'The sale could not be voided.'));
      },
    });
  }
}
