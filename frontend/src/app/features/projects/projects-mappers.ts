import { ProjectDto, ProjectInput } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';

/**
 * The body that updates a saved project: what the form sent, over the fields the form does not show.
 * The typed manager name goes with the chosen account; without one the old typed name stays.
 */
export function mergeProjectInput(current: ProjectDto, body: Partial<ProjectInput>): ProjectInput {
  const merged = {
    legalEntityId: current.legalEntityId, status: current.status, taxCodeId: current.taxCodeId ?? null, sourceType: current.sourceType ?? null, sourceId: current.sourceId ?? null,
    isTemplate: current.isTemplate, customFields: current.customFields ?? null, ...body,
  } as ProjectInput;
  if (!merged.managerUserId) merged.managerName = current.managerUserId ? null : current.managerName ?? null;
  return merged;
}

/** A copy of a drawer's values with the named date fields written as API dates (the local calendar day). */
export function withIsoDates(body: unknown, ...keys: string[]): Record<string, unknown> {
  const copy = { ...(body as Record<string, unknown>) };
  for (const key of keys) {
    const value = copy[key];
    if (value instanceof Date) copy[key] = toIsoDate(value);
  }
  return copy;
}
