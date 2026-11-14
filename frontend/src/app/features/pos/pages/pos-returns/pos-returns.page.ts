import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '@core/api/paged-list';
import { PosReturnDto, PosReturnsApi, PosReturnSummaryDto } from '@core/api/pos.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { CanPipe } from '@shared/pipes/can.pipe';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { tenderName } from '../../pos-cart';

/** Money given back at the counter, and why. Returns are taken on the terminal, against a receipt. */
@Component({
  selector: 'erp-pos-returns',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterModule, ErpDatePipe, NzButtonModule, NzDrawerModule, NzIconModule, HasPermissionDirective, CanPipe, PagedTableComponent],
  templateUrl: './pos-returns.page.html',
  styleUrl: './pos-returns.page.scss',
})
export class PosReturnsPage {
  private readonly returnsApi = inject(PosReturnsApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosReturnSummaryDto>((q) => this.returnsApi.list(q), { sort: '-returnDate' });
  readonly columns: TableColumn<PosReturnSummaryDto>[] = [
    { key: 'number', label: 'Return', sortField: 'number' },
    { key: 'originalNumber', label: 'Receipt' },
    { key: 'customerName', label: 'Customer', sortField: 'customerName' },
    { key: 'cashierName', label: 'Cashier' },
    { key: 'returnDate', label: 'Date', sortField: 'returnDate', filterField: 'returnDate' },
    { key: 'reason', label: 'Reason' },
    { key: 'totalAmount', label: 'Refunded', sortField: 'totalAmount', align: 'right', filter: false },
  ];

  readonly drawerOpen = signal(false);
  readonly detail = signal<PosReturnDto | null>(null);

  tender(method: string): string {
    return tenderName(method);
  }

  open(r: PosReturnSummaryDto): void {
    this.detail.set(null);
    this.drawerOpen.set(true);
    this.returnsApi.get(r.id).subscribe({
      next: (d) => this.detail.set(d),
      error: () => {
        this.drawerOpen.set(false);
        this.message.error('This return could not be loaded.');
      },
    });
  }
}
