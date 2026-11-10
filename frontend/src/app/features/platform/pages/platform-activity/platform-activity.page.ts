import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { createPagedList } from '@core/api/paged-list';
import { ActivityRow, PlatformApi } from '@core/api/platform.api';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { PLATFORM_STYLES } from '../../platform-ui';

/** Everything the platform team and the billing run have done, newest first. */
@Component({
  selector: 'erp-platform-activity',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ErpDatePipe, PagedTableComponent],
  styles: [PLATFORM_STYLES],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search the log" emptyText="Nothing has happened yet" [filterable]="false" [configurable]="false" [exportable]="false" [clickable]="false"
                     tableName="platform-activity" scrollX="980px">
      <ng-template #cell let-r let-col="column">
        @switch (col.key) {
          @case ('at') { <span class="pf-nowrap">{{ r.at | erpDate }}</span> }
          @case ('actor') { {{ r.actor }} }
          @case ('action') { <strong>{{ r.action }}</strong> }
          @case ('client') { @if (r.orgId) { <a [routerLink]="['/platform/clients', r.orgId]">{{ r.clientName }}</a> } @else { {{ r.clientName ?? '—' }} } }
          @case ('detail') { <span class="pf-muted">{{ r.detail }}</span> }
        }
      </ng-template>
    </erp-paged-table>`,
})
export class PlatformActivityPage {
  private readonly api = inject(PlatformApi);
  readonly list = createPagedList<ActivityRow>((q) => this.api.activity(q));
  readonly columns: TableColumn<ActivityRow>[] = [
    { key: 'action', label: 'What', width: '170px', value: (r) => r.action },
    { key: 'client', label: 'Client', width: '170px', value: (r) => r.clientName },
    { key: 'detail', label: 'Detail', width: '280px', value: (r) => r.detail },
    { key: 'at', label: 'When', width: '160px', value: (r) => r.at },
    { key: 'actor', label: 'Who', width: '200px', value: (r) => r.actor },
  ];
}
