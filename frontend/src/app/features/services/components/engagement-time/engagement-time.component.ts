import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTableModule } from 'ng-zorro-antd/table';
import { EngagementDto, ServicesEngagementsApi, ServicesTimeEntriesApi, TimeEntrySummaryDto } from '@core/api/services.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { ActionFormState } from '@shared/utils/action-form-state';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

/** The hours logged against one engagement: log, change, delete (until approved), approve, and bill the approved time of a time-and-materials engagement. */
@Component({
  selector: 'erp-engagement-time',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, NzPopconfirmModule, NzTableModule, HasPermissionDirective, RecordDrawerComponent, StatusTagComponent],
  styleUrl: './engagement-time.component.scss',
  templateUrl: './engagement-time.component.html',
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
