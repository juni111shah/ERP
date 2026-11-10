import { RecordHistoryComponent } from '@shared/components/record-history/record-history.component';
import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '@core/format/done-text';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { AcquisitionDto, AcquisitionsApi, DESCRIPTION_COLUMNS, FundingInstalmentDto, FundingSourceDto, humanize } from '@core/api/realestate.api';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { TranslatePipe } from '@core/i18n/translate.pipe';

/** One acquisition: cost summary, funding sources with their instalment schedules, completion and instalment payments. */
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

@Component({
  selector: 'erp-acquisition-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatCardComponent, RecordHistoryComponent, TranslatePipe, DecimalPipe, RouterLink, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzAlertModule, NzButtonModule, NzCardModule, NzDescriptionsModule, NzModalModule, NzTableModule, NzTabsModule, PaginationBarComponent],
  template: `
    @if (acq(); as a) {
      <p><a routerLink="/real-estate/acquisitions">← Acquisitions</a></p>
      <h1 data-testid="acq-title" style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin:0 0 12px;font-size:20px;font-weight:600">{{ a.number }} {{ a.propertyName }} <erp-status-tag [status]="a.status" /></h1>
      @if (a.status === 'completed') { <nz-alert nzType="success" nzShowIcon nzMessage="Completed and capitalised. Financing is fixed; pay instalments from each schedule." style="margin-bottom:16px"></nz-alert> }
      @if (a.status === 'draft') {
        <div style="display:flex;gap:8px;margin-bottom:16px">
          <button *erpHasPermission="'realestate.acquisitions.complete'" nz-button nzType="primary" [disabled]="busy()" (click)="complete()" data-testid="acq-complete">Complete the purchase</button>
          <button *erpHasPermission="'realestate.acquisitions.manage'" nz-button nzDanger [disabled]="busy()" (click)="cancel()" data-testid="acq-cancel">Cancel draft</button>
        </div>
      }
      <div class="erp-stat-grid" data-testid="acq-summary">
        <erp-stat-card label="Total cost" [value]="a.totalCost" format="number" [suffix]="a.currencyCode" caption="price + fees + costs" [tone]="0" />
        <erp-stat-card label="Cash at completion" [value]="a.cashAtCompletion" format="number" [suffix]="a.currencyCode" caption="down payment + fees + costs" [tone]="1" />
        <erp-stat-card label="Financed" [value]="a.financed" format="number" [suffix]="a.currencyCode" [caption]="a.funding.length + ' ' + ('ui.re.sources' | translate: 'sources')" [tone]="2" />
        <erp-stat-card label="Unfunded" [value]="a.unfunded" format="number" [suffix]="a.currencyCode" caption="due to seller" [tone]="a.unfunded > 0 ? 'warning' : 4" />
      </div>
      <nz-card nzTitle="Details">
        <nz-descriptions nzBordered [nzColumn]="columns">
          <nz-descriptions-item nzTitle="Seller / developer">{{ a.sellerName }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Purchase date">{{ a.purchaseDate | erpDate }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Contract reference">{{ a.contractReference ?? '—' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Purchase price">{{ a.currencyCode }} {{ a.purchasePrice | number: '1.2-2' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Down payment">{{ a.currencyCode }} {{ a.downPayment | number: '1.2-2' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Transfer and registration fees">{{ a.currencyCode }} {{ a.transferFees | number: '1.2-2' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Other costs">{{ a.currencyCode }} {{ a.otherCosts | number: '1.2-2' }}</nz-descriptions-item>
          @if (a.notes) { <nz-descriptions-item nzTitle="Notes">{{ a.notes }}</nz-descriptions-item> }
        </nz-descriptions>
      </nz-card>
      <nz-tabset style="margin-top:16px">
        <nz-tab nzTitle="Funding sources">
          @for (f of a.funding; track f.id) {
            <nz-card [nzTitle]="humanize(f.type) + ' · ' + f.lender" style="margin-bottom:16px">
              <p>Principal <b>{{ f.principal | number: '1.0-2' }}</b> · {{ 'ui.re.rate' | translate: 'rate' }} {{ f.annualRatePercent }}% · {{ f.termMonths }} {{ 'ui.common.months' | translate: 'months' }} · {{ 'ui.re.monthly' | translate: 'monthly' }} {{ f.monthlyPayment | number: '1.2-2' }} · {{ 'ui.re.financeCost' | translate: 'finance cost' }} {{ f.financeCost | number: '1.0-2' }} · {{ 'ui.common.outstandingLc' | translate: 'outstanding' }} <b>{{ f.outstanding | number: '1.0-2' }}</b> · {{ 'ui.re.repaid' | translate: 'repaid' }} {{ f.paid }}/{{ f.instalmentCount }}</p>
              @if (f.instalments.length) {
                <nz-table #ts [nzData]="f.instalments" nzSize="small" [nzShowPagination]="false" [nzPageIndex]="pager(f).page()" [nzPageSize]="pager(f).size()" [attr.data-testid]="'schedule-' + f.lender">
                  <thead><tr><th>#</th><th>Due</th><th style="text-align:end">Principal</th><th style="text-align:end">Interest</th><th style="text-align:end">Total</th><th style="text-align:end">Balance after</th><th>Status</th><th></th></tr></thead>
                  <tbody>
                    @for (i of ts.data; track i.id) {
                      <tr><td>{{ i.sequence }}</td><td>{{ i.dueDate | erpDate }}</td><td style="text-align:end">{{ i.principal | number: '1.2-2' }}</td><td style="text-align:end">{{ i.interest | number: '1.2-2' }}</td><td style="text-align:end">{{ i.total | number: '1.2-2' }}</td><td style="text-align:end">{{ i.balanceAfter | number: '1.2-2' }}</td>
                        <td><erp-status-tag [status]="i.status" /> @if (i.paidOn) { {{ i.paidOn | erpDate }} }</td>
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
      <nz-card *erpHasPermission="'core.audit.read'" style="margin-top:16px" [nzTitle]="'history.title' | translate: 'History'"><erp-record-history recordType="Acquisition" [recordId]="a.id" /></nz-card>
      <erp-record-drawer title="Bank account for the purchase" [open]="choosingBank()" [fields]="bankFields()" [saving]="busy()" [errors]="errors()" [width]="420" testId="acq-bank-form" submitLabel="Save and continue"
                         (submitted)="saveBank($event)" (closed)="choosingBank.set(false)" />
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
      const created = new ClientPager(12);
      p = created;
      const next = f.instalments.findIndex((i) => i.status === 'scheduled');
      if (next > 0) untracked(() => created.page.set(Math.floor(next / 12) + 1)); // runs while the template renders
      this.pagers.set(f.id, p);
    }
    return p;
  }
  readonly busy = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly paying = signal<FundingInstalmentDto | null>(null);
  readonly choosingBank = signal(false);
  readonly bankFields = computed<FieldSpec[]>(() => [
    { key: 'payFromAccountId', label: 'Pay from', type: 'select', required: true, hint: 'The down payment and fees are paid from this account when the purchase completes.', options: this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })) },
  ]);
  /** A draft with cash to pay and no bank cannot complete, and has no other screen to set one. */
  saveBank(v: Record<string, unknown>): void {
    const a = this.acq();
    if (!a) return;
    this.busy.set(true); this.errors.set(null);
    const body = { legalEntityId: a.legalEntityId, propertyId: a.propertyId, sellerName: a.sellerName, purchaseDate: a.purchaseDate, contractReference: a.contractReference ?? null, purchasePrice: a.purchasePrice, downPayment: a.downPayment, transferFees: a.transferFees, otherCosts: a.otherCosts, payFromAccountId: v['payFromAccountId'], notes: a.notes ?? null,
      funding: a.funding.map((f) => ({ type: f.type, lender: f.lender, principal: f.principal, annualRatePercent: f.annualRatePercent, termMonths: f.termMonths, firstDueDate: f.firstDueDate })) };
    this.api.save(a.id, body).subscribe({
      next: (saved) => { this.busy.set(false); this.acq.set(saved); this.choosingBank.set(false); this.message.success(`Bank account saved for purchase ${saved.number}`); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); if (!p?.errors) this.message.error(p?.detail ?? p?.title ?? 'Could not save the bank account'); },
    });
  }
  readonly payFields = computed<FieldSpec[]>(() => [
    { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })) },
    { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date() },
  ]);

  constructor() { this.load(); }

  isNext(f: FundingSourceDto, i: FundingInstalmentDto): boolean { return f.instalments.find((x) => x.status === 'scheduled')?.id === i.id; }
  startPay(i: FundingInstalmentDto): void { this.errors.set(null); this.paying.set(i); }

  complete(): void {
    const a = this.acq();
    if (a && a.cashAtCompletion > 0 && !a.payFromAccountId) { this.errors.set(null); this.choosingBank.set(true); return; }
    this.modal.confirm({ nzTitle: 'Complete this purchase?', nzContent: 'The property is capitalised and the instalment schedules are generated. Financing cannot be changed afterwards.', nzOkText: 'Complete', nzOnOk: () => this.run(this.api.complete(this.id), (a) => doneText('completed', 'Purchase', a.number)) });
  }
  cancel(): void { this.modal.confirm({ nzTitle: 'Cancel this draft?', nzOkDanger: true, nzOkText: 'Cancel draft', nzOnOk: () => this.run(this.api.cancel(this.id), (a) => doneText('cancelled', 'Purchase draft', a.number)) }); }

  pay(v: Record<string, unknown>): void {
    const i = this.paying();
    if (!i) return;
    this.busy.set(true);
    this.api.payInstalment(i.id, v['bankAccountId'] as string, v['date'] as string).subscribe({
      next: () => { this.busy.set(false); this.paying.set(null); this.message.success(`Instalment ${i.sequence} paid for purchase ${this.acq()?.number}`); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not pay the instalment'); },
    });
  }

  private run(call: ReturnType<AcquisitionsApi['complete']>, success: (a: AcquisitionDto) => string): Promise<void> {
    this.busy.set(true);
    return new Promise((resolve) => call.subscribe({
      next: (a) => { this.busy.set(false); this.acq.set(a); this.message.success(success(a)); resolve(); },
      error: (e) => { this.busy.set(false); this.message.error(problemMessage(e, 'Action failed')); resolve(); },
    }));
  }

  private load(): void { this.api.detail(this.id).subscribe((a) => this.acq.set(a)); }
}
