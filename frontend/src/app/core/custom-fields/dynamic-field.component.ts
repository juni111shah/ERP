import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { controlError } from '../http/problem-details';
import { FormFieldMeta, FormSectionMeta } from './custom-field.models';
import { LookupOption, LookupRegistry } from './lookup-registry';

/** Renders one custom field from its metadata (Odoo Studio pattern: the UI is driven by the field registry). */
@Component({
  selector: 'erp-dynamic-field',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzFormModule, NzInputModule, NzInputNumberModule, NzDatePickerModule, NzSelectModule, NzSwitchModule, NzCheckboxModule],
  template: `
    <nz-form-item class="erp-field" data-origin="custom" [formGroup]="group()" [attr.data-testid]="'cf-' + field().key">
      <nz-form-label [nzRequired]="field().required" [nzFor]="field().key">{{ field().label }}</nz-form-label>
      <nz-form-control [nzErrorTip]="error()" [nzExtra]="field().helpText ?? ''">
        @switch (field().dataType) {
          @case ('text') { <input nz-input [id]="field().key" [formControlName]="field().key" [attr.maxlength]="field().maxLength ?? 4000" /> }
          @case ('number') { <nz-input-number [nzId]="field().key" [formControlName]="field().key" [nzPrecision]="0" [nzMin]="field().min ?? -9007199254740991" [nzMax]="field().max ?? 9007199254740991" style="width:100%"></nz-input-number> }
          @case ('decimal') { <nz-input-number [nzId]="field().key" [formControlName]="field().key" [nzPrecision]="field().scale ?? 2" [nzMin]="field().min ?? -9007199254740991" [nzMax]="field().max ?? 9007199254740991" style="width:100%"></nz-input-number> }
          @case ('date') { <nz-date-picker [nzId]="field().key" [formControlName]="field().key" style="width:100%"></nz-date-picker> }
          @case ('dateTime') { <nz-date-picker [nzId]="field().key" [formControlName]="field().key" nzShowTime style="width:100%"></nz-date-picker> }
          @case ('bool') { <nz-switch [nzId]="field().key" [formControlName]="field().key"></nz-switch> }
          @case ('select') {
            <nz-select [nzId]="field().key" [formControlName]="field().key" nzAllowClear nzShowSearch>
              @for (o of field().options; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label ?? o.value"></nz-option> }
            </nz-select>
          }
          @case ('multiSelect') {
            <nz-select [nzId]="field().key" [formControlName]="field().key" nzMode="multiple" nzAllowClear>
              @for (o of field().options; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label ?? o.value"></nz-option> }
            </nz-select>
          }
          @case ('lookup') {
            <nz-select [nzId]="field().key" [formControlName]="field().key" nzShowSearch nzServerSearch nzAllowClear (nzOnSearch)="search($event)">
              @for (o of lookupOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }
            </nz-select>
          }
        }
      </nz-form-control>
    </nz-form-item>`,
})
export class DynamicFieldComponent {
  readonly field = input.required<FormFieldMeta>();
  readonly group = input.required<FormGroup>();
  readonly lookupOptions = signal<LookupOption[]>([]);
  private readonly lookups = inject(LookupRegistry);

  error(): string {
    return controlError(this.group().get(this.field().key));
  }

  search(term: string): void {
    this.lookups.search(this.field().lookupEntityType, term).subscribe((o) => this.lookupOptions.set(o));
  }
}

/** Renders all custom-field sections of a form; bind [group] to the nested `customFields` FormGroup. */
@Component({
  selector: 'erp-dynamic-fields',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DynamicFieldComponent],
  template: `
    @for (s of sections(); track s.key) {
      <section class="erp-form-section" [attr.data-testid]="'cf-section-' + s.key">
        <h3 class="erp-form-section__title">{{ s.label }}</h3>
        <div class="erp-field-grid">
          @for (f of s.fields; track f.key) { <erp-dynamic-field [field]="f" [group]="group()" /> }
        </div>
      </section>
    }`,
})
export class DynamicFieldsComponent {
  readonly sections = input.required<FormSectionMeta[]>();
  readonly group = input.required<FormGroup>();
}

/** Formats a custom-field value for list cells. */
export function formatCustomValue(field: FormFieldMeta, value: unknown): string {
  if (value == null || value === '') return '';
  if (field.dataType === 'bool') return value ? 'Yes' : 'No';
  const label = (v: unknown) => field.options.find((o) => o.value === v)?.label ?? String(v);
  if (field.dataType === 'multiSelect' && Array.isArray(value)) return value.map(label).join(', ');
  if (field.dataType === 'select') return label(value);
  return String(value);
}
