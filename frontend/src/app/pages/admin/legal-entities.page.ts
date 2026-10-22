import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { catchError, of } from 'rxjs';
import { AdminApi, LegalEntitiesApi, LegalEntityDto, yesNo } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

@Component({
  selector: 'erp-legal-entities',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Legal entities</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No legal entities yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'core.entities.write'" nz-button nzType="primary" (click)="state.create()" data-testid="entity-new"><span nz-icon nzType="plus"></span>New legal entity</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit legal entity' : 'New legal entity'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       customFieldEntityType="legalentity" [saving]="state.saving()" [errors]="state.errors()" testId="entity-form"
                       (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class LegalEntitiesPage {
  private readonly api = inject(LegalEntitiesApi);
  private readonly admin = inject(AdminApi);
  private readonly countries = toSignal(this.admin.countries().pipe(catchError(() => of([]))), { initialValue: [] });
  private readonly currencies = toSignal(this.admin.currencies().pipe(catchError(() => of([]))), { initialValue: [] });
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<LegalEntityDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Legal entity',
  });
  readonly columns: TableColumn<LegalEntityDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'countryCode', label: 'Country', width: '90px' },
    { key: 'baseCurrency', label: 'Currency', width: '90px' },
    { key: 'taxRegistrationNo', label: 'Tax no.' },
    { key: 'isActive', label: 'Active', value: (e) => yesNo(e.isActive) },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'legalName', label: 'Registered legal name', type: 'text', maxLength: 200 },
    { key: 'countryCode', label: 'Country', type: 'select', required: true, readonlyOnEdit: true, options: this.countries().map((c) => ({ value: c.code, label: `${c.name} (${c.code})` })) },
    { key: 'baseCurrency', label: 'Base currency', type: 'select', required: true, readonlyOnEdit: true, options: this.currencies().map((c) => ({ value: c.code, label: `${c.code} · ${c.name}` })) },
    { key: 'isTaxRegistered', label: 'Tax registered', type: 'switch', default: false },
    { key: 'taxRegistrationNo', label: 'Tax registration no.', type: 'text', maxLength: 50, hidden: (v) => !v['isTaxRegistered'] },
    { key: 'registrationNo', label: 'Company registration no.', type: 'text', maxLength: 50 },
    { key: 'address', label: 'Address', type: 'textarea', maxLength: 500 },
    { key: 'email', label: 'Email', type: 'text', maxLength: 150 },
    { key: 'phone', label: 'Phone', type: 'text', maxLength: 40 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);
}
