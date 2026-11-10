import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { createPagedList } from '@core/api/paged-list';
import { CreatePriceListInput, RetailPriceListDetailDto, RetailPriceListItemDto, RetailPriceListsApi, RetailPriceListSummaryDto } from '@core/api/retail.api';
import { toIsoDate } from '@core/format/dates';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { defaultEntityId, localDay, notBeforeStart, PriceListFormValue, revealInvalid } from '../../retail-forms';
import { RetailLookups } from '../../retail-lookups';

/** The usual currency codes offered on a price list, after the workspace's own and the edited list's. */
const COMMON_CURRENCIES = ['QAR', 'AED', 'SAR', 'USD', 'EUR', 'GBP'];

/** "1 Jan 2026 to 31 Mar 2026", "From ...", "Until ..." or "Always". */
function validity(row: RetailPriceListSummaryDto): string {
  if (row.validFrom && row.validTo) return `${displayDate(row.validFrom)} to ${displayDate(row.validTo)}`;
  if (row.validFrom) return `From ${displayDate(row.validFrom)}`;
  if (row.validTo) return `Until ${displayDate(row.validTo)}`;
  return 'Always';
}

/** The API body from the form; dates are the local calendar days picked. */
function toPriceListInput(val: PriceListFormValue, customFields: string | null): CreatePriceListInput {
  return {
    legalEntityId: val.legalEntityId,
    code: val.code,
    name: val.name,
    type: val.type,
    currency: val.currency,
    validFrom: val.validFrom ? toIsoDate(new Date(val.validFrom)) : null,
    validTo: val.validTo ? toIsoDate(new Date(val.validTo)) : null,
    isDefault: val.isDefault,
    isActive: val.isActive,
    description: val.description || null,
    items: val.items,
    customFields,
  };
}

/** Retail price lists: the paged list, the create/edit modal with its item lines and the detail drawer. */
@Component({
  selector: 'erp-pricelists-page',
  standalone: true,
  imports: [ErpLabelPipe, NzPopconfirmModule, NzToolTipModule, StatusTagComponent,
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
    NzSwitchModule,
    NzModalModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    NzTableModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  providers: [RetailLookups],
  templateUrl: './pricelists.page.html',
  styleUrl: './pricelists.page.scss'
})
export class PriceListsPageComponent implements OnInit {
  private readonly priceListsApi = inject(RetailPriceListsApi);
  private readonly lookups = inject(RetailLookups);
  private readonly sessionStore = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly entities = this.lookups.entities;
  readonly itemsList = this.lookups.items;
  readonly editing = signal<RetailPriceListDetailDto | null>(null);
  /** The usual codes, always with the workspace's own and the one on the list being edited. */
  readonly currencyOptions = computed(() => [...new Set([this.currency(), this.editing()?.currency, ...COMMON_CURRENCIES].filter((c): c is string => !!c))]);
  readonly isSaving = signal(false);
  readonly selectedDetail = signal<RetailPriceListDetailDto | null>(null);

  readonly isModalVisible = signal(false);
  readonly isDrawerVisible = signal(false);

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required]],
    name: ['', [Validators.required]],
    type: ['standard', [Validators.required]],
    currency: [this.currency(), [Validators.required]],
    validFrom: [null],
    validTo: [null, [notBeforeStart('validFrom')]],
    isDefault: [false],
    isActive: [true],
    description: [''],
    items: this.fb.array([])
  });

  readonly columns: TableColumn<RetailPriceListSummaryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'type', label: 'Type' },
    { key: 'currency', label: 'Currency' },
    { key: 'validity', label: 'Valid', filter: false, value: (r) => this.validity(r) },
    { key: 'itemCount', label: 'Items', align: 'right' },
    { key: 'isActive', label: 'Status', value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' }
  ];

  readonly list = createPagedList<RetailPriceListSummaryDto>((q) => this.priceListsApi.list(q), { sort: '-createdAt' });

  ngOnInit(): void {
    this.form.get('validFrom')!.valueChanges.subscribe(() => this.form.get('validTo')!.updateValueAndValidity());
    this.lookups.loadEntities();
    this.lookups.loadItems();
  }

  get itemsArray(): FormArray {
    return this.form.get('items') as FormArray;
  }

  validity(row: RetailPriceListSummaryDto): string {
    return validity(row);
  }

  private itemGroup(i?: RetailPriceListItemDto): FormGroup {
    return this.fb.group({
      itemId: [i?.itemId ?? null, [Validators.required]],
      itemCode: [i?.itemCode ?? ''],
      itemName: [i?.itemName ?? ''],
      basePrice: [i?.basePrice ?? 0, [Validators.required, Validators.min(0)]],
      price: [i?.price ?? null, [Validators.required, Validators.min(0)]],
      minQuantity: [i?.minQuantity ?? null]
    });
  }

  lineStatus(line: AbstractControl, key: string): 'error' | '' {
    const c = line.get(key);
    return c?.invalid && c.dirty ? 'error' : '';
  }

  addItem(): void {
    this.itemsArray.push(this.itemGroup());
  }

  removeItem(index: number): void {
    this.itemsArray.removeAt(index);
  }

  onItemChosen(index: number, itemId: string): void {
    const it = this.lookups.item(itemId);
    if (!it) return;
    const line = this.itemsArray.at(index);
    line.patchValue({
      itemCode: it.sku,
      itemName: it.name,
      basePrice: it.salesPrice ?? 0,
      price: line.value.price ?? it.salesPrice ?? null
    });
  }

  private setKeyFieldsEnabled(enabled: boolean): void {
    const control = this.form.get('legalEntityId')!;
    if (enabled) control.enable({ emitEvent: false });
    else control.disable({ emitEvent: false });
  }

  openEditModal(row: RetailPriceListSummaryDto): void {
    this.priceListsApi.getDetail(row.id).subscribe({
      next: (detail) => {
        this.editing.set(detail);
        this.fillForm(detail);
        this.setKeyFieldsEnabled(false);
        this.isModalVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The price list could not be loaded'))
    });
  }

  private fillForm(detail: RetailPriceListDetailDto): void {
    this.itemsArray.clear();
    detail.items.forEach((i) => this.itemsArray.push(this.itemGroup(i)));
    this.form.patchValue({
      legalEntityId: detail.legalEntityId,
      code: detail.code,
      name: detail.name,
      type: detail.type,
      currency: detail.currency,
      validFrom: detail.validFrom ? localDay(detail.validFrom) : null,
      validTo: detail.validTo ? localDay(detail.validTo) : null,
      isDefault: detail.isDefault,
      isActive: detail.isActive,
      description: detail.description ?? ''
    });
  }

  deletePriceList(row: RetailPriceListSummaryDto): void {
    this.priceListsApi.delete(row.id).subscribe({
      next: () => {
        this.msg.success(doneText('deleted', 'Price list', row.name));
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The price list could not be deleted'))
    });
  }

  openCreateModal(): void {
    this.editing.set(null);
    this.setKeyFieldsEnabled(true);
    this.itemsArray.clear();
    this.form.reset({
      legalEntityId: defaultEntityId(this.entities(), this.sessionStore.currentEntityId()),
      code: '',
      name: '',
      type: 'standard',
      currency: this.currencyService.code(),
      validFrom: null,
      validTo: null,
      isDefault: false,
      isActive: true,
      description: ''
    });
    this.isModalVisible.set(true);
  }

  closeModal(): void {
    this.isModalVisible.set(false);
  }

  viewDetail(row: RetailPriceListSummaryDto): void {
    this.priceListsApi.getDetail(row.id).subscribe({
      next: (detail) => {
        this.selectedDetail.set(detail);
        this.isDrawerVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The price list could not be loaded'))
    });
  }

  savePriceList(): void {
    if (this.form.invalid) {
      revealInvalid(this.form);
      this.itemsArray.controls.forEach((line) => revealErrors(line as FormGroup, { onlySelf: true }));
      this.msg.warning('Fill in the highlighted fields, including every item');
      return;
    }

    this.isSaving.set(true);
    const val = this.form.getRawValue() as PriceListFormValue;
    const editing = this.editing();
    const input = toPriceListInput(val, editing?.customFields ?? null);

    (editing ? this.priceListsApi.update(editing.id, input) : this.priceListsApi.create(input)).subscribe({
      next: () => {
        this.msg.success(doneText('saved', 'Price list', val.name));
        this.isSaving.set(false);
        this.closeModal();
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The price list could not be saved'));
        this.isSaving.set(false);
      }
    });
  }
}
