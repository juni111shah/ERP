import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { TAX_KINDS, TaxCodeDto, TaxCodesApi, yesNo } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

@Component({
  selector: 'erp-tax-codes',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Tax codes</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No tax codes yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'finance.tax.write'" nz-button nzType="primary" (click)="state.create()" data-testid="tax-new"><span nz-icon nzType="plus"></span>New tax code</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit tax code' : 'New tax code'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="tax-form" (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class TaxCodesPage {
  private readonly api = inject(TaxCodesApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<TaxCodeDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Tax code',
  });
  readonly columns: TableColumn<TaxCodeDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '140px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'kind', label: 'Kind', value: (t) => TAX_KINDS.find((k) => k.value === t.kind)?.label ?? t.kind },
    { key: 'rate', label: 'Rate %' },
    { key: 'isDefault', label: 'Default', value: (t) => yesNo(t.isDefault) },
    { key: 'isActive', label: 'Active', value: (t) => yesNo(t.isActive) },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 100 },
    { key: 'kind', label: 'Kind', type: 'select', required: true, options: TAX_KINDS, default: 'vat' },
    { key: 'rate', label: 'Rate (%)', type: 'number', required: true, min: 0, max: 100, default: 0 },
    { key: 'isRecoverable', label: 'Recoverable (input tax)', type: 'switch', default: true },
    { key: 'isDefault', label: 'Default for new documents', type: 'switch', default: false, hint: 'Making this the default clears the previous default.' },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];
}
