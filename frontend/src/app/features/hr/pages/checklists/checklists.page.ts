import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { CHECKLIST_KINDS, ChecklistDto, ChecklistRow, ChecklistsApi } from '@core/api/hr-people.api';
import { createPagedList } from '@core/api/paged-list';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { deleteWithConfirm } from '@shared/utils/delete-with-confirm';
import { MasterDataState } from '@shared/utils/master-data-state';
import { optionLabel } from '../../hr-options';

/** The drawer's values as saved: the steps without blank lines, the day offsets whole numbers. */
function checklistBody(v: Record<string, unknown>): Record<string, unknown> {
  const items = (Array.isArray(v['items']) ? v['items'] : []) as { title?: string; dueOffsetDays?: number }[];
  return { ...v, items: items.filter((i) => i.title).map((i) => ({ title: i.title, dueOffsetDays: Math.trunc(i.dueOffsetDays ?? 0) })) };
}

/** Reusable onboarding and offboarding checklists. */
@Component({
  selector: 'erp-checklists',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Checklists</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search checklists" emptyText="No checklists yet" (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="checklist-new"><span nz-icon nzType="plus"></span>New checklist</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit checklist' : 'New checklist'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="checklist-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.employees.manage'" nz-button nzDanger type="button" (click)="remove(e.id)" data-testid="checklist-delete">Delete</button></div> }
    </erp-record-drawer>`,
})
export class ChecklistsPage {
  private readonly modal = inject(NzModalService);
  private readonly message = inject(NzMessageService);
  private readonly checklistsApi = inject(ChecklistsApi);
  readonly list = createPagedList((q) => this.checklistsApi.list(q), { sort: 'name' });
  readonly state = new MasterDataState<ChecklistDto>({
    save: (id, body) => { const b = checklistBody(body as Record<string, unknown>); return id ? this.checklistsApi.update(id, b) : this.checklistsApi.create(b); },
    reload: () => this.list.reload(), message: this.message, entityName: 'Checklist',
  });
  readonly columns: TableColumn<ChecklistRow>[] = [
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'kind', label: 'Kind', value: (c) => optionLabel(CHECKLIST_KINDS, c.kind), width: '150px', filterField: 'kind', filterOptions: CHECKLIST_KINDS },
    { key: 'isActive', label: 'Active', value: (c) => (c.isActive ? 'Yes' : 'No'), width: '80px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'kind', label: 'Kind', type: 'select', required: true, options: CHECKLIST_KINDS, default: 'onboarding' },
    { key: 'items', label: 'Steps', type: 'rows', columns: [{ key: 'title', label: 'Step', type: 'text' }, { key: 'dueOffsetDays', label: 'Days', type: 'number' }], hint: 'Days count from the join date (onboarding) or back from the last day (offboarding, use a negative number).' },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];

  open(row: ChecklistRow): void { this.checklistsApi.detail(row.id).subscribe((c) => this.state.edit(c)); }

  remove(id: string): void {
    deleteWithConfirm(this.modal, this.message, {
      title: 'Delete this checklist?', remove: () => this.checklistsApi.delete(id), entity: 'Checklist', name: this.state.editing()?.name,
      failure: 'Could not delete the checklist', done: () => { this.state.close(); this.list.reload(); },
    });
  }
}
