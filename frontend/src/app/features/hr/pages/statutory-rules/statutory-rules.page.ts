import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { STATUTORY_BASES, STATUTORY_KINDS, STATUTORY_METHODS, StatutoryRuleDto, StatutoryRuleRow, StatutoryRulesApi } from '@core/api/hr-people.api';
import { createPagedList } from '@core/api/paged-list';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { deleteWithConfirm } from '@shared/utils/delete-with-confirm';
import { itemsSignal } from '@shared/utils/items-signal';
import { MasterDataState } from '@shared/utils/master-data-state';
import { entityOptions, optionLabel } from '../../hr-options';

/** The drawer's values as saved: slabs only for a slab rule. */
function ruleBody(v: Record<string, unknown>): Record<string, unknown> {
  return { ...v, slabs: v['method'] === 'slab' ? (v['slabs'] ?? []) : [] };
}

/** Statutory payroll rules of an employing entity: social security, pension, income tax (employee) and employer contributions. */
@Component({
  selector: 'erp-statutory-rules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Statutory rules</h1>
    <p style="color:var(--erp-color-text-secondary)">Deductions and employer contributions applied to every regular payroll run of the entity (social security, pension, tax ...).</p>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search rules" emptyText="No statutory rules yet" (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'hr.payroll.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="rule-new"><span nz-icon nzType="plus"></span>New rule</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit rule' : 'New rule'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="rule-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as e) { <div drawerActions style="margin-bottom:12px"><button *erpHasPermission="'hr.payroll.manage'" nz-button nzDanger type="button" (click)="remove(e.id)" data-testid="rule-delete">Delete</button></div> }
    </erp-record-drawer>`,
})
export class StatutoryRulesPage {
  private readonly modal = inject(NzModalService);
  private readonly message = inject(NzMessageService);
  private readonly rulesApi = inject(StatutoryRulesApi);
  private readonly entityPage = itemsSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }));
  readonly list = createPagedList((q) => this.rulesApi.list(q), { sort: 'code' });
  readonly state = new MasterDataState<StatutoryRuleDto>({
    save: (id, body) => { const b = ruleBody(body as Record<string, unknown>); return id ? this.rulesApi.update(id, b) : this.rulesApi.create(b); },
    reload: () => this.list.reload(), message: this.message, entityName: 'Rule',
  });

  readonly columns: TableColumn<StatutoryRuleRow>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '110px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'kind', label: 'Kind', value: (r) => optionLabel(STATUTORY_KINDS, r.kind), width: '160px', filterField: 'kind', filterOptions: STATUTORY_KINDS },
    { key: 'method', label: 'How', value: (r) => (r.method === 'flat' ? `${r.rate}% of ${r.base}` : 'Slabs'), width: '150px' },
    { key: 'isActive', label: 'Active', value: (r) => (r.isActive ? 'Yes' : 'No'), width: '80px' },
  ];

  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, readonlyOnEdit: true, options: entityOptions(this.entityPage().items) },
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'kind', label: 'Taken from', type: 'select', required: true, options: STATUTORY_KINDS, default: 'employeeDeduction', hint: 'An employer cost is added on top of the pay and not taken from the employee.' },
    { key: 'method', label: 'Method', type: 'select', required: true, options: STATUTORY_METHODS, default: 'flat' },
    { key: 'base', label: 'Applied to', type: 'select', required: true, options: STATUTORY_BASES, default: 'basic' },
    { key: 'rate', label: 'Rate (%)', type: 'number', min: 0, max: 100, default: 0, hidden: (v) => v['method'] === 'slab' },
    { key: 'slabs', label: 'Slabs', type: 'rows', columns: [{ key: 'fromAmount', label: 'From (monthly)', type: 'number' }, { key: 'rate', label: 'Percent', type: 'number' }], hidden: (v) => v['method'] !== 'slab', hint: 'Each percent applies to the part of the base above its amount, for example from 0 at 0%, from 1000 at 10%.' },
    { key: 'threshold', label: 'Nothing below (monthly)', type: 'number', min: 0, default: 0 },
    { key: 'cap', label: 'Cap on the base (monthly)', type: 'number', min: 0, default: 0, hint: '0 = no cap.' },
    { key: 'nationalities', label: 'Nationalities', type: 'text', maxLength: 300, hint: 'Blank = everyone. "Qatari, Omani" = only them. "!Qatari" = everyone except Qataris.' },
    { key: 'effectiveFrom', label: 'Effective from', type: 'date' },
    { key: 'effectiveTo', label: 'Effective to', type: 'date' },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);

  open(row: StatutoryRuleRow): void {
    this.rulesApi.detail(row.id).subscribe((r) => this.state.edit(r));
  }

  remove(id: string): void {
    deleteWithConfirm(this.modal, this.message, {
      title: 'Delete this rule?', remove: () => this.rulesApi.delete(id), entity: 'Statutory rule', name: this.state.editing()?.name,
      failure: 'Could not delete the rule', done: () => { this.state.close(); this.list.reload(); },
    });
  }
}
