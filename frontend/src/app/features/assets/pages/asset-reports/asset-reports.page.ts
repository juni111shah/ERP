import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { AssetCategoriesApi, AssetCategoryDto, AssetRegisterReport, AssetReportsApi } from '@core/api/assets.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { displayDate } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';

/** The asset register as it stood at the end of a chosen date, valued from the depreciation actually posted by then. */
@Component({
  selector: 'erp-asset-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzSelectModule, NzSkeletonModule, NzTableModule, StatePanelComponent],
  styles: [`
    .filters { display: flex; gap: 12px; flex-wrap: wrap; align-items: end; margin-bottom: 16px; }
    .filters label { display: block; font-size: 12px; color: var(--erp-color-text-muted); margin-bottom: 4px; }
    .r { text-align: end; white-space: nowrap; }
    tfoot td { font-weight: 600; }
  `],
  template: `
    <h1 class="erp-page-title">Asset reports</h1>
    <div class="filters">
      <div><label for="asof">As of</label><nz-date-picker id="asof" [ngModel]="asOf()" (ngModelChange)="asOf.set($event)" [nzAllowClear]="false" data-testid="asset-report-date" /></div>
      <div>
        <label for="ent">Legal entity</label>
        <nz-select id="ent" style="min-width:220px" nzAllowClear nzPlaceHolder="All" [ngModel]="entityId()" (ngModelChange)="entityId.set($event)" data-testid="asset-report-entity">
          @for (o of entities; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label" /> }
        </nz-select>
      </div>
      <div>
        <label for="cat">Category</label>
        <nz-select id="cat" style="min-width:220px" nzAllowClear nzPlaceHolder="All" [ngModel]="categoryId()" (ngModelChange)="categoryId.set($event)">
          @for (c of categories(); track c.id) { <nz-option [nzValue]="c.id" [nzLabel]="c.code + ' · ' + c.name" /> }
        </nz-select>
      </div>
      <button nz-button nzType="primary" type="button" (click)="run()" data-testid="asset-report-run">Show report</button>
    </div>
    @if (loading()) {
      <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 6 }" />
    } @else if (error()) {
      <erp-state-panel kind="error" title="Could not load the report" [hint]="error()" actionLabel="Try again" (action)="run()" />
    } @else if (report(); as r) {
      @if (r.rows.length) {
        <nz-table [nzData]="r.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="asset-report-table">
          <thead><tr><th>Number</th><th>Name</th><th>Category</th><th>In service</th><th class="r">Cost</th><th class="r">Accumulated depreciation</th><th class="r">Net book value</th></tr></thead>
          <tbody>
            @for (row of r.rows; track row.assetId) {
              <tr>
                <td><a [routerLink]="['/assets/register', row.assetId]">{{ row.number }}</a></td><td>{{ row.name }}</td><td>{{ row.categoryName }}</td><td>{{ fmt(row.capitalisedOn) }}</td>
                <td class="r">{{ row.cost | number: '1.2-2' }}</td><td class="r">{{ row.accumulatedDepreciation | number: '1.2-2' }}</td><td class="r">{{ row.netBookValue | number: '1.2-2' }}</td>
              </tr>
            }
          </tbody>
          <tfoot><tr><td colspan="4">Total at {{ fmt(r.asOf) }}</td><td class="r">{{ r.totalCost | number: '1.2-2' }}</td><td class="r">{{ r.totalAccumulatedDepreciation | number: '1.2-2' }}</td><td class="r">{{ r.totalNetBookValue | number: '1.2-2' }}</td></tr></tfoot>
        </nz-table>
      } @else {
        <erp-state-panel title="No assets on the books at this date" hint="Capitalise an asset, or choose a later date." />
      }
    }`,
})
export class AssetReportsPage {
  private readonly api = inject(AssetReportsApi);
  private readonly session = inject(SessionStore);
  readonly fmt = displayDate;
  readonly entities = legalEntityOptions(this.session);
  readonly asOf = signal<Date>(new Date());
  readonly entityId = signal<string | null>(defaultLegalEntityId(this.session));
  readonly categoryId = signal<string | null>(null);
  readonly categories = signal<AssetCategoryDto[]>([]);
  readonly report = signal<AssetRegisterReport | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    inject(AssetCategoriesApi).list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({ next: (r) => this.categories.set(r.items), error: () => undefined });
    this.run();
  }

  run(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.register(toIsoDate(this.asOf()), this.entityId(), this.categoryId()).subscribe({
      next: (r) => { this.report.set(r); this.loading.set(false); },
      error: (e) => { this.error.set(problemMessage(e, 'The report could not be loaded', 'legalEntityId')); this.loading.set(false); },
    });
  }
}
