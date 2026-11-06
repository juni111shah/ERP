import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { OperatorFunction, catchError, of } from 'rxjs';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { PortalProjectsComponent } from './portal-projects.component';
import { portalMoney, PortalInvoice, PortalLease, PortalReceipt, PortalRequest, PortalService, PortalStatement } from './portal.service';

/** The tenant's own view: leases and rent schedule, invoices, receipts, statement and maintenance requests. */
import { ClientPager, PaginationBarComponent } from '../../shared/pagination-bar.component';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { ErpDatePipe } from '../../shared/display.pipes';

@Component({
  selector: 'erp-portal-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, PortalProjectsComponent, TableToolsComponent, TranslatePipe, ReactiveFormsModule, NzSkeletonModule, NzTabsModule, NzTableModule, NzTagModule, NzButtonModule, NzFormModule, NzInputModule, NzSelectModule, PaginationBarComponent, StatCardComponent, StatePanelComponent, StatusTagComponent],
  styles: [`:host { display:block; max-width: 1000px; margin: 0 auto; padding: 24px 16px; } .top { display:flex; align-items:center; gap:12px; margin-bottom:16px; } .top h1 { margin:0; flex:1; } .lease { margin-bottom: 24px; }
    .panel { background: var(--erp-color-bg-surface); border: 1px solid var(--erp-color-border-subtle); border-radius: 12px; overflow: hidden; margin-bottom: 12px; }
    .num { text-align: end; white-space: nowrap; } .owed { color: var(--erp-color-error-text); font-weight: 600; } .clear { color: var(--erp-color-success-text); font-weight: 600; }
    .balance { background: var(--erp-color-bg-surface); padding: 16px; border-radius: 12px; border: 1px solid var(--erp-color-border-subtle); margin-bottom: 16px; }
    .balance .label { font-size: 14px; color: var(--erp-color-text-secondary); } .balance .amount { font-size: 24px; font-weight: 700; margin: 4px 0; } .balance .asof { font-size: 12px; color: var(--erp-color-text-muted); }
    .credit { margin-inline-start: 6px; }`],
  template: `
    <div class="top"><h1>{{ 'portal.welcome' | translate: 'Welcome' }}, {{ portal.name() }}</h1><button nz-button (click)="signOut()" data-testid="portal-signout">{{ 'portal.signOut' | translate: 'Sign out' }}</button></div>
    @if (failed()) {
      <erp-state-panel kind="error" title="Some of your details could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="refresh()" style="margin-bottom:16px" data-testid="portal-failed" />
    }
    @if (!ready()) {
      <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }"></nz-skeleton>
    } @else {
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="portal-summary">
      <erp-stat-card label="Balance due" [value]="statement() ? money(statement()!.balance, code()) : '—'" [caption]="statement() ? 'after payments on account' : null" [tone]="(statement()?.balance ?? 0) > 0 ? 'danger' : 'success'" />
      <erp-stat-card label="Invoices to pay" [value]="openInvoices()" format="number" [tone]="openInvoices() > 0 ? 'warning' : 'neutral'" />
      @if (leases().length) { <erp-stat-card label="Open maintenance requests" [value]="openRequests()" format="number" [tone]="2" /> }
    </div>
    <erp-table-tools [search]="q()" (searchChange)="search($event)" (refresh)="refresh()" placeholder="Search invoices, receipts and requests" testId="portal"></erp-table-tools>
    <nz-tabset>
      @if (leases().length) {
      <nz-tab [nzTitle]="'portal.tab.lease' | translate: 'My lease'">
        @for (l of leases(); track l.id) {
          <div class="lease" data-testid="portal-lease">
            <h3>{{ l.property }} · {{ 'ui.re.unit' | translate: 'Unit' }} {{ l.unit }} <erp-status [value]="l.status" /></h3>
            <p>{{ l.startDate | erpDate }} {{ 'ui.common.toLc' | translate: 'to' }} {{ l.endDate | erpDate }} · {{ 'ui.re.annualRent' | translate: 'Annual rent' }} {{ money(l.annualRent, l.currencyCode) }} · {{ 'ui.re.deposit' | translate: 'Deposit' }} {{ money(l.securityDeposit, l.currencyCode) }}</p>
            <nz-table [nzData]="l.schedule" nzSize="small" [nzFrontPagination]="false" [nzShowPagination]="false" nzNoResult="The rent schedule is not ready yet">
              <thead><tr><th>#</th><th>Due</th><th class="num">Amount</th><th>Status</th></tr></thead>
              <tbody>@for (s of l.schedule; track s.sequence) { <tr><td>{{ s.sequence }}</td><td>{{ s.dueDate | erpDate }}</td><td class="num">{{ money(s.amount, l.currencyCode) }}</td><td><erp-status [value]="s.state" /></td></tr> }</tbody>
            </nz-table>
          </div>
        }
      </nz-tab>
      }
      <nz-tab [nzTitle]="'portal.tab.invoices' | translate: 'Invoices'">
        <div class="panel">
          <nz-table #ti [nzData]="invoicesShown()" nzSize="middle" [nzShowPagination]="false" [nzPageIndex]="invPg.page()" [nzPageSize]="invPg.size()" nzNoResult="No invoices yet" data-testid="portal-invoices">
            <thead><tr><th>Number</th><th>Date</th><th>Due</th><th class="num">Total</th><th class="num">Outstanding</th><th>Status</th></tr></thead>
            <tbody>
              @for (i of ti.data; track i.id) {
                <tr>
                  <td style="white-space:nowrap"><span class="code-pill">{{ i.number }}</span>@if (i.isCreditNote) { <nz-tag class="credit">Credit note</nz-tag> }</td>
                  <td>{{ i.invoiceDate | erpDate }}</td>
                  <td>{{ i.dueDate | erpDate }}</td>
                  <td class="num"><strong>{{ money(i.isCreditNote ? -i.total : i.total, i.currencyCode) }}</strong></td>
                  <td class="num" [class.owed]="i.outstanding > 0 && !i.isCreditNote" [class.clear]="i.outstanding <= 0 || i.isCreditNote">{{ money(i.isCreditNote ? -i.outstanding : i.outstanding, i.currencyCode) }}</td>
                  <td><erp-status [value]="i.status" /></td>
                </tr>
              }
            </tbody>
          </nz-table>
        </div>
        <erp-pagination-bar [total]="invoicesShown().length" [page]="invPg.page()" [pageSize]="invPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:portal-invoices" (pageChange)="invPg.change($event)" />
      </nz-tab>
      <nz-tab [nzTitle]="'portal.tab.receipts' | translate: 'Receipts'">
        <div class="panel">
          <nz-table #tr [nzData]="receiptsShown()" nzSize="middle" [nzShowPagination]="false" [nzPageIndex]="recPg.page()" [nzPageSize]="recPg.size()" nzNoResult="No receipts yet">
            <thead><tr><th>Number</th><th>Date</th><th>Method</th><th class="num">Amount</th><th>Reference</th></tr></thead>
            <tbody>
              @for (r of tr.data; track r.id) {
                <tr>
                  <td><span class="code-pill">{{ r.number }}</span></td>
                  <td>{{ r.date | erpDate }}</td>
                  <td><erp-status [value]="r.method" /></td>
                  <td class="num clear">{{ money(r.amount, r.currencyCode) }}</td>
                  <td>{{ r.reference || '—' }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        </div>
        <erp-pagination-bar [total]="receiptsShown().length" [page]="recPg.page()" [pageSize]="recPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:portal-receipts" (pageChange)="recPg.change($event)" />
      </nz-tab>
      <nz-tab [nzTitle]="'portal.tab.statement' | translate: 'Statement'">
        @if (statement(); as s) {
          <div class="balance" data-testid="portal-balance">
            <span class="label">{{ 'portal.balance' | translate: 'Balance due' }}</span>
            <div class="amount" [class.owed]="s.balance > 0" [class.clear]="s.balance <= 0">{{ money(s.balance, code()) }}</div>
            <span class="asof">{{ 'ui.common.asOfLc' | translate: 'as of' }} {{ s.asOf | erpDate }}</span>
          </div>
          <div class="panel">
            <nz-table [nzData]="s.items" nzSize="middle" [nzFrontPagination]="false" [nzShowPagination]="false" nzNoResult="Nothing is outstanding">
              <thead><tr><th>Document</th><th>Due</th><th>Overdue</th><th class="num">Outstanding</th></tr></thead>
              <tbody>
                @for (i of s.items; track i.number) {
                  <tr>
                    <td style="white-space:nowrap"><span class="code-pill">{{ i.number }}</span>@if (i.isCreditNote) { <nz-tag class="credit">Credit note</nz-tag> }</td>
                    <td>{{ i.dueDate | erpDate }}</td>
                    <td>@if (i.isCreditNote || i.daysOverdue === 0) { Not overdue } @else { <nz-tag [nzColor]="i.daysOverdue > 30 ? 'red' : 'orange'">{{ i.daysOverdue }} {{ i.daysOverdue === 1 ? 'day' : 'days' }}</nz-tag> }</td>
                    <td class="num" [class.owed]="!i.isCreditNote" [class.clear]="i.isCreditNote">{{ money(i.isCreditNote ? -i.outstanding : i.outstanding, code()) }}</td>
                  </tr>
                }
              </tbody>
            </nz-table>
          </div>
          @if (s.unallocatedReceipts > 0) { <p class="clear" style="margin-top:12px">{{ 'ui.portal.onAccount' | translate: 'On account' }}: {{ money(s.unallocatedReceipts, code()) }}</p> }
        } @else {
          <erp-state-panel title="Your statement is not available right now" hint="Use Try again at the top of the page, or contact us if it keeps happening." icon="file-text" />
        }
      </nz-tab>
      <nz-tab [nzTitle]="'portal.tab.projects' | translate: 'Projects'">
        <ng-template nz-tab><erp-portal-projects /></ng-template>
      </nz-tab>
      @if (leases().length) {
      <nz-tab [nzTitle]="'portal.tab.maintenance' | translate: 'Maintenance'">
        @if (leases().length) {
          <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="raise()" style="max-width:520px">
            <nz-form-item><nz-form-label nzRequired>Lease</nz-form-label><nz-form-control>
              <nz-select formControlName="leaseId" data-testid="portal-req-lease">@for (l of leases(); track l.id) { <nz-option [nzValue]="l.id" [nzLabel]="l.property + ' · Unit ' + l.unit"></nz-option> }</nz-select></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label nzRequired>What is wrong?</nz-form-label><nz-form-control nzErrorTip="Describe the issue"><input nz-input formControlName="issue" maxlength="300" data-testid="portal-req-issue" /></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label>Priority</nz-form-label><nz-form-control>
              <nz-select formControlName="priority"><nz-option nzValue="normal" nzLabel="Normal"></nz-option><nz-option nzValue="high" nzLabel="Urgent"></nz-option><nz-option nzValue="low" nzLabel="Low"></nz-option></nz-select></nz-form-control></nz-form-item>
            <button nz-button nzType="primary" [nzLoading]="raising()" data-testid="portal-req-submit">{{ 'portal.sendRequest' | translate: 'Send request' }}</button>
          </form>
        }
        <nz-table #tq [nzData]="requestsShown()" nzSize="small" style="margin-top:16px" [nzShowPagination]="false" [nzPageIndex]="reqPg.page()" [nzPageSize]="reqPg.size()" nzNoResult="You have not sent any requests yet" data-testid="portal-requests">
          <thead><tr><th>Number</th><th>Issue</th><th>Reported</th><th>Status</th></tr></thead>
          <tbody>@for (r of tq.data; track r.id) { <tr><td style="white-space:nowrap">{{ r.number }}</td><td>{{ r.issue }}</td><td style="white-space:nowrap">{{ r.reportedOn | erpDate }}</td><td><erp-status [value]="r.status" /></td></tr> }</tbody>
        </nz-table>
        <erp-pagination-bar [total]="requestsShown().length" [page]="reqPg.page()" [pageSize]="reqPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:portal-requests" (pageChange)="reqPg.change($event)" />
      </nz-tab>
      }
    </nz-tabset>
    }`,
})
export class PortalHomePage implements OnInit {
  readonly portal = inject(PortalService);
  readonly money = portalMoney;
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  readonly leases = signal<PortalLease[]>([]);
  readonly invoices = signal<PortalInvoice[]>([]);
  readonly receipts = signal<PortalReceipt[]>([]);
  readonly requests = signal<PortalRequest[]>([]);
  readonly statement = signal<PortalStatement | null>(null);
  /** The tabs depend on whether this customer has a lease, so they wait for that answer. */
  readonly ready = signal(false);
  readonly failed = signal(false);
  readonly raising = signal(false);
  /** The statement has no currency of its own, so it takes the one on the customer's documents. */
  readonly code = computed(() => this.invoices()[0]?.currencyCode ?? this.leases()[0]?.currencyCode ?? this.receipts()[0]?.currencyCode ?? '');
  readonly openInvoices = computed(() => this.invoices().filter((i) => !i.isCreditNote && i.outstanding > 0).length);
  readonly openRequests = computed(() => this.requests().filter((r) => r.status !== 'completed' && r.status !== 'cancelled' && r.status !== 'closed').length);
  readonly invPg = new ClientPager(20);
  readonly recPg = new ClientPager(20);
  readonly reqPg = new ClientPager(20);
  /** Client-side search across the tabs' lists: the portal endpoints take no query parameters. */
  readonly q = signal('');
  readonly invoicesShown = computed(() => this.invoices().filter((r) => matchText(this.q(), r.number, r.status)));
  readonly receiptsShown = computed(() => this.receipts().filter((r) => matchText(this.q(), r.number, r.method, r.reference)));
  readonly requestsShown = computed(() => this.requests().filter((r) => matchText(this.q(), r.number, r.issue, r.status)));
  readonly form = inject(FormBuilder).nonNullable.group({ leaseId: ['', Validators.required], issue: ['', Validators.required], priority: ['normal'] });

  search(q: string): void { this.q.set(q); this.invPg.page.set(1); this.recPg.page.set(1); this.reqPg.page.set(1); }

  refresh(): void { this.ngOnInit(); }

  ngOnInit(): void {
    if (!this.portal.signedIn()) { void this.router.navigateByUrl('/portal/login'); return; }
    if (this.portal.kind() === 'vendor') { void this.router.navigateByUrl('/portal/vendor'); return; }
    this.failed.set(false);
    // A disabled module answers 403: that is not a failure, the customer simply has none of those records.
    const guard = <T>(fallback: T): OperatorFunction<T, T> => catchError((e: { status?: number }) => { if (e.status === 401) this.signOut(); else if (e.status !== 403 && e.status !== 404) this.failed.set(true); return of(fallback); });
    this.portal.leases().pipe(guard([] as PortalLease[])).subscribe((v) => { this.leases.set(v); this.ready.set(true); if (v.length === 1) this.form.patchValue({ leaseId: v[0].id }); });
    this.portal.invoices().pipe(guard([] as PortalInvoice[])).subscribe((v) => this.invoices.set(v));
    this.portal.receipts().pipe(guard([] as PortalReceipt[])).subscribe((v) => this.receipts.set(v));
    this.portal.statement().pipe(guard<PortalStatement | null>(null)).subscribe((v) => this.statement.set(v));
    this.loadRequests();
  }

  private loadRequests(): void { this.portal.requests().pipe(catchError(() => of([] as PortalRequest[]))).subscribe((v) => this.requests.set(v)); }

  raise(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.raising.set(true);
    this.portal.raise(this.form.getRawValue()).subscribe({
      next: () => { this.raising.set(false); this.message.success('Request sent'); this.form.patchValue({ issue: '' }); this.form.markAsPristine(); this.form.markAsUntouched(); this.loadRequests(); },
      error: () => { this.raising.set(false); this.message.error('Could not send the request'); },
    });
  }

  signOut(): void { this.portal.signOut(); void this.router.navigateByUrl('/portal/login'); }
}
