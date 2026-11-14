import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { AssetRegisterReport, AssetReportsApi } from '@core/api/assets.api';
import { toIsoDate } from '@core/format/dates';
import { displayDate } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { CanPipe } from '@shared/pipes/can.pipe';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { AssetLookups } from '../../asset-lookups';

/** The asset register as it stood at the end of a chosen date, valued from the depreciation actually posted by then. */
@Component({
  selector: 'erp-asset-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, RouterLink, NzButtonModule, NzDatePickerModule, NzSelectModule, NzSkeletonModule, NzTableModule, StatePanelComponent, CanPipe],
  providers: [AssetLookups],
  styleUrl: './asset-reports.page.scss',
  templateUrl: './asset-reports.page.html',
})
export class AssetReportsPage {
  private readonly reportsApi = inject(AssetReportsApi);
  private readonly session = inject(SessionStore);
  private readonly lookups = inject(AssetLookups);
  readonly fmt = displayDate;
  readonly entities = legalEntityOptions(this.session);
  readonly asOf = signal<Date>(new Date());
  readonly entityId = signal<string | null>(defaultLegalEntityId(this.session));
  readonly categoryId = signal<string | null>(null);
  readonly categories = this.lookups.categories;
  readonly report = signal<AssetRegisterReport | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    this.lookups.loadCategories();
    this.run();
  }

  run(): void {
    this.loading.set(true);
    this.error.set(null);
    this.reportsApi.register(toIsoDate(this.asOf()), this.entityId(), this.categoryId()).subscribe({
      next: (r) => { this.report.set(r); this.loading.set(false); },
      error: (e) => { this.error.set(problemMessage(e, 'The report could not be loaded', 'legalEntityId')); this.loading.set(false); },
    });
  }
}
