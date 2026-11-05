import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '../../core/api/paged-list';
import { PlatformApi, PlatformInvoiceRow } from '../../core/api/platform.api';
import { ErpDatePipe, ErpMoneyPipe } from '../../shared/display.pipes';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { InvoiceActionsComponent } from './invoice-actions.component';
import { PLATFORM_STYLES, invoiceTone } from './platform-ui';

/** Every invoice sent to a client. Payments arrive outside the system, so they are recorded here by hand. */
@Component({
  selector: 'erp-platform-invoices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, NzSelectModule, ErpDatePipe, ErpMoneyPipe, InvoiceActionsComponent, PagedTableComponent, StatusTagComponent],
  styles: [PLATFORM_STYLES, `
    .filter { width: 170px; }
    .stack { display: flex; flex-direction: column; }
  `],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search by number or client" emptyText="No invoices yet" [filterable]="false" [configurable]="false" [exportable]="false"
                     tableName="platform-invoices" scrollX="1100px" (rowClick)="open($event)">
      <nz-select toolbar class="filter" [ngModel]="status()" (ngModelChange)="setStatus($event)" nzAllowClear nzPlaceHolder="Any status" data-reveal="off" data-testid="invoices-status-filter">
        <nz-option nzValue="open" nzLabel="Open"></nz-option>
        <nz-option nzValue="overdue" nzLabel="Overdue"></nz-option>
        <nz-option nzValue="paid" nzLabel="Paid"></nz-option>
        <nz-option nzValue="void" nzLabel="Void"></nz-option>
      </nz-select>
      <ng-template #cell let-r let-col="column">
        @switch (col.key) {
          @case ('number') { <a class="pf-nowrap" [routerLink]="['/platform/invoices', r.id]" (click)="$event.stopPropagation()" [attr.data-testid]="'invoice-' + r.number">{{ r.number }}</a> }
          @case ('client') { <a [routerLink]="['/platform/clients', r.orgId]" (click)="$event.stopPropagation()">{{ r.clientName }}</a> }
          @case ('description') { {{ r.description }} }
          @case ('total') { <span class="pf-nowrap">{{ r.total | erpMoney: r.currency }}</span> }
          @case ('dueOn') { <span class="pf-nowrap">{{ r.dueOn | erpDate }}</span> }
          @case ('status') { <erp-status [value]="tone(r)" /> }
          @case ('payment') {
            @if (r.paidOn) { <div class="stack"><span class="pf-nowrap">{{ r.paidOn | erpDate }}</span>@if (r.paymentMethod || r.paymentReference) { <span class="pf-small pf-muted">{{ r.paymentMethod }}{{ r.paymentMethod && r.paymentReference ? ', ' : '' }}{{ r.paymentReference }}</span> }</div> } @else { — }
          }
          @case ('rowActions') { <erp-invoice-actions [invoice]="r" [clientName]="r.clientName" (changed)="list.reload()" /> }
        }
      </ng-template>
    </erp-paged-table>`,
})
export class PlatformInvoicesPage {
  private readonly api = inject(PlatformApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly status = signal<string | null>(this.route.snapshot.queryParamMap.get('status'));
  private readonly orgId = this.route.snapshot.queryParamMap.get('orgId');
  readonly list = createPagedList<PlatformInvoiceRow>((q) => this.api.invoices(q), { extra: this.extra() });
  readonly columns: TableColumn<PlatformInvoiceRow>[] = [
    { key: 'number', label: 'Number', width: '110px', value: (r) => r.number },
    { key: 'client', label: 'Client', width: '160px', value: (r) => r.clientName },
    { key: 'description', label: 'Description', value: (r) => r.description },
    { key: 'total', label: 'Total', width: '110px', align: 'right', value: (r) => r.total },
    { key: 'dueOn', label: 'Due', width: '105px', value: (r) => r.dueOn },
    { key: 'status', label: 'Status', width: '85px', value: (r) => r.status },
    { key: 'payment', label: 'Paid', width: '130px', value: (r) => r.paidOn },
    { key: 'rowActions', label: '', width: '205px' },
  ];

  private extra(): Record<string, string> {
    const extra: Record<string, string> = {};
    if (this.status()) extra['status'] = this.status()!;
    if (this.orgId) extra['orgId'] = this.orgId;
    return extra;
  }

  setStatus(value: string | null): void { this.status.set(value); this.list.setExtra(this.extra()); }
  tone(r: PlatformInvoiceRow): string { return invoiceTone(r); }
  open(r: PlatformInvoiceRow): void { void this.router.navigate(['/platform/invoices', r.id]); }
}
