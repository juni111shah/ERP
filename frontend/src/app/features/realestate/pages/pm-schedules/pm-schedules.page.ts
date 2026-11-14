import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { createPagedList } from '@core/api/paged-list';
import { PRIORITIES, PmScheduleDto, PmSchedulesApi, RealEstateLookups, humanize } from '@core/api/realestate.api';
import { I18nService } from '@core/i18n/i18n.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';

/** Recurring maintenance tasks; the nightly job (or "Run now") raises a work order shortly before each is due. */
@Component({
  selector: 'erp-pm-schedules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  templateUrl: './pm-schedules.page.html',
})
export class PmSchedulesPage {
  private readonly i18n = inject(I18nService);
  private readonly schedulesApi = inject(PmSchedulesApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  private readonly session = inject(SessionStore);
  /** A row opens the edit drawer, so only people who may change schedules can open it. */
  readonly canManage = computed(() => this.session.hasPermission('realestate.pm.manage'));
  readonly list = createPagedList((q) => this.schedulesApi.list(q), { sort: 'nextDue' });
  readonly state = new MasterDataState<PmScheduleDto>({
    save: (id, body) => (id ? this.schedulesApi.update(id, body) : this.schedulesApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Schedule',
  });
  readonly columns: TableColumn<PmScheduleDto>[] = [
    { key: 'title', label: 'Task' },
    { key: 'category', label: 'Category' },
    { key: 'frequencyMonths', label: 'Every', value: (s) => `${s.frequencyMonths} ${this.i18n.t('ui.common.monthsParen', 'month(s)')}`, width: '110px' },
    { key: 'nextDue', label: 'Next due', sortField: 'nextDue', width: '120px' },
    { key: 'priority', label: 'Priority', value: (s) => humanize(s.priority), width: '100px' },
    { key: 'isActive', label: 'Active', value: (s) => (s.isActive ? 'Yes' : 'Paused'), width: '80px' },
    { key: 'lastGeneratedOn', label: 'Last work order', width: '130px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'propertyId', label: 'Property', type: 'lookup', lookup: this.lookups.property, required: true, readonlyOnEdit: true },
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: this.lookups.unit, hint: 'Leave blank for common areas.' },
    { key: 'title', label: 'Task', type: 'text', required: true, maxLength: 300 },
    { key: 'category', label: 'Category', type: 'text', maxLength: 50 },
    { key: 'priority', label: 'Priority', type: 'select', options: PRIORITIES, default: 'normal', required: true },
    { key: 'frequencyMonths', label: 'Repeat every (months)', type: 'number', min: 1, max: 120, precision: 0, default: 3, required: true },
    { key: 'nextDue', label: 'Next due', type: 'date', required: true },
    { key: 'leadDays', label: 'Raise work order (days before)', type: 'number', min: 0, max: 365, precision: 0, default: 7 },
    { key: 'estimatedCost', label: 'Estimated cost', type: 'number', min: 0, default: 0 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 4000 },
  ];

  /** Raises the work orders that are due now instead of waiting for the nightly job. */
  run(): void {
    this.schedulesApi.run().subscribe({ next: (r) => { this.message.success(`${r.workOrdersCreated} ${this.i18n.t('ui.msg.workOrdersRaised', 'work order(s) raised')}`); this.list.reload(); }, error: () => this.message.error('Could not run the schedules') });
  }
}
