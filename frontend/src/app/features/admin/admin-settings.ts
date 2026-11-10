import { SettingView } from '@core/api/admin.api';
import { displayLabel } from '@core/format/display';

const MODULE_NAMES: Record<string, string> = { hr: 'HR and payroll', realestate: 'Real estate', crm: 'CRM', pos: 'Point of sale' };

/** A settings group's heading: "General" for core, the module's name otherwise. */
export function settingsModuleName(module: string): string {
  return module === 'core' ? 'General' : MODULE_NAMES[module] ?? displayLabel(module);
}

/** Settings under their module, in the order the server lists them. JSON settings have their own editors elsewhere. */
export function groupSettings(settings: SettingView[]): { module: string; items: SettingView[] }[] {
  const byModule = new Map<string, SettingView[]>();
  for (const s of settings.filter((x) => x.type !== 'json')) byModule.set(s.module, [...(byModule.get(s.module) ?? []), s]);
  return [...byModule].map(([module, items]) => ({ module, items }));
}

/** The values in `draft` that differ from `original`. */
export function changedValues(draft: Record<string, unknown>, original: Record<string, unknown>): Record<string, unknown> {
  const changed: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(draft)) if (v !== original[k]) changed[k] = v;
  return changed;
}
