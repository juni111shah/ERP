import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { API_BASE_URL } from '../core/api/api-config';
import { PagedResult } from '../core/api/api.models';
import { toListParams } from '../core/api/paging';
import { buildCustomFieldsGroup, toApiValues } from '../core/custom-fields/custom-field-form';
import { CustomFieldMetadataService } from '../core/custom-fields/custom-field-metadata.service';
import { FormSectionMeta } from '../core/custom-fields/custom-field.models';
import { DynamicFieldsComponent } from '../core/custom-fields/dynamic-field.component';
import { applyProblemDetailsToForm, asProblem, controlError } from '../core/http/problem-details';
import { HasPermissionDirective } from '../shared/has-permission.directive';

interface PartyDto {
  id: string; code: string; name: string; kind: string; roles: string[]; email?: string; phone?: string; taxNumber?: string;
  idNumber?: string; iban?: string; currencyCode?: string; paymentTermsDays?: number; address?: string; isActive: boolean;
  customFields: Record<string, unknown>;
}

const ROLES = ['customer', 'vendor', 'employee', 'other'];

/** Parties (customers, vendors...): server-paged list with search, and a drawer form that renders the tenant's custom fields. */
@Component({
  selector: 'erp-parties',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzTableModule, NzButtonModule, NzInputModule, NzInputNumberModule, NzDrawerModule, NzFormModule,
    NzSelectModule, NzSwitchModule, NzTagModule, DynamicFieldsComponent, HasPermissionDirective],
  styles: [`.bar { display: flex; gap: 12px; margin-bottom: 16px; } .bar nz-input-group { max-width: 360px; } .spacer { flex: 1; }`],
  template: `
    <h1>Parties</h1>
    <div class="bar">
      <nz-input-group nzSearch [nzAddOnAfter]="searchBtn">
        <input #searchInput nz-input placeholder="Search name, code, email" (keyup.enter)="runSearch(searchInput.value)" data-testid="parties-search" />
      </nz-input-group>
      <ng-template #searchBtn><button nz-button nzType="primary" nzSearch (click)="runSearch(searchInput.value)" aria-label="Search">Search</button></ng-template>
      <span class="spacer"></span>
      <button *erpHasPermission="'core.parties.write'" nz-button nzType="primary" (click)="openCreate()" data-testid="party-new">New party</button>
    </div>

    <nz-table nzShowSizeChanger [nzFrontPagination]="false" [nzLoading]="loading()" [nzTotal]="total()" [nzPageIndex]="page()" [nzPageSize]="pageSize()"
              [nzData]="rows()" (nzQueryParams)="onQuery($event)" data-testid="parties-table">
      <thead><tr><th>Code</th><th>Name</th><th>Roles</th><th>Email</th><th>Status</th><th></th></tr></thead>
      <tbody>
        @for (p of rows(); track p.id) {
          <tr [attr.data-testid]="'party-row-' + p.code">
            <td><strong>{{ p.code }}</strong></td>
            <td>{{ p.name }}</td>
            <td>@for (r of p.roles; track r) { <nz-tag>{{ r }}</nz-tag> }</td>
            <td>{{ p.email }}</td>
            <td><nz-tag [nzColor]="p.isActive ? 'green' : 'default'">{{ p.isActive ? 'Active' : 'Inactive' }}</nz-tag></td>
            <td><a *erpHasPermission="'core.parties.write'" (click)="openEdit(p)">Edit</a></td>
          </tr>
        }
      </tbody>
    </nz-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzTitle]="editingId() ? 'Edit party' : 'New party'" nzWidth="560" (nzOnClose)="drawerOpen.set(false)">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="save()" data-testid="party-form">
          <nz-form-item><nz-form-label nzRequired>Name</nz-form-label>
            <nz-form-control [nzErrorTip]="err('name')"><input nz-input formControlName="name" data-testid="party-name" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Type</nz-form-label>
            <nz-form-control><nz-select formControlName="kind"><nz-option nzValue="company" nzLabel="Company"></nz-option><nz-option nzValue="individual" nzLabel="Individual"></nz-option></nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Roles</nz-form-label>
            <nz-form-control [nzErrorTip]="err('roles')"><nz-select formControlName="roles" nzMode="multiple" data-testid="party-roles">
              @for (r of roleOptions; track r) { <nz-option [nzValue]="r" [nzLabel]="r"></nz-option> }
            </nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Email</nz-form-label>
            <nz-form-control [nzErrorTip]="err('email')"><input nz-input type="email" formControlName="email" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Phone</nz-form-label><nz-form-control><input nz-input formControlName="phone" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Tax number</nz-form-label><nz-form-control><input nz-input formControlName="taxNumber" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Currency</nz-form-label>
            <nz-form-control [nzErrorTip]="err('currencyCode')"><input nz-input formControlName="currencyCode" maxlength="3" placeholder="ISO code" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Payment terms (days)</nz-form-label>
            <nz-form-control><nz-input-number formControlName="paymentTermsDays" [nzMin]="0" [nzPrecision]="0" style="width:100%"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Address</nz-form-label><nz-form-control><textarea nz-input rows="2" formControlName="address"></textarea></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Active</nz-form-label><nz-form-control><nz-switch formControlName="isActive"></nz-switch></nz-form-control></nz-form-item>
          <erp-dynamic-fields [sections]="sections()" [group]="customGroup()" />
          <button nz-button nzType="primary" nzBlock [nzLoading]="saving()" data-testid="party-save">Save</button>
        </form>
      </ng-container>
    </nz-drawer>`,
})
export class PartiesPage {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly fb = inject(FormBuilder);
  private readonly message = inject(NzMessageService);
  private readonly metadata = inject(CustomFieldMetadataService);

  readonly roleOptions = ROLES;
  readonly rows = signal<PartyDto[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly search = signal('');
  readonly loading = signal(false);
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly sections = signal<FormSectionMeta[]>([]);
  readonly customGroup = signal<FormGroup>(new FormGroup({}));

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    kind: ['company'],
    roles: [['customer'] as string[], Validators.required],
    email: ['', Validators.email],
    phone: [''],
    taxNumber: [''],
    currencyCode: ['', [Validators.minLength(3), Validators.maxLength(3)]],
    paymentTermsDays: [null as number | null],
    address: [''],
    isActive: [true],
  });

  err(name: string): string { return controlError(this.form.get(name)); }

  runSearch(term: string): void {
    this.search.set(term.trim());
    this.page.set(1);
    this.load();
  }

  onQuery(q: NzTableQueryParams): void {
    this.page.set(q.pageIndex);
    this.pageSize.set(q.pageSize);
    this.load();
  }

  openCreate(): void { this.open(null); }
  openEdit(p: PartyDto): void { this.open(p); }

  save(): void {
    if (this.form.invalid || this.customGroup().invalid) {
      this.form.markAllAsTouched();
      this.customGroup().markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      ...v,
      roles: v.roles.join(','),
      email: v.email || null, phone: v.phone || null, taxNumber: v.taxNumber || null,
      currencyCode: v.currencyCode ? v.currencyCode.toUpperCase() : null, address: v.address || null,
      customFields: toApiValues(this.sections(), this.customGroup().getRawValue()),
    };
    const id = this.editingId();
    this.saving.set(true);
    const call = id ? this.http.put(`${this.base}/parties/${id}`, body) : this.http.post(`${this.base}/parties`, body);
    call.subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success('Party saved'); this.load(); },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const unmatched = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(unmatched[0] ?? p?.detail ?? p?.title ?? 'Could not save the party');
      },
    });
  }

  private open(p: PartyDto | null): void {
    this.metadata.sections('party').subscribe((sections) => {
      this.sections.set(sections);
      this.customGroup.set(buildCustomFieldsGroup(sections, p?.customFields, !p));
      this.editingId.set(p?.id ?? null);
      this.form.reset({
        name: p?.name ?? '', kind: p?.kind ?? 'company', roles: p ? p.roles.map((r) => r.toLowerCase()) : ['customer'],
        email: p?.email ?? '', phone: p?.phone ?? '', taxNumber: p?.taxNumber ?? '', currencyCode: p?.currencyCode ?? '',
        paymentTermsDays: p?.paymentTermsDays ?? null, address: p?.address ?? '', isActive: p?.isActive ?? true,
      });
      this.drawerOpen.set(true);
    });
  }

  private load(): void {
    this.loading.set(true);
    const params = toListParams({ page: this.page(), pageSize: this.pageSize() }, { search: this.search() });
    this.http.get<PagedResult<PartyDto>>(`${this.base}/parties`, { params }).subscribe({
      next: (r) => { this.rows.set(r.items); this.total.set(r.total); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('Could not load parties'); },
    });
  }
}
