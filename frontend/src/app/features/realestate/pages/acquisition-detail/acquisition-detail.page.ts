import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal, untracked } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { Observable } from 'rxjs';
import { AccountsApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { AcquisitionDto, AcquisitionsApi, DESCRIPTION_COLUMNS, FundingInstalmentDto, FundingSourceDto, humanize } from '@core/api/realestate.api';
import { doneText } from '@core/format/done-text';
import { asProblem, problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { ClientPager, PaginationBarComponent } from '@shared/components/pagination-bar/pagination-bar.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RecordHistoryComponent } from '@shared/components/record-history/record-history.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { itemsSignal } from '@shared/utils/items-signal';
import { accountOptions, bankAccounts } from '@shared/utils/payment-accounts';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { acquisitionWithBank } from '../../realestate-acquisitions';

/** Instalments shown per page of a funding schedule. */
const SCHEDULE_PAGE_SIZE = 12;

/** One acquisition: cost summary, funding sources with their instalment schedules, completion and instalment payments. */
@Component({
  selector: 'erp-acquisition-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatCardComponent, RecordHistoryComponent, TranslatePipe, DecimalPipe, RouterLink, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzAlertModule, NzButtonModule, NzCardModule, NzDescriptionsModule, NzModalModule, NzTableModule, NzTabsModule, PaginationBarComponent],
  templateUrl: './acquisition-detail.page.html',
})
export class AcquisitionDetailPage {
  private readonly acquisitionsApi = inject(AcquisitionsApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  private readonly accountPage = itemsSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }));
  private readonly bankOptions = computed(() => accountOptions(bankAccounts(this.accountPage().items)));
  readonly columns = DESCRIPTION_COLUMNS;
  readonly humanize = humanize;
  readonly acq = signal<AcquisitionDto | null>(null);
  private readonly pagers = new Map<string, ClientPager>();
  readonly busy = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly paying = signal<FundingInstalmentDto | null>(null);
  readonly choosingBank = signal(false);
  readonly bankFields = computed<FieldSpec[]>(() => [
    { key: 'payFromAccountId', label: 'Pay from', type: 'select', required: true, hint: 'The down payment and fees are paid from this account when the purchase completes.', options: this.bankOptions() },
  ]);
  readonly payFields = computed<FieldSpec[]>(() => [
    { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: this.bankOptions() },
    { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date() },
  ]);

  constructor() { this.load(); }

  /** Page state per funding schedule; opens on the page holding the next instalment due. */
  pager(f: FundingSourceDto): ClientPager {
    let p = this.pagers.get(f.id);
    if (!p) {
      const created = new ClientPager(SCHEDULE_PAGE_SIZE);
      p = created;
      const next = f.instalments.findIndex((i) => i.status === 'scheduled');
      if (next > 0) untracked(() => created.page.set(Math.floor(next / SCHEDULE_PAGE_SIZE) + 1)); // runs while the template renders
      this.pagers.set(f.id, p);
    }
    return p;
  }

  /** A draft with cash to pay and no bank cannot complete, and has no other screen to set one. */
  saveBank(v: Record<string, unknown>): void {
    const a = this.acq();
    if (!a) return;
    this.busy.set(true); this.errors.set(null);
    this.acquisitionsApi.save(a.id, acquisitionWithBank(a, v['payFromAccountId'] as string)).subscribe({
      next: (saved) => { this.busy.set(false); this.acq.set(saved); this.choosingBank.set(false); this.message.success(`Bank account saved for purchase ${saved.number}`); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); if (!p?.errors) this.message.error(p?.detail ?? p?.title ?? 'Could not save the bank account'); },
    });
  }

  isNext(f: FundingSourceDto, i: FundingInstalmentDto): boolean { return f.instalments.find((x) => x.status === 'scheduled')?.id === i.id; }
  startPay(i: FundingInstalmentDto): void { this.errors.set(null); this.paying.set(i); }

  complete(): void {
    const a = this.acq();
    if (a && a.cashAtCompletion > 0 && !a.payFromAccountId) { this.errors.set(null); this.choosingBank.set(true); return; }
    this.modal.confirm({ nzTitle: 'Complete this purchase?', nzContent: 'The property is capitalised and the instalment schedules are generated. Financing cannot be changed afterwards.', nzOkText: 'Complete', nzOnOk: () => this.run(this.acquisitionsApi.complete(this.id), (a) => doneText('completed', 'Purchase', a.number)) });
  }
  cancel(): void { this.modal.confirm({ nzTitle: 'Cancel this draft?', nzOkDanger: true, nzOkText: 'Cancel draft', nzOnOk: () => this.run(this.acquisitionsApi.cancel(this.id), (a) => doneText('cancelled', 'Purchase draft', a.number)) }); }

  pay(v: Record<string, unknown>): void {
    const i = this.paying();
    if (!i) return;
    this.busy.set(true);
    this.acquisitionsApi.payInstalment(i.id, v['bankAccountId'] as string, v['date'] as string).subscribe({
      next: () => { this.busy.set(false); this.paying.set(null); this.message.success(`Instalment ${i.sequence} paid for purchase ${this.acq()?.number}`); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not pay the instalment'); },
    });
  }

  /** Runs a workflow action and shows the acquisition it returns; resolves when done, so a confirm dialog can wait for it. */
  private run(call: Observable<AcquisitionDto>, success: (a: AcquisitionDto) => string): Promise<void> {
    this.busy.set(true);
    return new Promise((resolve) => call.subscribe({
      next: (a) => { this.busy.set(false); this.acq.set(a); this.message.success(success(a)); resolve(); },
      error: (e) => { this.busy.set(false); this.message.error(problemMessage(e, 'Action failed')); resolve(); },
    }));
  }

  private load(): void { this.acquisitionsApi.detail(this.id).subscribe((a) => this.acq.set(a)); }
}
