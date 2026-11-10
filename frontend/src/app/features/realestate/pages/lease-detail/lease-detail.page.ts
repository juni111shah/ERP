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
  styles: [`.num { text-align: end; white-space: nowrap; }`],
  template: `
    @if (failed()) {
      <p><a routerLink="/real-estate/leases">← Leases</a></p>
      <erp-state-panel kind="error" title="This lease could not be loaded" hint="It may have been removed, or your connection dropped." actionLabel="Try again" (action)="reload()" />
    }
    @if (lease(); as l) {
      <p><a routerLink="/real-estate/leases">← Leases</a></p>
      <h1 data-testid="lease-title" style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin:0 0 12px;font-size:20px;font-weight:600">{{ l.number }} <erp-status-tag [status]="l.status" /></h1>
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin:12px 0" data-testid="lease-actions">
        @if (l.status === 'draft') {
          <button *erpHasPermission="'realestate.leases.activate'" nz-button nzType="primary" [disabled]="busy()" (click)="activate()" data-testid="lease-activate">Activate</button>
          <button *erpHasPermission="'realestate.leases.edit'" nz-button (click)="state.edit(l)" data-testid="lease-edit">Edit</button>
          <button *erpHasPermission="'realestate.leases.edit'" nz-button nzDanger [disabled]="busy()" (click)="cancel()" data-testid="lease-cancel">Cancel draft</button>
        }
        @if (l.status === 'active' || l.status === 'holdingOver') {
          <button *erpHasPermission="'realestate.leases.renew'" nz-button nzType="primary" (click)="startAction('renew')" data-testid="lease-renew">Renew</button>
          <button *erpHasPermission="'realestate.leases.terminate'" nz-button nzDanger (click)="startAction('terminate')" data-testid="lease-terminate">Terminate early</button>
        }
        @if (l.status === 'active') {
          <button *erpHasPermission="'realestate.leases.edit'" nz-button [disabled]="busy()" (click)="toggleHoldingOver(l)" data-testid="lease-holding-over">{{ l.holdingOverFlag ? 'Tenant is leaving' : 'Tenant will hold over' }}</button>
        }
        @if (l.status === 'terminated') {
          <button *erpHasPermission="'realestate.leases.terminate'" nz-button nzType="primary" [disabled]="busy()" (click)="creditNotes()" data-testid="lease-credit-notes">Raise credit notes for rent after termination</button>
        }
        @if (l.status === 'holdingOver') {
          <button *erpHasPermission="'realestate.leases.terminate'" nz-button [disabled]="busy()" (click)="moveOut()" data-testid="lease-move-out">Confirm move-out</button>
        }
        <a [href]="'/print/lease/' + l.id" target="_blank" nz-button data-testid="lease-print">
          <span nz-icon nzType="printer"></span>Print contract
        </a>
      </div>
      <div class="erp-stat-grid" data-testid="lease-kpis">
        <erp-stat-card label="Annual rent" [value]="l.annualRent" format="number" [suffix]="l.currencyCode" [caption]="planLabel(l.paymentFrequency)" [tone]="0" />
        <erp-stat-card label="Rent for term" [value]="l.rentForTerm" format="number" [suffix]="l.currencyCode" [tone]="1" />
        <erp-stat-card label="Security deposit (agreed)" [value]="l.securityDepositAmount" format="number" [suffix]="l.currencyCode" [tone]="2" />
        <erp-stat-card label="Days to expiry" [value]="l.daysToExpiry ?? null" format="number" [tone]="l.daysToExpiry !== null && l.daysToExpiry !== undefined && l.daysToExpiry < 30 ? 'danger' : 3" />
      </div>
      <nz-card style="margin-top:16px" nzTitle="Details">
        <nz-descriptions nzBordered [nzColumn]="descriptionColumns">
          <nz-descriptions-item nzTitle="Tenant">{{ l.tenantName }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Unit"><a [routerLink]="['/real-estate/properties', l.propertyId]">{{ l.unitNumber }}</a></nz-descriptions-item>
          <nz-descriptions-item nzTitle="Term">{{ l.startDate | erpDate }} → {{ l.endDate | erpDate }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Rent-free days">{{ l.graceDays }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Notice period">{{ l.noticePeriodDays }} {{ 'ui.common.days' | translate: 'days' }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="VAT on rent">{{ humanize(l.suggestedVat) }}</nz-descriptions-item>
          @if (l.activatedAt) { <nz-descriptions-item nzTitle="Activated">{{ l.activatedAt | erpDate }}</nz-descriptions-item> }
          @if (l.terminationDate) { <nz-descriptions-item nzTitle="Terminated on">{{ l.terminationDate | erpDate }} · {{ l.terminationReason }}</nz-descriptions-item> }
          @if (l.renewalOfLeaseId) { <nz-descriptions-item nzTitle="Renewal of"><a [routerLink]="['/real-estate/leases', l.renewalOfLeaseId]">previous lease</a></nz-descriptions-item> }
          @if (l.renewedAsLeaseId) { <nz-descriptions-item nzTitle="Renewed as"><a [routerLink]="['/real-estate/leases', l.renewedAsLeaseId]">renewal lease</a></nz-descriptions-item> }
          @for (cf of customFieldList(l); track cf[0]) { <nz-descriptions-item [nzTitle]="cf[0]">{{ cf[1] }}</nz-descriptions-item> }
          @if (l.notes) { <nz-descriptions-item nzTitle="Notes" [nzSpan]="2">{{ l.notes }}</nz-descriptions-item> }
        </nz-descriptions>
      </nz-card>
      <nz-card style="margin-top:16px" nzTitle="Rent schedule">
        @if (l.schedule.length) {
          <nz-table #t [nzData]="l.schedule" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rent-schedule">
            <thead><tr><th>#</th><th>Period</th><th>Due</th><th class="num">Rent excl. VAT ({{ l.currencyCode }})</th><th>Status</th></tr></thead>
            <tbody>
              @for (i of t.data; track i.id) {
                <tr><td>{{ i.sequence }}</td><td>{{ i.periodFrom | erpDate }} → {{ i.periodTo | erpDate }}</td><td>{{ i.dueDate | erpDate }}</td><td class="num">{{ i.amount.toLocaleString() }}</td><td><erp-status-tag [status]="i.status" /></td></tr>
              }
            </tbody>
          </nz-table>
        } @else { <p>The rent schedule is generated when the lease is activated.</p> }
      </nz-card>
      @if (l.status !== 'draft') { <erp-lease-deposit [lease]="l" /> }
      <erp-attachments ownerType="lease" [ownerId]="l.id" />
      <nz-card *erpHasPermission="'core.audit.read'" style="margin-top:16px" [nzTitle]="'history.title' | translate: 'History'"><erp-record-history recordType="Lease" [recordId]="l.id" /></nz-card>
      <erp-record-drawer title="Edit draft lease" [open]="state.open()" [fields]="editFields" [value]="$any(state.editing())" customFieldEntityType="realestate.lease"
                         [saving]="state.saving()" [errors]="state.errors()" testId="lease-form" (submitted)="state.submit($event)" (closed)="state.close()" />
      <erp-record-drawer [title]="action() === 'renew' ? 'Renew lease' : 'Terminate lease'" [open]="!!action()" [fields]="actionFields()" [saving]="actionSaving()" [errors]="actionErrors()"
                         [width]="440" testId="lease-action-form" (submitted)="runAction($event)" (closed)="action.set(null)" />
    }`,
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
