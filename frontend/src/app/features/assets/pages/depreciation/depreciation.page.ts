import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { DepreciationRunDto, DepreciationRunsApi } from '@core/api/assets.api';
import { createPagedList } from '@core/api/paged-list';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ActionFormState } from '@shared/utils/action-form-state';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { MONTH_OPTIONS, periodLabel, previousMonth } from '../../asset-periods';

/** The toast after a run: already run, nothing due, or done. */
function runResult(run: DepreciationRunDto): string {
  if (run.alreadyRun) return `${periodLabel(run)} had already been run. Nothing new was posted.`;
  if (!run.journalEntryId) return `Nothing was due for ${periodLabel(run)}.`;
  return doneText('completed', 'Depreciation', periodLabel(run));
}

/** Monthly depreciation: the runs already made and a form to run the next period. */
@Component({
  selector: 'erp-depreciation',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, NzModalModule, NzTableModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent],
  styleUrl: './depreciation.page.scss',
  templateUrl: './depreciation.page.html',
})
export class DepreciationPage {
  private readonly runsApi = inject(DepreciationRunsApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  readonly selected = signal<DepreciationRunDto | null>(null);
  readonly list = createPagedList((q) => this.runsApi.list(q), { sort: '-periodEnd' });
  readonly columns: TableColumn<DepreciationRunDto>[] = [
    { key: 'period', label: 'Period', sortField: 'periodEnd', filter: false, value: (r) => this.periodLabel(r) },
    { key: 'periodEnd', label: 'Period end', sortField: 'periodEnd', filter: false, value: (r) => displayDate(r.periodEnd) },
    { key: 'assetCount', label: 'Assets', align: 'right' },
    { key: 'total', label: 'Depreciation', align: 'right', sortField: 'total' },
    { key: 'runAt', label: 'Run on', sortField: 'runAt' },
    { key: 'journal', label: 'Journal', filter: false, value: (r) => (r.journalEntryId ? 'Journal' : '') },
  ];
  readonly runForm = new ActionFormState({
    run: (p) => this.runsApi.run({ legalEntityId: String(p['legalEntityId']), year: Number(p['year']), month: Number(p['month']) }),
    success: (r) => runResult(r as DepreciationRunDto),
    done: () => this.list.reload(), message: this.message, failure: 'Could not run depreciation',
  });
  readonly fields = computed<FieldSpec[]>(() => {
    const prev = previousMonth();
    return [
      { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: legalEntityOptions(this.session), default: defaultLegalEntityId(this.session) ?? legalEntityOptions(this.session)[0]?.value ?? null },
      { key: 'year', label: 'Year', type: 'number', required: true, min: 2000, max: 2100, precision: 0, default: prev.year },
      { key: 'month', label: 'Month', type: 'select', required: true, options: MONTH_OPTIONS, default: prev.month, hint: 'A month that has not ended cannot be run. Skipped months are caught up by the next run.' },
    ];
  });

  periodLabel(r: { year: number; month: number }): string { return periodLabel(r); }

  show(row: DepreciationRunDto): void {
    this.runsApi.get(row.id).subscribe({ next: (r) => this.selected.set(r), error: (e) => this.message.error(problemMessage(e, 'Could not load this run')) });
  }
}
