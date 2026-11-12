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
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ThermalReceiptComponent, ThermalReceiptData } from '@shared/components/thermal-receipt/thermal-receipt.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { askReason } from '@shared/utils/reason-prompt';
import { tenderName } from '../../pos-cart';
import { saleReceipt } from '../../pos-receipt';

/** Back-office list of till sales; a row (or /pos/orders/:id) opens the receipt, its returns and the void action. */
@Component({
  selector: 'erp-pos-orders-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatusTagComponent, DecimalPipe, RouterModule, NzAlertModule, NzButtonModule, NzDrawerModule, NzIconModule, NzModalModule, PagedTableComponent, HasPermissionDirective, ThermalReceiptComponent],
  templateUrl: './pos-orders-list.page.html',
  styleUrl: './pos-orders-list.page.scss',
})
export class PosOrdersListPage {
  private readonly ordersApi = inject(PosOrdersApi);
  private readonly returnsApi = inject(PosReturnsApi);
  private readonly registersApi = inject(PosRegistersApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosOrderSummaryDto>((q) => this.ordersApi.list(q), { sort: '-orderDate' });

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
    return tenderName(method);
  }

  open(id: string): void {
    this.drawerOpen.set(true);
    this.loadFailed.set(false);
    this.order.set(null);
    this.returnsOfOrder.set([]);
    // The receipt still shows if the register's wording or the returns cannot be loaded.
    forkJoin({
      order: this.ordersApi.get(id),
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
    this.ordersApi.voidOrder(o.id, reason).subscribe({
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
