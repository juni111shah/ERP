import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { OrderDto, SalesOrdersApi, SalesOrderStatus } from '@core/api/sales.api';
import { toIsoDate } from '@core/format/dates';
import { problemMessage } from '@core/http/problem-details';
import { CreateProjectButtonComponent } from '@shared/components/create-project-button/create-project-button.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { FulfillmentDraftLine, fulfilledPercent, hasNetDelivery, toFulfillmentDraft, toFulfillmentInputs } from '../../sales-fulfillment';
import { statusChangedText } from '../../sales-status';

/** One sales order: its lines, status changes, recording a delivery and turning it into an invoice. */
@Component({
  selector: 'erp-order-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatePanelComponent, StatusTagComponent, NzSkeletonModule, NzToolTipModule,
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    CreateProjectButtonComponent,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTableModule,
    NzDescriptionsModule,
    NzProgressModule,
    NzModalModule,
    NzInputNumberModule,
    NzPopconfirmModule,
    NzDatePickerModule,
    NzSelectModule,
    HasPermissionDirective,
  ],
  templateUrl: './order-detail.page.html',
  styleUrl: './order-detail.page.scss',
})
export class OrderDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly ordersApi = inject(SalesOrdersApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly message = inject(NzMessageService);

  readonly order = signal<OrderDto | null>(null);
  readonly fulfillmentModalVisible = signal(false);
  readonly deliveryDate = signal<Date>(new Date());
  readonly warehouseId = signal<string | null>(null);
  readonly warehouses = signal<WarehouseRow[]>([]);
  readonly fulfillmentDraft = signal<FulfillmentDraftLine[]>([]);
  readonly failed = signal(false);
  readonly saving = signal(false);
  private orderId: string | null = null;

  /** Something has been delivered and not all of it has come back. */
  readonly hasDelivered = () => hasNetDelivery(this.order());
  readonly fulfillmentPercentage = () => fulfilledPercent(this.order());

  constructor() {
    loadInto(this.warehouses, this.warehousesApi.list({ page: 1, pageSize: 100, sort: 'code' }), (r) => r.items.filter((w) => w.isActive));
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.loadOrder(id);
    });
  }

  reload(): void {
    if (this.orderId) this.loadOrder(this.orderId);
  }

  loadOrder(id: string): void {
    this.orderId = id;
    this.failed.set(false);
    this.ordersApi.get(id).subscribe({
      next: (o) => this.order.set(o),
      error: () => this.failed.set(true),
    });
  }

  changeStatus(status: SalesOrderStatus): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.setStatus(o.id, status).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.message.success(statusChangedText(o.number, status));
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to update status')),
    });
  }

  openFulfillmentModal(): void {
    const o = this.order();
    if (!o) return;
    this.fulfillmentDraft.set(toFulfillmentDraft(o.lines));
    this.deliveryDate.set(new Date());
    this.warehouseId.set(null);
    this.fulfillmentModalVisible.set(true);
  }

  saveFulfillment(): void {
    const o = this.order();
    if (!o) return;
    const lines = toFulfillmentInputs(this.fulfillmentDraft());
    this.saving.set(true);
    this.ordersApi.updateFulfillment(o.id, lines, { deliveryDate: toIsoDate(this.deliveryDate()), warehouseId: this.warehouseId() }).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.order.set(updated);
        this.fulfillmentModalVisible.set(false);
        this.message.success(`Delivery recorded for ${o.number}.`);
      },
      error: (err) => {
        this.saving.set(false);
        this.message.error(problemMessage(err, 'Failed to update delivery'));
      },
    });
  }

  convertToInvoice(): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.convertToInvoice(o.id).subscribe({
      next: () => {
        this.message.success(`Invoice created from order ${o.number}`);
        this.loadOrder(o.id);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to generate invoice')),
    });
  }
}
