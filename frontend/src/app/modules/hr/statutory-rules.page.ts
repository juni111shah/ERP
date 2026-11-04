import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { STATUTORY_BASES, STATUTORY_KINDS, STATUTORY_METHODS, StatutoryRuleDto, StatutoryRuleRow, StatutoryRulesApi } from './hr-people.api';

/** "1000:10, 3000:20" -> [{ fromAmount: 1000, rate: 10 }, ...]; blank -> []. */
export function parseSlabs(text: unknown): { fromAmount: number; rate: number }[] {
  return String(text ?? '').split(',').map((p) => p.trim()).filter(Boolean)
    .map((p) => p.split(':').map((n) => Number(n.trim())))
    .filter((p) => p.length === 2 && p.every((n) => Number.isFinite(n)))
    .map(([fromAmount, rate]) => ({ fromAmount, rate }));
}

export function slabsText(slabs: { fromAmount: number; rate: number }[] | undefined): string {
  return (slabs ?? []).map((s) => `${s.fromAmount}:${s.rate}`).join(', ');
}

/** Statutory payroll rules of an employing entity: social security, pension, income tax (employee) and employer contributions. */
@Component({
  selector: 'erp-statutory-rules',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Statutory rules</h1>
    <p style="color:var(--erp-color-text-secondary)">Deductions and employer contributions applied to every regular payroll run of the entity (social security, pension, tax ...).</p>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search rules" emptyText="No statutory rules yet" (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'hr.payroll.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="rule-new"><span nz-icon nzType="plus"></span>New rule</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit rule' : 'New rule'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="rule-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class StatutoryRulesPage {
  private readonly api = inject(StatutoryRulesApi);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<StatutoryRuleDto>({
    save: (id, body) => { const b = this.toBody(body as Record<string, unknown>); return id ? this.api.update(id, b) : this.api.create(b); },
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Rule',
  });

  readonly columns: TableColumn<StatutoryRuleRow>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '110px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'kind', label: 'Kind', value: (r) => STATUTORY_KINDS.find((k) => k.value === r.kind)?.label ?? r.kind, width: '160px', filterField: 'kind', filterOptions: STATUTORY_KINDS },
    { key: 'method', label: 'How', value: (r) => (r.method === 'flat' ? `${r.rate}% of ${r.base}` : 'Slabs'), width: '150px' },
    { key: 'isActive', label: 'Active', value: (r) => (r.isActive ? 'Yes' : 'No'), width: '80px' },
  ];

  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'legalEntityId', label: 'Employer (legal entity)', type: 'select', required: true, readonlyOnEdit: true, options: this.entityPage().items.map((e) => ({ value: e.id, label: e.name })) },
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'kind', label: 'Taken from', type: 'select', required: true, options: STATUTORY_KINDS, default: 'employeeDeduction', hint: 'An employer cost is added on top of the pay and not taken from the employee.' },
    { key: 'method', label: 'Method', type: 'select', required: true, options: STATUTORY_METHODS, default: 'flat' },
    { key: 'base', label: 'Applied to', type: 'select', required: true, options: STATUTORY_BASES, default: 'basic' },
    { key: 'rate', label: 'Rate (%)', type: 'number', min: 0, max: 100, default: 0, hidden: (v) => v['method'] === 'slab' },
    { key: 'slabsText', label: 'Slabs (from:percent)', type: 'text', maxLength: 300, hidden: (v) => v['method'] !== 'slab', hint: 'Monthly amounts, for example 0:0, 1000:10, 3000:20. Each percent applies to the part above its amount.' },
    { key: 'threshold', label: 'Nothing below (monthly)', type: 'number', min: 0, default: 0 },
    { key: 'cap', label: 'Cap on the base (monthly)', type: 'number', min: 0, default: 0, hint: '0 = no cap.' },
    { key: 'nationalities', label: 'Nationalities', type: 'text', maxLength: 300, hint: 'Blank = everyone. "Qatari, Omani" = only them. "!Qatari" = everyone except Qataris.' },
    { key: 'effectiveFrom', label: 'Effective from', type: 'date' },
    { key: 'effectiveTo', label: 'Effective to', type: 'date' },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);

  open(row: StatutoryRuleRow): void {
    this.api.detail(row.id).subscribe((r) => this.state.edit({ ...r, slabsText: slabsText(r.slabs) } as StatutoryRuleDto));
  }

  private toBody(v: Record<string, unknown>): Record<string, unknown> {
    const { slabsText: text, ...rest } = v;
    return { ...rest, slabs: v['method'] === 'slab' ? parseSlabs(text) : [] };
  }
}
