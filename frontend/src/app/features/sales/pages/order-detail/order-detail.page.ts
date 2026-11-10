import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '@core/http/problem-details';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { CreateProjectButtonComponent } from '@shared/components/create-project-button/create-project-button.component';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { toIsoDate } from '@core/format/dates';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { OrderDto, OrderFulfillmentLineInput, SalesOrdersApi, SalesOrderStatus } from '@core/api/sales.api';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { displayLabel } from '@core/format/display';

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
  private readonly message = inject(NzMessageService);

  readonly order = signal<OrderDto | null>(null);
  readonly fulfillmentModalVisible = signal(false);
  readonly deliveryDate = signal<Date>(new Date());
  readonly warehouseId = signal<string | null>(null);
  readonly warehouses = signal<WarehouseRow[]>([]);
  readonly fulfillmentDraft = signal<{ lineId: string; description: string; quantity: number; alreadyDelivered: number; deliveredQuantity: number }[]>([]);
  readonly failed = signal(false);
  readonly saving = signal(false);
  private orderId: string | null = null;

  /** Something has been delivered and not all of it has come back. */
  readonly hasDelivered = () => (this.order()?.lines ?? []).some((l) => l.deliveredQuantity - (l.returnedQuantity ?? 0) > 0);

  readonly fulfillmentPercentage = () => {
    const o = this.order();
    if (!o || o.lines.length === 0) return 0;
    const totalOrdered = o.lines.reduce((acc, l) => acc + l.quantity, 0);
    const totalDelivered = o.lines.reduce((acc, l) => acc + l.deliveredQuantity, 0);
    if (totalOrdered <= 0) return 0;
    return Math.min(100, Math.round((totalDelivered / totalOrdered) * 100));
  };

  constructor() {
    inject(WarehousesApi).list({ page: 1, pageSize: 100, sort: 'code' }).subscribe({ next: (r) => this.warehouses.set(r.items.filter((w) => w.isActive)), error: () => undefined });
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
        this.message.success(`${o.number} is now ${displayLabel(status).toLowerCase()}.`);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to update status')),
    });
  }

  openFulfillmentModal(): void {
    const o = this.order();
    if (!o) return;
    this.fulfillmentDraft.set(
      o.lines.map((l) => ({
        lineId: l.id,
        description: l.description,
        quantity: l.quantity,
        alreadyDelivered: l.deliveredQuantity,
        deliveredQuantity: l.quantity,
      }))
    );
    this.deliveryDate.set(new Date());
    this.warehouseId.set(null);
    this.fulfillmentModalVisible.set(true);
  }

  saveFulfillment(): void {
    const o = this.order();
    if (!o) return;
    const lines: OrderFulfillmentLineInput[] = this.fulfillmentDraft().map((d) => ({
      lineId: d.lineId,
      deliveredQuantity: d.deliveredQuantity,
    }));
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
