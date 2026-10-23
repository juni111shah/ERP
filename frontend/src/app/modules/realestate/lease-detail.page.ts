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
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { leaseFields } from './leases.page';
import { DESCRIPTION_COLUMNS, LeaseDto, LeasesApi, PAYMENT_PLANS, RealEstateLookups, TerminationResult, humanize } from './realestate.api';
import { LeaseDepositComponent } from './lease-deposit.component';
import { AttachmentsComponent } from '../../shared/attachments.component';
import { StatusTagComponent } from './status-tag.component';

type Action = 'terminate' | 'renew';

/** Lease detail: KPIs, the rent schedule and the workflow actions for the lease's current status. */
@Component({
  selector: 'erp-lease-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink, LeaseDepositComponent, AttachmentsComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzCardModule, NzDescriptionsModule, NzGridModule, NzModalModule,
    NzStatisticModule, NzTableModule, NzTagModule],
  template: `
    @if (lease(); as l) {
      <p><a routerLink="/real-estate/leases">← Leases</a></p>
      <h1 class="erp-page-title" data-testid="lease-title">{{ l.number }} <erp-status-tag [status]="l.status" /></h1>
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
      </div>
      <div nz-row [nzGutter]="[16, 16]">
        <div nz-col [nzXs]="24" [nzMd]="6"><nz-card><nz-statistic nzTitle="Annual rent" [nzValue]="l.annualRent.toLocaleString()" [nzSuffix]="l.currencyCode"></nz-statistic><small>{{ planLabel(l.paymentFrequency) }}</small></nz-card></div>
        <div nz-col [nzXs]="24" [nzMd]="6"><nz-card><nz-statistic nzTitle="Rent for term" [nzValue]="l.rentForTerm.toLocaleString()" [nzSuffix]="l.currencyCode"></nz-statistic></nz-card></div>
        <div nz-col [nzXs]="24" [nzMd]="6"><nz-card><nz-statistic nzTitle="Security deposit (agreed)" [nzValue]="l.securityDepositAmount.toLocaleString()" [nzSuffix]="l.currencyCode"></nz-statistic></nz-card></div>
        <div nz-col [nzXs]="24" [nzMd]="6"><nz-card><nz-statistic nzTitle="Days to expiry" [nzValue]="l.daysToExpiry ?? '—'"></nz-statistic></nz-card></div>
      </div>
      <nz-card style="margin-top:16px" nzTitle="Details">
        <nz-descriptions nzBordered [nzColumn]="descriptionColumns">
          <nz-descriptions-item nzTitle="Unit">{{ l.unitNumber }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Term">{{ l.startDate }} → {{ l.endDate }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Rent-free days">{{ l.graceDays }}</nz-descriptions-item>
          <nz-descriptions-item nzTitle="Notice period">{{ l.noticePeriodDays }} days</nz-descriptions-item>
          <nz-descriptions-item nzTitle="VAT on rent">{{ humanize(l.suggestedVat) }}</nz-descriptions-item>
          @if (l.activatedAt) { <nz-descriptions-item nzTitle="Activated">{{ l.activatedAt | date: 'medium' }}</nz-descriptions-item> }
          @if (l.terminationDate) { <nz-descriptions-item nzTitle="Terminated on">{{ l.terminationDate }} · {{ l.terminationReason }}</nz-descriptions-item> }
          @if (l.renewalOfLeaseId) { <nz-descriptions-item nzTitle="Renewal of"><a [routerLink]="['/real-estate/leases', l.renewalOfLeaseId]">previous lease</a></nz-descriptions-item> }
          @if (l.renewedAsLeaseId) { <nz-descriptions-item nzTitle="Renewed as"><a [routerLink]="['/real-estate/leases', l.renewedAsLeaseId]">renewal lease</a></nz-descriptions-item> }
          @for (cf of customFieldList(l); track cf[0]) { <nz-descriptions-item [nzTitle]="cf[0]">{{ cf[1] }}</nz-descriptions-item> }
          @if (l.notes) { <nz-descriptions-item nzTitle="Notes" [nzSpan]="2">{{ l.notes }}</nz-descriptions-item> }
        </nz-descriptions>
      </nz-card>
      <nz-card style="margin-top:16px" nzTitle="Rent schedule">
        @if (l.schedule.length) {
          <nz-table #t [nzData]="l.schedule" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rent-schedule">
            <thead><tr><th>#</th><th>Period</th><th>Due</th><th>Rent (excl. VAT)</th><th>Status</th></tr></thead>
            <tbody>
              @for (i of t.data; track i.id) {
                <tr><td>{{ i.sequence }}</td><td>{{ i.periodFrom }} → {{ i.periodTo }}</td><td>{{ i.dueDate }}</td><td>{{ i.amount.toLocaleString() }}</td><td><erp-status-tag [status]="i.status" /></td></tr>
              }
            </tbody>
          </nz-table>
        } @else { <p>The rent schedule is generated when the lease is activated.</p> }
      </nz-card>
      @if (l.status !== 'draft') { <erp-lease-deposit [lease]="l" /> }
      <erp-attachments ownerType="lease" [ownerId]="l.id" />
      <erp-record-drawer title="Edit draft lease" [open]="state.open()" [fields]="editFields" [value]="$any(state.editing())" customFieldEntityType="realestate.lease"
                         [saving]="state.saving()" [errors]="state.errors()" testId="lease-form" (submitted)="state.submit($event)" (closed)="state.close()" />
      <erp-record-drawer [title]="action() === 'renew' ? 'Renew lease' : 'Terminate lease'" [open]="!!action()" [fields]="actionFields()" [saving]="actionSaving()" [errors]="actionErrors()"
                         [width]="440" testId="lease-action-form" (submitted)="runAction($event)" (closed)="action.set(null)" />
    }`,
})
export class LeaseDetailPage {
  private readonly api = inject(LeasesApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly modal = inject(NzModalService);
  readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly humanize = humanize;
  readonly descriptionColumns = DESCRIPTION_COLUMNS;
  readonly lease = signal<LeaseDto | null>(null);
  readonly busy = signal(false);
  readonly editFields = leaseFields(inject(RealEstateLookups), true);
  readonly state = new MasterDataState<LeaseDto>({
    save: (id, body) => this.api.saveDraft(id, body), reload: () => this.load(), message: this.message, entityName: 'Lease',
  });
  readonly action = signal<Action | null>(null);
  readonly actionSaving = signal(false);
  readonly actionErrors = signal<ProblemDetails | null>(null);

  constructor() { this.load(); }

  planLabel(n: number): string { return PAYMENT_PLANS.find((p) => p.value === n)?.label ?? `${n} a year`; }

  customFieldList(l: LeaseDto): [string, string][] {
    return Object.entries(l.customFields ?? {}).map(([k, v]) => [humanize(k.replace(/^cf_/, '').replace(/_/g, ' ')), String(v)]);
  }

  /** A computed (stable identity) so the drawer keeps what the user typed. */
  readonly actionFields = computed<FieldSpec[]>(() => this.action() === 'renew'
    ? [
        { key: 'endDate', label: 'New end date', type: 'date', hint: 'Blank = one year from the old end date.' },
        { key: 'annualRent', label: 'New annual rent', type: 'number', min: 0, default: this.lease()?.annualRent },
        { key: 'paymentFrequency', label: 'Payment plan', type: 'select', options: PAYMENT_PLANS, default: this.lease()?.paymentFrequency },
        { key: 'notes', label: 'Notes', type: 'textarea' },
      ]
    : [
        { key: 'date', label: 'Termination date', type: 'date', required: true },
        { key: 'reason', label: 'Reason', type: 'text', required: true, maxLength: 500 },
      ]);

  startAction(a: Action): void { this.actionErrors.set(null); this.action.set(a); }

  activate(): void { this.run(this.api.activate(this.id), 'Lease activated'); }

  cancel(): void {
    this.modal.confirm({ nzTitle: 'Cancel this draft lease?', nzOkText: 'Cancel draft', nzOkDanger: true, nzOnOk: () => this.run(this.api.cancel(this.id), 'Draft cancelled') });
  }

  toggleHoldingOver(l: LeaseDto): void { this.run(this.api.holdingOver(this.id, !l.holdingOverFlag), 'Updated'); }

  creditNotes(): void {
    this.busy.set(true);
    this.api.terminationCredits(this.id).subscribe({
      next: (r) => { this.busy.set(false); this.message.success(r.length ? `${r.length} credit note(s) issued, ${r.reduce((s, c) => s + c.amount, 0).toLocaleString()} credited` : 'Nothing to credit'); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not raise the credit notes'); },
    });
  }

  moveOut(): void { this.run(this.api.moveOut(this.id), 'Move-out confirmed'); }

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
        if (kind === 'renew') { this.message.success('Renewal draft created'); void this.router.navigate(['/real-estate/leases', (result as LeaseDto).id]); return; }
        const t = result as { cancelledInstalments: number; instalmentsNeedingCreditNote: unknown[] };
        this.message.success(`Lease terminated · ${t.cancelledInstalments} instalments cancelled` + (t.instalmentsNeedingCreditNote.length ? ` · ${t.instalmentsNeedingCreditNote.length} invoiced instalments need a credit note` : ''));
        this.load();
      },
      error: (e) => { this.actionSaving.set(false); const p = asProblem(e); this.actionErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); },
    });
  }

  private run(call: ReturnType<LeasesApi['activate']>, success: string): void {
    this.busy.set(true);
    call.subscribe({
      next: (l) => { this.busy.set(false); this.lease.set(l); this.message.success(success); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Action failed'); },
    });
  }

  private load(): void { this.api.detail(this.id).subscribe((l) => this.lease.set(l)); }
}
