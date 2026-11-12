import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ItemDto, ItemsApi } from '@core/api/admin.api';
import { BomComponentDto, BomDto, BomSummaryDto, ManufacturingBomsApi } from '@core/api/manufacturing.api';
import { createPagedList } from '@core/api/paged-list';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { setControlsEnabled } from '@shared/forms/set-controls-enabled';
import {
  BOM_CREATE_ONLY_FIELDS,
  BomFormValue,
  bomComponentGroup,
  bomEditValues,
  emptyBomForm,
  revealLeafErrors,
  toBomInput,
} from '../../manufacturing-boms';
import { ManufacturingLookups } from '../../manufacturing-lookups';

/** Bills of materials: the paged list, a BOM's components, and the create / edit drawer with its component rows. */
@Component({
  selector: 'erp-boms-list-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    NzButtonModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzSwitchModule,
    NzTableModule,
    NzToolTipModule,
    HasPermissionDirective,
    PagedTableComponent,
    StatusTagComponent,
  ],
  providers: [ManufacturingLookups],
  templateUrl: './boms-list.page.html',
  styleUrl: './boms-list.page.scss',
})
export class BomsListPageComponent implements OnInit {
  private readonly api = inject(ManufacturingBomsApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly lookups = inject(ManufacturingLookups);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly list = createPagedList<BomSummaryDto>((q) => this.api.list(q), { sort: '-createdAt' });
  readonly legalEntities = this.lookups.legalEntities;
  private readonly items = signal<ItemDto[]>([]);
  /** The server only takes product items, both to produce and as components. */
  readonly products = computed(() => this.items().filter((i) => i.type === 'product' && i.isActive));
  readonly detailVisible = signal(false);
  readonly createVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly selectedBom = signal<BomDto | null>(null);
  readonly editingBom = signal<BomDto | null>(null);

  readonly bomForm: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    code: ['', [Validators.required, Validators.maxLength(50)]],
    producedItemId: [null, [Validators.required]],
    producedItemCode: [''],
    producedItemName: [''],
    name: ['', [Validators.required, Validators.maxLength(150)]],
    outputQuantity: [1, [Validators.required, Validators.min(0.01)]],
    uom: ['PCS', [Validators.required]],
    version: ['1.0'],
    laborCostPerUnit: [0],
    overheadCostPerUnit: [0],
    isActive: [true],
    description: [''],
    components: this.fb.array([]),
  });

  readonly columns: TableColumn<BomSummaryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name and product', sortField: 'name' },
    { key: 'producedItemName', label: 'Product', hidden: true },
    { key: 'outputQuantity', label: 'Batch', align: 'right', value: (r) => `${r.outputQuantity} ${r.uom}` },
    { key: 'totalUnitCost', label: 'Unit cost', align: 'right' },
    { key: 'version', label: 'Version' },
    { key: 'componentCount', label: 'Components', align: 'right' },
    { key: 'isActive', label: 'Status', value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  get componentsArray(): FormArray {
    return this.bomForm.get('components') as FormArray;
  }

  ngOnInit(): void {
    this.itemsApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.items.set(res.items ?? []),
    });
    this.lookups.loadEntities();
  }

  onProducedItemSelected(itemId: string): void {
    const it = this.item(itemId);
    if (!it) return;
    this.bomForm.patchValue({
      producedItemCode: it.sku,
      producedItemName: it.name,
      name: this.bomForm.value.name || it.name,
      uom: it.uom || 'PCS',
    });
  }

  onComponentItemSelected(index: number, itemId: string): void {
    const it = this.item(itemId);
    if (!it) return;
    const row = this.componentsArray.at(index);
    row.patchValue({
      componentItemCode: it.sku,
      componentItemName: it.name,
      uom: it.uom || 'PCS',
      // A cost already typed is kept; the catalog only fills an empty one.
      unitCost: row.value.unitCost || it.purchasePrice || 0,
    });
  }

  lineStatus(line: AbstractControl, key: string): 'error' | '' {
    const c = line.get(key);
    return c?.invalid && c.dirty ? 'error' : '';
  }

  addComponent(): void {
    this.componentsArray.push(this.componentGroup());
  }

  removeComponent(index: number): void {
    this.componentsArray.removeAt(index);
  }

  openCreateDrawer(): void {
    this.editingBom.set(null);
    setControlsEnabled(this.bomForm, BOM_CREATE_ONLY_FIELDS, true);
    this.componentsArray.clear();
    const entity = this.lookups.defaultEntity(this.session.currentEntityId());
    this.bomForm.reset(emptyBomForm(entity?.id ?? ''));
    this.addComponent();
    this.createVisible.set(true);
  }

  openEditDrawer(row: BomSummaryDto): void {
    this.api.getDetail(row.id).subscribe({
      next: (bom) => {
        this.editingBom.set(bom);
        setControlsEnabled(this.bomForm, BOM_CREATE_ONLY_FIELDS, false);
        this.componentsArray.clear();
        bom.components.forEach((c) => this.componentsArray.push(this.componentGroup(c)));
        this.bomForm.patchValue(bomEditValues(bom));
        this.createVisible.set(true);
      },
      error: (err) => this.message.error(problemMessage(err, 'The bill of materials could not be loaded')),
    });
  }

  viewDetail(row: BomSummaryDto): void {
    this.api.getDetail(row.id).subscribe({
      next: (bom) => {
        this.selectedBom.set(bom);
        this.detailVisible.set(true);
      },
      error: (err) => this.message.error(problemMessage(err, 'The bill of materials could not be loaded')),
    });
  }

  saveBom(): void {
    if (this.bomForm.invalid) {
      revealLeafErrors(this.bomForm);
      this.message.warning('Fill in the highlighted fields, including every component');
      return;
    }
    this.isSubmitting.set(true);
    const editing = this.editingBom();
    const payload = toBomInput(this.bomForm.getRawValue() as BomFormValue, editing);
    const request = editing ? this.api.update(editing.id, payload) : this.api.create(payload);
    request.subscribe({
      next: () => {
        this.message.success(doneText(editing ? 'updated' : 'added', 'Bill of materials', payload.name));
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'The bill of materials could not be saved'));
        this.isSubmitting.set(false);
      },
    });
  }

  deleteBom(row: BomSummaryDto): void {
    this.api.delete(row.id).subscribe({
      next: () => {
        this.message.success(doneText('deleted', 'Bill of materials', row.name));
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'The bill of materials could not be deleted')),
    });
  }

  private item(id: string): ItemDto | undefined {
    return this.items().find((x) => x.id === id);
  }

  private componentGroup(c?: Partial<BomComponentDto>): FormGroup {
    return bomComponentGroup(this.fb, c);
  }
}
