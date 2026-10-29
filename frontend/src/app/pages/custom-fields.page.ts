import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { CustomFieldDefinitionsApi, CustomFieldMetadataService } from '../core/custom-fields/custom-field-metadata.service';
import { CUSTOM_FIELD_TYPES, CustomFieldDefinition, CustomFieldDefinitionInput } from '../core/custom-fields/custom-field.models';
import { applyProblemDetailsToForm, asProblem, controlError } from '../core/http/problem-details';
import { HasPermissionDirective } from '../shared/has-permission.directive';
import { RevealDirective } from '../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { TranslatePipe } from '../core/i18n/translate.pipe';

/** Custom fields admin (Odoo Studio style): pick an entity type, list its fields, add / edit / deactivate in a drawer. */
@Component({
  selector: 'erp-custom-fields',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzToolTipModule, RevealDirective, FormsModule, ReactiveFormsModule, HasPermissionDirective, NzTableModule, NzSelectModule, NzButtonModule, NzDrawerModule, NzFormModule,
    NzInputModule, NzInputNumberModule, NzSwitchModule, NzTagModule, NzIconModule],
  styles: [`.bar { display:flex; gap:12px; margin-bottom:12px; align-items:center } .spacer { flex:1 }`],
  template: `
    <h1 class="erp-page-title">Custom fields</h1>
    <div class="bar">
      <nz-select style="width:280px" [ngModel]="entityType()" (ngModelChange)="selectType($event)" nzPlaceHolder="Choose a record type" data-testid="cf-entity-type">
        @for (t of types(); track t.key) { <nz-option [nzValue]="t.key" [nzLabel]="t.label"></nz-option> }
      </nz-select>
      <span class="spacer"></span>
      <button nz-button nzType="default" type="button" (click)="load()" [disabled]="!entityType() || loading()" data-testid="cf-refresh"><span nz-icon nzType="reload"></span> {{ 'ui.common.refresh' | translate: 'Refresh' }}</button>
      <button *erpHasPermission="'core.customfields.manage'" nz-button nzType="primary" [disabled]="!entityType()" (click)="open(null)" data-testid="cf-new"><span nz-icon nzType="plus"></span> Add field</button>
    </div>
    <div class="erp-page-card" style="background:#fff; border:1px solid #eef2f6; border-radius:12px; overflow:hidden; box-shadow:0 1px 3px rgba(0,0,0,0.04);">
      <nz-table [nzData]="defs()" [nzFrontPagination]="false" [nzShowPagination]="false" [nzLoading]="loading()" nzSize="middle" data-testid="cf-table">
        <thead><tr><th>Label</th><th>Key</th><th>Type</th><th>Section</th><th>Required</th><th>Status</th></tr></thead>
        <tbody>
          @for (d of defs(); track d.id) {
            <tr (click)="open(d)" style="cursor:pointer" [attr.data-testid]="'cf-row-' + d.key">
              <td style="font-weight:600; color:#1e293b;">{{ d.label }}</td>
              <td><span class="code-pill">{{ d.key }}</span></td>
              <td><nz-tag nzColor="blue">{{ typeLabel(d.dataType) }}</nz-tag></td>
              <td style="color:#64748b;">{{ d.section || 'Additional information' }}</td>
              <td>
                @if (d.required) {
                  <nz-tag nzColor="orange">Required</nz-tag>
                } @else {
                  <span style="color:#94a3b8;">Optional</span>
                }
              </td>
              <td><nz-tag [nzColor]="d.isActive ? 'green' : 'default'">{{ d.isActive ? 'Active' : 'Inactive' }}</nz-tag></td>
            </tr>
          }
        </tbody>
      </nz-table>
    </div>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="560" [nzTitle]="editing() ? 'Edit field' : 'Add field'" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form">
          <nz-form-item><nz-form-label nzRequired>Label</nz-form-label><nz-form-control [nzErrorTip]="err('label')"><input nz-input formControlName="label" (input)="suggestKey()" data-testid="cf-label" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Key</nz-form-label><nz-form-control [nzErrorTip]="err('key') || 'cf_ followed by lowercase letters, digits or _'">
            <input nz-input formControlName="key" [readonly]="!!editing()" data-testid="cf-key" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Type</nz-form-label><nz-form-control>
            <nz-select formControlName="dataType" [nzDisabled]="!!editing()" data-testid="cf-type">@for (t of fieldTypes; track t.value) { <nz-option [nzValue]="t.value" [nzLabel]="t.label"></nz-option> }</nz-select>
          </nz-form-control></nz-form-item>
          @if (hasChoices()) {
            <nz-form-item><nz-form-label nzRequired>Choices (one per line)</nz-form-label><nz-form-control [nzErrorTip]="err('options')">
              <textarea nz-input rows="4" formControlName="choices" data-testid="cf-choices"></textarea></nz-form-control></nz-form-item>
          }
          <nz-form-item><nz-form-label>Section</nz-form-label><nz-form-control><input nz-input formControlName="section" placeholder="Additional information" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Help text</nz-form-label><nz-form-control><input nz-input formControlName="helpText" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Order</nz-form-label><nz-form-control><nz-input-number formControlName="order" [nzMin]="0"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Required</nz-form-label><nz-form-control><nz-switch formControlName="required"></nz-switch></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Show in lists</nz-form-label><nz-form-control><nz-switch formControlName="showInList"></nz-switch></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Searchable</nz-form-label><nz-form-control><nz-switch formControlName="isSearchable"></nz-switch></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Active</nz-form-label><nz-form-control><nz-switch formControlName="isActive"></nz-switch></nz-form-control></nz-form-item>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Cancel</button>
      <button *erpHasPermission="'core.customfields.manage'" nz-button nzType="primary" [nzLoading]="saving()" (click)="save()" style="margin-inline-start:8px" data-testid="cf-save">Save</button>
    </ng-template>`,
})
export class CustomFieldsPage {
  private readonly api = inject(CustomFieldDefinitionsApi);
  private readonly meta = inject(CustomFieldMetadataService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  readonly fieldTypes = CUSTOM_FIELD_TYPES;
  readonly types = toSignal(this.api.entityTypes().pipe(catchError(() => of([]))), { initialValue: [] });
  readonly entityType = signal<string | null>(null);
  readonly defs = signal<CustomFieldDefinition[]>([]);
  readonly loading = signal(false);
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly editing = signal<CustomFieldDefinition | null>(null);
  readonly form = this.fb.nonNullable.group({
    label: ['', [Validators.required, Validators.maxLength(100)]],
    key: ['', [Validators.required, Validators.pattern(/^cf_[a-z0-9_]{1,60}$/)]],
    dataType: ['text' as CustomFieldDefinition['dataType'], Validators.required],
    choices: [''],
    section: [''],
    helpText: [''],
    order: [0],
    required: [false],
    showInList: [false],
    isSearchable: [false],
    isActive: [true],
  });
  private readonly dataType = toSignal(this.form.controls.dataType.valueChanges, { initialValue: 'text' as CustomFieldDefinition['dataType'] });
  readonly hasChoices = computed(() => this.dataType() === 'select' || this.dataType() === 'multiSelect');

  typeLabel(t: string): string { return this.fieldTypes.find((x) => x.value === t)?.label ?? t; }
  err(name: string): string { return controlError(this.form.get(name)); }

  selectType(t: string): void {
    this.entityType.set(t);
    this.load();
  }

  load(): void {
    const t = this.entityType();
    if (!t) return;
    this.loading.set(true);
    this.api.list(t).subscribe({ next: (d) => { this.defs.set(d); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  open(d: CustomFieldDefinition | null): void {
    this.editing.set(d);
    this.form.reset({
      label: d?.label ?? '', key: d?.key ?? '', dataType: d?.dataType ?? 'text',
      choices: (d?.options?.choices ?? []).map((c) => c.value).join('\n'), section: d?.section ?? '', helpText: d?.helpText ?? '',
      order: d?.order ?? this.defs().length * 10, required: d?.required ?? false, showInList: d?.showInList ?? false,
      isSearchable: d?.isSearchable ?? false, isActive: d?.isActive ?? true,
    });
    this.drawerOpen.set(true);
  }

  suggestKey(): void {
    if (this.editing() || this.form.controls.key.dirty) return;
    const key = 'cf_' + this.form.controls.label.value.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '').slice(0, 60);
    this.form.controls.key.setValue(key);
  }

  save(): void {
    if (this.form.invalid) { Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const choices = v.choices.split('\n').map((s) => s.trim()).filter(Boolean).map((value) => ({ value, label: value }));
    const body: CustomFieldDefinitionInput = {
      entityType: this.entityType()!, key: v.key, label: v.label, dataType: v.dataType, required: v.required,
      options: this.hasChoices() ? { choices } : null, order: v.order, section: v.section || null, helpText: v.helpText || null,
      isSearchable: v.isSearchable, isActive: v.isActive, showInList: v.showInList, sortable: false, defaultValue: null, legalEntityId: null,
    };
    const d = this.editing();
    this.saving.set(true);
    (d ? this.api.update(d.id, body) : this.api.create(body)).subscribe({
      next: () => {
        this.saving.set(false); this.drawerOpen.set(false); this.message.success('Field saved');
        this.meta.invalidate(body.entityType); this.load();
      },
      error: (e) => {
        this.saving.set(false);
        const p = asProblem(e);
        const rest = p ? applyProblemDetailsToForm(this.form, p) : [];
        this.message.error(rest[0] ?? p?.detail ?? p?.title ?? 'Could not save the field');
      },
    });
  }
}
