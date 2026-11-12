import { CustomFieldDefinition, CustomFieldDefinitionInput } from '@core/custom-fields/custom-field.models';

/** The raw value of the custom-field form. */
export interface CustomFieldFormValue {
  label: string; key: string; dataType: CustomFieldDefinition['dataType']; choices: string; section: string; helpText: string; order: number;
  required: boolean; showInList: boolean; isSearchable: boolean; isActive: boolean;
}

/** The data types whose values come from a list of choices. */
export function hasChoiceList(dataType: CustomFieldDefinition['dataType']): boolean {
  return dataType === 'select' || dataType === 'multiSelect';
}

/** The form's starting value: the field being edited, or a new active text field placed after the `count` existing ones. */
export function customFieldFormValue(d: CustomFieldDefinition | null, count: number): CustomFieldFormValue {
  return {
    label: d?.label ?? '', key: d?.key ?? '', dataType: d?.dataType ?? 'text',
    choices: (d?.options?.choices ?? []).map((c) => c.value).join('\n'), section: d?.section ?? '', helpText: d?.helpText ?? '',
    order: d?.order ?? count * 10, required: d?.required ?? false, showInList: d?.showInList ?? false,
    isSearchable: d?.isSearchable ?? false, isActive: d?.isActive ?? true,
  };
}

/** The key suggested for a label: "Delivery window" becomes "cf_delivery_window". */
export function suggestedCustomFieldKey(label: string): string {
  return 'cf_' + label.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '').slice(0, 60);
}

/** The form as a definition for `entityType`; the choices (one per line) are only sent for list types. */
export function toCustomFieldInput(v: CustomFieldFormValue, entityType: string, withChoices: boolean): CustomFieldDefinitionInput {
  const choices = v.choices.split('\n').map((s) => s.trim()).filter(Boolean).map((value) => ({ value, label: value }));
  return {
    entityType, key: v.key, label: v.label, dataType: v.dataType, required: v.required,
    options: withChoices ? { choices } : null, order: v.order, section: v.section || null, helpText: v.helpText || null,
    isSearchable: v.isSearchable, isActive: v.isActive, showInList: v.showInList, sortable: false, defaultValue: null, legalEntityId: null,
  };
}
