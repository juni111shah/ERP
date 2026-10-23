import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { PRIORITIES, PmScheduleDto, PmSchedulesApi, RealEstateLookups, humanize } from './realestate.api';

/** Recurring maintenance tasks; the nightly job (or "Run now") raises a work order shortly before each is due. */
@Component({
  selector: 'erp-pm-schedules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Preventive maintenance</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search schedules" emptyText="No schedules yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'realestate.pm.manage'" nz-button (click)="run()" data-testid="pm-run">Run now</button>
      <button toolbar *erpHasPermission="'realestate.pm.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="pm-new"><span nz-icon nzType="plus"></span>New schedule</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit schedule' : 'New schedule'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="pm-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class PmSchedulesPage {
  private readonly api = inject(PmSchedulesApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'nextDue' });
  readonly state = new MasterDataState<PmScheduleDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Schedule',
  });
  readonly columns: TableColumn<PmScheduleDto>[] = [
    { key: 'title', label: 'Task' },
    { key: 'category', label: 'Category' },
    { key: 'frequencyMonths', label: 'Every', value: (s) => `${s.frequencyMonths} month(s)`, width: '110px' },
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

  run(): void {
    this.api.run().subscribe({ next: (r) => { this.message.success(`${r.workOrdersCreated} work order(s) raised`); this.list.reload(); }, error: () => this.message.error('Could not run the schedules') });
  }
}
