import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { OperatorFunction, catchError, of } from 'rxjs';
import { PortalInvoice, PortalLease, PortalReceipt, PortalRequest, PortalService, PortalStatement } from './portal.service';

/** The tenant's own view: leases and rent schedule, invoices, receipts, statement and maintenance requests. */
import { ClientPager, PaginationBarComponent } from '../../shared/pagination-bar.component';

@Component({
  selector: 'erp-portal-home',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, DatePipe, CurrencyPipe, ReactiveFormsModule, NzTabsModule, NzTableModule, NzTagModule, NzButtonModule, NzFormModule, NzInputModule, NzSelectModule, PaginationBarComponent],
  styles: [`:host { display:block; max-width: 1000px; margin: 0 auto; padding: 24px 16px; } .top { display:flex; align-items:center; gap:12px; margin-bottom:16px; } .top h1 { margin:0; flex:1; } .lease { margin-bottom: 24px; }`],
  template: `
    <div class="top"><h1>{{ 'portal.welcome' | translate: 'Welcome' }}, {{ portal.name() }}</h1><button nz-button (click)="signOut()" data-testid="portal-signout">{{ 'portal.signOut' | translate: 'Sign out' }}</button></div>
    <nz-tabset>
      <nz-tab [nzTitle]="'portal.tab.lease' | translate: 'My lease'">
        @for (l of leases(); track l.id) {
          <div class="lease" data-testid="portal-lease">
            <h3>{{ l.property }} · Unit {{ l.unit }} <nz-tag>{{ l.status }}</nz-tag></h3>
            <p>{{ l.startDate }} to {{ l.endDate }} · Annual rent {{ l.annualRent | currency: l.currencyCode }} · Deposit {{ l.securityDeposit | currency: l.currencyCode }}</p>
            <nz-table [nzData]="l.schedule" nzSize="small" [nzShowPagination]="false">
              <thead><tr><th>#</th><th>Due</th><th>Amount</th><th>Status</th></tr></thead>
              <tbody>@for (s of l.schedule; track s.sequence) { <tr><td>{{ s.sequence }}</td><td>{{ s.dueDate }}</td><td>{{ s.amount | currency: l.currencyCode }}</td><td><nz-tag [nzColor]="s.state === 'paid' ? 'green' : s.state === 'overdue' ? 'red' : 'default'">{{ s.state }}</nz-tag></td></tr> }</tbody>
            </nz-table>
          </div>
        } @empty { <p>{{ 'portal.noLease' | translate: 'No lease on file.' }}</p> }
      </nz-tab>
      <nz-tab [nzTitle]="'portal.tab.invoices' | translate: 'Invoices'">
        <nz-table #ti [nzData]="invoices()" nzSize="small" [nzShowPagination]="false" [nzPageIndex]="invPg.page()" [nzPageSize]="invPg.size()" data-testid="portal-invoices">
          <thead><tr><th>Number</th><th>Date</th><th>Due</th><th>Total</th><th>Outstanding</th><th>Status</th></tr></thead>
          <tbody>@for (i of ti.data; track i.id) { <tr><td>{{ i.number }}</td><td>{{ i.invoiceDate }}</td><td>{{ i.dueDate }}</td><td>{{ i.total | currency: i.currencyCode }}</td><td>{{ i.outstanding | currency: i.currencyCode }}</td><td>{{ i.status }}</td></tr> }</tbody>
        </nz-table>
        <erp-pagination-bar [total]="invoices().length" [page]="invPg.page()" [pageSize]="invPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:portal-invoices" (pageChange)="invPg.change($event)" />
      </nz-tab>
      <nz-tab [nzTitle]="'portal.tab.receipts' | translate: 'Receipts'">
        <nz-table #tr [nzData]="receipts()" nzSize="small" [nzShowPagination]="false" [nzPageIndex]="recPg.page()" [nzPageSize]="recPg.size()">
          <thead><tr><th>Number</th><th>Date</th><th>Method</th><th>Amount</th><th>Reference</th></tr></thead>
          <tbody>@for (r of tr.data; track r.id) { <tr><td>{{ r.number }}</td><td>{{ r.date }}</td><td>{{ r.method }}</td><td>{{ r.amount | currency: r.currencyCode }}</td><td>{{ r.reference }}</td></tr> }</tbody>
        </nz-table>
        <erp-pagination-bar [total]="receipts().length" [page]="recPg.page()" [pageSize]="recPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:portal-receipts" (pageChange)="recPg.change($event)" />
      </nz-tab>
      <nz-tab [nzTitle]="'portal.tab.statement' | translate: 'Statement'">
        @if (statement(); as s) {
          <p data-testid="portal-balance"><b>{{ 'portal.balance' | translate: 'Balance due' }}: {{ s.balance | currency }}</b> (as of {{ s.asOf }})</p>
          <nz-table [nzData]="s.items" nzSize="small" [nzShowPagination]="false">
            <thead><tr><th>Document</th><th>Due</th><th>Days overdue</th><th>Outstanding</th></tr></thead>
            <tbody>@for (i of s.items; track i.number) { <tr><td>{{ i.number }}</td><td>{{ i.dueDate }}</td><td>{{ i.daysOverdue }}</td><td>{{ i.outstanding | currency }}</td></tr> }</tbody>
          </nz-table>
          @if (s.unallocatedReceipts > 0) { <p>On account: {{ s.unallocatedReceipts | currency }}</p> }
        }
      </nz-tab>
      <nz-tab [nzTitle]="'portal.tab.maintenance' | translate: 'Maintenance'">
        @if (leases().length) {
          <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="raise()" style="max-width:520px">
            <nz-form-item><nz-form-label nzRequired>Lease</nz-form-label><nz-form-control>
              <nz-select formControlName="leaseId" data-testid="portal-req-lease">@for (l of leases(); track l.id) { <nz-option [nzValue]="l.id" [nzLabel]="l.property + ' · Unit ' + l.unit"></nz-option> }</nz-select></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label nzRequired>What is wrong?</nz-form-label><nz-form-control nzErrorTip="Describe the issue"><input nz-input formControlName="issue" maxlength="300" data-testid="portal-req-issue" /></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label>Priority</nz-form-label><nz-form-control>
              <nz-select formControlName="priority"><nz-option nzValue="normal" nzLabel="Normal"></nz-option><nz-option nzValue="high" nzLabel="Urgent"></nz-option><nz-option nzValue="low" nzLabel="Low"></nz-option></nz-select></nz-form-control></nz-form-item>
            <button nz-button nzType="primary" data-testid="portal-req-submit">{{ 'portal.sendRequest' | translate: 'Send request' }}</button>
          </form>
        }
        <nz-table #tq [nzData]="requests()" nzSize="small" style="margin-top:16px" [nzShowPagination]="false" [nzPageIndex]="reqPg.page()" [nzPageSize]="reqPg.size()" data-testid="portal-requests">
          <thead><tr><th>Number</th><th>Issue</th><th>Reported</th><th>Status</th></tr></thead>
          <tbody>@for (r of tq.data; track r.id) { <tr><td>{{ r.number }}</td><td>{{ r.issue }}</td><td>{{ r.reportedOn }}</td><td>{{ r.status }}</td></tr> }</tbody>
        </nz-table>
        <erp-pagination-bar [total]="requests().length" [page]="reqPg.page()" [pageSize]="reqPg.size()" [hideOnSinglePage]="true" storageKey="erp.table.pageSize:portal-requests" (pageChange)="reqPg.change($event)" />
      </nz-tab>
    </nz-tabset>`,
})
export class PortalHomePage implements OnInit {
  readonly portal = inject(PortalService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  readonly leases = signal<PortalLease[]>([]);
  readonly invoices = signal<PortalInvoice[]>([]);
  readonly receipts = signal<PortalReceipt[]>([]);
  readonly requests = signal<PortalRequest[]>([]);
  readonly statement = signal<PortalStatement | null>(null);
  readonly invPg = new ClientPager(20);
  readonly recPg = new ClientPager(20);
  readonly reqPg = new ClientPager(20);
  readonly form = inject(FormBuilder).nonNullable.group({ leaseId: ['', Validators.required], issue: ['', Validators.required], priority: ['normal'] });

  ngOnInit(): void {
    if (!this.portal.signedIn()) { void this.router.navigateByUrl('/portal/login'); return; }
    if (this.portal.kind() === 'vendor') { void this.router.navigateByUrl('/portal/vendor'); return; }
    const guard = <T>(fallback: T): OperatorFunction<T, T> => catchError((e: { status?: number }) => { if (e.status === 401) this.signOut(); return of(fallback); });
    this.portal.leases().pipe(guard([] as PortalLease[])).subscribe((v) => { this.leases.set(v); if (v.length === 1) this.form.patchValue({ leaseId: v[0].id }); });
    this.portal.invoices().pipe(guard([] as PortalInvoice[])).subscribe((v) => this.invoices.set(v));
    this.portal.receipts().pipe(guard([] as PortalReceipt[])).subscribe((v) => this.receipts.set(v));
    this.portal.statement().pipe(guard<PortalStatement | null>(null)).subscribe((v) => this.statement.set(v));
    this.loadRequests();
  }

  private loadRequests(): void { this.portal.requests().pipe(catchError(() => of([] as PortalRequest[]))).subscribe((v) => this.requests.set(v)); }

  raise(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.portal.raise(this.form.getRawValue()).subscribe({
      next: () => { this.message.success('Request sent'); this.form.patchValue({ issue: '' }); this.loadRequests(); },
      error: () => this.message.error('Could not send the request'),
    });
  }

  signOut(): void { this.portal.signOut(); void this.router.navigateByUrl('/portal/login'); }
}
