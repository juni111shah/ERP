import { CommonModule } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
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
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  PosRegisterDto,
  PosRegisterInput,
  PosRegistersApi,
} from '../../core/api/pos.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatusTagComponent } from '../../shared/status-tag.component';

@Component({
  selector: 'erp-pos-registers',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ErpDatePipe,
    StatusTagComponent,
    FormsModule,
    RouterModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
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
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search code or name..." emptyText="No registers yet. Add one to start selling." exportFilename="pos-registers" [clickable]="false">
      <button toolbar *erpHasPermission="'pos.registers.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="pos-register-new">
        <span nz-icon nzType="plus"></span>Add New Register
      </button>
          <ng-template #cell let-reg let-column="column" let-value="value">
            @if (column.key === 'code') {
              <strong class="nowrap">{{ reg.code }}</strong>
            } @else if (column.key === 'name') {
              {{ reg.name }}
            } @else if (column.key === 'receiptHeader') {
              <span class="header-preview" [title]="reg.receiptHeader || ''">{{ reg.receiptHeader || '-' }}</span>
            } @else if (column.key === 'isActive') {
              <erp-status [value]="reg.isActive ? 'active' : 'inactive'" />
            } @else if (column.key === 'createdAt') {
              <span class="nowrap">{{ reg.createdAt | erpDate }}</span>
            } @else if (column.key === 'actions') {
              <button *erpHasPermission="'pos.registers.manage'" nz-button nzType="link" nzSize="small" (click)="editRegister(reg)">
                Edit
              </button>
            } @else {
              {{ value }}
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
              <nz-form-control [nzValidateStatus]="submitted && !formCode.trim() ? 'error' : ''" nzErrorTip="Enter a register code.">
                <input nz-input [(ngModel)]="formCode" name="code" placeholder="e.g. REG-01" [disabled]="!!editingId" required />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Station Name</nz-form-label>
              <nz-form-control [nzValidateStatus]="submitted && !formName.trim() ? 'error' : ''" nzErrorTip="Enter a station name.">
                <input nz-input [(ngModel)]="formName" name="name" placeholder="e.g. Front counter 1" required />
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
                <input nz-input [(ngModel)]="formHeader" name="receiptHeader" placeholder="Business or store name" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Receipt Footer Message</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="formFooter" name="receiptFooter" placeholder="e.g. Thank you for your visit" />
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
  styles: [`
    .nowrap { white-space: nowrap; }
    .header-preview { display: block; max-width: 340px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  `],
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
    { key: 'isActive', label: 'Status', filterField: 'isActive', filter: false, value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'createdAt', label: 'Created', sortField: 'createdAt' },
    { key: 'actions', label: 'Actions' },
  ];

  drawerVisible = false;
  submitted = false;
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
    this.submitted = false;
    this.editingId = null;
    this.formCode = `REG-0${this.list.items().length + 1}`;
    this.formName = '';
    this.formHeader = '';
    this.formFooter = '';
    this.formIsActive = true;
    this.drawerVisible = true;
  }

  editRegister(r: PosRegisterDto): void {
    this.submitted = false;
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
    this.submitted = true;
    if (!this.formCode.trim() || !this.formName.trim() || !this.formLegalEntityId) return;

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
        error: (err) => this.message.error(problemMessage(err, 'Update failed.')),
      });
    } else {
      this.api.create(payload).subscribe({
        next: () => {
          this.drawerVisible = false;
          this.list.reload();
          this.message.success('Register created.');
        },
        error: (err) => this.message.error(problemMessage(err, 'Creation failed.')),
      });
    }
  }
}
