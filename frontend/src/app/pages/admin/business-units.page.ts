import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { BusinessUnitDto, BusinessUnitsApi, LegalEntitiesApi, yesNo } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

@Component({
  selector: 'erp-business-units',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Business units</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No business units yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'core.businessunits.write'" nz-button nzType="primary" (click)="state.create()" data-testid="bu-new"><span nz-icon nzType="plus"></span>New business unit</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit business unit' : 'New business unit'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       customFieldEntityType="businessunit" [saving]="state.saving()" [errors]="state.errors()" testId="bu-form"
                       (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class BusinessUnitsPage {
  private readonly api = inject(BusinessUnitsApi);
  private readonly entities = inject(LegalEntitiesApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<BusinessUnitDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Business unit',
  });
  readonly columns: TableColumn<BusinessUnitDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '140px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'address', label: 'Address' },
    { key: 'isActive', label: 'Active', value: (b) => yesNo(b.isActive) },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'legalEntityId', label: 'Legal entity', type: 'lookup', required: true, readonlyOnEdit: true, lookup: this.entities.lookup, currentLabel: (r) => String(r['legalEntityId']) },
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 30 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'address', label: 'Address', type: 'textarea', maxLength: 500 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];
}
