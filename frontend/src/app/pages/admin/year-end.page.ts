import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { FiscalYearCloseDto, YearEndApi } from '../../core/api/period-close.api';
import { createPagedList } from '../../core/api/paged-list';
import { displayDate } from '../../core/format/display';
import { doneText } from '../../core/format/done-text';
import { SessionStore } from '../../core/session/session.store';
import { ActionFormState } from '../../shared/action-form-state';
import { defaultLegalEntityId, legalEntityOptions } from '../../shared/entity-options';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

/** Year-end close: moves the year's income and expense to retained earnings, locks the books to the year end, and can be undone for the latest year. */
@Component({
  selector: 'erp-year-end',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzButtonModule, NzIconModule, HasPermissionDirective, PagedTableComponent, RecordDrawerComponent, StatusTagComponent],
  template: `
    <h1 class="erp-page-title">Year-end close</h1>
    <p>Closing a year posts one entry that moves all income and expense balances to retained earnings and locks the books up to the year end. Only the latest closed year can be reopened.</p>
    <erp-paged-table [list]="list" [columns]="columns" [searchable]="false" [clickable]="false" emptyText="No year has been closed yet.">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('status') { <erp-status [value]="row.reopenedAt ? 'reopened' : 'closed'" [label]="row.reopenedAt ? 'Reopened' : 'Closed'" /> }
          @case ('netResult') { <b>{{ row.netResult | number: '1.2-2' }}</b> }
          @case ('journal') { @if (row.journalEntryId) { <a [routerLink]="['/finance/journals', row.journalEntryId]">Open entry</a> } @else { — } }
          @case ('actions') {
            @if (canReopen(row)) {
              <button *erpHasPermission="'finance.yearend.reopen'" nz-button nzSize="small" nzDanger type="button" (click)="reopenForm.show(row)" data-testid="year-reopen">Reopen</button>
            }
          }
          @default { {{ value }} }
        }
      </ng-template>
      <button toolbar *erpHasPermission="'finance.yearend.close'" nz-button nzType="primary" (click)="closeForm.show()" data-testid="year-close"><span nz-icon nzType="lock"></span>Close a year</button>
    </erp-paged-table>
    <erp-record-drawer title="Close a fiscal year" submitLabel="Close year" [open]="closeForm.open()" [fields]="closeFields()" [saving]="closeForm.saving()" [errors]="closeForm.errors()"
                       testId="year-close-form" (submitted)="closeForm.submit($event)" (closed)="closeForm.close()" />
    <erp-record-drawer title="Reopen a fiscal year" submitLabel="Reopen year" [open]="reopenForm.open()" [fields]="reopenFields" [saving]="reopenForm.saving()" [errors]="reopenForm.errors()"
                       testId="year-reopen-form" (submitted)="reopenForm.submit($event)" (closed)="reopenForm.close()" />`,
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
    success: (r) => {
      const y = r as FiscalYearCloseDto;
      return y.alreadyClosed ? `Fiscal year ${y.fiscalYear} was already closed. Nothing new was posted.` : doneText('completed', 'Year-end close', y.fiscalYear);
    },
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
