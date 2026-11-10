import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { Observable } from 'rxjs';
import { ProblemDetails } from '@core/api/api.models';
import { DESCRIPTION_COLUMNS, LeaseDto, LeasesApi, PAYMENT_PLANS, RealEstateLookups, TerminationResult, humanize } from '@core/api/realestate.api';
import { fromIsoDate } from '@core/format/from-iso-date';
import { asProblem } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RecordHistoryComponent } from '@shared/components/record-history/record-history.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { MasterDataState } from '@shared/utils/master-data-state';
import { LeaseDepositComponent } from '../../components/lease-deposit/lease-deposit.component';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { defaultLeaseEnd, leaseFields } from '../../realestate-forms';

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
  private readonly leasesApi = inject(LeasesApi);
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
    save: (id, body) => this.leasesApi.saveDraft(id, body), reload: () => this.load(), message: this.message, entityName: 'Lease',
  });
  readonly action = signal<Action | null>(null);
  readonly actionSaving = signal(false);
  readonly actionErrors = signal<ProblemDetails | null>(null);

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

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((p) => { this.id = p.get('id')!; this.lease.set(null); this.load(); });
  }

  planLabel(n: number): string { return PAYMENT_PLANS.find((p) => p.value === n)?.label ?? `${n} ${this.i18n.t('ui.re.aYear', 'a year')}`; }

  customFieldList(l: LeaseDto): [string, string][] {
    return Object.entries(l.customFields ?? {}).map(([k, v]) => [humanize(k.replace(/^cf_/, '').replace(/_/g, ' ')), String(v)]);
  }

  startAction(a: Action): void { this.actionErrors.set(null); this.action.set(a); }

  activate(): void { this.run(this.leasesApi.activate(this.id), (l) => `Lease ${l.number ?? l.unitNumber} activated`); }

  cancel(): void {
    this.modal.confirm({ nzTitle: 'Cancel this draft lease?', nzOkText: 'Cancel draft', nzOkDanger: true, nzOnOk: () => this.run(this.leasesApi.cancel(this.id), (l) => `Draft lease for unit ${l.unitNumber} cancelled`) });
  }

  toggleHoldingOver(l: LeaseDto): void { this.run(this.leasesApi.holdingOver(this.id, !l.holdingOverFlag), (u) => `Holding over switched ${u.holdingOverFlag ? 'on' : 'off'} for lease ${u.number ?? u.unitNumber}`); }

  /** Raises credit notes for the invoiced instalments a termination cancelled. */
  creditNotes(): void {
    this.busy.set(true);
    this.leasesApi.terminationCredits(this.id).subscribe({
      next: (r) => { this.busy.set(false); this.message.success(r.length ? `${r.length} ${this.i18n.t('ui.msg.cnIssued', 'credit note(s) issued')}, ${r.reduce((s, c) => s + c.amount, 0).toLocaleString()} ${this.i18n.t('ui.msg.credited', 'credited')}` : 'Nothing to credit'); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not raise the credit notes'); },
    });
  }

  moveOut(): void { this.run(this.leasesApi.moveOut(this.id), (l) => `Move-out confirmed for unit ${l.unitNumber}`); }

  /** The action drawer's OK: renew (then open the renewal draft) or terminate. */
  runAction(values: Record<string, unknown>): void {
    const kind = this.action();
    if (!kind) return;
    this.actionSaving.set(true);
    this.actionErrors.set(null);
    const call: Observable<LeaseDto | TerminationResult> = kind === 'renew'
      ? this.leasesApi.renew(this.id, values)
      : this.leasesApi.terminate(this.id, { date: values['date'] as string, reason: values['reason'] as string });
    call.subscribe({
      next: (result) => {
        this.actionSaving.set(false);
        this.action.set(null);
        if (kind === 'renew') this.renewed(result as LeaseDto);
        else this.terminated(result as TerminationResult);
      },
      error: (e) => { this.actionSaving.set(false); const p = asProblem(e); this.actionErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); },
    });
  }

  reload(): void { this.load(); }

  /** Mirrors RenewLease on the server: the renewal starts the day after the old end date and runs one year. */
  private renewalEnd(): Date | undefined {
    const end = this.lease()?.endDate;
    return end ? defaultLeaseEnd(fromIsoDate(end, 1)) : undefined;
  }

  private renewed(renewal: LeaseDto): void {
    this.message.success(`Renewal draft created for unit ${renewal.unitNumber}`);
    void this.router.navigate(['/real-estate/leases', renewal.id]);
  }

  private terminated(t: TerminationResult): void {
    this.message.success(`${this.i18n.t('ui.msg.leaseTerminated', 'Lease terminated')} · ${t.cancelledInstalments} ${this.i18n.t('ui.msg.instalmentsCancelled', 'instalments cancelled')}` + (t.instalmentsNeedingCreditNote.length ? ` · ${t.instalmentsNeedingCreditNote.length} ${this.i18n.t('ui.msg.instalmentsNeedCn', 'invoiced instalments need a credit note')}` : ''));
    this.load();
  }

  private run(call: Observable<LeaseDto>, success: (l: LeaseDto) => string): void {
    this.busy.set(true);
    call.subscribe({
      next: (l) => { this.busy.set(false); this.lease.set(l); this.message.success(success(l)); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); },
    });
  }

  private load(): void { this.failed.set(false); this.leasesApi.detail(this.id).subscribe({ next: (l) => this.lease.set(l), error: () => this.failed.set(true) }); }
}
