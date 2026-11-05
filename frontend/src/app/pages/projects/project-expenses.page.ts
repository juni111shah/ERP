import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { createPagedList } from '../../core/api/paged-list';
import { ProjectsApi } from '../../core/api/projects.api';
import { EXPENSE_CATEGORIES, ExpenseDto, ProjectExpensesApi, ProjectsExtraApi, UserOption } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

const COLORS: Record<string, string> = { Draft: 'default', Submitted: 'blue', Approved: 'green', Rejected: 'red' };

/** Money spent for a project: record it, send it for approval, and charge it to the customer (with a mark-up) when it is billed. */
@Component({
  selector: 'erp-project-expenses',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, RouterModule, NzButtonModule, NzIconModule, NzTagModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search expenses" emptyText="No expenses recorded yet.">
      <button toolbar *erpHasPermission="'projects.expenses.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="expense-new"><span nz-icon nzType="plus"></span> New expense</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('projectName') { <a [routerLink]="['/projects', row.projectId]">{{ nameOf(row.projectId) }}</a> }
          @case ('amount') { {{ currency() }} {{ row.amount | number: '1.2-2' }} }
          @case ('chargeAmount') { @if (row.isBillable) { {{ currency() }} {{ row.chargeAmount | number: '1.2-2' }} } @else { <span class="muted">not charged</span> } }
          @case ('state') { <nz-tag [nzColor]="color(row.status)" [title]="row.rejectReason || ''">{{ row.status }}</nz-tag> }
          @case ('billing') {
            @if (row.writtenOff) { <nz-tag>Written off</nz-tag> } @else if (!row.isBillable) { <nz-tag>Not billable</nz-tag> } @else if (row.isBilled) { <nz-tag nzColor="green">Billed</nz-tag> } @else { <nz-tag nzColor="orange">Unbilled</nz-tag> }
          }
          @case ('actions') {
            @if (!row.isBilled && !row.writtenOff) {
              <span *erpHasPermission="'projects.expenses.manage'">
                <button nz-button nzSize="small" (click)="state.edit(row)" [attr.data-testid]="'expense-edit-' + row.id">Edit</button>
                @if (row.status === 'Draft' || row.status === 'Rejected') { <button nz-button nzSize="small" (click)="submit(row)">Submit</button> }
                <button nz-button nzSize="small" nzDanger (click)="remove(row)">Delete</button>
              </span>
            }
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit expense' : 'New expense'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="expense-form" (submitted)="state.submit($event)" (closed)="state.close()" />
  `,
  styles: ['.muted { color: #94a3b8; font-size: 12px; }'],
})
export class ProjectExpensesPage {
  private readonly api = inject(ProjectExpensesApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });
  private readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly users = toSignal(this.extra.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });

  readonly state = new MasterDataState<ExpenseDto>({
    save: (id, body) => {
      const b = { ...(body as Record<string, unknown>) } as Record<string, unknown>;
      if (b['date'] instanceof Date) b['date'] = toIsoDate(b['date']);
      return id ? this.api.update(id, b as never) : this.api.create(b as never);
    },
    reload: () => this.list.reload(), message: this.msg, entityName: 'Expense',
  });

  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', required: true, readonlyOnEdit: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) },
    { key: 'date', label: 'Date', type: 'date', required: true, default: new Date() },
    { key: 'category', label: 'Category', type: 'select', required: true, options: EXPENSE_CATEGORIES, default: 'Travel' },
    { key: 'description', label: 'What was it for', type: 'text', required: true, maxLength: 500 },
    { key: 'amount', label: 'Amount (what it cost)', type: 'number', required: true, min: 0.01, precision: 2 },
    { key: 'isBillable', label: 'Charge to the customer', type: 'switch', default: true },
    { key: 'markupPercent', label: 'Mark-up %', type: 'number', min: 0, max: 500, default: 0, hint: 'Added to the amount on the invoice.', hidden: (v) => v['isBillable'] === false },
    { key: 'workerUserId', label: 'Paid by', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'workerName', label: 'or a name', type: 'text', maxLength: 100, hidden: (v) => !!v['workerUserId'] },
  ]);

  readonly columns: TableColumn<ExpenseDto>[] = [
    { key: 'date', label: 'Date', sortField: 'date', width: '110px' },
    { key: 'projectName', label: 'Project', width: '180px', filter: false },
    { key: 'category', label: 'Category', sortField: 'category', width: '130px' },
    { key: 'description', label: 'Description' },
    { key: 'amount', label: 'Cost', width: '120px', align: 'right', filter: false },
    { key: 'chargeAmount', label: 'Charged', width: '120px', align: 'right', filter: false },
    { key: 'state', label: 'Approval', width: '110px', filter: false },
    { key: 'billing', label: 'Billing', width: '110px', filter: false },
    { key: 'actions', label: '', width: '220px' },
  ];

  nameOf(id: string): string { const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Project'; }
  color(s: string): string { return COLORS[s] ?? 'default'; }

  submit(e: ExpenseDto): void {
    this.api.submit([e.id]).subscribe({ next: () => { this.msg.success('Submitted for approval'); this.list.reload(); }, error: (er) => this.msg.error(er?.error?.detail ?? 'Could not submit.') });
  }
  remove(e: ExpenseDto): void {
    this.modal.confirm({
      nzTitle: 'Delete this expense?', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(e.id).subscribe({ next: () => { this.msg.success('Expense deleted'); this.list.reload(); }, error: (er) => this.msg.error(er?.error?.detail ?? 'Could not delete.') }),
    });
  }
}
