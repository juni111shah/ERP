import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { LeaveApi, LeaveTypeDto, LeaveTypesApi } from './hr.api';
import { asProblem } from '../../core/http/problem-details';

/** Kinds of leave: paid or unpaid, yearly entitlement, carry-forward cap and whether a request needs approval. */
@Component({
  selector: 'erp-leave-types',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Leave types</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search leave types" emptyText="No leave types yet" (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'hr.leave.approve'" nz-button (click)="applyDefaults()" data-testid="leave-defaults">Apply country defaults</button>
      <button toolbar *erpHasPermission="'hr.leave.approve'" nz-button nzType="primary" (click)="state.create()" data-testid="leave-type-new"><span nz-icon nzType="plus"></span>New leave type</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit leave type' : 'New leave type'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="leave-type-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class LeaveTypesPage {
  private readonly api = inject(LeaveTypesApi);
  private readonly message = inject(NzMessageService);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<LeaveTypeDto>({
    save: (id, body) => { const b = this.toBody(body as Record<string, unknown>); return id ? this.api.update(id, b) : this.api.create(b); }, reload: () => this.list.reload(), message: this.message, entityName: 'Leave type',
  });

  private readonly leaveApi = inject(LeaveApi);
  applyDefaults(): void {
    this.leaveApi.applyCountryDefaults().subscribe({ next: () => { this.message.success('Leave types and public holidays of the entity countries were added'); this.list.reload(); }, error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not apply the defaults') });
  }

  open(t: LeaveTypeDto): void {
    this.state.edit({
      ...t,
      tiersText: (t.tiers ?? []).map((x) => `${x.fromServiceYears}:${x.days}`).join(', '),
      bandsText: (t.payBands ?? []).map((x) => `${x.fromDay}:${x.payPercent}`).join(', '),
    } as LeaveTypeDto);
  }

  /** "5:30, 10:35" -> pairs; blank -> an empty list (clears them). */
  private pairs(text: unknown): [number, number][] {
    return String(text ?? '').split(',').map((p) => p.trim()).filter(Boolean).map((p) => p.split(':').map((n) => Number(n.trim())) as [number, number]).filter((p) => p.length === 2 && p.every((n) => Number.isFinite(n)));
  }

  private toBody(v: Record<string, unknown>): Record<string, unknown> {
    const { tiersText, bandsText, ...rest } = v;
    return {
      ...rest,
      tiers: this.pairs(tiersText).map(([fromServiceYears, days]) => ({ fromServiceYears, days })),
      payBands: this.pairs(bandsText).map(([fromDay, payPercent]) => ({ fromDay, payPercent })),
    };
  }
  readonly columns: TableColumn<LeaveTypeDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '110px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'isPaid', label: 'Paid', value: (t) => (t.isPaid ? 'Paid' : 'Unpaid'), width: '90px' },
    { key: 'annualDays', label: 'Days a year', value: (t) => (t.annualDays > 0 ? String(t.annualDays) : 'No limit'), width: '120px' },
    { key: 'carryForwardCap', label: 'Carry forward', value: (t) => String(t.carryForwardCap), width: '120px' },
    { key: 'requiresApproval', label: 'Approval', value: (t) => (t.requiresApproval ? 'Needed' : 'Automatic'), width: '110px' },
    { key: 'isActive', label: 'Active', value: (t) => (t.isActive ? 'Yes' : 'No'), width: '80px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 100 },
    { key: 'isPaid', label: 'Paid leave', type: 'switch', default: true, hint: 'Unpaid leave is deducted from salary like an unpaid absence.' },
    { key: 'annualDays', label: 'Days a year', type: 'number', min: 0, max: 366, default: 0, hint: '0 = no yearly limit.' },
    { key: 'carryForwardCap', label: 'Carry forward (max days)', type: 'number', min: 0, max: 366, default: 0 },
    { key: 'requiresApproval', label: 'Needs approval', type: 'switch', default: true },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
    { key: 'proRataFirstYear', label: 'Pro-rata in the first year', type: 'switch', default: false, hint: 'New joiners get a share of the yearly days for the months left.' },
    { key: 'accrualMode', label: 'Entitlement builds up', type: 'select', default: 'upfront', options: [{ value: 'upfront', label: 'All at the start of the year' }, { value: 'monthly', label: 'Month by month' }] },
    { key: 'requiresAttachmentAfterDays', label: 'Document needed after (days)', type: 'number', min: 0, max: 366, hint: 'Longer requests need a supporting document (for example a medical certificate). Blank = never.' },
    { key: 'encashable', label: 'Paid out when the employee leaves', type: 'switch', default: false },
    { key: 'tiersText', label: 'Service-based entitlement', type: 'text', maxLength: 200, hint: 'years:days pairs, e.g. 0:21, 5:30 = 21 days, 30 days after 5 years of service.' },
    { key: 'bandsText', label: 'Pay bands (sick leave)', type: 'text', maxLength: 200, hint: 'day:percent pairs, e.g. 1:100, 16:50, 31:0 = full pay, then half, then unpaid. Counted per employee per year.' },
  ];
}
