/** Mirrors the API's CustomFieldDefinitionDto (enums are camelCase). */
export type CustomFieldDataType =
  | 'text' | 'number' | 'decimal' | 'date' | 'dateTime' | 'bool' | 'select' | 'multiSelect' | 'lookup';

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
    if (!sections.has(label)) sections.set(label, { key: label.toLowerCase().replace(/\W+/g, '-'), label, order: sections.size, fields: [] });
    sections.get(label)!.fields.push({
      key: d.key, label: d.label, dataType: d.dataType, required: d.required, defaultValue: d.defaultValue,
      min: d.options?.min ?? null, max: d.options?.max ?? null, minLength: d.options?.minLength ?? null, maxLength: d.options?.maxLength ?? null,
      pattern: d.options?.pattern ?? null, helpText: d.helpText, options: (d.options?.choices ?? []).filter((c) => c.isActive !== false),
      lookupEntityType: d.options?.target ?? null, order: d.order, scale: d.options?.scale ?? null, showInList: d.showInList,
    });
  }
  return [...sections.values()];
}
