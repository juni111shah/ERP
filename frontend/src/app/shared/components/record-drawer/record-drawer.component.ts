import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { Subscription } from 'rxjs';
import { ProblemDetails } from '@core/api/api.models';
import { buildCustomFieldsGroup, toApiValues } from '@core/custom-fields/custom-field-form';
import { CustomFieldMetadataService } from '@core/custom-fields/custom-field-metadata.service';
import { FormSectionMeta } from '@core/custom-fields/custom-field.models';
import { DynamicFieldsComponent } from '@core/custom-fields/dynamic-field.component';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { applyProblemDetailsToForm, controlError } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { FieldSpec, RowColumn, SelectOption, cellOptions, createRecordForm, groupOptions, serialiseField } from './record-form';

export type { FieldSpec, FieldType, RowColumn, SelectOption } from './record-form';

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
  imports: [TranslatePipe, ReactiveFormsModule, NzAlertModule, NzDrawerModule, NzFormModule, NzInputModule, NzInputNumberModule, NzDatePickerModule, NzSelectModule, NzSwitchModule, NzButtonModule, DynamicFieldsComponent],
  templateUrl: './record-drawer.component.html',
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
  readonly form = computed(() => createRecordForm(this.fields(), this.open() ? (this.value() ?? null) : null));
  readonly sections = signal<FormSectionMeta[]>([]);
  readonly customGroup = signal<FormGroup>(new FormGroup({}));
  readonly lookupOptions = signal<Record<string, LookupOption[]>>({});
  /** Server messages that belong to no field of this form. */
  readonly formErrors = signal<string[]>([]);
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
      this.formErrors.set(problem ? untracked(() => applyProblemDetailsToForm(this.form(), problem)) : []);
    });
  }

  // ---------- `rows` fields ----------
  rowsOf(key: string): Record<string, unknown>[] { const v = this.form().get(key)?.value; return Array.isArray(v) ? v : []; }
  addRow(key: string): void {
    const c = this.form().get(key);
    const defaults = this.fields().find((f) => f.key === key)?.rowDefaults ?? {};
    c?.setValue([...this.rowsOf(key), { ...defaults }]);
    c?.markAsDirty();
  }
  removeRow(key: string, index: number): void { const c = this.form().get(key); c?.setValue(this.rowsOf(key).filter((_, i) => i !== index)); c?.markAsDirty(); }
  setCell(key: string, index: number, col: RowColumn, text: string): void {
    const c = this.form().get(key);
    const rows = this.rowsOf(key).map((r, i) => (i === index ? { ...r, [col.key]: col.type === 'number' ? (text === '' ? null : Number(text)) : text } : r));
    c?.setValue(rows, { emitEvent: false }); // typing must not rebuild the inputs (the cursor would jump)
    c?.markAsDirty();
  }
  /** The options of a select cell, plus the value the line already holds when it is not among them. */
  selectOptions(col: RowColumn, current: unknown): SelectOption[] { return cellOptions(col, current); }

  // ---------- selects and lookups ----------
  hasGroups(f: FieldSpec): boolean { return (f.options ?? []).some((o) => !!o.group); }
  /** The options under their headings, in the order they were given; options without a heading come under "Other". */
  groupsOf(f: FieldSpec): { label: string; options: SelectOption[] }[] { return groupOptions(f); }

  search(field: FieldSpec, term: string): void {
    field.lookup?.(term).subscribe((options) => this.lookupOptions.update((all) => ({ ...all, [field.key]: options })));
  }

  err(key: string): string { return controlError(this.form().get(key)); }

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
    for (const f of visible) payload[f.key] = serialiseField(f, raw[f.key]);
    if (this.sections().length) payload['customFields'] = toApiValues(this.sections(), this.customGroup().getRawValue());
    this.submitted.emit(payload);
  }

  /** Side effects after the form exists: change tracking, lookup suggestions and the tenant's custom fields. */
  private initialise(form: FormGroup, fields: FieldSpec[], record: Record<string, unknown> | null, entityType: string | undefined): void {
    this.trackChanges(form, fields);
    this.loadLookups(fields, record);
    this.loadCustomFields(record, entityType);
  }

  /** Counts every change (fields can hide on other values) and runs each field's `onChange` suggestions. */
  private trackChanges(form: FormGroup, fields: FieldSpec[]): void {
    this.valueSub?.unsubscribe();
    let previous = form.getRawValue() as Record<string, unknown>;
    this.valueSub = form.valueChanges.subscribe(() => { previous = form.getRawValue(); this.changes.update((n) => n + 1); });
    for (const f of fields) {
      const control = f.onChange ? form.get(f.key) : null;
      if (!control) continue;
      this.valueSub.add(control.valueChanges.subscribe((v) => {
        const patch = f.onChange!(v, form.getRawValue(), previous);
        for (const [key, next] of Object.entries(patch ?? {})) {
          const target = form.get(key);
          if (target && target.enabled && target.value !== next) target.setValue(next);
        }
      }));
    }
    this.changes.update((n) => n + 1);
  }

  /** The edited record's current lookup values as options, and the first suggestions of a new record. */
  private loadLookups(fields: FieldSpec[], record: Record<string, unknown> | null): void {
    const options: Record<string, LookupOption[]> = {};
    for (const f of fields) {
      if (f.type !== 'lookup') continue;
      const current = record?.[f.key];
      if (record && current != null) options[f.key] = [{ value: String(current), label: f.currentLabel?.(record) ?? String(current) }];
    }
    this.lookupOptions.set(options);
    for (const f of fields) if (f.type === 'lookup' && !record) this.search(f, ''); // initial suggestions
  }

  private loadCustomFields(record: Record<string, unknown> | null, entityType: string | undefined): void {
    this.sections.set([]);
    this.customGroup.set(new FormGroup({}));
    if (!entityType) return;
    this.meta.sections(entityType).subscribe((sections) => {
      this.sections.set(sections);
      this.customGroup.set(buildCustomFieldsGroup(sections, (record?.['customFields'] ?? null) as Record<string, unknown> | null, !record));
    });
  }
}
