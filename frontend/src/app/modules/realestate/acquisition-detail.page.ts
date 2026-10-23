import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { AcquisitionDto, AcquisitionsApi, DESCRIPTION_COLUMNS, FundingInstalmentDto, FundingSourceDto, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';

/** One acquisition: cost summary, funding sources with their instalment schedules, completion and instalment payments. */
import { ClientPager, PaginationBarComponent } from '../../shared/pagination-bar.component';

@Component({
  selector: 'erp-acquisition-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzAlertModule, NzButtonModule, NzCardModule, NzDescriptionsModule, NzModalModule, NzTableModule, NzTabsModule, PaginationBarComponent],
  template: `
    @if (acq(); as a) {
      <p><a routerLink="/real-estate/acquisitions">← Acquisitions</a></p>
      <h1 class="erp-page-title" data-testid="acq-title">{{ a.number }} {{ a.propertyName }} <erp-status-tag [status]="a.status" /></h1>
      @if (a.status === 'completed') { <nz-alert nzType="success" nzShowIcon nzMessage="Completed and capitalised. Financing is fixed; pay instalments from each schedule." style="margin-bottom:16px"></nz-alert> }
      @if (a.status === 'draft') {
        <div style="display:flex;gap:8px;margin-bottom:16px">
          <button *erpHasPermission="'realestate.acquisitions.complete'" nz-button nzType="primary" [disabled]="busy()" (click)="complete()" data-testid="acq-complete">Complete the purchase</button>
          <button *erpHasPermission="'realestate.acquisitions.manage'" nz-button nzDanger [disabled]="busy()" (click)="cancel()" data-testid="acq-cancel">Cancel draft</button>
        </div>
      }
      <div style="display:flex;gap:16px;flex-wrap:wrap;margin-bottom:16px" data-testid="acq-summary">
        <nz-card><small>Total cost</small><div><b>{{ a.totalCost | number: '1.0-2' }}</b> {{ a.currencyCode }}</div><small>price + fees + costs</small></nz-card>
        <nz-card><small>Cash at completion</small><div><b>{{ a.cashAtCompletion | number: '1.0-2' }}</b></div><small>down payment + fees + costs</small></nz-card>
        <nz-card><small>Financed</small><div><b>{{ a.financed | number: '1.0-2' }}</b></div><small>{{ a.funding.length }} sources</small></nz-card>
        <nz-card><small>Unfunded</small><div><b>{{ a.unfunded | number: '1.0-2' }}</b></div><small>due to seller</small></nz-card>
      </div>
      <nz-card nzTitle="Details">
        <nz-descriptions nzBordered [nzColumn]="columns">
          <nz-descriptions-item nzTitle="Seller / developer">{{ a.sellerName }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Purchase date">{{ a.purchaseDate }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Contract reference">{{ a.contractReference ?? '—' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Purchase price">{{ a.purchasePrice | number: '1.2-2' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Down payment">{{ a.downPayment | number: '1.2-2' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Transfer and registration fees">{{ a.transferFees | number: '1.2-2' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Other costs">{{ a.otherCosts | number: '1.2-2' }}</nz-descriptions-item>
          @if (a.notes) { <nz-descriptions-item nzTitle="Notes">{{ a.notes }}</nz-descriptions-item> }
        </nz-descriptions>
      </nz-card>
      <nz-tabset style="margin-top:16px">
        <nz-tab nzTitle="Funding sources">
          @for (f of a.funding; track f.id) {
            <nz-card [nzTitle]="humanize(f.type) + ' · ' + f.lender" style="margin-bottom:16px">
              <p>Principal <b>{{ f.principal | number: '1.0-2' }}</b> · rate {{ f.annualRatePercent }}% · {{ f.termMonths }} months · monthly {{ f.monthlyPayment | number: '1.2-2' }} · finance cost {{ f.financeCost | number: '1.0-2' }} · outstanding <b>{{ f.outstanding | number: '1.0-2' }}</b> · repaid {{ f.paid }}/{{ f.instalmentCount }}</p>
              @if (f.instalments.length) {
                <nz-table #ts [nzData]="f.instalments" nzSize="small" [nzShowPagination]="false" [nzPageIndex]="pager(f).page()" [nzPageSize]="pager(f).size()" [attr.data-testid]="'schedule-' + f.lender">
                  <thead><tr><th>#</th><th>Due</th><th>Principal</th><th>Interest</th><th>Total</th><th>Balance after</th><th>Status</th><th></th></tr></thead>
                  <tbody>
                    @for (i of ts.data; track i.id) {
                      <tr><td>{{ i.sequence }}</td><td>{{ i.dueDate }}</td><td>{{ i.principal | number: '1.2-2' }}</td><td>{{ i.interest | number: '1.2-2' }}</td><td>{{ i.total | number: '1.2-2' }}</td><td>{{ i.balanceAfter | number: '1.0-2' }}</td>
                        <td><erp-status-tag [status]="i.status" /> @if (i.paidOn) { {{ i.paidOn }} }</td>
                        <td>@if (i.status === 'scheduled' && isNext(f, i)) { <a *erpHasPermission="'realestate.funding.pay'" (click)="startPay(i)" data-testid="pay-instalment">Pay</a> }</td></tr>
                    }
                  </tbody>
                </nz-table>
                <erp-pagination-bar [total]="f.instalments.length" [page]="pager(f).page()" [pageSize]="pager(f).size()" [pageSizeOptions]="[12, 24, 48, 120]" [hideOnSinglePage]="true" (pageChange)="pager(f).change($event)" />
              } @else { <p>The schedule is generated when the purchase completes.</p> }
            </nz-card>
          } @empty { <p>No funding sources. The purchase is paid from equity.</p> }
        </nz-tab>
      </nz-tabset>
      <erp-record-drawer title="Pay instalment" [open]="paying() !== null" [fields]="payFields()" [saving]="busy()" [errors]="errors()" [width]="420" testId="pay-form" submitLabel="Pay"
                         (submitted)="pay($event)" (closed)="paying.set(null)" />
    }`,
})
export class AcquisitionDetailPage {
  private readonly api = inject(AcquisitionsApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly columns = DESCRIPTION_COLUMNS;
  readonly humanize = humanize;
  readonly acq = signal<AcquisitionDto | null>(null);
  private readonly pagers = new Map<string, ClientPager>();
  /** Page state per funding schedule; opens on the page holding the next instalment due. */
  pager(f: FundingSourceDto): ClientPager {
    let p = this.pagers.get(f.id);
    if (!p) {
      p = new ClientPager(12);
      const next = f.instalments.findIndex((i) => i.status === 'scheduled');
      if (next > 0) p.page.set(Math.floor(next / 12) + 1);
      this.pagers.set(f.id, p);
    }
    return p;
  }
  readonly busy = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly paying = signal<FundingInstalmentDto | null>(null);
  readonly payFields = computed<FieldSpec[]>(() => [
    { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })) },
    { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date() },
  ]);

  constructor() { this.load(); }

  isNext(f: FundingSourceDto, i: FundingInstalmentDto): boolean { return f.instalments.find((x) => x.status === 'scheduled')?.id === i.id; }
  startPay(i: FundingInstalmentDto): void { this.errors.set(null); this.paying.set(i); }

  complete(): void {
    this.modal.confirm({ nzTitle: 'Complete this purchase?', nzContent: 'The property is capitalised and the instalment schedules are generated. Financing cannot be changed afterwards.', nzOkText: 'Complete', nzOnOk: () => this.run(this.api.complete(this.id), 'Purchase completed') });
  }
  cancel(): void { this.modal.confirm({ nzTitle: 'Cancel this draft?', nzOkDanger: true, nzOkText: 'Cancel draft', nzOnOk: () => this.run(this.api.cancel(this.id), 'Draft cancelled') }); }

  pay(v: Record<string, unknown>): void {
    const i = this.paying();
    if (!i) return;
    this.busy.set(true);
    this.api.payInstalment(i.id, v['bankAccountId'] as string, v['date'] as string).subscribe({
      next: () => { this.busy.set(false); this.paying.set(null); this.message.success('Instalment paid'); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not pay the instalment'); },
    });
  }

  private run(call: ReturnType<AcquisitionsApi['complete']>, success: string): Promise<void> {
    this.busy.set(true);
    return new Promise((resolve) => call.subscribe({
      next: (a) => { this.busy.set(false); this.acq.set(a); this.message.success(success); resolve(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); resolve(); },
    }));
  }

  private load(): void { this.api.detail(this.id).subscribe((a) => this.acq.set(a)); }
}
