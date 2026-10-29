import { ChangeDetectionStrategy, Component, DestroyRef, Injectable, computed, inject, signal, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
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
import { SessionStore } from '../core/session/session.store';
import { HasPermissionDirective } from '../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../shared/paged-table.component';
import { RevealDirective } from '../shared/reveal.directive';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../core/i18n/translate.pipe';
import { StatCardComponent } from '../shared/stat-card.component';

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
const titleFor = (type: string | null) => (type === 'customer' ? 'Customers' : type === 'vendor' ? 'Vendors' : 'Parties');
const roleFilter = (type: string | null) => (type ? [{ field: 'roles', op: 'has' as const, value: type.toLowerCase() }] : []);

@Component({
  selector: 'erp-parties',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, ReactiveFormsModule, FormsModule, PagedTableComponent, DynamicFieldsComponent, HasPermissionDirective, NzButtonModule, NzDrawerModule,
    NzFormModule, NzInputModule, NzSelectModule, NzSwitchModule, NzIconModule, NzTagModule, StatCardComponent],
  template: `
    <h1 class="erp-page-title">{{ pageTitle() }}</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="party-stats">
      <erp-stat-card [label]="'Total ' + (typeFilter() === 'customer' ? 'customers' : typeFilter() === 'vendor' ? 'vendors' : 'parties')"
                     [value]="stats().total"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     [caption]="stats().countOnPage + ' ' + ('table.onThisPage' | translate: 'on this page')"
                     [tone]="0" />
      <erp-stat-card label="Active"
                     [value]="stats().active"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="active accounts"
                     [tone]="1" />
      <erp-stat-card label="Corporate"
                     [value]="stats().companies"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="company entities"
                     [tone]="2" />
      <erp-stat-card label="Individual"
                     [value]="stats().individuals"
                     format="number"
                     [loading]="list.loading() && list.items().length === 0"
                     caption="private clients"
                     [tone]="3" />
    </div>

    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search name, code, email" (rowClick)="open($event)" [emptyText]="emptyText()">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('code') {
            <span class="code-pill">{{ row.code }}</span>
          }
          @case ('name') {
            <span class="name-cell">{{ row.name }}</span>
          }
          @case ('roles') {
            <div style="display:flex;gap:4px;flex-wrap:wrap">
              @for (r of row.roles; track r) {
                <nz-tag [nzColor]="roleTagColor(r)">{{ r }}</nz-tag>
              }
            </div>
          }
          @case ('email') {
            @if (row.email) {
              <a [href]="'mailto:' + row.email" class="email-link" (click)="$event.stopPropagation()">
                <span nz-icon nzType="mail"></span> {{ row.email }}
              </a>
            } @else {
              <span class="muted-text">—</span>
            }
          }
          @case ('isActive') {
            <nz-tag [nzColor]="row.isActive ? 'success' : 'default'">
              <span nz-icon [nzType]="row.isActive ? 'check-circle' : 'minus-circle'"></span>
              {{ row.isActive ? 'Active' : 'Inactive' }}
            </nz-tag>
          }
          @default {
            {{ value }}
          }
        }
      </ng-template>
      @if (!typeFilter()) {
        <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All roles" (ngModelChange)="filterRole($event)" [ngModelOptions]="{standalone: true}" [ngModel]="null" data-testid="party-role-filter">
          @for (r of roles; track r) { <nz-option [nzValue]="r" [nzLabel]="r"></nz-option> }
        </nz-select>
      }
      <button toolbar *erpHasPermission="'core.parties.write'" nz-button nzType="primary" (click)="open(null)" data-testid="party-new"><span nz-icon nzType="plus"></span>New {{ typeFilter() === 'customer' ? 'customer' : typeFilter() === 'vendor' ? 'vendor' : 'party' }}</button>
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
          <nz-form-item><nz-form-label>Address</nz-form-label><nz-form-control><textarea nz-input rows="2" formControlName="address" data-testid="party-address"></textarea></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Currency</nz-form-label><nz-form-control><input nz-input formControlName="currencyCode" maxlength="3" placeholder="e.g. AED" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Payment terms (days)</nz-form-label><nz-form-control><input nz-input type="number" min="0" formControlName="paymentTermsDays" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>ID number</nz-form-label><nz-form-control [nzExtra]="sensitiveLocked() ? restrictedHint : ''"><input nz-input formControlName="idNumber" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>IBAN</nz-form-label><nz-form-control [nzExtra]="sensitiveLocked() ? restrictedHint : ''"><input nz-input formControlName="iban" /></nz-form-control></nz-form-item>
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
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly session = inject(SessionStore);
  readonly roles = ROLES;
  readonly restrictedHint = "Restricted: you don't have permission to view or edit this field";
  /** Editing an existing party without core.sensitive.read: ID number and IBAN arrive masked, so they are read-only. */
  readonly sensitiveLocked = signal(false);
  /** `?type=` known up front, so the first request is already filtered (no unfiltered load racing the filtered one). */
  private readonly initialType = this.route.snapshot.queryParamMap.get('type');
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'code', filters: roleFilter(this.initialType) });
  readonly typeFilter = signal<string | null>(this.initialType);
  readonly pageTitle = signal(titleFor(this.initialType));
  /** Never say "No parties yet" while loading or after a failed load: both looked like an empty list. */
  readonly emptyText = computed(() => {
    if (this.list.loading()) return 'Loading…';
    if (this.list.error()) return 'Could not load the list. Reload the page to try again.';
    const type = this.typeFilter();
    return type === 'customer' ? 'No customers yet' : type === 'vendor' ? 'No vendors yet' : 'No parties yet';
  });
  readonly columns: TableColumn<PartyDto>[] = [
    { key: 'code', label: 'Code', sortField: 'code', width: '130px' },
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'roles', label: 'Roles', width: '160px' },
    { key: 'email', label: 'Email', sortField: 'email' },
    { key: 'isActive', label: 'Active', sortField: 'isActive', width: '110px' },
  ];
  readonly stats = computed(() => {
    const rows = this.list.items();
    return {
      total: this.list.total() || rows.length,
      active: rows.filter((r) => r.isActive).length,
      companies: rows.filter((r) => r.kind === 'company').length,
      individuals: rows.filter((r) => r.kind === 'individual').length,
      countOnPage: rows.length,
    };
  });

  roleTagColor(role: string): string {
    switch ((role ?? '').toLowerCase()) {
      case 'customer': return 'blue';
      case 'vendor': return 'purple';
      case 'employee': return 'cyan';
      default: return 'default';
    }
  }
  readonly drawerOpen = signal(false);
  readonly editing = signal<PartyDto | null>(null);
  readonly saving = signal(false);
  readonly sections = signal<FormSectionMeta[]>([]);
  readonly customGroup = signal<FormGroup>(new FormGroup({}));
  form = this.newForm();

  ngOnInit(): void {
    // Re-apply on every query change: Customers -> Vendors from the menu reuses this component.
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((qp) => {
      const type = qp.get('type');
      if (type !== this.typeFilter()) {
        this.typeFilter.set(type);
        this.pageTitle.set(titleFor(type));
        this.list.setFilters(roleFilter(type));
      }
      // `?edit=<id>` (from the party detail page's Edit) opens the edit drawer for that party.
      const editId = qp.get('edit');
      if (editId && !(this.drawerOpen() && this.editing()?.id === editId)) this.api.get(editId).subscribe((p) => this.edit(p));
    });
  }

  private edit(p: PartyDto): void {
    this.editing.set(p);
    this.form = this.newForm(p);
    this.meta.sections('party').subscribe((s) => {
      this.sections.set(s);
      const group = buildCustomFieldsGroup(s, p.customFields as never, false);
      this.form.setControl('customFields', group);
      this.customGroup.set(group);
    });
    this.drawerOpen.set(true);
  }

  filterRole(role: string | null): void {
    this.list.setFilters(role ? [{ field: 'roles', op: 'has', value: role.toLowerCase() }] : []);
  }

  open(p: PartyDto | null): void {
    if (p) {
      void this.router.navigate(['/parties', p.id]);
      return;
    }
    this.editing.set(null);
    this.form = this.newForm(null);
    this.meta.sections('party').subscribe((s) => {
      this.sections.set(s);
      const group = buildCustomFieldsGroup(s, null, true);
      this.form.setControl('customFields', group);
      this.customGroup.set(group);
    });
    this.drawerOpen.set(true);
  }

  close(): void {
    this.drawerOpen.set(false);
    this.clearEditParam();
  }

  /** Drop `?edit=<id>` once the drawer closes, so a reload or Back does not reopen it. */
  private clearEditParam(): void {
    if (this.route.snapshot.queryParamMap.has('edit')) void this.router.navigate([], { relativeTo: this.route, queryParams: { edit: null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  err(name: string): string { return controlError(this.form.get(name)); }

  save(): void {
    if (this.form.invalid) { Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    // Never send the masked ID number / IBAN back; the API keeps the stored values for users without core.sensitive.read.
    if (this.sensitiveLocked()) { delete v.idNumber; delete v.iban; }
    const body = { ...v, currencyCode: v.currencyCode || null, paymentTermsDays: v.paymentTermsDays === '' || v.paymentTermsDays === null ? null : Number(v.paymentTermsDays), address: v.address || null, ...(this.sensitiveLocked() ? {} : { idNumber: v.idNumber || null, iban: v.iban || null }), roles: (v.roles as string[]).join(', '), customFields: toApiValues(this.sections(), (v.customFields ?? {}) as Record<string, unknown>) };
    const p = this.editing();
    this.saving.set(true);
    (p ? this.api.update(p.id, body) : this.api.create(body)).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success('Saved'); this.list.reload(); if (p) void this.router.navigate(['/parties', p.id]); else this.clearEditParam(); },
      error: (e) => {
        this.saving.set(false);
        const problem = asProblem(e);
        const rest = problem ? applyProblemDetailsToForm(this.form, problem) : [];
        this.message.error(rest[0] ?? problem?.detail ?? problem?.title ?? 'Could not save');
      },
    });
  }

  private newForm(p?: PartyDto | null): FormGroup {
    const locked = !!p && !this.session.hasPermission('core.sensitive.read');
    this.sensitiveLocked.set(locked);
    return this.fb.group({
      name: [p?.name ?? '', [Validators.required, Validators.maxLength(200)]],
      kind: [p?.kind?.toLowerCase() ?? 'company', Validators.required],
      roles: [p?.roles ?? ['Customer'], Validators.required],
      email: [p?.email ?? '', Validators.email],
      phone: [p?.phone ?? ''],
      taxNumber: [p?.taxNumber ?? ''],
      address: [p?.address ?? ''],
      currencyCode: [p?.currencyCode ?? '', Validators.maxLength(3)],
      paymentTermsDays: [p?.paymentTermsDays ?? null, Validators.min(0)],
      idNumber: [{ value: p?.idNumber ?? '', disabled: locked }],
      iban: [{ value: p?.iban ?? '', disabled: locked }],
      isActive: [p?.isActive ?? true],
      customFields: new FormGroup({}),
    });
  }
}
