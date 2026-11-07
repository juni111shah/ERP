import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTableModule } from 'ng-zorro-antd/table';
import { EngagementDto, ServicesEngagementsApi, ServicesTimeEntriesApi, TimeEntrySummaryDto } from '../../core/api/services.api';
import { displayDate } from '../../core/format/display';
import { doneText } from '../../core/format/done-text';
import { problemMessage } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';
import { ActionFormState } from '../../shared/action-form-state';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

/** The hours logged against one engagement: log, change, delete (until approved), approve, and bill the approved time of a time-and-materials engagement. */
@Component({
  selector: 'erp-engagement-time',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, NzPopconfirmModule, NzTableModule, HasPermissionDirective, RecordDrawerComponent, StatusTagComponent],
  styles: [`.r { text-align: end; white-space: nowrap; } .muted { color: var(--erp-color-text-muted); } .bar { display: flex; gap: 8px; margin-bottom: 8px; flex-wrap: wrap; } .acts { display: flex; gap: 4px; flex-wrap: wrap; }`],
  template: `
    <div class="bar">
      <button *erpHasPermission="'services.timeentries.manage'" nz-button nzSize="small" type="button" [disabled]="!canLog()" (click)="form.show(null)" data-testid="time-log"><span nz-icon nzType="plus"></span>Log time</button>
      @if (engagement().type === 'timeAndMaterials') {
        <button *erpHasPermission="'services.engagements.manage'" nz-button nzSize="small" nzType="primary" type="button" [disabled]="!billable()" (click)="invoiceTime()" data-testid="time-invoice">Invoice approved time</button>
      }
    </div>
    @if (entries().length) {
      <nz-table [nzData]="entries()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="time-table">
        <thead><tr><th>Date</th><th>Who</th><th class="r">Hours</th><th class="r">Amount</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (e of entries(); track e.id) {
            <tr>
              <td>{{ fmt(e.activityDate) }}<div class="muted">{{ e.taskDescription }}</div></td><td>{{ e.consultantName }}</td>
              <td class="r">{{ e.hours | number: '1.0-2' }}</td><td class="r">{{ e.totalAmount | number: '1.2-2' }}</td>
              <td>
                <erp-status [value]="e.invoiceId ? 'invoiced' : e.isApproved ? 'approved' : 'pending'" />
                @if (!e.isBillable) { <span class="muted"> Not billable</span> }
              </td>
              <td>
                <div class="acts" *erpHasPermission="'services.timeentries.manage'">
                  @if (!e.isApproved) {
                    <button nz-button nzSize="small" nzType="primary" type="button" [disabled]="e.consultantName === me()" (click)="approve(e)" data-testid="time-approve">Approve</button>
                    <button nz-button nzSize="small" type="button" (click)="form.show(e)" data-testid="time-edit">Edit</button>
                    <button nz-button nzSize="small" nzDanger type="button" nz-popconfirm nzPopconfirmTitle="Delete this time entry?" (nzOnConfirm)="remove(e)" data-testid="time-delete">Delete</button>
                  }
                  @if (e.invoiceId) { <a [routerLink]="['/finance/invoices', e.invoiceId]" data-testid="time-invoice-link">Invoice</a> }
                </div>
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
    } @else { <p class="muted">No time has been logged against this engagement.</p> }
    <erp-record-drawer [title]="form.target() ? 'Edit time entry' : 'Log time'" [open]="form.open()" [fields]="fields" [value]="$any(form.target())" [saving]="form.saving()" [errors]="form.errors()"
                       testId="time-form" (submitted)="form.submit($event)" (closed)="form.close()" />`,
})
export class EngagementTimeComponent {
  readonly engagement = input.required<EngagementDto>();
  readonly changed = output<void>();
  private readonly api = inject(ServicesTimeEntriesApi);
  private readonly engagements = inject(ServicesEngagementsApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  readonly fmt = displayDate;
  readonly entries = signal<TimeEntrySummaryDto[]>([]);
  readonly me = computed(() => this.session.me()?.user.fullName ?? '');
  readonly canLog = computed(() => this.engagement().status !== 'cancelled');
  /** Something approved and not yet invoiced. */
  readonly billable = computed(() => this.entries().some((e) => e.isApproved && e.isBillable && !e.invoiceId));

  readonly fields: FieldSpec[] = [
    { key: 'activityDate', label: 'Date', type: 'date', required: true },
    { key: 'hours', label: 'Hours', type: 'number', required: true, min: 0.25, max: 24 },
    { key: 'hourlyRate', label: 'Hourly rate', type: 'number', required: true, min: 0, default: 0 },
    { key: 'taskDescription', label: 'What was done', type: 'textarea', required: true, maxLength: 500 },
    { key: 'isBillable', label: 'Billable', type: 'switch', default: true },
  ];

  readonly form = new ActionFormState<TimeEntrySummaryDto>({
    run: (p, row) => {
      const e = this.engagement();
      const user = this.session.me()?.user;
      const body = {
        legalEntityId: e.legalEntityId, engagementId: e.id, consultantUserId: row ? undefined : user?.id, consultantName: row ? row.consultantName : user?.fullName ?? '',
        activityDate: p['activityDate'], hours: Number(p['hours']), hourlyRate: Number(p['hourlyRate'] ?? 0), taskDescription: String(p['taskDescription'] ?? ''), isBillable: !!p['isBillable'],
      };
      return row ? this.api.update(row.id, body as never) : this.api.create(body as never);
    },
    success: () => doneText('saved', 'Time entry'), done: () => this.reload(true), message: this.message, failure: 'Could not save the time entry',
  });

  constructor() {
    effect(() => {
      this.engagement();
      untracked(() => this.reload(false));
    });
  }

  reload(notify: boolean): void {
    this.api.list({ page: 1, pageSize: 100, sort: '-activityDate', filters: [{ field: 'engagementId', op: 'eq', value: this.engagement().id }] }).subscribe({
      next: (r) => { this.entries.set(r.items); if (notify) this.changed.emit(); },
      error: () => this.entries.set([]),
    });
  }

  approve(e: TimeEntrySummaryDto): void {
    this.api.approve(e.id).subscribe({
      next: () => { this.message.success(doneText('approved', 'Time entry')); this.reload(true); },
      error: (err) => this.message.error(problemMessage(err, 'Could not approve the time entry')),
    });
  }

  remove(e: TimeEntrySummaryDto): void {
    this.api.delete(e.id).subscribe({
      next: () => { this.message.success(doneText('deleted', 'Time entry')); this.reload(true); },
      error: (err) => this.message.error(problemMessage(err, 'Could not delete the time entry')),
    });
  }

  invoiceTime(): void {
    this.engagements.invoiceTime(this.engagement().id).subscribe({
      next: (inv) => { this.message.success(`Draft invoice ${inv.number} created from the approved time. Issue it from Invoices.`); this.reload(true); },
      error: (err) => this.message.error(problemMessage(err, 'Could not invoice the time')),
    });
  }
}
