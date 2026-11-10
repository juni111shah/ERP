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
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { TENDER_LABELS } from '../../pos-cart';

/** Money given back at the counter, and why. Returns are taken on the terminal, against a receipt. */
@Component({
  selector: 'erp-pos-returns',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterModule, ErpDatePipe, NzButtonModule, NzDrawerModule, NzIconModule, HasPermissionDirective, PagedTableComponent],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search return, receipt, customer, cashier" emptyText="No returns yet. Take one from the terminal, against the receipt." exportFilename="pos-returns" (rowClick)="open($event)">
      <a toolbar *erpHasPermission="'pos.orders.refund'" routerLink="/pos/terminal" nz-button nzType="primary" data-testid="pos-open-terminal"><span nz-icon nzType="rollback"></span>Take a return</a>
      <ng-template #cell let-r let-column="column" let-value="value">
        @if (column.key === 'number') {
          <strong class="nowrap">{{ r.number }}</strong>
        } @else if (column.key === 'originalNumber') {
          <a [routerLink]="['/pos/orders', r.originalOrderId]" (click)="$event.stopPropagation()" class="nowrap">{{ r.originalNumber }}</a>
        } @else if (column.key === 'returnDate') {
          <span class="nowrap">{{ r.returnDate | erpDate }}</span>
        } @else if (column.key === 'totalAmount') {
          <strong class="amount">{{ currency() }} {{ r.totalAmount | number:'1.2-2' }}</strong>
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzTitle]="detail()?.number ?? 'Return'" [nzWidth]="460" (nzOnClose)="drawerOpen.set(false)">
      <ng-container *nzDrawerContent>
        @if (detail(); as r) {
          <div class="head" data-testid="pos-return-detail">
            <div class="kv"><span>Against receipt</span><a [routerLink]="['/pos/orders', r.originalOrderId]">{{ r.originalNumber }}</a></div>
            <div class="kv"><span>Date</span><span>{{ r.returnDate | erpDate }}</span></div>
            <div class="kv"><span>Customer</span><span>{{ r.customerName }}</span></div>
            <div class="kv"><span>Cashier</span><span>{{ r.cashierName }}</span></div>
            <div class="kv"><span>Reason</span><span>{{ r.reason }}</span></div>
            @if (r.creditNoteNumber) { <div class="kv"><span>Credit note</span><a [routerLink]="['/finance/invoices', r.creditNoteId]">{{ r.creditNoteNumber }}</a></div> }
          </div>
          <h4>Items</h4>
          @for (l of r.lines; track l.id) {
            <div class="row">
              <span class="grow">{{ l.quantity | number:'1.0-2' }} × {{ l.description }} @if (!l.restock) { <small class="muted">not restocked</small> }</span>
              <strong class="amount">{{ currency() }} {{ l.lineTotal | number:'1.2-2' }}</strong>
            </div>
          }
          <h4>Money back</h4>
          @for (f of r.refunds; track f.id) {
            <div class="row">
              <span class="grow">{{ tender(f.method) }} @if (f.reference) { <small class="muted">{{ f.reference }}</small> }</span>
              <strong class="amount">{{ currency() }} {{ f.amount | number:'1.2-2' }}</strong>
            </div>
          }
          <div class="row total"><span class="grow">Total</span><strong class="amount">{{ currency() }} {{ r.totalAmount | number:'1.2-2' }}</strong></div>
          <a class="print" [href]="'/print/pos-return/' + r.id" target="_blank" rel="noopener" nz-button data-testid="pos-return-print"><span nz-icon nzType="printer"></span>Print refund receipt</a>
        }
      </ng-container>
    </nz-drawer>
  `,
  styles: [`
    .nowrap { white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .muted { color: var(--erp-color-text-muted); }
    .head { display: flex; flex-direction: column; gap: 4px; }
    .kv { display: flex; justify-content: space-between; gap: 16px; }
    .kv > span:first-child { color: var(--erp-color-text-muted); }
    h4 { margin: 14px 0 4px; font-size: 13px; font-weight: 700; }
    .row { display: flex; gap: 10px; padding: 5px 0; border-top: 1px solid var(--erp-color-border-subtle); }
    .grow { flex: 1; min-width: 0; }
    .total { font-weight: 700; border-top-width: 2px; margin-top: 6px; }
    .print { margin-top: 16px; }
  `],
})
export class PosReturnsPage {
  private readonly api = inject(PosReturnsApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosReturnSummaryDto>((q: any) => this.api.list(q), { sort: '-returnDate' });
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
    return TENDER_LABELS[method as keyof typeof TENDER_LABELS] ?? method;
  }

  open(r: PosReturnSummaryDto): void {
    this.detail.set(null);
    this.drawerOpen.set(true);
    this.api.get(r.id).subscribe({
      next: (d) => this.detail.set(d),
      error: () => {
        this.drawerOpen.set(false);
        this.message.error('This return could not be loaded.');
      },
    });
  }
}
