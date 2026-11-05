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
import { ItemDto, ItemsApi, LegalEntitiesApi } from '../../core/api/admin.api';
import { BomDto, BomInput, BomSummaryDto, ManufacturingBomsApi } from '../../core/api/manufacturing.api';
import { createPagedList } from '../../core/api/paged-list';
import { problemMessage } from '../../core/http/problem-details';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

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
  template: `
    <erp-paged-table [list]="list" [columns]="columns" (rowClick)="viewDetail($event)" searchPlaceholder="Search by code or name" emptyText="No bills of materials yet. Add one to plan production.">
      <button toolbar *erpHasPermission="'manufacturing.boms.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="bom-new">
        <span nz-icon nzType="plus"></span> Add BOM
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('code') { <span class="code strong">{{ row.code }}</span> }
          @case ('name') {
            <div>{{ row.name }}</div>
            <div class="sub">Makes {{ row.producedItemName }}</div>
          }
          @case ('outputQuantity') { <span class="num">{{ row.outputQuantity | number:'1.0-4' }} {{ row.uom }}</span> }
          @case ('totalUnitCost') { <span class="num strong">{{ currency() }} {{ row.totalUnitCost | number:'1.2-2' }}</span> }
          @case ('version') { <span class="nowrap">v{{ row.version }}</span> }
          @case ('componentCount') { <span class="num">{{ row.componentCount }}</span> }
          @case ('isActive') { <erp-status [value]="row.isActive ? 'active' : 'inactive'" /> }
          @case ('actions') {
            <div class="table-actions" (click)="$event.stopPropagation()">
              <button *erpHasPermission="'manufacturing.boms.manage'" nz-button nzType="link" nzSize="small" (click)="openEditDrawer(row)">
                <span nz-icon nzType="edit"></span> Edit
              </button>
              <button *erpHasPermission="'manufacturing.boms.manage'" nz-button nzType="link" nzDanger nzSize="small"
                      nz-popconfirm [nzPopconfirmTitle]="'Delete ' + row.code + '?'" (nzOnConfirm)="deleteBom(row)">
                <span nz-icon nzType="delete"></span> Delete
              </button>
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

    <!-- Detail Drawer -->
    <nz-drawer [nzVisible]="detailVisible()" [nzWidth]="650" [nzTitle]="selectedBom()?.code ?? 'Bill of materials'" (nzOnClose)="detailVisible.set(false)">
      <ng-container *nzDrawerContent>
        @if (selectedBom(); as bom) {
          <dl class="facts">
            <div><dt>Name</dt><dd>{{ bom.name }}</dd></div>
            <div><dt>Status</dt><dd><erp-status [value]="bom.isActive ? 'active' : 'inactive'" /></dd></div>
            <div><dt>Product</dt><dd>{{ bom.producedItemName }} <span class="code sub">{{ bom.producedItemCode }}</span></dd></div>
            <div><dt>Batch output</dt><dd>{{ bom.outputQuantity | number:'1.0-4' }} {{ bom.uom }}</dd></div>
            <div><dt>Version</dt><dd>v{{ bom.version }}</dd></div>
            <div><dt>Materials per batch</dt><dd>{{ currency() }} {{ bom.totalMaterialCost | number:'1.2-2' }}</dd></div>
            <div><dt>Labor per unit</dt><dd>{{ currency() }} {{ bom.laborCostPerUnit | number:'1.2-2' }}</dd></div>
            <div><dt>Overhead per unit</dt><dd>{{ currency() }} {{ bom.overheadCostPerUnit | number:'1.2-2' }}</dd></div>
            <div><dt>Standard cost per unit</dt><dd class="strong">{{ currency() }} {{ bom.totalUnitCost | number:'1.2-2' }}</dd></div>
          </dl>
          @if (bom.description) { <p class="sub">{{ bom.description }}</p> }

          <h4 class="section-title">Components</h4>
          <nz-table #compTable [nzData]="bom.components" [nzShowPagination]="false" [nzFrontPagination]="false" nzSize="small" nzNoResult="This bill of materials has no components yet.">
            <thead>
              <tr>
                <th>Component</th>
                <th nzAlign="right">Quantity</th>
                <th nzAlign="right">Scrap</th>
                <th nzAlign="right">Unit cost</th>
                <th nzAlign="right">Cost with scrap</th>
              </tr>
            </thead>
            <tbody>
              @for (c of compTable.data; track c.id) {
                <tr>
                  <td><strong>{{ c.componentItemName }}</strong><div class="code sub">{{ c.componentItemCode }}</div></td>
                  <td nzAlign="right" class="num">{{ c.quantityRequired | number:'1.0-4' }} {{ c.uom }}</td>
                  <td nzAlign="right" class="num">{{ c.scrapPercentage | number:'1.0-2' }}%</td>
                  <td nzAlign="right" class="num">{{ currency() }} {{ c.unitCost | number:'1.2-2' }}</td>
                  <td nzAlign="right" class="num">{{ currency() }} {{ c.extendedCost | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        }
      </ng-container>
    </nz-drawer>

    <!-- Create / Edit Drawer -->
    <nz-drawer [nzVisible]="createVisible()" [nzWidth]="760" [nzTitle]="editingBom() ? 'Edit ' + editingBom()!.code : 'Add bill of materials'" (nzOnClose)="createVisible.set(false)">
      <ng-container *nzDrawerContent>
        <form [formGroup]="bomForm" nz-form nzLayout="vertical" (ngSubmit)="saveBom()">
          @if (!editingBom()) {
            <nz-form-item>
              <nz-form-label nzRequired>Legal entity</nz-form-label>
              <nz-form-control nzErrorTip="Choose a legal entity">
                <nz-select formControlName="legalEntityId" nzPlaceHolder="Choose a legal entity">
                  @for (e of legalEntities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Code</nz-form-label>
              <nz-form-control nzErrorTip="Enter a code">
                <input nz-input formControlName="code" placeholder="e.g. BOM-PROD-001" />
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Product to produce</nz-form-label>
              <nz-form-control nzErrorTip="Choose a product">
                <nz-select formControlName="producedItemId" nzShowSearch nzPlaceHolder="Choose a product" (ngModelChange)="onProducedItemSelected($event)">
                  @for (it of products(); track it.id) { <nz-option [nzValue]="it.id" [nzLabel]="it.sku + ' - ' + it.name"></nz-option> }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
          }
          <nz-form-item>
            <nz-form-label nzRequired>Name</nz-form-label>
            <nz-form-control nzErrorTip="Enter a name">
              <input nz-input formControlName="name" placeholder="e.g. Office desk, standard build" />
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label nzRequired>Batch output</nz-form-label>
            <nz-form-control nzErrorTip="Enter a quantity above zero">
              <nz-input-number formControlName="outputQuantity" [nzMin]="0.01" class="full"></nz-input-number>
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label nzRequired>Unit of measure</nz-form-label>
            <nz-form-control nzErrorTip="Enter a unit">
              <input nz-input formControlName="uom" placeholder="e.g. PCS" />
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label>Version</nz-form-label>
            <nz-form-control>
              <input nz-input formControlName="version" placeholder="1.0" />
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label>Labor cost per unit ({{ currency() }})</nz-form-label>
            <nz-form-control>
              <nz-input-number formControlName="laborCostPerUnit" [nzMin]="0" class="full"></nz-input-number>
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label>Overhead cost per unit ({{ currency() }})</nz-form-label>
            <nz-form-control>
              <nz-input-number formControlName="overheadCostPerUnit" [nzMin]="0" class="full"></nz-input-number>
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-control>
              <label><nz-switch formControlName="isActive"></nz-switch><span class="switch-label">Active, can be used on new work orders</span></label>
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label>Description</nz-form-label>
            <nz-form-control>
              <textarea nz-input formControlName="description" rows="2" placeholder="Instructions and specifications"></textarea>
            </nz-form-control>
          </nz-form-item>

          <div class="components" formArrayName="components">
            <div class="components-header">
              <h4 class="section-title">Components</h4>
              <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addComponent()">
                <span nz-icon nzType="plus"></span> Add component
              </button>
            </div>
            @if (componentsArray.length === 0) {
              <p class="sub">No components yet. Add the materials one batch uses.</p>
            } @else {
              <div class="component-grid component-head" aria-hidden="true">
                <span>Component</span><span>Quantity per batch</span><span>Scrap %</span><span>Unit cost</span><span></span>
              </div>
            }
            @for (comp of componentsArray.controls; track comp; let i = $index) {
              <div class="component-grid" [formGroupName]="i">
                <nz-select formControlName="componentItemId" nzShowSearch nzPlaceHolder="Choose a product" (ngModelChange)="onComponentItemSelected(i, $event)" [attr.aria-label]="'Component ' + (i + 1)" [nzStatus]="lineStatus(comp, 'componentItemId')">
                  @for (it of products(); track it.id) { <nz-option [nzValue]="it.id" [nzLabel]="it.sku + ' - ' + it.name"></nz-option> }
                </nz-select>
                <nz-input-number formControlName="quantityRequired" [nzMin]="0.0001" class="full" nzPlaceHolder="Quantity" [nzStatus]="lineStatus(comp, 'quantityRequired')"></nz-input-number>
                <nz-input-number formControlName="scrapPercentage" [nzMin]="0" [nzMax]="100" class="full" nzPlaceHolder="Scrap %"></nz-input-number>
                <nz-input-number formControlName="unitCost" [nzMin]="0" class="full" nzPlaceHolder="Unit cost" [nzStatus]="lineStatus(comp, 'unitCost')"></nz-input-number>
                <button nz-button nzType="text" nzDanger type="button" nz-tooltip nzTooltipTitle="Remove component" aria-label="Remove component" (click)="removeComponent(i)">
                  <span nz-icon nzType="minus-circle"></span>
                </button>
              </div>
            }
          </div>

          <div class="drawer-footer">
            <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
            <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Save</button>
          </div>
        </form>
      </ng-container>
    </nz-drawer>
  `,
  styles: [`
    .code { font-family: var(--erp-font-family-mono); white-space: nowrap; }
    .strong { font-weight: 600; }
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .nowrap { white-space: nowrap; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px 16px; margin: 0 0 16px; }
    .facts dt { font-size: 12px; color: var(--erp-color-text-muted); }
    .facts dd { margin: 2px 0 0; }
    .section-title { margin: 16px 0 8px; font-size: 14px; font-weight: 600; }
    .components { grid-column: 1 / -1; }
    .components-header { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-block-end: 8px; }
    .components-header .section-title { margin: 0; }
    .component-grid { display: grid; grid-template-columns: minmax(0, 3fr) minmax(0, 1.2fr) minmax(0, 1fr) minmax(0, 1.2fr) 32px; gap: 8px; align-items: center; margin-block-end: 8px; }
    .component-head { font-size: 12px; color: var(--erp-color-text-muted); margin-block-end: 4px; }
    .switch-label { margin-inline-start: 8px; }
    .drawer-footer { grid-column: 1 / -1; display: flex; justify-content: flex-end; gap: 12px; margin-block-start: 24px; }
    .full { width: 100%; }
  `],
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
        this.message.success(editing ? 'Bill of materials updated' : 'Bill of materials added');
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
        this.message.success('Bill of materials deleted');
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'The bill of materials could not be deleted')),
    });
  }
}
