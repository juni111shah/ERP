import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, Subscription } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { ProblemDetails } from '../core/api/api.models';
import { buildCustomFieldsGroup, toApiValues, toIsoDate } from '../core/custom-fields/custom-field-form';
import { CustomFieldMetadataService } from '../core/custom-fields/custom-field-metadata.service';
import { FormSectionMeta } from '../core/custom-fields/custom-field.models';
import { DynamicFieldsComponent } from '../core/custom-fields/dynamic-field.component';
import { LookupOption } from '../core/custom-fields/lookup-registry';
import { applyProblemDetailsToForm, controlError } from '../core/http/problem-details';

export type FieldType = 'text' | 'textarea' | 'number' | 'date' | 'select' | 'switch' | 'lookup';
export interface SelectOption { value: string | number | boolean; label: string; }

/** Declarative description of one form field. The drawer renders, validates and serialises it. */
export interface FieldSpec {
  key: string;
  label: string;
  type: FieldType;
  required?: boolean;
  options?: SelectOption[];
  /** server-side search for `lookup` fields (returns id + label) */
  lookup?: (term: string) => Observable<LookupOption[]>;
  /** label of the currently selected lookup value when editing (the search has not run yet) */
  currentLabel?: (record: Record<string, unknown>) => string | undefined;
  min?: number;
  max?: number;
  maxLength?: number;
  precision?: number;
  hint?: string;
  default?: unknown;
  /** cannot be changed once the record exists */
  readonlyOnEdit?: boolean;
  /** hide the field depending on the other values */
  hidden?: (value: Record<string, unknown>) => boolean;
}

/**
 * Reusable master-data side drawer: give it field specs and an optional record, it builds the form, shows server validation
 * errors on the right fields and appends the tenant's custom fields for `customFieldEntityType`. The parent does the API call.
 *   <erp-record-drawer title="New unit" [open]="open()" [fields]="fields" [value]="editing()" customFieldEntityType="realestate.unit"
 *                      [saving]="saving()" [errors]="errors()" (submitted)="save($event)" (closed)="open.set(false)" />
 */
@Component({
  selector: 'erp-record-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzDrawerModule, NzFormModule, NzInputModule, NzInputNumberModule, NzDatePickerModule, NzSelectModule, NzSwitchModule, NzButtonModule, DynamicFieldsComponent],
  template: `
    <nz-drawer [nzVisible]="open()" [nzWidth]="width()" [nzTitle]="title()" (nzOnClose)="closed.emit()" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <ng-content select="[drawerActions]" />
        <form nz-form nzLayout="vertical" [formGroup]="form()" (ngSubmit)="submit()" [attr.data-testid]="testId()">
          @for (f of visibleFields(); track f.key) {
            <nz-form-item>
              <nz-form-label [nzRequired]="!!f.required" [nzFor]="f.key">{{ f.label }}</nz-form-label>
              <nz-form-control [nzErrorTip]="err(f.key)" [nzExtra]="f.hint ?? ''">
                @switch (f.type) {
                  @case ('text') { <input nz-input [id]="f.key" [formControlName]="f.key" [attr.maxlength]="f.maxLength ?? null" [attr.data-testid]="'f-' + f.key" /> }
                  @case ('textarea') { <textarea nz-input rows="3" [id]="f.key" [formControlName]="f.key" [attr.maxlength]="f.maxLength ?? null" [attr.data-testid]="'f-' + f.key"></textarea> }
                  @case ('number') { <nz-input-number [nzId]="f.key" [formControlName]="f.key" [nzMin]="f.min ?? -1e12" [nzMax]="f.max ?? 1e12" [nzPrecision]="f.precision ?? 2" style="width:100%" [attr.data-testid]="'f-' + f.key"></nz-input-number> }
                  @case ('date') { <nz-date-picker [nzId]="f.key" [formControlName]="f.key" style="width:100%" [attr.data-testid]="'f-' + f.key"></nz-date-picker> }
                  @case ('switch') { <nz-switch [nzId]="f.key" [formControlName]="f.key" [attr.data-testid]="'f-' + f.key"></nz-switch> }
                  @case ('select') {
                    <nz-select [nzId]="f.key" [formControlName]="f.key" nzAllowClear [attr.data-testid]="'f-' + f.key">
                      @for (o of f.options ?? []; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }
                    </nz-select>
                  }
                  @case ('lookup') {
                    <nz-select [nzId]="f.key" [formControlName]="f.key" nzShowSearch nzServerSearch nzAllowClear (nzOnSearch)="search(f, $event)" [attr.data-testid]="'f-' + f.key">
                      @for (o of lookupOptions()[f.key]; track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }
                    </nz-select>
                  }
                }
              </nz-form-control>
            </nz-form-item>
          }
          @if (sections().length) { <erp-dynamic-fields [sections]="sections()" [group]="customGroup()" /> }
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button type="button" (click)="closed.emit()">Cancel</button>
      <button nz-button nzType="primary" type="button" [nzLoading]="saving()" (click)="submit()" style="margin-inline-start:8px" [attr.data-testid]="testId() + '-save'">{{ submitLabel() }}</button>
    </ng-template>`,
})
export class RecordDrawerComponent {
  readonly title = input.required<string>();
  readonly open = input.required<boolean>();
  readonly fields = input.required<FieldSpec[]>();
  /** the record being edited; null/undefined = create */
  readonly value = input<Record<string, unknown> | null | undefined>(null);
  readonly customFieldEntityType = input<string>();
  readonly saving = input(false);
  readonly errors = input<ProblemDetails | null>(null);
  readonly submitLabel = input('Save');
  readonly width = input(560);
  readonly testId = input('record-form');
  readonly submitted = output<Record<string, unknown>>();
  readonly closed = output<void>();

  private readonly meta = inject(CustomFieldMetadataService);
  /**
   * Built synchronously from the inputs (a fresh group every time the drawer opens or its record/fields change), so the template
   * never renders controls that do not exist yet. Pass stable `fields` (a constant or a computed), not a new array per change detection.
   */
  readonly form = computed(() => this.createForm(this.fields(), this.open() ? (this.value() ?? null) : null));
  readonly sections = signal<FormSectionMeta[]>([]);
  readonly customGroup = signal<FormGroup>(new FormGroup({}));
  readonly lookupOptions = signal<Record<string, LookupOption[]>>({});
  private readonly changes = signal(0);
  readonly visibleFields = computed(() => {
    this.changes(); // re-evaluate when any value changes (fields can hide depending on other values)
    const value = this.form().getRawValue();
    return this.fields().filter((f) => !f.hidden?.(value));
  });
  private valueSub?: Subscription;

  constructor() {
    effect(() => {
      const form = this.form();
      if (!this.open()) return;
      const fields = this.fields();
      const record = this.value() ?? null;
      const type = this.customFieldEntityType();
      untracked(() => this.initialise(form, fields, record, type));
    });
    effect(() => {
      const problem = this.errors();
      if (problem) untracked(() => applyProblemDetailsToForm(this.form(), problem));
    });
  }

  err(key: string): string { return controlError(this.form().get(key)); }

  search(field: FieldSpec, term: string): void {
    field.lookup?.(term).subscribe((options) => this.lookupOptions.update((all) => ({ ...all, [field.key]: options })));
  }

  submit(): void {
    const form = this.form();
    const visible = this.visibleFields();
    // Hidden fields are not part of the form the user sees, so they must not block saving.
    const invalid = visible.filter((f) => form.get(f.key)?.invalid);
    if (invalid.length > 0 || this.customGroup().invalid) {
      for (const f of invalid) { const c = form.get(f.key)!; c.markAsDirty(); c.markAsTouched(); c.updateValueAndValidity(); }
      this.customGroup().markAllAsTouched();
      return;
    }
    const raw = form.getRawValue() as Record<string, unknown>;
    const payload: Record<string, unknown> = {};
    for (const f of visible) payload[f.key] = this.serialise(f, raw[f.key]);
    if (this.sections().length) payload['customFields'] = toApiValues(this.sections(), this.customGroup().getRawValue());
    this.submitted.emit(payload);
  }

  private createForm(fields: FieldSpec[], record: Record<string, unknown> | null): FormGroup {
    const controls: Record<string, FormControl> = {};
    for (const f of fields) {
      const initial = record ? (record[f.key] ?? null) : (f.default ?? (f.type === 'switch' ? false : null));
      const validators = [];
      if (f.required && f.type !== 'switch') validators.push(Validators.required);
      if (f.type === 'text' || f.type === 'textarea') validators.push(Validators.maxLength(f.maxLength ?? 2000));
      controls[f.key] = new FormControl({ value: f.type === 'date' ? this.toDate(initial) : initial, disabled: !!record && !!f.readonlyOnEdit }, validators);
    }
    return new FormGroup(controls);
  }

  /** Side effects after the form exists: change tracking, lookup suggestions and the tenant's custom fields. */
  private initialise(form: FormGroup, fields: FieldSpec[], record: Record<string, unknown> | null, entityType: string | undefined): void {
    this.valueSub?.unsubscribe();
    this.valueSub = form.valueChanges.subscribe(() => this.changes.update((n) => n + 1));
    this.changes.update((n) => n + 1);

    const options: Record<string, LookupOption[]> = {};
    for (const f of fields) {
      if (f.type !== 'lookup') continue;
      const current = record?.[f.key];
      if (record && current != null) options[f.key] = [{ value: String(current), label: f.currentLabel?.(record) ?? String(current) }];
    }
    this.lookupOptions.set(options);
    for (const f of fields) if (f.type === 'lookup' && !record) this.search(f, ''); // initial suggestions

    this.sections.set([]);
    this.customGroup.set(new FormGroup({}));
    if (entityType) {
      this.meta.sections(entityType).subscribe((sections) => {
        this.sections.set(sections);
        this.customGroup.set(buildCustomFieldsGroup(sections, (record?.['customFields'] ?? null) as Record<string, unknown> | null, !record));
      });
    }
  }

  private serialise(f: FieldSpec, v: unknown): unknown {
    if (v === '' || v === undefined) return null;
    if (f.type === 'date') return v instanceof Date ? toIsoDate(v) : v;
    if (f.type === 'text' || f.type === 'textarea') return typeof v === 'string' ? v.trim() || null : v;
    return v;
  }

  private toDate(v: unknown): Date | null {
    if (!v) return null;
    const [y, m, d] = String(v).slice(0, 10).split('-').map(Number);
    return new Date(y, m - 1, d); // local midnight: no timezone day shift
  }
}
