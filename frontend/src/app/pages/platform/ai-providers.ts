import { AssistantConsoleView } from '../../core/api/platform.api';

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
