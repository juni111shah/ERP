import { RecordHistoryComponent } from '@shared/components/record-history/record-history.component';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProblemDetails } from '@core/api/api.models';
import { asProblem } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { defaultLeaseEnd, leaseFields } from '../leases/leases.page';
import { DESCRIPTION_COLUMNS, LeaseDto, LeasesApi, PAYMENT_PLANS, RealEstateLookups, TerminationResult, humanize } from '@core/api/realestate.api';
import { LeaseDepositComponent } from '../../components/lease-deposit/lease-deposit.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { I18nService } from '@core/i18n/i18n.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { NzIconModule } from 'ng-zorro-antd/icon';

type Action = 'terminate' | 'renew';

/** Lease detail: KPIs, the rent schedule and the workflow actions for the lease's current status. */
@Component({
  selector: 'erp-lease-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatCardComponent, StatePanelComponent, NzIconModule, RecordHistoryComponent, TranslatePipe, RouterLink, LeaseDepositComponent, AttachmentsComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzCardModule, NzDescriptionsModule, NzGridModule, NzModalModule,
    NzStatisticModule, NzTableModule, NzTagModule],
  styleUrl: './lease-detail.page.scss',
  templateUrl: './lease-detail.page.html',
})
export class LeaseDetailPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(LeasesApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly modal = inject(NzModalService);
  private readonly route = inject(ActivatedRoute);
  /** Follows the route: Renew and the renewal links open another lease on this same page. */
  id = this.route.snapshot.paramMap.get('id')!;
  readonly humanize = humanize;
  readonly descriptionColumns = DESCRIPTION_COLUMNS;
  readonly lease = signal<LeaseDto | null>(null);
  readonly failed = signal(false);
  readonly busy = signal(false);
  readonly editFields = leaseFields(inject(RealEstateLookups), true);
  readonly state = new MasterDataState<LeaseDto>({
    save: (id, body) => this.api.saveDraft(id, body), reload: () => this.load(), message: this.message, entityName: 'Lease',
  });
  readonly action = signal<Action | null>(null);
  readonly actionSaving = signal(false);
  readonly actionErrors = signal<ProblemDetails | null>(null);

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((p) => { this.id = p.get('id')!; this.lease.set(null); this.load(); });
  }

  planLabel(n: number): string { return PAYMENT_PLANS.find((p) => p.value === n)?.label ?? `${n} ${this.i18n.t('ui.re.aYear', 'a year')}`; }

  customFieldList(l: LeaseDto): [string, string][] {
    return Object.entries(l.customFields ?? {}).map(([k, v]) => [humanize(k.replace(/^cf_/, '').replace(/_/g, ' ')), String(v)]);
  }

  /** Mirrors RenewLease on the server: the renewal starts the day after the old end date and runs one year. */
  private renewalEnd(): Date | undefined {
    const end = this.lease()?.endDate;
    if (!end) return undefined;
    const [y, m, d] = end.split('-').map(Number);
    return defaultLeaseEnd(new Date(y, m - 1, d + 1));
  }

  /** A computed (stable identity) so the drawer keeps what the user typed. */
  readonly actionFields = computed<FieldSpec[]>(() => this.action() === 'renew'
    ? [
        { key: 'endDate', label: 'New end date', type: 'date', hint: 'Blank = one year from the old end date.', default: this.renewalEnd() },
        { key: 'annualRent', label: 'New annual rent', type: 'number', min: 0, default: this.lease()?.annualRent },
        { key: 'paymentFrequency', label: 'Payment plan', type: 'select', options: PAYMENT_PLANS, default: this.lease()?.paymentFrequency },
        { key: 'notes', label: 'Notes', type: 'textarea' },
      ]
    : [
        { key: 'date', label: 'Termination date', type: 'date', required: true },
        { key: 'reason', label: 'Reason', type: 'text', required: true, maxLength: 500 },
      ]);

  startAction(a: Action): void { this.actionErrors.set(null); this.action.set(a); }

  activate(): void { this.run(this.api.activate(this.id), (l) => `Lease ${l.number ?? l.unitNumber} activated`); }

  cancel(): void {
    this.modal.confirm({ nzTitle: 'Cancel this draft lease?', nzOkText: 'Cancel draft', nzOkDanger: true, nzOnOk: () => this.run(this.api.cancel(this.id), (l) => `Draft lease for unit ${l.unitNumber} cancelled`) });
  }

  toggleHoldingOver(l: LeaseDto): void { this.run(this.api.holdingOver(this.id, !l.holdingOverFlag), (u) => `Holding over switched ${u.holdingOverFlag ? 'on' : 'off'} for lease ${u.number ?? u.unitNumber}`); }

  creditNotes(): void {
    this.busy.set(true);
    this.api.terminationCredits(this.id).subscribe({
      next: (r) => { this.busy.set(false); this.message.success(r.length ? `${r.length} ${this.i18n.t('ui.msg.cnIssued', 'credit note(s) issued')}, ${r.reduce((s, c) => s + c.amount, 0).toLocaleString()} ${this.i18n.t('ui.msg.credited', 'credited')}` : 'Nothing to credit'); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not raise the credit notes'); },
    });
  }

  moveOut(): void { this.run(this.api.moveOut(this.id), (l) => `Move-out confirmed for unit ${l.unitNumber}`); }

  runAction(values: Record<string, unknown>): void {
    const kind = this.action();
    if (!kind) return;
    this.actionSaving.set(true);
    this.actionErrors.set(null);
    const call: Observable<LeaseDto | TerminationResult> = kind === 'renew'
      ? this.api.renew(this.id, values)
      : this.api.terminate(this.id, { date: values['date'] as string, reason: values['reason'] as string });
    call.subscribe({
      next: (result) => {
        this.actionSaving.set(false);
        this.action.set(null);
        if (kind === 'renew') { this.message.success(`Renewal draft created for unit ${(result as LeaseDto).unitNumber}`); void this.router.navigate(['/real-estate/leases', (result as LeaseDto).id]); return; }
        const t = result as { cancelledInstalments: number; instalmentsNeedingCreditNote: unknown[] };
        this.message.success(`${this.i18n.t('ui.msg.leaseTerminated', 'Lease terminated')} · ${t.cancelledInstalments} ${this.i18n.t('ui.msg.instalmentsCancelled', 'instalments cancelled')}` + (t.instalmentsNeedingCreditNote.length ? ` · ${t.instalmentsNeedingCreditNote.length} ${this.i18n.t('ui.msg.instalmentsNeedCn', 'invoiced instalments need a credit note')}` : ''));
        this.load();
      },
      error: (e) => { this.actionSaving.set(false); const p = asProblem(e); this.actionErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); },
    });
  }

  private run(call: ReturnType<LeasesApi['activate']>, success: (l: LeaseDto) => string): void {
    this.busy.set(true);
    call.subscribe({
      next: (l) => { this.busy.set(false); this.lease.set(l); this.message.success(success(l)); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); },
    });
  }

  reload(): void { this.load(); }
  private load(): void { this.failed.set(false); this.api.detail(this.id).subscribe({ next: (l) => this.lease.set(l), error: () => this.failed.set(true) }); }
}
