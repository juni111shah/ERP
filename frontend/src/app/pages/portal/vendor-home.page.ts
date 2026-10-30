import { CurrencyPipe } from '@angular/common';
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
import { OperatorFunction, catchError, of } from 'rxjs';
import { asProblem } from '../../core/http/problem-details';
import { PortalService, VendorBill, VendorPayment, VendorStatement, VendorWorkOrder } from './portal.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** The vendor's own view: bills, payments, statement of what is owed to them, and the work orders assigned to them (start, hold, complete). */
import { ClientPager, PaginationBarComponent } from '../../shared/pagination-bar.component';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';

@Component({
  selector: 'erp-vendor-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TableToolsComponent, TranslatePipe, CurrencyPipe, FormsModule, NzTabsModule, NzTableModule, NzTagModule, NzButtonModule, NzInputModule, NzInputNumberModule, NzModalModule, PaginationBarComponent],
  styles: [`:host { display:block; max-width: 1000px; margin: 0 auto; padding: 24px 16px; } .top { display:flex; align-items:center; gap:12px; margin-bottom:16px; } .top h1 { margin:0; flex:1; }`],
  template: `
    <div class="top"><h1>{{ 'ui.portal.welcome' | translate: 'Welcome' }}, {{ portal.name() }}</h1><button nz-button (click)="signOut()" data-testid="portal-signout">Sign out</button></div>
    <erp-table-tools [search]="q()" (searchChange)="search($event)" (refresh)="refresh()" testId="vendor"></erp-table-tools>
    <nz-tabset>
      <nz-tab nzTitle="Bills">
        <div class="erp-page-card" style="background:#fff; border:1px solid #eef2f6; border-radius:12px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.04); margin-bottom:12px;">
          <nz-table #tb [nzData]="billsShown()" nzSize="middle" [nzShowPagination]="false" [nzPageIndex]="billPg.page()" [nzPageSize]="billPg.size()" data-testid="vendor-bills">
            <thead><tr><th>Number</th><th>Your reference</th><th>Date</th><th>Due</th><th>Total</th><th>Outstanding</th><th>Status</th></tr></thead>
            <tbody>
              @for (b of tb.data; track b.id) {
                <tr>
                  <td><span class="code-pill">{{ b.number }}</span></td>
                  <td>{{ b.vendorReference || '—' }}</td>
                  <td>{{ b.billDate }}</td>
                  <td>{{ b.dueDate }}</td>
                  <td><strong>{{ b.total | currency: b.currencyCode }}</strong></td>
                  <td [style.color]="b.outstanding > 0 ? '#ef4444' : '#10955d'" style="font-weight:600;">{{ b.outstanding | currency: b.currencyCode }}</td>
                  <td><nz-tag [nzColor]="b.status === 'paid' ? 'green' : (b.status === 'overdue' ? 'red' : 'blue')">{{ b.status }}</nz-tag></td>
                </tr>
              }
            </tbody>
          </nz-table>
        </div>
        <erp-pagination-bar [total]="billsShown().length" [page]="billPg.page()" [pageSize]="billPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:vendor-bills" (pageChange)="billPg.change($event)" />
      </nz-tab>
      <nz-tab nzTitle="Payments">
        <div class="erp-page-card" style="background:#fff; border:1px solid #eef2f6; border-radius:12px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.04); margin-bottom:12px;">
          <nz-table #tp [nzData]="paymentsShown()" nzSize="middle" [nzShowPagination]="false" [nzPageIndex]="payPg.page()" [nzPageSize]="payPg.size()" data-testid="vendor-payments">
            <thead><tr><th>Number</th><th>Date</th><th>Method</th><th>Amount</th><th>Reference</th></tr></thead>
            <tbody>
              @for (p of tp.data; track p.id) {
                <tr>
                  <td><span class="code-pill">{{ p.number }}</span></td>
                  <td>{{ p.date }}</td>
                  <td><nz-tag>{{ p.method }}</nz-tag></td>
                  <td><strong style="color:#10955d;">{{ p.amount | currency: p.currencyCode }}</strong></td>
                  <td>{{ p.reference || '—' }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        </div>
        <erp-pagination-bar [total]="paymentsShown().length" [page]="payPg.page()" [pageSize]="payPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:vendor-payments" (pageChange)="payPg.change($event)" />
      </nz-tab>
      <nz-tab nzTitle="Statement">
        @if (statement(); as s) {
          <div style="background:#f8fafc; padding:16px; border-radius:12px; border:1px solid #e2e8f0; margin-bottom:16px;" data-testid="vendor-balance">
            <span style="font-size:14px; color:#64748b;">{{ 'ui.portal.owedToYou' | translate: 'Owed to you' }}</span>
            <div style="font-size:24px; font-weight:700; color:#10955d; margin:4px 0;">{{ s.balance | currency }}</div>
            <span style="font-size:12px; color:#94a3b8;">{{ 'ui.common.asOfLc' | translate: 'as of' }} {{ s.asOf }}</span>
          </div>
          <div class="erp-page-card" style="background:#fff; border:1px solid #eef2f6; border-radius:12px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.04);">
            <nz-table [nzData]="s.items" nzSize="middle" [nzShowPagination]="false">
              <thead><tr><th>Bill</th><th>Due</th><th>Days overdue</th><th>Outstanding</th></tr></thead>
              <tbody>
                @for (i of s.items; track i.number) {
                  <tr>
                    <td><span class="code-pill">{{ i.number }}</span></td>
                    <td>{{ i.dueDate }}</td>
                    <td><nz-tag [nzColor]="i.daysOverdue > 30 ? 'red' : 'orange'">{{ i.daysOverdue }} d</nz-tag></td>
                    <td><strong style="color:#ef4444;">{{ i.outstanding | currency }}</strong></td>
                  </tr>
                }
              </tbody>
            </nz-table>
          </div>
        }
      </nz-tab>
      <nz-tab nzTitle="Work orders">
        <nz-table #to [nzData]="ordersShown()" nzSize="small" [nzShowPagination]="false" [nzPageIndex]="woPg.page()" [nzPageSize]="woPg.size()" data-testid="vendor-orders">
          <thead><tr><th>Number</th><th>Issue</th><th>Priority</th><th>Status</th><th>Estimate</th><th></th></tr></thead>
          <tbody>
            @for (o of to.data; track o.id) {
              <tr>
                <td>{{ o.number }}</td><td>{{ o.issue }}</td><td>{{ o.priority }}</td><td><nz-tag>{{ o.status }}</nz-tag></td><td>{{ o.estimatedCost | currency }}</td>
                <td>
                  @if (o.status === 'assigned' || o.status === 'new' || o.status === 'onHold') { <button nz-button nzSize="small" (click)="update(o, 'inProgress')" [attr.data-testid]="'wo-start-' + o.id">Start</button> }
                  @if (o.status === 'inProgress') {
                    <button nz-button nzSize="small" (click)="update(o, 'onHold')">Hold</button>
                    <button nz-button nzSize="small" nzType="primary" (click)="openComplete(o)" [attr.data-testid]="'wo-complete-' + o.id">Complete</button>
                  }
                </td>
              </tr>
            } @empty { <tr><td colspan="6">No work orders assigned to you.</td></tr> }
          </tbody>
        </nz-table>
        <erp-pagination-bar [total]="ordersShown().length" [page]="woPg.page()" [pageSize]="woPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:vendor-orders" (pageChange)="woPg.change($event)" />
      </nz-tab>
    </nz-tabset>
    <nz-modal [nzVisible]="!!completing()" nzTitle="Complete work order" (nzOnCancel)="completing.set(null)" (nzOnOk)="complete()" nzOkText="Complete">
      <ng-container *nzModalContent>
        <p>Actual cost</p><nz-input-number [(ngModel)]="cost" [nzMin]="0" style="width:100%" data-testid="vendor-cost"></nz-input-number>
        <p style="margin-top:12px">Notes</p><textarea nz-input rows="3" [(ngModel)]="notes" data-testid="vendor-notes"></textarea>
      </ng-container>
    </nz-modal>`,
})
export class VendorHomePage implements OnInit {
  readonly portal = inject(PortalService);
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
  /** Client-side search across the tabs' lists: the portal endpoints take no query parameters. */
  readonly q = signal('');
  readonly billsShown = computed(() => this.bills().filter((r) => matchText(this.q(), r.number, r.vendorReference, r.status)));
  readonly paymentsShown = computed(() => this.payments().filter((r) => matchText(this.q(), r.number, r.method, r.reference)));
  readonly ordersShown = computed(() => this.orders().filter((r) => matchText(this.q(), r.number, r.issue, r.priority, r.status)));
  cost: number | null = null;
  notes = '';

  search(q: string): void { this.q.set(q); this.billPg.page.set(1); this.payPg.page.set(1); this.woPg.page.set(1); }

  refresh(): void { this.ngOnInit(); }

  ngOnInit(): void {
    if (!this.portal.signedIn() || this.portal.kind() !== 'vendor') { void this.router.navigateByUrl('/portal/login'); return; }
    const guard = <T>(fallback: T): OperatorFunction<T, T> => catchError((e: { status?: number }) => { if (e.status === 401) this.signOut(); return of(fallback); });
    this.portal.vendorBills().pipe(guard([] as VendorBill[])).subscribe((v) => this.bills.set(v));
    this.portal.vendorPayments().pipe(guard([] as VendorPayment[])).subscribe((v) => this.payments.set(v));
    this.portal.vendorStatement().pipe(guard<VendorStatement | null>(null)).subscribe((v) => this.statement.set(v));
    this.loadOrders();
  }

  private loadOrders(): void { this.portal.vendorWorkOrders().pipe(catchError(() => of([] as VendorWorkOrder[]))).subscribe((v) => this.orders.set(v)); }

  update(o: VendorWorkOrder, status: string, extra: { actualCost?: number | null; notes?: string | null } = {}): void {
    this.portal.updateWorkOrder(o.id, { status, ...extra }).subscribe({
      next: () => { this.message.success('Work order updated'); this.completing.set(null); this.loadOrders(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the work order'),
    });
  }

  openComplete(o: VendorWorkOrder): void { this.cost = o.estimatedCost; this.notes = ''; this.completing.set(o); }
  complete(): void { const o = this.completing(); if (o) this.update(o, 'completed', { actualCost: this.cost, notes: this.notes || null }); }

  signOut(): void { this.portal.signOut(); void this.router.navigateByUrl('/portal/login'); }
}
