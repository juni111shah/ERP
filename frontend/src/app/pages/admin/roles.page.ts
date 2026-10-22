import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PermissionDefinition, RoleDto, RolesApi, yesNo } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { applyProblemDetailsToForm, asProblem, controlError } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

/** Roles with a permission checklist grouped by module and resource. Built-in roles are read-only. */
@Component({
  selector: 'erp-roles',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDrawerModule, NzFormModule, NzInputModule, NzCheckboxModule, NzIconModule],
  styles: [`.perm-group { margin-bottom: 16px } .perm-group h4 { margin: 0 0 4px; text-transform: capitalize } .perm { display:block; margin: 2px 0 }`],
  template: `
    <h1 class="erp-page-title">Roles</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search roles" emptyText="No roles" (rowClick)="open($event)">
      <button toolbar *erpHasPermission="'core.roles.write'" nz-button nzType="primary" (click)="open(null)" data-testid="role-new"><span nz-icon nzType="plus"></span>New role</button>
    </erp-paged-table>
    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="560" [nzTitle]="editing() ? editing()!.name : 'New role'" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="role-form">
          <nz-form-item><nz-form-label nzRequired>Name</nz-form-label><nz-form-control [nzErrorTip]="err('name')"><input nz-input formControlName="name" data-testid="role-name" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Description</nz-form-label><nz-form-control><input nz-input formControlName="description" /></nz-form-control></nz-form-item>
        </form>
        <h4>Permissions</h4>
        @for (g of groups(); track g.title) {
          <div class="perm-group">
            <h4>{{ g.title }}</h4>
            @for (p of g.items; track p.key) {
              <label nz-checkbox class="perm" [nzChecked]="selected().has(p.key)" [nzDisabled]="readOnly()" (nzCheckedChange)="toggle(p.key, $event)" [attr.data-testid]="'perm-' + p.key">{{ p.description }}</label>
            }
          </div>
        }
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Cancel</button>
      @if (!readOnly()) { <button nz-button nzType="primary" [nzLoading]="saving()" (click)="save()" style="margin-inline-start:8px" data-testid="role-save">Save</button> }
    </ng-template>`,
})
export class RolesPage {
  private readonly api = inject(RolesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'name' });
  readonly columns: TableColumn<RoleDto>[] = [
    { key: 'name', label: 'Name', sortField: 'name' },
    { key: 'description', label: 'Description' },
    { key: 'isBuiltIn', label: 'Built-in', value: (r) => yesNo(r.isBuiltIn) },
    { key: 'permissions', label: 'Permissions', value: (r) => (r.permissions.includes('*') ? 'All' : r.permissions.length) },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly editing = signal<RoleDto | null>(null);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly permissions = signal<PermissionDefinition[]>([]);
  readonly readOnly = computed(() => !!this.editing()?.isBuiltIn);
  readonly groups = computed(() => {
    const map = new Map<string, PermissionDefinition[]>();
    for (const p of this.permissions()) map.set(`${p.module} · ${p.resource}`, [...(map.get(`${p.module} · ${p.resource}`) ?? []), p]);
    return [...map].map(([title, items]) => ({ title, items }));
  });
  readonly form = this.fb.nonNullable.group({ name: ['', [Validators.required, Validators.maxLength(100)]], description: [''] });

  constructor() { this.api.permissions().subscribe((p) => this.permissions.set(p)); }

  err(name: string): string { return controlError(this.form.get(name)); }

  open(role: RoleDto | null): void {
    this.editing.set(role);
    this.form.reset({ name: role?.name ?? '', description: role?.description ?? '' });
    this.readOnly() ? this.form.disable() : this.form.enable();
    this.selected.set(new Set(role?.permissions ?? []));
    this.drawerOpen.set(true);
  }

  toggle(key: string, on: boolean): void {
    this.selected.update((s) => { const next = new Set(s); if (on) next.add(key); else next.delete(key); return next; });
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const body = { name: v.name, description: v.description || null, permissions: [...this.selected()] };
    const role = this.editing();
    this.saving.set(true);
    (role ? this.api.update(role.id, body) : this.api.create(body)).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success('Role saved'); this.list.reload(); },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(rest[0] ?? p?.detail ?? p?.title ?? 'Could not save the role');
      },
    });
  }
}
