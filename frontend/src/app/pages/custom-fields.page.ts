import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { CUSTOM_FIELD_TYPES, CustomFieldDefinition, CustomFieldDefinitionInput, CustomFieldDataType, CustomizableEntityType } from '../core/custom-fields/custom-field.models';
import { CustomFieldDefinitionsApi, CustomFieldMetadataService } from '../core/custom-fields/custom-field-metadata.service';
import { applyProblemDetailsToForm, asProblem, controlError } from '../core/http/problem-details';
import { HasPermissionDirective } from '../shared/has-permission.directive';

/** Field-definition admin: add fields to any customizable entity without code changes. */
@Component({
  selector: 'erp-custom-fields',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ReactiveFormsModule, NzTableModule, NzButtonModule, NzSelectModule, NzDrawerModule, NzFormModule, NzInputModule,
    NzInputNumberModule, NzSwitchModule, NzTagModule, NzPopconfirmModule, HasPermissionDirective],
  styles: [`.bar { display: flex; gap: 12px; margin-bottom: 16px; align-items: center; } .spacer { flex: 1; }`],
  template: `
    <h1>Custom fields</h1>
    <div class="bar">
      <nz-select style="width: 280px" [ngModel]="entityType()" (ngModelChange)="selectType($event)" nzPlaceHolder="Entity" data-testid="cf-entity-type">
        @for (t of types(); track t.key) { <nz-option [nzValue]="t.key" [nzLabel]="t.label + ' (' + t.module + ')'"></nz-option> }
      </nz-select>
      <span class="spacer"></span>
      <button *erpHasPermission="'core.customfields.manage'" nz-button nzType="primary" [disabled]="!entityType()" (click)="openCreate()" data-testid="cf-new">New field</button>
    </div>

    <nz-table [nzData]="defs()" [nzFrontPagination]="false" data-testid="cf-table">
      <thead><tr><th>Order</th><th>Label</th><th>Key</th><th>Type</th><th>Required</th><th>Status</th><th></th></tr></thead>
      <tbody>
        @for (d of defs(); track d.id) {
          <tr [attr.data-testid]="'cf-row-' + d.key">
            <td>{{ d.order }}</td><td>{{ d.label }}</td><td><code>{{ d.key }}</code></td><td>{{ d.dataType }}</td>
            <td>{{ d.required ? 'Yes' : 'No' }}</td>
            <td><nz-tag [nzColor]="d.isActive ? 'green' : 'default'">{{ d.isActive ? 'Active' : 'Inactive' }}</nz-tag></td>
            <td *erpHasPermission="'core.customfields.manage'">
              <a (click)="openEdit(d)">Edit</a>
              @if (d.isActive) { · <a nz-popconfirm nzPopconfirmTitle="Deactivate this field? Stored values are kept." (nzOnConfirm)="deactivate(d)">Deactivate</a> }
            </td>
          </tr>
        }
      </tbody>
    </nz-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzTitle]="editingId() ? 'Edit field' : 'New field'" nzWidth="480" (nzOnClose)="drawerOpen.set(false)">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" (ngSubmit)="save()" data-testid="cf-form">
          <nz-form-item><nz-form-label nzRequired>Label</nz-form-label>
            <nz-form-control [nzErrorTip]="err('label')"><input nz-input formControlName="label" data-testid="cf-label" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Key</nz-form-label>
            <nz-form-control [nzErrorTip]="err('key')" nzExtra="Starts with cf_, lower-case letters, digits and underscores. Cannot change later.">
              <input nz-input formControlName="key" data-testid="cf-key" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Type</nz-form-label>
            <nz-form-control><nz-select formControlName="dataType" data-testid="cf-type">
              @for (t of fieldTypes; track t.value) { <nz-option [nzValue]="t.value" [nzLabel]="t.label"></nz-option> }
            </nz-select></nz-form-control></nz-form-item>
          @if (hasChoices()) {
            <nz-form-item><nz-form-label>Choices</nz-form-label>
              <nz-form-control nzExtra="One choice per line."><textarea nz-input rows="4" formControlName="choices" data-testid="cf-choices"></textarea></nz-form-control></nz-form-item>
          }
          <nz-form-item><nz-form-label>Section</nz-form-label><nz-form-control><input nz-input formControlName="section" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Help text</nz-form-label><nz-form-control><input nz-input formControlName="helpText" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Order</nz-form-label>
            <nz-form-control><nz-input-number formControlName="order" [nzPrecision]="0" style="width:100%"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Required</nz-form-label><nz-form-control><nz-switch formControlName="required"></nz-switch></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Searchable</nz-form-label><nz-form-control><nz-switch formControlName="isSearchable"></nz-switch></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Show in list</nz-form-label><nz-form-control><nz-switch formControlName="showInList"></nz-switch></nz-form-control></nz-form-item>
          <button nz-button nzType="primary" nzBlock [nzLoading]="saving()" data-testid="cf-save">Save</button>
        </form>
      </ng-container>
    </nz-drawer>`,
})
export class CustomFieldsPage {
  private readonly api = inject(CustomFieldDefinitionsApi);
  private readonly metadata = inject(CustomFieldMetadataService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly fieldTypes = CUSTOM_FIELD_TYPES;
  readonly types = signal<CustomizableEntityType[]>([]);
  readonly entityType = signal<string | null>(null);
  readonly defs = signal<CustomFieldDefinition[]>([]);
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly editingId = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required, Validators.maxLength(100)]],
    key: ['', [Validators.required, Validators.pattern(/^cf_[a-z0-9_]{1,60}$/)]],
    dataType: ['text' as CustomFieldDataType],
    choices: [''],
    section: [''],
    helpText: [''],
    order: [0],
    required: [false],
    isSearchable: [false],
    showInList: [false],
  });

  constructor() {
    this.api.entityTypes().subscribe((t) => {
      this.types.set(t);
      if (t.length) this.selectType(t[0].key);
    });
  }

  err(name: string): string { return controlError(this.form.get(name)); }
  hasChoices(): boolean { return ['select', 'multiSelect'].includes(this.form.controls.dataType.value); }

  selectType(key: string): void {
    this.entityType.set(key);
    this.load();
  }

  openCreate(): void {
    this.editingId.set(null);
    this.form.reset({ dataType: 'text', order: (this.defs().at(-1)?.order ?? 0) + 10 });
    this.form.controls.key.enable();
    this.drawerOpen.set(true);
  }

  openEdit(d: CustomFieldDefinition): void {
    this.editingId.set(d.id);
    this.form.reset({
      label: d.label, key: d.key, dataType: d.dataType, section: d.section ?? '', helpText: d.helpText ?? '', order: d.order,
      choices: (d.options?.choices ?? []).map((c) => c.value).join('\n'),
      required: d.required, isSearchable: d.isSearchable, showInList: d.showInList,
    });
    this.form.controls.key.disable(); // keys are immutable: stored values are keyed by them
    this.drawerOpen.set(true);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    const choices = v.choices.split('\n').map((c) => c.trim()).filter(Boolean).map((value) => ({ value, label: value }));
    const existing = this.defs().find((d) => d.id === this.editingId());
    const body: CustomFieldDefinitionInput = {
      entityType: this.entityType()!, key: v.key, label: v.label, dataType: v.dataType, required: v.required,
      options: this.hasChoices() ? { ...(existing?.options ?? {}), choices } : (existing?.options ?? null),
      order: v.order, section: v.section || null, helpText: v.helpText || null, isSearchable: v.isSearchable,
      isActive: existing?.isActive ?? true, defaultValue: existing?.defaultValue, legalEntityId: existing?.legalEntityId ?? null,
      showInList: v.showInList, sortable: existing?.sortable ?? false,
    };
    this.saving.set(true);
    const id = this.editingId();
    (id ? this.api.update(id, body) : this.api.create(body)).subscribe({
      next: () => { this.saving.set(false); this.drawerOpen.set(false); this.message.success('Field saved'); this.metadata.invalidate(this.entityType()!); this.load(); },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const unmatched = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(unmatched[0] ?? p?.detail ?? p?.title ?? 'Could not save the field');
      },
    });
  }

  deactivate(d: CustomFieldDefinition): void {
    this.api.deactivate(d.id).subscribe({
      next: () => { this.message.success('Field deactivated'); this.metadata.invalidate(d.entityType); this.load(); },
      error: (e) => this.message.error(asProblem(e)?.title ?? 'Could not deactivate the field'),
    });
  }

  private load(): void {
    const type = this.entityType();
    if (type) this.api.list(type).subscribe((d) => this.defs.set(d));
  }
}
