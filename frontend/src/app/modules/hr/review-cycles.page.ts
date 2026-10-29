import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { REVIEW_CYCLE_STATUSES, ReviewCycleDto, ReviewCyclesApi } from './hr.api';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

import { NzTagModule } from 'ng-zorro-antd/tag';

/** Review rounds (for example "Annual review 2026"); close a cycle to stop new reviews in it. */
@Component({
  selector: 'erp-review-cycles',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Review cycles</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search cycles" emptyText="No review cycles yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'hr.reviews.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="cycle-new"><span nz-icon nzType="plus"></span> New cycle</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'name') {
          <span class="name-cell">{{ row.name }}</span>
        } @else if (column.key === 'status') {
          <nz-tag [nzColor]="row.status === 'open' ? 'green' : 'default'">{{ row.status === 'open' ? 'Open' : 'Closed' }}</nz-tag>
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit cycle' : 'New cycle'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="cycle-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class ReviewCyclesPage {
  private readonly api = inject(ReviewCyclesApi);
  private readonly message = inject(NzMessageService);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-periodStart' });
  readonly state = new MasterDataState<ReviewCycleDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Review cycle',
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
