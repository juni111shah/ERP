import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
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
import { ItemDto, ItemsApi, LegalEntitiesApi } from '@core/api/admin.api';
import { BomDto, BomInput, BomSummaryDto, ManufacturingBomsApi } from '@core/api/manufacturing.api';
import { createPagedList } from '@core/api/paged-list';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

@Component({
  selector: 'erp-boms-list-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    NzButtonModule,
    NzIconModule,
    NzSwitchModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzTableModule,
    NzToolTipModule,
    PagedTableComponent,
    HasPermissionDirective,
    StatusTagComponent,
  ],
  templateUrl: './boms-list.page.html',
  styleUrl: './boms-list.page.scss',
})
export class BomsListPageComponent implements OnInit {
  private readonly api = inject(ManufacturingBomsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly itemsApi = inject(ItemsApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly list = createPagedList<BomSummaryDto>((q) => this.api.list(q), { sort: '-createdAt' });
  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
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
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.legalEntities.set(res.items.map((e) => ({ id: e.id, name: e.name }))),
    });
  }

  onProducedItemSelected(itemId: string): void {
    const it = this.items().find((x) => x.id === itemId);
    if (!it) return;
    this.bomForm.patchValue({
      producedItemCode: it.sku,
      producedItemName: it.name,
      name: this.bomForm.value.name || it.name,
      uom: it.uom || 'PCS',
    });
  }

  onComponentItemSelected(index: number, itemId: string): void {
    const it = this.items().find((x) => x.id === itemId);
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

  private componentGroup(c?: Partial<BomDto['components'][number]>): FormGroup {
    return this.fb.group({
      componentItemId: [c?.componentItemId ?? null, Validators.required],
      componentItemCode: [c?.componentItemCode ?? ''],
      componentItemName: [c?.componentItemName ?? ''],
      quantityRequired: [c?.quantityRequired ?? 1, [Validators.required, Validators.min(0.0001)]],
      uom: [c?.uom ?? 'PCS'],
      scrapPercentage: [c?.scrapPercentage ?? 0, [Validators.min(0), Validators.max(100)]],
      unitCost: [c?.unitCost ?? 0, [Validators.required, Validators.min(0)]],
      notes: [c?.notes ?? null],
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

  /** Legal entity, code and product cannot change on an existing BOM, so they are not validated when editing. */
  private setCreateFieldsEnabled(enabled: boolean): void {
    for (const key of ['legalEntityId', 'code', 'producedItemId']) {
      const control = this.bomForm.get(key)!;
      if (enabled) control.enable({ emitEvent: false });
      else control.disable({ emitEvent: false });
    }
  }

  openCreateDrawer(): void {
    this.editingBom.set(null);
    this.setCreateFieldsEnabled(true);
    this.componentsArray.clear();
    const entity = this.legalEntities().find((e) => e.id === this.session.currentEntityId()) ?? this.legalEntities()[0];
    this.bomForm.reset({
      legalEntityId: entity?.id ?? '',
      code: '',
      producedItemId: null,
      producedItemCode: '',
      producedItemName: '',
      name: '',
      outputQuantity: 1,
      uom: 'PCS',
      version: '1.0',
      laborCostPerUnit: 0,
      overheadCostPerUnit: 0,
      isActive: true,
      description: '',
    });
    this.addComponent();
    this.createVisible.set(true);
  }

  openEditDrawer(row: BomSummaryDto): void {
    this.api.getDetail(row.id).subscribe({
      next: (bom) => {
        this.editingBom.set(bom);
        this.setCreateFieldsEnabled(false);
        this.componentsArray.clear();
        bom.components.forEach((c) => this.componentsArray.push(this.componentGroup(c)));
        this.bomForm.patchValue({
          name: bom.name,
          outputQuantity: bom.outputQuantity,
          uom: bom.uom,
          version: bom.version,
          laborCostPerUnit: bom.laborCostPerUnit,
          overheadCostPerUnit: bom.overheadCostPerUnit,
          isActive: bom.isActive,
          description: bom.description ?? '',
        });
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
      const mark = (group: FormGroup | FormArray) => Object.values(group.controls).forEach((c) => {
        if (c instanceof FormGroup || c instanceof FormArray) mark(c);
        else { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); }
      });
      mark(this.bomForm);
      this.message.warning('Fill in the highlighted fields, including every component');
      return;
    }
    this.isSubmitting.set(true);
    const v = this.bomForm.getRawValue();
    const editing = this.editingBom();
    const payload: BomInput = {
      legalEntityId: v.legalEntityId,
      code: v.code,
      name: v.name,
      producedItemId: v.producedItemId,
      producedItemCode: v.producedItemCode,
      producedItemName: v.producedItemName,
      outputQuantity: v.outputQuantity,
      uom: v.uom,
      version: v.version,
      laborCostPerUnit: v.laborCostPerUnit ?? 0,
      overheadCostPerUnit: v.overheadCostPerUnit ?? 0,
      isActive: v.isActive,
      description: v.description || null,
      components: v.components.map((c: BomInput['components'][number]) => ({ ...c, scrapPercentage: c.scrapPercentage ?? 0 })),
      customFields: editing?.customFields ?? null,
    };
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
}
