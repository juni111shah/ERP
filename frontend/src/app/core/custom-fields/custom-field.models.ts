/** Mirrors the API's CustomFieldDefinitionDto (enums are camelCase). */
export type CustomFieldDataType =
  | 'text' | 'number' | 'decimal' | 'date' | 'dateTime' | 'bool' | 'select' | 'multiSelect' | 'lookup';

/** The data types a custom field can have, with their names for the definition editor. */
export const CUSTOM_FIELD_TYPES: { value: CustomFieldDataType; label: string }[] = [
  { value: 'text', label: 'Text' }, { value: 'number', label: 'Whole number' }, { value: 'decimal', label: 'Decimal' },
  { value: 'date', label: 'Date' }, { value: 'dateTime', label: 'Date and time' }, { value: 'bool', label: 'Yes / no' },
  { value: 'select', label: 'Single choice' }, { value: 'multiSelect', label: 'Multiple choice' }, { value: 'lookup', label: 'Link to record' },
];

export interface CustomFieldOption { value: string; label?: string; isActive?: boolean; }

export interface CustomFieldOptions {
  choices?: CustomFieldOption[];
  minLength?: number; maxLength?: number; pattern?: string;
  min?: number; max?: number; scale?: number; maxSelections?: number; target?: string;
}

export interface CustomFieldDefinition {
  id: string;
  entityType: string;
  key: string;
  label: string;
  dataType: CustomFieldDataType;
  required: boolean;
  options?: CustomFieldOptions | null;
  order: number;
  section?: string | null;
  helpText?: string | null;
  isSearchable: boolean;
  isActive: boolean;
  moduleKey?: string | null;
  defaultValue?: unknown;
  legalEntityId?: string | null;
  showInList: boolean;
  sortable: boolean;
}

export type CustomFieldDefinitionInput = Omit<CustomFieldDefinition, 'id' | 'moduleKey'>;

/** Field metadata the renderer works with (shape used by custom-field-form.ts). */
export interface FormFieldMeta {
  key: string; label: string; dataType: CustomFieldDataType; required: boolean; defaultValue?: unknown;
  min?: number | null; max?: number | null; minLength?: number | null; maxLength?: number | null; pattern?: string | null;
  placeholder?: string | null; helpText?: string | null; options: CustomFieldOption[]; lookupEntityType?: string | null; order: number;
  scale?: number | null; showInList?: boolean;
}

export interface FormSectionMeta { key: string; label: string; order: number; fields: FormFieldMeta[]; }

export interface CustomizableEntityType { key: string; label: string; module: string; }

/** Groups active definitions into ordered form sections ("Additional information" when no section is set). */
export function toSections(defs: readonly CustomFieldDefinition[]): FormSectionMeta[] {
  const sections = new Map<string, FormSectionMeta>();
  for (const d of [...defs].filter((x) => x.isActive).sort((a, b) => a.order - b.order)) {
    const label = d.section?.trim() || 'Additional information';
    let section = sections.get(label);
    if (!section) {
      section = { key: label.toLowerCase().replace(/\W+/g, '-'), label, order: sections.size, fields: [] };
      sections.set(label, section);
    }
    section.fields.push(toFieldMeta(d));
  }
  return [...sections.values()];
}

/** One definition as the renderer's field metadata; inactive choices are left out. */
function toFieldMeta(d: CustomFieldDefinition): FormFieldMeta {
  const o = d.options;
  return {
    key: d.key, label: d.label, dataType: d.dataType, required: d.required, defaultValue: d.defaultValue,
    min: o?.min ?? null, max: o?.max ?? null, minLength: o?.minLength ?? null, maxLength: o?.maxLength ?? null,
    pattern: o?.pattern ?? null, helpText: d.helpText, options: (o?.choices ?? []).filter((c) => c.isActive !== false),
    lookupEntityType: o?.target ?? null, order: d.order, scale: o?.scale ?? null, showInList: d.showInList,
  };
}
