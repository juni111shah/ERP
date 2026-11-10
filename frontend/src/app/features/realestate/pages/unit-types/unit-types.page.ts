import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { createPagedList } from '@core/api/paged-list';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { USAGES, UnitTypeDto, UnitTypesApi, humanize } from '@core/api/realestate.api';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '@core/i18n/translate.pipe';

@Component({
  selector: 'erp-unit-types',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  templateUrl: './unit-types.page.html',
})
export class UnitTypesPage {
  private readonly api = inject(UnitTypesApi);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<UnitTypeDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Unit type',
  });
  readonly columns: TableColumn<UnitTypeDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '140px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'defaultUsage', label: 'Default usage', value: (t) => humanize(t.defaultUsage) },
    { key: 'defaultBedrooms', label: 'Bedrooms' },
    { key: 'isActive', label: 'Active', width: '90px' },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 30, hint: 'For example STUDIO, 2BR, OFFICE.' },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 100 },
    { key: 'defaultUsage', label: 'Default usage', type: 'select', required: true, options: USAGES, default: 'residential' },
    { key: 'defaultBedrooms', label: 'Default bedrooms', type: 'number', min: 0, max: 20, precision: 0 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];
}
