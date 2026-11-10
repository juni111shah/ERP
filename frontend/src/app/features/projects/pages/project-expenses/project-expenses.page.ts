import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { createPagedList } from '@core/api/paged-list';
import { ProjectsApi } from '@core/api/projects.api';
import { EXPENSE_CATEGORIES, ExpenseDto, ProjectExpensesApi, ProjectsConfig, ProjectsExtraApi, UserOption } from '@core/api/projects-extra.api';
import { toIsoDate } from '@core/format/dates';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { APPROVAL_STATUSES, errorText, filterOptions, PRJ_STYLES, ProjectTagComponent } from '../../projects-ui';

/** Money spent for a project: record it, send it for approval, and charge it to the customer (with a mark-up) when it is billed. */
@Component({
  selector: 'erp-project-expenses',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterModule, NzButtonModule, NzIconModule, NzTagModule, NzModalModule, AttachmentsComponent, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, ProjectTagComponent],
  templateUrl: './project-expenses.page.html',
  styles: [PRJ_STYLES + '.acts { display:inline-flex; gap:4px; }'],
})
export class ProjectExpensesPage {
  /** Set by the project page, or by ?projectId= in the address. */
  readonly projectId = input<string | null>(null);

  private readonly api = inject(ProjectExpensesApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly config = toSignal(this.extra.config().pipe(catchError(() => of(null as ProjectsConfig | null))), { initialValue: null });

  readonly list = createPagedList((q) => {
    const id = this.projectId();
    return this.api.list(id ? { ...q, filters: [...(q.filters ?? []), { field: 'projectId', op: 'eq', value: id }] } : { ...q, extra: { ...(q.extra ?? {}), live: true } });
  }, { sort: '-date' });
  readonly receipts = signal<ExpenseDto | null>(null);
  private readonly projects = toSignal(this.projectsApi.open().pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly users = toSignal(this.extra.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });

  readonly state: MasterDataState<ExpenseDto> = new MasterDataState<ExpenseDto>({
    save: (id, body) => {
      const b = { ...(body as Record<string, unknown>) } as Record<string, unknown>;
      if (b['date'] instanceof Date) b['date'] = toIsoDate(b['date']);
      if (this.projectId()) b['projectId'] = this.projectId();
      else if (id) b['projectId'] = this.state.editing()?.projectId;
      return id ? this.api.update(id, b as never) : this.api.create(b as never);
    },
    reload: () => this.list.reload(), message: this.msg, entityName: 'Expense',
  });

  readonly fields = computed<FieldSpec[]>(() => [
    ...(this.projectId() ? [] : [{ key: 'projectId', label: 'Project', type: 'select' as const, required: true, readonlyOnEdit: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) }]),
    { key: 'date', label: 'Date', type: 'date', required: true, default: new Date() },
    { key: 'category', label: 'Category', type: 'select', required: true, options: EXPENSE_CATEGORIES, default: 'Travel' },
    { key: 'description', label: 'What was it for', type: 'text', required: true, maxLength: 500 },
    { key: 'amount', label: `Amount, what it cost (${this.currency()})`, type: 'number', required: true, min: 0.01, precision: 2 },
    { key: 'isBillable', label: 'Charge to the customer', type: 'switch', default: true },
    { key: 'markupPercent', label: 'Mark-up %', type: 'number', min: 0, max: 500, default: 0, hint: 'Added to the amount on the invoice.', hidden: (v) => v['isBillable'] === false },
    { key: 'workerUserId', label: 'Paid by', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'workerName', label: 'Or a name', type: 'text', maxLength: 100, hidden: (v) => !!v['workerUserId'] },
  ]);

  readonly columns = computed<TableColumn<ExpenseDto>[]>(() => [
    { key: 'date', label: 'Date', sortField: 'date', width: '110px', filterField: 'date' },
    ...(this.projectId() ? [] : [{ key: 'projectName', label: 'Project', width: '190px', filter: false as const }]),
    { key: 'category', label: 'Category', width: '130px', filterField: 'category', filterOptions: EXPENSE_CATEGORIES },
    { key: 'description', label: 'Description', width: '240px', filter: false },
    { key: 'amount', label: 'Cost', width: '120px', align: 'right', filter: false },
    { key: 'chargeAmount', label: 'Charged', width: '120px', align: 'right', filter: false },
    { key: 'status', label: 'Approval', sortField: 'status', width: '130px', filterField: 'status', filterOptions: filterOptions(APPROVAL_STATUSES) },
    { key: 'billing', label: 'Billing', width: '110px', filter: false },
    { key: 'actions', label: '', width: '250px' },
  ]);

  constructor() {
    let first = true;
    effect(() => { this.projectId(); if (first) { first = false; return; } this.list.reload(); });
  }

  editable(e: ExpenseDto): boolean {
    if (!this.config()?.expenseApproval) return e.status !== 'submitted';
    return e.status === 'draft' || e.status === 'rejected';
  }

  submit(e: ExpenseDto): void {
    this.api.submit([e.id]).subscribe({ next: () => { this.msg.success(`Expense ${e.description} sent for approval`); this.list.reload(); }, error: (er) => this.msg.error(errorText(er, 'Could not submit.')) });
  }
  remove(e: ExpenseDto): void {
    this.modal.confirm({
      nzTitle: 'Delete this expense?', nzContent: e.description, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(e.id).subscribe({ next: () => { this.msg.success(doneText('deleted', 'Expense', e.description)); this.list.reload(); }, error: (er) => this.msg.error(errorText(er, 'Could not delete.')) }),
    });
  }
}
