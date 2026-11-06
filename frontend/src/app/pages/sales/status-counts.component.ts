import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { ListQuery, PagedResult } from '../../core/api/api.models';
import { StatusTagComponent } from '../../shared/status-tag.component';

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
  styles: [`
    :host { display: block; height: 100%; }
    .panel { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); height: 100%; padding: 16px 20px; border-radius: var(--erp-radius-card, 8px); }
    .head { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin-bottom: 12px; }
    h3 { margin: 0; font-size: 15px; font-weight: 600; color: var(--erp-color-text-primary); }
    .total { margin-inline-start: 6px; font-weight: 400; color: var(--erp-color-text-muted); }
    .row { display: grid; grid-template-columns: 150px 1fr 48px; align-items: center; gap: 12px; padding: 6px 0; }
    .track { height: 6px; border-radius: 3px; background: var(--erp-color-bg-subtle); overflow: hidden; }
    .fill { height: 100%; border-radius: 3px; background: var(--erp-color-primary); }
    .count { text-align: end; font-weight: 600; font-variant-numeric: tabular-nums; color: var(--erp-color-text-primary); }
    .muted { margin: 8px 0 0; color: var(--erp-color-text-muted); }
    .note { margin: 12px 0 0; padding-top: 12px; border-top: 1px solid var(--erp-color-border-subtle); color: var(--erp-color-text-secondary); }
  `],
  template: `
    <section class="panel" [attr.data-testid]="testId()">
      <div class="head">
        <h3>{{ title() }}@if (rows()) { <span class="total">{{ total() }}</span> }</h3>
        @if (link()) { <a nz-button nzType="link" nzSize="small" [routerLink]="link()">{{ linkLabel() }}</a> }
      </div>
      @if (rows(); as list) {
        @if (total() === 0) {
          <p class="muted">{{ emptyText() }}</p>
        } @else {
          @for (r of list; track r.value) {
            <div class="row">
              <span><erp-status [value]="r.value" /></span>
              <div class="track" aria-hidden="true"><div class="fill" [style.width.%]="(r.count / total()) * 100"></div></div>
              <span class="count">{{ r.count }}</span>
            </div>
          }
        }
        @if (note()) { <p class="note">{{ note() }}</p> }
      } @else if (failed()) {
        <p class="muted">These counts could not be loaded.</p>
        <button nz-button nzSize="small" type="button" style="margin-top: 8px" (click)="retry.emit()">Try again</button>
      } @else {
        <nz-skeleton [nzActive]="true" [nzTitle]="false" [nzParagraph]="{ rows: 4 }" />
      }
    </section>
  `,
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
