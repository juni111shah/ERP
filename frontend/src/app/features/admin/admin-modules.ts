import { WorkspaceModule } from '@core/api/modules.api';

/** Modules under their category ("Other" when they have none), in the order the server lists them. */
export function modulesByCategory(modules: WorkspaceModule[]): { category: string; modules: WorkspaceModule[] }[] {
  const byCategory = new Map<string, WorkspaceModule[]>();
  for (const m of modules) byCategory.set(m.category || 'Other', [...(byCategory.get(m.category || 'Other') ?? []), m]);
  return [...byCategory].map(([category, list]) => ({ category, modules: list }));
}

/** The names of the modules `module` needs switched on first (the key when a module is not listed). */
export function requiredModuleNames(module: WorkspaceModule, all: WorkspaceModule[]): string {
  return module.requires.map((key) => all.find((x) => x.key === key)?.name ?? key).join(', ');
}
