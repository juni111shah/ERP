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
import { I18nService } from '../../core/i18n/i18n.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'erp-legal-entities',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">Legal entities</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name" emptyText="No legal entities yet" (rowClick)="state.edit($event)">
      <button toolbar *erpHasPermission="'core.entities.write'" nz-button nzType="primary" (click)="state.create()" data-testid="entity-new"><span nz-icon nzType="plus"></span> New legal entity</button>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit legal entity' : 'New legal entity'" [open]="state.open()" [fields]="fields()" [value]="$any(editValue())"
                       customFieldEntityType="legalentity" [saving]="state.saving()" [errors]="state.errors()" testId="entity-form"
                       (submitted)="state.submit($event)" (closed)="state.close()" />`,
})
export class LegalEntitiesPage {
  private readonly api = inject(LegalEntitiesApi);
  private readonly admin = inject(AdminApi);
  private readonly i18n = inject(I18nService);
  private readonly countries = toSignal(this.admin.countries().pipe(catchError(() => of([]))), { initialValue: [] });
  private readonly currencies = toSignal(this.admin.currencies().pipe(catchError(() => of([]))), { initialValue: [] });
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly state = new MasterDataState<LegalEntityDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)),
    reload: () => this.list.reload(), message: inject(NzMessageService), entityName: 'Legal entity',
  });
  /** The record for the drawer: an entity that already has a tax number opens with "Tax registered" suggested on (saved only if the user saves). */
  readonly editValue = computed(() => {
    const e = this.state.editing();
    return e ? { ...e, isTaxRegistered: e.isTaxRegistered || !!e.taxRegistrationNo?.trim() } : e;
  });
  readonly columns: TableColumn<LegalEntityDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'countryCode', label: 'Country', width: '90px' },
    { key: 'baseCurrency', label: 'Currency', width: '90px' },
    { key: 'taxRegistrationNo', label: 'Tax no.' },
    { key: 'registrationNo', label: 'Trade licence', value: (e) => e.registrationNo ?? '' },
    { key: 'isTaxRegistered', label: 'Tax registered', width: '120px', value: (e) => yesNo(e.isTaxRegistered) },
    { key: 'isActive', label: 'Active', value: (e) => yesNo(e.isActive) },
  ];
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'legalName', label: 'Registered legal name', type: 'text', maxLength: 200 },
    { key: 'countryCode', label: 'Country', type: 'select', required: true, readonlyOnEdit: true, options: this.countries().map((c) => ({ value: c.code, label: `${c.name} (${c.code})` })) },
    { key: 'baseCurrency', label: 'Base currency', type: 'select', required: true, readonlyOnEdit: true, options: this.currencies().map((c) => ({ value: c.code, label: `${c.code} · ${c.name}` })) },
    // Always visible: a hidden field is not sent, so editing an entity with the switch off used to clear its stored number.
    { key: 'taxRegistrationNo', label: this.i18n.t('entity.trn', 'Tax registration no. (TRN)'), type: 'text', maxLength: 50,
      hint: this.i18n.t('entity.trnHint', 'Printed on tax invoices. Entering a number switches "Tax registered" on.'),
      onChange: (v) => (typeof v === 'string' && v.trim() ? { isTaxRegistered: true } : null) },
    { key: 'isTaxRegistered', label: this.i18n.t('entity.taxRegistered', 'Tax registered'), type: 'switch', default: false },
    { key: 'registrationNo', label: this.i18n.t('entity.tradeLicence', 'Trade licence / company registration no.'), type: 'text', maxLength: 50,
      hint: this.i18n.t('entity.tradeLicenceHint', 'The licence or commercial registration number issued by the registering authority.') },
    { key: 'address', label: 'Address', type: 'textarea', maxLength: 500 },
    { key: 'email', label: 'Email', type: 'text', maxLength: 150 },
    { key: 'phone', label: 'Phone', type: 'text', maxLength: 40 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ]);
}
