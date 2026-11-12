import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '@core/api/paged-list';
import { PlatformApi, PlatformInvoiceRow } from '@core/api/platform.api';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { ErpDatePipe, ErpMoneyPipe } from '@shared/pipes/display.pipes';
import { InvoiceActionsComponent } from '../../components/invoice-actions/invoice-actions.component';
import { PLATFORM_STYLES, invoiceTone } from '../../platform-ui';

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
  templateUrl: './platform-invoices.page.html',
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
