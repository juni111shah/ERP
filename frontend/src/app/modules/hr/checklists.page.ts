import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { CHECKLIST_KINDS, ChecklistDto, ChecklistRow, ChecklistsApi } from './hr-people.api';

/** One step per line, "title | days": days after the join date (onboarding) or before the last day (offboarding, negative). */
export function parseItems(text: unknown): { title: string; dueOffsetDays: number }[] {
  return String(text ?? '').split(/\r?\n/).map((l) => l.trim()).filter(Boolean).map((line) => {
    const [title, days] = line.split('|').map((p) => p.trim());
    const n = Number(days ?? 0);
    return { title, dueOffsetDays: Number.isFinite(n) ? Math.trunc(n) : 0 };
  }).filter((i) => i.title);
}

export function itemsText(items: { title: string; dueOffsetDays: number }[] | undefined): string {
  return (items ?? []).map((i) => `${i.title} | ${i.dueOffsetDays}`).join('\n');
}

/** Reusable onboarding and offboarding checklists. */
@Component({
  selector: 'erp-checklists',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Checklists</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search checklists" emptyText="No checklists yet" (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'hr.employees.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="checklist-new"><span nz-icon nzType="plus"></span>New checklist</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit checklist' : 'New checklist'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="checklist-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class ChecklistsPage {
  private readonly api = inject(ChecklistsApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'name' });
  readonly state = new MasterDataState<ChecklistDto>({
    save: (id, body) => { const b = this.toBody(body as Record<string, unknown>); return id ? this.api.update(id, b) : this.api.create(b); },
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Checklist',
  });
  readonly columns: TableColumn<ChecklistRow>[] = [
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'kind', label: 'Kind', value: (c) => CHECKLIST_KINDS.find((k) => k.value === c.kind)?.label ?? c.kind, width: '150px', filterField: 'kind', filterOptions: CHECKLIST_KINDS },
    { key: 'isActive', label: 'Active', value: (c) => (c.isActive ? 'Yes' : 'No'), width: '80px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'kind', label: 'Kind', type: 'select', required: true, options: CHECKLIST_KINDS, default: 'onboarding' },
    { key: 'items', label: 'Steps', type: 'rows', columns: [{ key: 'title', label: 'Step', type: 'text' }, { key: 'dueOffsetDays', label: 'Days', type: 'number' }], hint: 'Days count from the join date (onboarding) or back from the last day (offboarding, use a negative number).' },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];

  open(row: ChecklistRow): void { this.api.detail(row.id).subscribe((c) => this.state.edit(c)); }

  private toBody(v: Record<string, unknown>): Record<string, unknown> {
    const items = (Array.isArray(v['items']) ? v['items'] : []) as { title?: string; dueOffsetDays?: number }[];
    return { ...v, items: items.filter((i) => i.title).map((i) => ({ title: i.title, dueOffsetDays: Math.trunc(i.dueOffsetDays ?? 0) })) };
  }
}
