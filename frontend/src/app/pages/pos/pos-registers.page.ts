import { CommonModule, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  PosRegisterDto,
  PosRegisterInput,
  PosRegistersApi,
} from '../../core/api/pos.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-pos-registers',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzSwitchModule,
    NzDividerModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns">
      <button toolbar *erpHasPermission="'pos.registers.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="pos-register-new">
        <span nz-icon nzType="plus"></span>Add New Register
      </button>
          <ng-template #cell let-reg let-column="column">
            @if (column.key === 'code') {
              <strong>{{ reg.code }}</strong>
            } @else if (column.key === 'name') {
              {{ reg.name }}
            } @else if (column.key === 'receiptHeader') {
              {{ reg.receiptHeader || 'Default Store' }}
            } @else if (column.key === 'isActive') {
              <nz-tag [nzColor]="reg.isActive ? 'green' : 'default'">
                {{ reg.isActive ? 'Active' : 'Disabled' }}
              </nz-tag>
            } @else if (column.key === 'createdAt') {
              {{ reg.createdAt | date:'mediumDate' }}
            } @else if (column.key === 'actions') {
              <button *erpHasPermission="'pos.registers.manage'" nz-button nzType="link" nzSize="small" (click)="editRegister(reg)">
                Edit
              </button>
            }
          </ng-template>
        </erp-paged-table>

      <!-- Create / Edit Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible"
        [nzTitle]="editingId ? 'Edit Register' : 'New Cash Register'"
        [nzWidth]="420"
        (nzOnClose)="drawerVisible = false"
      >
        <ng-container *nzDrawerContent>
          <form nz-form nzLayout="vertical" (ngSubmit)="saveRegister()">
            <nz-form-item>
              <nz-form-label nzRequired>Register Code</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="formCode" name="code" placeholder="e.g. REG-01" [disabled]="!!editingId" required />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Station Name</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="formName" name="name" placeholder="e.g. Main Front Counter Till 1" required />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Legal Entity / Store</nz-form-label>
              <nz-form-control>
                <nz-select [(ngModel)]="formLegalEntityId" name="legalEntityId" required>
                  @for (e of entities(); track e.id) {
                    <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Receipt Header Title</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="formHeader" name="receiptHeader" placeholder="e.g. Enterprise Flagship Store" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Receipt Footer Message</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="formFooter" name="receiptFooter" placeholder="e.g. Please keep receipt for returns within 14 days" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Active Status</nz-form-label>
              <nz-form-control>
                <nz-switch [(ngModel)]="formIsActive" name="isActive"></nz-switch>
              </nz-form-control>
            </nz-form-item>

            <div style="margin-top: 24px; display: flex; gap: 8px;">
              <button nz-button type="button" (click)="drawerVisible = false">Cancel</button>
              <button nz-button nzType="primary" type="submit">Save Register</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
  `,
  styles: [``],
})
export class PosRegistersPage {
  private readonly api = inject(PosRegistersApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);

  readonly list = createPagedList<PosRegisterDto>((q: any) => this.api.list(q));
  readonly entities = signal<{ id: string; name: string }[]>([]);

  readonly columns: TableColumn<PosRegisterDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'receiptHeader', label: 'Receipt Header' },
    { key: 'isActive', label: 'Status', sortField: 'isActive' },
    { key: 'createdAt', label: 'Created', sortField: 'createdAt' },
    { key: 'actions', label: 'Actions' },
  ];

  drawerVisible = false;
  editingId: string | null = null;
  formCode = '';
  formName = '';
  formLegalEntityId = '';
  formHeader = '';
  formFooter = '';
  formIsActive = true;

  constructor() {
    this.entitiesApi.list({ page: 1, pageSize: 20 }).subscribe({
      next: (res: any) => {
        const items = res.items || [];
        this.entities.set(items.map((i: any) => ({ id: i.id, name: i.name })));
        if (items.length > 0 && !this.formLegalEntityId) {
          this.formLegalEntityId = items[0].id;
        }
      },
    });
  }

  openCreateDrawer(): void {
    this.editingId = null;
    this.formCode = `REG-0${this.list.items().length + 1}`;
    this.formName = '';
    this.formHeader = 'Enterprise Retail Store';
    this.formFooter = 'Thank you for your visit!';
    this.formIsActive = true;
    this.drawerVisible = true;
  }

  editRegister(r: PosRegisterDto): void {
    this.editingId = r.id;
    this.formCode = r.code;
    this.formName = r.name;
    this.formLegalEntityId = r.legalEntityId;
    this.formHeader = r.receiptHeader || '';
    this.formFooter = r.receiptFooter || '';
    this.formIsActive = r.isActive;
    this.drawerVisible = true;
  }

  saveRegister(): void {
    if (!this.formName.trim() || !this.formLegalEntityId) {
      this.message.warning('Please fill in required fields.');
      return;
    }

    const payload: PosRegisterInput = {
      legalEntityId: this.formLegalEntityId,
      code: this.formCode,
      name: this.formName,
      receiptHeader: this.formHeader || null,
      receiptFooter: this.formFooter || null,
      isActive: this.formIsActive,
    };

    if (this.editingId) {
      this.api.update(this.editingId, payload).subscribe({
        next: () => {
          this.drawerVisible = false;
          this.list.reload();
          this.message.success('Register updated.');
        },
        error: (err) => this.message.error(err?.error?.message || 'Update failed.'),
      });
    } else {
      this.api.create(payload).subscribe({
        next: () => {
          this.drawerVisible = false;
          this.list.reload();
          this.message.success('Register created.');
        },
        error: (err) => this.message.error(err?.error?.message || 'Creation failed.'),
      });
    }
  }
}
