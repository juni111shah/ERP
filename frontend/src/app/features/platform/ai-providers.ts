import { AiProviderInput, AiProviderView, AssistantBot, AssistantConsoleView } from '@core/api/platform.api';
import { FieldSpec } from '@shared/components/record-drawer/record-drawer.component';

/** A ready-made AI service: the values it fills into the add form. */
export interface AiProviderPreset { value: string; label: string; name: string; baseUrl: string; model: string; }

/**
 * Ready-made addresses, so adding a service is mostly pasting a key. Every value stays editable, and "custom" covers any other
 * service that uses the same chat format. This is the one place in the code that names AI services.
 */
export const AI_PROVIDER_PRESETS: AiProviderPreset[] = [
  { value: 'groq', label: 'Groq', name: 'Groq', baseUrl: 'https://api.groq.com/openai/v1', model: 'openai/gpt-oss-120b' },
  { value: 'cloudflare', label: 'Cloudflare Workers AI', name: 'Cloudflare', baseUrl: 'https://api.cloudflare.com/client/v4/accounts/YOUR_ACCOUNT_ID/ai/v1', model: '@cf/google/gemma-4-26b-a4b-it' },
  { value: 'gemini', label: 'Google Gemini', name: 'Gemini', baseUrl: 'https://generativelanguage.googleapis.com/v1beta/openai', model: '' },
  { value: 'custom', label: 'Another service', name: '', baseUrl: '', model: '' },
];

/** The values a preset fills in, or null when the choice is not a preset. */
export function presetValues(value: unknown): Pick<AiProviderPreset, 'name' | 'baseUrl' | 'model'> | null {
  const preset = AI_PROVIDER_PRESETS.find((p) => p.value === value);
  return preset ? { name: preset.name, baseUrl: preset.baseUrl, model: preset.model } : null;
}

/** The list with one item moved up (-1) or down (+1). Moving past either end leaves it as it is. */
export function moved<T>(items: readonly T[], index: number, delta: -1 | 1): T[] {
  const list = [...items];
  const target = index + delta;
  if (index < 0 || index >= list.length || target < 0 || target >= list.length) return list;
  [list[index], list[target]] = [list[target], list[index]];
  return list;
}

/** An assistant is switched on with no service to answer for it, for example after a service was deleted. */
export function coverageWarnings(view: AssistantConsoleView): string[] {
  const warnings: string[] = [];
  if (view.brandEnabled && !view.providers.some((p) => p.useForBrand)) warnings.push('The public assistant is on, but no service is set to answer for it.');
  if (view.workspaceEnabled && !view.providers.some((p) => p.useForWorkspace)) warnings.push('The workspace assistant is on, but no service is cleared for client data.');
  return warnings;
}

/** Tokens one assistant has used today, across all its services. */
export function tokensUsedToday(view: AssistantConsoleView | null, bot: AssistantBot): number {
  return (view?.usageToday ?? []).filter((u) => u.bot === bot).reduce((sum, u) => sum + u.inputTokens + u.outputTokens, 0);
}

const COMMON_FIELDS: FieldSpec[] = [
  { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 80, hint: 'Only you see this.' },
  { key: 'baseUrl', label: 'Address', type: 'text', required: true, maxLength: 300, hint: 'The part before /chat/completions. It must start with https://.' },
  { key: 'model', label: 'Model', type: 'text', required: true, maxLength: 150, hint: "Copy the exact name from the service's model list." },
];
const USE_FIELDS: FieldSpec[] = [
  { key: 'useForBrand', label: 'Use for the public assistant', type: 'switch', default: true, hint: 'Visitor questions are sent to this service.' },
  { key: 'useForWorkspace', label: 'Clear for client data', type: 'switch', default: false, hint: "The workspace assistant sends client data to this service. Read its data terms first, and leave this off for free plans that may keep or review what you send." },
];

/** The fields of the add-a-service drawer: a preset fills in the rest, and the key is required. */
export const AI_PROVIDER_ADD_FIELDS: FieldSpec[] = [
  {
    key: 'preset', label: 'Service', type: 'select', default: 'groq', options: AI_PROVIDER_PRESETS.map((p) => ({ value: p.value, label: p.label })),
    hint: 'Fills in the details below. Any service that uses the same chat format works.',
    onChange: (value) => presetValues(value),
  },
  { ...COMMON_FIELDS[0], default: presetValues('groq')!.name },
  { ...COMMON_FIELDS[1], default: presetValues('groq')!.baseUrl },
  { ...COMMON_FIELDS[2], default: presetValues('groq')!.model },
  { key: 'apiKey', label: 'API key', type: 'password', required: true, hint: 'It is stored encrypted and is never shown again.' },
  ...USE_FIELDS,
];

/** The fields of the edit-a-service drawer: an empty key keeps the stored one. */
export const AI_PROVIDER_EDIT_FIELDS: FieldSpec[] = [
  ...COMMON_FIELDS,
  { key: 'apiKey', label: 'New API key', type: 'password', hint: 'Leave empty to keep the current key. If you change the address, you must paste the key again.' },
  ...USE_FIELDS,
];

/** A service as the edit drawer shows it (the key is never sent back). */
export function providerDrawerValue(p: AiProviderView): Record<string, unknown> {
  return { name: p.name, baseUrl: p.baseUrl, model: p.model, useForBrand: p.useForBrand, useForWorkspace: p.useForWorkspace };
}

/** The add or edit drawer as a service; an empty key is sent as null. */
export function toAiProviderInput(v: Record<string, unknown>): AiProviderInput {
  return {
    name: v['name'] as string, baseUrl: v['baseUrl'] as string, model: v['model'] as string, apiKey: (v['apiKey'] as string | null) || null,
    useForBrand: !!v['useForBrand'], useForWorkspace: !!v['useForWorkspace'],
  };
}
