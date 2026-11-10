import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { createPagedList } from '@core/api/paged-list';
import { ActivityRow, PlatformApi } from '@core/api/platform.api';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { PLATFORM_STYLES } from '../../platform-ui';

/** Everything the platform team and the billing run have done, newest first. */
@Component({
  selector: 'erp-platform-activity',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ErpDatePipe, PagedTableComponent],
  styles: [PLATFORM_STYLES],
  templateUrl: './platform-activity.page.html',
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
