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
import { toIsoDate } from '@core/format/dates';
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
  styleUrl: './asset-reports.page.scss',
  templateUrl: './asset-reports.page.html',
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
