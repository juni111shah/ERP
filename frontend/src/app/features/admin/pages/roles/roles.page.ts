import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { PermissionDefinition, RoleDto, RolesApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { doneText } from '@core/format/done-text';
import { controlError } from '@core/http/problem-details';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { showFormSaveError } from '@shared/forms/save-errors';
import { groupPermissions } from '../../admin-access';

/** Roles with a permission checklist grouped by module and resource. Built-in roles are read-only. */
@Component({
  selector: 'erp-roles',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, ReactiveFormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDrawerModule, NzFormModule, NzInputModule, NzCheckboxModule, NzIconModule],
  styleUrl: './roles.page.scss',
  templateUrl: './roles.page.html',
})
export class RolesPage {
  private readonly api = inject(RolesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'name' });
  readonly columns: TableColumn<RoleDto>[] = [
    { key: 'name', label: 'Role name', sortField: 'name' },
    { key: 'description', label: 'Description' },
    { key: 'isBuiltIn', label: 'Type', value: (r) => (r.isBuiltIn ? 'System' : 'Custom') },
    { key: 'permissions', label: 'Permissions', value: (r) => (r.permissions.includes('*') ? 'All access (*)' : `${r.permissions.length} granted`) },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly editing = signal<RoleDto | null>(null);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly permissions = signal<PermissionDefinition[]>([]);
  readonly readOnly = computed(() => !!this.editing()?.isBuiltIn);
  readonly groups = computed(() => groupPermissions(this.permissions()));
  readonly form = this.fb.nonNullable.group({ name: ['', [Validators.required, Validators.maxLength(100)]], description: [''] });

  constructor() { this.api.permissions().subscribe((p) => this.permissions.set(p)); }

  err(name: string): string { return controlError(this.form.get(name)); }

  open(role: RoleDto | null): void {
    this.editing.set(role);
    this.form.reset({ name: role?.name ?? '', description: role?.description ?? '' });
    if (this.readOnly()) this.form.disable();
    else this.form.enable();
    this.selected.set(new Set(role?.permissions ?? []));
    this.drawerOpen.set(true);
  }

  toggle(key: string, on: boolean): void {
    this.selected.update((s) => { const next = new Set(s); if (on) next.add(key); else next.delete(key); return next; });
  }

  save(): void {
    if (this.form.invalid) { revealErrors(this.form, { touchAll: true }); return; }
    const v = this.form.getRawValue();
    const body = { name: v.name, description: v.description || null, permissions: [...this.selected()] };
    const role = this.editing();
    this.saving.set(true);
    (role ? this.api.update(role.id, body) : this.api.create(body)).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success(doneText('saved', 'Role', v.name)); this.list.reload(); },
      error: (e) => {
        this.saving.set(false);
        showFormSaveError(this.form, e, this.message, 'Could not save the role');
      },
    });
  }
}
