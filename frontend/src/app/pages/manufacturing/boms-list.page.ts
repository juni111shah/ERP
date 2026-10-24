import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  BomDto,
  BomSummaryDto,
  ManufacturingBomsApi
} from '../../core/api/manufacturing.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-boms-list-page',
  standalone: true,
  imports: [
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
    NzSelectModule,
    NzTableModule,
    PagedTableComponent
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Bills of Materials (BOM)</h2>
          <p class="text-secondary">Define product recipes, standard bill-of-materials structures, and cost rollups</p>
        </div>
        <button nz-button nzType="primary" (click)="openCreateDrawer()">
          <span nz-icon nzType="plus"></span> Add BOM Recipe
        </button>
      </div>

      <nz-card [nzBordered]="false" class="mb-4">
        <erp-paged-table
          #table
          [list]="list"
          [columns]="columns">
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'code'" class="mono-code font-bold">{{ row.code }}</span>
              <span *ngSwitchCase="'name'">
                <a (click)="viewDetail(row)">{{ row.name }}</a>
              </span>
              <span *ngSwitchCase="'producedItemName'">
                <span>{{ row.producedItemName }}</span>
              </span>
              <span *ngSwitchCase="'outputQuantity'">
                <span>{{ row.outputQuantity }}</span>
              </span>
              <span *ngSwitchCase="'uom'">
                <nz-tag>{{ row.uom }}</nz-tag>
              </span>
              <span *ngSwitchCase="'totalUnitCost'">
                <strong>\${{ row.totalUnitCost | number:'1.2-2' }}</strong>
              </span>
              <span *ngSwitchCase="'version'">
                <nz-tag nzColor="blue">v{{ row.version }}</nz-tag>
              </span>
              <span *ngSwitchCase="'componentCount'">
                <nz-badge [nzCount]="row.componentCount" [nzStyle]="{ backgroundColor: '#1890ff' }"></nz-badge>
              </span>
              <span *ngSwitchCase="'isActive'">
                <nz-tag [nzColor]="row.isActive ? 'success' : 'default'">
                  {{ row.isActive ? 'Active' : 'Inactive' }}
                </nz-tag>
              </span>
              <span *ngSwitchCase="'actions'">
                <button nz-button nzType="link" nzSize="small" (click)="viewDetail(row)">
                  <span nz-icon nzType="eye"></span> Recipe
                </button>
                <button nz-button nzType="link" nzDanger nzSize="small" (click)="deleteBom(row)">
                  <span nz-icon nzType="delete"></span> Delete
                </button>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>
      </nz-card>

      <!-- Detail Drawer -->
      <nz-drawer
        [nzVisible]="detailVisible()"
        [nzWidth]="650"
        nzTitle="Bill of Materials Recipe"
        (nzOnClose)="detailVisible.set(false)">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedBom() as bom" class="detail-content">
            <div class="detail-header-card">
              <div class="detail-prop">
                <span class="label">BOM Code:</span>
                <span class="val font-semibold">{{ bom.code }}</span>
              </div>
              <div class="detail-prop">
                <span class="label">Product:</span>
                <span class="val">{{ bom.outputItemName }} ({{ bom.outputItemCode }})</span>
              </div>
              <div class="detail-prop">
                <span class="label">Batch Output:</span>
                <span class="val">{{ bom.baseQuantity }} {{ bom.unitOfMeasure }}</span>
              </div>
              <div class="detail-prop">
                <span class="label">Standard Unit Cost:</span>
                <span class="val font-semibold text-primary">\${{ bom.standardCostPerUnit | number:'1.2-2' }}</span>
              </div>
            </div>

            <h4 class="mt-4 mb-2">Recipe Components</h4>
            <nz-table #compTable [nzData]="bom.components" [nzShowPagination]="false" nzSize="small">
              <thead>
                <tr>
                  <th>Component</th>
                  <th>Qty Required</th>
                  <th>Unit Cost</th>
                  <th>Total Cost</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let c of compTable.data">
                  <td>
                    <div><strong>{{ c.componentItemName }}</strong></div>
                    <small class="text-secondary">{{ c.componentItemCode }}</small>
                  </td>
                  <td>{{ c.quantityPerUnit }} {{ c.unitOfMeasure }}</td>
                  <td>\${{ c.unitCost | number:'1.2-2' }}</td>
                  <td>\${{ c.extendedCost | number:'1.2-2' }}</td>
                </tr>
              </tbody>
            </nz-table>
          </div>
        </ng-container>
      </nz-drawer>

      <!-- Create / Edit Drawer -->
      <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="760"
        nzTitle="Create Bill of Materials (BOM)"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="bomForm" nz-form nzLayout="vertical" (ngSubmit)="saveBom()">
            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Workspace / Legal Entity</nz-form-label>
                <nz-form-control nzErrorTip="Please select a legal entity">
                  <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                    <nz-option *ngFor="let e of legalEntities()" [nzValue]="e.id" [nzLabel]="e.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>BOM Code</nz-form-label>
                <nz-form-control nzErrorTip="Code is required">
                  <input nz-input formControlName="code" placeholder="e.g. BOM-PROD-001" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Recipe / Product Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="name" placeholder="e.g. Ergonomic Office Desk Recipe" />
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Produced Item Code</nz-form-label>
                <nz-form-control nzErrorTip="Item code is required">
                  <input nz-input formControlName="outputItemCode" placeholder="e.g. DSK-ERGO-01" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-3 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Batch Output Qty</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="baseQuantity" [nzMin]="0.01" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Unit of Measure</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="unitOfMeasure">
                    <nz-option nzValue="PCS" nzLabel="Pieces (PCS)"></nz-option>
                    <nz-option nzValue="KG" nzLabel="Kilograms (KG)"></nz-option>
                    <nz-option nzValue="LTR" nzLabel="Liters (LTR)"></nz-option>
                    <nz-option nzValue="BOX" nzLabel="Boxes (BOX)"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Version</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="version" placeholder="1.0" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label>Labor Cost / Unit</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="laborCostPerUnit" [nzMin]="0" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Overhead Cost / Unit</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="overheadCostPerUnit" [nzMin]="0" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Description & Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="description" rows="2" placeholder="Recipe instructions and specifications..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="components-header">
              <h4>BOM Components / Raw Materials</h4>
              <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addComponent()">
                <span nz-icon nzType="plus"></span> Add Component
              </button>
            </div>

            <div formArrayName="components" class="components-list">
              <div *ngFor="let comp of componentsArray.controls; let i = index" [formGroupName]="i" class="component-row">
                <div class="grid grid-cols-4 gap-2">
                  <input nz-input formControlName="componentItemCode" placeholder="Raw Code (e.g. WD-OAK-01)" />
                  <input nz-input formControlName="componentItemName" placeholder="Raw Name (e.g. Oak Board)" />
                  <nz-input-number formControlName="quantityRequired" [nzMin]="0.01" nzPlaceHolder="Qty Req" class="w-full"></nz-input-number>
                  <div class="flex items-center gap-1">
                    <nz-input-number formControlName="unitCost" [nzMin]="0" nzPlaceHolder="Cost" class="w-full"></nz-input-number>
                    <button nz-button nzType="text" nzDanger nzSize="small" type="button" (click)="removeComponent(i)">
                      <span nz-icon nzType="minus-circle"></span>
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Save Recipe</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
    </div>
  `,
  styles: [`
    .page-container { padding: 1.5rem; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .page-header h2 { margin: 0; font-size: 1.5rem; font-weight: 600; }
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-primary { color: var(--erp-primary, #0ea5e9); }
    .detail-header-card { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem; background: var(--erp-bg-secondary, #f8fafc); padding: 1rem; border-radius: 8px; margin-bottom: 1rem; }
    .detail-prop { display: flex; flex-direction: column; }
    .detail-prop .label { font-size: 0.75rem; color: #64748b; text-transform: uppercase; }
    .detail-prop .val { font-size: 0.95rem; }
    .components-header { display: flex; justify-content: space-between; align-items: center; margin: 1.5rem 0 0.75rem 0; }
    .components-list { display: flex; flex-direction: column; gap: 0.5rem; max-height: 250px; overflow-y: auto; margin-bottom: 1.5rem; }
    .component-row { background: var(--erp-bg-secondary, #f8fafc); padding: 0.5rem; border-radius: 6px; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 2rem; }
    .grid { display: grid; }
    .grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .grid-cols-3 { grid-template-columns: repeat(3, minmax(0, 1fr)); }
    .grid-cols-4 { grid-template-columns: repeat(4, minmax(0, 1fr)); }
    .gap-2 { gap: 0.5rem; }
    .gap-4 { gap: 1rem; }
    .w-full { width: 100%; }
    .flex { display: flex; }
    .items-center { align-items: center; }
  `]
})
export class BomsListPageComponent implements OnInit {
  private readonly api = inject(ManufacturingBomsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly list = createPagedList<BomSummaryDto>((q: any) => this.api.list(q));
  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  readonly detailVisible = signal(false);
  readonly createVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly selectedBom = signal<BomDto | null>(null);

  bomForm!: FormGroup;

  readonly columns: TableColumn<BomSummaryDto>[] = [
    { key: 'code', label: 'BOM Code', sortField: 'code' },
    { key: 'name', label: 'Recipe Name', sortField: 'name' },
    { key: 'producedItemName', label: 'Product Produced', sortField: 'producedItemName' },
    { key: 'outputQuantity', label: 'Batch Qty' },
    { key: 'uom', label: 'UOM' },
    { key: 'totalUnitCost', label: 'Standard Unit Cost' },
    { key: 'version', label: 'Version' },
    { key: 'componentCount', label: 'Raw Items' },
    { key: 'isActive', label: 'Status' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  ngOnInit(): void {
    this.initForm();
    this.loadLegalEntities();
  }

  private initForm(): void {
    this.bomForm = this.fb.group({
      legalEntityId: ['', [Validators.required]],
      code: ['', [Validators.required]],
      name: ['', [Validators.required]],
      outputItemCode: ['', [Validators.required]],
      baseQuantity: [1, [Validators.required, Validators.min(0.01)]],
      unitOfMeasure: ['PCS', [Validators.required]],
      version: ['1.0'],
      laborCostPerUnit: [0],
      overheadCostPerUnit: [0],
      description: [''],
      components: this.fb.array([])
    });
  }

  get componentsArray(): FormArray {
    return this.bomForm.get('components') as FormArray;
  }

  addComponent(): void {
    const group = this.fb.group({
      componentItemId: ['3fa85f64-5717-4562-b3fc-2c963f66afa6'],
      componentItemCode: ['', Validators.required],
      componentItemName: ['', Validators.required],
      quantityRequired: [1, [Validators.required, Validators.min(0.01)]],
      unitOfMeasure: ['PCS'],
      unitCost: [0, [Validators.required, Validators.min(0)]],
      sequenceOrder: [this.componentsArray.length + 1]
    });
    this.componentsArray.push(group);
  }

  removeComponent(index: number): void {
    this.componentsArray.removeAt(index);
  }

  private loadLegalEntities(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.legalEntities.set(res.items.map(e => ({ id: e.id, name: e.name })));
          if (res.items.length > 0 && !this.bomForm.value.legalEntityId) {
            this.bomForm.patchValue({ legalEntityId: res.items[0].id });
          }
        }
      }
    });
  }

  openCreateDrawer(): void {
    this.bomForm.reset({
      legalEntityId: this.legalEntities()[0]?.id || '',
      baseQuantity: 1,
      unitOfMeasure: 'PCS',
      version: '1.0',
      laborCostPerUnit: 0,
      overheadCostPerUnit: 0
    });
    this.componentsArray.clear();
    this.addComponent();
    this.createVisible.set(true);
  }

  viewDetail(row: BomSummaryDto): void {
    this.api.getDetail(row.id).subscribe({
      next: res => {
        this.selectedBom.set(res.data);
        this.detailVisible.set(true);
      },
      error: () => this.message.error('Failed to load BOM detail')
    });
  }

  saveBom(): void {
    if (this.bomForm.invalid) {
      Object.values(this.bomForm.controls).forEach(c => c.markAsDirty());
      return;
    }
    this.isSubmitting.set(true);
    const v = this.bomForm.value;
    const payload = {
      legalEntityId: v.legalEntityId,
      code: v.code,
      name: v.name,
      outputItemId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
      outputItemCode: v.outputItemCode,
      outputItemName: v.name,
      baseQuantity: v.baseQuantity,
      unitOfMeasure: v.unitOfMeasure,
      version: v.version,
      routingNotes: v.description,
      components: v.components.map((c: any) => ({
        componentItemId: c.componentItemId,
        componentItemCode: c.componentItemCode,
        componentItemName: c.componentItemName,
        quantityPerUnit: c.quantityRequired,
        unitOfMeasure: c.unitOfMeasure || 'PCS',
        unitCost: c.unitCost,
        sequenceOrder: c.sequenceOrder || 1
      }))
    };

    this.api.create(payload as any).subscribe({
      next: () => {
        this.message.success('Bill of Materials created');
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: () => {
        this.message.error('Failed to create BOM');
        this.isSubmitting.set(false);
      }
    });
  }

  deleteBom(row: BomSummaryDto): void {
    this.api.delete(row.id).subscribe({
      next: () => {
        this.message.success('BOM deleted');
        this.list.reload();
      },
      error: () => this.message.error('Failed to delete BOM')
    });
  }
}
