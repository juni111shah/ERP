import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '@core/api/paged-list';
import { FiscalYearCloseDto, YearEndApi } from '@core/api/period-close.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ActionFormState } from '@shared/utils/action-form-state';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';

/** The message after a close: a year that was closed before posts nothing new. */
const closedText = (y: FiscalYearCloseDto): string =>
  y.alreadyClosed ? `Fiscal year ${y.fiscalYear} was already closed. Nothing new was posted.` : doneText('completed', 'Year-end close', y.fiscalYear);

/** Year-end close: moves the year's income and expense to retained earnings, locks the books to the year end, and can be undone for the latest year. */
@Component({
  selector: 'erp-year-end',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  templateUrl: './year-end.page.html',
})
export class YearEndPage {
  private readonly api = inject(YearEndApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-yearEnd' });
  readonly columns: TableColumn<FiscalYearCloseDto>[] = [
    { key: 'entity', label: 'Legal entity', filter: false, value: (r) => this.entityName(r.legalEntityId) },
    { key: 'fiscalYear', label: 'Fiscal year', sortField: 'yearEnd', filter: false },
    { key: 'yearStart', label: 'From', filter: false, value: (r) => displayDate(r.yearStart) },
    { key: 'yearEnd', label: 'To', filter: false, value: (r) => displayDate(r.yearEnd) },
    { key: 'netResult', label: 'Net result', align: 'right', filter: false },
    { key: 'closedAt', label: 'Closed on', filter: false },
    { key: 'status', label: 'Status', filter: false, value: (r) => (r.reopenedAt ? 'Reopened' : 'Closed') },
    { key: 'journal', label: 'Closing entry', filter: false, value: (r) => (r.journalEntryId ? 'Entry' : '') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly closeForm = new ActionFormState({
    run: (p) => this.api.close(String(p['legalEntityId']), Number(p['fiscalYear'])),
    success: (r) => closedText(r as FiscalYearCloseDto),
    done: () => this.list.reload(), message: this.message, failure: 'Could not close the year',
  });
  readonly reopenForm = new ActionFormState<FiscalYearCloseDto>({
    run: (p, row) => this.api.reopen(row!.legalEntityId, row!.fiscalYear, String(p['reason'] ?? '').trim()),
    success: (_r, _p, row) => doneText('completed', 'Reopening', row?.fiscalYear),
    done: () => this.list.reload(), message: this.message, failure: 'Could not reopen the year',
  });
  readonly closeFields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: legalEntityOptions(this.session), default: defaultLegalEntityId(this.session) ?? legalEntityOptions(this.session)[0]?.value ?? null },
    { key: 'fiscalYear', label: 'Fiscal year', type: 'number', required: true, min: 2000, max: 2100, precision: 0, default: new Date().getFullYear() - 1, hint: 'Named for the calendar year it ends in. A year that is still running cannot be closed.' },
  ]);
  readonly reopenFields: FieldSpec[] = [
    { key: 'reason', label: 'Reason', type: 'textarea', required: true, maxLength: 500, hint: '3 to 500 characters. It is kept in the audit log.' },
  ];

  private entityName(id: string): string {
    const e = this.session.me()?.legalEntities.find((x) => x.id === id);
    return e ? `${e.code} · ${e.name}` : '';
  }

  /** Reopening works on the latest closed year of an entity only. */
  canReopen(row: FiscalYearCloseDto): boolean {
    if (row.reopenedAt) return false;
    return !this.list.items().some((r) => r.legalEntityId === row.legalEntityId && !r.reopenedAt && r.fiscalYear > row.fiscalYear);
  }
}
