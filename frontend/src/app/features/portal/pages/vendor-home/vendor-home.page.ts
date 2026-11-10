import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { PortalService, VendorBill, VendorPayment, VendorStatement, VendorWorkOrder, VendorWorkOrderUpdate } from '@core/api/portal.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { PortalLoadHandlers, portalFallback, portalMoney } from '../../portal-view';

/** The vendor's own view: bills, payments, statement of what is owed to them, and the work orders assigned to them (start, hold, complete). */
@Component({
  selector: 'erp-vendor-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, TableToolsComponent, TranslatePipe, FormsModule, NzTabsModule, NzTableModule, NzTagModule, NzButtonModule, NzInputModule, NzInputNumberModule, NzModalModule, PaginationBarComponent, StatePanelComponent, StatusTagComponent],
  styleUrl: './vendor-home.page.scss',
  templateUrl: './vendor-home.page.html',
})
export class VendorHomePage implements OnInit {
  readonly portal = inject(PortalService);
  readonly money = portalMoney;
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  readonly bills = signal<VendorBill[]>([]);
  readonly payments = signal<VendorPayment[]>([]);
  readonly billPg = new ClientPager(20);
  readonly payPg = new ClientPager(20);
  readonly woPg = new ClientPager(20);
  readonly statement = signal<VendorStatement | null>(null);
  readonly orders = signal<VendorWorkOrder[]>([]);
  readonly completing = signal<VendorWorkOrder | null>(null);
  readonly failed = signal(false);
  /** The statement and work orders carry no currency of their own, so they take the one on the supplier's documents. */
  readonly code = computed(() => this.bills()[0]?.currencyCode ?? this.payments()[0]?.currencyCode ?? '');
  /** Client-side search across the tabs' lists: the portal endpoints take no query parameters. */
  readonly q = signal('');
  readonly billsShown = computed(() => this.bills().filter((r) => matchText(this.q(), r.number, r.vendorReference, r.status)));
  readonly paymentsShown = computed(() => this.payments().filter((r) => matchText(this.q(), r.number, r.method, r.reference)));
  readonly ordersShown = computed(() => this.orders().filter((r) => matchText(this.q(), r.number, r.issue, r.priority, r.status)));
  cost: number | null = null;
  notes = '';
  private readonly onLoadError: PortalLoadHandlers = { signOut: () => this.signOut(), failed: () => this.failed.set(true) };

  search(q: string): void { this.q.set(q); this.billPg.page.set(1); this.payPg.page.set(1); this.woPg.page.set(1); }

  refresh(): void { this.ngOnInit(); }

  ngOnInit(): void {
    if (!this.portal.signedIn() || this.portal.kind() !== 'vendor') { void this.router.navigateByUrl('/portal/login'); return; }
    this.failed.set(false);
    this.portal.vendorBills().pipe(portalFallback([] as VendorBill[], this.onLoadError)).subscribe((v) => this.bills.set(v));
    this.portal.vendorPayments().pipe(portalFallback([] as VendorPayment[], this.onLoadError)).subscribe((v) => this.payments.set(v));
    this.portal.vendorStatement().pipe(portalFallback<VendorStatement | null>(null, this.onLoadError)).subscribe((v) => this.statement.set(v));
    this.loadOrders();
  }

  update(o: VendorWorkOrder, status: string, extra: Omit<VendorWorkOrderUpdate, 'status'> = {}): void {
    this.portal.updateWorkOrder(o.id, { status, ...extra }).subscribe({
      next: () => { this.message.success(doneText('updated', 'Work order', o.number)); this.completing.set(null); this.loadOrders(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the work order'),
    });
  }

  openComplete(o: VendorWorkOrder): void { this.cost = o.estimatedCost; this.notes = ''; this.completing.set(o); }
  complete(): void { const o = this.completing(); if (o) this.update(o, 'completed', { actualCost: this.cost, notes: this.notes || null }); }

  signOut(): void { this.portal.signOut(); void this.router.navigateByUrl('/portal/login'); }

  private loadOrders(): void { this.portal.vendorWorkOrders().pipe(catchError(() => of([] as VendorWorkOrder[]))).subscribe((v) => this.orders.set(v)); }
}
