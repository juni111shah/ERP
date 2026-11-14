import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { catchError, map, of } from 'rxjs';
import { AccountsApi, LegalEntitiesApi } from '@core/api/admin.api';
import { WarehousesApi } from '@core/api/inventory.api';
import { createPagedList } from '@core/api/paged-list';
import { EMPTY_GUID, PosRegisterDto, PosRegisterInput, PosRegistersApi } from '@core/api/pos.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { MasterDataState } from '@shared/utils/master-data-state';
import { CLEARABLE_REGISTER_ACCOUNTS, RegisterAccountOption, RegisterOption, registerFields } from '../../pos-register-form';

/** The tills: where each one sells from, which accounts its money goes to, and the wording on its receipts. */
@Component({
  selector: 'erp-pos-registers',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective],
  templateUrl: './pos-registers.page.html',
  styleUrl: './pos-registers.page.scss',
})
export class PosRegistersPage {
  private readonly registersApi = inject(PosRegistersApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);

  /** A row opens the edit form, so only for someone who may change registers (there is no read-only view of one). */
  readonly canManage = computed(() => this.session.hasPermission('pos.registers.manage'));

  readonly list = createPagedList<PosRegisterDto>((q) => this.registersApi.list(q));

  private readonly entities = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 50 }).pipe(
    map((r) => r.items.map((e): RegisterOption => ({ value: e.id, label: e.name }))),
    catchError(() => of([] as RegisterOption[])),
  ), { initialValue: [] as RegisterOption[] });

  // Whoever may not read warehouses or accounts still gets the rest of the form.
  private readonly warehouses = toSignal(inject(WarehousesApi).firstPage(200).pipe(
    map((r) => r.items.filter((w) => w.isActive).map((w): RegisterOption => ({ value: w.id, label: `${w.code} · ${w.name}` }))),
    catchError(() => of([] as RegisterOption[])),
  ), { initialValue: [] as RegisterOption[] });

  private readonly accounts = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200 }).pipe(
    map((r) => r.items.filter((a) => a.isPostable && a.isActive).map((a): RegisterAccountOption => ({ value: a.id, label: `${a.code} · ${a.name}`, type: a.type }))),
    catchError(() => of([] as RegisterAccountOption[])),
  ), { initialValue: [] as RegisterAccountOption[] });

  readonly columns: TableColumn<PosRegisterDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'warehouse', label: 'Warehouse', filter: false, value: (r) => this.warehouseName(r.warehouseId) },
    { key: 'receiptHeader', label: 'Receipt header', filter: false },
    { key: 'isActive', label: 'Status', filterField: 'isActive', filter: false, value: (r) => (r.isActive ? 'Active' : 'Inactive') },
    { key: 'createdAt', label: 'Created', sortField: 'createdAt' },
    { key: 'actions', label: 'Actions', filter: false },
  ];

  readonly fields = computed<FieldSpec[]>(() => registerFields(this.entities(), this.warehouses(), this.accounts()));

  readonly state = new MasterDataState<PosRegisterDto>({
    save: (id, body) => (id ? this.registersApi.update(id, body as PosRegisterInput) : this.registersApi.create(body as PosRegisterInput)),
    reload: () => this.list.reload(),
    message: this.message,
    entityName: 'Register',
  });

  warehouseName(id?: string | null): string {
    return (id && this.warehouses().find((w) => w.value === id)?.label.split(' · ')[0]) || '';
  }

  onRow(reg: PosRegisterDto): void {
    if (this.canManage()) this.state.edit(reg);
  }

  /** On the server an account left out keeps its value, so clearing one here sends the empty GUID. */
  save(values: Record<string, unknown>): void {
    if (!this.canManage()) return;
    const body = { ...values };
    for (const key of CLEARABLE_REGISTER_ACCOUNTS) {
      if (!body[key] && this.state.editing()) body[key] = EMPTY_GUID;
    }
    this.state.submit(body);
  }

  remove(reg: PosRegisterDto): void {
    this.modal.confirm({
      nzTitle: `Delete ${reg.name}?`,
      nzContent: 'Its past shifts and sales stay on record. A register with an open shift cannot be deleted.',
      nzOkText: 'Delete',
      nzOkDanger: true,
      nzOnOk: () => this.registersApi.delete(reg.id).subscribe({
        next: () => {
          this.list.reload();
          this.message.success(doneText('deleted', 'Register', reg.name));
        },
        error: (err) => this.message.error(problemMessage(err, 'The register could not be deleted.')),
      }),
    });
  }
}
