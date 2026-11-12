import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { ListQuery, PagedResult } from '@core/api/api.models';
import { StatusTagComponent } from '../status-tag/status-tag.component';

/** How many records are in one status. */
export interface StatusCount { value: string; count: number; }

/** How many records are in each status, using the total of a filtered list so the numbers cover every record and not just a page. Null when the counts could not be loaded. */
export function countByStatus<T>(list: (q: ListQuery) => Observable<PagedResult<T>>, statuses: readonly string[]): Observable<StatusCount[] | null> {
  const requests = statuses.map((value) =>
    list({ page: 1, pageSize: 1, filters: [{ field: 'status', op: 'eq', value }] }).pipe(map((r) => ({ value, count: r.total }))));
  return forkJoin(requests).pipe(catchError(() => of(null)));
}

/** A panel with one row per status: the tag, a bar for its share and the count. */
@Component({
  selector: 'erp-status-counts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterModule, NzButtonModule, NzSkeletonModule, StatusTagComponent],
  styleUrl: './status-counts.component.scss',
  templateUrl: './status-counts.component.html',
})
export class StatusCountsComponent {
  readonly title = input.required<string>();
  /** Null while loading. */
  readonly rows = input<StatusCount[] | null>(null);
  readonly failed = input(false);
  readonly link = input<string | null>(null);
  readonly linkLabel = input('Open');
  readonly emptyText = input('Nothing here yet.');
  readonly note = input<string | null>(null);
  readonly testId = input<string | null>(null);
  readonly retry = output<void>();
  readonly total = computed(() => (this.rows() ?? []).reduce((sum, r) => sum + r.count, 0));
}
