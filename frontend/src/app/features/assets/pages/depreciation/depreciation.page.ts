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
import { ActionFormState } from '@shared/utils/action-form-state';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';

export const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** The month before today: the latest period depreciation can be run for. */
export function previousMonth(today = new Date()): { year: number; month: number } {
  const d = new Date(today.getFullYear(), today.getMonth() - 1, 1);
  return { year: d.getFullYear(), month: d.getMonth() + 1 };
}

/** Monthly depreciation: the runs already made and a form to run the next period. */
@Component({
  selector: 'erp-depreciation',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, NzModalModule, NzTableModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent],
  styles: [`.r { text-align: end; white-space: nowrap; }`],
  template: `
    <h1 class="erp-page-title">Depreciation</h1>
    <erp-paged-table [list]="list" [columns]="columns" [searchable]="false" emptyText="No depreciation has been run yet." (rowClick)="show($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('period') { <b>{{ periodLabel(row) }}</b> }
          @case ('total') { {{ row.total | number: '1.2-2' }} }
          @case ('journal') {
            @if (row.journalEntryId) { <a [routerLink]="['/finance/journals', row.journalEntryId]" (click)="$event.stopPropagation()">Open journal</a> } @else { — }
          }
          @default { {{ value }} }
        }
      </ng-template>
      <button toolbar *erpHasPermission="'assets.depreciation.run'" nz-button nzType="primary" (click)="runForm.show()" data-testid="depreciation-run"><span nz-icon nzType="play-circle"></span>Run depreciation</button>
    </erp-paged-table>
    <erp-record-drawer title="Run depreciation" submitLabel="Run" [open]="runForm.open()" [fields]="fields()" [saving]="runForm.saving()" [errors]="runForm.errors()"
                       testId="depreciation-form" (submitted)="runForm.submit($event)" (closed)="runForm.close()" />
    <nz-modal [nzVisible]="!!selected()" [nzTitle]="selected() ? 'Depreciation for ' + periodLabel(selected()!) : ''" [nzFooter]="null" [nzWidth]="720" (nzOnCancel)="selected.set(null)">
      <ng-container *nzModalContent>
        @if (selected(); as run) {
          @if (run.entries?.length) {
            <nz-table [nzData]="run.entries!" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="run-entries">
              <thead><tr><th>Asset</th><th>Name</th><th class="r">Depreciation</th><th class="r">Accumulated after</th></tr></thead>
              <tbody>
                @for (e of run.entries!; track e.assetId) {
                  <tr><td><a [routerLink]="['/assets/register', e.assetId]" (click)="selected.set(null)">{{ e.number }}</a></td><td>{{ e.name }}</td><td class="r">{{ e.amount | number: '1.2-2' }}</td><td class="r">{{ e.accumulatedAfter | number: '1.2-2' }}</td></tr>
                }
              </tbody>
            </nz-table>
          } @else { <p>Nothing was due in this period.</p> }
        }
      </ng-container>
    </nz-modal>`,
})
export class DepreciationPage {
  private readonly api = inject(DepreciationRunsApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  readonly selected = signal<DepreciationRunDto | null>(null);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-periodEnd' });
  readonly columns: TableColumn<DepreciationRunDto>[] = [
    { key: 'period', label: 'Period', sortField: 'periodEnd', filter: false, value: (r) => this.periodLabel(r) },
    { key: 'periodEnd', label: 'Period end', sortField: 'periodEnd', filter: false, value: (r) => displayDate(r.periodEnd) },
    { key: 'assetCount', label: 'Assets', align: 'right' },
    { key: 'total', label: 'Depreciation', align: 'right', sortField: 'total' },
    { key: 'runAt', label: 'Run on', sortField: 'runAt' },
    { key: 'journal', label: 'Journal', filter: false, value: (r) => (r.journalEntryId ? 'Journal' : '') },
  ];
  readonly runForm = new ActionFormState({
    run: (p) => this.api.run({ legalEntityId: String(p['legalEntityId']), year: Number(p['year']), month: Number(p['month']) }),
    success: (r) => {
      const run = r as DepreciationRunDto;
      if (run.alreadyRun) return `${this.periodLabel(run)} had already been run. Nothing new was posted.`;
      if (!run.journalEntryId) return `Nothing was due for ${this.periodLabel(run)}.`;
      return doneText('completed', 'Depreciation', this.periodLabel(run));
    },
    done: () => this.list.reload(), message: this.message, failure: 'Could not run depreciation',
  });
  readonly fields = computed<FieldSpec[]>(() => {
    const prev = previousMonth();
    return [
      { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: legalEntityOptions(this.session), default: defaultLegalEntityId(this.session) ?? legalEntityOptions(this.session)[0]?.value ?? null },
      { key: 'year', label: 'Year', type: 'number', required: true, min: 2000, max: 2100, precision: 0, default: prev.year },
      { key: 'month', label: 'Month', type: 'select', required: true, options: MONTH_NAMES.map((n, i) => ({ value: i + 1, label: n })), default: prev.month, hint: 'A month that has not ended cannot be run. Skipped months are caught up by the next run.' },
    ];
  });

  periodLabel(r: { year: number; month: number }): string { return `${MONTH_NAMES[r.month - 1]} ${r.year}`; }

  show(row: DepreciationRunDto): void {
    this.api.get(row.id).subscribe({ next: (r) => this.selected.set(r), error: (e) => this.message.error(problemMessage(e, 'Could not load this run')) });
  }
}
