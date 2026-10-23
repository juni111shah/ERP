import { ChangeDetectionStrategy, Component, Injectable, inject, signal, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { CrudApiService } from '../core/api/crud-api.service';
import { createPagedList } from '../core/api/paged-list';
import { buildCustomFieldsGroup, toApiValues } from '../core/custom-fields/custom-field-form';
import { CustomFieldMetadataService } from '../core/custom-fields/custom-field-metadata.service';
import { FormSectionMeta } from '../core/custom-fields/custom-field.models';
import { DynamicFieldsComponent } from '../core/custom-fields/dynamic-field.component';
import { applyProblemDetailsToForm, asProblem, controlError } from '../core/http/problem-details';
import { HasPermissionDirective } from '../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../shared/paged-table.component';

export interface PartyDto {
  id: string; code: string; name: string; kind: string; roles: string[]; email?: string; phone?: string; taxNumber?: string;
  idNumber?: string; iban?: string; currencyCode?: string; paymentTermsDays?: number; address?: string; isActive: boolean;
  customFields: Record<string, unknown>;
}

@Injectable({ providedIn: 'root' })
export class PartiesApi extends CrudApiService<PartyDto, unknown> {
  protected readonly resource = 'parties';
}

const ROLES = ['Customer', 'Vendor', 'Employee', 'Other'];

/** Parties (customers, vendors, ...): generic paged table + master-data side drawer with custom fields. */
@Component({
  selector: 'erp-parties',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormsModule, PagedTableComponent, DynamicFieldsComponent, HasPermissionDirective, NzButtonModule, NzDrawerModule,
    NzFormModule, NzInputModule, NzSelectModule, NzSwitchModule, NzIconModule],
  template: `
    <h1 class="erp-page-title">{{ pageTitle() }}</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search name, code, email" (rowClick)="open($event)" emptyText="No parties yet">
      @if (!typeFilter()) {
        <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All roles" (ngModelChange)="filterRole($event)" [ngModelOptions]="{standalone: true}" [ngModel]="null" data-testid="party-role-filter">
          @for (r of roles; track r) { <nz-option [nzValue]="r" [nzLabel]="r"></nz-option> }
        </nz-select>
      }
      <button toolbar *erpHasPermission="'core.parties.write'" nz-button nzType="primary" (click)="open(null)" data-testid="party-new"><span nz-icon nzType="plus"></span>New party</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="640" [nzTitle]="editing() ? editing()!.name : 'New party'" (nzOnClose)="close()" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="party-form">
          <nz-form-item><nz-form-label nzRequired>Name</nz-form-label><nz-form-control [nzErrorTip]="err('name')"><input nz-input formControlName="name" data-testid="party-name" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Type</nz-form-label><nz-form-control>
            <nz-select formControlName="kind"><nz-option nzValue="company" nzLabel="Company"></nz-option><nz-option nzValue="individual" nzLabel="Individual"></nz-option></nz-select>
          </nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Roles</nz-form-label><nz-form-control [nzErrorTip]="err('roles')">
            <nz-select formControlName="roles" nzMode="multiple" data-testid="party-roles">@for (r of roles; track r) { <nz-option [nzValue]="r" [nzLabel]="r"></nz-option> }</nz-select>
          </nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Email</nz-form-label><nz-form-control [nzErrorTip]="err('email')"><input nz-input formControlName="email" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Phone</nz-form-label><nz-form-control><input nz-input formControlName="phone" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Tax number</nz-form-label><nz-form-control><input nz-input formControlName="taxNumber" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Active</nz-form-label><nz-form-control><nz-switch formControlName="isActive"></nz-switch></nz-form-control></nz-form-item>
          @if (sections().length) { <erp-dynamic-fields [sections]="sections()" [group]="customGroup()" /> }
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="close()">Cancel</button>
      <button nz-button nzType="primary" [nzLoading]="saving()" (click)="save()" data-testid="party-save" style="margin-inline-start:8px">Save</button>
    </ng-template>`,
})
export class PartiesPage implements OnInit {
  private readonly api = inject(PartiesApi);
  private readonly meta = inject(CustomFieldMetadataService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  readonly roles = ROLES;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code' });
  readonly typeFilter = signal<string | null>(null);
  readonly pageTitle = signal('Parties');
  readonly columns: TableColumn<PartyDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '120px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'roles', label: 'Roles' },
    { key: 'email', label: 'Email', sortField: 'email' },
    { key: 'isActive', label: 'Active', sortField: 'isActive', width: '90px' },
  ];
  readonly drawerOpen = signal(false);
  readonly editing = signal<PartyDto | null>(null);
  readonly saving = signal(false);
  readonly sections = signal<FormSectionMeta[]>([]);
  readonly customGroup = signal<FormGroup>(new FormGroup({}));
  form = this.newForm();

  ngOnInit(): void {
    const type = this.route.snapshot.queryParamMap.get('type');
    if (type) {
      this.typeFilter.set(type);
      this.pageTitle.set(type === 'customer' ? 'Customers' : type === 'vendor' ? 'Vendors' : 'Parties');
      this.filterRole(type.charAt(0).toUpperCase() + type.slice(1));
    }
  }

  filterRole(role: string | null): void {
    this.list.setFilters(role ? [{ field: 'roles', op: 'has', value: role.toLowerCase() }] : []);
  }

  open(p: PartyDto | null): void {
    this.editing.set(p);
    this.form = this.newForm(p);
    this.meta.sections('party').subscribe((s) => {
      this.sections.set(s);
      const group = buildCustomFieldsGroup(s, p?.customFields, !p);
      this.form.setControl('customFields', group);
      this.customGroup.set(group);
    });
    this.drawerOpen.set(true);
  }

  close(): void { this.drawerOpen.set(false); }

  err(name: string): string { return controlError(this.form.get(name)); }

  save(): void {
    if (this.form.invalid) { Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const body = { ...v, roles: (v.roles as string[]).join(', '), customFields: toApiValues(this.sections(), (v.customFields ?? {}) as Record<string, unknown>) };
    const p = this.editing();
    this.saving.set(true);
    (p ? this.api.update(p.id, body) : this.api.create(body)).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success('Saved'); this.list.reload(); },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        const rest = problem ? applyProblemDetailsToForm(this.form, problem) : [];
        this.message.error(rest[0] ?? problem?.detail ?? problem?.title ?? 'Could not save');
      },
    });
  }

  private newForm(p?: PartyDto | null): FormGroup {
    return this.fb.group({
      name: [p?.name ?? '', [Validators.required, Validators.maxLength(200)]],
      kind: [p?.kind?.toLowerCase() ?? 'company', Validators.required],
      roles: [p?.roles ?? ['Customer'], Validators.required],
      email: [p?.email ?? '', Validators.email],
      phone: [p?.phone ?? ''],
      taxNumber: [p?.taxNumber ?? ''],
      isActive: [p?.isActive ?? true],
      customFields: new FormGroup({}),
    });
  }
}
