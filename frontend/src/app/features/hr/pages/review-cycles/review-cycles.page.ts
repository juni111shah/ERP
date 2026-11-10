import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { REVIEW_CYCLE_STATUSES, ReviewCycleDto, ReviewCyclesApi } from '@core/api/hr.api';
import { createPagedList } from '@core/api/paged-list';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';

/** Review rounds (for example "Annual review 2026"); close a cycle to stop new reviews in it. */
@Component({
  selector: 'erp-review-cycles',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, StatusTagComponent],
  templateUrl: './review-cycles.page.html',
})
export class ReviewCyclesPage {
  private readonly cyclesApi = inject(ReviewCyclesApi);
  private readonly message = inject(NzMessageService);
  readonly list = createPagedList((q) => this.cyclesApi.list(q), { sort: '-periodStart' });
  readonly state = new MasterDataState<ReviewCycleDto>({
    save: (id, body) => (id ? this.cyclesApi.update(id, body) : this.cyclesApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Review cycle',
  });
  readonly columns: TableColumn<ReviewCycleDto>[] = [
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'periodStart', label: 'From', sortField: 'periodStart', width: '130px' },
    { key: 'periodEnd', label: 'To', width: '130px' },
    { key: 'status', label: 'Status', value: (c) => (c.status === 'open' ? 'Open' : 'Closed'), width: '110px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'periodStart', label: 'Period start', type: 'date', required: true },
    { key: 'periodEnd', label: 'Period end', type: 'date', required: true },
    { key: 'status', label: 'Status', type: 'select', options: REVIEW_CYCLE_STATUSES, default: 'open', required: true },
  ];
}
